using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class OskarModelBuilder
{
    [MenuItem(Menu+"1. Собрать и поставить Оскара",false,290)]
    static void MenuApply(){Apply(null,1,0,false);}
    public static void Apply(Speaker requested,float scale,float offset,bool overridePose)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("OSKAR: останови Play.");return;}
        int undo=-1;
        try
        {
            var scene=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Сначала сохрани игровую сцену.");
            var story=One<StoryFlow>(scene);string role=story!=null?story.oskarName:"Оскар";
            var target=requested!=null?requested:FindTarget(scene,role);
            if(target!=null&&(target.gameObject.scene!=scene||!string.Equals(target.displayName,role,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Выбери Speaker «"+role+"» в текущей сцене. Миру и других NPC сборщик не заменяет.");
            Vector3 position=Vector3.zero;Quaternion rotation=Quaternion.identity;
            if(target==null)ResolvePlacement(story,out position,out rotation);
            // Validate the input before any scene mutation or asset generation.
            OskarModelData.Load();
            Sprite markerSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/CandyKit/Icons/icon_star.png");
            if(target==null&&markerSprite==null)throw new InvalidOperationException("Нет спрайта звезды CandyKit. Сначала установи UI-патч или создай NPC Оскара существующим NPC Builder.");
            string directory="UserSettings/TW_OskarBackups/"+scene.name;Directory.CreateDirectory(directory);
            string backup=directory+"/"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Не удалось сохранить копию сцены.");
            var prefab=BuildPrefab();
            Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Oskar model 01");
            bool created=target==null;
            if(created)
            {
                var go=new GameObject("NPC_Oskar");Undo.RegisterCreatedObjectUndo(go,"Oskar NPC");go.transform.SetPositionAndRotation(position,rotation);
                target=Undo.AddComponent<Speaker>(go);target.displayName=role;target.nameColor=new Color32(143,184,255,255);target.faceNearbyPlayer=true;target.faceRadius=5;
                var col=Undo.AddComponent<CapsuleCollider>(go);col.radius=.32f;col.height=2.17f;col.center=new Vector3(0,1.085f,0);
                var sequence=Undo.AddComponent<DialogueSequence>(go);sequence.trigger=DialogueSequence.Trigger.Manual;sequence.once=true;sequence.radius=3;
                sequence.lines=new[]{new DialogueSequence.Line{text="Погоди, малец. Сначала костёр и разговор с Мирой. Тогда приходи."}};
                var markerObject=new GameObject("Marker");markerObject.transform.SetParent(go.transform,false);Undo.RegisterCreatedObjectUndo(markerObject,"Oskar marker");
                Undo.AddComponent<SpriteRenderer>(markerObject).sprite=markerSprite;sequence.marker=Undo.AddComponent<NpcMarker>(markerObject);
            }
            var states=target.GetComponentsInChildren<OskarModelState>(true);
            if(states.Length>1)throw new InvalidOperationException("Дубли OskarModelState. Верни прежнее оформление перед повторной сборкой.");
            var state=states.Length==1?states[0]:null;
            if(state==null)
            {
                var go=new GameObject("TW_OskarModel");go.transform.SetParent(target.transform,false);Undo.RegisterCreatedObjectUndo(go,"Oskar snapshot");state=Undo.AddComponent<OskarModelState>(go);
            }
            Undo.RegisterCompleteObjectUndo(state,"Oskar model snapshot");state.target=target;state.createdNpc|=created;state.backup=backup;
            if(!state.captured)
            {
                state.captured=true;state.previousTurnRoot=target.turnRoot;state.previousFace=target.faceNearbyPlayer;state.previousBubble=target.bubbleOffset;
                var seq=target.GetComponent<DialogueSequence>();state.marker=seq!=null&&seq.marker!=null?seq.marker:target.GetComponentInChildren<NpcMarker>(true);
                if(state.marker!=null){state.markerPosition=state.marker.transform.localPosition;state.markerSize=state.marker.size;state.markerBob=state.marker.bobHeight;}
            }
            Vector3 localPosition=Vector3.up*offset;Quaternion localRotation=Quaternion.identity;Vector3 localScale=Vector3.one*Mathf.Clamp(scale,.25f,3f);
            if(state.model!=null)
            {
                if(!overridePose){localPosition=state.model.transform.localPosition;localRotation=state.model.transform.localRotation;localScale=state.model.transform.localScale;}
                Undo.DestroyObjectImmediate(state.model);
            }
            foreach(var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.GetComponentInParent<NpcMarker>(true)!=null||renderer is ParticleSystemRenderer||renderer is LineRenderer||renderer is TrailRenderer)continue;
                if(!state.renderers.Exists(s=>s.target==renderer))state.renderers.Add(new OskarModelState.RendererState{target=renderer,enabled=renderer.enabled});
                Undo.RecordObject(renderer,"Oskar old visual");renderer.enabled=false;
            }
            foreach(var behaviour in target.GetComponentsInChildren<Behaviour>(true))
            {
                string name=behaviour.GetType().Name;
                if(!(behaviour is Animator)&&name!="NpcLife"&&name!="MiraMotion"&&name!="MiraPresence"&&name!="MiraLifeSetup"&&name!="OskarIdle")continue;
                if(!state.behaviours.Exists(s=>s.target==behaviour))state.behaviours.Add(new OskarModelState.BehaviourState{target=behaviour,enabled=behaviour.enabled});
                Undo.RecordObject(behaviour,"Oskar old animation");behaviour.enabled=false;
            }
            if(target.GetComponent<DialogueSequence>()==null)
            {
                var seq=Undo.AddComponent<DialogueSequence>(target.gameObject);seq.trigger=DialogueSequence.Trigger.Manual;seq.once=true;seq.radius=3;state.added.Add(seq);
            }
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,target.transform);Undo.RegisterCreatedObjectUndo(instance,"Oskar new model");instance.name=ModelName;
            instance.transform.localPosition=localPosition;instance.transform.localRotation=localRotation;instance.transform.localScale=localScale;state.model=instance;
            Undo.RecordObject(target,"Oskar visual turn root");target.turnRoot=instance.transform;
            float top=target.transform.position.y+1;
            // Mesh-space bounds also work for an NPC that is initially inactive for the story.
            foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh!=null)
            {
                Bounds bounds=filter.sharedMesh.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    top=Mathf.Max(top,filter.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))).y);
            }
            target.bubbleOffset=Vector3.up*(top-target.transform.position.y+.18f);
            if(state.marker!=null)
            {
                Undo.RecordObject(state.marker,"Oskar marker size");Undo.RecordObject(state.marker.transform,"Oskar marker height");
                state.marker.size=.48f;state.marker.bobHeight=.035f;state.marker.transform.position=target.transform.position+target.bubbleOffset+Vector3.up*.28f;
            }
            var errors=InspectModel(instance);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            EditorUtility.SetDirty(state);EditorUtility.SetDirty(target);EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(undo);Selection.activeGameObject=instance;
            Debug.Log("OSKAR MODEL: готово. Модель 01, отдельные суставы/реквизит, белка и скрытая подпись flopy. Старые рендеры сохранены выключенными; существующие диалоги, задания и коллайдеры не менялись. Ctrl+S. Копия сцены: "+backup,target);
        }
        catch(Exception e){if(undo>=0)Undo.RevertAllDownToGroup(undo);Debug.LogError("OSKAR: изменения сцены отменены. "+e);}
    }
    public static Speaker FindTarget(Scene scene,string role)
    {
        Speaker result=null;foreach(var speaker in All<Speaker>(scene))if(string.Equals(speaker.displayName,role,StringComparison.OrdinalIgnoreCase))
        {if(result!=null)throw new InvalidOperationException("Несколько NPC с именем «"+role+"» в сцене. Дубликаты не удаляются автоматически.");result=speaker;}return result;
    }
    static void ResolvePlacement(StoryFlow story,out Vector3 position,out Quaternion rotation)
    {
        if(story==null)throw new InvalidOperationException("Нет Оскара в сцене и нет StoryFlow. Создай NPC или настрой сюжет.");
        if(story.oskarSpot!=null){position=story.oskarSpot.position;rotation=story.oskarSpot.rotation;return;}
        if(story.hut==null)throw new InvalidOperationException("Нет Оскара, Oskar Spot и ссылки на хижину. Назначь StoryFlow.oskarSpot или создай NPC существующим NPC Builder.");
        Physics.SyncTransforms();
        position=story.hut.transform.position+story.hut.transform.right*3.5f;float baseY=position.y,best=float.MaxValue;bool found=false;
        foreach(var hit in Physics.RaycastAll(position+Vector3.up*5,Vector3.down,12,~0,QueryTriggerInteraction.Ignore))
        {
            if(hit.collider==null||hit.collider.gameObject.scene!=story.gameObject.scene||hit.normal.y<.7f||hit.collider.GetComponentInParent<Speaker>()!=null||hit.collider.GetComponentInParent<ResourceItem>()!=null||hit.collider.GetComponentInParent<BuildingController>()!=null)continue;
            float distance=Mathf.Abs(hit.point.y-baseY);if(distance>2||distance>=best)continue;best=distance;position.y=hit.point.y;found=true;
        }
        if(!found)throw new InvalidOperationException("Рядом с хижиной не найден пол. Назначь StoryFlow.oskarSpot в безопасном месте. Модель над пустотой не создаётся.");
        Vector3 direction=story.hut.transform.position-position;direction.y=0;rotation=direction.sqrMagnitude>.01f?Quaternion.LookRotation(direction):Quaternion.identity;
    }
    static List<T> All<T>(Scene scene)where T:Component{var all=new List<T>();foreach(var root in scene.GetRootGameObjects())all.AddRange(root.GetComponentsInChildren<T>(true));return all;}
    static T One<T>(Scene scene)where T:Component
    {T result=null;foreach(var item in All<T>(scene))if(item.gameObject.activeInHierarchy){if(result!=null)throw new InvalidOperationException("Несколько активных "+typeof(T).Name+" в сцене.");result=item;}return result;}
    [MenuItem(Menu+"3. Окно модели",false,292)]
    static void Window(){OskarModelWindow.Open();}
    [MenuItem(Menu+"4. Проверить сцену",false,293)]
    static void QA()
    {
        var states=All<OskarModelState>(SceneManager.GetActiveScene());if(states.Count==0){Debug.LogError("OSKAR QA: сначала поставь модель в сцену.");return;}
        foreach(var state in states)
        {
            var errors=InspectModel(state.model);
            if(state.target==null||state.target.GetComponent<DialogueSequence>()==null)errors.Add("Нет Speaker / DialogueSequence.");
            else if(state.model!=null&&state.target.turnRoot!=state.model.transform)errors.Add("Speaker поворачивает старую модель.");
            foreach(var old in state.behaviours)if(old.target!=null&&old.target.enabled)errors.Add("Включён старый аниматор: "+old.target.GetType().Name);
            foreach(var old in state.renderers)if(old.target!=null&&old.target.enabled)errors.Add("Включён старый рендер: "+old.target.name);
            if(errors.Count>0)Debug.LogError("OSKAR QA: "+string.Join("\n",errors));
            else Debug.Log("OSKAR QA: меши, материалы, риг, реквизит, flopy и ссылки проверены. Итоговый свет, диалоги и подходы проверь в Play Mode.");
        }
    }
    [MenuItem(Menu+"6. Вернуть прежнюю модель",false,295)]
    static void Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("OSKAR: останови Play.");return;}
        var scene=SceneManager.GetActiveScene();Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Restore Oskar model");
        foreach(var state in All<OskarModelState>(scene))
        {
            if(state.createdNpc&&state.target!=null){Undo.DestroyObjectImmediate(state.target.gameObject);continue;}
            if(state.model!=null)Undo.DestroyObjectImmediate(state.model);
            foreach(var renderer in state.renderers)if(renderer.target!=null){Undo.RecordObject(renderer.target,"Restore old Oskar visual");renderer.target.enabled=renderer.enabled;}
            foreach(var behaviour in state.behaviours)if(behaviour.target!=null){Undo.RecordObject(behaviour.target,"Restore old Oskar animation");behaviour.target.enabled=behaviour.enabled;}
            foreach(var component in state.added)if(component!=null)Undo.DestroyObjectImmediate(component);
            if(state.target!=null){Undo.RecordObject(state.target,"Restore Oskar Speaker");state.target.turnRoot=state.previousTurnRoot;state.target.faceNearbyPlayer=state.previousFace;state.target.bubbleOffset=state.previousBubble;}
            if(state.marker!=null){Undo.RecordObject(state.marker,"Restore Oskar marker");Undo.RecordObject(state.marker.transform,"Restore marker position");state.marker.size=state.markerSize;state.marker.bobHeight=state.markerBob;state.marker.transform.localPosition=state.markerPosition;}
            Undo.DestroyObjectImmediate(state.gameObject);
        }
        Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);Debug.Log("OSKAR: прежняя модель и настройки возвращены. Ctrl+S.");
    }
}

