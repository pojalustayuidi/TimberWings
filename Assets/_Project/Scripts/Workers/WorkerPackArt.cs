// Assets/_Project/Scripts/Workers/WorkerPackArt.cs (НЕ в Editor)
// Графика паков, нарисованная кодом (ни одного нового ассета): подарочная коробка с бантом и листьями, лента-баннер для заголовка,
// четырёхконечная звезда рубашки, лист. Используется вкладкой «Паки» (WorkersWindow.Build.cs) и столом пака (WorkerPackTable.cs).
// Хотите свои спрайты: подмените картинки по именам дочерних объектов (Body, Lid, RibV, RibLid, LoopL, LoopR, Knot, Star, Glint, Leaf*, Shadow).
// Размеры даны в координатах коробки 300x300 от центра (вверх плюс), масштаб задаёт вызывающий.
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

public static class WorkerPackArt
{
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    static Sprite leaf, star4, tail;

    // цвет ленты на коробке: у Артельного белая, у остальных золотая
    public static Color RibbonOf(WorkerPackType t)
    {
        return t == WorkerPackType.Artel ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 0.82f, 0.32f, 1f);
    }

    public static readonly Color Gold = new Color(1f, 0.80f, 0.25f, 1f);

    static Image Part(Transform p, string name, Sprite sp, bool sliced, float bs, Vector2 pos, Vector2 size, float rot)
    {
        var im = UiKit.MakeImage(p, name, sp, sliced, bs);
        im.raycastTarget = false;
        Anch(im.rectTransform, Mid, Mid, pos, size);
        im.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
        return im;
    }

    // ============================================================ подарочная коробка
    public static RectTransform GiftBox(Transform parent, WorkerPackType type, float scale)
    {
        var box = NewRT("GiftBox", parent);
        Anch(box, Mid, Mid, Vector2.zero, new Vector2(300f, 300f));
        box.localScale = new Vector3(scale, scale, 1f);

        Part(box, "Shadow", UiKit.Slot, true, 0.8f, new Vector2(0f, -104f), new Vector2(180f, 36f), 0f);
        Part(box, "LeafL1", Leaf, false, 1f, new Vector2(-100f, -62f), new Vector2(78f, 38f), 28f);
        Part(box, "LeafL2", Leaf, false, 1f, new Vector2(-112f, -26f), new Vector2(70f, 34f), 68f);
        Part(box, "LeafR1", Leaf, false, 1f, new Vector2(100f, -62f), new Vector2(78f, 38f), 152f);
        Part(box, "LeafR2", Leaf, false, 1f, new Vector2(112f, -26f), new Vector2(70f, 34f), 112f);
        Part(box, "Body", UiKit.Panel, true, 0.5f, new Vector2(0f, -30f), new Vector2(140f, 104f), 0f);
        Part(box, "RibV", UiDraw.Pill, true, 0.3f, new Vector2(0f, -30f), new Vector2(24f, 104f), 0f);
        Part(box, "Lid", UiKit.Panel, true, 0.4f, new Vector2(0f, 36f), new Vector2(160f, 46f), 0f);
        Part(box, "RibLid", UiDraw.Pill, true, 0.3f, new Vector2(0f, 36f), new Vector2(24f, 46f), 0f);
        Part(box, "LoopL", UiDraw.Pill, true, 0.3f, new Vector2(-24f, 68f), new Vector2(46f, 30f), 28f);
        Part(box, "LoopR", UiDraw.Pill, true, 0.3f, new Vector2(24f, 68f), new Vector2(46f, 30f), -28f);
        Part(box, "Knot", UiDraw.Pill, true, 0.3f, new Vector2(0f, 62f), new Vector2(24f, 24f), 0f);
        Part(box, "Star", Star4, false, 1f, new Vector2(48f, 94f), new Vector2(42f, 42f), 12f);
        Part(box, "Glint", UiDraw.Pill, true, 0.2f, new Vector2(-52f, -14f), new Vector2(8f, 46f), 0f);

        Tint(box, type);
        return box;
    }

    // Перекрасить коробку под пак (стол пака использует одну коробку для всех трёх)
    public static void Tint(RectTransform box, WorkerPackType type)
    {
        Color accent = WorkerUi.PackColor(type);
        Color rib = RibbonOf(type);
        Color leafC = new Color(0.40f, 0.78f, 0.46f, 1f);

        Set(box, "Shadow", new Color(UiCandy.Ink.r, UiCandy.Ink.g, UiCandy.Ink.b, 0.16f));
        Set(box, "LeafL1", leafC); Set(box, "LeafL2", leafC);
        Set(box, "LeafR1", leafC); Set(box, "LeafR2", leafC);
        Set(box, "Body", Color.Lerp(Color.white, accent, 0.85f));
        Set(box, "Lid", Color.Lerp(Color.white, accent, 0.62f));
        Set(box, "RibV", rib); Set(box, "RibLid", rib);
        Set(box, "LoopL", rib); Set(box, "LoopR", rib);
        Set(box, "Knot", Color.Lerp(rib, accent, 0.3f));
        Set(box, "Star", Gold);
        Set(box, "Glint", new Color(1f, 1f, 1f, 0.8f));
        var star = box.Find("Star");
        if (star != null) star.gameObject.SetActive(type != WorkerPackType.Artel);
    }

    static void Set(RectTransform box, string n, Color c)
    {
        var t = box.Find(n);
        if (t == null) return;
        var im = t.GetComponent<Image>();
        if (im != null) im.color = c;
    }

    // ============================================================ лента-баннер за заголовком
    // pos: центр баннера от центра родителя; plateW: ширина плашки. Хвосты с вырезом торчат по бокам и чуть ниже
    public static RectTransform Banner(Transform parent, Vector2 pos, float plateW)
    {
        var rt = NewRT("Banner", parent);
        Anch(rt, Mid, Mid, pos, new Vector2(plateW, 70f));
        Color tailC = new Color(0.52f, 0.47f, 0.80f, 1f);

        var l = Part(rt, "TailL", Tail, false, 1f, new Vector2(-plateW * 0.5f - 38f, -12f), new Vector2(120f, 60f), 4f);
        l.color = tailC;
        var r = Part(rt, "TailR", Tail, false, 1f, new Vector2(plateW * 0.5f + 38f, -12f), new Vector2(120f, 60f), -4f);
        r.color = tailC;
        r.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        var sh = Part(rt, "PlateShadow", UiKit.Slot, true, 0.8f, new Vector2(0f, -6f), new Vector2(plateW, 70f), 0f);
        sh.color = new Color(UiCandy.Ink.r, UiCandy.Ink.g, UiCandy.Ink.b, 0.18f);
        var plate = Part(rt, "Plate", UiKit.Panel, true, 0.5f, Vector2.zero, new Vector2(plateW, 70f), 0f);
        plate.color = new Color(0.97f, 0.96f, 1f, 1f);
        return rt;
    }

    // ============================================================ спрайты, нарисованные кодом
    public static Sprite Star4
    {
        get
        {
            if (star4 == null)
                star4 = MakeTex("Pack_Star4", 96, 96, (u, v) =>
                {
                    float s = Mathf.Pow(Mathf.Abs(u), 0.62f) + Mathf.Pow(Mathf.Abs(v), 0.62f);
                    return Mathf.Clamp01((1f - s) * 3.5f);
                });
            return star4;
        }
    }

    public static Sprite Leaf
    {
        get
        {
            if (leaf == null)
                leaf = MakeTex("Pack_Leaf", 64, 32, (u, v) =>
                {
                    float hw = Mathf.Sin(Mathf.PI * (u * 0.5f + 0.5f));
                    return Mathf.Clamp01((hw * 0.9f - Mathf.Abs(v)) * 8f);
                });
            return leaf;
        }
    }

    // хвост ленты: слева V-образный вырез, справа сплошной (прячется под плашкой)
    public static Sprite Tail
    {
        get
        {
            if (tail == null)
                tail = MakeTex("Pack_Tail", 64, 32, (u, v) =>
                {
                    float notch = ((u + 1f) - 0.35f * (1f - Mathf.Abs(v))) * 20f;
                    float edge = (0.95f - Mathf.Abs(v)) * 20f;
                    return Mathf.Clamp01(notch) * Mathf.Clamp01(edge);
                });
            return tail;
        }
    }

    static Sprite MakeTex(string name, int w, int h, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float a = Mathf.Clamp01(alpha(u, v));
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        return s;
    }
}
