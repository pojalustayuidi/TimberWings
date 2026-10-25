// Assets/_Project/Scripts/UI/Hud/HudPlanks.cs (НЕ в Editor)
// Слот «Доски» общей панели ресурсов. В Candy HUD это пилюля «Planks» (иконка, блик, индиго-число), её собирает меню
// Tools > BuilderSim > UI > Candy Kit > Собрать HUD; слот лежит в сцене, в рантайме ничего не копируется и не строится.
// Сам скрипт от стиля не зависит: он только плавно показывает слот и растит панель, вид слота задаёт сборщик.
// Скрипт делает три вещи:
//   1) держит ссылку на число (Set), чтобы UIManager.UpdatePlanks работал как раньше;
//   2) плавно показывает слот, когда у игрока появились доски (или пила в хижине открыта): слот проявляется,
//      а вся панель и разделитель перед слотом растут вправо; после появления слот больше не прячется;
//   3) отдаёт точку FlyPoint (центр иконки), откуда летит иконка цели (ObjectiveView.SourcePoint).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HudPlanks : MonoBehaviour
{
    public static HudPlanks Instance { get; private set; }

    [Tooltip("true = слот виден всегда, даже с нулём досок")]
    public bool alwaysShow = false;

    [Header("Ссылки (расставляет сборщик Candy HUD)")]
    public TMP_Text value;               // число
    public RectTransform icon;           // иконка в медальоне (точка вылета для ObjectiveView)
    public RectTransform panel;          // корень ResourcePanel: меняет ширину
    public CanvasGroup group;            // прозрачность слота
    public Image divider;                // разделитель перед слотом (в Candy HUD его нет: null)

    [Header("Размеры (единицы канваса)")]
    public float baseWidth = 373f;       // ширина панели без слота досок
    public float slotWidth = 187f;       // на сколько панель растёт, когда доски появились
    public float appearTime = 0.35f;

    int planks;
    bool everHad, unlockedSeen;
    float shown, checkAt;
    float dividerAlpha = 0.55f;
    PlankWorkshop workshop;

    // Старый вход: раньше панель досок копировалась из панели дерева прямо в игре. Теперь слот собирается в Editor.
    // Метод оставлен, чтобы UIManager.Start не менять; если слота нет, говорит об этом один раз.
    public static void Create(TMP_Text woodText)
    {
        if (Instance != null || woodText == null) return;
        Debug.LogWarning("[HudPlanks] Слот «Доски» не найден в сцене. Запусти Tools > BuilderSim > UI > Candy Kit > Собрать HUD и сохрани сцену.");
    }

    void Awake()
    {
        Instance = this;
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        if (divider != null) dividerAlpha = divider.color.a;
        if (value != null) value.text = planks.ToString();
        Apply(0f);                       // в Editor слот показан целиком (чтобы его было видно при вёрстке), в игре начинает скрытым
    }

    public void Set(int n)
    {
        planks = Mathf.Max(0, n);
        if (planks > 0) everHad = true;
        if (value != null) value.text = planks.ToString();
    }

    // центр иконки в мировых координатах: сюда «прилетает» иконка цели
    public Vector3 FlyPoint
    {
        get
        {
            var r = icon != null ? icon : (RectTransform)transform;
            return r.TransformPoint(r.rect.center);
        }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void LateUpdate()
    {
        // пила открыта: слот появляется заранее, до первой доски
        if (!unlockedSeen && Time.unscaledTime >= checkAt)
        {
            checkAt = Time.unscaledTime + 0.5f;
            if (workshop == null) workshop = FindFirstObjectByType<PlankWorkshop>();
            if (workshop != null && workshop.unlocked) unlockedSeen = true;
        }

        bool want = alwaysShow || everHad || unlockedSeen;
        float target = want ? 1f : 0f;
        if (Mathf.Approximately(shown, target)) return;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, appearTime));
        Apply(shown);
    }

    // k: 0 = слота нет, 1 = слот на месте. Панель растёт плавно (без перелёта), сам слот чуть «вырастает» с пружинкой
    void Apply(float k)
    {
        if (panel != null)
            panel.sizeDelta = new Vector2(baseWidth + slotWidth * UiDraw.EaseOutCubic(k), panel.sizeDelta.y);

        if (group != null) group.alpha = Mathf.Clamp01((k - 0.15f) / 0.85f);

        float s = k > 0.001f ? Mathf.LerpUnclamped(0.8f, 1f, UiDraw.EaseOutBack(k)) : 0f;
        transform.localScale = Vector3.one * s;

        if (divider != null)
        {
            var c = divider.color;
            c.a = dividerAlpha * Mathf.Clamp01((k - 0.4f) / 0.6f);
            divider.color = c;
        }
    }
}
