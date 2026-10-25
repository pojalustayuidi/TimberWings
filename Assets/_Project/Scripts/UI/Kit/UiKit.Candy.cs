// Assets/_Project/Scripts/UI/Kit/UiKit.Candy.cs   (НЕ в Editor)
// Candy-скин для UiKit: облачные светлые панели, мягкий индиго-контур, голубая «юбка» снизу, пастельные кнопки-«желе».
// Это вторая половина partial-класса UiKit: здесь только рисование Candy-спрайтов и переключатель скина.
// Геометрия (размеры спрайтов и границы 9-slice) совпадает со старым латунным скином, поэтому окна не «едут» при смене скина,
// а имена UiBtn / UiBar и все вызовы UiKit.Make* остаются прежними.
//
// Как включить в окне (скин действует только внутри using, потом возвращается прежний):
//     using (UiKit.UseSkin(UiSkin.Candy)) { BuildWindow(); }
// Спрайты берутся при сборке окна. Если окно достаёт спрайты UiKit позже (смена состояния кнопки в Update),
// заверни и это место в такой же using или запомни скин в поле окна.
//
// Соответствие стилей кнопок:  Amber = персик-лимон, Green = мята, Yellow = лимон, Gray = облачно-лавандовая.
// Соответствие полос:          Amber = персик, Green = мята, Blue = небо.
// Иконки (задача 2): в Candy UiKit.Icon(icon) сначала берёт готовый спрайт из CandyHud (если HUD Candy есть в сцене),
// иначе рисует сам: 10 иконок заново (монета, звезда, галочка, крест, стрелки, play, часы, замок, молния),
// остальные берут старый рисунок и проходят «конфетный» фильтр (мягче цвета, глянец сверху) с индиго-контуром.
// UiKit.CandyIcon(icon) отдаёт Candy-иконку независимо от текущего скина (для HUD и мест вне окон).
using System;
using UnityEngine;
using Sdf = System.Func<float, float, float>;
using Shade = System.Func<float, float, UnityEngine.Color>;

public enum UiSkin { Brass, Candy }

public static partial class UiKit
{
    // ---------- переключатель скина ----------
    // По умолчанию Brass: пока окно не перенесено, оно остаётся латунным. В задаче 19 значение по умолчанию станет Candy.
    public static UiSkin Skin = UiSkin.Brass;
    public static bool IsCandy { get { return Skin == UiSkin.Candy; } }

    public static IDisposable UseSkin(UiSkin skin) { return new SkinScope(skin); }