public sealed class OskarModelWindow:EditorWindow
{
    Speaker target;float scale=1,offset;
    public static void Open(){var window=GetWindow<OskarModelWindow>(true,"Оскар · модель 01",true);window.minSize=new Vector2(410,280);window.Show();}
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Собирает Оскара по первому референсу. Суставы и реквизит отдельные. flopy спрятан снизу хвоста деревянной белки. Существующие диалоги и коллайдеры сохраняются.",MessageType.Info);
        target=(Speaker)EditorGUILayout.ObjectField("Оскар (пусто = найти)",target,typeof(Speaker),true);
        scale=EditorGUILayout.Slider("Масштаб модели",scale,.5f,2);offset=EditorGUILayout.Slider("Сдвиг по Y",offset,-1,1);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("Собрать и поставить",GUILayout.Height(34)))OskarModelBuilder.Apply(target,scale,offset,true);
            if(GUILayout.Button("Только префаб, без сцены"))try{EditorGUIUtility.PingObject(OskarModelBuilder.BuildPrefab());}catch(Exception e){Debug.LogError(e);}
        }
        EditorGUILayout.Space();EditorGUILayout.LabelField("Префаб: "+OskarModelBuilder.PrefabPath,EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("Полноценную анимацию можно добавить поверх OskarRig; сначала отключи лёгкий OskarIdle.",EditorStyles.wordWrappedMiniLabel);
    }
}
