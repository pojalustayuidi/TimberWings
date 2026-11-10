#if UNITY_EDITOR
// Assets/_Project/Editor/GateBridge/GateBridgeCheck.cs (Editor)
// Tools > BuilderSim > Локация > Проверить мост и финальный остров (работает в Edit Mode, сцену не меняет: всё включённое временно возвращается).
// Проверяет: число и цены оплачиваемых этапов, соответствие этапов и видов, ссылки, швы и непрерывность настила, бортики,
// проходимость с обоих берегов по сетке (капсула Лео), опасные края дальнего острова, место для камеры, радиус записки, положение зоны стройки.
// Это проверка геометрии и ссылок, а не Play Mode: сохранения, показ стройки и финал надо смотреть в игре (список в PATCH_NOTES.md).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GateBridgeCheck
{
    static StringBuilder sb;
    static int fails, warns;

    static void Ok(string t) { sb.AppendLine("OK   " + t); }
    static void Fail(string t) { sb.AppendLine("FAIL " + t); fails++; }
    static void Warn(string t) { sb.AppendLine("WARN " + t); warns++; }

    [MenuItem("Tools/BuilderSim/Локация/Проверить мост и финальный остров", false, 104)]
    public static void Run()
    {
        sb = new StringBuilder("=== Проверка моста и финального острова ===\n");
        fails = 0; warns = 0;
        string err;
        var gb = GateBridgeBuilder.FindOrCreate(out err);
        if (gb == null || gb.walkway == null) { EditorUtility.DisplayDialog("GateBridge", "Мост не собран: " + (err ?? "сначала запусти сборку"), "OK"); return; }
        var fi = gb.GetComponent<GateBridgePlacement>();
        if (fi == null) { EditorUtility.DisplayDialog("GateBridge", "Нет GateBridgePlacement на объекте моста.", "OK"); return; }
        var p = GateBridgeBuilder.MakePlan(fi, gb, out err);
        if (p == null) { EditorUtility.DisplayDialog("GateBridge", err, "OK"); return; }

        CheckData(gb);
        CheckRefs(gb, fi);
        CheckStates(gb);

        // временное состояние: проход открыт, стена ворот снята
        bool walkWas = gb.walkway.activeSelf;
        var gate = fi.GateRoot;
        bool gateWas = gate != null && gate.gameObject.activeSelf;
        gb.walkway.SetActive(true);
        if (gate != null) gate.gameObject.SetActive(false);
        Physics.SyncTransforms();
        try
        {
            CheckDeck(gb, p);
            CheckWalk(gb, fi, p);
            CheckCameraSpace(gb, p);
            CheckNote(gb, p);
        }
        finally
        {
            gb.walkway.SetActive(walkWas);
            if (gate != null) gate.gameObject.SetActive(gateWas);
            Physics.SyncTransforms();
        }
        CheckZone(gb, p);

        sb.AppendLine("\nИтого: FAIL " + fails + ", WARN " + warns + ".");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("GateBridge", sb.ToString(), "OK");
    }

    // ------------------------------------------------------------ данные стройки
    static BuildingController FindBridgeSite()
    {
        foreach (var b in Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.SiteKind == BuildSiteKind.Bridge) return b;
        return null;
    }

    static void CheckData(GateBridge gb)
    {
        var b = FindBridgeSite();
        if (b == null) { Fail("в сцене нет зоны стройки моста (BuildingController вида Bridge)"); return; }
        int n = b.StepsTotal;
        if (n == 3) Ok("оплачиваемых этапов 3 (BuildingController «" + b.name + "», SaveId «" + b.SaveId + "»)");
        else Fail("оплачиваемых этапов " + n + ", ожидалось 3 (GameBalance.Bridge*)");
        if (b.data != null)
        {
            for (int i = 0; i < Mathf.Min(n, GameBalance.BridgeWood.Length); i++)
            {
                var s = b.data.steps[i];
                if (s.woodCost != GameBalance.BridgeWood[i] || s.plankCost != GameBalance.BridgePlanks[i] || s.coinCost != GameBalance.BridgeCoins[i])
                    Warn("этап " + (i + 1) + " «" + s.name + "»: цена в Build Site Data (" + s.woodCost + "/" + s.plankCost + "/" + s.coinCost + ") отличается от GameBalance ("
                         + GameBalance.BridgeWood[i] + "/" + GameBalance.BridgePlanks[i] + "/" + GameBalance.BridgeCoins[i] + "). Цены сборка не меняет");
            }
            Ok("этапы по порядку: " + string.Join(", ", System.Array.ConvertAll(b.data.steps, x => x.name)));
        }
        else Warn("у зоны моста нет Build Site Data: работают запасные поля");
        sb.AppendLine("     вид по этапам: 0 сдано = вид 1 (сломан); 1 сдано «Опоры» = вид 2, новые опоры и тросы; 2 сдано «Настил» = вид 2 + доски (DeckStep2); 3 сдано = вид 3 и проход");
    }

    static void CheckRefs(GateBridge gb, GateBridgePlacement fi)
    {
        bool stagesOk = gb.stages != null && gb.stages.Length == 3 && gb.stages[0] != null && gb.stages[1] != null && gb.stages[2] != null;
        if (stagesOk) Ok("три вида собраны"); else Fail("не хватает видов моста: пересобери");
        if (gb.deckStep2 != null && gb.walkway != null) Ok("deckStep2 и walkway на месте"); else Fail("нет deckStep2 или walkway");
        if (gb.source == null) Fail("GateBridge.source (RiverBridge) пуст: мост не узнает о стройке");
        else
        {
            Ok("RiverBridge найден");
            if (gb.source.stages != null)
                foreach (var g in gb.source.stages)
                    if (g != null && g.transform.parent == gb.transform) Warn("RiverBridge.stages включает старый объект «" + g.name + "» под GateBridge");
        }
        var b = FindBridgeSite();
        if (b != null)
        {
            var link = b.GetComponent<BuildStageLink>();
            if (link == null) Warn("на зоне моста нет BuildStageLink: StoryFlow добавит его при старте сцены (проверь в Play Mode)");
            else if (link.bridge == null) Warn("BuildStageLink.bridge пуст: StoryFlow проставит при старте");
            else if (link.bridge != gb.source) Warn("BuildStageLink.bridge не тот RiverBridge, что у GateBridge.source");
            else Ok("BuildStageLink связывает стройку и RiverBridge");
        }
        if (fi.GateRoot == null) Warn("у GateBridgePlacement нет GateWalls: проход не закрыт до стройки (запусти BuildWalls)"); else Ok("GateWalls есть");
        var sf = Object.FindFirstObjectByType<StoryFlow>(FindObjectsInactive.Include);
        if (sf == null) Warn("StoryFlow в сцене нет");
        else if (sf.noteSpot == gb.noteSpot && gb.noteSpot != null) Ok("StoryFlow.noteSpot = площадка дальнего острова");
        else Fail("StoryFlow.noteSpot не указывает на NoteSpot площадки");
        if (sf != null && !sf.useEnding) Warn("StoryFlow.useEnding выключен: вместо заставки пойдёт запасное чтение записки (оно тоже работает)");
    }

    static void CheckStates(GateBridge gb)
    {
        var was = new bool[3];
        for (int i = 0; i < 3; i++) was[i] = gb.stages[i] != null && gb.stages[i].activeSelf;
        bool stepWas = gb.deckStep2.activeSelf, walkWas = gb.walkway.activeSelf;
        int[,] cases = { { 1, 0 }, { 2, 1 }, { 2, 2 }, { 3, 3 } };
        bool allOk = true;
        for (int c = 0; c < 4; c++)
        {
            gb.Preview(cases[c, 0], cases[c, 1], false);
            bool ok = gb.stages[0].activeSelf == (cases[c, 0] == 1) && gb.stages[1].activeSelf == (cases[c, 0] == 2) && gb.stages[2].activeSelf == (cases[c, 0] == 3)
                      && gb.deckStep2.activeSelf == (cases[c, 0] == 2 && cases[c, 1] >= 2) && !gb.walkway.activeSelf;
            if (!ok) { allOk = false; Fail("состояние «сдано " + cases[c, 1] + "» показывает неверные объекты"); }
        }
        if (allOk) Ok("4 состояния стройки (0, 1, 2, 3 сдано) показывают разные объекты, проход закрыт при любом виде без завершённой стройки");
        for (int i = 0; i < 3; i++) gb.stages[i].SetActive(was[i]);
        gb.deckStep2.SetActive(stepWas);
        gb.walkway.SetActive(walkWas);
    }

    // ------------------------------------------------------------ настил
    static bool Hit(Vector3 from, float len, out RaycastHit h)
    {
        var hs = Physics.RaycastAll(from, Vector3.down, len, ~0, QueryTriggerInteraction.Ignore);
        h = default(RaycastHit);
        float best = float.MaxValue; bool found = false;
        foreach (var x in hs)
            if (x.distance < best) { best = x.distance; h = x; found = true; }
        return found;
    }

    static void CheckDeck(GateBridge gb, GBPlan p)
    {
        float maxStep = 0f, worstNy = 1f; int miss = 0; Vector3 prev = Vector3.zero; bool have = false;
        float[] xs = { 0f, p.hw - 0.4f, -(p.hw - 0.4f) };
        foreach (float x in xs)
        {
            have = false;
            for (float s = -0.5f; s <= p.Lb + 0.7f; s += 0.25f)
            {
                Vector3 f, r, u;
                p.Frame(Mathf.Clamp(s, 0f, p.Lb), out f, out r, out u);
                Vector3 pos = p.Pw(s) + r * x;
                RaycastHit h;
                if (!Hit(pos + Vector3.up * 2f, 4f, out h) || h.collider.gameObject != gb.walkway) { miss++; have = false; continue; }
                if (have) maxStep = Mathf.Max(maxStep, Mathf.Abs(h.point.y - prev.y));
                worstNy = Mathf.Min(worstNy, h.normal.y);
                prev = h.point; have = true;
            }
        }
        if (miss == 0) Ok("настил под лучами непрерывный от берега до берега (3 дорожки, шаг 0,25 м)"); else Fail("в настиле " + miss + " точек без коллайдера моста");
        if (maxStep <= 0.08f) Ok("перепад высоты между соседними точками настила не больше " + (maxStep * 100f).ToString("0.0") + " см"); else Fail("в настиле ступенька " + (maxStep * 100f).ToString("0.0") + " см");
        if (worstNy >= 0.9f) Ok("наклон настила: нормаль не ниже " + worstNy.ToString("0.00")); else Warn("наклон настила круче ожидаемого: нормаль " + worstNy.ToString("0.00"));

        // бортики: луч поперёк с обеих сторон на высоте 0,5 м над настилом
        int rail = 0, tot = 0;
        for (float s = 0.5f; s < p.Lb; s += 1f)
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 f, r, u;
                p.Frame(s, out f, out r, out u);
                Vector3 from = p.P(s) + r * (side * (p.hw + 1f)) + u * 0.5f;
                tot++;
                if (Physics.Raycast(from, -r * side, 1f, ~0, QueryTriggerInteraction.Ignore)) rail++;
            }
        if (rail == tot) Ok("бортики моста сплошные с обеих сторон (" + tot + " проб)"); else Fail("в бортиках дыры: " + (tot - rail) + " из " + tot + " проб");
    }

    // ------------------------------------------------------------ проходимость по сетке
    static void CheckWalk(GateBridge gb, GateBridgePlacement fi, GBPlan p)
    {
        const float cell = 0.4f;
        float sc = p.sF + p.R;                         // s центра дальнего острова
        float s0 = -3.5f, s1 = sc + p.R + 1.5f, b0 = -(p.R + 1.5f), b1 = p.R + 1.5f;
        int ns = Mathf.CeilToInt((s1 - s0) / cell) + 1, nb = Mathf.CeilToInt((b1 - b0) / cell) + 1;
        var h = new float[ns, nb];
        var ground = new bool[ns, nb];
        Vector3 Pos(int i, int j, float y) { Vector2 w = p.S2 + p.N * (s0 + i * cell) + p.Tn * (b0 + j * cell); return new Vector3(w.x, y, w.y); }

        for (int i = 0; i < ns; i++)
            for (int j = 0; j < nb; j++)
            {
                RaycastHit hit;
                // выше всего, что ходит: берём ближайший к высоте y0+1 попадание сверху
                var hs = Physics.RaycastAll(Pos(i, j, p.y0 + 4f), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MinValue; bool f = false; hit = default(RaycastHit);
                foreach (var x in hs)
                {
                    if (x.point.y > p.y0 + 2.5f) continue;     // кроны, арка сверху
                    if (x.normal.y < 0.7f) continue;
                    if (x.point.y > best) { best = x.point.y; hit = x; f = true; }
                }
                ground[i, j] = f; h[i, j] = f ? best : 0f;
            }

        bool Free(int i, int j)
        {
            Vector3 g = Pos(i, j, h[i, j]);
            return !Physics.CheckCapsule(g + Vector3.up * 0.4f, g + Vector3.up * 1.3f, 0.33f, ~0, QueryTriggerInteraction.Ignore);
        }

        int si = Mathf.RoundToInt((-2.5f - s0) / cell), sj = Mathf.RoundToInt(-b0 / cell);
        int ai = Mathf.RoundToInt((-p.R + 2.4f + sc - s0) / cell);
        if (!ground[si, sj]) { Warn("точка старта на главном острове (2,5 м до моста) без земли: проверка прохода пропущена"); return; }
        var seen = new bool[ns, nb];
        var q = new Queue<int>();
        q.Enqueue(si * nb + sj); seen[si, sj] = true;
        int reached = 0;
        while (q.Count > 0)
        {
            int v = q.Dequeue(); int i = v / nb, j = v % nb; reached++;
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    int ni = i + di, nj = j + dj;
                    if ((di == 0 && dj == 0) || ni < 0 || nj < 0 || ni >= ns || nj >= nb || seen[ni, nj] || !ground[ni, nj]) continue;
                    if (Mathf.Abs(h[ni, nj] - h[i, j]) > 0.3f || !Free(ni, nj)) continue;
                    seen[ni, nj] = true; q.Enqueue(ni * nb + nj);
                }
        }
        if (seen[ai, Mathf.RoundToInt(-b0 / cell)]) Ok("с главного берега по мосту можно дойти до места прибытия на дальнем острове (капсула Лео r 0,33, шаг сетки 0,4 м, обратный путь по тем же клеткам)");
        else Fail("с главного берега до места прибытия дойти нельзя: проверь бортики, ворота, деревья у въезда");

        // опасные края: достижимая клетка, рядом с которой (0,45 м) нет земли, за началом провала
        int exposed = 0; Vector3 firstBad = Vector3.zero;
        for (int i = 0; i < ns; i++)
            for (int j = 0; j < nb; j++)
            {
                if (!seen[i, j] || s0 + i * cell < p.sE + 0.4f) continue;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    int ni = i + Mathf.RoundToInt(Mathf.Cos(a) * 1.15f), nj = j + Mathf.RoundToInt(Mathf.Sin(a) * 1.15f);
                    if (ni < 0 || nj < 0 || ni >= ns || nj >= nb || !ground[ni, nj]) { if (exposed++ == 0) firstBad = Pos(i, j, h[i, j]); break; }
                }
            }
        if (exposed == 0) Ok("достижимые клетки моста и дальнего острова нигде не подходят к пустоте ближе 0,45 м: опасных краёв нет");
        else Fail("опасные края: " + exposed + " достижимых клеток рядом с пустотой, первая около " + firstBad.ToString("0.0") + " (дыра в бортиках или заграждении кромки)");
        sb.AppendLine("     достижимых клеток: " + reached);
    }

    // ------------------------------------------------------------ камера и записка
    static float FreeDist(Vector3 origin, float maxLen)
    {
        float min = maxLen;
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI / 4f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            RaycastHit hit;
            if (Physics.SphereCast(origin, 0.3f, dir, out hit, maxLen, ~0, QueryTriggerInteraction.Ignore) && hit.distance > 0.01f) min = Mathf.Min(min, hit.distance);
        }
        return min;
    }

    static void CheckCameraSpace(GateBridge gb, GBPlan p)
    {
        Vector3 arrive = gb.arrivalSpot != null ? gb.arrivalSpot.position : p.Pw(p.Lb + 1f);
        float a = FreeDist(arrive + Vector3.up * 1.3f, 8f);
        if (a >= 3.5f) Ok("у места прибытия свободно " + a.ToString("0.0") + " м во все стороны (камера и разворот Лео)"); else Warn("у места прибытия тесно: " + a.ToString("0.0") + " м до препятствия");
        if (gb.noteSpot != null)
        {
            Vector3 plat = gb.noteSpot.position - gb.noteSpot.parent.forward * 0.9f;
            float b = FreeDist(plat + Vector3.up * 0.3f, 8f);
            if (b >= 2.5f) Ok("на площадке свободно " + b.ToString("0.0") + " м (до пюпитра, арки или колонн)"); else Warn("на площадке тесно: " + b.ToString("0.0") + " м");
            Vector3 eye = plat + Vector3.up * 1.3f, tgt = new Vector3(p.E.x, eye.y, p.E.y);
            RaycastHit hit;
            if (Physics.Linecast(eye, tgt, out hit, ~0, QueryTriggerInteraction.Ignore)) Warn("вид с площадки назад на главный остров перекрыт объектом «" + hit.collider.name + "» на расстоянии " + hit.distance.ToString("0.0") + " м");
            else Ok("вид с площадки назад на главный остров свободен");
        }
        Vector3 gate = p.Pw(-1.8f);
        float c = FreeDist(gate + Vector3.up * 1.3f, 6f);
        if (c >= 2.5f) Ok("перед входом на мост со стороны главного острова свободно " + c.ToString("0.0") + " м"); else Warn("перед входом на мост тесно: " + c.ToString("0.0") + " м (стена ворот снята на время проверки, остальные заграждения нет)");
    }

    static void CheckNote(GateBridge gb, GBPlan p)
    {
        var sf = Object.FindFirstObjectByType<StoryFlow>(FindObjectsInactive.Include);
        if (gb.noteSpot == null || sf == null) return;
        float r = sf.noteRadius;
        Vector3 n = gb.noteSpot.position;
        float dGate = Vector2.Distance(new Vector2(n.x, n.z), p.E);
        float dBridgeEnd = Vector2.Distance(new Vector2(n.x, n.z), p.Q2);
        float dNear = Vector2.Distance(new Vector2(n.x, n.z), p.S2);
        if (dBridgeEnd > r + 2f && dNear > r + 2f) Ok("радиус записки " + r.ToString("0.#") + " м: до ворот " + dGate.ToString("0") + " м, до конца моста " + dBridgeEnd.ToString("0.0") + " м, финал не сработает ни с главного берега, ни на мосту");
        else Fail("радиус записки " + r.ToString("0.#") + " м достаёт до моста или берега (до конца моста " + dBridgeEnd.ToString("0.0") + " м)");
        if (gb.arrivalSpot != null)
        {
            float da = Vector2.Distance(new Vector2(n.x, n.z), new Vector2(gb.arrivalSpot.position.x, gb.arrivalSpot.position.z));
            if (da > r + 2f) Ok("с места прибытия до записки " + da.ToString("0.0") + " м: финал не запускается сразу при сходе с моста"); else Warn("место прибытия слишком близко к записке: " + da.ToString("0.0") + " м");
        }
    }

    static void CheckZone(GateBridge gb, GBPlan p)
    {
        var b = FindBridgeSite();
        if (b == null) return;
        Vector3 z = b.transform.position;
        Vector2 z2 = new Vector2(z.x, z.z);
        float along = Vector2.Dot(z2 - p.S2, p.N);
        float side = Mathf.Abs(Vector2.Dot(z2 - p.S2, p.Tn));
        if (along > 0.5f) Warn("зона стройки моста стоит на настиле или за краем (на " + along.ToString("0.0") + " м вдоль моста): игрок встанет на провале. Подвинь её на главный остров, не переименовывая объект (от пути объекта зависит SaveId)");
        else if (-along > 14f || side > 10f) Warn("зона стройки моста далеко от ворот (" + (-along).ToString("0") + " м назад, " + side.ToString("0") + " м вбок)");
        else Ok("зона стройки моста у ворот на главном острове (" + (-along).ToString("0.0") + " м до начала настила)");
    }
}
#endif