    sealed class SkinScope : IDisposable
    {
        readonly UiSkin prev;
        bool disposed;
        public SkinScope(UiSkin skin) { prev = Skin; Skin = skin; }
        public void Dispose() { if (disposed) return; disposed = true; Skin = prev; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSkin() { Skin = UiSkin.Brass; }   // на случай отключённой перезагрузки домена

    // ---------- палитра Candy ----------
    public static readonly Color CandyInk = C(0x2B, 0x2F, 0x5C);      // текст и контур (как в CandyTaskView)
    public static readonly Color CandyMuted = C(0x5E, 0x6A, 0x96);    // второстепенный текст
    public static readonly Color CandyCloud = C(0xF4, 0xF7, 0xFF);    // светлое поле
    public static readonly Color CandyLavender = C(0xBD, 0xC2, 0xFA); // тонкий внутренний ободок
    public static readonly Color CandySky = C(0x8F, 0xC8, 0xFF);      // «юбка» панели
    public static readonly Color CandySkyHi = C(0xBF, 0xE0, 0xFF);

    // ======================================================================================
    //  ПАНЕЛИ
    // ======================================================================================
    // header = true: сверху лавандовая лента-заголовок (аналог деревянной планки). Границы 9-slice те же: 26 и 36 сверху.
    static Sprite BuildPanelCandy(bool header)
    {
        const float S = 96f, R = 24f;
        var p = new Painter(96, 96);
        p.Fill(RR(0, 0, S, S, R), CandyInk);                                           // контур
        Sdf body = RR(2.5f, 2.5f, S - 5f, S - 5f, R - 2.5f);
        p.Fill(body, VGrad(2.5f, S - 2.5f, C(0xE6, 0xEE, 0xFF), Color.white));         // облачное поле
        p.Fill(Ring(body, 1.6f), new Color(CandyLavender.r, CandyLavender.g, CandyLavender.b, 0.9f));   // лавандовый ободок
        p.Fill(Intersect(body, RR(0, 0, S, 15f, 0f)), VGrad(2.5f, 15f, CandySky, CandySkyHi));          // голубая «юбка»
        p.Fill(RR(8f, 15f, S - 16f, 1.6f, 0.8f), new Color(1f, 1f, 1f, 0.85f));        // светлая кромка над юбкой

        if (header)
        {
            float hy = S - 32f;   // нижняя граница ленты-заголовка
            p.Fill(ClipMin(RR(0, hy - R, S, 32f + R, R), hy), CandyInk);
            p.Fill(ClipMin(RR(2.5f, hy - R, S - 5f, 32f + R - 2.5f, R - 2.5f), hy + 2f), VGrad(hy + 2f, S - 2.5f, C(0x8E, 0x86, 0xEE), C(0xB9, 0xB3, 0xFF)));
            p.Fill(Intersect(body, RR(0, hy - 5f, S, 5f, 0f)), new Color(CandyInk.r, CandyInk.g, CandyInk.b, 0.12f));   // тень под лентой
            p.Fill(RR(12f, S - 9f, S - 24f, 2.5f, 1.25f), new Color(1f, 1f, 1f, 0.45f));                                  // блик на ленте
        }
        else
        {
            p.Fill(RR(14f, S - 9f, S - 28f, 2.5f, 1.25f), new Color(1f, 1f, 1f, 0.9f));   // блик сверху
        }
        return Make(p, 26, 26, 26, header ? 36 : 26);
    }

    // табличка: тёплый персиковый «пряник»
    static Sprite BuildPlaqueCandy()
    {
        var p = new Painter(72, 44);
        p.Fill(RR(0, 0, 72, 44, 16), CandyInk);
        Sdf face = RR(2.5f, 2.5f, 67f, 39f, 13.5f);
        p.Fill(face, VGrad(2.5f, 41.5f, C(0xFF, 0xC9, 0x8F), C(0xFF, 0xEA, 0xCB)));
        p.Fill(Ring(face, 1.5f), new Color(1f, 1f, 1f, 0.35f));
        p.Fill(RR(9f, 34.5f, 54f, 3f, 1.5f), new Color(1f, 1f, 1f, 0.6f));
        return Make(p, 18, 18, 18, 18);
    }

    // ячейка: мягкая вдавленность (сверху чуть темнее)
    static Sprite BuildSlotCandy()
    {
        var p = new Painter(56, 56);
        p.Fill(RR(0, 0, 56, 56, 14), CandyInk);
        Sdf inner = RR(2.5f, 2.5f, 51f, 51f, 11.5f);
        p.Fill(inner, VGrad(2.5f, 53.5f, C(0xF4, 0xF7, 0xFF), C(0xCF, 0xD7, 0xF6)));
        p.Fill(Ring(inner, 1.5f), new Color(CandyInk.r, CandyInk.g, CandyInk.b, 0.12f));
        return Make(p, 16, 16, 16, 16);
    }

    // ======================================================================================
    //  ПОЛОСЫ
    // ======================================================================================
    // рамка: заливка в MakeBar отступает на 5 единиц, поэтому контур 3 и дорожка 22 дают ровный просвет
    static Sprite BuildBarFrameCandy()
    {
        var p = new Painter(128, 28);
        p.Fill(RR(0, 0, 128, 28, 14), CandyInk);
        p.Fill(RR(3, 3, 122, 22, 11), VGrad(3, 25, C(0xF4, 0xF7, 0xFF), C(0xCB, 0xD4, 0xF2)));
        return Make(p, 14, 14, 14, 14);
    }

    static Sprite BuildBarFillCandy(UiBar color)
    {
        Color c;
        switch (color)
        {
            case UiBar.Green: c = C(0x4F, 0xD9, 0xA0); break;   // мята
            case UiBar.Blue: c = C(0x4D, 0xB2, 0xFF); break;    // небо
            default: c = C(0xFF, 0xB4, 0x5E); break;            // персик
        }
        var p = new Painter(256, 16);
        p.Fill(RR(0, 0, 256, 16, 8), VGrad(0, 16, Darken(c, 0.12f), Brighten(c, 0.25f)));
        for (int i = 0; i < 7; i++)                                                         // пузырьки
            p.Fill(Circle(22f + i * 36f, 5.5f + (i % 2) * 2.5f, 1.4f), new Color(1f, 1f, 1f, 0.35f));
        p.Fill(RR(8, 10, 240, 3.5f, 1.75f), new Color(1f, 1f, 1f, 0.5f));                   // глянец
        return Make(p, 0, 0, 0, 0);
    }

    // ======================================================================================
    //  МЕДАЛЬОН, КЛАВИША, РАМКА КАРТЫ, ХВОСТИК, ВЫМПЕЛ
    // ======================================================================================
    static Sprite BuildMedallionCandy(bool withKey)
    {
        var p = new Painter(48, 48);
        p.Fill(Circle(24, 24, 24), CandyInk);
        p.Fill(Circle(24, 24, 21.5f), VGrad(2.5f, 45.5f, C(0xA2, 0x9B, 0xF2), C(0xD6, 0xD2, 0xFF)));   // лавандовое кольцо
        p.Fill(Circle(24, 24, 18.5f), VGrad(5.5f, 42.5f, C(0xE6, 0xEE, 0xFF), Color.white));            // светлая середина
        p.Fill(RR(13f, 36f, 22f, 2.5f, 1.25f), new Color(1f, 1f, 1f, 0.9f));
        if (withKey)
        {
            p.Fill(RR(10.5f, 9f, 27f, 29f, 8f), CandyInk);
            p.Fill(RR(12f, 10.5f, 24f, 26f, 7f), C(0xE8, 0xB5, 0x3A));                                  // «губа» клавиши
            p.Fill(RR(12f, 14.5f, 24f, 22f, 7f), VGrad(14.5f, 36.5f, C(0xFF, 0xE0, 0x66), C(0xFF, 0xF4, 0xA8)));
            p.Fill(RR(15f, 31f, 18f, 2.5f, 1.25f), new Color(1f, 1f, 1f, 0.6f));
        }
        return Make(p, 0, 0, 0, 0);
    }

    static Sprite BuildMapRingCandy()
    {
        var p = new Painter(96, 96);
        p.Fill(Ring(Circle(48, 48, 48f), 6f), CandyInk);
        p.Fill(Ring(Circle(48, 48, 46.8f), 3.8f), VGrad(0, 96, C(0xA2, 0x9B, 0xF2), Color.white));
        return Make(p, 0, 0, 0, 0);
    }

    // хвостик под плашкой-подсказкой: цвета «юбки» панели, чтобы стык был незаметен
    static Sprite BuildTailCandy()
    {
        var p = new Painter(36, 22);
        p.Fill(Poly(V(0, 22), V(36, 22), V(18, 0)), CandyInk);
        p.Fill(Poly(V(4, 22), V(32, 22), V(18, 5.5f)), VGrad(0, 22, CandySky, CandySkyHi));
        return Make(p, 0, 0, 0, 0);
    }

    // вымпел, красится через Image.color. Пропорции 50:64 те же
    static Sprite BuildBannerCandy()
    {
        var p = new Painter(50, 64);
        p.Fill(Poly(V(0, 64), V(50, 64), V(50, 11), V(25, 0), V(0, 11)), CandyInk);
        Sdf body = Poly(V(2.5f, 64), V(47.5f, 64), V(47.5f, 12.2f), V(25, 3.2f), V(2.5f, 12.2f));
        p.Fill(body, VGrad(3, 64, new Color(0.84f, 0.86f, 1f, 1f), Color.white));
        p.Fill(RR(6, 14, 4f, 44f, 2f), new Color(1f, 1f, 1f, 0.5f));
        p.Fill(Intersect(body, RR(0, 50, 50, 14, 0)), new Color(0f, 0f, 0f, 0.08f));
        return Make(p, 0, 0, 0, 0);
    }

    // ======================================================================================
    //  КНОПКИ-«ЖЕЛЕ»
    // ======================================================================================
    static void CandyBtnColors(UiBtn s, out Color top, out Color mid, out Color lip)
    {
        switch (s)
        {
            case UiBtn.Amber: top = C(0xFF, 0xE0, 0xA0); mid = C(0xFF, 0xC4, 0x63); lip = C(0xE3, 0x9A, 0x3A); break;    // персик-лимон
            case UiBtn.Green: top = C(0x9D, 0xF0, 0xC8); mid = C(0x5F, 0xD9, 0xA2); lip = C(0x33, 0xA8, 0x77); break;    // мята
            case UiBtn.Yellow: top = C(0xFF, 0xF0, 0x8A); mid = C(0xFF, 0xDD, 0x48); lip = C(0xD4, 0xAE, 0x20); break;   // лимон
            default: top = C(0xE4, 0xE8, 0xFA); mid = C(0xC9, 0xCF, 0xEA); lip = C(0x9A, 0xA2, 0xC9); break;              // облако
        }
    }

    static Sprite BuildButtonCandy(UiBtn style, UiBtnState state)
    {
        const float W = 64f, H = 44f, R = 16f;
        Color top, mid, lip;
        CandyBtnColors(state == UiBtnState.Disabled ? UiBtn.Gray : style, out top, out mid, out lip);
        float lipH = 6f;
        if (state == UiBtnState.Hover) { top = Brighten(top, 0.18f); mid = Brighten(mid, 0.12f); }
        else if (state == UiBtnState.Pressed) { top = Darken(top, 0.05f); mid = Darken(mid, 0.07f); lipH = 2f; }
        else if (state == UiBtnState.Disabled) lipH = 3f;

        var p = new Painter(64, 44);
        p.Fill(RR(0, 0, W, H, R), CandyInk);
        p.Fill(RR(2.5f, 2.5f, W - 5f, H - 5f, R - 2.5f), lip);
        float fy = 2.5f + lipH;
        p.Fill(RR(2.5f, fy, W - 5f, H - 2.5f - fy, R - 2.5f), VGrad(fy, H - 2.5f, mid, top));
        if (state != UiBtnState.Disabled)
        {
            p.Fill(RR(9f, H - 10.5f, W - 18f, 4f, 2f), new Color(1f, 1f, 1f, 0.5f));          // глянец сверху
            p.Fill(RR(11f, fy + 2f, W - 22f, 2f, 1f), new Color(1f, 1f, 1f, 0.18f));          // отсвет снизу, как у желе
        }
        return Make(p, 16, 16, 16, 16);
    }

    // ======================================================================================
    //  ИКОНКИ
    // ======================================================================================
    public static Sprite CandyIcon(UiIcon icon)
    {
        using (UseSkin(UiSkin.Candy)) return Icon(icon);
    }

    // ключи спрайтов Candy Kit в CandyHud (их собирает Editor-меню Candy Kit)
    static string CandyHudKey(UiIcon icon)
    {
        switch (icon)
        {
            case UiIcon.Coin: return "icon_coin";
            case UiIcon.Log: return "icon_log";
            case UiIcon.Plank:
            case UiIcon.Planks: return "icon_planks";
            case UiIcon.Axe: return "icon_axe";
            case UiIcon.Boot: return "icon_boot";
            case UiIcon.Backpack: return "icon_backpack";
            case UiIcon.Saw: return "icon_saw";
            case UiIcon.Star: return "icon_star";
            case UiIcon.Trophy: return "icon_trophy";
            case UiIcon.Paw: return "icon_paw";
            case UiIcon.Lock: return "icon_lock";
            case UiIcon.Box: return "icon_gift";
        }
        return null;
    }

    static Sprite CandyHudIcon(UiIcon icon)
    {
        var hud = CandyHud.Instance;
        if (hud == null) return null;
        string key = CandyHudKey(icon);
        return key != null ? hud.Get(key) : null;
    }

    static Sprite BuildIconCandy(UiIcon icon)
    {
        var p = new Painter(64, 64);
        if (!DrawIconCandy(p, icon))
        {
            DrawIcon(p, icon);                          // старый рисунок как основа
            if (icon != UiIcon.Gem) CandySoften(p);     // камень красится через Image.color: его тени не трогаем
        }
        p.Outline(2.4f, CandyInk);
        return Make(p, 0, 0, 0, 0);
    }

    // «конфетный» фильтр: чуть насыщеннее, светлее, плюс мягкий глянец к верху иконки
    static void CandySoften(Painter p)
    {
        for (int i = 0; i < p.px.Length; i++)
        {
            Color c = p.px[i];
            if (c.a <= 0.001f) continue;
            float t = (float)(i / p.W) / p.H;   // 0 внизу, 1 вверху
            float gray = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            float r = Mathf.LerpUnclamped(gray, c.r, 1.12f);
            float g = Mathf.LerpUnclamped(gray, c.g, 1.12f);
            float b = Mathf.LerpUnclamped(gray, c.b, 1.12f);
            float lift = 0.08f + 0.22f * Mathf.Clamp01((t - 0.5f) / 0.5f);
            c.r = Mathf.Clamp01(Mathf.Lerp(r, 1f, lift));
            c.g = Mathf.Clamp01(Mathf.Lerp(g, 1f, lift));
            c.b = Mathf.Clamp01(Mathf.Lerp(b, 1f, lift));
            p.px[i] = c;
        }
    }

    // иконки, которые рисуем заново (сильнее всего были завязаны на латунь и тёмно-синий). Остальные: false
    static bool DrawIconCandy(Painter p, UiIcon icon)
    {
        Color white = Color.white;
        Shade mint = VGrad(8, 57, C(0x2F, 0xBF, 0x84), C(0x9D, 0xF0, 0xC8));
        Shade gold = VGrad(3, 61, C(0xFF, 0xB2, 0x30), C(0xFF, 0xF0, 0x7A));
        switch (icon)
        {
            case UiIcon.Coin:
                p.Fill(Circle(32, 32, 27), VGrad(5, 59, C(0xFF, 0xB2, 0x30), C(0xFF, 0xE6, 0x80)));
                p.Fill(Circle(32, 32, 21.5f), VGrad(10, 54, C(0xFF, 0xCF, 0x45), C(0xFF, 0xF4, 0xB0)));
                p.Fill(Ring(Circle(32, 32, 15.5f), 1.8f), C(0xE8, 0x9A, 0x1E, 150));
                p.Fill(Capsule(22, 41, 30, 49, 1.9f), new Color(1f, 1f, 1f, 0.85f));
                p.Fill(Circle(35, 51.5f, 1.5f), new Color(1f, 1f, 1f, 0.85f));
                return true;

            case UiIcon.Star:
            {
                var pts = new Vector2[10];
                var small = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    float r = (i % 2 == 0) ? 27f : 12.5f;
                    pts[i] = V(32f + Mathf.Cos(a) * r, 30.5f + Mathf.Sin(a) * r);
                    small[i] = V(32f + Mathf.Cos(a) * r * 0.5f, 33f + Mathf.Sin(a) * r * 0.5f);
                }
                p.Fill(Poly(pts), VGrad(8, 58, C(0xFF, 0xB8, 0x2E), C(0xFF, 0xF0, 0x7A)));
                p.Fill(Poly(small), new Color(1f, 1f, 1f, 0.4f));
                return true;
            }

            case UiIcon.Check:
                p.Fill(Circle(32, 32, 27), VGrad(5, 59, C(0x2F, 0xBF, 0x84), C(0x9D, 0xF0, 0xC8)));
                p.Fill(Ellipse(32, 47, 17f, 6f), new Color(1f, 1f, 1f, 0.25f));
                p.Fill(Capsule(19, 32, 28, 22.5f, 4.6f), white);
                p.Fill(Capsule(28, 22.5f, 46, 42, 4.6f), white);
                return true;

            case UiIcon.Cross:
            {
                Shade berry = VGrad(14, 50, C(0xF0, 0x45, 0x6F), C(0xFF, 0x8F, 0xA6));
                p.Fill(Capsule(18, 18, 46, 46, 5.4f), berry);
                p.Fill(Capsule(18, 46, 46, 18, 5.4f), berry);
                p.Fill(Capsule(21, 43, 28, 50, 1.2f), new Color(1f, 1f, 1f, 0.5f));
                return true;
            }

            case UiIcon.ArrowUp:
                p.Fill(Poly(V(32, 57), V(55, 31), V(41, 31), V(41, 8), V(23, 8), V(23, 31), V(9, 31)), mint);
                p.Fill(Capsule(27, 14, 27, 31, 1.4f), new Color(1f, 1f, 1f, 0.45f));
                return true;

            case UiIcon.ArrowRight:
                p.Fill(Poly(V(6, 23), V(34, 23), V(34, 11), V(59, 32), V(34, 53), V(34, 41), V(6, 41)), VGrad(11, 53, C(0xFF, 0xAA, 0x3D), C(0xFF, 0xE0, 0x8A)));
                p.Fill(Capsule(10, 36, 31, 36, 1.4f), new Color(1f, 1f, 1f, 0.5f));
                return true;

            case UiIcon.Play:
                p.Fill(Poly(V(16, 8), V(16, 56), V(56, 32)), mint);
                p.Fill(Capsule(21, 22, 21, 46, 1.5f), new Color(1f, 1f, 1f, 0.45f));
                return true;

            case UiIcon.Clock:
                p.Fill(Circle(32, 32, 27), VGrad(5, 59, C(0x8E, 0x86, 0xEE), C(0xC9, 0xC4, 0xFF)));
                p.Fill(Circle(32, 32, 21.5f), VGrad(10.5f, 53.5f, C(0xE6, 0xEE, 0xFF), white));
                p.Fill(Capsule(32, 49, 32, 52, 1.3f), CandyInk);
                p.Fill(Capsule(32, 12, 32, 15, 1.3f), CandyInk);
                p.Fill(Capsule(12, 32, 15, 32, 1.3f), CandyInk);
                p.Fill(Capsule(49, 32, 52, 32, 1.3f), CandyInk);
                p.Fill(Capsule(32, 32, 32, 45, 2.2f), CandyInk);
                p.Fill(Capsule(32, 32, 42, 27, 2.2f), CandyInk);
                p.Fill(Circle(32, 32, 3.4f), C(0xFF, 0x6B, 0x8B));
                return true;

            case UiIcon.Lock:
            {
                Sdf shackle = Ring(Circle(32, 38, 13), 5.5f);
                Shade steel = VGrad(26, 52, C(0x9A, 0xA2, 0xC9), C(0xE4, 0xE8, 0xFA));
                p.Fill(ClipMin(shackle, 38f), steel);
                p.Fill(RR(19, 26, 5.5f, 12, 0), steel);
                p.Fill(RR(39.5f, 26, 5.5f, 12, 0), steel);
                p.Fill(RR(12, 8, 40, 28, 9), VGrad(8, 36, C(0xFF, 0xB2, 0x30), C(0xFF, 0xE6, 0x80)));
                p.Fill(RR(15, 29, 34, 3f, 1.5f), new Color(1f, 1f, 1f, 0.5f));
                p.Fill(Circle(32, 24, 4.3f), CandyInk);
                p.Fill(Poly(V(29.5f, 22), V(34.5f, 22), V(36, 13), V(28, 13)), CandyInk);
                return true;
            }

            case UiIcon.Bolt:
                p.Fill(Poly(V(38, 61), V(15, 31), V(29, 31), V(24, 3), V(50, 39), V(36, 39), V(47, 61)), gold);
                p.Fill(Poly(V(38, 61), V(15, 31), V(22, 31), V(42, 58)), new Color(1f, 1f, 1f, 0.45f));
                return true;
        }
        return false;
    }
}
