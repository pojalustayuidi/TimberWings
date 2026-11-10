using UnityEngine;

public static class MysticPal
{
    public static readonly Color Wood = new Color(0.64f, 0.46f, 0.58f);
    public static readonly Color WoodLight = new Color(0.84f, 0.66f, 0.74f);
    public static readonly Color Stone = new Color(0.66f, 0.60f, 0.84f);
    public static readonly Color StoneDark = new Color(0.48f, 0.43f, 0.68f);
    public static readonly Color Moss = new Color(0.45f, 0.76f, 0.58f);
    public static readonly Color Violet = new Color(0.78f, 0.55f, 1.00f);
    public static readonly Color Cyan = new Color(0.45f, 1.00f, 0.92f);
    public static readonly Color Pink = new Color(1.00f, 0.62f, 0.80f);
    public static readonly Color Cream = new Color(0.97f, 0.93f, 0.88f);
    public static readonly Color[] Crystals = { Violet, Cyan, Pink };
}


// Compatibility names only; no automatic runtime attachment or world generation.
public static class MysticGladeBootstrap { public static readonly string[] padNames={"PAD_Forest"}; }
[DisallowMultipleComponent]
public partial class MysticGladeBuilder : MonoBehaviour
{
    public const string RootName="_MysticGlade";
    [Header("Сборка")]
    public bool buildOnStart = false;
    public int seed = 5309;

    [Header("Центр")]
    [Tooltip("Центр зоны = Дедушка (GrandpaVibe), иначе ближайший LandmarkTree, в этом радиусе от метки")]
    public bool snapToLandmark = true;
    public float landmarkSearch = 30f;

    [Header("Вход")]
    public bool autoEntry = true;
    [Tooltip("Если autoEntry выключен: направление ОТ центра К входу, градусы мира (0 = +Z, 90 = +X)")]
    public float entryYaw = 180f;

    [Header("Раскладка (метры от ствола)")]
    public float runeRadius = 4.8f;
    public float benchRadius = 7.2f;
    public int benchCount = 6;
    public float treeRadius = 12.5f;
    public int treeCount = 6;
    public float clearingRadius = 15f;

    [Header("Детали")]
    [Range(0f, 2f)] public float scatterDensity = 1f;
    public bool addVibe = true;

    [Header("Старое")]
    [Tooltip("Сдвинуть деревья из зоны за её границу (только в Play Mode)")]
    public bool relocateTrees = true;
    [Tooltip("Выключить старую поляну рубки: ChopGroveBuilder рядом, ChopGladeDecor, GladeVibe (только в Play Mode)")]
    public bool replaceOldGlade = true;
    public string oldDecorName = "ChopGladeDecor";


    [ContextMenu("Собрать зону в Editor")]
    public void Build()
    {
#if UNITY_EDITOR
        if(!Application.isPlaying){UnityEditor.EditorApplication.ExecuteMenuItem("Tools/BuilderSim/Лес/MYSTIC FOREST/2. Собрать Круг молчания в сцене");return;}
#endif
        Debug.LogWarning("MYSTIC FOREST: окружение собирается в Editor, перед Play Mode.");
    }
    [ContextMenu("Вернуть прежнее окружение")]
    public void Clear()
    {
#if UNITY_EDITOR
        if(!Application.isPlaying){UnityEditor.EditorApplication.ExecuteMenuItem("Tools/BuilderSim/Лес/MYSTIC FOREST/3. Вернуть окружение до установки");return;}
#endif
        Debug.LogWarning("MYSTIC FOREST: откат выполняется в Editor.");
    }
}
