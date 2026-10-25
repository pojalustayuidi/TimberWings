// Assets/_Project/Scripts/Vibe/QuestCelebration.cs (НЕ в Editor)
// Праздник за выполненное задание (GameEvents.QuestDone), вид Candy: облачная плашка сверху «Хижина отремонтирована»
// (звезда, индиго-текст, пружинный выезд, конфетти-искры на плашке), конфетти над героем в цветах Candy, лёгкая тряска камеры. Звук играет SfxManager. На новый уровень (GameEvents.LevelUp) только конфетти:
// плашку уровня у тебя уже рисует HUD.
// «Уменьшить эффекты»: без конфетти и тряски, плашка остаётся.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestCelebration : MonoBehaviour
{
    [Header("Плашка")]
    public float slideIn = 0.45f;
    public float hold = 2.2f;
    public float fadeOut = 0.5f;
    public float shownY = -180f;      // ниже строки цели

    static readonly Dictionary<string, string> Titles = new Dictionary<string, string>
    {
        { "intro", "Костёр горит!" },
        { "supply", "Припасы доставлены" },
        { "upgrade", "Рюкзак улучшен" },
        { "hut", "Хижина отремонтирована" },
        { "planks", "Доски готовы" },
        { "market", "Прилавок построен" },
        { "firstSale", "Первая сделка!" },
        { "tent", "Лагерь разбит" }
    };

    readonly Queue<string> queue = new Queue<string>();
    RectTransform canvasRt, panel;
    CanvasGroup group;
    TMP_Text title, main;
    ParticleSystem confetti;
    Transform player;
    float findAt;

    bool showing;
    float t;

    void OnEnable()
    {
        GameEvents.QuestDone += OnQuest;
        GameEvents.LevelUp += OnLevel;
    }

    void OnDisable()
    {
        GameEvents.QuestDone -= OnQuest;
        GameEvents.LevelUp -= OnLevel;
    }

    void OnDestroy()
    {
        if (canvasRt != null) Destroy(canvasRt.gameObject);
    }

    void OnQuest(string id)
    {
        string text;
        if (!Titles.TryGetValue(id ?? "", out text)) text = "Задание выполнено";
        queue.Enqueue(text);
        Burst(46);
        if (!VibeSettings.ReduceEffects) CameraFollow.Shake(0.06f, 0.2f);
    }

    void OnLevel(int level) { Burst(30); }

    // ------------------------------------------------------------ конфетти
    void Burst(int count)
    {
        if (VibeSettings.ReduceEffects) return;
        if (confetti == null) confetti = MakeConfetti();
        if (player == null) FindPlayer();
        Vector3 pos = player != null ? player.position + Vector3.up * 2.2f : Vector3.up * 3f;
        confetti.transform.position = pos;
        confetti.Emit(count);
    }

    void FindPlayer()
    {
        if (Time.unscaledTime < findAt) return;
        findAt = Time.unscaledTime + 1f;
        var pm = PlayerMovement.Instance;
        if (pm != null) player = pm.transform;
    }

    ParticleSystem MakeConfetti()
    {
        var go = new GameObject("Confetti");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // конус смотрит вверх
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = VibeGfx.ParticleMat(VibeGfx.Square);
        r.renderMode = ParticleSystemRenderMode.Billboard;

        var grad = new Gradient();
        grad.mode = GradientMode.Fixed;
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(UiCandy.Lemon, 0.2f),
                new GradientColorKey(UiCandy.Mint, 0.4f),
                new GradientColorKey(new Color32(0xFF, 0x8F, 0x9E, 255), 0.6f),
                new GradientColorKey(UiCandy.Sky, 0.8f),
                new GradientColorKey(UiCandy.LavenderDeep, 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        var mg = new ParticleSystem.MinMaxGradient(grad);
        mg.mode = ParticleSystemGradientMode.RandomColor;

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 150;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.2f;
        main.startColor = mg;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 35f;
        sh.radius = 0.3f;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;

        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------ плашка
    void Build()
    {
        var cgo = new GameObject("Celebration_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.layer = 5;
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.Popups;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)cgo.transform;

        var pgo = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
        pgo.layer = 5;
        pgo.transform.SetParent(canvasRt, false);
        panel = (RectTransform)pgo.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.sizeDelta = new Vector2(780f, 124f);
        group = pgo.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // облачная панель Candy, звезда Candy слева, подпись цветом «выполнено», название индиго
        var bg = AddImage(panel, "Bg", CandyTag.Panel, Color.white, Vector2.zero, Vector2.zero, true);
        bg.pixelsPerUnitMultiplier = 1f / 0.7f;
        AddImage(panel, "Star", CandyTag.Icon(UiIcon.Star), Color.white, new Vector2(-324f, 6f), new Vector2(84f, 84f), false);

        TMP_FontAsset font = null;
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null) font = UIManager.Instance.objectiveText.font;
        title = AddText(panel, "Title", font, 22f, CandyTag.Good, new Vector2(40f, 30f), new Vector2(620f, 30f), false);
        title.characterSpacing = 6f;
        main = AddText(panel, "Main", font, 40f, CandyTag.Ink, new Vector2(40f, -8f), new Vector2(620f, 56f), true);
        title.text = "ЗАДАНИЕ ВЫПОЛНЕНО";

        // пружинный «щелчок» при каждом показе (плашку включает и выключает Update); прозрачностью управляет group
        var pop = panel.gameObject.AddComponent<CandyPopIn>();
        pop.fade = false;
        pop.fromScale = 0.88f;
        panel.gameObject.SetActive(false);
    }

    static Image AddImage(RectTransform parent, string name, Sprite sp, Color c, Vector2 pos, Vector2 size, bool stretch)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        var im = go.GetComponent<Image>();
        im.sprite = sp;
        im.color = c;
        im.raycastTarget = false;
        if (stretch)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            im.type = UnityEngine.UI.Image.Type.Sliced;
        }
        else
        {
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            im.preserveAspect = true;
        }
        return im;
    }

    static TMP_Text AddText(RectTransform parent, string name, TMP_FontAsset font, float size, Color c, Vector2 pos, Vector2 box, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var tx = go.GetComponent<TextMeshProUGUI>();
        CandyTag.Font(tx, font);                 // круглый шрифт Candy, а если его нет, шрифт HUD
        tx.fontSize = size;
        tx.alignment = TextAlignmentOptions.Center;
        tx.color = c;
        tx.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tx.raycastTarget = false;
        tx.enableAutoSizing = true;
        tx.fontSizeMin = 16f;
        tx.fontSizeMax = size;
        return tx;
    }

    void Update()
    {
        if (!showing)
        {
            if (queue.Count == 0) return;
            if (panel == null) Build();
            main.text = queue.Dequeue();
            panel.gameObject.SetActive(true);
            showing = true;
            t = 0f;
            if (!VibeSettings.ReduceEffects) CandyFx.Burst(UiCandy.Kit, panel, UiCandy.Lemon, 10, 300f);   // искры на плашке
        }

        t += Time.unscaledDeltaTime;
        float total = slideIn + hold + fadeOut;
        if (t >= total)
        {
            panel.gameObject.SetActive(false);
            showing = false;
            return;
        }

        float k = Mathf.Clamp01(t / slideIn);
        float y = Mathf.LerpUnclamped(160f, shownY, UiDraw.EaseOutBack(k));   // выезжает сверху с лёгким пружинным добегом
        panel.anchoredPosition = new Vector2(0f, y);
        float alpha = t > slideIn + hold ? 1f - (t - slideIn - hold) / fadeOut : Mathf.Clamp01(t / 0.15f);
        group.alpha = alpha;
    }
}
