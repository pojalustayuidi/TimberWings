using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    internal static class TWMysticGround
    {
        // Real terrain triangles, clipped and feathered; no floating ground quad.
        public static void Build(TWMysticContext x,TWMysticAssets assets,Transform parent,string key)
        {
            const int sides=48;var edge=new Vector2[sides];
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides;
                Vector3 w=x.World(new Vector3(Mathf.Cos(a)*x.groundRadiusX,0,Mathf.Sin(a)*x.groundRadiusZ));edge[i]=new Vector2(w.x,w.z);
            }
            var mesh=new Mesh{name=key};var v=new List<Vector3>();var colours=new List<Color>();var t=new List<int>();
            foreach(var f in x.faces)
            {
                var polygon=new List<Vector3>{f.a,f.b,f.c};
                for(int i=0;i<sides && polygon.Count>0;i++)polygon=Clip(polygon,edge[i],edge[(i+1)%sides]);
                for(int i=1;i+1<polygon.Count;i++)Add(polygon[0],polygon[i],polygon[i+1],0);
            }
            if(v.Count>65000)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);mesh.SetColors(colours);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(IslandBlockout.MaterialPath)){name=key};
            AssetDatabase.CreateAsset(material,assets.folder+"/"+key+".mat");
            var go=assets.Object(parent,key,assets.Save(mesh,key),new[]{material},false);
            go.AddComponent<MeshCollider>().sharedMesh=mesh;

            void Add(Vector3 a,Vector3 b,Vector3 c,int depth)
            {
                Vector3 normal=Vector3.Cross(b-a,c-a);if(normal.sqrMagnitude<1e-12f)return;
                if(depth<6 && Mathf.Max((b-a).sqrMagnitude,Mathf.Max((c-b).sqrMagnitude,(a-c).sqrMagnitude))>0.6f*0.6f)
                {
                    Vector3 ab=(a+b)*0.5f,bc=(b+c)*0.5f,ca=(c+a)*0.5f;
                    Add(a,ab,ca,depth+1);Add(ab,b,bc,depth+1);Add(ca,bc,c,depth+1);Add(ab,bc,ca,depth+1);return;
                }
                if(normal.y<0){Vector3 tmp=b;b=c;c=tmp;}
                int start=v.Count;
                foreach(var p in new[]{a,b,c})
                {
                    v.Add(parent.InverseTransformPoint(p+Vector3.up*0.014f));
                    Vector3 local=Quaternion.Inverse(x.rotation)*(p-x.center);
                    float r=new Vector2(local.x/x.groundRadiusX,local.z/x.groundRadiusZ).magnitude;
                    float distance=new Vector2(local.x,local.z).magnitude;
                    float ring=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Abs(distance-7.2f)/1.7f));
                    float entrance=local.z>0?1-Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Abs(local.x)/2.0f)):0;
                    float wear=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.80f,1f,r)))*(0.25f+0.7f*Mathf.Max(ring,entrance));
                    Color colour=Color.Lerp(new Color(0.57f,0.52f,0.39f),new Color(0.73f,0.66f,0.49f),Mathf.PerlinNoise(p.x*0.6f,p.z*0.6f)).linear;
                    colour.a=wear*0.65f;colours.Add(colour);
                }
                t.Add(start);t.Add(start+1);t.Add(start+2);
            }
        }
        static List<Vector3> Clip(List<Vector3> p,Vector2 a,Vector2 b)
        {
            var result=new List<Vector3>();Vector2 edge=b-a;
            float Side(Vector3 v){return edge.x*(v.z-a.y)-edge.y*(v.x-a.x);}
            for(int i=0;i<p.Count;i++)
            {
                Vector3 u=p[i],v=p[(i+1)%p.Count];float x=Side(u),y=Side(v);bool inside=x>=0,other=y>=0;
                if(inside)result.Add(u);if(inside!=other)result.Add(Vector3.Lerp(u,v,x/(x-y)));
            }
            return result;
        }
    }
}

