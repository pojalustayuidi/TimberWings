using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_6000_0_OR_NEWER
using SurfaceMaterial=UnityEngine.PhysicsMaterial;
#else
using SurfaceMaterial=UnityEngine.PhysicMaterial;
#endif

namespace IslandV2
{
    internal static class TWWorkyardPhysics
    {
        const string CollisionName="TW_Collision";

        public static GameObject Build(TWWorkyardContext x,string folder)
        {
            var material=new SurfaceMaterial("Workyard_Slide"){dynamicFriction=0,staticFriction=0,bounciness=0};
#if UNITY_6000_0_OR_NEWER
            material.frictionCombine=PhysicsMaterialCombine.Minimum;
#else
            material.frictionCombine=PhysicMaterialCombine.Minimum;
#endif
            AssetDatabase.CreateAsset(material,folder+"/Workyard_Slide.physicMaterial");

            // The floor is a sibling of Stage1/Stage2. Their visibility and build-scale animation cannot remove it.
            var root=Child(x.tent.transform,"TW_WORKYARD_PHYSICS");
            Vector3 scale=x.tent.transform.lossyScale;
            if(Mathf.Min(scale.x,Mathf.Min(scale.y,scale.z))<0.001f)throw new InvalidOperationException("Недопустимый масштаб палатки.");
            root.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            var ground=x.built.GetComponentsInChildren<MeshFilter>(true).SingleOrDefault(f=>f.name=="Built_Ground");
            if(ground==null || ground.sharedMesh==null)throw new InvalidOperationException("Нет поверхности Built_Ground для опоры двора.");
            root.layer=ground.gameObject.layer;
            Mesh mesh=UnityEngine.Object.Instantiate(ground.sharedMesh);mesh.name="Workyard_Support";
            var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++)
                vertices[i]=root.transform.InverseTransformPoint(ground.transform.TransformPoint(vertices[i])-Vector3.up*0.014f);
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            TWWorkyardValidation.Mesh(mesh);AssetDatabase.CreateAsset(mesh,folder+"/Workyard_Support.asset");
            var support=root.AddComponent<MeshCollider>();support.sharedMaterial=material;
            support.cookingOptions=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.EnableMeshCleaning|
                MeshColliderCookingOptions.WeldColocatedVertices|MeshColliderCookingOptions.UseFastMidphase;
            support.sharedMesh=mesh;

            Mesh deck=null;
            foreach(var prop in new[]{x.site,x.built}.SelectMany(t=>t.GetComponentsInChildren<WorkyardProp>(true)))
            {
                RemoveGenerated(prop.transform);
                foreach(var tree in prop.GetComponentsInChildren<WorkyardTree>(true))RemoveGenerated(tree.transform);
                var go=prop.gameObject;
                if(prop.key=="Built_Canopy" || prop.key=="Site_Frame")
                {
                    foreach(int side in new[]{-1,1})foreach(int z in new[]{-1,1})
                        Capsule(go,new Vector3(side*2,1.05f,z*1.24f),0.11f,2.3f,1,material);
                    if(prop.key=="Built_Canopy")
                    {
                        if(deck==null){deck=DeckMesh();TWWorkyardValidation.Mesh(deck);AssetDatabase.CreateAsset(deck,folder+"/Workyard_Deck.asset");}
                        var c=Holder(go).AddComponent<MeshCollider>();c.sharedMesh=deck;c.sharedMaterial=material;
                    }
                }
                var station=prop.GetComponent<WorkyardStation>();
                if(station!=null)
                {
                    if(station.role==WorkerRole.Lumberjack && station.tree!=null)
                        Capsule(station.tree.gameObject,new Vector3(0,0.16f,0),0.12f,0.35f,1,material);
                    else if(station.role!=WorkerRole.Lumberjack)
                    {
                        float z=station.role==WorkerRole.Sawyer?0.55f:0.60f;
                        foreach(int side in new[]{-1,1})Capsule(go,new Vector3(side*0.21f,0.34f,z),0.235f,0.74f,1,material,false);
                        Obstacle(Holder(go),new Vector3(0,0.34f,z),new Vector3(0.9f,0.74f,0.48f));
                    }
                }
                if(prop.key=="Warehouse")
                {
                    Capsule(go,new Vector3(0,0.63f,0),0.63f,2.7f,0,material,false);
                    Obstacle(Holder(go),new Vector3(0,0.63f,0),new Vector3(2.7f,1.26f,1.26f));
                    foreach(var spot in prop.GetComponentsInChildren<WorkyardWorkSpot>(true))
                        if(spot.prompt=="склад артели: забрать или продать")
                        {Undo.RecordObject(spot,"Update warehouse action text");spot.prompt="открыть склад артели";}
                }
            }
            return root;
        }

