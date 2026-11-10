using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class TWWorkyardArt
    {
        const string Menu="Tools/BuilderSim/Здания/WORKYARD ART/";

        [MenuItem(Menu+"1. Проверить палатку и систему рабочих",false,320)]
        public static void Audit()
        {
            try
            {
                var x=Inspect();Debug.Log("WORKYARD: "+x.tent.name+", корневой масштаб "+x.tent.transform.lossyScale+
                    ". Слотов "+WorkerBalance.CampSlots+", одной роли "+WorkerBalance.MaxPerRole+", склад "+WorkerBalance.BaseLogStock+" брёвен / "+WorkerBalance.BasePlankStock+
                    " досок. Строительных платежей: "+x.build.StepsTotal+". Баланс, этапы оплаты и сохранения не меняются.");
            }
            catch(Exception e){Debug.LogError("WORKYARD: "+e.Message);}
        }

        [MenuItem(Menu+"2. Переделать палатку в рабочий двор",false,321)]
        public static void Apply()
        {
            int group=-1;string folder=null;
            try
            {
                var x=Inspect(requireWorkerManager:true);var scene=x.tent.gameObject.scene;
                if(scene.isDirty && !EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Не удалось сохранить исходную сцену.");
                string backup=Backup(scene);
                Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("TimberWings — рабочий двор артели");
                var previous=x.previous;
                var state=previous!=null?previous:Undo.AddComponent<WorkyardArtState>(x.tent.gameObject);
                Undo.RecordObject(state,"Workyard ownership");
                EnsureWorkers(x,state);
                string before=TWWorkyardValidation.Logic(x);
                if(previous==null)
                {
                    state.oldCurtains=x.tent.curtains;state.oldFlags=x.tent.flagSegs;state.oldSwingers=x.tent.swingers;state.oldLights=x.tent.lights;
                }
                var hidden=state.hidden.Where(h=>h.target!=null).ToList();
                var collisions=state.collisions.Where(c=>c.target!=null).ToList();
                RetireCandidates(x,hidden,collisions);
                var oldSite=state.siteArt;var oldBuilt=state.builtArt;var oldPhysics=state.physicsRoot;
                if(previous!=null)
                    foreach(var art in new[]{oldSite,oldBuilt})if(art!=null)
                        foreach(var h in art.GetComponentsInChildren<WorkyardProp>(true))
                            if(Vector3.Distance(h.transform.position,h.defaultPosition)>0.001f || Quaternion.Angle(h.transform.rotation,h.defaultRotation)>0.05f ||
                                Vector3.Distance(h.transform.lossyScale,h.defaultScale)>0.001f || !h.gameObject.activeSelf)x.keep[h.key]=h;
                folder="Assets/_Project/IslandV2/WorkyardArt/Scenes/"+AssetDatabase.AssetPathToGUID(scene.path)+"/Build_"+Guid.NewGuid().ToString("N").Substring(0,12);
                IslandLayout.EnsureFolder(folder);var assets=new TWWorkyardAssets(folder);
                x.site=ArtRoot(x.tent.stage1.transform,"TW_WORKYARD_SITE");x.built=ArtRoot(x.tent.stage2.transform,"TW_WORKYARD_BUILT");
                Transform abandoned=Child(x.site,"0_Заброшенная_площадка"),frame=Child(x.site,"1_Сборка_каркаса");
                EditorUtility.DisplayProgressBar("TimberWings","Три вида здания, мастерская и склад",0.2f);
                TWWorkyardModels.Site(x,assets,abandoned,frame);TWWorkyardModels.Built(x,assets);
                TWWorkyardGround.Build(x,assets,x.site,"Site_Ground");TWWorkyardGround.Build(x,assets,x.built,"Built_Ground");
                var view=state.view!=null?state.view:Undo.AddComponent<WorkyardView>(x.tent.gameObject);
                Undo.RecordObject(view,"Bind workyard to the existing simulation");
                view.tent=x.tent;view.build=x.build;view.camp=x.camp;view.abandoned=abandoned.gameObject;view.frame=frame.gameObject;view.built=x.built.gameObject;
                view.actorRoot=Child(x.built,"Живые_рабочие");view.stations=x.built.GetComponentsInChildren<WorkyardStation>(true);
                view.stock=x.built.GetComponentsInChildren<WorkyardStockDisplay>(true).Single();
                view.actorPrefabs=new WorkyardActor[3];
                for(int i=0;i<3;i++)view.actorPrefabs[i]=TWWorkyardModels.Actor(assets,(WorkerRole)i);
                foreach(var h in x.keep.Values)
                    if(h!=null && !h.transform.IsChildOf(x.site) && !h.transform.IsChildOf(x.built))
                        Undo.SetTransformParent(h.transform,h.key.StartsWith("Site_",StringComparison.Ordinal)?x.site:x.built,"Preserve manually placed prop");
                state.physicsRoot=TWWorkyardPhysics.Build(x,folder);
                frame.gameObject.SetActive(false);abandoned.gameObject.SetActive(true);
                foreach(var h in hidden){Undo.RecordObject(h.target,"Hide old tent geometry");h.target.SetActive(false);}
                foreach(var c in collisions){Undo.RecordObject(c.target,"Disable old tent collision");c.target.enabled=false;}
                Undo.RecordObject(x.tent,"Retire old visual animation handles");
                x.tent.curtains=Array.Empty<Transform>();x.tent.flagSegs=Array.Empty<Transform>();x.tent.swingers=Array.Empty<Transform>();x.tent.lights=Array.Empty<Light>();
                x.tent.EditorResetGeneratedCache();
                state.siteArt=x.site.gameObject;state.builtArt=x.built.gameObject;state.view=view;state.camp=x.camp;state.manager=x.manager;
                state.hidden=hidden.ToArray();state.collisions=collisions.ToArray();state.folder=folder;state.logicHash=before;
                Physics.SyncTransforms();TWWorkyardValidation.Art(x,state.siteArt,state.builtArt,view,state.physicsRoot);
                if(TWWorkyardValidation.Logic(x)!=before)throw new InvalidOperationException("Изменились настройки стройки, рабочих, события или ресурсные деревья. Операция отменена.");
                if(oldSite!=null)Undo.DestroyObjectImmediate(oldSite);if(oldBuilt!=null)Undo.DestroyObjectImmediate(oldBuilt);
                if(oldPhysics!=null)Undo.DestroyObjectImmediate(oldPhysics);
                EditorUtility.SetDirty(state);EditorUtility.SetDirty(view);EditorUtility.SetDirty(x.tent);AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);Selection.activeGameObject=x.tent.gameObject;
                Debug.Log("WORKYARD: готово. Три визуальных состояния, 9 мест для трёх ролей, до "+WorkerBalance.CampSlots+
                    " активных NPC, склад по данным WorkerManager. Оплата, выработка, цены и Save ID сохранены. Ctrl+S; пункт 4 — QA. Копия сцены: "+backup);
            }
            catch(Exception e)
            {
                if(group>=0)Undo.RevertAllDownToGroup(group);
                if(folder!=null && AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
                Debug.LogError("WORKYARD: установка отменена, прежняя сцена восстановлена. "+e);
            }
            finally{EditorUtility.ClearProgressBar();}
        }

        static Transform Child(Transform parent,string name)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static Transform ArtRoot(Transform parent,string name)
        {
            var root=Child(parent,name);Undo.RegisterCreatedObjectUndo(root.gameObject,"Create staged workyard art");
            Vector3 scale=parent.lossyScale;
            if(Mathf.Min(scale.x,Mathf.Min(scale.y,scale.z))<0.001f)throw new InvalidOperationException("Недопустимый масштаб стадии палатки.");
            root.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);return root;
        }
        static void EnsureWorkers(TWWorkyardContext x,WorkyardArtState state)
        {
            if(x.manager==null)
            {
                var go=new GameObject("WorkerManager");Undo.RegisterCreatedObjectUndo(go,"Create missing worker manager");x.manager=Undo.AddComponent<WorkerManager>(go);state.createdManager=go;
            }
            if(x.manager.database==null)
            {
                var database=AssetDatabase.LoadAssetAtPath<WorkerDatabase>("Assets/_Project/Data/WorkerDatabase.asset");
                if(database==null)throw new InvalidOperationException("Нет WorkerDatabase. Создай базу штатным меню Рабочие → Создать базу рабочих.");
                state.originalDatabase=x.manager.database;state.assignedDatabase=true;Undo.RecordObject(x.manager,"Bind existing worker database");x.manager.database=database;EditorUtility.SetDirty(x.manager);
            }
            if(x.camp==null){x.camp=Undo.AddComponent<WorkerCamp>(x.tent.gameObject);state.createdCamp=true;}
        }
        static bool Owned(Transform t,TWWorkyardContext x)
        {return x.previous!=null && ((x.previous.siteArt!=null && t.IsChildOf(x.previous.siteArt.transform)) || (x.previous.builtArt!=null && t.IsChildOf(x.previous.builtArt.transform)));}
        static void RetireCandidates(TWWorkyardContext x,List<WorkyardArtState.Hidden> hidden,List<WorkyardArtState.Collision> collisions)
        {
            foreach(var stage in new[]{x.tent.stage1,x.tent.stage2})
            {
                foreach(Transform child in stage.transform)
                {
                    if(Owned(child,x) || (x.tent.buildPuff!=null && x.tent.buildPuff.transform.IsChildOf(child)))continue;
                    if(child.GetComponentsInChildren<Canvas>(true).Length!=0)continue;
                    if(child.GetComponentsInChildren<BuildingController>(true).Length!=0 || child.GetComponentsInChildren<ResourceItem>(true).Length!=0 ||
                        child.GetComponentsInChildren<WorkerCamp>(true).Length!=0 || child.GetComponentsInChildren<MapZone>(true).Length!=0 ||
                        (x.tent.buildPoint!=null && x.tent.buildPoint.IsChildOf(child)))
                        throw new InvalidOperationException("Игровые компоненты внутри старого визуального слоя: "+child.name);
                    if(!hidden.Any(h=>h.target==child.gameObject))hidden.Add(new WorkyardArtState.Hidden{target=child.gameObject,active=child.gameObject.activeSelf});
                }
                foreach(var collider in stage.GetComponents<Collider>())
                    if(!collider.isTrigger && !collisions.Any(c=>c.target==collider))collisions.Add(new WorkyardArtState.Collision{target=collider,enabled=collider.enabled});
            }
        }

        [MenuItem(Menu+"3. Вернуть прежнюю палатку",false,322)]
        public static void Rollback()
        {
            int group=-1;
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Останови Play Mode.");
                var scene=SceneManager.GetActiveScene();var states=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WorkyardArtState>(true)).ToArray();
                if(states.Length!=1)throw new InvalidOperationException("Нужен один установленный WORKYARD ART.");
                if(scene.isDirty && !EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Не удалось сохранить сцену.");Backup(scene);
                var state=states[0];var tent=state.GetComponent<TentStages>();
                Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Restore original tent visuals");Undo.RecordObject(tent,"Restore tent visual animation handles");
                tent.curtains=state.oldCurtains;tent.flagSegs=state.oldFlags;tent.swingers=state.oldSwingers;tent.lights=state.oldLights;tent.EditorResetGeneratedCache();
                foreach(var h in state.hidden)if(h.target!=null){Undo.RecordObject(h.target,"Restore old tent geometry");h.target.SetActive(h.active);}
                foreach(var c in state.collisions)if(c.target!=null){Undo.RecordObject(c.target,"Restore tent collision");c.target.enabled=c.enabled;}
                if(state.siteArt!=null)Undo.DestroyObjectImmediate(state.siteArt);if(state.builtArt!=null)Undo.DestroyObjectImmediate(state.builtArt);
                if(state.physicsRoot!=null)Undo.DestroyObjectImmediate(state.physicsRoot);
                if(state.view!=null)Undo.DestroyObjectImmediate(state.view);
                if(state.createdCamp && state.camp!=null)Undo.DestroyObjectImmediate(state.camp);
                if(state.createdManager!=null)Undo.DestroyObjectImmediate(state.createdManager);
                else if(state.assignedDatabase && state.manager!=null){Undo.RecordObject(state.manager,"Restore database binding");state.manager.database=state.originalDatabase;}
                Undo.DestroyObjectImmediate(state);Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("WORKYARD: прежняя палатка возвращена. Ctrl+S. Сохранение игры и баланс не изменялись.");
            }
            catch(Exception e){if(group>=0)Undo.RevertAllDownToGroup(group);Debug.LogError("WORKYARD: "+e.Message);}
        }
        [MenuItem(Menu+"4. Проверить установленную артель",false,323)]
        public static void QA()
        {
            try
            {
                var x=Inspect();var s=x.previous;if(s==null || s.view==null)throw new InvalidOperationException("Сначала пункт 2.");
                TWWorkyardValidation.Art(x,s.siteArt,s.builtArt,s.view,s.physicsRoot);
                foreach(var h in s.hidden)if(h.target!=null && h.target.activeSelf)throw new InvalidOperationException("Включён старый визуальный слой: "+h.target.name);
                if(TWWorkyardValidation.Logic(x)!=s.logicHash)Debug.LogWarning("WORKYARD: настройки или деревья изменены после установки; сравни ручные правки. При установке снимки совпадали.");
                Debug.Log("WORKYARD QA: геометрия, постоянный пол, пологий настил, округлые коллайдеры, роли, склад и деревья NPC проверены. Обход, производство и загрузку проверь в Play Mode.");
            }
            catch(Exception e){Debug.LogError("WORKYARD QA: "+e.Message);}
        }
        [MenuItem(Menu+"5. Показать вид 0 — заброшенный участок",false,324)] static void Preview0(){Preview(0);}
        [MenuItem(Menu+"6. Показать вид 1 — каркас",false,325)] static void Preview1(){Preview(1);}
        [MenuItem(Menu+"7. Показать вид 2 — готовый двор",false,326)] static void Preview2(){Preview(2);}
        [MenuItem(Menu+"9. Исправить пол и коллайдеры — сохранить расстановку",false,328)]
        public static void RepairPhysics()
        {
            int group=-1;string folder=null;
            try
            {
                var x=Inspect();var state=x.previous;
                if(state==null || state.siteArt==null || state.builtArt==null || state.view==null)throw new InvalidOperationException("Сначала установи рабочий двор пунктом 2.");
                var scene=x.tent.gameObject.scene;
                if(scene.isDirty && !EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Не удалось сохранить сцену.");
                string backup=Backup(scene),before=TWWorkyardValidation.Logic(x),workersBefore=TWWorkyardValidation.WorkerSettings(scene);
                Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Repair workyard floor and collision");
                Undo.RecordObject(state,"Store workyard support");var old=state.physicsRoot;
                x.site=state.siteArt.transform;x.built=state.builtArt.transform;
                folder="Assets/_Project/IslandV2/WorkyardArt/Scenes/"+AssetDatabase.AssetPathToGUID(scene.path)+"/Physics_"+Guid.NewGuid().ToString("N").Substring(0,12);
                IslandLayout.EnsureFolder(folder);
                state.physicsRoot=TWWorkyardPhysics.Build(x,folder);
                Physics.SyncTransforms();TWWorkyardValidation.Art(x,state.siteArt,state.builtArt,state.view,state.physicsRoot);
                if(TWWorkyardValidation.Logic(x)!=before || TWWorkyardValidation.WorkerSettings(scene)!=workersBefore)
                    throw new InvalidOperationException("Изменились игровые настройки или один из менеджеров рабочих. Исправление отменено.");
                if(old!=null)Undo.DestroyObjectImmediate(old);
                EditorUtility.SetDirty(state);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);
                Selection.activeGameObject=x.tent.gameObject;
                Debug.Log("WORKYARD FIX: постоянный пол, вход на настил и коллайдеры обновлены. Расстановка и баланс сохранены. Ctrl+S, затем пункты 4 и 8. Копия сцены: "+backup);
            }
            catch(Exception e)
            {
                if(group>=0)Undo.RevertAllDownToGroup(group);
                if(folder!=null && AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
                Debug.LogError("WORKYARD FIX: "+(group>=0?"операция отменена, сцена восстановлена. ":"проверка остановлена до изменения сцены. ")+e);
            }
        }
        static void Preview(int phase)
        {
            try
            {
                var x=Inspect();if(x.previous==null)throw new InvalidOperationException("Сначала пункт 2.");var view=x.previous.view;
                Undo.RecordObjects(new UnityEngine.Object[]{x.tent.stage1,x.tent.stage2,view.abandoned,view.frame},"Preview workyard appearance");
                x.tent.stage1.SetActive(phase<2);x.tent.stage2.SetActive(phase==2);view.abandoned.SetActive(phase==0);view.frame.SetActive(phase==1);
                EditorSceneManager.MarkSceneDirty(x.tent.gameObject.scene);Selection.activeGameObject=x.tent.gameObject;
                SceneView.lastActiveSceneView?.LookAt(x.origin+Vector3.up,x.rotation*Quaternion.Euler(42,180,0),11f);
            }
            catch(Exception e){Debug.LogError("WORKYARD: "+e.Message);}
        }
        // Selection only: never disables, destroys or merges managers. Physics repair can proceed with no unambiguous choice.
        internal static int ChooseWorkerManagerIndex(bool[] active,int savedIndex)
        {
            if(active==null || active.Length==0)return -1;
            bool saved=savedIndex>=0 && savedIndex<active.Length;
            if(saved && active[savedIndex])return savedIndex;
            int count=0,only=-1;
            for(int i=0;i<active.Length;i++)if(active[i]){count++;only=i;}
            if(count==1)return only;
            if(count>1)return -1;
            return saved?savedIndex:active.Length==1?0:-1;
        }
        static TWWorkyardContext Inspect(bool requireWorkerManager=false)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)throw new InvalidOperationException("Останови Play Mode и дождись компиляции.");
            Scene scene=SceneManager.GetActiveScene();if(!scene.IsValid() || string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Открой сохранённую игровую сцену острова.");
            var roots=scene.GetRootGameObjects();var tents=roots.SelectMany(g=>g.GetComponentsInChildren<TentStages>(true)).ToArray();
            if(tents.Length!=1)throw new InvalidOperationException("В сцене должна быть одна палатка с TentStages.");var tent=tents[0];
            if(tent.stage1==null || tent.stage2==null || tent.stage1.transform.parent!=tent.transform || tent.stage2.transform.parent!=tent.transform)throw new InvalidOperationException("Неожиданная структура Stage1/Stage2 палатки.");
            var previous=tent.GetComponent<WorkyardArtState>();
            var managers=roots.SelectMany(g=>g.GetComponentsInChildren<WorkerManager>(true)).ToArray();
            int managerIndex=ChooseWorkerManagerIndex(managers.Select(m=>m.isActiveAndEnabled).ToArray(),previous!=null?Array.IndexOf(managers,previous.manager):-1);
            if(requireWorkerManager && managers.Length>0 && managerIndex<0)
                throw new InvalidOperationException("Для полной установки не удалось выбрать WorkerManager. Исправление пола пунктом 9 доступно без выбора менеджера.");
            WorkerManager manager=managerIndex>=0?managers[managerIndex]:null;
            if(managers.Length>1)Debug.Log("WORKYARD: найдено WorkerManager: "+managers.Length+". "+
                (manager!=null?"Для проверок выбран «"+manager.name+"».":"Геометрия проверяется без выбора менеджера.")+" Все менеджеры и их настройки сохранены.");
            var camps=tent.GetComponentsInChildren<WorkerCamp>(true);if(camps.Length>1)throw new InvalidOperationException("В палатке несколько WorkerCamp.");
            var controllers=tent.GetComponentsInChildren<BuildingController>(true).ToList();
            controllers.AddRange(roots.SelectMany(g=>g.GetComponentsInChildren<BuildStageLink>(true)).Where(l=>l.tent==tent && l.build!=null).Select(l=>l.build));
            foreach(var story in roots.SelectMany(g=>g.GetComponentsInChildren<StoryFlow>(true)))if(story.tent!=null && (story.tent.transform.IsChildOf(tent.transform) || controllers.Count==0))controllers.Add(story.tent);
            var unique=controllers.Distinct().ToArray();if(unique.Length!=1)throw new InvalidOperationException("Не найден единственный существующий BuildingController палатки.");
            var island=roots.SingleOrDefault(g=>g.name==IslandBlockout.RootName);if(island==null)throw new InvalidOperationException("Нет IslandV2.");
            var x=new TWWorkyardContext{tent=tent,build=unique[0],camp=camps.FirstOrDefault(),manager=manager,previous=previous,
                groundRoot=island.transform.Find(IslandBlockout.GeneratedName),origin=tent.transform.position,rotation=tent.transform.rotation};
            if(x.groundRoot==null)throw new InvalidOperationException("Нет _Generated с поверхностью земли.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(IslandBlockout.MaterialPath);
            if(material==null || !material.HasProperty("_GroundTex") || material.GetTexture("_GroundTex")==null || Shader.Find("Project/CandyToon")==null)throw new InvalidOperationException("Нет исходных материалов земли / CandyToon.");
            foreach(var ri in roots.SelectMany(g=>g.GetComponentsInChildren<ResourceItem>(true)))
            {
                Vector3 p=Quaternion.Inverse(x.rotation)*(ri.transform.position-x.origin);
                if(new Vector2(p.x,p.z).magnitude<9f)x.resourceTrees.Add(new Vector2(p.x,p.z));
            }
            x.ReadGround();return x;
        }
        static string Backup(Scene scene)
        {
            string project=Directory.GetParent(Application.dataPath).FullName;
            string dir=Path.Combine(project,"UserSettings/TW_WorkyardBackups",AssetDatabase.AssetPathToGUID(scene.path));Directory.CreateDirectory(dir);
            string p=Path.Combine(dir,DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")+".unity");File.Copy(Path.Combine(project,scene.path),p,false);return p;
        }
    }
}
