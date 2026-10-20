// Assets/_Project/Scripts/UI/HudGainAnchor.cs
// Повесь на панель в HUD (бревна / уровень / монеты). Система находит панели сама по этому компоненту,
// поэтому UIManager трогать не нужно.
//   Kind = Wood  -> панель счётчика дерева: из-под неё выезжает "+2 бревна", сюда летят бревна
//   Kind = Xp    -> панель уровня: из-под неё выезжает "+2 XP"
//   Kind = Coin  -> панель монет: из-под неё выезжает "+8"
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HudGainAnchor : MonoBehaviour
{
    public HudGain.Kind kind;

    [Header("Необязательно")]
    [Tooltip("Куда долетают бревна. Пусто = первая дочерняя Image (иконка) или сама панель")]
    public RectTransform flyTarget;
    [Tooltip("Иконка летящего бревна. Пусто = нарисованная кодом")]
    public Sprite flyIcon;
    [Tooltip("Шрифт плашки. Пусто = берём из первого TMP-текста внутри панели")]
    public TMP_FontAsset font;
    [Tooltip("Для слота общей панели (ResourcePanel): её фон. Плашка «+N» встаёт в иерархии прямо ПЕРЕД ним и выезжает из-под панели. " +
             "Должен лежать в том же родителе, что и этот слот. Пусто = плашка встаёт перед самой панелью, как раньше")]
    public RectTransform gainBehind;
    [Range(0f, 0.4f)] public float punchStrength = 0.16f;

    static readonly List<HudGainAnchor> all = new List<HudGainAnchor>();

    RectTransform cachedFly;
    float punch;
    Vector3 baseScale = Vector3.one;
    bool punching;

    public RectTransform Rect { get { return (RectTransform)transform; } }

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }

    void OnDisable()
    {
        all.Remove(this);
        if (punching) { transform.localScale = baseScale; punching = false; punch = 0f; }
    }

    public static HudGainAnchor Find(HudGain.Kind k)
    {
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var a = all[i];
            if (a != null && a.kind == k && a.isActiveAndEnabled) return a;
        }
        return null;
    }

    public RectTransform FlyRect
    {
        get
        {
            if (flyTarget != null) return flyTarget;
            if (cachedFly != null) return cachedFly;
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject == gameObject || !img.enabled) continue;
                cachedFly = (RectTransform)img.transform;
                break;
            }
            if (cachedFly == null) cachedFly = Rect;
            return cachedFly;
        }
    }

    // мировая точка центра цели полёта
    public Vector3 FlyPoint
    {
        get
        {
            var r = FlyRect;
            return r.TransformPoint(r.rect.center);
        }
    }

    public TMP_FontAsset GetFont()
    {
        if (font != null) return font;
        var t = GetComponentInChildren<TMP_Text>(true);
        return t != null ? t.font : null;
    }

    // «подпрыгнуть» панелью (вызывается при прилёте бревна / получении XP)
    public void Punch()
    {
        if (!punching) baseScale = transform.localScale;
        punching = true;
        punch = 1f;
    }

    void Update()
    {
        if (!punching) return;
        punch -= Time.unscaledDeltaTime / 0.25f;
        if (punch <= 0f)
        {
            punching = false;
            transform.localScale = baseScale;
            return;
        }
        float s = 1f + punchStrength * Mathf.Sin((1f - punch) * Mathf.PI);
        transform.localScale = baseScale * s;
    }
}
