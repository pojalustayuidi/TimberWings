// Assets/_Project/Scripts/Editor/HutV2/LumberjackHutBuilder.cs   (ТОЛЬКО в папке Editor!)
// Генератор «Хижина лесоруба v2» + пилорама, три стадии под стиль Cloud Candy.
//
// ЗАПУСК: Tools > BuilderSim > Хижина лесоруба v2 > Окно генератора. Выбери хижину (объект с HutStages), нажми «Собрать».
//
// Что делает:
//   • прячет старые Stage1..3 внутрь неактивного «OldStages_Backup» (их можно вернуть кнопкой);
//   • собирает новые Stage1 (нулевая: двор в заборе, материалы разобраны), Stage2 (каркас, скелет пилорамы), Stage3 (готово);
//   • вешает HutLife (дым, флаги, фонари), SawmillAnimator (диск, каретка, опилки), StageVisibility (уход «нулевых» материалов);
//   • настраивает HutStages.Configure, BuildPoint и PlankWorkshop.interactPoint (E между хижиной и пилорамой).
//
// Раскладка (локальные метры, хижина в нуле, дверь смотрит на +Z, пилорама справа на +X; поворот: параметр «Поворот»):
//   хижина 4.4 x 3.8 м; настил пилорамы x 3.9..9.1; забор x -5.6..10.6, z -4.6..4.8; ворота по центру x = 2.4 на фронте.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class LumberjackHutBuilder
{
    public class Options
    {
        public float scale = 1f;            // общий масштаб (1 = метры как в проекте)
        public float yaw = 0f;              // поворот всей постройки вокруг Y
        public float groundY = 0f;          // высота земли относительно корня хижины
        public int seed = 7;                // зерно случайности: одно число = одна и та же раскладка
        public bool keepOld = true;         // старые стадии убрать в backup, а не удалять
        public bool mossyRoof = true;       // мох на крыше
        public bool fenceColliders = false; // забор непроходимый (по умолчанию декор, чтобы не мешать навигации)
    }

    static Options O = new Options();

    // ---------------------------------------------------------------- раскладка
    const float W = 4.4f, D = 3.8f;                 // хижина по X и по Z
    const float FoundH = 0.28f;                     // высота каменного основания
    const float Course = 0.30f, LogR = 0.17f;       // шаг и радиус венца
    const int CoursesStage2 = 5, CoursesTotal = 8;
    const float EaveY = 2.55f, Rise = 1.7f, RoofLen = 5.5f;
    const float SawX0 = 3.9f, SawX1 = 9.1f, SawZ = -0.3f;
    const int DeckN = 7;
    const float FenceX0 = -5.6f, FenceX1 = 10.6f, FenceZ0 = -4.6f, FenceZ1 = 4.8f;
    const float GateX = 2.4f, GateW = 2.4f;

    static float YC(int c) { return FoundH + 0.15f + c * Course; }

    // ---------------------------------------------------------------- реестр «жизни» текущей стадии
    static readonly List<HutLife.ClothChain> cloths = new List<HutLife.ClothChain>();
    static readonly List<Transform> swingers = new List<Transform>();
    static Transform smokePoint;
    static readonly List<StageVisibility> pendingFlyTarget = new List<StageVisibility>();

    static GameObject BeginStage(Transform root, string name)
    {
        cloths.Clear();
        swingers.Clear();
        smokePoint = null;
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(0f, O.yaw, 0f);
        go.transform.localScale = Vector3.one * O.scale;
        new GameObject("__HutV2").transform.SetParent(go.transform, false);   // метка: «это сделал генератор v2» (повторная сборка заменяет, а не прячет)
        return go;
    }

    static void EndStage(GameObject s, int n)
    {
        if (cloths.Count > 0 || swingers.Count > 0 || smokePoint != null)
        {
            var hl = s.AddComponent<HutLife>();
            hl.cloths = cloths.ToArray();
            hl.swingers = swingers.ToArray();
            if (smokePoint != null)
            {
                hl.smokePoint = smokePoint;
                hl.puffMesh = HutMeshLib.Blob(1);
                hl.smokeMat = Mat("HutSmoke");
            }
        }
        cloths.Clear();
        swingers.Clear();
        smokePoint = null;
    }

    // ---------------------------------------------------------------- сборка
    public static bool Build(HutStages hs, Options opt)
    {
        if (hs == null) { EditorUtility.DisplayDialog("Хижина v2", "Не выбрана хижина (объект с HutStages).", "OK"); return false; }
        O = opt ?? new Options();
        rng = new System.Random(O.seed);
        pendingFlyTarget.Clear();
        HutMeshLib.EnsureFolders();
        HutMeshLib.ClearCache();
        mats.Clear();

        var root = hs.transform;
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Хижина лесоруба v2: сборка");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            EditorUtility.DisplayProgressBar("Хижина v2", "Убираю старые стадии", 0.05f);
            RetireOldStages(hs);

            EditorUtility.DisplayProgressBar("Хижина v2", "Стадия 1: двор, забор, разложенные материалы", 0.2f);
            Transform s1 = BuildStage1(root);
            var fly = Obj(s1, "FlyTarget", new Vector3(0f, 1.4f, 0f)).transform;

            EditorUtility.DisplayProgressBar("Хижина v2", "Стадия 2: каркас и скелет пилорамы", 0.5f);
            Transform s2 = BuildStage2(root);

            EditorUtility.DisplayProgressBar("Хижина v2", "Стадия 3: хижина, крыша, пилорама", 0.75f);
            Transform s3 = BuildStage3(root);

            for (int i = 0; i < pendingFlyTarget.Count; i++)
                if (pendingFlyTarget[i] != null) pendingFlyTarget[i].flyTarget = fly;
            pendingFlyTarget.Clear();

            // BuildPoint: внутри ворот, лицом к хижине
            var bp = hs.BuildPoint != null ? hs.BuildPoint : null;
            if (bp == null)
            {
                var bgo = new GameObject("BuildPoint");
                bgo.transform.SetParent(root, false);
                bp = bgo.transform;
            }
            bp.position = s1.TransformPoint(new Vector3(GateX - 0.9f, 0f, 3.5f));
            bp.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(s1.position - bp.position, Vector3.up).normalized == Vector3.zero ? s1.forward : Vector3.ProjectOnPlane(s1.position - bp.position, Vector3.up).normalized, Vector3.up);

            hs.Configure(s1.gameObject, s2.gameObject, s3.gameObject, bp, 1);

            // E у пилорамы: точка взаимодействия между хижиной и пилой
            var ws = hs.GetComponentInParent<PlankWorkshop>(true);
            if (ws == null) ws = hs.GetComponentInChildren<PlankWorkshop>(true);
            if (ws != null)
            {
                Transform ip = ws.interactPoint;
                if (ip == null || ip.parent != root)
                {
                    var igo = new GameObject("SawInteractPoint");
                    igo.transform.SetParent(root, false);
                    ip = igo.transform;
                    Undo.RegisterCreatedObjectUndo(igo, "SawInteractPoint");
                }
                ip.position = s1.TransformPoint(new Vector3(3.25f, 0f, 2.1f));
                Undo.RecordObject(ws, "PlankWorkshop.interactPoint");
                ws.interactPoint = ip;
                EditorUtility.SetDirty(ws);
            }
            else
            {
                Debug.LogWarning("[HutV2] PlankWorkshop у этой хижины не найден: E у пилорамы останется как было. Если пила висит на другом объекте, назначь ему «Interact Point» = SawInteractPoint вручную.");
            }

            // показать стадию 3 для проверки
            hs.SetStage(3);

            EditorUtility.SetDirty(hs);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("[HutV2] Готово. Стадии: Stage1 (нулевая), Stage2 (каркас), Stage3 (готово). Проверка: правый клик по HutStages > «Тест: показать этап N». Меши и материалы: " + HutMeshLib.Root);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[HutV2] Сборка прервана: " + e);
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // старые стадии: чужие прячем в OldStages_Backup (неактивный), стадии v2 (с меткой) удаляем
    static void RetireOldStages(HutStages hs)
    {
        var root = hs.transform;
        for (int n = 1; n <= 3; n++)
        {
            var go = hs.GetStageObject(n);
            if (go == null) continue;
            if (go.transform.Find("__HutV2") != null)
            {
                Undo.DestroyObjectImmediate(go);
                continue;
            }
            if (!O.keepOld)
            {
                Undo.DestroyObjectImmediate(go);
                continue;
            }
            var backup = root.Find("OldStages_Backup");
            if (backup == null)
            {
                var b = new GameObject("OldStages_Backup");
                Undo.RegisterCreatedObjectUndo(b, "Backup");
                b.transform.SetParent(root, false);
                b.SetActive(false);
                backup = b.transform;
            }
            Undo.SetTransformParent(go.transform, backup, "Backup stage");
            Undo.RecordObject(go, "Rename old stage");
            go.name = "Old_Stage" + n;
        }
    }

    // вернуть старые стадии и убрать v2
    public static void Restore(HutStages hs)
    {
        if (hs == null) return;
        var root = hs.transform;
        Undo.IncrementCurrentGroup();
        for (int n = 1; n <= 3; n++)
        {
            var go = hs.GetStageObject(n);
            if (go != null && go.transform.Find("__HutV2") != null) Undo.DestroyObjectImmediate(go);
        }
        var backup = root.Find("OldStages_Backup");
        GameObject[] olds = new GameObject[3];
        if (backup != null)
        {
            for (int n = 1; n <= 3; n++)
            {
                var t = backup.Find("Old_Stage" + n);
                if (t == null) continue;
                Undo.SetTransformParent(t, root, "Restore stage");
                t.name = "Stage" + n;
                olds[n - 1] = t.gameObject;
            }
            Undo.DestroyObjectImmediate(backup.gameObject);
        }
        hs.Configure(olds[0], olds[1], olds[2], hs.BuildPoint, 1);
        EditorUtility.SetDirty(hs);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("[HutV2] Старые стадии возвращены.");
    }

    // ---------------------------------------------------------------- поиск хижины
    public static HutStages FindSelectedHut()
    {
        var go = Selection.activeGameObject;
        if (go != null)
        {
            var hs = go.GetComponentInParent<HutStages>(true);
            if (hs == null) hs = go.GetComponentInChildren<HutStages>(true);
            if (hs != null) return hs;
        }
        // если в сцене один HutStages с «хижинным» именем
        HutStages best = null;
        foreach (var h in Object.FindObjectsByType<HutStages>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = h.name.ToLowerInvariant();
            if (n.Contains("hut") || n.Contains("хиж") || n.Contains("lumber") || n.Contains("лесор")) { if (best != null) return null; best = h; }
        }
        return best;
    }

    // ---------------------------------------------------------------- меню
    [MenuItem("Tools/BuilderSim/Хижина лесоруба v2/Окно генератора")]
    static void OpenWindow() { LumberjackHutWindow.Open(); }

    [MenuItem("Tools/BuilderSim/Хижина лесоруба v2/Собрать на выбранной хижине (по умолчанию)")]
    static void QuickBuild() { Build(FindSelectedHut(), new Options()); }

    [MenuItem("Tools/BuilderSim/Хижина лесоруба v2/Вернуть старые стадии")]
    static void QuickRestore() { Restore(FindSelectedHut()); }
}

