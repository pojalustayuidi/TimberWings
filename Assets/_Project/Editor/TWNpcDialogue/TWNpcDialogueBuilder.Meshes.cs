using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class TWNpcDialogueBuilder
{
    static NpcDialogueOutline.Actor[] Actors(Scene scene)
    {
        var actors=new List<NpcDialogueOutline.Actor>();var cache=new Dictionary<Mesh,Mesh>();
        foreach(var speaker in All<Speaker>(scene))
        {
            if(speaker.GetComponent<PlayerMovement>()!=null)continue;
            var roots=new List<Transform>();
            foreach(var rig in speaker.GetComponentsInChildren<MiraRig>(true))roots.Add(rig.transform);
            foreach(var rig in speaker.GetComponentsInChildren<OskarRig>(true))roots.Add(rig.transform);
            var parts=new List<NpcDialogueOutline.Part>();var seen=new HashSet<MeshFilter>();
            foreach(var root in roots)foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!seen.Add(filter)||filter.sharedMesh==null||filter.GetComponentInParent<NpcMarker>(true)!=null)continue;
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||!renderer.enabled||!VisibleWithin(filter.transform,speaker.transform))continue;
                // Steam is transparent; decorative UI/particles do not form part of the opaque silhouette.
                bool opaque=false;foreach(var material in renderer.sharedMaterials)if(material!=null&&material.renderQueue<3000&&material.name.IndexOf("Steam",StringComparison.OrdinalIgnoreCase)<0){opaque=true;break;}
                if(!opaque)continue;
                var source=filter.sharedMesh;Mesh shell;if(!cache.TryGetValue(source,out shell)){shell=Shell(source);cache.Add(source,shell);}
                parts.Add(new NpcDialogueOutline.Part{mesh=shell,renderer=renderer,transform=filter.transform});
            }
            if(parts.Count>0)actors.Add(new NpcDialogueOutline.Actor{speaker=speaker,parts=parts.ToArray()});
        }
        return actors.ToArray();
    }
    static bool VisibleWithin(Transform transform,Transform owner)
    {
        // A quest NPC may start inactive. Internal model switches still have to be respected.
        for(var current=transform;current!=null&&current!=owner;current=current.parent)if(!current.gameObject.activeSelf)return false;
        return true;
    }
    static Vector3Int Quantize(Vector3 p){return new Vector3Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000),Mathf.RoundToInt(p.z*10000));}
    static Mesh Shell(Mesh source)
    {
        if(source.vertexCount==0||source.vertexCount>200000)throw new InvalidOperationException("Некорректный размер меша NPC: "+source.name);
        string guid;long local;
        string id=AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out guid,out local)?guid+"_"+local:"scene_"+source.GetInstanceID();
        string path=Generated+"/shell_"+id+".asset";
        using(var data=MeshUtility.AcquireReadOnlyMeshData(source))
        using(var positions=new NativeArray<Vector3>(source.vertexCount,Allocator.Temp))
        using(var normals=new NativeArray<Vector3>(source.vertexCount,Allocator.Temp))
        {
            var input=data[0];input.GetVertices(positions);if(source.HasVertexAttribute(VertexAttribute.Normal))input.GetNormals(normals);
            var sums=new Dictionary<Vector3Int,Vector3>();
            for(int i=0;i<positions.Length;i++)
            {
                var key=Quantize(positions[i]);Vector3 sum;sums.TryGetValue(key,out sum);var normal=normals[i];
                if(normal.sqrMagnitude<.0001f)normal=positions[i]-source.bounds.center;sums[key]=sum+normal;
            }
            var smooth=new Vector3[positions.Length];
            for(int i=0;i<smooth.Length;i++){var normal=sums[Quantize(positions[i])];if(normal.sqrMagnitude<.0001f)normal=positions[i]-source.bounds.center;smooth[i]=normal.sqrMagnitude>.0001f?normal.normalized:Vector3.up;}
            var result=new Mesh{name=source.name+"_NpcOutline",indexFormat=source.indexFormat};result.vertices=positions.ToArray();result.normals=smooth;result.subMeshCount=input.subMeshCount;
            for(int i=0;i<input.subMeshCount;i++)
            {
                var sub=input.GetSubMesh(i);using(var indices=new NativeArray<int>(sub.indexCount,Allocator.Temp)){input.GetIndices(indices,i,true);result.SetIndices(indices.ToArray(),sub.topology,i,false);}
            }
            result.bounds=source.bounds;
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){EditorUtility.CopySerialized(result,existing);Object.DestroyImmediate(result);EditorUtility.SetDirty(existing);return existing;}
            AssetDatabase.CreateAsset(result,path);return result;
        }
    }
}
