#if UNITY_EDITOR
// Assets/Editor/NpcBuilder.cs
// Меню: Tools -> BuilderSim -> NPC Builder   (собирает префаб NPC: модель, коллайдер, Speaker, диалог, кружок «!»)
//       Tools -> BuilderSim -> Добавить реплики Лео игроку
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NpcBuilder : ScriptableWizard
{
    public enum Preset { Mira, Oskar, Custom }

    [Header("Кто это")]
    public Preset preset = Preset.Mira;
    [Tooltip("Модель NPC (префаб или объект). Пусто = временная капсула, потом заменишь на свою")]
    public GameObject model;
    public string npcName = "Мира";
    public Color nameColor = new Color32(0xF6, 0xA1, 0x5A, 255);

    [Header("Разговор")]
    public DialogueSequence.Trigger trigger = DialogueSequence.Trigger.PlayerNear;
    public float radius = 3f;
    [TextArea(1, 3)]
    public string[] lines =
    {
        "Ты живой! Идём, мне нужны дрова, и тебе тоже нужно согреться.",
        "Деревья вон там, в лесу. Держи E, пока не упадёт."
    };

    [Header("Остальное")]
    public bool showMarker = true;
    public bool faceThePlayer = true;
    public bool placeInScene = true;

    [SerializeField] Preset applied = (Preset)(-1);

    const string Folder = "Assets/_Game/NPC";

    [MenuItem("Tools/BuilderSim/Мир и персонаж/NPC Builder", false, 401)]
    static void Open()
    {
        var w = DisplayWizard<NpcBuilder>("NPC Builder", "Создать префаб");
        w.helpString = "Соберёт префаб NPC с кружком «!», пузырём реплик и разговором при подходе. Реплики можно менять в префабе.";
    }

    void OnWizardUpdate()
    {
        if (preset == applied) return;
        applied = preset;
        switch (preset)
        {
            case Preset.Mira:
                npcName = "Мира";
                nameColor = new Color32(0xF6, 0xA1, 0x5A, 255);
                trigger = DialogueSequence.Trigger.PlayerNear;
                radius = 3f;
                lines = new[]
                {
                    "Ты живой! Идём, мне нужны дрова, и тебе тоже нужно согреться.",
                    "Деревья вон там, в лесу. Держи E, пока не упадёт."
                };
                break;
            case Preset.Oskar:
                npcName = "Оскар";
                nameColor = new Color32(0x8F, 0xB8, 0xFF, 255);
                trigger = DialogueSequence.Trigger.Manual;   // появляется на 2 уровне: включи объект и вызови Play()
                lines = new[] { "Гуннар говорил: топор что друг, чем чаще держишь, тем лучше слушается." };
                break;
        }
    }

    void OnWizardCreate() { Build(); }

    // ---------------------------------------------------------------- сборка префаба
    void Build()
    {
        if (string.IsNullOrWhiteSpace(npcName)) npcName = "NPC";
        Directory.CreateDirectory(Folder);

        var root = new GameObject("NPC_" + npcName.Trim());

        // ----- модель
        GameObject m;
        if (model != null)
        {
            m = PrefabUtility.IsPartOfPrefabAsset(model)
                ? (GameObject)PrefabUtility.InstantiatePrefab(model)
                : Instantiate(model);
            m.name = "Model";
            m.transform.SetParent(root.transform, false);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
        }
        else
        {
            m = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            m.name = "Model";
            DestroyImmediate(m.GetComponent<Collider>());
            m.transform.SetParent(root.transform, false);
            m.transform.localPosition = new Vector3(0f, 1f, 0f);
        }
        foreach (var c in m.GetComponentsInChildren<Collider>()) DestroyImmediate(c);   // свой коллайдер на корне

        // ----- размеры
        Bounds b = new Bounds(root.transform.position + Vector3.up, new Vector3(0.8f, 2f, 0.8f));
        bool has = false;
        foreach (var r in m.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        float h = Mathf.Max(1.2f, b.max.y - root.transform.position.y);

        var col = root.AddComponent<CapsuleCollider>();
        col.height = h;
        col.center = new Vector3(0f, h * 0.5f, 0f);
        col.radius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z), 0.25f, 0.6f);

        // ----- говорящий
        var sp = root.AddComponent<Speaker>();
        sp.displayName = npcName.Trim();
        sp.nameColor = nameColor;
        sp.bubbleOffset = new Vector3(0f, h + 0.35f, 0f);
        sp.faceNearbyPlayer = faceThePlayer;
        sp.turnRoot = m.transform;
        sp.faceRadius = Mathf.Max(radius + 2f, 5f);

        // ----- кружок над головой
        NpcMarker marker = null;
        if (showMarker)
        {
            var mg = new GameObject("Marker");
            mg.transform.SetParent(root.transform, false);
            mg.transform.localPosition = new Vector3(0f, h + 0.95f, 0f);
            var sr = mg.AddComponent<SpriteRenderer>();
            sr.sprite = GetMarkerSprite();
            marker = mg.AddComponent<NpcMarker>();
        }

        // ----- разговор
        var seq = root.AddComponent<DialogueSequence>();
        seq.trigger = trigger;
        seq.radius = radius;
        seq.once = true;
        seq.marker = marker;
        var arr = new DialogueSequence.Line[lines.Length];
        for (int i = 0; i < arr.Length; i++)
            arr[i] = new DialogueSequence.Line { speaker = "", text = lines[i], pauseAfter = 0.45f };
        seq.lines = arr;

        // ----- префаб
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/NPC_" + npcName.Trim() + ".prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(prefab);

        if (placeInScene)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(inst, "Place NPC");
            inst.transform.position = SpawnPoint();
            Selection.activeGameObject = inst;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        Debug.Log("NPC Builder: префаб создан: " + path);
    }

    static Vector3 SpawnPoint()
    {
        Vector3 p = Vector3.zero;
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) p = sv.pivot;
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out hit, 200f)) p = hit.point;
        return p;
    }

    static Sprite GetMarkerSprite()
    {
        string path = Folder + "/npc_marker.png";
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;

        var tex = NpcMarker.MakeTexture();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 128f;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------------------------------------------------------------- Лео
    [MenuItem("Tools/BuilderSim/Мир и персонаж/Добавить реплики Лео игроку", false, 420)]
    static void AddLeo()
    {
        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm == null)
        {
            EditorUtility.DisplayDialog("Лео", "В сцене не найден игрок (PlayerMovement).", "OK");
            return;
        }

        var go = pm.gameObject;
        var sp = go.GetComponent<Speaker>();
        if (sp == null) sp = Undo.AddComponent<Speaker>(go);
        Undo.RecordObject(sp, "Leo speaker");
        sp.displayName = "Лео";
        sp.nameColor = new Color32(0x6B, 0xCB, 0x5A, 255);
        sp.bubbleOffset = new Vector3(0f, 2.6f, 0f);
        sp.faceNearbyPlayer = false;

        var seq = go.GetComponent<DialogueSequence>();
        if (seq == null) seq = Undo.AddComponent<DialogueSequence>(go);
        Undo.RecordObject(seq, "Leo lines");
        seq.trigger = DialogueSequence.Trigger.OnStart;
        seq.startDelay = 1.4f;
        seq.once = true;
        seq.lines = new[]
        {
            new DialogueSequence.Line { speaker = "", text = "Дядя? ...Никого. Ладно, осмотрюсь.", pauseAfter = 0.3f }
        };

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = go;
        Debug.Log("Лео добавлен: Speaker + стартовая реплика на игроке. Высоту пузыря подгони в Speaker → Bubble Offset.");
    }
}
#endif