using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class LeoAnimationBuilder
{
    public const string Menu="Tools/BuilderSim/Игрок/Лео — анимации/";
    public const string Folder="Assets/_Project/Animation/Leo";
    public static readonly string[] Clips =
    {
        "Idle", "Walk", "Run", "HeavyWalk", "HeavyRun", "Dash", "AxeReady", "Chop",
        "Cancel", "Sheathe", "Draw", "IdleHolstered", "Celebrate", "SwimIdle", "Swim", "Fall", "Land"
    };
    public static bool Loop(string name) => name == "Idle" || name == "Walk" || name == "Run" ||
        name == "HeavyWalk" || name == "HeavyRun" || name == "AxeReady" || name == "IdleHolstered" ||
        name == "SwimIdle" || name == "Swim";
    public static PlayerMovement Player()
    {
        PlayerMovement result=null;foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach(var player in root.GetComponentsInChildren<PlayerMovement>(true))if(player.gameObject.activeInHierarchy)
        {if(result!=null)throw new InvalidOperationException("Несколько активных PlayerMovement. Выбери игровую сцену без дублей.");result=player;}
        if(result==null)throw new InvalidOperationException("Нет активного игрока.");return result;
    }
    public static LeoRig Rig(PlayerMovement player)
    {
        var rig=player.model!=null?player.model.GetComponent<LeoRig>():null;
        if(rig==null)throw new InvalidOperationException("Сначала поставь Leo Model01: Лео — модель / 1.");
        Vector3 scale=rig.transform.lossyScale;
        if(scale.x<=0||Mathf.Abs(scale.x-scale.y)>.001f||Mathf.Abs(scale.x-scale.z)>.001f)throw new InvalidOperationException("Нужен положительный равномерный масштаб модели Лео.");
        if(Vector3.Distance(rig.transform.localScale,Vector3.one*LeoModelBuilder.VisualScale)>.001f)
            throw new InvalidOperationException($"Масштаб Лео должен быть {LeoModelBuilder.VisualScale:0.000} по каждой оси. Запусти пункт 0 в меню модели.");
        if(LeoMotionProfile.Load()==null||PlayerInteraction.LeoReadbackVersion!=1)throw new InvalidOperationException("Не полностью скопирован патч анимаций.");return rig;
    }
    [MenuItem(Menu+"1. Подключить анимации",false,370)]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("LEO ANIMATION: останови Play.");return;}int undo=-1;
        try
        {
            var scene=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Сначала сохрани игровую сцену.");
            var player=Player();var rig=Rig(player);var prototype=rig.GetComponent<LeoModelPresentation>();var state=rig.GetComponent<LeoAnimationSetup>();
            var controller=rig.GetComponent<LeoAnimationController>();
            if(controller!=null&&(state==null||state.controller!=controller))throw new InvalidOperationException("На модели есть вручную добавленный контроллер. Удали его или подключи анимации к чистой Model01.");
            string physics=LeoModelBuilder.PhysicsSnapshot(player);var originalAnimator=player.animator;var interaction=player.GetComponent<PlayerInteraction>();
            var eventAnimator=interaction!=null?interaction.animator:null;
            // Build persistent preview assets before changing scene ownership; a failure leaves the scene untouched.
            LeoAnimationClips.Bake();
            string directory="UserSettings/TW_LeoAnimationBackups/"+scene.name;Directory.CreateDirectory(directory);
            string backup=directory+"/"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Не удалось сохранить копию сцены.");
            Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Leo animation setup");
            Undo.RecordObjects(rig.GetComponentsInChildren<Transform>(true),"Leo pose before setup");
            foreach(var bone in rig.GetComponentsInChildren<Transform>(true))if(bone.GetComponent<MeshFilter>()==null)Undo.RecordObject(bone.gameObject,"Leo prop visibility before setup");
            if(state==null)
            {
                state=Undo.AddComponent<LeoAnimationSetup>(rig.gameObject);
                state.previousPresentation=prototype;state.presentationWasEnabled=prototype!=null&&prototype.enabled;
            }
            Undo.RecordObject(state,"Leo animation snapshot");state.rig=rig;state.backup=backup;
            if(prototype!=null){Undo.RecordObject(prototype,"Single Leo pose owner");prototype.enabled=false;}
            if(controller==null)controller=Undo.AddComponent<LeoAnimationController>(rig.gameObject);
            Undo.RecordObject(controller,"Leo animation inputs");controller.rig=rig;controller.player=player;controller.interaction=interaction;
            controller.backpack=player.GetComponent<BackpackTag>();controller.speaker=player.GetComponent<Speaker>();controller.dialogue=UnityEngine.Object.FindFirstObjectByType<DialogueManager>();controller.enabled=true;
            state.controller=controller;
            if(state.trailObject==null)state.trailObject=Trail(rig);
            controller.axeTrail=state.trailObject.GetComponent<TrailRenderer>();
            var errors=Inspect(player,rig);
            if(physics!=LeoModelBuilder.PhysicsSnapshot(player))errors.Add("Изменилась физика игрока.");
            if(player.animator!=originalAnimator||interaction!=null&&interaction.animator!=eventAnimator)errors.Add("Изменился владелец игровых событий.");
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            EditorUtility.SetDirty(state);EditorUtility.SetDirty(controller);Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=rig.gameObject;
            Debug.Log("LEO ANIMATION OK: один владелец позы, ходьба/бег/груз, замах/контакт/возврат, пояс для топора, радость. Generic-клипы в "+Folder+". Сохрани сцену. Меню 2 — QA, 3 — тесты. Копия: "+backup,rig);
        }
        catch(Exception e){if(undo>=0)Undo.RevertAllDownToGroup(undo);Debug.LogError("LEO ANIMATION: подключение отменено. "+e);}
    }
    static GameObject Trail(LeoRig rig)
    {
        var shader=Shader.Find("TimberWings/LeoAxeTrail");if(shader==null)throw new InvalidOperationException("Не найден LeoAxeTrail.shader.");
        EnsureFolder(Folder);string path=Folder+"/Leo_AxeTrail.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader){name="Leo_AxeTrail"};AssetDatabase.CreateAsset(mat,path);}
        var go=new GameObject("Leo_AxeTrail");Undo.RegisterCreatedObjectUndo(go,"Leo axe trail");go.transform.SetParent(rig.axeBladeTip,false);go.layer=rig.gameObject.layer;
        var trail=Undo.AddComponent<TrailRenderer>(go);trail.sharedMaterial=mat;trail.time=.075f;trail.minVertexDistance=.008f;trail.widthMultiplier=.075f;
        trail.widthCurve=AnimationCurve.Linear(0,1,1,0);trail.numCapVertices=2;trail.numCornerVertices=2;trail.textureMode=LineTextureMode.Stretch;trail.emitting=false;
        trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trail.lightProbeUsage=LightProbeUsage.Off;trail.reflectionProbeUsage=ReflectionProbeUsage.Off;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.85f,.48f),0),new GradientColorKey(new Color(1,.66f,.30f),1)},new[]{new GradientAlphaKey(.3f,0),new GradientAlphaKey(0,1)});trail.colorGradient=gradient;return go;
    }
    public static List<string> Inspect(PlayerMovement player,LeoRig rig)
    {
        var errors=new List<string>();var controller=rig.GetComponent<LeoAnimationController>();var state=rig.GetComponent<LeoAnimationSetup>();var old=rig.GetComponent<LeoModelPresentation>();
        if(controller==null||!controller.enabled||controller.rig!=rig||controller.player!=player)errors.Add("Нет правильно подключённого контроллера.");
        if(old!=null&&old.enabled)errors.Add("Одновременно работает старый LeoModelPresentation.");
        if(state==null||state.controller!=controller||state.rig!=rig)errors.Add("Нет снимка подключения.");
        if(controller!=null&&controller.interaction!=player.GetComponent<PlayerInteraction>())errors.Add("Неверная ссылка на рубку.");
        if(rig.GetComponentsInChildren<Animator>(true).Length>0)errors.Add("На процедурной модели появился второй Animator.");
        if(rig.GetComponentsInChildren<Collider>(true).Length>0||rig.GetComponentsInChildren<Rigidbody>(true).Length>0)errors.Add("У визуала не должно быть своей физики.");
        return errors;
    }
    [MenuItem(Menu+"2. Проверить подключение",false,371)]
    static void QA(){try{var player=Player();var errors=Inspect(player,Rig(player));if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));Debug.Log("LEO ANIMATION QA OK: ссылки, единственный владелец позы, отсутствие физики у визуала. Ритм/таймер, диалоги, склон и сохранение проверь в Play.");}catch(Exception e){Debug.LogError("LEO ANIMATION QA: "+e.Message);}}
    [MenuItem(Menu+"4. Пересобрать клипы",false,373)]
    static void BakeMenu(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;try{LeoAnimationClips.Bake();Debug.Log("LEO: клипы пересобраны из тех же поз, что использует игра.");}catch(Exception e){Debug.LogError("LEO CLIPS: "+e);}}
    [MenuItem(Menu+"5. Вернуть базовые движения Model01",false,374)]
    static void Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try
        {
            var rig=Rig(Player());var state=rig.GetComponent<LeoAnimationSetup>();if(state==null)return;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Restore Leo Model01 presentation");
            if(state.controller!=null)Undo.DestroyObjectImmediate(state.controller);
            if(state.trailObject!=null)Undo.DestroyObjectImmediate(state.trailObject);
            if(state.previousPresentation!=null){Undo.RecordObject(state.previousPresentation,"Restore Leo presentation");state.previousPresentation.enabled=state.presentationWasEnabled;}
            Undo.RecordObjects(rig.GetComponentsInChildren<Transform>(true),"Reset Leo pose");rig.ResetPose();rig.EquipHammer(false);
            Undo.DestroyObjectImmediate(state);Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);Debug.Log("LEO: базовая анимация Model01 возвращена. Сохрани сцену.");
        }
        catch(Exception e){Debug.LogError("LEO RESTORE: "+e.Message);}
    }
    public static void EnsureFolder(string path){string[] bits=path.Split('/');string current=bits[0];for(int i=1;i<bits.Length;i++){string next=current+"/"+bits[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,bits[i]);current=next;}}
}
