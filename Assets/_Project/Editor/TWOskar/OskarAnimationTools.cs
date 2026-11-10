using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class OskarAnimationTools
{
    public const string Menu="Tools/BuilderSim/NPC/Оскар — анимации/";
    [MenuItem(Menu+"1. Подключить анимации",false,310)]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("OSKAR MOTION: останови Play.");return;}
        int undo=-1;
        try
        {
            var scene=SceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(scene.path)||!scene.path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Открой сохранённую игровую сцену.");
            var story=One<StoryFlow>(scene);string name=story!=null?story.oskarName:"Оскар";Speaker speaker=null;
            foreach(var s in All<Speaker>(scene))if(string.Equals(s.displayName,name,StringComparison.OrdinalIgnoreCase))
            {if(speaker!=null)throw new InvalidOperationException("Несколько NPC с именем "+name+". Дубли не удаляются автоматически.");speaker=s;}
            if(speaker==null)throw new InvalidOperationException("Нет Оскара. Сначала поставь модель 01 предыдущим сборщиком.");
            OskarRig rig=null;foreach(var r in speaker.GetComponentsInChildren<OskarRig>(true))if(r.gameObject.activeSelf)
            {if(rig!=null)throw new InvalidOperationException("Несколько моделей OskarRig. Оставь одну актуальную модель.");rig=r;}
            if(rig==null||rig.handL==null||rig.pencil==null||rig.pocket==null||rig.blueprint==null)throw new InvalidOperationException("Нужна полная модель 01 с ригом и реквизитом.");
            var book=OskarMotionProfile.Load();if(book==null||book.clips==null||book.clips.Length!=5)throw new InvalidOperationException("Нет пяти жестов Motion.json. Скопируй Assets целиком.");
            string directory="UserSettings/TW_OskarAnimationBackups/"+scene.name;Directory.CreateDirectory(directory);
            string backup=directory+"/"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Не удалось сохранить копию сцены.");
            Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Oskar procedural animations");
            var states=speaker.GetComponentsInChildren<OskarAnimationState>(true);if(states.Length>1)throw new InvalidOperationException("Дубли OskarAnimationState.");
            var state=states.Length==1?states[0]:null;
            if(state==null){var go=new GameObject("TW_OskarAnimation");go.transform.SetParent(speaker.transform,false);Undo.RegisterCreatedObjectUndo(go,"Oskar animation snapshot");state=Undo.AddComponent<OskarAnimationState>(go);}
            Undo.RegisterCompleteObjectUndo(state,"Oskar animation snapshot");
            if(state.rig!=null&&state.rig!=rig){state.captured=false;state.workingPencil=null;state.disabled.Clear();}
            state.rig=rig;state.backup=backup;
            var actor=rig.GetComponent<OskarIdle>();bool created=actor==null;if(created)actor=Undo.AddComponent<OskarIdle>(rig.gameObject);
            if(!state.captured)
            {
                state.captured=true;state.createdActor=created;state.previousEnabled=actor.enabled;state.previousPencil=actor.workingPencil;
                state.previousSpeaker=actor.speaker;state.previousDialogue=actor.dialogue;state.previousManager=actor.manager;
            }
            state.actor=actor;
            foreach(var behaviour in speaker.GetComponentsInChildren<Behaviour>(true))
            {
                if(behaviour==actor||behaviour==rig)continue;
                if(!(behaviour is Animator)&&behaviour.GetType().Name!="NpcLife"&&behaviour.GetType().Name!="MiraMotion")continue;
                if(!state.disabled.Exists(s=>s.target==behaviour))state.disabled.Add(new OskarAnimationState.BehaviourState{target=behaviour,enabled=behaviour.enabled});
                Undo.RecordObject(behaviour,"Oskar pose owner");behaviour.enabled=false;
            }
            if(state.workingPencil==null)
            {
                var clone=Object.Instantiate(rig.pencil.gameObject,rig.pocket);Undo.RegisterCreatedObjectUndo(clone,"Oskar working pencil");clone.name="WorkingPencil";
                clone.transform.position=rig.transform.TransformPoint(new Vector3(-.29f,1.00f,.345f));clone.transform.localRotation=Quaternion.Euler(0,0,-12);clone.transform.localScale=Vector3.one*.6f;
                state.workingPencil=clone;
            }
            Undo.RecordObject(actor,"Oskar motion settings");actor.rig=rig;actor.speaker=speaker;actor.dialogue=speaker.GetComponent<DialogueSequence>();actor.manager=One<DialogueManager>(scene);actor.workingPencil=state.workingPencil.transform;
            actor.idleActivities=actor.greet=actor.conversationGestures=true;actor.enabled=true;
            Undo.RecordObject(rig,"Oskar rest pose");rig.CaptureRest();
            EditorUtility.SetDirty(actor);EditorUtility.SetDirty(rig);EditorUtility.SetDirty(state);
            var errors=Inspect(state);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=rig.gameObject;
            Debug.Log("OSKAR MOTION: подключены приветствие, рубанок, рабочий карандаш/чертёж, раздумье, поправка белки и жесты разговора. Сюжет не менялся. Ctrl+S. Копия сцены: "+backup,actor);
        }
        catch(Exception e){if(undo>=0)Undo.RevertAllDownToGroup(undo);Debug.LogError("OSKAR MOTION: изменения сцены отменены. "+e);}
    }
    public static List<string> Inspect(OskarAnimationState state)
    {
        var errors=new List<string>();if(state==null||state.actor==null||state.rig==null){errors.Add("Нет контроллера / рига.");return errors;}
        if(!state.actor.enabled)errors.Add("OskarIdle выключен.");
        if(state.workingPencil==null||state.actor.workingPencil==null)errors.Add("Нет рабочего карандаша.");
        else if(state.workingPencil.GetComponentsInChildren<Collider>(true).Length>0)errors.Add("У рабочего карандаша появился коллайдер.");
        if(state.actor.speaker==null||state.actor.dialogue==null)errors.Add("Нет ссылок на существующего собеседника / диалог.");
        if(state.rig.GetComponents<OskarIdle>().Length!=1)errors.Add("Дубли контроллера позы.");
        foreach(var b in state.disabled)if(b.target!=null&&b.target.enabled)errors.Add("Другой контроллер одновременно пишет позу: "+b.target.GetType().Name);
        return errors;
    }
    [MenuItem(Menu+"2. Проверить подключение",false,311)]
    static void QA()
    {
        var states=All<OskarAnimationState>(SceneManager.GetActiveScene());if(states.Count==0){Debug.LogError("OSKAR MOTION QA: сначала подключи анимации пунктом 1.");return;}
        foreach(var state in states){var errors=Inspect(state);if(errors.Count>0)Debug.LogError("OSKAR MOTION QA: "+string.Join("\n",errors));else Debug.Log("OSKAR MOTION QA: ссылки, карандаш и один владелец позы проверены. Переходы, жесты и камеру проверь в Play Mode.");}
    }
    [MenuItem(Menu+"4. Оставить только базовое оживление",false,313)]
    static void Baseline()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("OSKAR MOTION: останови Play.");return;}
        var scene=SceneManager.GetActiveScene();Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Oskar basic idle");
        foreach(var state in All<OskarAnimationState>(scene))
        {
            if(state.actor!=null)
            {
                Undo.RecordObject(state.actor,"Oskar basic idle");state.actor.Restore();state.actor.idleActivities=state.actor.greet=state.actor.conversationGestures=false;
                state.actor.workingPencil=state.previousPencil;state.actor.speaker=state.previousSpeaker;state.actor.dialogue=state.previousDialogue;state.actor.manager=state.previousManager;state.actor.enabled=state.previousEnabled;
            }
            if(state.workingPencil!=null)Undo.DestroyObjectImmediate(state.workingPencil);
            foreach(var b in state.disabled)if(b.target!=null){Undo.RecordObject(b.target,"Restore pose owner");b.target.enabled=b.enabled;}
            if(state.createdActor&&state.actor!=null)Undo.DestroyObjectImmediate(state.actor);
            if(state.rig!=null){Undo.RecordObject(state.rig,"Oskar basic rest");state.rig.CaptureRest();}
            Undo.DestroyObjectImmediate(state.gameObject);
        }
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Debug.Log("OSKAR MOTION: дополнительные действия выключены, базовое оживление оставлено. Ctrl+S.");
    }
    static List<T> All<T>(Scene scene)where T:Component{var result=new List<T>();foreach(var root in scene.GetRootGameObjects())result.AddRange(root.GetComponentsInChildren<T>(true));return result;}
    static T One<T>(Scene scene)where T:Component{T result=null;foreach(var item in All<T>(scene))if(item.gameObject.activeInHierarchy){if(result!=null)throw new InvalidOperationException("Несколько активных "+typeof(T).Name+".");result=item;}return result;}
}
