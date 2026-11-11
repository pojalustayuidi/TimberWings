using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class LeoModelBuilder
{
    public const string Menu="Tools/BuilderSim/Игрок/Лео — модель/";
    public const string Folder="Assets/_Project/Characters/Leo";
    public const string PrefabPath="Assets/_Project/Resources/LeoAnimation/Leo_Model.prefab";
    public const string ModelName="LeoModel";
    public static GameObject BuildPrefab()
    {
        var data=LeoModelData.Load();
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
        if(shader==null)throw new InvalidOperationException("Не найден URP Lit или Standard.");
        EnsureFolder(Folder+"/Meshes");EnsureFolder(Folder+"/Materials");
        var materials=new Dictionary<string,Material>();var units=new Dictionary<string,Mesh>();GameObject root=null;
        try
        {
            foreach(var def in data.materials)
            {
                string path=Folder+"/Materials/Leo_"+def.name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(shader){name="Leo_"+def.name};AssetDatabase.CreateAsset(mat,path);}
                Color color;ColorUtility.TryParseHtmlString(def.hex,out color);
                if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);if(mat.HasProperty("_Color"))mat.SetColor("_Color",color);
                if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",def.smoothness);if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",def.smoothness);
                if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",def.metallic);mat.enableInstancing=true;EditorUtility.SetDirty(mat);materials[def.name]=mat;
            }
            foreach(var def in data.meshes)
            {
                var mesh=new Mesh{name="Leo_source_"+def.name};units.Add(def.name,mesh);
                if(def.vertices.Length/3>65535)mesh.indexFormat=IndexFormat.UInt32;
                var positions=new Vector3[def.vertices.Length/3];var normals=new Vector3[positions.Length];
                for(int i=0;i<positions.Length;i++){positions[i]=new Vector3(def.vertices[i*3],def.vertices[i*3+1],def.vertices[i*3+2]);normals[i]=new Vector3(def.normals[i*3],def.normals[i*3+1],def.normals[i*3+2]);}
                mesh.vertices=positions;mesh.normals=normals;mesh.triangles=def.triangles;mesh.RecalculateBounds();
            }
            root=new GameObject(ModelName);root.transform.localScale=Vector3.one*VisualScale;
            var bones=new Dictionary<string,Transform>();
            foreach(var def in data.nodes)
            {
                var go=new GameObject(def.name);go.transform.SetParent(string.IsNullOrEmpty(def.parent)?root.transform:bones[def.parent],false);
                go.transform.localPosition=LeoModelData.V(def.position);go.transform.localRotation=LeoModelData.Q(def.rotation);bones.Add(def.name,go.transform);go.SetActive(def.active);
            }
            var upgrade=new GameObject("LogUpgrade");upgrade.transform.SetParent(bones["Backpack"],false);
            bones.Add("LogUpgrade",upgrade.transform);upgrade.SetActive(false);
            // Carrier v2: the canvas bag is dropped; belt shelf, hook lugs, tie straps and the
            // hugging bandolier come from LeoModel.json (Tools/patch_leo_carrier.py). Cargo0..9 are data too.
            var groups=new Dictionary<string,List<LeoModelData.PartDef>>();
            foreach(var part in data.parts)
            {
                if(part.node=="BackpackFlap"||part.node=="BackpackPocket"||
                   part.name.StartsWith("Pack")||part.name=="SidePouch"||part.name=="SidePouchFlap"||
                   part.name.StartsWith("Lid")||part.name.StartsWith("CargoTie"))continue;
                if(part.name=="ChestStrap"||part.name=="BackStrap")part.material="Leather";
                string key=part.node+"__"+part.material+(part.name.StartsWith("Axe_TW_")?"__"+part.name:"");
                if(!groups.ContainsKey(key))groups[key]=new List<LeoModelData.PartDef>();groups[key].Add(part);
            }
            foreach(var entry in groups)
            {
                var first=entry.Value[0];var combines=new CombineInstance[entry.Value.Count];int vertices=0;
                for(int i=0;i<combines.Length;i++)
                {
                    var p=entry.Value[i];combines[i]=new CombineInstance{mesh=units[p.mesh],transform=Matrix4x4.TRS(LeoModelData.V(p.position),LeoModelData.Q(p.rotation),LeoModelData.V(p.scale))};vertices+=units[p.mesh].vertexCount;
                }
                string path=Folder+"/Meshes/Leo_"+entry.Key+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool created=mesh==null;
                if(created)mesh=new Mesh{name="Leo_"+entry.Key};else mesh.Clear();mesh.indexFormat=vertices>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                mesh.CombineMeshes(combines,true,true,false);mesh.RecalculateBounds();
                if(created)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
                var go=new GameObject(first.name.StartsWith("Axe_TW_")?first.name:"Geo_"+entry.Key);go.transform.SetParent(bones[first.node],false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[first.material];renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            var rig=root.AddComponent<LeoRig>();rig.Bind();rig.PrepareAnimationRest();var presentation=root.AddComponent<LeoModelPresentation>();presentation.rig=rig;
            var errors=InspectModel(root);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);if(prefab==null)throw new IOException("Не удалось сохранить префаб Лео.");
            AssetDatabase.SaveAssets();return prefab;
        }
        finally{if(root!=null)Object.DestroyImmediate(root);foreach(var mesh in units.Values)if(mesh!=null)Object.DestroyImmediate(mesh);}
    }
    public static List<string> InspectModel(GameObject model)
    {
        var errors=new List<string>();if(model==null){errors.Add("Нет модели Лео.");return errors;}
        var rig=model.GetComponent<LeoRig>();
        if(rig==null){errors.Add("Нет LeoRig.");return errors;}
        foreach(var joint in new[]{rig.hips,rig.spine,rig.chest,rig.neck,rig.head,rig.handL,rig.handR,rig.elbowL,rig.elbowR,rig.upperArmL,rig.upperArmR,rig.hipL,rig.hipR,rig.kneeL,rig.kneeR,rig.ankleL,rig.ankleR,rig.toeL,rig.toeR,rig.compass,rig.compassNeedle,rig.backpack,rig.backpackFlap,rig.backpackPocket,rig.axeRoot,rig.hammerRoot,rig.handGripR,rig.handGripL,rig.axeHolster,rig.buildGripL,rig.carryGripL,rig.carryGripR})
            if(joint==null){errors.Add("Не назначен один из суставов или реквизита.");break;}
        if(!rig.HasRest||rig.JointCount<70)errors.Add("Нет полной позы покоя.");
        if(rig.axeBladeTip==null||rig.hammerStrike==null)errors.Add("Нет точек удара инструментов.");
        if(rig.hemFrontR==null||rig.hemFrontL==null||rig.hemBackR==null||rig.hemBackL==null)errors.Add("Нет панелей подола куртки (JacketHem*).");
        if (rig.HammerHolster == null) errors.Add("Нет крепления HammerHolster на поясе.");
        if(rig.cargo==null||rig.cargo.Length!=12)errors.Add("Для переноски нужны 12 визуальных слотов брёвен.");
        if(rig.frames==null||rig.frames.Length!=3||System.Array.Exists(rig.frames,f=>f==null)||rig.cargoTies2==null)errors.Add("Нет рамы переноски (FrameT1..3) или второй стяжки CargoTies2.");
        if(model.GetComponentsInChildren<Collider>(true).Length>0||model.GetComponentsInChildren<Rigidbody>(true).Length>0)errors.Add("Визуальная модель не должна содержать физику.");
        if(Vector3.Distance(model.transform.localScale,Vector3.one*VisualScale)>.0001f)errors.Add($"Неверный размер Лео: нужен масштаб {VisualScale:0.000} по каждой оси.");
        int triangles=0,letters=0;bool carrier=false;
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.name.StartsWith("Axe_TW_"))letters++;
            if(filter.name=="Geo_Backpack__Leather"||filter.name=="Geo_Backpack__Brass")carrier=true;
            if(filter.name.StartsWith("Geo_Backpack__Canvas"))errors.Add("На Лео остался меш старого рюкзака.");
            if(filter.sharedMesh==null)errors.Add("Пустой меш "+filter.name);else triangles+=filter.sharedMesh.triangles.Length/3;
        }
        if(!carrier)errors.Add("Не найдены ремни для переноски брёвен.");
        if(letters!=2)errors.Add("Гравировка TW должна быть с обеих сторон топора.");
        if(triangles<10000||triangles>140000)errors.Add("Неожиданный бюджет треугольников: "+triangles);
        foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))if(renderer.sharedMaterial==null)errors.Add("Нет материала "+renderer.name);
        return errors;
    }
    static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');EnsureFolder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
    [MenuItem(Menu+"2. Только собрать префаб",false,351)]
    static void PrefabOnly()
    {if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("LEO: останови Play.");return;}try{var prefab=BuildPrefab();EditorGUIUtility.PingObject(prefab);Debug.Log("LEO: префаб сохранён: "+PrefabPath);}catch(Exception e){Debug.LogError("LEO: "+e);}}
}
