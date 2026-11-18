using System;
using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    internal sealed class TWWorkyardContext
    {
        public struct Face{public Vector3 a,b,c;}
        public TentStages tent;
        public BuildingController build;
        public WorkerCamp camp;
        public WorkerManager manager;
        public WorkyardArtState previous;
        public Transform groundRoot,site,built;
        public readonly List<Face> faces=new List<Face>();
        public readonly Dictionary<string,WorkyardProp> keep=new Dictionary<string,WorkyardProp>();
        public readonly List<Vector2> resourceTrees=new List<Vector2>();
        public Vector3 origin;
        public Quaternion rotation;

        public Vector3 World(Vector3 local){return origin+rotation*local;}
        public Vector3 Ground(Vector2 local)
        {
            Vector3 w=World(new Vector3(local.x,0,local.y));
            return new Vector3(local.x,Height(new Vector2(w.x,w.z))-origin.y,local.y);
        }
        public float Height(Vector2 p)
        {
            if (TryHeight(faces, p, out float height)) return height;
            throw new InvalidOperationException("Нет поверхности земли у палатки: "+p);
        }
        public static bool TryHeight(IReadOnlyList<Face> surface, Vector2 p, out float height)
        {
            foreach(var f in surface)
            {
                float d=(f.b.z-f.c.z)*(f.a.x-f.c.x)+(f.c.x-f.b.x)*(f.a.z-f.c.z);
                if(Mathf.Abs(d)<0.000001f)continue;
                float u=((f.b.z-f.c.z)*(p.x-f.c.x)+(f.c.x-f.b.x)*(p.y-f.c.z))/d;
                float v=((f.c.z-f.a.z)*(p.x-f.c.x)+(f.a.x-f.c.x)*(p.y-f.c.z))/d;
                if(u>=-0.0001f && v>=-0.0001f && u+v<=1.0001f)
                {
                    height = u*f.a.y+v*f.b.y+(1-u-v)*f.c.y;
                    return true;
                }
            }
            height = 0f;
            return false;
        }
        public void ReadGround()
        {
            float reach = TWWorkyardGround.SupportRadius + 0.5f;
            foreach(var filter in groundRoot.GetComponentsInChildren<MeshFilter>())
            {
                if(!filter.name.StartsWith(IslandBlockout.ChunkPrefix,StringComparison.Ordinal)||filter.sharedMesh==null ||
                    !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)continue;
                var v=filter.sharedMesh.vertices;var t=filter.sharedMesh.triangles;var m=filter.transform.localToWorldMatrix;
                for(int i=0;i<t.Length;i+=3)
                {
                    Vector3 a=m.MultiplyPoint3x4(v[t[i]]),b=m.MultiplyPoint3x4(v[t[i+1]]),c=m.MultiplyPoint3x4(v[t[i+2]]);
                    if(Mathf.Min(a.x,Mathf.Min(b.x,c.x))>origin.x+reach || Mathf.Max(a.x,Mathf.Max(b.x,c.x))<origin.x-reach ||
                        Mathf.Min(a.z,Mathf.Min(b.z,c.z))>origin.z+reach || Mathf.Max(a.z,Mathf.Max(b.z,c.z))<origin.z-reach)continue;
                    faces.Add(new Face{a=a,b=b,c=c});
                }
            }
            if(faces.Count==0)throw new InvalidOperationException("Не найдена земля Ground_Chunk_* у PAD_Tent.");
        }
        public GameObject Finish(TWWorkyardAssets a,Transform parent,TWWorkyardModel model,string key,string name,Vector2 position,float yaw=0,bool keep=true)
        {
            if(keep && this.keep.TryGetValue(key,out WorkyardProp existing))
            {
                UnityEditor.Undo.SetTransformParent(existing.transform,parent,"Keep moved workyard object");return existing.gameObject;
            }
            Vector3 p=Ground(position);
            var go=model.Finish(a,parent,key,World(p),rotation*Quaternion.Euler(0,yaw,0));go.name=name;
            var handle=go.AddComponent<WorkyardProp>();handle.key=key;handle.description="Отдельный объект рабочего двора. Рабочие места и декор можно перемещать целиком.";
            handle.defaultPosition=go.transform.position;handle.defaultRotation=go.transform.rotation;handle.defaultScale=go.transform.lossyScale;
            return go;
        }
    }
}
