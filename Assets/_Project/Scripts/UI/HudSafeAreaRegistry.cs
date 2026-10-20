// Assets/_Project/Scripts/UI/HudSafeAreaRegistry.cs
// Безопасная зона HUD: сюда нельзя ставить метки над миром (WorldTag, BuildSiteTagView).
// API тот же: Instance, Register, Unregister, GetSafeRect, PushOutOfSafe.
//
// Что исправлено:
//  * Раньше в реестр попадали ВСЕ картинки внутри HUD_Auto, включая фоны на пол-экрана,
//    и метка вечно уезжала. Теперь панели больше maxFraction экрана игнорируются.
//  * Скан раз в 2 секунды и без выделений памяти (раньше каждую секунду GameObject.Find + массивы).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HudSafeAreaRegistry : MonoBehaviour
{
    public static HudSafeAreaRegistry Instance { get; private set; }

    [Tooltip("Отступ вокруг панели, px")]
    public float pad = 14f;
    [Tooltip("Панели шире или выше этой доли экрана считаются фоном и игнорируются")]
    [Range(0.2f, 1f)] public float maxFraction = 0.5f;
    public float scanInterval = 2f;
    public string[] panelNames = { "Coins", "Wood", "Task", "Level", "Minimap", "ActionPrompt", "HUD_Auto" };

    readonly List<RectTransform> panels = new List<RectTransform>(24);
    readonly List<RectTransform> autoAdded = new List<RectTransform>(24);
    readonly List<RectTransform> tmpChildren = new List<RectTransform>(32);
    readonly Vector3[] corners = new Vector3[4];
    Camera uiCam;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Scan();
    }

    void OnEnable()
    {
        if (Instance == this) InvokeRepeating(nameof(Scan), scanInterval, scanInterval);
    }

    void OnDisable() { CancelInvoke(); }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Register(RectTransform rt)
    {
        if (rt != null && !panels.Contains(rt)) panels.Add(rt);
    }

    public void Unregister(RectTransform rt) { panels.Remove(rt); }

    void AddAuto(RectTransform rt)
    {
        if (rt == null || panels.Contains(rt)) return;
        panels.Add(rt);
        autoAdded.Add(rt);
    }

    void Scan()
    {
        // чистим только то, что добавили сами; ручная регистрация (HudSafeAreaElement) остаётся
        for (int i = 0; i < autoAdded.Count; i++) panels.Remove(autoAdded[i]);
        autoAdded.Clear();

        var canvas = FindFirstObjectByType<Canvas>();
        uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        for (int n = 0; n < panelNames.Length; n++)
        {
            var go = GameObject.Find(panelNames[n]);
            if (go == null) continue;
            var rt = go.GetComponent<RectTransform>();
            AddAuto(rt);

            tmpChildren.Clear();
            go.GetComponentsInChildren(true, tmpChildren);
            for (int i = 0; i < tmpChildren.Count; i++)
            {
                var ch = tmpChildren[i];
                if (ch == rt) continue;
                if (ch.GetComponent<Image>() != null) AddAuto(ch);
            }
        }

        foreach (var a in FindObjectsByType<HudGainAnchor>(FindObjectsSortMode.None))
            AddAuto((RectTransform)a.transform);

        var ap = FindFirstObjectByType<ActionPrompt>();
        if (ap != null && ap.group != null) AddAuto((RectTransform)ap.group.transform);
    }

    // true, если точка sp (центр метки размером tagSize) попала в чужую панель; safe = её расширенный прямоугольник
    public bool GetSafeRect(Vector2 sp, Vector2 tagSize, out Rect safe)
    {
        safe = default;
        bool hit = false;
        float best = float.MaxValue;
        float maxW = Screen.width * maxFraction, maxH = Screen.height * maxFraction;

        for (int i = 0; i < panels.Count; i++)
        {
            var rt = panels[i];
            if (rt == null || !rt.gameObject.activeInHierarchy) continue;
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 0.01f) continue;

            rt.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(uiCam, corners[2]);
            if (max.x - min.x > maxW || max.y - min.y > maxH) continue;   // фон, а не панель

            Rect r = Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
            r.xMin -= tagSize.x * 0.5f; r.xMax += tagSize.x * 0.5f;
            r.yMin -= tagSize.y * 0.5f; r.yMax += tagSize.y * 0.5f;
            if (!r.Contains(sp)) continue;

            float d = Mathf.Min(sp.x - r.xMin, r.xMax - sp.x, sp.y - r.yMin, r.yMax - sp.y);
            if (d < best) { best = d; safe = r; hit = true; }
        }
        return hit;
    }

    // Выталкивает центр метки на ближайший край расширенного прямоугольника: метка оказывается целиком СНАРУЖИ панели.
    // side помнит выбранную сторону (0 нет, 1 вверх, 2 вниз, 3 влево, 4 вправо) и держится за неё,
    // пока другая сторона не станет заметно выгоднее: так метка не перескакивает вверх-вниз.
    public Vector2 PushOutOfSafe(Vector2 sp, Rect safe, Vector2 tagSize, ref int side)
    {
        const float gap = 2f;
        float hx = tagSize.x * 0.5f + 14f, hy = tagSize.y * 0.5f + 14f;

        Vector2[] cand =
        {
            new Vector2(sp.x, safe.yMax + gap),   // 1 вверх
            new Vector2(sp.x, safe.yMin - gap),   // 2 вниз
            new Vector2(safe.xMin - gap, sp.y),   // 3 влево
            new Vector2(safe.xMax + gap, sp.y)    // 4 вправо
        };

        int pick = 1;
        float bestCost = float.MaxValue;
        for (int k = 0; k < 4; k++)
        {
            float cost = PushCost(cand[k], sp, k < 2 ? 1f : 1.6f, hx, hy);
            if (side == k + 1) cost *= 0.4f;      // липкость: текущая сторона в 2.5 раза «дешевле»
            if (cost < bestCost) { bestCost = cost; pick = k + 1; }
        }
        side = pick;
        return cand[pick - 1];
    }

    public Vector2 PushOutOfSafe(Vector2 sp, Rect safe, Vector2 tagSize)
    {
        int side = 0;
        return PushOutOfSafe(sp, safe, tagSize, ref side);
    }

    static float PushCost(Vector2 cand, Vector2 from, float k, float hx, float hy)
    {
        float cost = (cand - from).magnitude * k;
        float outX = Mathf.Max(0f, hx - cand.x, cand.x - (Screen.width - hx));
        float outY = Mathf.Max(0f, hy - cand.y, cand.y - (Screen.height - hy));
        return cost + (outX + outY) * 20f;   // вылезает за экран: сильно невыгодно
    }
}