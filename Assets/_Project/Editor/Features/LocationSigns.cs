#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Features/LocationSigns.cs   (версия 2)
// Таблички острова «Кедровый Мыс». Их мало, и у каждой одна понятная роль:
//
//  1) ГЛАВНЫЙ УКАЗАТЕЛЬ в центре (один): столб с досками «Кедровый Бор», «Лавка Миры», «Подвесной мост»...
//     На каждой доске слева стрелка: куда идти (стрелка вверх = от камеры, как на мини-карте).
//  2) УКАЗАТЕЛЬ НА РАЗВИЛКЕ (только если у площадок есть родитель-тропа): куда пойти дальше и «назад в центр».
//  3) ТАБЛИЧКА ЗОНЫ у входа на каждую площадку и у моста: просто название места, «вы здесь».
//
//  Каждая табличка это ОТДЕЛЬНЫЙ объект на сцене (Location/Signs/Sign_...): можно двигать, поворачивать,
//  менять текст (дочерний объект Text) и сохранять как префаб. Доски лицом к камере (yaw 45, как в игре).
//  Места подбираются так, чтобы не стоять в воде, на склоне и внутри камней/деревьев.
//
//  Запуск: меню Tools -> BuilderSim -> Location: указатели и названия зон (остров перестраивать не надо),
//          или из генератора строкой  LocationSigns.Build(ctx);  после Center/Cliffs (до Polish).
//  Названия зон правятся в таблице ZoneNames ниже. Зоны для карты создаются вашим MapZone.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ====================================================================================
//  ТАБЛИЦА НАЗВАНИЙ ЗОН
//  keys: подстроки имени площадки (в нижнем регистре). Первое совпадение выигрывает.
//  Если имя площадки не нашлось, зона получит имя самой площадки.
// ====================================================================================
public static class ZoneNames
{
    public sealed class Def
    {
        public string id, name, desc;
        public string[] keys;
    }

    static Def D(string id, string name, string desc, params string[] keys)
    {
        return new Def { id = id, name = name, desc = desc, keys = keys };
    }

    public static readonly Def Center =
        D("center", "Старый Стапель", "Каменный круг, где когда-то собирали дирижабли.");

    public static readonly Def Bridge =
        D("bridge", "Подвесной мост", "Скрипит, качается, но держит.");

    static readonly Def[] Table =
    {
        D("forest",  "Кедровый Бор",        "Шишки, грибы и светлячки. Здесь рубят дрова.", "forest", "лес", "бор", "wood"),
        D("market",  "Лавка Миры",          "Торговый прилавок: тут покупают и продают.",   "market", "shop", "trade", "stall", "mira", "мира", "торг", "рын", "лавк"),
        D("hut",     "Дядина Хижина",       "Старая хижина, которую пора починить.",        "hut", "house", "cabin", "home", "хиж", "дом"),
        D("camp",    "Лагерь у Палатки",    "С этого места всё и началось.",                "camp", "tent", "spawn", "лагер", "палат"),
        D("meadow",  "Ромашковая Поляна",   "Тихое место для прогулок и бабочек.",          "meadow", "glade", "field", "полян", "луг"),
        D("quarry",  "Каменный Карьер",     "Серые камни и эхо от кирки.",                  "quarry", "mine", "stone", "rock", "карьер", "шахт", "камен"),
        D("beach",   "Песчаный Берег",      "Тёплый песок и шум облачного моря.",           "beach", "shore", "coast", "берег", "пляж"),
        D("dock",    "Причал Дирижаблей",   "Сюда редко, но заходят грузовые дирижабли.",   "dock", "port", "pier", "airship", "причал", "порт"),
        D("pond",    "Тихий Пруд",          "Гладкая вода и кувшинки.",                     "pond", "lake", "пруд", "озер"),
        D("fisher",  "Рыбацкий Мысок",      "Удочка, ведёрко и никакой спешки.",            "fish", "рыб"),
        D("garden",  "Огород",              "Грядки, клумбы и пугало.",                     "garden", "farm", "огород", "сад"),
        D("cairn",   "Каменный Кэрн",       "Пирамидка из камней. Кто-то её складывал.",    "cairn", "кэрн", "кейрн"),
    };

