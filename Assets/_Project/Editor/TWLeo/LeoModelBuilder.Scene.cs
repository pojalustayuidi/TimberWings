using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class LeoModelBuilder
{
    // Midpoint of the measured Oskar/Mira heights in Recovery 0 (8), divided
    // by the height of Leo v3.1 from the soles to the curl.
    public const float VisualScale = .524f;

    [MenuItem(Menu + "0. Подогнать размер Лео v3.1", false, 349)]
    public static void FitV31InScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("LEO v3.1: останови Play.");
            return;
        }

        try
        {
            Scene scene = SceneManager.GetActiveScene();
            PlayerMovement player = Player(scene);
            Transform visual = player != null ? player.model : null;
            LeoRig rig = visual != null ? visual.GetComponent<LeoRig>() : null;
            var source = rig != null ? PrefabUtility.GetCorrespondingObjectFromSource(rig.gameObject) : null;
            if (rig == null || source == null || AssetDatabase.GetAssetPath(source) != PrefabPath ||
                rig.HammerHolster == null || !rig.HasRest)
            {
                throw new InvalidOperationException("Открой игровую сцену с установленным префабом Leo v3.1.");
            }

            var capsule = player.GetComponent<CapsuleCollider>();
            if (capsule == null || !capsule.enabled || capsule.isTrigger || capsule.direction != 1)
                throw new InvalidOperationException("У игрока нет активной вертикальной капсулы для расчёта точки опоры.");

            var soles = new List<MeshRenderer>();
            foreach (Transform ankle in new[] { rig.ankleL, rig.ankleR })
            {
                if (ankle == null) throw new InvalidOperationException("Не найден узел ботинка.");
                foreach (var renderer in ankle.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.enabled && renderer.name.EndsWith("__Sole", StringComparison.Ordinal))
                        soles.Add(renderer);
                }
            }
            if (soles.Count == 0) throw new InvalidOperationException("Не найдены видимые подошвы Лео.");

            Vector3 targetScale = Vector3.one * VisualScale;
            float supportY = capsule.bounds.min.y;
            if (Vector3.Distance(visual.localScale, targetScale) > .00001f ||
                Mathf.Abs(SoleHeight(soles) - supportY) > .00001f)
            {
                Undo.RecordObject(visual, "Fit Leo v3.1 visual to player support");
                visual.localScale = targetScale;
                visual.position += Vector3.up * (supportY - SoleHeight(soles));
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Selection.activeGameObject = visual.gameObject;
            Debug.Log($"LEO v3.1: масштаб {VisualScale:0.000}. Подошвы на уровне опоры капсулы. Сохрани сцену (Ctrl+S).", visual);
        }
        catch (Exception e)
        {
            Debug.LogError("LEO v3.1: " + e.Message);
        }
    }

    static float SoleHeight(List<MeshRenderer> soles)
    {
        float lowest = float.PositiveInfinity;
        foreach (var sole in soles)
            lowest = Mathf.Min(lowest, sole.bounds.min.y);
        return lowest;
    }
    [MenuItem(Menu+"1. Собрать и поставить Лео",false,350)]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("LEO: останови Play.");return;}
        int undo=-1;
        try
        {
            var scene=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Открой сохранённую игровую сцену.");
            var player=Player(scene);if(player==null)throw new InvalidOperationException("Нет активного PlayerMovement.");
            var states=player.GetComponents<LeoModelState>();if(states.Length>1)throw new InvalidOperationException("На игроке несколько LeoModelState.");
            var state=states.Length==1?states[0]:null;var data=LeoModelData.Load();
            Vector3 position,scale;Quaternion rotation;
            if(state!=null&&state.model!=null){position=state.model.transform.localPosition;rotation=state.model.transform.localRotation;scale=Vector3.one*VisualScale;}
            else Fit(player,state!=null&&state.previousModel!=null?state.previousModel:player.model,data,out position,out rotation,out scale);
            string physics=PhysicsSnapshot(player);
            var prefab=BuildPrefab();
            string directory="UserSettings/TW_LeoModelBackups/"+scene.name;Directory.CreateDirectory(directory);
            string backup=directory+"/"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Не удалось сохранить копию сцены.");
            Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Leo procedural player model");
            if(state==null)state=Undo.AddComponent<LeoModelState>(player.gameObject);
            Undo.RecordObject(state,"Leo model snapshot");state.player=player;state.backup=backup;
            if(!state.captured)
            {
                state.captured=true;state.previousModel=player.model;state.speaker=player.GetComponent<Speaker>();
                state.previousTurnRoot=state.speaker!=null?state.speaker.turnRoot:null;
            }
            if(state.model!=null)Undo.DestroyObjectImmediate(state.model);
            var legacy=state.previousModel;
            if(legacy!=null)
            {
                foreach(var renderer in legacy.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer is ParticleSystemRenderer||renderer is LineRenderer||renderer is TrailRenderer||renderer.GetComponentInParent<Canvas>(true)!=null)continue;
                    if(!state.renderers.Exists(r=>r.target==renderer))state.renderers.Add(new LeoModelState.RendererState{target=renderer,enabled=renderer.enabled});
                    Undo.RecordObject(renderer,"Leo old visual");renderer.enabled=false;
                }
            }
            var interaction=player.GetComponent<PlayerInteraction>();
            var animators=new HashSet<Animator>();if(player.animator!=null)animators.Add(player.animator);if(interaction!=null&&interaction.animator!=null)animators.Add(interaction.animator);
            foreach(var animator in animators)
            {
                if(!state.animators.Exists(a=>a.target==animator))state.animators.Add(new LeoModelState.AnimatorState{target=animator,culling=animator.cullingMode,rootMotion=animator.applyRootMotion});
                Undo.RecordObject(animator,"Leo legacy event bridge");animator.applyRootMotion=false;
                if(interaction!=null&&interaction.useAnimationEvents)animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            }
            // Keep the original Animator/PlayerAnimationEvents references: they still own hit timing.
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,player.transform);Undo.RegisterCreatedObjectUndo(instance,"Leo player visual");instance.name=ModelName;
            instance.transform.localPosition=position;instance.transform.localRotation=rotation;instance.transform.localScale=scale;state.model=instance;
            foreach(var t in instance.GetComponentsInChildren<Transform>(true))t.gameObject.layer=player.gameObject.layer;
            Undo.RecordObject(player,"Leo player model reference");player.model=instance.transform;
            var motion=instance.GetComponent<LeoModelPresentation>();motion.player=player;motion.interaction=interaction;motion.backpack=player.GetComponent<BackpackTag>();motion.speaker=player.GetComponent<Speaker>();
            if(state.speaker!=null&&state.previousTurnRoot!=null&&legacy!=null&&(state.previousTurnRoot==legacy||state.previousTurnRoot.IsChildOf(legacy)))
            {Undo.RecordObject(state.speaker,"Leo speaking visual");state.speaker.turnRoot=instance.transform;}
            var errors=InspectModel(instance);if(player.model!=instance.transform)errors.Add("Не обновилась ссылка PlayerMovement.model.");
            if(physics!=PhysicsSnapshot(player))errors.Add("Изменилась физика игрока. Замена отменена.");
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            EditorUtility.SetDirty(player);EditorUtility.SetDirty(state);EditorUtility.SetDirty(motion);Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=instance;
            Debug.Log($"LEO MODEL: масштаб {VisualScale:0.000}, полка с зацепками для брёвен вместо рюкзака, суставы, топор TW и компас. Старые игровые события сохранены. Ctrl+S. Копия сцены: "+backup,instance);
        }
        catch(Exception e){if(undo>=0)Undo.RevertAllDownToGroup(undo);Debug.LogError("LEO: изменения сцены отменены. "+e);}
    }
    static void Fit(PlayerMovement player,Transform previous,LeoModelData.Document data,out Vector3 position,out Quaternion rotation,out Vector3 scale)
    {
        // Match the visible legacy body, not the collision capsule (the current capsule is shorter than the character).
        var renderers=new List<Renderer>();if(previous!=null)
        {
            foreach(var renderer in previous.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(renderer.enabled)renderers.Add(renderer);
            if(renderers.Count==0)foreach(var renderer in previous.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.enabled&&renderer.GetComponentInParent<Canvas>(true)==null)renderers.Add(renderer);
        }
        bool found=false;float minimum=float.MaxValue,maximum=float.MinValue;
        foreach(var renderer in renderers)
        {
            Bounds bounds;Matrix4x4 matrix;
            var skinned=renderer as SkinnedMeshRenderer;
            if(skinned!=null){bounds=skinned.localBounds;matrix=player.transform.worldToLocalMatrix*skinned.transform.localToWorldMatrix;}
            else
            {
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                bounds=filter.sharedMesh.bounds;matrix=player.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            }
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                float py=matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))).y;minimum=Mathf.Min(minimum,py);maximum=Mathf.Max(maximum,py);found=true;
            }
        }
        float height=found?maximum-minimum:data.height;
        if(float.IsNaN(height)||float.IsInfinity(height)||height<.4f||height>6f)throw new InvalidOperationException("Не удалось определить высоту старого визуала. Сначала собери префаб пунктом 2 и поставь его вручную.");
        float factor=VisualScale;scale=Vector3.one*VisualScale;
        Vector3 previousPosition=previous!=null?player.transform.InverseTransformPoint(previous.position):Vector3.zero;
        position=new Vector3(previousPosition.x,(found?minimum:0)-data.feet*factor,previousPosition.z);
        Vector3 forward=previous!=null?player.transform.InverseTransformDirection(previous.forward):Vector3.forward;forward.y=0;
        rotation=forward.sqrMagnitude>.001f?Quaternion.LookRotation(forward):Quaternion.identity;
    }
    public static string PhysicsSnapshot(PlayerMovement player)
    {
        var data=new System.Text.StringBuilder();foreach(var body in player.GetComponentsInChildren<Rigidbody>(true))data.Append(EditorJsonUtility.ToJson(body));
        foreach(var collider in player.GetComponentsInChildren<Collider>(true))data.Append(EditorJsonUtility.ToJson(collider));return data.ToString();
    }
    static PlayerMovement Player(Scene scene)
    {
        PlayerMovement player=null;foreach(var root in scene.GetRootGameObjects())foreach(var found in root.GetComponentsInChildren<PlayerMovement>(true))if(found.gameObject.activeInHierarchy)
        {if(player!=null)throw new InvalidOperationException("Несколько активных PlayerMovement. Дубли не удаляются автоматически.");player=found;}return player;
    }
    [MenuItem(Menu+"3. Проверить модель в сцене",false,352)]
    public static void QA()
    {
        try
        {
            var player=Player(SceneManager.GetActiveScene());var state=player!=null?player.GetComponent<LeoModelState>():null;
            if(state==null||state.model==null)throw new InvalidOperationException("Сначала поставь модель пунктом 1.");
            var errors=InspectModel(state.model);if(player.model!=state.model.transform)errors.Add("PlayerMovement.model указывает на другой объект.");
            if(Vector3.Distance(state.model.transform.localScale,Vector3.one*VisualScale)>.0001f)errors.Add($"Масштаб Лео должен быть {VisualScale:0.000} по каждой оси. Запусти пункт 0.");
            foreach(var item in state.renderers)if(item.target!=null&&item.target.enabled)errors.Add("Старый визуал включён одновременно с Лео: "+item.target.name);
            if(state.model.GetComponent<LeoModelPresentation>()==null)errors.Add("Нет визуального моста.");
            if(errors.Count>0)Debug.LogError("LEO QA: "+string.Join("\n",errors));else Debug.Log("LEO QA OK: суставы, пальцы, реквизит, материалы и ссылки. Движение, рубку, камеру и сохранения проверь в Play Mode.");
        }
        catch(Exception e){Debug.LogError("LEO QA: "+e.Message);}
    }
    [MenuItem(Menu+"4. Вернуть прежнюю модель игрока",false,353)]
    public static void Revert()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;var scene=SceneManager.GetActiveScene();var player=Player(scene);var state=player!=null?player.GetComponent<LeoModelState>():null;if(state==null)return;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Restore previous player visual");
        Undo.RecordObject(player,"Restore player model");player.model=state.previousModel;
        if(state.speaker!=null){Undo.RecordObject(state.speaker,"Restore speaker visual");state.speaker.turnRoot=state.previousTurnRoot;}
        foreach(var r in state.renderers)if(r.target!=null){Undo.RecordObject(r.target,"Restore old visual");r.target.enabled=r.enabled;}
        foreach(var a in state.animators)if(a.target!=null){Undo.RecordObject(a.target,"Restore old animation settings");a.target.cullingMode=a.culling;a.target.applyRootMotion=a.rootMotion;}
        if(state.model!=null)Undo.DestroyObjectImmediate(state.model);Undo.DestroyObjectImmediate(state);Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Debug.Log("LEO: прежний визуал возвращён. Ctrl+S.");
    }
}
