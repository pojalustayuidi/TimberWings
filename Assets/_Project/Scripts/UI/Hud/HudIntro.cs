// Assets/_Project/Scripts/UI/Hud/HudIntro.cs
// При старте игры панели HUD по очереди «въезжают» на место с лёгкой пружинкой и проявляются.
// Настраивается автоматически (Tools > BuilderSim > Build HUD).
using System.Collections.Generic;
using UnityEngine;

public class HudIntro : MonoBehaviour
{
    [System.Serializable]
    public class Item
    {
        public RectTransform rt;
        public Vector2 offset;      // откуда въезжает (смещение от финальной позиции, в единицах канваса)
        public float delay;
        [System.NonSerialized] public Vector2 final;
        [System.NonSerialized] public CanvasGroup cg;
    }

    public List<Item> items = new List<Item>();
    public float duration = 0.75f;

    float t;

    void Start()
    {
        foreach (var it in items)
        {
            if (it == null || it.rt == null) continue;
            it.final = it.rt.anchoredPosition;
            it.cg = it.rt.GetComponent<CanvasGroup>();
            if (it.cg == null) it.cg = it.rt.gameObject.AddComponent<CanvasGroup>();
            it.rt.anchoredPosition = it.final + it.offset;
            it.cg.alpha = 0f;
        }
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        bool done = true;
        foreach (var it in items)
        {
            if (it == null || it.rt == null || it.cg == null) continue;
            float k = Mathf.Clamp01((t - it.delay) / Mathf.Max(0.05f, duration));
            if (k < 1f) done = false;
            it.rt.anchoredPosition = Vector2.LerpUnclamped(it.final + it.offset, it.final, EaseOutBack(k));
            it.cg.alpha = Mathf.Clamp01(k * 2.5f);
        }

        if (done)
        {
            foreach (var it in items)
            {
                if (it == null || it.rt == null) continue;
                it.rt.anchoredPosition = it.final;
                if (it.cg != null) it.cg.alpha = 1f;
            }
            enabled = false;
        }
    }
}