    public static Def ForPad(string padName)
    {
        string n = (padName ?? "").ToLowerInvariant();
        foreach (var d in Table)
            foreach (var k in d.keys)
                if (n.Contains(k)) return d;
        string id = n.Trim().Replace(' ', '_');
        if (id.Length == 0) id = "zone";
        return new Def { id = id, name = string.IsNullOrEmpty(padName) ? "Зона" : padName, desc = "", keys = new string[0] };
    }
}

// ====================================================================================
public sealed class LocationSigns
{
    // ----- внешний вид -----
    const float CamYaw = 45f;          // как yaw в CameraFollow / PlayerMovement
    const float Tilt = 22f;            // доски чуть откинуты назад, к камере сверху (так текст читается лучше)
    const float WaterY = -0.12f;       // ниже этого считаем водой (как в LocationCliffs)
    const int MaxBoards = 7;           // сколько досок максимум на одном столбе

    const string PrefsKey = "BuilderSim.LocationBuilder.Settings.v2";
    const string OwnMatDir = LocKit.Dir + "/Signs/Materials";

    static readonly Color WoodC = new Color(0.52f, 0.34f, 0.19f);
    static readonly Color WoodLightC = new Color(0.78f, 0.60f, 0.38f);
    static readonly Color PlaqueC = new Color(0.93f, 0.80f, 0.56f);
    static readonly Color FrameC = new Color(0.34f, 0.22f, 0.13f);
    static readonly Color StoneC = new Color(0.56f, 0.58f, 0.60f);
    static readonly Color InkC = new Color(0.17f, 0.10f, 0.06f);

    // ----- данные, одинаковые для обоих способов запуска -----
    sealed class Src
    {
        public LocationBuilder.Settings s;
        public Transform root;
        public float ws, centerR;
        public Func<float, float, float> heightU;
        public bool hasBridge;
        public Vector2 bridgePos;
        public float bridgeSide = -1f;
        public string matDir;
    }

    sealed class PadInfo
    {
        public string name, parentName;
        public ZoneNames.Def zone;
        public Vector2 pos;
        public float rad, fork;
        public Color col;
    }

    sealed class Target { public string name; public Vector2 pos; public Color col; }

    sealed class Board
    {
        public string text;
        public Color col;
        public bool arrow;
        public float angle;      // угол стрелки на доске, градусы (0 = вправо, 90 = вверх)
        public float sortKey;
    }

    readonly Src src;
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    readonly List<PadInfo> pads = new List<PadInfo>();
    readonly HashSet<string> existingZones = new HashSet<string>();

    Transform node, zonesRoot;
    TMP_FontAsset font;
    bool warnedFont;
    int signCount;

    LocationSigns(Src s) { src = s; }

    float H(Vector2 p) { return src.heightU(p.x, p.y); }
    bool Dry(Vector2 p) { return H(p) > WaterY; }

    // ====================================================================== вход из генератора
    public static void Build(LocationCtx ctx)
    {
        var s = new Src
        {
            s = ctx.s,
            root = ctx.root,
            ws = ctx.ws,
            centerR = ctx.centerRadius,
            heightU = ctx.heightU,
            hasBridge = ctx.hasBridge,
            bridgePos = ctx.bridgePos,
            bridgeSide = ctx.bridgeSide,
            matDir = LocKit.MatDir
        };
        new LocationSigns(s).Run();
    }

