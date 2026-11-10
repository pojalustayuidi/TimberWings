using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class OskarModelBuilder
{
    public const string Menu="Tools/BuilderSim/NPC/Оскар — модель/";
    public const string Folder="Assets/_Project/Characters/Oskar";
    public const string PrefabPath=Folder+"/Oskar_Model.prefab";
    public const string ModelName="OskarModel";
    public static GameObject BuildPrefab()
    {
        var data=OskarModelData.Load();
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
        if(shader==null)throw new InvalidOperationException("Не найден URP Lit или Standard.");
        EnsureFolder(Folder+"/Meshes");EnsureFolder(Folder+"/Materials");
        var materials=new Dictionary<string,Material>();var units=new Dictionary<string,Mesh>();GameObject root=null;
        try
        {
            foreach(var def in data.materials)
            {
                string path=Folder+"/Materials/Oskar_"+def.name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(shader){name="Oskar_"+def.name};AssetDatabase.CreateAsset(mat,path);}
                Color color;ColorUtility.TryParseHtmlString(def.hex,out color);
                if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);if(mat.HasProperty("_Color"))mat.SetColor("_Color",color);
                if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",def.smoothness);if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",def.smoothness);
                if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",0);mat.enableInstancing=true;EditorUtility.SetDirty(mat);materials[def.name]=mat;
            }
            foreach(var def in data.meshes)
            {
                var mesh=new Mesh{name="Oskar_source_"+def.name};units.Add(def.name,mesh);
                if(def.vertices.Length/3>65535)mesh.indexFormat=IndexFormat.UInt32;
                var positions=new Vector3[def.vertices.Length/3];var normals=new Vector3[positions.Length];
                for(int i=0;i<positions.Length;i++){positions[i]=new Vector3(def.vertices[i*3],def.vertices[i*3+1],def.vertices[i*3+2]);normals[i]=new Vector3(def.normals[i*3],def.normals[i*3+1],def.normals[i*3+2]);}
                mesh.vertices=positions;mesh.normals=normals;mesh.triangles=def.triangles;mesh.RecalculateBounds();
            }
            root=new GameObject(ModelName);var bones=new Dictionary<string,Transform>();
            foreach(var def in data.nodes)
            {
                var go=new GameObject(def.name);go.transform.SetParent(string.IsNullOrEmpty(def.parent)?root.transform:bones[def.parent],false);
                go.transform.localPosition=OskarModelData.V(def.position);go.transform.localRotation=OskarModelData.Q(def.rotation);bones.Add(def.name,go.transform);
            }
            // Combine only within the same joint/material. Animation pivots and props stay separate.
            var groups=new Dictionary<string,List<OskarModelData.PartDef>>();
            foreach(var part in data.parts)
            {
                string key=part.node+"__"+part.material+(part.name=="Flopy_Engraving"?"__Flopy_Engraving":"");
                if(!groups.ContainsKey(key))groups[key]=new List<OskarModelData.PartDef>();groups[key].Add(part);
            }
            foreach(var entry in groups)
            {
                var first=entry.Value[0];var combines=new CombineInstance[entry.Value.Count];int vertices=0;
                for(int i=0;i<combines.Length;i++)
                {
                    var p=entry.Value[i];combines[i]=new CombineInstance{mesh=units[p.mesh],transform=Matrix4x4.TRS(OskarModelData.V(p.position),OskarModelData.Q(p.rotation),OskarModelData.V(p.scale))};vertices+=units[p.mesh].vertexCount;
                }
                string path=Folder+"/Meshes/Oskar_"+entry.Key+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool created=mesh==null;
                if(created)mesh=new Mesh{name="Oskar_"+entry.Key};else mesh.Clear();mesh.indexFormat=vertices>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                mesh.CombineMeshes(combines,true,true,false);mesh.RecalculateBounds();
                if(created)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
                var go=new GameObject(first.name=="Flopy_Engraving"?"Flopy_Engraving":"Geo_"+entry.Key);go.transform.SetParent(bones[first.node],false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[first.material];renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            var rig=root.AddComponent<OskarRig>();rig.Bind();rig.CaptureRest();var idle=root.AddComponent<OskarIdle>();idle.rig=rig;
            var errors=InspectModel(root);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);if(prefab==null)throw new IOException("Не удалось сохранить префаб Оскара.");
            AssetDatabase.SaveAssets();return prefab;
        }
        finally{if(root!=null)Object.DestroyImmediate(root);foreach(var mesh in units.Values)if(mesh!=null)Object.DestroyImmediate(mesh);}
    }
    public static List<string> InspectModel(GameObject model)
    {
        var errors=new List<string>();if(model==null){errors.Add("Нет модели Оскара.");return errors;}
        var rig=model.GetComponent<OskarRig>();
        if(rig==null){errors.Add("Нет OskarRig.");return errors;}
        foreach(var pair in new[]{rig.hips,rig.spine,rig.head,rig.eyeL,rig.eyeR,rig.mouth,rig.beard,rig.shoulderL,rig.elbowL,rig.handL,rig.shoulderR,rig.elbowR,rig.handR,rig.hipL,rig.kneeL,rig.ankleL,rig.hipR,rig.kneeR,rig.ankleR,rig.squirrel,rig.squirrelHead,rig.squirrelTail,rig.planer,rig.planerKnob,rig.planerGrip,rig.blueprint,rig.pencil,rig.wingCharm})
            if(pair==null){errors.Add("Не назначен один из суставов/реквизита.");break;}
        if(!rig.HasRest||rig.JointCount<35)errors.Add("Не сохранена поза покоя.");
        if(model.GetComponentsInChildren<Collider>(true).Length>0)errors.Add("У визуальной модели появились коллайдеры.");
        bool flopy=false;int triangles=0;
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.name=="Flopy_Engraving")flopy=true;
            if(filter.sharedMesh==null)errors.Add("Пустой меш "+filter.name);else triangles+=filter.sharedMesh.triangles.Length/3;
        }
        if(!flopy)errors.Add("Нет подписи flopy.");if(triangles<1000||triangles>140000)errors.Add("Неожиданный бюджет треугольников: "+triangles);
        foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))if(renderer.sharedMaterial==null)errors.Add("Не назначен материал "+renderer.name);
        return errors;
    }
    static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');EnsureFolder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
    [MenuItem(Menu+"2. Только собрать префаб",false,291)]
    static void PrefabOnly()
    {if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("OSKAR: останови Play.");return;}try{var prefab=BuildPrefab();EditorGUIUtility.PingObject(prefab);Debug.Log("OSKAR: префаб сохранён: "+PrefabPath);}catch(Exception e){Debug.LogError("OSKAR: "+e);}}
}
