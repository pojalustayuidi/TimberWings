#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestGuardLite.cs (Editor)  НОВЫЙ ФАЙЛ
// «Лёгкие хранители»: дешёвые нерубимые деревья-кулисы. Заменяют старые фоновые Decor_* (кедры и дубы стеной): теперь нерубимое это не стена,
// а редкие крупные акценты-ориентиры в стиле хранителей (пастель, облака, магия), но без частиц, света и тяжёлых мешей.
// Меню: Tools/BuilderSim/Лес/Красота леса/13. Лёгкие хранители: кулисы (242), 14. Убрать лёгких хранителей (243).
// Безопасно жать повторно: корень GuardianLite в IslandV2/_Manual пересоздаётся целиком, префабы и меши перезаписываются. «0. Всё сразу» запускает пункт сама
// (сразу после «9. Деревья-хранители», до подлеска).
//
// 4 вида (формы те же, что у хранителей, меши SacredMeshes.Body с lod=1: шары по 20 граней, стволы по 5 граней; материалы те же Sacred_*):
//   Облачное, Цветущее, Кристальное, Фонарное (сонный страж не берём: у него лицо, оно читается только вблизи и стоит дороже).
// 2 размера (один меш, разный масштаб и знак):
//   Крупное (префаб Lite_<Вид>, масштаб 0.95-1.05)  тот же ЕДИНЫЙ ЗНАК «не рубится», что у хранителей: каменное кольцо с бирюзовыми рунами у корней
//                                                    и парящий бирюзовый кристалл над кроной. Вокруг чистая полянка 4.2 м, иначе кольцо не читается.
//   Мелкое  (префаб LiteS_<Вид>, масштаб 0.55-0.75) без кольца: отличается пастельной кроной (у рубимых только зелень, берёза, осень),
//                                                    формой кроны и парящим кристаллом поменьше. Полянка 2.6 м.
//   Рубимые деревья не имеют ни кольца, ни кристалла никогда.
// Что внутри префаба: меш дерева + (крупные) меш кольца + меш кристалла, LODGroup (LOD0 дерево, LOD1 только кристалл как далёкий маяк, дальше скрыто),
//   CapsuleCollider только на ствол, LiteGuardian (покачивание, один драйвер на сцену). Нет Light, FireSource, частиц, тега Resource и ResourceItem.
//   Тени отбрасывают только крупные.
//
// Расстановка (детерминировано по Seed, акценты, а не стена; лимиты LargeMax и SmallMax):
//   Крупные, по ролям, по очереди:
//     «ворота»   по одному с каждой стороны тропы, ведущей к зоне рубки, за ChopZone.ClearR + 8 м от PAD_Forest: перед полянкой рубки;
//     «поворот»  у крутых поворотов троп, с внешней стороны поворота (видны с тропы издалека);
//     «вода»     у воды (4.5-8 м от берега), цветущие;
//     «обрыв»    по краю острова (5-11 м до обрыва), облачные и кристальные, кулисами на фоне неба. Между крупными не ближе SpaceLarge.
//   Мелкие: переход к крупным (по 2 вокруг каждого на 7.5-11 м: «крупные по краям, мелкие внутри»), 5-6 «за забором» зоны рубки на 6.5-9 м от забора,
//     остальные внутри чащи по весу леса.
//   Не ставит: на тропе, воде, в зданиях, у метки лагеря и Дедушки (те же запасы, что у хранителей), внутри ChopZone (забор + 5.5 м), ближе 10 м (крупные)
//   или 7 м (мелкие) к большим хранителям. Рубимые деревья в радиусе полянки убираются (как у хранителей); ZoneTree_* не трогаем.
//   Каждое дерево лицом к ближайшей тропе. Не нашлось место для роли = строка «[!]» в отчёте.
// Подлесок и зона рубки обходят полянки лёгких хранителей сами (ChopZone.Avoid читает LiteGuardian.clearRadius).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class ForestGuardLite
    {
        public const string RootName = "GuardianLite";
        const string Dir = "Assets/_Project/Prefabs/Sacred/Lite";
        const string SlipPath = ForestMats.MatDir + "/TreeTrunk_Slip.asset";
        const int Seed = 6619;

        // ---- числа расстановки (м)
        const int LargeMax = 10, SmallMax = 24;
        const float SpaceLarge = 18f;            // между крупными: акценты, а не ряд
        const float SpaceAny = 6.5f;             // крупное и любое другое (кольцо 3.45 + крона соседа)
        const float SpaceSmall = 6f;             // мелкие между собой
        const float SacredGapLarge = 10f, SacredGapSmall = 7f;   // до больших хранителей
        const float LargeClear = 4.2f;           // полянка крупного: кольцо до 3.45 м, на масштабе 1.05 это 3.6 + запас
        const float SmallClear = 2.6f;
        const float FenceMargin = 5.5f;          // от забора ChopZone наружу: ближе не ставим (там озеленение)
        const float TurnMinDeg = 38f;            // поворот тропы считается поворотом
        const float TurnWindow = 6f;             // плечо измерения поворота по тропе
        const float TurnSpacing = 14f;           // между найденными поворотами
        const float SideOffset = 5.5f;           // на сколько отступаем от оси тропы вбок
        const float LodNear = 0.10f, LodFar = 0.02f;   // доли высоты экрана: LOD0 дерево, LOD1 только кристалл (камера игры FOV 35, ~10 м)

        static readonly SacredKind[] Kinds = { SacredKind.Cloud, SacredKind.Blossom, SacredKind.Crystal, SacredKind.Lantern };

        static string Id(SacredKind k)
        {
            switch (k) { case SacredKind.Cloud: return "Cloud"; case SacredKind.Blossom: return "Blossom"; case SacredKind.Crystal: return "Crystal"; default: return "Lantern"; }
        }

        // ============================================================ меню
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/13. Лёгкие хранители: кулисы", false, 242)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Лёгкие хранители: останови Play Mode."); return; }
            var rootGo = GameObject.Find("IslandV2");
            if (rootGo == null) { Debug.LogError("Лёгкие хранители: в сцене нет IslandV2 (пункт «2. Собрать блокаут»)."); return; }
            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (L == null) { Debug.LogError("Лёгкие хранители: нет раскладки " + IslandLayout.AssetPath); return; }
            var f = new IslandField(L);
            var sb = new StringBuilder("Лёгкие хранители:\n");

            // ---- материалы (те же, что у больших хранителей: один набор на всех, меньше SetPass)
            Material stone, rune, gemMat;
            ForestSacred.MarkMaterials(out stone, out rune, out gemMat);
            if (stone == null || rune == null || gemMat == null) { Debug.LogError("Лёгкие хранители: не создались материалы (проверь шейдер TreeToon: Tools/BuilderSim/Лес/Tree Toon)."); return; }

            // ---- меши и префабы
            EditorAssets.EnsureFolder(Dir);
            var markMesh = ForestMats.SaveMesh(SacredMeshes.Mark("Lite_Mark"), Dir + "/Lite_Mark.asset");
            var gemMesh = ForestMats.SaveMesh(SacredMeshes.Gem("Lite_Gem"), Dir + "/Lite_Gem.asset");
            var big = new Dictionary<SacredKind, GameObject>();
            var small = new Dictionary<SacredKind, GameObject>();
            int trisBody = 0;
            foreach (var k in Kinds)
            {
                var body = ForestMats.SaveMesh(SacredMeshes.Body(k, 1, "Lite_" + Id(k)), Dir + "/Lite_" + Id(k) + ".asset");
                var mats = ForestSacred.Mats(k);
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == null) { Debug.LogError("Лёгкие хранители: не создался материал #" + i + " вида " + Id(k) + " (проверь шейдер TreeToon)."); return; }
                big[k] = BuildPrefab(k, false, body, markMesh, gemMesh, mats, stone, rune, gemMat);
                small[k] = BuildPrefab(k, true, body, markMesh, gemMesh, mats, stone, rune, gemMat);
                trisBody += body.triangles.Length / 3;
            }
            AssetDatabase.SaveAssets();
            sb.AppendLine("  префабы: " + Dir + "/Lite_*.prefab (4 крупных) и LiteS_*.prefab (4 мелких); среднее число треугольников тела " + (trisBody / Kinds.Length) + ", кольцо " + (markMesh.triangles.Length / 3) + ", кристалл " + (gemMesh.triangles.Length / 3));

            // ---- корень
            var parentGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = parentGo != null ? parentGo.transform : rootGo.transform;
            var old = parent.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = NewRoot(parent, RootName);

            // ---- что уже стоит и что надо учесть
            var ctx = new Ctx { f = f, L = L, rng = new System.Random(Seed), root = root };
            ctx.sacred = ReadSacred(parent);
            Vector2 pad;
            ctx.hasPad = AnchorXY(f, L, ChopZone.PadName, out pad);
            ctx.pad = pad;
            foreach (var ri in Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!ri.name.StartsWith(ChopZone.TreePrefix)) ctx.trees.Add(ri.transform);
            ctx.big = big; ctx.small = small;

            PlaceLarge(ctx, sb);
            PlaceSmall(ctx, sb);

            sb.AppendLine("  поставлено: крупных " + ctx.nLarge + " (с кольцом и кристаллом), мелких " + ctx.nSmall + " (с кристаллом); убрано рубимых деревьев на полянках: " + ctx.removed);
            foreach (var kv in ctx.byKind) sb.Append("    " + kv.Key + ": " + kv.Value + "\n");
            foreach (var m in ctx.misses) sb.AppendLine("  [!] не нашлось места: " + m + " (правила роли строже, чем позволяет остров; можно поставить руками из префабов)");
            sb.AppendLine("  Корень: IslandV2/_Manual/" + RootName + ". Двигай, удаляй и дублируй руками: это обычные префабы.");
            sb.AppendLine("  Дальше: «2. Подлесок» (обойдёт полянки), «3. Проверить блокаут», «16. Проверка леса» (нет Decor_*, нет нерубимых в зоне рубки), «15. Замер леса».");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/14. Убрать лёгких хранителей", false, 243)]
        static void Clear()
        {
            var parentGo = GameObject.Find("IslandV2/_Manual");
            if (parentGo == null) { Debug.LogWarning("Лёгкие хранители: нет IslandV2/_Manual."); return; }
            var t = parentGo.transform.Find(RootName);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            Debug.Log("Лёгкие хранители: корень " + RootName + " убран (убранные ими рубимые деревья вернёт «4. Лес: расставить рабочие деревья»).");
        }

        // ============================================================ контекст расстановки
        sealed class Ctx
        {
            public IslandField f; public IslandLayout L; public System.Random rng; public Transform root;
            public List<Vector2> sacred = new List<Vector2>();
            public List<KeyValuePair<Vector2, bool>> placed = new List<KeyValuePair<Vector2, bool>>();   // позиция, крупное?
            public List<Transform> trees = new List<Transform>();
            public Dictionary<SacredKind, GameObject> big, small;
            public Dictionary<string, int> byKind = new Dictionary<string, int>();
            public List<string> misses = new List<string>();
            public bool hasPad; public Vector2 pad;
            public int nLarge, nSmall, removed;
        }

        // ============================================================ префаб
        static GameObject BuildPrefab(SacredKind kind, bool isSmall, Mesh body, Mesh markMesh, Mesh gemMesh, Material[] mats, Material stone, Material rune, Material gemMat)
        {
            string name = (isSmall ? "LiteS_" : "Lite_") + Id(kind);
            var root = new GameObject(name);
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var rBody = AddMesh(visual.transform, "Body", body, mats, !isSmall);   // тени только у крупных
                MeshRenderer rMark = null;
                if (!isSmall) rMark = AddMesh(root.transform, "Mark", markMesh, new[] { stone, rune }, false);   // кольцо с рунами: знак «не рубится»
                var rGem = AddMesh(root.transform, "Gem", gemMesh, new[] { gemMat }, false);
                float gemY = SacredMeshes.GemY(kind);
                rGem.transform.localPosition = new Vector3(0f, gemY, 0f);
                rGem.transform.localScale = Vector3.one * (isSmall ? 0.8f : 1f);

                var lg = root.AddComponent<LODGroup>();
                lg.fadeMode = LODFadeMode.None;
                var lod0 = new List<Renderer> { rBody, rGem };
                if (rMark != null) lod0.Add(rMark);
                lg.SetLODs(new[] { new LOD(LodNear, lod0.ToArray()), new LOD(LodFar, new Renderer[] { rGem }) });
                lg.RecalculateBounds();

                // сплошной ствол: капсула, как у хранителей
                var col = root.AddComponent<CapsuleCollider>();
                float tr = SacredMeshes.TrunkR(kind), th = SacredMeshes.TrunkH(kind);
                col.direction = 1; col.radius = tr; col.height = Mathf.Max(th, tr * 2f + 0.01f); col.center = new Vector3(0f, th * 0.5f - 0.1f, 0f);
#if UNITY_6000_0_OR_NEWER
                var slip = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(SlipPath);
#else
                var slip = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(SlipPath);
#endif
                if (slip != null) col.sharedMaterial = slip;

                var lite = root.AddComponent<LiteGuardian>();
                lite.tier = isSmall ? LiteGuardian.Tier.Small : LiteGuardian.Tier.Large;
                lite.kind = kind;
                lite.visual = visual.transform;
                lite.gem = rGem.transform;
                lite.gemHeight = gemY;
                lite.swayDeg = isSmall ? 0.9f : 0.6f;
                lite.clearRadius = isSmall ? SmallClear : LargeClear;

                return PrefabUtility.SaveAsPrefabAsset(root, Dir + "/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static MeshRenderer AddMesh(Transform parent, string name, Mesh mesh, Material[] mats, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            int subs = Mathf.Max(1, mesh.subMeshCount);
            var arr = new Material[subs];
            for (int i = 0; i < subs; i++) arr[i] = mats[Mathf.Min(i, mats.Length - 1)];
            mr.sharedMaterials = arr;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return mr;
        }

        // ============================================================ крупные
        sealed class Turn { public Vector2 p, outward; public float deg; }

        static void PlaceLarge(Ctx c, StringBuilder sb)
        {
            var f = c.f; var L = c.L; var rng = c.rng;
            int want;

            // ---- 1. «ворота»: по одному с каждой стороны тропы к зоне рубки
            if (c.hasPad)
            {
                bool any = false;
                for (int i = 0; i < L.paths.Count; i++)
                {
                    var pl = f.PathPolyline(i);
                    if (pl == null || pl.Length < 4) continue;
                    float minD = float.MaxValue;
                    for (int j = 0; j < pl.Length; j++) minD = Mathf.Min(minD, Vector2.Distance(pl[j], c.pad));
                    if (minD > ChopZone.BaseR + 3f) continue;                 // тропа не ведёт к зоне рубки
                    int best = -1; float bd = float.MaxValue;
                    float target = ChopZone.ClearR + 8f;
                    for (int j = 0; j < pl.Length; j++)
                    {
                        float d = Mathf.Abs(Vector2.Distance(pl[j], c.pad) - target);
                        if (d < bd) { bd = d; best = j; }
                    }
                    if (best < 1 || best >= pl.Length - 1 || bd > 4f) continue;
                    Vector2 tan = (pl[best + 1] - pl[best - 1]).normalized;
                    Vector2 side = new Vector2(-tan.y, tan.x);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        bool ok = false;
                        for (float off = SideOffset; off <= SideOffset + 3.5f && !ok; off += 1f)
                            ok = TryPlace(c, pl[best] + side * (sgn * off), false, sgn < 0 ? SacredKind.Lantern : SacredKind.Blossom, "ворота");
                        any |= ok;
                    }
                }
                if (!any) c.misses.Add("крупные «ворота» перед зоной рубки");
            }

            // ---- 2. повороты троп (с внешней стороны)
            var turns = FindTurns(f, L);
            int nTurn = 0;
            for (int i = 0; i < turns.Count && nTurn < 4 && c.nLarge < LargeMax; i++)
            {
                var t = turns[i];
                bool ok = false;
                for (float off = SideOffset; off <= SideOffset + 4f && !ok; off += 1f)
                    ok = TryPlace(c, t.p + t.outward * off, false, (nTurn % 2 == 0) ? SacredKind.Lantern : SacredKind.Blossom, "поворот");
                if (ok) nTurn++;
            }
            if (nTurn == 0) c.misses.Add("крупные у поворотов троп (поворотов > " + TurnMinDeg + "° найдено " + turns.Count + ")");

            // ---- 3. у воды
            var water = Grid(f, L, (x, z, e, pd, wd, fw) => wd >= 4.5f && wd <= 8f && e >= 5f && pd >= 4.6f, c, true);
            int nWater = 0;
            for (int i = 0; i < water.Count && nWater < 2 && c.nLarge < LargeMax; i++)
                if (TryPlace(c, water[i], false, SacredKind.Blossom, "вода")) nWater++;
            if (nWater == 0) c.misses.Add("крупные у воды");

            // ---- 4. по краю острова, кулисами на фоне неба
            var edge = Grid(f, L, (x, z, e, pd, wd, fw) => e >= 5f && e <= 11f && pd >= 4.6f && pd <= 40f, c, true);
            int nEdge = 0;
            for (int i = 0; i < edge.Count && c.nLarge < LargeMax; i++)
                if (TryPlace(c, edge[i], false, (nEdge % 3 == 2) ? SacredKind.Crystal : SacredKind.Cloud, "обрыв")) nEdge++;
            want = LargeMax - c.nLarge;
            if (nEdge == 0) c.misses.Add("крупные по краю острова");
            sb.AppendLine("  крупные по ролям: ворота/повороты " + nTurn + "+, вода " + nWater + ", край " + nEdge + (want > 0 ? " (до лимита " + LargeMax + " ещё " + want + ": места по правилам кончились)" : ""));
        }

        // ============================================================ мелкие
        static void PlaceSmall(Ctx c, StringBuilder sb)
        {
            var f = c.f; var rng = c.rng;
            int nTrans = 0, nFence = 0, nInner = 0;

            // ---- переход к крупным: по 2 мелких вокруг каждого крупного
            var larges = new List<Vector2>();
            for (int i = 0; i < c.placed.Count; i++) if (c.placed[i].Value) larges.Add(c.placed[i].Key);
            for (int i = 0; i < larges.Count && c.nSmall < SmallMax; i++)
            {
                int got = 0;
                for (int t = 0; t < 14 && got < 2; t++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = Mathf.Lerp(7.5f, 11f, (float)rng.NextDouble());
                    var p = larges[i] + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    if (TryPlace(c, p, true, Kinds[rng.Next(Kinds.Length)], "переход")) { got++; nTrans++; }
                }
            }

            // ---- за забором зоны рубки: редкие акценты на 6.5-9 м снаружи от забора
            if (c.hasPad)
            {
                for (int k = 0; k < 7 && c.nSmall < SmallMax; k++)
                {
                    bool ok = false;
                    for (int t = 0; t < 12 && !ok; t++)
                    {
                        float a = (k / 7f + ((float)rng.NextDouble() - 0.5f) * 0.06f) * Mathf.PI * 2f;
                        float rr = ChopZone.Rb(a) + Mathf.Lerp(6.5f, 9f, (float)rng.NextDouble());
                        var p = c.pad + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                        ok = TryPlace(c, p, true, (k % 2 == 0) ? SacredKind.Blossom : SacredKind.Lantern, "забор");
                    }
                    if (ok) nFence++;
                }
            }

            // ---- внутри чащи: по весу леса, редко
            var inner = Grid(f, c.L, (x, z, e, pd, wd, fw) => fw >= 0.5f && e >= 8f && pd >= 4f && pd <= 20f, c, false);
            for (int i = 0; i < inner.Count && c.nSmall < SmallMax; i++)
                if (TryPlace(c, inner[i], true, Kinds[rng.Next(Kinds.Length)], "чаща")) nInner++;

            sb.AppendLine("  мелкие по ролям: переход к крупным " + nTrans + ", за забором " + nFence + ", в чаще " + nInner + (c.nSmall >= SmallMax ? " (упёрлось в лимит " + SmallMax + ")" : ""));
        }

        // ============================================================ поиск мест
        delegate bool CellRule(float x, float z, float edge, float path, float water, float forest);

        // клетки сетки 2.5 м, подходящие по правилу и по базовой пригодности, в случайном порядке (детерминированно по seed)
        static List<Vector2> Grid(IslandField f, IslandLayout L, CellRule rule, Ctx c, bool large)
        {
            var l = new List<Vector2>();
            for (float gx = f.Min.x; gx <= f.Max.x; gx += 2.5f)
                for (float gz = f.Min.y; gz <= f.Max.y; gz += 2.5f)
                {
                    if (!f.Inside(gx, gz)) continue;
                    float e = f.EdgeDistance(gx, gz), pd = f.PathDistance(gx, gz), wd = Mathf.Min(f.WaterDistance(gx, gz), 99f), fw = f.ForestWeight(gx, gz);
                    if (!rule(gx, gz, e, pd, wd, fw)) continue;
                    l.Add(new Vector2(gx, gz));
                }
            for (int i = l.Count - 1; i > 0; i--) { int j = c.rng.Next(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
            return l;
        }

        // пригодность места: общие запреты (вода, тропа, склон, здания, метки) и запас под кольцо у крупных
        static bool Valid(Ctx c, float x, float z, bool isSmall)
        {
            var f = c.f; var L = c.L;
            if (!f.Inside(x, z)) return false;
            float edgeMin = isSmall ? 3f : 5f, waterMin = isSmall ? 2.5f : 3.5f, pathMin = isSmall ? 3f : 4.6f, slopeMax = isSmall ? 16f : 14f, bldMin = isSmall ? 5f : 7f;
            if (f.EdgeDistance(x, z) < edgeMin) return false;
            if (f.WaterDistance(x, z) < waterMin || f.IsFord(x, z)) return false;
            if (f.PathDistance(x, z) < pathMin) return false;
            if (f.Slope(x, z) > slopeMax) return false;
            var p = new Vector2(x, z);
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.kind == IslandLayout.AnchorKind.Building) { if (f.RectDistance(a, x, z) < bldMin) return false; }
                else
                {
                    float clear = a.name == "PAD_Grandpa" ? (isSmall ? 12f : 14f) : (a.name == "PAD_Forest" || a.name == "PAD_GroveSouth") ? 0f : a.name == "SPAWN_Player" ? 6f : (isSmall ? 3.5f : 5f);
                    if (clear > 0f && Vector2.Distance(f.AnchorWorld(a), p) < clear) return false;
                }
            }
            for (int i = 0; i < L.meadows.Count; i++)
                if (Vector2.Distance(L.meadows[i].pos, p) < L.meadows[i].radius * 0.8f) return false;
            if (c.hasPad)
            {
                // зона рубки: забор и озеленение снаружи (внутри нерубимых деревьев быть не должно). Зона рубки теперь вокруг PAD_GroveSouth (ChopZone.PadName)
                Vector2 d = p - c.pad;
                if (d.magnitude < ChopZone.Rb(Mathf.Atan2(d.y, d.x)) + FenceMargin) return false;
            }
            var landmark = L.FindAnchor("PAD_Forest");   // маленькая достопримечательность: свои 5-6 деревьев, чужих нерубимых рядом не надо
            if (landmark != null && Vector2.Distance(f.AnchorWorld(landmark), p) < (isSmall ? 6f : 8f)) return false;
            return true;
        }

        static bool TryPlace(Ctx c, Vector2 p, bool isSmall, SacredKind kind, string role)
        {
            if (isSmall ? c.nSmall >= SmallMax : c.nLarge >= LargeMax) return false;
            if (!Valid(c, p.x, p.y, isSmall)) return false;
            for (int i = 0; i < c.sacred.Count; i++)
                if (Vector2.Distance(c.sacred[i], p) < (isSmall ? SacredGapSmall : SacredGapLarge)) return false;
            for (int i = 0; i < c.placed.Count; i++)
            {
                float d = Vector2.Distance(c.placed[i].Key, p);
                bool otherLarge = c.placed[i].Value;
                if (!isSmall && otherLarge && d < SpaceLarge) return false;
                if ((!isSmall || otherLarge) && d < SpaceAny) return false;
                if (isSmall && !otherLarge && d < SpaceSmall) return false;
            }

            float clearR = isSmall ? SmallClear : LargeClear;
            c.removed += ClearTrees(c.trees, p, clearR);
            c.removed += ForestWings.ClearAround(p, clearR - 0.6f);   // кулисы с венком в полянке лёгкого хранителя тоже уходят

            var prefab = isSmall ? c.small[kind] : c.big[kind];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, c.root);
            Undo.RegisterCreatedObjectUndo(go, "Лёгкий хранитель");
            int n = isSmall ? c.nSmall : c.nLarge;
            go.name = (isSmall ? "LiteS_" : "Lite_") + Id(kind) + "_" + n;
            go.transform.position = new Vector3(p.x, c.f.Height(p.x, p.y), p.y);
            Vector2 target = ForestSacred.NearestPathPoint(c.f, c.L, p);
            Vector2 dir = target - p;
            float yaw = dir.sqrMagnitude > 0.01f ? Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg : (float)c.rng.NextDouble() * 360f;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float sc = isSmall ? Mathf.Lerp(0.55f, 0.75f, (float)c.rng.NextDouble()) : Mathf.Lerp(0.95f, 1.05f, (float)c.rng.NextDouble());
            go.transform.localScale = new Vector3(sc, sc, sc);

            c.placed.Add(new KeyValuePair<Vector2, bool>(p, !isSmall));
            if (isSmall) c.nSmall++; else c.nLarge++;
            string key = (isSmall ? "мелкие " : "крупные ") + Id(kind);
            int cnt; c.byKind.TryGetValue(key, out cnt); c.byKind[key] = cnt + 1;
            return true;
        }

        // повороты троп: угол между направлением «до» и «после» по плечам TurnWindow м; с внешней стороны поворота
        static List<Turn> FindTurns(IslandField f, IslandLayout L)
        {
            var res = new List<Turn>();
            for (int i = 0; i < L.paths.Count; i++)
            {
                var pl = f.PathPolyline(i);
                if (pl == null || pl.Length < 5) continue;
                var cum = new float[pl.Length];
                for (int j = 1; j < pl.Length; j++) cum[j] = cum[j - 1] + Vector2.Distance(pl[j], pl[j - 1]);
                for (int j = 1; j < pl.Length - 1; j++)
                {
                    int ja = j, jb = j;
                    while (ja > 0 && cum[j] - cum[ja] < TurnWindow) ja--;
                    while (jb < pl.Length - 1 && cum[jb] - cum[j] < TurnWindow) jb++;
                    if (cum[j] - cum[ja] < TurnWindow * 0.8f || cum[jb] - cum[j] < TurnWindow * 0.8f) continue;   // у концов тропы поворота нет
                    Vector2 a = (pl[j] - pl[ja]).normalized, b = (pl[jb] - pl[j]).normalized;
                    float deg = Vector2.Angle(a, b);
                    if (deg < TurnMinDeg) continue;
                    bool near = false;
                    for (int k = 0; k < res.Count && !near; k++) if (Vector2.Distance(res[k].p, pl[j]) < TurnSpacing) near = true;
                    if (near) continue;
                    Vector2 inward = b - a;
                    if (inward.sqrMagnitude < 1e-4f) continue;
                    res.Add(new Turn { p = pl[j], outward = -inward.normalized, deg = deg });
                }
            }
            res.Sort((x, y) => y.deg.CompareTo(x.deg));   // самые крутые первыми
            return res;
        }

        // рубимые деревья в радиусе полянки убираем; возвращает сколько убрано
        static int ClearTrees(List<Transform> trees, Vector2 p, float r)
        {
            int n = 0;
            for (int i = trees.Count - 1; i >= 0; i--)
            {
                var t = trees[i];
                if (t == null) { trees.RemoveAt(i); continue; }
                Vector2 q = new Vector2(t.position.x, t.position.z);
                if ((q - p).sqrMagnitude >= r * r) continue;
                var inst = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                GameObject target = inst != null ? inst : t.gameObject;
                trees.RemoveAt(i);
                Undo.DestroyObjectImmediate(target);
                n++;
            }
            return n;
        }

        static List<Vector2> ReadSacred(Transform manual)
        {
            var l = new List<Vector2>();
            var s = manual.Find("SacredTrees");
            if (s != null) for (int i = 0; i < s.childCount; i++) l.Add(new Vector2(s.GetChild(i).position.x, s.GetChild(i).position.z));
            return l;
        }

        static bool AnchorXY(IslandField f, IslandLayout L, string n, out Vector2 p)
        {
            var a = L.FindAnchor(n);
            if (a == null) { p = Vector2.zero; return false; }
            p = f.AnchorWorld(a);
            return true;
        }

        static Transform NewRoot(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }
    }
}
#endif
