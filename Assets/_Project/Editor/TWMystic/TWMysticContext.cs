using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace IslandV2
{
    internal sealed class TWMysticContext
    {
        public struct Face { public Vector3 a,b,c; }
        public Transform pad,manual,generated,root,gate;
        public LandmarkTree elder;
        public MysticArtState state;
        public ResourceItem[] resources;
        public Vector3 center;
        public Quaternion rotation;
        public readonly List<Face> faces=new List<Face>();
        public readonly Dictionary<string,MysticProp> keep=new Dictionary<string,MysticProp>();
        public readonly Dictionary<string,Vector2> sites=new Dictionary<string,Vector2>();
        readonly Dictionary<Vector2Int,List<Face>> surface=new Dictionary<Vector2Int,List<Face>>();
        readonly HashSet<Vector2Int> reachable=new HashSet<Vector2Int>();
        struct Space { public Vector2 point; public float radius; }
        readonly List<Space> spaces=new List<Space>();
        public float groundRadiusX=18f,groundRadiusZ=17f,zoneRadius=18f;
        public Vector3 groupOrigin;
        static Vector2Int Cell(Vector3 p){return new Vector2Int(Mathf.FloorToInt(p.x/3f),Mathf.FloorToInt(p.z/3f));}
        public Vector3 World(Vector2 p){return center+rotation*new Vector3(p.x,0,p.y);}
        public Vector3 World(Vector3 p){return center+rotation*p;}
        public Vector2 Local(Vector3 p){Vector3 q=Quaternion.Inverse(rotation)*(p-center);return new Vector2(q.x,q.z);}
        public Vector3 Ground(Vector2 p){Vector3 w=World(p);w.y=Height(w);return w;}
        public bool TryGround(Vector2 p,out Vector3 world)
        {
            world=World(p);if(!TryHeight(world,out float height))return false;world.y=height;return true;
        }
        public bool Supported(Vector2 p,float radius)
        {
            if(!TryGround(p,out var centerPoint))return false;
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;Vector2 offset=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                if(!TryGround(p+offset,out var edge) || Mathf.Abs(edge.y-centerPoint.y)>0.65f)return false;
            }
            return true;
        }
        public Vector2 Site(string key,Vector2 preferred){return sites.TryGetValue(key,out var point)?point:preferred;}
        public bool Occupied(Vector2 point,float padding)
        {foreach(var space in spaces)if(Vector2.Distance(point,space.point)<space.radius+padding)return true;return false;}
        public Vector2 SafeTreePoint(Vector2 planned)
        {
            return Place("Tree_Fallback_"+sites.Count,planned,9,24,1.1f,1.85f);
        }
        void ReachableSurface()
        {
            reachable.Clear();var heights=new Dictionary<Vector2Int,float>();
            Vector2Int seed=Vector2Int.zero;float best=float.PositiveInfinity;
            for(int z=-29;z<=29;z++)for(int x=-29;x<=29;x++)
            {
                var key=new Vector2Int(x,z);Vector2 point=new Vector2(x,z);
                if(!Supported(point,0.35f) || !TryGround(point,out var world))continue;
                heights[key]=world.y;
                if(point.sqrMagnitude<best){best=point.sqrMagnitude;seed=key;}
            }
            if(heights.Count==0 || best>8*8)throw new InvalidOperationException("У Дедушки не найдена связная поверхность Ground_Chunk_*. Проверь положение метки и исходные меши земли.");
            var queue=new Queue<Vector2Int>();queue.Enqueue(seed);reachable.Add(seed);
            var steps=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
            while(queue.Count>0)
            {
                var p=queue.Dequeue();
                foreach(var step in steps)
                {
                    var next=p+step;
                    if(reachable.Contains(next) || !heights.TryGetValue(next,out float y) || Mathf.Abs(y-heights[p])>0.75f)continue;
                    reachable.Add(next);queue.Enqueue(next);
                }
            }
            if(!TryGround(Vector2.zero,out groupOrigin))TryGround(new Vector2(seed.x,seed.y),out groupOrigin);
            groupOrigin.x=center.x;groupOrigin.z=center.z;
        }
        bool Available(Vector2 p,float footprint,float radius,bool allowPath)
        {
            if(!reachable.Contains(new Vector2Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y))))return false;
            if(!allowPath && p.y>4.3f && Mathf.Abs(p.x)<1.35f+radius)return false;
            foreach(var space in spaces)if(Vector2.Distance(p,space.point)<radius+space.radius+0.4f)return false;
            return Supported(p,footprint);
        }
        Vector2 Place(string key,Vector2 preferred,float min,float max,float footprint,float radius,bool allowPath=false)
        {
            if(keep.TryGetValue(key,out var fixedProp))
            {
                Vector2 point=Local(fixedProp.transform.position);
                if(!Available(point,footprint,radius,allowPath))throw new InvalidOperationException("Перемещённый объект «"+fixedProp.name+"» вне доступной суши или перекрывает подход. Сдвинь его на землю и повтори сборку.");
                sites[key]=point;spaces.Add(new Space{point=point,radius=radius});return point;
            }
            Vector2 chosen=preferred;float cost=float.PositiveInfinity;
            void Consider(Vector2 p)
            {
                if(p.magnitude<min || p.magnitude>max)return;
                float c=(p-preferred).sqrMagnitude;
                if(c>=cost || !Available(p,footprint,radius,allowPath))return;
                chosen=p;cost=c;
            }
            Consider(preferred);
            if(cost>0.001f)
            {
                foreach(var resource in resources??Array.Empty<ResourceItem>())if(resource!=null)Consider(Local(resource.transform.position));
                float start=Mathf.Atan2(preferred.x,preferred.y)*Mathf.Rad2Deg;
                for(float r=min;r<=max;r+=0.7f)for(int angle=0;angle<360;angle+=6)Consider(Ring(start+angle,r));
            }
            if(float.IsPositiveInfinity(cost))throw new InvalidOperationException("Не найдено свободное место на связной суше для «"+key+"». Подходы и расстояния сохраняются; сцена не изменена.");
            sites[key]=chosen;spaces.Add(new Space{point=chosen,radius=radius});return chosen;
        }
        public void PlanLayout()
        {
            sites.Clear();spaces.Clear();ReachableSurface();
            if(state!=null && state.art!=null)
                foreach(var prop in state.art.GetComponentsInChildren<MysticProp>(true))
                    if(Vector3.Distance(prop.transform.position,prop.defaultPosition)>0.001f || Quaternion.Angle(prop.transform.rotation,prop.defaultRotation)>0.05f ||
                        Vector3.Distance(prop.transform.lossyScale,prop.defaultScale)>0.001f || !prop.gameObject.activeSelf)keep[prop.key]=prop;
            for(int i=0;i<6;i++)Place("Bench_"+i,Ring(42+i*55.2f,7.2f),5.2f,11.6f,1.6f,1.7f);
            for(int i=0;i<6;i++)Place("Tree_"+i,Ring(36+i*57f,i%2==0?13.5f:14.2f),9,24,1.1f,1.85f);
            for(int i=0;i<3;i++)Place("Witness_"+i,Ring(new[]{77f,188f,282f}[i],10.1f),8.5f,20,0.5f,0.8f);
            Place("Ribbon_Memory",new Vector2(-2.7f,12.6f),4.7f,20,0.5f,0.6f,true);
            Place("Gunnar_Memory",new Vector2(4.2f,4.1f),4.7f,13,0.5f,0.6f,true);
            Vector2 bench=sites["Bench_4"];Place("Bench_Memory",bench+bench.normalized*2.7f,5,21,0.5f,0.6f,true);
            groundRadiusX=18;groundRadiusZ=17;zoneRadius=18;
            foreach(var p in sites.Values){groundRadiusX=Mathf.Max(groundRadiusX,Mathf.Abs(p.x)+2);groundRadiusZ=Mathf.Max(groundRadiusZ,Mathf.Abs(p.y)+2);zoneRadius=Mathf.Max(zoneRadius,p.magnitude+2.5f);}
            float scale=1;
            foreach(var p in sites.Values)scale=Mathf.Max(scale,new Vector2(p.x/groundRadiusX,p.y/groundRadiusZ).magnitude+0.04f);
            groundRadiusX*=scale;groundRadiusZ*=scale;
            Vector2 entry=gate!=null?Local(gate.position):new Vector2(0,18);
            if(keep.TryGetValue("Entrance",out var keptGate))sites["Entrance"]=Local(keptGate.transform.position);
            else
            {
                Vector2 selected=entry;float score=float.PositiveInfinity;
                for(float z=-6;z<=6;z+=0.5f)for(float x=-6;x<=6;x+=0.5f)
                {
                    Vector2 p=entry+new Vector2(x,z);float cost=(p-entry).sqrMagnitude;if(cost>=score || !TryGround(p,out var ground))continue;
                    Vector2 tangent=p.sqrMagnitude>0.01f?new Vector2(p.y,-p.x).normalized:Vector2.right;
                    Vector2 left=p+tangent*2.55f,right=p-tangent*2.55f;
                    if((left.y>4.3f && left.y<zoneRadius && Mathf.Abs(left.x)<1f) || (right.y>4.3f && right.y<zoneRadius && Mathf.Abs(right.x)<1f))continue;
                    if(!Supported(p+tangent*2.55f,0.3f) || !Supported(p-tangent*2.55f,0.3f))continue;
                    selected=p;score=cost;
                }
                if(float.IsPositiveInfinity(score))throw new InvalidOperationException("Не найдено место для стоек арки возле ForestGate.");sites["Entrance"]=selected;
            }
        }
        public Vector3 InscriptionPoint(Vector2 preferred)
        {
            if(Supported(preferred,0.55f))return Ground(preferred);
            Vector2 selected=preferred;float score=float.PositiveInfinity;
            for(float r=4.5f;r<=11;r+=0.6f)for(int angle=0;angle<360;angle+=8)
            {
                Vector2 p=Ring(angle,r);float c=(p-preferred).sqrMagnitude;if(c>=score || !Supported(p,0.55f))continue;selected=p;score=c;
            }
            if(float.IsPositiveInfinity(score))throw new InvalidOperationException("Не найдено место на земле для надписи.");return Ground(selected);
        }
        internal static void SurfaceTests()
        {
            // No scene objects: the real planner runs against synthetic native-ground triangles.
            TWMysticContext Make(bool gap)
            {
                var x=new TWMysticContext{center=new Vector3(41,3,-17),rotation=Quaternion.Euler(0,37,0),resources=Array.Empty<ResourceItem>()};
                void Rectangle(float left,float bottom,float right,float top)
                {
                    Vector3 a=x.World(new Vector2(left,bottom)),b=x.World(new Vector2(left,top)),c=x.World(new Vector2(right,top)),d=x.World(new Vector2(right,bottom));
                    x.faces.Add(new Face{a=a,b=b,c=c});x.faces.Add(new Face{a=a,b=c,c=d});
                }
                if(!gap)Rectangle(-25,-1,25,31);
                else{Rectangle(-25,-1,-2.5f,31);Rectangle(2.5f,-1,25,31);Rectangle(-2.5f,-1,2.5f,7);Rectangle(-2.5f,14,2.5f,31);}
                x.IndexSurface();return x;
            }
            foreach(bool gap in new[]{false,true})
            {
                var x=Make(gap);x.PlanLayout();
                for(int i=0;i<6;i++)
                {
                    Vector2 tree=x.sites["Tree_"+i],bench=x.sites["Bench_"+i];
                    if(!x.Supported(tree,1.1f) || !x.Supported(bench,1.6f))throw new InvalidOperationException("Тест: предмет над берегом или разрывом поверхности.");
                    if(tree.y>4.3f && Mathf.Abs(tree.x)<3.19f)throw new InvalidOperationException("Тест: дерево перекрыло проход.");
                    for(int j=0;j<i;j++)if(Vector2.Distance(tree,x.sites["Tree_"+j])<4.09f)throw new InvalidOperationException("Тест: деревья наложились.");
                    Vector3 world=x.Ground(tree);
                    if(Mathf.Abs(world.y-3)>0.001f)throw new InvalidOperationException("Тест: потеряна высота или трансформация земли.");
                }
                if(x.sites["Tree_2"].y<0)throw new InvalidOperationException("Тест: не сработал перенос дерева с отсутствующей половины кольца.");
                if(x.groundRadiusX<18 || x.zoneRadius<18)throw new InvalidOperationException("Тест: неверный охват адаптивной зоны.");
            }
        }
        public float Height(Vector3 p)
        {
            if(TryHeight(p,out float height))return height;
            throw new InvalidOperationException("Нет земли в точке "+p+". География острова не меняется, выбери место внутри суши.");
        }
        bool TryHeight(Vector3 p,out float height)
        {
            height=0;IEnumerable<Face> candidates=faces;
            if(surface.Count>0){if(!surface.TryGetValue(Cell(p),out var bucket))return false;candidates=bucket;}
            foreach(var f in candidates)
            {
                float d=(f.b.z-f.c.z)*(f.a.x-f.c.x)+(f.c.x-f.b.x)*(f.a.z-f.c.z);
                if(Mathf.Abs(d)<0.000001f)continue;
                float u=((f.b.z-f.c.z)*(p.x-f.c.x)+(f.c.x-f.b.x)*(p.z-f.c.z))/d;
                float v=((f.c.z-f.a.z)*(p.x-f.c.x)+(f.a.x-f.c.x)*(p.z-f.c.z))/d;
                if(u>=-0.0001f && v>=-0.0001f && u+v<=1.0001f){height=u*f.a.y+v*f.b.y+(1-u-v)*f.c.y;return true;}
            }
            return false;
        }
        public void IndexSurface()
        {
            surface.Clear();
            foreach(var f in faces)
            {
                var lo=Cell(Vector3.Min(f.a,Vector3.Min(f.b,f.c)));var hi=Cell(Vector3.Max(f.a,Vector3.Max(f.b,f.c)));
                for(int z=lo.y;z<=hi.y;z++)for(int x=lo.x;x<=hi.x;x++)
                {
                    var key=new Vector2Int(x,z);if(!surface.TryGetValue(key,out var bucket)){bucket=new List<Face>();surface.Add(key,bucket);}bucket.Add(f);
                }
            }
        }
        public void ReadGround()
        {
            foreach(var f in generated.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!f.name.StartsWith(IslandBlockout.ChunkPrefix,StringComparison.Ordinal)||f.sharedMesh==null)continue;
                var verts=f.sharedMesh.vertices;var t=f.sharedMesh.triangles;
                for(int i=0;i<t.Length;i+=3)
                {
                    Vector3 a=f.transform.TransformPoint(verts[t[i]]),b=f.transform.TransformPoint(verts[t[i+1]]),c=f.transform.TransformPoint(verts[t[i+2]]);
                    if(Mathf.Min(a.x,Mathf.Min(b.x,c.x))>center.x+34 || Mathf.Max(a.x,Mathf.Max(b.x,c.x))<center.x-34 ||
                        Mathf.Min(a.z,Mathf.Min(b.z,c.z))>center.z+34 || Mathf.Max(a.z,Mathf.Max(b.z,c.z))<center.z-34)continue;
                    faces.Add(new Face{a=a,b=b,c=c});
                }
            }
            if(faces.Count==0)throw new InvalidOperationException("Не найдены исходные Ground_Chunk_*.");
            IndexSurface();
        }
        public GameObject Prop(TWMysticAssets assets,TWMysticModel model,string key,string name,Vector2 p,float yaw=0,bool glow=false)
        {
            if(keep.TryGetValue(key,out var old))
            {Undo.SetTransformParent(old.transform,root,"Keep authored mystic prop");return old.gameObject;}
            p=Site(key,p);
            Vector3 position=key=="Circle_Stones" || key=="Ties" || key.StartsWith("Understory_",StringComparison.Ordinal)?groupOrigin:Ground(p);
            var go=model.Finish(assets,root,key,position,rotation*Quaternion.Euler(0,yaw,0));go.name=name;
            var prop=go.AddComponent<MysticProp>();prop.key=key;prop.description="Отдельная часть Круга молчания. Перемещай и вращай целиком.";
            prop.defaultPosition=go.transform.position;prop.defaultRotation=go.transform.rotation;prop.defaultScale=go.transform.lossyScale;
            if(glow)go.GetComponent<Renderer>().sharedMaterial=assets.veil;
            return go;
        }
        public static Vector2 Ring(float angle,float r){float a=angle*Mathf.Deg2Rad;return new Vector2(Mathf.Sin(a)*r,Mathf.Cos(a)*r);}
    }
}
