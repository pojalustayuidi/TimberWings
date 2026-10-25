// Assets/_Project/Scripts/UI/Hud/CandyPlaque.cs (НЕ в Editor)
// Общие мелочи для всплывающих плашек Candy (RewardPopups, ZoneToast, SaveIndicator): светлое облачное поле,
// цвета-акценты и мягкая «пилюля» для цветных линий. Ничего не знает про логику плашек.
using UnityEngine;
using UnityEngine.UI;

public static class CandyPlaque
{
    static Sprite pill;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { pill = null; }

    // Candy-панель UiKit на весь родитель. Не участвует в layout-группах родителя, не ловит клики.
    // tint умножается на кремово-белое поле (белый = без окраски). borderScale меньше 1 = тоньше ободок для мелких плашек.
    public static Image Fill(Transform parent, Color tint, float borderScale = 0.45f, string name = "Fill")
    {
        Image img;
        using (UiKit.UseSkin(UiSkin.Candy))
            img = UiKit.MakeImage(parent, name, UiKit.Panel, true, borderScale);
        img.color = tint;
        img.raycastTarget = false;

        var le = img.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.transform.SetAsFirstSibling();   // поле всегда позади содержимого
        return img;
    }

    // светлая окраска поля цветом-акцентом: k = 0 белое, 1 полный цвет
    public static Color Tint(Color accent, float k = 0.3f)
    {
        return Color.Lerp(Color.white, new Color(accent.r, accent.g, accent.b, 1f), k);
    }

    // белый и почти белый акцент (так звали старые вызовы ShowText) = нейтральная облачная плашка
    public static bool IsNeutral(Color c) { return c.r > 0.9f && c.g > 0.9f && c.b > 0.9f; }

    // цвет акцента, читаемый как мелкая подпись на светлом поле: тёмнее, к индиго
    public static Color Readable(Color accent, float k = 0.5f)
    {
        return Color.Lerp(new Color(accent.r, accent.g, accent.b, 1f), UiCandy.Ink, k);
    }

    // скруглённая пилюля 48x24 с границей 12: для тонких цветных линий (Image.Type.Sliced)
    public static Sprite Pill
    {
        get
        {
            if (pill != null) return pill;
            const int w = 48, h = 24;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float c = Mathf.Clamp01(0.5f - UiDraw.RRect(x + 0.5f, y + 0.5f, w, h, h * 0.5f));
                    px[y * w + x] = new Color(1f, 1f, 1f, c);
                }
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.Apply();
            pill = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
            pill.hideFlags = HideFlags.HideAndDontSave;
            return pill;
        }
    }
}