// ==================================================================================== окно
public class LumberjackHutWindow : EditorWindow
{
    HutStages hut;
    LumberjackHutBuilder.Options opt = new LumberjackHutBuilder.Options();
    Vector2 scroll;

    public static void Open()
    {
        var w = GetWindow<LumberjackHutWindow>(true, "Хижина лесоруба v2", true);
        w.minSize = new Vector2(380f, 440f);
        w.hut = LumberjackHutBuilder.FindSelectedHut();
        w.Show();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Генератор собирает низкополигональную хижину лесоруба с пилорамой в стиле Cloud Candy и три стадии стройки. " +
                                "Стадия 1 = «нулевая»: двор в заборе, материалы разобраны и разложены. Выбери в сцене хижину (объект с HutStages) и нажми «Собрать».", MessageType.Info);

        hut = (HutStages)EditorGUILayout.ObjectField("Хижина (HutStages)", hut, typeof(HutStages), true);
        if (hut == null && GUILayout.Button("Взять выбранную в сцене")) hut = LumberjackHutBuilder.FindSelectedHut();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Параметры", EditorStyles.boldLabel);
        opt.scale = EditorGUILayout.Slider(new GUIContent("Масштаб", "1 = метры как в проекте. Игрок высотой ~1.8 м"), opt.scale, 0.6f, 1.6f);
        opt.yaw = EditorGUILayout.Slider(new GUIContent("Поворот, °", "Куда смотрит дверь. 0 = на +Z"), opt.yaw, -180f, 180f);
        opt.groundY = EditorGUILayout.FloatField(new GUIContent("Высота земли", "Если корень хижины стоит не на земле"), opt.groundY);
        opt.seed = EditorGUILayout.IntField(new GUIContent("Зерно случайности", "Меняй, чтобы получить другую раскладку мелочей"), opt.seed);
        opt.keepOld = EditorGUILayout.Toggle(new GUIContent("Старые стадии в backup", "Выключи, чтобы удалить старые Stage1..3 насовсем"), opt.keepOld);
        opt.mossyRoof = EditorGUILayout.Toggle("Мох на крыше", opt.mossyRoof);
        opt.fenceColliders = EditorGUILayout.Toggle(new GUIContent("Забор непроходимый", "По умолчанию забор декоративный (игрок проходит сквозь, чтобы не застревать)"), opt.fenceColliders);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(hut == null))
        {
            if (GUILayout.Button("Собрать хижину и пилораму", GUILayout.Height(34f)))
                LumberjackHutBuilder.Build(hut, opt);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Проверка стадий (в режиме редактирования)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Стадия 1")) hut.SetStage(1);
                if (GUILayout.Button("Стадия 2")) hut.SetStage(2);
                if (GUILayout.Button("Стадия 3")) hut.SetStage(3);
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Вернуть старые стадии (убрать v2)"))
                if (EditorUtility.DisplayDialog("Хижина v2", "Удалить сгенерированные стадии и вернуть прежние из OldStages_Backup?", "Да", "Отмена"))
                    LumberjackHutBuilder.Restore(hut);
        }
        EditorGUILayout.EndScrollView();
    }
}