        static GameObject Child(Transform parent,string name)
        {
            var go=new GameObject(name);go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
            Undo.RegisterCreatedObjectUndo(go,"Workyard collision");return go;
        }
        static GameObject Holder(GameObject go)
        {
            var t=go.transform.Find(CollisionName);return t!=null?t.gameObject:Child(go.transform,CollisionName);
        }
        static void RemoveGenerated(Transform parent)
        {
            var old=new List<GameObject>();
            foreach(Transform child in parent)
            {
                bool legacy=child.name.StartsWith("Collision_",StringComparison.Ordinal) && int.TryParse(child.name.Substring(10),out _);
                if(child.name!=CollisionName && !legacy)continue;
                // Generated physics holders contain no authored model, gameplay component or user's nested object.
                if(child.Cast<Transform>().Any(t=>child.name!=CollisionName || t.name!="Nav" || t.childCount!=0) ||
                    child.GetComponentsInChildren<Component>(true).Any(c=>!(c is Transform)&&!(c is Collider)&&!(c is NavMeshObstacle)))
                    throw new InvalidOperationException("В служебный коллайдер добавлены свои объекты: "+child.name+". Перенеси их рядом и повтори.");
                old.Add(child.gameObject);
            }
            foreach(var go in old)Undo.DestroyObjectImmediate(go);
        }
        static void Capsule(GameObject go,Vector3 center,float radius,float height,int direction,SurfaceMaterial material,bool obstacle=true)
        {
            var holder=Holder(go);var c=holder.AddComponent<CapsuleCollider>();c.center=center;c.radius=radius;c.height=height;c.direction=direction;
            c.sharedMaterial=material;
            if(obstacle)Obstacle(holder,center,new Vector3(radius*2,height,radius*2));
        }
        static void Obstacle(GameObject go,Vector3 center,Vector3 size)
        {
            // One object per obstacle: Unity allows a single NavMeshObstacle on a GameObject.
            var n=Child(go.transform,"Nav");var obstacle=n.AddComponent<NavMeshObstacle>();
            obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=center;obstacle.size=size;obstacle.carving=true;
        }
        static Mesh DeckMesh()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            Vector3[] top={new Vector3(-2.01f,0.14f,-1.3f),new Vector3(-2.01f,0.14f,1.3f),new Vector3(2.01f,0.14f,1.3f),new Vector3(2.01f,0.14f,-1.3f)};
            Vector3[] edge={new Vector3(-2.43f,-0.015f,-1.72f),new Vector3(-2.43f,-0.015f,1.72f),new Vector3(2.43f,-0.015f,1.72f),new Vector3(2.43f,-0.015f,-1.72f)};
            Quad(top[0],top[1],top[2],top[3]);
            for(int i=0;i<4;i++){int next=(i+1)%4;Quad(edge[i],edge[next],top[next],top[i]);}
            var mesh=new Mesh{name="Workyard_Deck"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                if(Vector3.Cross(b-a,c-a).y<0){Vector3 tmp=b;b=d;d=tmp;}
                int start=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
        }
    }
}
