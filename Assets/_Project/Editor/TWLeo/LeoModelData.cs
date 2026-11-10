using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Geometry and assembly recipe exported by the included deterministic Blender source.
// The Unity builder creates persistent meshes/materials/prefab from these tables; no Blender install is needed.
public static class LeoModelData
{
    public const string Path="Assets/_Project/Editor/TWLeo/LeoModel.json";
    [Serializable]public sealed class Document {public int version;public float height,feet;public string name,authorNote;public MaterialDef[] materials;public NodeDef[] nodes;public PartDef[] parts;public MeshDef[] meshes;}
    [Serializable]public sealed class MaterialDef {public string name,hex;public float smoothness,metallic;}
    [Serializable]public sealed class NodeDef {public string name,parent;public bool active=true;public float[] position,rotation;}
    [Serializable]public sealed class PartDef {public string name,node,mesh,material;public float[] position,rotation,scale;}
    [Serializable]public sealed class MeshDef {public string name;public float[] vertices,normals;public int[] triangles;}
    public static Vector3 V(float[] p){return new Vector3(p[0],p[1],p[2]);}
    public static Quaternion Q(float[] p){return new Quaternion(p[0],p[1],p[2],p[3]);}
    public static Document Load()
    {
        var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(Path);if(asset==null)throw new InvalidOperationException("Нет "+Path+". Скопируй Assets из архива целиком.");
        var data=JsonUtility.FromJson<Document>(asset.text);var errors=Validate(data);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));return data;
    }
    public static List<string> Validate(Document data)
    {
        var errors=new List<string>();
        if(data==null||data.version!=1||data.nodes==null||data.parts==null||data.materials==null||data.meshes==null){errors.Add("Неполные данные модели Лео.");return errors;}
        bool Vector(float[] value,int n){if(value==null||value.Length!=n)return false;foreach(float f in value)if(float.IsNaN(f)||float.IsInfinity(f))return false;return true;}
        var nodes=new HashSet<string>();var meshes=new HashSet<string>();var mats=new HashSet<string>();var names=new HashSet<string>();
        foreach(var node in data.nodes)
        {
            if(node==null||string.IsNullOrEmpty(node.name)){errors.Add("Пустой узел.");continue;}
            if(!string.IsNullOrEmpty(node.parent)&&!nodes.Contains(node.parent))errors.Add("Родитель отсутствует или указан после потомка: "+node.name);
            if(!nodes.Add(node.name))errors.Add("Повтор узла "+node.name);
            if(!Vector(node.position,3)||!Vector(node.rotation,4))errors.Add("Поза узла "+node.name);
        }
        foreach(var mat in data.materials){Color color;if(mat==null||string.IsNullOrEmpty(mat.name)||!mats.Add(mat.name)||!ColorUtility.TryParseHtmlString(mat.hex,out color))errors.Add("Некорректный материал.");}
        foreach(var mesh in data.meshes)
        {
            if(mesh==null||string.IsNullOrEmpty(mesh.name)||!meshes.Add(mesh.name)){errors.Add("Повтор/пустой меш.");continue;}
            if(mesh.vertices==null||mesh.normals==null||mesh.triangles==null||mesh.vertices.Length%3!=0||mesh.normals.Length!=mesh.vertices.Length||mesh.triangles.Length%3!=0){errors.Add("Массивы меша "+mesh.name);continue;}
            foreach(float f in mesh.vertices)if(float.IsNaN(f)||float.IsInfinity(f)){errors.Add("Неконечная вершина "+mesh.name);break;}
            foreach(int i in mesh.triangles)if(i<0||i>=mesh.vertices.Length/3){errors.Add("Индекс меша "+mesh.name);break;}
        }
        foreach(var part in data.parts)
        {
            if(part==null||string.IsNullOrEmpty(part.name)){errors.Add("Пустая деталь.");continue;}
            if(!names.Add(part.name))errors.Add("Повтор детали "+part.name);
            if(!nodes.Contains(part.node)||!meshes.Contains(part.mesh)||!mats.Contains(part.material))errors.Add("Ссылка детали "+part.name);
            if(!Vector(part.position,3)||!Vector(part.rotation,4)||!Vector(part.scale,3))errors.Add("Трансформация детали "+part.name);
            else foreach(float f in part.scale)if(f<=0)errors.Add("Неположительный масштаб "+part.name);
        }
        foreach(string required in new[]{"Root","Hips","Spine","Chest","Neck","Head","HandR","HandL","AxeRoot","AxeHolster","HandGripR","HammerRoot","AxeBladeTip","HammerStrike","Backpack","Cargo0","Cargo5","Compass","CompassNeedle","ToeR","ToeL","JacketHemFrontR","JacketHemFrontL","JacketHemBackR","JacketHemBackL"})if(!nodes.Contains(required))errors.Add("Нет узла "+required);
        if(!names.Contains("Axe_TW_Right")||!names.Contains("Axe_TW_Left"))errors.Add("Нет гравировки TW на топоре.");
        if(float.IsNaN(data.height)||float.IsInfinity(data.height)||data.height<1f||data.height>4f)errors.Add("Неверная высота модели.");
        return errors;
    }
}
