using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace IslandV2
{
    internal static class TWWorkyardValidation
    {
        [Serializable] sealed class TentContract
        {
            public GameObject stage1,stage2;
            public Transform buildPoint;
            public int startStage;
            public float buildTime,swingAmp,swingSpeed,flagAmp;
            public UnityEngine.Events.UnityEvent<int> onStageChanged;
        }
        static readonly Regex Refs=new Regex("\\\"instanceID\\\"\\s*:\\s*(-?\\d+)");
        static string Key(Object obj)
        {
            if(obj==null)return "null";
            Transform t=obj is GameObject go?go.transform:obj is Component c?c.transform:null;
            if(t!=null && t.gameObject.scene.IsValid())
            {
                string path="";for(var p=t;p!=null;p=p.parent)path="/"+p.name+path;
                return t.gameObject.scene.path+path+":"+obj.GetType().FullName;
            }
            if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long id))return guid+":"+id;
            return obj.GetType().FullName+":"+obj.name;
        }
        static string Canonical(string json)
        {
            return Refs.Replace(json,m=>
            {
                int id=int.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
                string key=Key(id==0?null:EditorUtility.InstanceIDToObject(id));
                return "\"stableRef\":\""+key.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
            });
        }
        public static string Logic(TWWorkyardContext x)
        {
            var t=x.tent;var contract=new TentContract{stage1=t.stage1,stage2=t.stage2,buildPoint=t.buildPoint,startStage=t.startStage,
                buildTime=t.buildTime,swingAmp=t.swingAmp,swingSpeed=t.swingSpeed,flagAmp=t.flagAmp,onStageChanged=t.onStageChanged};
            var text=new StringBuilder(Canonical(JsonUtility.ToJson(contract)));
            text.Append(t.transform.localToWorldMatrix.ToString("R"));
            if(t.buildPoint!=null)text.Append(t.buildPoint.localToWorldMatrix.ToString("R"));
            foreach(Object obj in new Object[]{x.build,x.build.data,x.manager,x.manager!=null?x.manager.database:null,x.camp})
                if(obj!=null)text.Append(Canonical(EditorJsonUtility.ToJson(obj)));
            var roots=t.gameObject.scene.GetRootGameObjects();
            foreach(var link in roots.SelectMany(g=>g.GetComponentsInChildren<BuildStageLink>(true)).Where(l=>l.tent==t))text.Append(Canonical(EditorJsonUtility.ToJson(link)));
            foreach(var ri in roots.SelectMany(g=>g.GetComponentsInChildren<ResourceItem>(true)).OrderBy(r=>Key(r),StringComparer.Ordinal))
            {text.Append(Canonical(EditorJsonUtility.ToJson(ri)));text.Append(ri.transform.localToWorldMatrix.ToString("R"));text.Append(ri.gameObject.activeSelf);}
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");
        }
        public static void Mesh(Mesh m)
        {
            if(m==null || m.vertexCount<3)throw new InvalidOperationException("Пустой меш артели.");
            var v=m.vertices;var t=m.triangles;
            if(t.Length==0 || t.Length%3!=0)throw new InvalidOperationException("Нет треугольников: "+m.name);
            foreach(var p in v)if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z))throw new InvalidOperationException("NaN/Infinity: "+m.name);
            for(int i=0;i<t.Length;i+=3)
            {
                if(t[i]<0||t[i+1]<0||t[i+2]<0||t[i]>=v.Length||t[i+1]>=v.Length||t[i+2]>=v.Length)throw new InvalidOperationException("Неверные индексы: "+m.name);
                if(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude<1e-12f)throw new InvalidOperationException("Вырожденная грань: "+m.name);
            }
        }
        public static string WorkerSettings(UnityEngine.SceneManagement.Scene scene)
        {
            // Include every manager, even inactive or unselected ones, in the before/after guard for physics-only repairs.
            var text=new StringBuilder();
            foreach(var manager in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WorkerManager>(true)).OrderBy(m=>Key(m),StringComparer.Ordinal))
            {
                text.Append(Key(manager));text.Append(manager.gameObject.activeSelf);text.Append(manager.gameObject.activeInHierarchy);
                text.Append(Canonical(EditorJsonUtility.ToJson(manager)));
                if(manager.database!=null)text.Append(Canonical(EditorJsonUtility.ToJson(manager.database)));
            }
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");
        }
        public static void Art(TWWorkyardContext x,GameObject site,GameObject built,WorkyardView view,GameObject physicsRoot)
        {
            Support(x,physicsRoot);
            foreach(var art in new[]{site,built})
            {
                foreach(var f in art.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh(f.sharedMesh);var renderer=f.GetComponent<MeshRenderer>();
                    if(renderer==null||renderer.sharedMaterials.Length!=f.sharedMesh.subMeshCount||renderer.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name=="Hidden/InternalErrorShader"))throw new InvalidOperationException("Материалы: "+f.name);
                }
                if(art.GetComponentsInChildren<ResourceItem>(true).Length!=0)throw new InvalidOperationException("Деревья артели не должны становиться ресурсами игрока.");
                if(art.GetComponentsInChildren<Light>(true).Length!=0)throw new InvalidOperationException("Фонари должны использовать пул света.");
                if(art.GetComponentsInChildren<BuildingController>(true).Length!=0)throw new InvalidOperationException("Дублируется контроллер стройки.");
                foreach(var c in art.GetComponentsInChildren<Collider>(true))
                {
                    if(c.isTrigger)throw new InvalidOperationException("Новый декор не должен создавать игровые триггеры.");
                    if(c.transform.name=="TW_Collision" && (c.sharedMaterial==null || c.sharedMaterial.staticFriction!=0 || c.sharedMaterial.dynamicFriction!=0))
                        throw new InvalidOperationException("У коллайдера рабочего двора нет материала скольжения: "+c.name);
                    if(c is BoxCollider || c is CapsuleCollider)
                    {
                        if(x.tent.buildPoint!=null && Clearance(c,x.tent.buildPoint.position)<0.65f)
                            throw new InvalidOperationException("Перекрыта точка подхода к стройке: "+c.name);
                        foreach(var resource in x.resourceTrees)
                            if(Clearance(c,x.World(new Vector3(resource.x,0,resource.y)))<1.1f)
                                throw new InvalidOperationException("Реквизит перекрывает подход к существующему ресурсному дереву. Дерево сохранено, операция отменена.");
                    }
                }
            }
            if(view.stations.Length!=3*WorkerBalance.MaxPerRole)throw new InvalidOperationException("Не хватает рабочих мест трёх ролей.");
            foreach(WorkerRole role in Enum.GetValues(typeof(WorkerRole)))
                for(int i=0;i<WorkerBalance.MaxPerRole;i++)
                    if(view.stations.Count(s=>s!=null&&s.role==role&&s.index==i)!=1)throw new InvalidOperationException("Повтор или пропуск рабочего места: "+role+" "+i);
            if(view.actorPrefabs.Length!=3 || view.actorPrefabs.Any(a=>a==null))throw new InvalidOperationException("Не созданы модели трёх ролей.");
            if(view.stock==null || view.stock.logUnits.Length==0 || view.stock.plankUnits.Length==0)throw new InvalidOperationException("Нет визуального склада.");
            var canopy=built.GetComponentsInChildren<WorkyardProp>(true).Single(p=>p.key=="Built_Canopy");
            if(!canopy.GetComponentsInChildren<MeshCollider>(true).Any(c=>c.sharedMesh!=null && c.sharedMesh.name=="Workyard_Deck"))
                throw new InvalidOperationException("Нет опоры и пологого входа на деревянный настил. Выполни пункт 9.");
            foreach(var tree in built.GetComponentsInChildren<WorkyardTree>(true))
                if(tree.GetComponentsInChildren<Transform>(true).Any(t=>t.CompareTag("Resource")))throw new InvalidOperationException("Дерево NPC помечено ресурсом игрока.");
        }
        static float Clearance(Collider collider,Vector3 point)
        {
            Vector3 q=collider.transform.InverseTransformPoint(point);
            float distance=float.PositiveInfinity;
            if(collider is BoxCollider box)
            {
                q-=box.center;
                distance=new Vector2(Mathf.Max(0,Mathf.Abs(q.x)-box.size.x*0.5f),Mathf.Max(0,Mathf.Abs(q.z)-box.size.z*0.5f)).magnitude;
            }
            if(collider is CapsuleCollider capsule)
            {
                q-=capsule.center;float half=Mathf.Max(0,capsule.height*0.5f-capsule.radius);
                if(capsule.direction==0)q.x=Mathf.Max(0,Mathf.Abs(q.x)-half);
                if(capsule.direction==2)q.z=Mathf.Max(0,Mathf.Abs(q.z)-half);
                distance=Mathf.Max(0,new Vector2(q.x,q.z).magnitude-capsule.radius);
            }
            Vector3 scale=collider.transform.lossyScale;
            return distance*Mathf.Max(0.0001f,Mathf.Min(Mathf.Abs(scale.x),Mathf.Abs(scale.z)));
        }
        static void Support(TWWorkyardContext x,GameObject root)
        {
            if(root==null || root.transform.parent!=x.tent.transform || !root.activeSelf)
                throw new InvalidOperationException("Нет постоянного пола вне стадий стройки. Выполни пункт 9.");
            var floor=root.GetComponent<MeshCollider>();
            if(floor==null || !floor.enabled || floor.isTrigger || floor.convex || floor.sharedMesh==null)
                throw new InvalidOperationException("Некорректная физическая поверхность рабочего двора.");
            if(root.GetComponentsInChildren<Renderer>(true).Length!=0 || root.GetComponentsInChildren<Rigidbody>(true).Length!=0)
                throw new InvalidOperationException("Служебная опора должна быть статичной и невидимой.");
            Mesh(floor.sharedMesh);
            // Cast against this collider alone, so the original TerrainCollider cannot hide a missing floor.
            if(root.activeInHierarchy)
            {
                var vertices=floor.sharedMesh.vertices;var triangles=floor.sharedMesh.triangles;
                int stride=Mathf.Max(1,triangles.Length/3/48)*3;
                for(int i=0;i<triangles.Length;i+=stride)
                {
                    Vector3 p=floor.transform.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3f);
                    if(!floor.Raycast(new Ray(p+Vector3.up,Vector3.down),out RaycastHit hit,2f) || Mathf.Abs(hit.point.y-p.y)>0.03f)
                        throw new InvalidOperationException("Опора не ловит игрока в точке "+p+". Повтори пункт 9.");
                }
            }
        }
    }
}
