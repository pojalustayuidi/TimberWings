#if UNITY_EDITOR
// Assets/_Project/Editor/World/CharacterAnimatorBuilder.cs
// Tools > BuilderSim > Аниматор персонажа
// Собирает Animator Controller под скрипты игры: параметры IsMoving и IsChopping (их ставят PlayerMovement и PlayerInteraction).
//   Idle  <->  Run   по IsMoving
//   (из любого)  ->  Chop   пока IsChopping = true, потом назад в Idle
// Указываешь три анимации из пака (Idle, Run, Chop), нажимаешь «Создать». Если выбран объект с Animator, контроллер сразу на него назначается.
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static EditorAssets;

public class CharacterAnimatorBuilder : EditorWindow
{
    AnimationClip idle, run, chop;
    string folder = "Assets/_Project/Animation";
    string fileName = "PlayerAnimator";

    [MenuItem("Tools/BuilderSim/Мир и персонаж/Аниматор персонажа", false, 410)]
    static void Open() { GetWindow<CharacterAnimatorBuilder>("Аниматор персонажа"); }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Перетащи анимации из пака (обычно Assets/LowPolyNature/Animation или внутри FBX персонажа). " +
                                "Для Idle, Run и Chop в настройках клипа включи Loop Time (иначе анимация сыграет один раз).", MessageType.Info);
        idle = (AnimationClip)EditorGUILayout.ObjectField("Idle (стоит)", idle, typeof(AnimationClip), false);
        run = (AnimationClip)EditorGUILayout.ObjectField("Run (бежит)", run, typeof(AnimationClip), false);
        chop = (AnimationClip)EditorGUILayout.ObjectField("Chop (рубит, необязательно)", chop, typeof(AnimationClip), false);
        folder = EditorGUILayout.TextField("Папка", folder);
        fileName = EditorGUILayout.TextField("Имя файла", fileName);

        var sel = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInChildren<Animator>() : null;
        EditorGUILayout.LabelField("Назначить на", sel != null ? sel.name + " (выбран в сцене)" : "ничего не выбрано: контроллер только создастся");

        using (new EditorGUI.DisabledScope(idle == null || run == null))
            if (GUILayout.Button("Создать")) Build(sel);
    }

    void Build(Animator target)
    {
        EnsureFolder(folder);
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName + ".controller");
        var c = AnimatorController.CreateAnimatorControllerAtPath(path);
        c.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        c.AddParameter("IsChopping", AnimatorControllerParameterType.Bool);

        var sm = c.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle");
        sIdle.motion = idle;
        sm.defaultState = sIdle;
        var sRun = sm.AddState("Run");
        sRun.motion = run;

        var t1 = sIdle.AddTransition(sRun);
        Setup(t1); t1.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        var t2 = sRun.AddTransition(sIdle);
        Setup(t2); t2.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        if (chop != null)
        {
            var sChop = sm.AddState("Chop");
            sChop.motion = chop;
            var t3 = sm.AddAnyStateTransition(sChop);
            Setup(t3); t3.canTransitionToSelf = false; t3.AddCondition(AnimatorConditionMode.If, 0f, "IsChopping");
            var t4 = sChop.AddTransition(sIdle);
            Setup(t4); t4.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsChopping");
        }

        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();

        if (target != null)
        {
            Undo.RecordObject(target, "Назначить контроллер");
            target.runtimeAnimatorController = c;
            EditorUtility.SetDirty(target);
        }
        Selection.activeObject = c;
        EditorGUIUtility.PingObject(c);
        Debug.Log("[Аниматор] Создан " + path + (target != null ? " и назначен на " + target.name : ". Назначь его вручную в поле Controller у Animator."));
    }

    static void Setup(AnimatorStateTransition t)
    {
        t.hasExitTime = false;
        t.duration = 0.1f;
    }

}
#endif