    // ====================================================================== вход из меню (без перестройки)
    // вызывается кнопкой в окне Location Builder (отдельного пункта меню больше нет)
    internal static void BuildExisting()
    {
        var root = FindRoot();
        if (root == null)
        {
            EditorUtility.DisplayDialog("Указатели", "В сцене нет объекта \"Location\". Сначала построй остров в Location Builder.", "OK");
            return;
        }

        var s = new LocationBuilder.Settings();
        string json = EditorPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, s); }
            catch (Exception) { }
        }

        float ws = Mathf.Max(0.01f, s.worldScale);
        var terrain = root.GetComponentInChildren<Terrain>();
        Transform rt = root.transform;
        Func<float, float, float> h = (x, z) =>
        {
            if (terrain == null) return 0f;
            Vector3 w = rt.TransformPoint(new Vector3(x * ws, 0f, z * ws));
            return (terrain.SampleHeight(w) + terrain.transform.position.y - rt.position.y) / ws;
        };

        var src = new Src
        {
            s = s,
            root = rt,
            ws = ws,
            centerR = s.centerRadius,
            heightU = h,
            matDir = OwnMatDir
        };

        // мост ищем по имени среди объектов острова
        Transform bridge = FindByName(rt, "bridge");
        if (bridge != null)
        {
            Vector3 l = rt.InverseTransformPoint(bridge.position) / ws;
            src.hasBridge = true;
            src.bridgePos = new Vector2(l.x, l.z);
            src.bridgeSide = l.x >= 0f ? 1f : -1f;
        }

        LocKit.EnsureFolder(src.matDir);

        foreach (string n in new[] { "Signs", "Zones" })
        {
            Transform old;
            while ((old = rt.Find(n)) != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        try { new LocationSigns(src).Run(); }
        catch (Exception e) { Debug.LogException(e); }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static GameObject FindRoot()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            if (!sc.isLoaded) continue;
            foreach (var go in sc.GetRootGameObjects())
                if (go.name == "Location") return go;
        }
        return null;
    }

    static Transform FindByName(Transform root, string lowerPart)
    {
        var q = new Queue<Transform>();
        q.Enqueue(root);
        int guard = 0;
        while (q.Count > 0 && guard++ < 4000)
        {
            var t = q.Dequeue();
            if (t != root && t.name.ToLowerInvariant().Contains(lowerPart)) return t;
            foreach (Transform c in t) q.Enqueue(c);
        }
        return null;
    }

    // ====================================================================== сборка
    void Run()
    {
        var go = new GameObject("Signs");
        go.transform.SetParent(src.root, false);
        go.transform.localScale = Vector3.one * src.ws;
        node = go.transform;

        var zg = new GameObject("Zones");
        zg.transform.SetParent(src.root, false);
        zonesRoot = zg.transform;

        foreach (var ez in UnityEngine.Object.FindObjectsByType<MapZone>(FindObjectsSortMode.None))
            if (ez != null && ez.transform.parent != zonesRoot) existingZones.Add(ez.zoneName);

        font = FindFont();
        Physics.SyncTransforms();
        ReadPads();

        // ----- зоны для карты
        AddZone(ZoneNames.Center, Vector2.zero, Mathf.Max(6f, src.centerR + 3f), new Color(1f, 0.85f, 0.4f));
        var log = new System.Text.StringBuilder("LocationSigns: зоны острова:\n");
        log.AppendLine("  центр -> " + ZoneNames.Center.name);
        foreach (var p in pads)
        {
            AddZone(p.zone, p.pos, p.rad * 1.15f, p.col);
            log.AppendLine("  площадка \"" + p.name + "\" -> " + p.zone.name);
        }
        Color bridgeCol = new Color(0.45f, 0.75f, 0.95f);
        if (src.hasBridge)
        {
            AddZone(ZoneNames.Bridge, src.bridgePos, 6f, bridgeCol);
            log.AppendLine("  мост -> " + ZoneNames.Bridge.name);
        }

        // ----- таблички
        bool main = CenterSignpost(bridgeCol);
        int forks = ForkSignposts();
        foreach (var p in pads) NameSign(p);
        if (src.hasBridge) BridgeNameSign(bridgeCol);

        log.AppendLine("Табличек: " + signCount + " (главный указатель: " + (main ? "да" : "нет") + ", развилок: " + forks + ")");
        Debug.Log(log.ToString());
    }

    void ReadPads()
    {
        pads.Clear();
        Transform padsT = src.root.Find("Pads");
        foreach (var d in src.s.pads)
        {
            var p = new PadInfo();
            p.name = d.name;
            Transform t = padsT != null ? padsT.Find("PAD_" + d.name) : null;
            if (t != null)
            {
                Vector3 l = src.root.InverseTransformPoint(t.position) / src.ws;
                p.pos = new Vector2(l.x, l.z);
            }
            else
            {
                float a = d.angle * Mathf.Deg2Rad;
                p.pos = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * d.distance;
            }
            p.rad = d.round ? d.size.x : Mathf.Max(d.size.x, d.size.y) * 0.5f;
            p.col = d.gizmoColor;
            p.zone = ZoneNames.ForPad(d.name);
            p.parentName = d.parent;
            p.fork = d.forkDistance;
            pads.Add(p);
        }
    }

    PadInfo FindPad(string n)
    {
        if (string.IsNullOrEmpty(n)) return null;
        foreach (var p in pads)
            if (string.Equals(p.name, n, StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }

    // ====================================================================== 1) главный указатель в центре
    bool CenterSignpost(Color bridgeCol)
    {
        var targets = new List<Target>();
        var trailDirs = new List<Vector2>();
        foreach (var p in pads)
        {
            if (FindPad(p.parentName) != null) continue;      // дочерние площадки покажет указатель на развилке
            targets.Add(new Target { name = p.zone.name, pos = p.pos, col = p.col });
            if (p.pos.sqrMagnitude > 0.01f) trailDirs.Add(p.pos.normalized);
        }
        if (src.hasBridge)
            targets.Add(new Target { name = ZoneNames.Bridge.name, pos = src.bridgePos, col = bridgeCol });
        if (targets.Count == 0) return false;

        Vector2? spot = FindCenterSpot(trailDirs);
        if (spot == null)
        {
            Debug.LogWarning("LocationSigns: в центре не нашлось свободного места под главный указатель.");
            return false;
        }

        var boards = MakeBoards(spot.Value, targets);
        MakeSign("Указатель", spot.Value, boards, false);
        return true;
    }

    // место по краю площади: подальше от спавна, не на тропах, не внутри объектов; сначала ближе к югу (откуда игрок пришёл)
    Vector2? FindCenterSpot(List<Vector2> trailDirs)
    {
        float R = Mathf.Max(7f, src.centerR);
        float[] radii = { R * 0.8f, R * 0.95f, R * 0.65f };
        Vector2 spawn = new Vector2(0f, -4.2f);

        for (int pass = 0; pass < 2; pass++)          // сначала с проверкой коллайдеров, потом без
            foreach (float r in radii)
                for (int k = 0; k < 26; k++)
                {
                    float off = ((k + 1) / 2) * 14f * ((k & 1) == 0 ? 1f : -1f);
                    float a = (180f + off) * Mathf.Deg2Rad;
                    Vector2 p = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                    if ((p - spawn).magnitude < 2.6f) continue;

                    bool onTrail = false;
                    foreach (var td in trailDirs)
                        if (Vector2.Angle(p, td) < 16f) { onTrail = true; break; }
                    if (onTrail) continue;

                    if (Free(p, pass == 0)) return p;
                }
        return null;
    }

    // ====================================================================== 2) указатели на развилках
    int ForkSignposts()
    {
        int n = 0;
        var done = new HashSet<string>();
        foreach (var ch in pads)
        {
            var par = FindPad(ch.parentName);
            if (par == null || par.pos.sqrMagnitude < 0.01f) continue;
            string key = par.name + "@" + Mathf.RoundToInt(ch.fork);
            if (!done.Add(key)) continue;

            Vector2 dirP = par.pos.normalized;
            Vector2 fork = dirP * ch.fork;
            Vector2 perp = new Vector2(dirP.y, -dirP.x);

            var ts = new List<Target>();
            foreach (var c2 in pads)
                if (string.Equals(c2.parentName, par.name, StringComparison.OrdinalIgnoreCase) && Mathf.Abs(c2.fork - ch.fork) < 1f)
                    ts.Add(new Target { name = c2.zone.name, pos = c2.pos, col = c2.col });
            ts.Add(new Target { name = par.zone.name, pos = par.pos, col = par.col });
            ts.Add(new Target { name = ZoneNames.Center.name, pos = Vector2.zero, col = new Color(1f, 0.85f, 0.4f) });

            var cands = new List<Vector2>();
            foreach (float along in new[] { 0f, 2.5f, -2.5f })
                foreach (float off in new[] { 2.8f, 4.0f })
                    foreach (float side in new[] { 1f, -1f })
                        cands.Add(fork + dirP * along + perp * (side * off));

            Vector2? spot = null;
            for (int pass = 0; pass < 2 && spot == null; pass++)
                foreach (var c in cands)
                    if (Free(c, pass == 0)) { spot = c; break; }
            if (spot == null) { Debug.LogWarning("LocationSigns: нет места под указатель на развилке \"" + par.name + "\"."); continue; }

            MakeSign("Развилка_" + par.zone.name, spot.Value, MakeBoards(spot.Value, ts), false);
            n++;
        }
        return n;
    }

    // ====================================================================== 3) таблички зон
    void NameSign(PadInfo p)
    {
        Vector2 start = Vector2.zero;
        var par = FindPad(p.parentName);
        if (par != null && par.pos.sqrMagnitude > 0.01f) start = par.pos.normalized * p.fork;

        Vector2 to = p.pos - start;
        if (to.sqrMagnitude < 1f) return;
        Vector2 dir = to.normalized;
        Vector2 perp = new Vector2(dir.y, -dir.x);

        float side0 = Mathf.Min(p.rad * 0.55f + 1f, 3.4f);
        for (int pass = 0; pass < 2; pass++)
            foreach (float back in new[] { p.rad + 1.6f, p.rad + 3.2f, p.rad * 0.6f })
                foreach (float side in new[] { 1f, -1f })
                    foreach (float off in new[] { side0, 2.4f, 4.6f })
                    {
                        Vector2 pos = p.pos - dir * back + perp * (side * off);
                        if (!Free(pos, pass == 0)) continue;
                        MakeSign("Зона_" + p.zone.name, pos, SingleBoard(p.zone.name, p.col), true);
                        return;
                    }
        Debug.LogWarning("LocationSigns: нет места под табличку зоны \"" + p.zone.name + "\".");
    }

    void BridgeNameSign(Color col)
    {
        Vector2 toRiver = new Vector2(src.bridgeSide, 0f);
        for (int pass = 0; pass < 2; pass++)
            foreach (float back in new[] { 3.4f, 5f, 2.4f })
                foreach (float off in new[] { 2.5f, -2.5f, 4f, -4f })
                {
                    Vector2 pos = src.bridgePos - toRiver * back + new Vector2(0f, off);
                    if (!Free(pos, pass == 0)) continue;
                    MakeSign("Зона_" + ZoneNames.Bridge.name, pos, SingleBoard(ZoneNames.Bridge.name, col), true);
                    return;
                }
        Debug.LogWarning("LocationSigns: нет места под табличку моста.");
    }

    // ====================================================================== доски
    static List<Board> SingleBoard(string name, Color col)
    {
        return new List<Board> { new Board { text = name, col = col, arrow = false } };
    }

    // доски для указателя: по часовой стрелке от «вперёд» (вверх на экране)
    List<Board> MakeBoards(Vector2 from, List<Target> targets)
    {
        targets.Sort((a, b) => (a.pos - from).sqrMagnitude.CompareTo((b.pos - from).sqrMagnitude));
        if (targets.Count > MaxBoards) targets.RemoveRange(MaxBoards, targets.Count - MaxBoards);

        var res = new List<Board>();
        foreach (var t in targets)
        {
            Vector2 d = t.pos - from;
            float ang = ScreenAngle(d);
            res.Add(new Board
            {
                text = t.name,
                col = t.col,
                arrow = true,
                angle = ang,
                sortKey = Mathf.Repeat(90f - ang, 360f)
            });
        }
        res.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));
        return res;
    }

    // направление по земле -> угол стрелки на доске, которая смотрит в камеру.
    // Тот же расчёт, что у стрелки игрока на мини-карте: вверх на экране = от камеры.
    static float ScreenAngle(Vector2 dir)
    {
        float y = CamYaw * Mathf.Deg2Rad;
        Vector3 right = new Vector3(Mathf.Cos(y), 0f, -Mathf.Sin(y));
        Vector3 up = new Vector3(Mathf.Sin(y), 0f, Mathf.Cos(y));
        Vector3 d = new Vector3(dir.x, 0f, dir.y);
        return Mathf.Atan2(Vector3.Dot(d, up), Vector3.Dot(d, right)) * Mathf.Rad2Deg;
    }

    // ====================================================================== проверка места
    bool Free(Vector2 p, bool checkColliders)
    {
        if (!Dry(p)) return false;
        float h0 = H(p);

        // не на склоне
        const float e = 0.9f;
        if (Mathf.Abs(H(p + new Vector2(e, 0f)) - h0) > 0.5f || Mathf.Abs(H(p - new Vector2(e, 0f)) - h0) > 0.5f ||
            Mathf.Abs(H(p + new Vector2(0f, e)) - h0) > 0.5f || Mathf.Abs(H(p - new Vector2(0f, e)) - h0) > 0.5f) return false;

        // не внутри камней, деревьев, построек (террейн и свои таблички не считаются)
        if (checkColliders)
        {
            Vector3 w = node.TransformPoint(new Vector3(p.x, h0 + 1.3f, p.y));
            var hits = Physics.OverlapSphere(w, 0.9f * src.ws, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                if (c == null || c is TerrainCollider) continue;
                if (c.transform.IsChildOf(node)) continue;
                return false;
            }
        }
        return true;
    }

    // ====================================================================== сама табличка (отдельный объект)
    // boards: если одна доска без стрелки, это «табличка зоны» (крупнее); иначе указатель со стрелками.
    GameObject MakeSign(string name, Vector2 sp, List<Board> boards, bool big)
    {
        if (boards == null || boards.Count == 0) return null;

        float y = H(sp);
        var root = new GameObject("Sign_" + name);
        root.transform.SetParent(node, false);
        root.transform.localPosition = new Vector3(sp.x, y, sp.y);

        // размеры
        float em = big ? 0.40f : 0.30f;                  // высота букв (u)
        float bh = big ? 0.80f : 0.62f;                  // высота доски
        float gap = 0.10f;
        int n = boards.Count;
        float stack = n * bh + (n - 1) * gap;
        float postH = Mathf.Max(big ? 2.1f : 2.4f, stack + 1.15f);

        int maxLen = 0;
        bool anyArrow = false;
        foreach (var b in boards)
        {
            maxLen = Mathf.Max(maxLen, b.text.Length);
            anyArrow |= b.arrow;
        }
        float textW = 0.6f * em * maxLen;
        float arrowZone = anyArrow ? 0.62f : 0.25f;
        float W = Mathf.Clamp(0.1f + arrowZone + textW + 0.3f, big ? 1.8f : 1.5f, 5f);

        Material wood = Mat("SignWood", WoodC, 0.05f);
        Material woodL = Mat("SignWoodL", WoodLightC, 0.05f);
        Material plaque = Mat("SignPlaque", PlaqueC, 0.05f);
        Material frame = Mat("SignFrame", FrameC, 0.05f);
        Material stone = Mat("SignStone", StoneC, 0.05f);
        Material ink = Mat("SignInk", InkC, 0.05f);

        // основание, столб, шапка
        Part(root.transform, "Base", new Vector3(0f, 0.1f, 0f), Quaternion.Euler(0f, CamYaw, 0f), new Vector3(0.85f, 0.22f, 0.85f), stone);
        Part(root.transform, "Post", new Vector3(0f, postH * 0.5f, 0f), Quaternion.identity, new Vector3(0.22f, postH, 0.22f), wood);
        Part(root.transform, "Cap", new Vector3(0f, postH + 0.08f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.36f, 0.16f, 0.36f), woodL);

        // доски лицом к камере, чуть вперёд от столба
        Quaternion q = Quaternion.Euler(Tilt, CamYaw, 0f);
        Vector3 toCam = Quaternion.Euler(0f, CamYaw, 0f) * Vector3.back * 0.18f;

        for (int i = 0; i < n; i++)
        {
            var b = boards[i];
            float yc = postH - 0.25f - bh * 0.5f - i * (bh + gap);
            var bo = new GameObject("Board_" + b.text);
            bo.transform.SetParent(root.transform, false);
            bo.transform.localPosition = new Vector3(0f, yc, 0f) + toCam;
            bo.transform.localRotation = q;

            Material stripe = Mat("SignStripe_" + ColorUtility.ToHtmlStringRGB(b.col), b.col, 0.1f);

            // рамка сзади, доска, цветная полоска слева (цвет зоны, как на карте)
            Part(bo.transform, "Frame", new Vector3(0f, 0f, 0.04f), Quaternion.identity, new Vector3(W + 0.12f, bh + 0.12f, 0.05f), frame);
            Part(bo.transform, "Plaque", Vector3.zero, Quaternion.identity, new Vector3(W, bh, 0.07f), plaque);
            Part(bo.transform, "Stripe", new Vector3(-W * 0.5f + 0.05f, 0f, -0.045f), Quaternion.identity, new Vector3(0.1f, bh, 0.02f), stripe);

            float x0 = -W * 0.5f + 0.1f + (b.arrow ? 0.62f : 0.2f);
            float x1 = W * 0.5f - 0.15f;

            if (b.arrow) Arrow(bo.transform, new Vector3(-W * 0.5f + 0.1f + 0.31f, 0f, -0.05f), b.angle, ink);

            Text(bo.transform, b.text, new Vector3((x0 + x1) * 0.5f, 0f, -0.05f), x1 - x0, bh * 0.9f, em,
                 b.arrow ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
        }

        // коллайдер столба, чтобы игрок не проходил насквозь
        var bc = root.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, postH * 0.5f, 0f);
        bc.size = new Vector3(0.5f, postH, 0.5f);

        signCount++;
        return root;
    }

    // стрелка из трёх брусков (стебель и два крыла), смотрит вдоль +X, поворачивается на angle
    void Arrow(Transform board, Vector3 center, float angle, Material m)
    {
        var pivot = new GameObject("Arrow");
        pivot.transform.SetParent(board, false);
        pivot.transform.localPosition = center;
        pivot.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        Part(pivot.transform, "Stem", new Vector3(-0.03f, 0f, 0f), Quaternion.identity, new Vector3(0.30f, 0.075f, 0.03f), m);
        Part(pivot.transform, "WingA", new Vector3(0.1265f, 0.0528f, 0f), Quaternion.Euler(0f, 0f, -40f), new Vector3(0.165f, 0.075f, 0.03f), m);
        Part(pivot.transform, "WingB", new Vector3(0.1265f, -0.0528f, 0f), Quaternion.Euler(0f, 0f, 40f), new Vector3(0.165f, 0.075f, 0.03f), m);
    }

    void Text(Transform board, string text, Vector3 center, float w, float h, float em, TextAlignmentOptions al)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(board, false);
        go.transform.localPosition = center;

        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.rectTransform.sizeDelta = new Vector2(w, h);
        t.richText = false;
        t.fontStyle = FontStyles.Bold;
        t.alignment = al;
        t.color = InkC;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.fontSize = em * 10f;                 // у 3D-текста TMP 10 пунктов = 1 единица высоты
        t.enableAutoSizing = true;             // страховка: если не влезло по ширине, чуть уменьшит
        t.fontSizeMax = em * 10f;
        t.fontSizeMin = em * 10f * 0.55f;
        t.text = text;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        CheckGlyphs(text);
        t.ForceMeshUpdate();
    }

    GameObject Part(Transform parent, string name, Vector3 lp, Quaternion lr, Vector3 ls, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localRotation = lr;
        go.transform.localScale = ls;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    TMP_FontAsset FindFont()
    {
        var ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        if (ui != null && ui.objectiveText != null && ui.objectiveText.font != null) return ui.objectiveText.font;
        return TMP_Settings.defaultFontAsset;
    }

    void CheckGlyphs(string text)
    {
        if (font == null || warnedFont) return;
        foreach (char ch in text)
        {
            if (char.IsWhiteSpace(ch)) continue;
            if (!font.HasCharacter(ch, true))
            {
                warnedFont = true;
                Debug.LogWarning("LocationSigns: в шрифте \"" + font.name + "\" нет символа '" + ch +
                                 "'. Нужен шрифт с кириллицей (тот же, что у HUD).");
                return;
            }
        }
    }

    // ------------------------------------------------------------ зона карты
    // Используется ваш MapZone (его читают HudMinimap и ZoneToast): название, круг, радиус в метрах, цвет.
    void AddZone(ZoneNames.Def def, Vector2 pos, float radiusU, Color col)
    {
        if (existingZones.Contains(def.name))
        {
            Debug.Log("LocationSigns: зона \"" + def.name + "\" уже есть в сцене, пропускаю.");
            return;
        }
        existingZones.Add(def.name);

        var go = new GameObject("ZONE_" + def.name);
        go.transform.SetParent(zonesRoot, false);
        go.transform.localPosition = new Vector3(pos.x * src.ws, src.heightU(pos.x, pos.y) * src.ws, pos.y * src.ws);
        var z = go.AddComponent<MapZone>();
        z.zoneName = def.name;
        z.shape = MapZone.Shape.Circle;
        z.radius = radiusU * src.ws;
        z.color = new Color(col.r, col.g, col.b, 1f);
        z.showOnMap = true;
        z.announce = true;
    }

    // ------------------------------------------------------------ материалы
    Material Mat(string name, Color col, float smooth)
    {
        Material m;
        if (mats.TryGetValue(name, out m) && m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh) { name = name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        m = LocKit.SaveAsset(m, src.matDir + "/" + name + ".mat");
        mats[name] = m;
        return m;
    }
}
#endif