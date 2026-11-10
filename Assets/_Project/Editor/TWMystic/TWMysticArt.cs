using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace IslandV2
{
    public static class TWMysticArt
    {
        const string Menu="Tools/BuilderSim/Лес/MYSTIC FOREST/";
        internal const string RootName="TW_MYSTIC_FOREST";
        [MenuItem(Menu+"1. Проверить Старый лес",false,350)] public static void Audit()
        {
            try{var x=Inspect();Debug.Log("MYSTIC FOREST: Дедушка — "+x.elder.name+"; ресурсных кандидатов "+x.resources.Length+". Окружение сохраняется в сцене; большой ствол и метки не перемещаются.");}
            catch(Exception e){Debug.LogError("MYSTIC FOREST: "+e.Message);}
        }
        [MenuItem(Menu+"2. Собрать Круг молчания в сцене",false,351)] public static void Apply()
        {
            int group=-1;string folder=null;
            try
            {
                var x=Inspect();EditorUtility.DisplayProgressBar("TimberWings","Подбор связной суши и свободных мест",0.05f);x.PlanLayout();var scene=x.pad.gameObject.scene;
                if(scene.isDirty && !EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Не удалось сохранить исходную сцену.");
                string backup=Backup(scene);Vector3 elderPosition=x.elder.transform.position;Quaternion elderRotation=x.elder.transform.rotation;Vector3 elderScale=x.elder.transform.localScale;
                Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("TimberWings — Круг молчания");
                var state=x.state;if(state==null){state=Undo.AddComponent<MysticArtState>(x.pad.gameObject);x.state=state;}
                Undo.RecordObject(state,"Mystic ownership and rollback");var old=state.art;
                if(old!=null)
                {
                    foreach(var prop in old.GetComponentsInChildren<MysticProp>(true))
                        if(Vector3.Distance(prop.transform.position,prop.defaultPosition)>0.001f || Quaternion.Angle(prop.transform.rotation,prop.defaultRotation)>0.05f ||
                            Vector3.Distance(prop.transform.lossyScale,prop.defaultScale)>0.001f || !prop.gameObject.activeSelf)x.keep[prop.key]=prop;
                    TWMysticTrees.Restore(state,false);
                }
                else
                {
                    state.elder=x.elder;state.elderJson=EditorJsonUtility.ToJson(x.elder);TWMysticTrees.Capture(x);
                }
                folder="Assets/_Project/IslandV2/MysticArt/Scenes/"+AssetDatabase.AssetPathToGUID(scene.path)+"/Build_"+Guid.NewGuid().ToString("N").Substring(0,12);
                IslandLayout.EnsureFolder(folder);var assets=new TWMysticAssets(folder);
                var root=new GameObject(RootName+"__building");root.transform.SetParent(x.manual,false);root.transform.SetPositionAndRotation(x.center,x.rotation);
                Undo.RegisterCreatedObjectUndo(root,"Create authored mystic environment");x.root=root.transform;
                Retire(x);
                EditorUtility.DisplayProgressBar("TimberWings","Грунт, шесть деревьев, скамьи и сюжетные следы",0.25f);
                TWMysticGround.Build(x,assets,x.root,"00_Conforming_Ground");var surface=TWMysticModels.Slip(folder);
                TWMysticTrees.Build(x,assets,surface);TWMysticModels.Build(x,assets,surface);
                EditorUtility.DisplayProgressBar("TimberWings","Сохранённый свет, эффекты и дневная/ночная атмосфера",0.7f);
                TWMysticEffects.Build(x,assets);
                Undo.RecordObject(x.elder,"The elder remembers the missing visitors");
                x.elder.caption="ХРАНИТЕЛЬ КРУГА";x.elder.toastColor=new Color(0.62f,0.75f,0.82f);
                x.elder.greetings=new[]{"ЗДЕСЬ ЖДАЛИ ДО РАССВЕТА","ЛЕС ПОМНИТ ТЕХ, КТО НЕ ВЕРНУЛСЯ","ГУННАР ТОЖЕ СЛЫШАЛ ЭТОТ ЗОВ","ШЕСТЬ УЗЛОВ. ОДНА ОБОРВАННАЯ ЛЕНТА.","НЕ ВСЕ ГОЛОСА ПРИНАДЛЕЖАТ ВЕТРУ"};
                state.art=root;state.folder=folder;state.groundRadiusX=x.groundRadiusX;state.groundRadiusZ=x.groundRadiusZ;state.zoneRadius=x.zoneRadius;
                Physics.SyncTransforms();TWMysticValidation.Art(x);
                if(x.elder.transform.position!=elderPosition || Quaternion.Angle(x.elder.transform.rotation,elderRotation)>0.001f || x.elder.transform.localScale!=elderScale)
                    throw new InvalidOperationException("Сдвинулся Дедушка. Сборка отменена.");
                if(old!=null)Undo.DestroyObjectImmediate(old);root.name=RootName;
                EditorUtility.SetDirty(state);EditorUtility.SetDirty(x.elder);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);
                Selection.activeGameObject=root;
                Debug.Log("MYSTIC FOREST: готово. Шесть рубимых деревьев, шесть скамей, три читаемых следа, две ночные надписи. Свет, геометрия, звук и эффекты сохранены в сцене. Ctrl+S; пункт 4 — QA. Копия: "+backup);
            }
            catch(Exception e)
            {
                if(group>=0)Undo.RevertAllDownToGroup(group);
                if(folder!=null && AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
                Debug.LogError("MYSTIC FOREST: сборка отменена. "+e);
            }
            finally{EditorUtility.ClearProgressBar();}
        }
        internal static void Disable(MysticArtState state,Behaviour target)
        {
            if(target==null)return;
            if(!state.disabled.Any(d=>d.target==target))state.disabled=state.disabled.Concat(new[]{new MysticArtState.Disabled{target=target,enabled=target.enabled}}).ToArray();
            Undo.RecordObject(target,"Retire runtime world construction");target.enabled=false;
        }
        static void Retire(TWMysticContext x)
        {
            var state=x.state;var all=x.pad.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach(var t in all)
            {
                if((t.position-x.center).sqrMagnitude>40*40)continue;
                foreach(var behaviour in t.GetComponents<Behaviour>())
                    if(behaviour is MysticGladeBuilder || behaviour is MysticGladeVibe || behaviour is GladeVibe || behaviour is GrandpaVibe || behaviour is ChopGroveBuilder)
                        Disable(state,behaviour);
            }
            var renderers=state.worldRenderers.ToList();var collisions=state.worldCollisions.ToList();var hidden=state.hidden.ToList();
            string[] oldNames={"ChopZone","ChopCamp","ChopGladeDecor","GrandpaGrove","ForestArch","_MysticGlade"};
            foreach(var t in all.Where(t=>oldNames.Contains(t.name)))
            {
                foreach(var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if(r.transform.IsChildOf(x.elder.transform) || r.GetComponentInParent<ResourceItem>()!=null || Vector3.Distance(r.bounds.center,x.center)>31)continue;
                    if(!renderers.Any(v=>v.target==r))renderers.Add(new MysticArtState.RendererRecord{target=r,enabled=r.enabled});Undo.RecordObject(r,"Retire old forest scenery");r.enabled=false;
                }
                foreach(var c in t.GetComponentsInChildren<Collider>(true))
                {
                    Vector3 colliderCenter=c is BoxCollider box?c.transform.TransformPoint(box.center):c is CapsuleCollider capsule?c.transform.TransformPoint(capsule.center):
                        c is SphereCollider sphere?c.transform.TransformPoint(sphere.center):c.bounds.center;
                    if(c.isTrigger || c.GetComponentInParent<BuildingController>()!=null || c.transform.IsChildOf(x.elder.transform) || c.GetComponentInParent<ResourceItem>()!=null || Vector3.Distance(colliderCenter,x.center)>31)continue;
                    if(!collisions.Any(v=>v.target==c))collisions.Add(new MysticArtState.ColliderRecord{target=c,json=EditorJsonUtility.ToJson(c)});Undo.RecordObject(c,"Retire old forest collision");c.enabled=false;
                }
            }
            var chosen=state.trees.Select(r=>r.target).ToArray();
            foreach(var t in all)
            {
                Vector2 local=x.Local(t.position);float distance=local.magnitude;
                if(distance>x.zoneRadius || t.IsChildOf(x.elder.transform) || (state.art!=null && t.IsChildOf(state.art.transform)))continue;
                var resource=t.GetComponent<ResourceItem>();var decorative=t.GetComponent<ToonSway>();
                if(resource!=null && chosen.Contains(resource))continue;
                if(resource==null && (decorative==null || t.GetComponent<LandmarkTree>()!=null))continue;
                if(resource==null && distance>17.5f && !x.Occupied(local,2.5f))continue;
                if(!hidden.Any(h=>h.target==t.gameObject))hidden.Add(new MysticArtState.Hidden{target=t.gameObject,active=t.gameObject.activeSelf});
                Undo.RecordObject(t.gameObject,"Open the mystic clearing");t.gameObject.SetActive(false);
            }
            state.worldRenderers=renderers.ToArray();state.worldCollisions=collisions.ToArray();state.hidden=hidden.ToArray();
        }
        [MenuItem(Menu+"3. Вернуть окружение до установки",false,352)] public static void Rollback()
        {
            int group=-1;
            try
            {
                var x=Inspect();var state=x.state;if(state==null)throw new InvalidOperationException("Нет установленного MYSTIC FOREST.");
                var scene=x.pad.gameObject.scene;if(scene.isDirty && !EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Сцена не сохранена.");Backup(scene);
                Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Restore forest environment");
                TWMysticTrees.Restore(state,true);
                foreach(var v in state.worldRenderers)if(v.target!=null){Undo.RecordObject(v.target,"Restore forest renderer");v.target.enabled=v.enabled;}
                foreach(var v in state.worldCollisions)if(v.target!=null){Undo.RecordObject(v.target,"Restore forest collider");EditorJsonUtility.FromJsonOverwrite(v.json,v.target);}
                foreach(var v in state.hidden)if(v.target!=null){Undo.RecordObject(v.target,"Restore forest object");v.target.SetActive(v.active);}
                foreach(var v in state.disabled)if(v.target!=null){Undo.RecordObject(v.target,"Restore forest behaviour");v.target.enabled=v.enabled;}
                if(state.elder!=null){Undo.RecordObject(state.elder,"Restore elder dialogue");EditorJsonUtility.FromJsonOverwrite(state.elderJson,state.elder);}
                if(state.adjustedCamera && state.effectCamera!=null)
                {
                    var data=state.effectCamera.GetComponent<UniversalAdditionalCameraData>();
                    if(data!=null)
                    {
                        if(state.createdCameraData)Undo.DestroyObjectImmediate(data);
                        else{Undo.RecordObject(data,"Restore camera postprocessing");data.renderPostProcessing=state.originalPostProcessing;data.volumeLayerMask=state.originalVolumeMask;}
                    }
                }
                if(state.art!=null)Undo.DestroyObjectImmediate(state.art);Undo.DestroyObjectImmediate(state);Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("MYSTIC FOREST: прежнее сохранённое окружение и деревья восстановлены. Ctrl+S. Старый runtime-генератор этим пакетом выведен из эксплуатации.");
            }
            catch(Exception e){if(group>=0)Undo.RevertAllDownToGroup(group);Debug.LogError("MYSTIC FOREST: "+e);}
        }
        [MenuItem(Menu+"4. Проверить сохранённую локацию",false,353)] public static void QA()
        {
            try{var x=Inspect();if(x.state==null || x.state.art==null)throw new InvalidOperationException("Сначала пункт 2.");x.root=x.state.art.transform;TWMysticValidation.Art(x);Debug.Log("MYSTIC FOREST QA: PASS. Меши, физика, 6 ресурсов, подходы, свет, профили и сюжетные следы проверены. Play Mode проверяется отдельно.");}
            catch(Exception e){Debug.LogError("MYSTIC FOREST QA: "+e);}
        }
        [MenuItem(Menu+"5. Превью — мистический день",false,354)] static void Day(){Preview(0);}
        [MenuItem(Menu+"6. Превью — тревожная ночь",false,355)] static void Night(){Preview(1);}
        [MenuItem(Menu+"7. Вернуть время суток игры",false,356)] static void Auto(){Preview(-1);}
        static void Preview(float night)
        {
            try
            {
                var x=Inspect();var v=x.state!=null?x.state.art.GetComponentInChildren<MysticAtmosphere>(true):null;if(v==null)throw new InvalidOperationException("Сначала пункт 2.");
                Undo.RecordObject(v,"Mystic preview");v.previewNight=night;
                foreach(var volume in new[]{v.dayVolume,v.nightVolume})Undo.RecordObject(volume,"Mystic preview volume");
                v.dayVolume.weight=night==0?1:0;v.nightVolume.weight=night==1?1:0;
                foreach(var light in v.lights)if(light!=null){Undo.RecordObject(light,"Mystic preview light");light.enabled=night>=0;light.intensity=night==1?1.4f:0.3f;}
                foreach(var word in v.nightWords)if(word!=null){Undo.RecordObject(word,"Mystic preview inscription");Color c=word.color;c.a=night==1?0.6f:0;word.color=c;}
                foreach(var r in v.apparitions)if(r!=null){var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);block.SetFloat("_Alpha",night==1?0.12f:0);r.SetPropertyBlock(block);}
                EditorSceneManager.MarkSceneDirty(x.pad.gameObject.scene);SceneView.RepaintAll();
            }
            catch(Exception e){Debug.LogError("MYSTIC FOREST: "+e.Message);}
        }
        internal static TWMysticContext Inspect()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)throw new InvalidOperationException("Останови Play Mode и дождись компиляции.");
            var scene=SceneManager.GetActiveScene();if(!scene.IsValid() || string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Открой сохранённую сцену острова.");
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var pad=all.FirstOrDefault(t=>t.name=="PAD_Forest" && t.gameObject.activeInHierarchy)??all.FirstOrDefault(t=>t.name=="PAD_Forest");
            if(pad==null)throw new InvalidOperationException("Нет метки PAD_Forest.");var state=pad.GetComponent<MysticArtState>();
            var grandpa=all.Select(t=>t.GetComponent<GrandpaVibe>()).Where(t=>t!=null && Vector3.Distance(t.transform.position,pad.position)<35).OrderBy(t=>(t.transform.position-pad.position).sqrMagnitude).FirstOrDefault();
            var elder=state!=null && state.elder!=null?state.elder:grandpa!=null?grandpa.GetComponent<LandmarkTree>():all.Select(t=>t.GetComponent<LandmarkTree>()).Where(t=>t!=null).OrderBy(t=>(t.transform.position-pad.position).sqrMagnitude).FirstOrDefault();
            if(elder==null)throw new InvalidOperationException("Не найден существующий Дедушка с LandmarkTree.");
            var island=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==IslandBlockout.RootName);if(island==null)throw new InvalidOperationException("Нет IslandV2.");
            var manual=island.transform.Find(IslandBlockout.ManualName);var generated=island.transform.Find(IslandBlockout.GeneratedName);
            if(manual==null || generated==null || Vector3.Distance(manual.lossyScale,Vector3.one)>0.001f)throw new InvalidOperationException("Нужны _Manual с масштабом 1 и исходная земля _Generated.");
            var gate=all.FirstOrDefault(t=>t.name=="ForestGate");Vector3 direction=gate!=null?gate.position-elder.transform.position:pad.position-elder.transform.position;direction.y=0;if(direction.sqrMagnitude<0.1f)direction=Vector3.back;
            var x=new TWMysticContext{pad=pad,manual=manual,generated=generated,state=state,elder=elder,gate=gate,center=elder.transform.position,rotation=Quaternion.LookRotation(direction)};
            if(state!=null){x.groundRadiusX=Mathf.Max(18,state.groundRadiusX);x.groundRadiusZ=Mathf.Max(17,state.groundRadiusZ);x.zoneRadius=Mathf.Max(18,state.zoneRadius);}
            x.resources=state!=null && state.trees.Length>0?state.trees.Where(t=>t.target!=null).Select(t=>t.target).ToArray():all.Select(t=>t.GetComponent<ResourceItem>()).Where(r=>r!=null && Vector3.Distance(r.transform.position,elder.transform.position)<27).OrderByDescending(r=>r.gameObject.activeInHierarchy).ThenBy(r=>(r.transform.position-pad.position).sqrMagnitude).Take(6).ToArray();
            x.ReadGround();return x;
        }
        static string Backup(Scene scene)
        {
            string project=Directory.GetParent(Application.dataPath).FullName;
            string dir=Path.Combine(project,"UserSettings/TW_MysticBackups",AssetDatabase.AssetPathToGUID(scene.path));Directory.CreateDirectory(dir);
            string path=Path.Combine(dir,DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+".unity");File.Copy(Path.Combine(project,scene.path),path,false);return path;
        }
    }
}
