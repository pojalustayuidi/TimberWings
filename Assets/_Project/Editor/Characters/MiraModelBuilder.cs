// Assets/_Project/Scripts/Editor/Characters/MiraModelBuilder.cs   (ТОЛЬКО в папке Editor!)
// Генератор модели Миры (Camp Guide): Tools > BuilderSim > NPC > Мира: модель.
//
// Что делает:
//   1. Собирает модель из таблицы MiraModelData (мягкие примитивы в стиле «глина», как на листе персонажа) и кладёт ассеты
//      в Assets/_Project/Characters/Mira/ (Meshes, Materials, Mira_Model.prefab). Повторный запуск обновляет те же файлы.
//   2. «Собрать и заменить Миру в сцене»: находит NPC со Speaker «Мира», ставит ей новую модель дочерним объектом
//      MiraModel и гасит рендеры СТАРОЙ модели (рендеры выключаются, не удаляются: всё откатывается Ctrl+Z).
//      Не трогает Speaker, DialogueSequence, NpcMarker («!»), SupplyPoint, TradingStall, коллайдеры и ссылки.
//
// Скелет без костей: обычные Transform-суставы (Hips > Spine > Head, плечи, локти, кисти, бёдра, колени, лодыжки,
// глаза для моргания, маятники реквизита). Компонент MiraRig хранит ссылки и позу покоя, под анимации.
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MiraModelBuilder
{
    public const string Folder = "Assets/_Project/Characters/Mira";
    const string MeshDir = Folder + "/Meshes";
    const string MatDir = Folder + "/Materials";
    public const string PrefabPath = Folder + "/Mira_Model.prefab";
    public const string ModelName = "MiraModel";
    public const string SpeakerName = "Мира";

    // ---------------------------------------------------------------- меню
    [MenuItem("Tools/BuilderSim/NPC/Мира: собрать префаб модели")]
    public static void MenuBuildPrefab()
    {
        var prefab = BuildPrefab();
        if (prefab == null) return;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Мира: префаб собран → " + PrefabPath);
    }

    // ---------------------------------------------------------------- сборка префаба
    public static GameObject BuildPrefab()
    {
        EnsureFolder(Folder); EnsureFolder(MeshDir); EnsureFolder(MatDir);

        var mats = new Dictionary<string, Material>();
        foreach (var d in MiraModelData.Materials) mats[d.name] = GetMaterial(d);

        var root = new GameObject(ModelName);
        try
        {
            var joints = new Dictionary<string, Transform>();
            var bind = new Dictionary<string, Vector3>();
            joints["Root"] = root.transform;
            bind["Root"] = Vector3.zero;

            // суставы: пока без поворотов (bind-поза), чтобы детали встали ровно
            foreach (var j in MiraModelData.Joints)
            {
                var go = new GameObject(j.name);
                go.transform.SetParent(joints[j.parent], false);
                go.transform.localPosition = j.bindPos - bind[j.parent];
                joints[j.name] = go.transform;
                bind[j.name] = j.bindPos;
            }

            // детали
            var meshCache = new Dictionary<string, Mesh>();
            foreach (var p in MiraModelData.Parts)
            {
                var go = new GameObject(p.name);
                var t = go.transform;
                t.SetParent(joints[p.joint], false);
                t.localPosition = p.pos;
                t.localRotation = Quaternion.Euler(p.euler);
                t.localScale = p.scale;

                string key = MeshKey(p);
                Mesh mesh;
                if (!meshCache.TryGetValue(key, out mesh)) { mesh = GetMesh(key, p); meshCache[key] = mesh; }

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mats[p.mat];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }

            // поза покоя: руки (кружка / рука на поясе), чуть расставленные ноги
            foreach (var j in MiraModelData.Joints) joints[j.name].localRotation = Quaternion.Euler(j.restEuler);

            var rig = root.AddComponent<MiraRig>();
            if (rig == null)
            {
                Debug.LogError("Мира: не удалось добавить компонент MiraRig (AddComponent вернул null). Проверьте: файл называется MiraRig.cs, " +
                               "класс `public class MiraRig : MonoBehaviour`, лежит НЕ в папке Editor, в проекте нет ошибок компиляции. " +
                               "Подробности смотрите в Console выше этого сообщения.");
                return null;
            }
            rig.Bind();
            rig.CaptureRest();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---------------------------------------------------------------- замена в сцене
    public static Speaker[] AllSpeakers()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<Speaker>(true);
#endif
    }

    public static Speaker FindMira()
    {
        foreach (var s in AllSpeakers())
            if (s != null && s.gameObject.scene.IsValid() && !string.IsNullOrEmpty(s.displayName) &&
                string.Equals(s.displayName.Trim(), SpeakerName, System.StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }

    // scale: масштаб модели; yOffset: сдвиг вверх/вниз (если у корня NPC ноги не на нуле); hideOld: погасить рендеры старой модели
    public static GameObject ReplaceInScene(Speaker target, float scale, float yOffset, bool hideOld)
    {
        if (target == null) return null;
        var prefab = BuildPrefab();
        if (prefab == null) return null;
        var root = target.transform;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // прошлую новую модель (повторный запуск) убираем целиком
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var ch = root.GetChild(i);
            if (ch != null && ch.name.StartsWith(ModelName)) Undo.DestroyObjectImmediate(ch.gameObject);
        }

        // старый визуал гасим ДО появления нового
        int hidden = hideOld ? HideOldVisuals(root) : 0;

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        Undo.RegisterCreatedObjectUndo(inst, "Мира: новая модель");
        inst.name = ModelName;
        inst.transform.localPosition = new Vector3(0f, yOffset, 0f);
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        // если Speaker крутил старую модель к игроку, теперь крутит новую
        if (target.turnRoot != null && target.turnRoot != root && !target.turnRoot.IsChildOf(inst.transform))
        {
            Undo.RecordObject(target, "Мира: turnRoot");
            target.turnRoot = inst.transform;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        Selection.activeGameObject = inst;
        Debug.Log("Мира: новая модель поставлена на «" + target.name + "». Старых рендеров погашено: " + hidden +
                  ". Не так встала по высоте? Поменяй «Сдвиг по Y» в окне или Position.y у объекта MiraModel.", target);
        return inst;
    }

    static int HideOldVisuals(Transform root)
    {
        int n = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled) continue;
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;   // эффекты не трогаем
            if (r.GetComponentInParent<NpcMarker>(true) != null) continue;                           // кружок «!»
            if (r.GetComponent("TMP_Text") != null) continue;                                        // подписи
            if (r.transform.name.StartsWith(ModelName) || r.GetComponentInParent<MiraRig>(true) != null) continue;
            Undo.RecordObject(r, "Скрыть старую модель Миры");
            r.enabled = false;
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- ассеты
    static string MeshKey(MiraModelData.PartDef p)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}_{3}_{4}", p.kind, p.a, p.b, p.c, p.d).Replace('.', 'p').Replace('-', 'm');
    }

    static Mesh GetMesh(string key, MiraModelData.PartDef p)
    {
        string path = MeshDir + "/Mira_" + key + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "Mira_" + key };
        MiraMeshKit.Fill(mesh, p.kind, p.a, p.b, p.c, p.d);
        if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Material GetMaterial(MiraModelData.MatDef d)
    {
        string path = MatDir + "/Mira_" + d.name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = "Mira_" + d.name };
            AssetDatabase.CreateAsset(m, path);
        }
        Color c;
        if (!ColorUtility.TryParseHtmlString(d.hex, out c)) c = Color.magenta;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", d.smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", d.smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        string parent = path.Substring(0, i), name = path.Substring(i + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}

// ---------------------------------------------------------------- окно
public class MiraModelWindow : EditorWindow
{
    Speaker target;
    float scale = 1f;
    float yOffset = 0f;
    bool hideOld = true;

    [MenuItem("Tools/BuilderSim/NPC/Мира: модель (окно)")]
    static void Open()
    {
        var w = GetWindow<MiraModelWindow>(true, "Мира: модель", true);
        w.minSize = new Vector2(380f, 300f);
        w.target = MiraModelBuilder.FindMira();
        w.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Собирает новую модель Миры (Camp Guide) из примитивов и ставит её NPC вместо старой.\n" +
            "Диалоги, «!», отдача брёвен и торговля остаются как были: меняется только внешний вид.", MessageType.Info);

        EditorGUILayout.Space();
        target = (Speaker)EditorGUILayout.ObjectField("NPC «Мира» в сцене", target, typeof(Speaker), true);
        if (target == null && GUILayout.Button("Найти Speaker «Мира» в открытых сценах")) target = MiraModelBuilder.FindMira();

        scale = EditorGUILayout.Slider("Масштаб модели", scale, 0.5f, 2f);
        yOffset = EditorGUILayout.Slider("Сдвиг по Y", yOffset, -1f, 1f);
        hideOld = EditorGUILayout.ToggleLeft("Погасить рендеры старой модели (Ctrl+Z вернёт)", hideOld);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(target == null))
        {
            if (GUILayout.Button("Собрать и заменить Миру в сцене", GUILayout.Height(32f)))
                MiraModelBuilder.ReplaceInScene(target, scale, yOffset, hideOld);
        }
        if (GUILayout.Button("Только собрать префаб (без сцены)"))
            MiraModelBuilder.MenuBuildPrefab();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Префаб: " + MiraModelBuilder.PrefabPath, EditorStyles.miniLabel);
    }
}
