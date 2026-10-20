// Assets/_Project/Scripts/UI/UiLayout.cs (НЕ в Editor)
// Общие помощники раскладки UI: создание RectTransform, якоря, подписи, клик по картинке.
// Раньше эти методы были скопированы в каждое окно (Build/Sell/Workers/PlankWorkshop/Upgrade/SkillTree/...).
// Подключение в окне: using static UiLayout;  (локальный метод с тем же именем всегда главнее общего).
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class UiLayout
{
    public static RectTransform NewRT(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    public static void Anch(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    // от левого верхнего угла родителя вниз-вправо
    public static void TL(RectTransform r, float x, float y, float w, float h)
    {
        Anch(r, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));
    }

    // то же, но по центру элемента
    public static void TLC(RectTransform r, float cx, float cy, float w, float h)
    {
        Anch(r, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(cx, -cy), new Vector2(w, h));
    }

    public static void AutoSize(TMP_Text t, float min, float max)
    {
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        t.enableWordWrapping = false;
    }

    // подпись UiKit в прямоугольнике от левого верхнего угла, с автоподбором размера
    public static TMP_Text Lbl(Transform parent, string text, float size, Color c, TextAlignmentOptions al, bool bold, float x, float y, float w, float h)
    {
        var tx = UiKit.MakeLabel(parent, text, size, c, al, bold);
        TL(tx.rectTransform, x, y, w, h);
        AutoSize(tx, Mathf.Max(11f, size * 0.6f), size);
        return tx;
    }

    // клик по картинке: без перехода цвета и без навигации (Enter и стрелки не жмут кнопку)
    public static void AddClick(Image img, UnityAction a)
    {
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(a);
    }
}
