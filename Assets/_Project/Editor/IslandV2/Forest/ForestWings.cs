#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestWings.cs (Editor)  НОВЫЙ ФАЙЛ
// «Кулисные деревья»: массовые нерубимые деревья-акценты в стиле хранителей (мультяшные, пастельные, облачные), но дешёвые: без частиц, Light, FireSource и
// компонента на каждом дереве. Заменяют старые фоновые Decor_* (кедры и дубы стеной) и кольцо старых дубов вокруг зоны рубки.
// Меню: Tools/BuilderSim/Лес/Красота леса/13. Кулисные деревья: сгенерировать (248), 13a. Кулисные деревья: убрать (249).
// Из «4. Лес» (ForestBackdrop.Run) вызывается сам, ПЕРЕД рощами рубимых деревьев: рощи обходят кулисы, а не наоборот, поэтому рубимый лес не страдает.
// Безопасно жать повторно: корень ForestWings в IslandV2/_Manual пересоздаётся целиком, меши и префабы перезаписываются, всё детерминировано по Seed.
// Геометрия видов и венка: WingTreeMeshes.cs. Рантайм: Scripts/Forest/WingForest.cs (один компонент на корне).
//
// ВИДЫ (7, у каждого 3 формы, то есть 21 префаб Wing_<Вид><0..2>): Cloud облачко-зонтик, Cotton сахарная вата, Willow радужная ива, Mushroom мятный
// гриб-дерево, Moon лунное дерево, Ribbon ленточное, Bush облачный куст (карлик 1-2 м). Деревья 4-14 м (масштаб 0.72-1.25 с зажимом по высоте).
//
// «НЕ РУБИТСЯ» с первого взгляда (два слоя):
//   1. Цвет. Нерубимые только пастель: белый, розовый, сиреневый, мятный, голубой, стволы лиловые или кремовые. Рубимые деревья (Tree_*) обычная зелень,
//      берёза, осень, коричневая кора: этих цветов у них нет. _PosVary у материалов кулис снижен до 0.10, чтобы авто-вариация шейдера не «позеленила» пастель.
//   2. Знак. У корня каждого дерева венок из камней: низкие камни и стоячие стелы с бирюзовыми рунами (тот же материал Sacred_Rune и Sacred_Stone, что у
//      хранителей, один меш на вид, без света). У хранителей и лёгких хранителей кольцо крупнее и есть парящий кристалл: кулисы это «младшие братья».
//
// ТЕХНИКА: у префаба меш в ассетах (Prefabs/Wings/Meshes), LODGroup из 2 уровней (LOD0 тело + венок, LOD1 тело попроще, дальше скрыто), капсульный коллайдер ствола
// (скользкий материал TreeTrunk_Slip), у куста коллайдера нет (как у подлеска). Нет тега Resource и нет ResourceItem. Тени отбрасывают только крупные виды.
//
// РАССТАНОВКА (акценты, а не стена; плотность задаёт площадь острова: один кластер на ClusterArea м²):
//   Кластер = 1-3 дерева разных видов и размеров рядом (кроны чуть наползают, венки не пересекаются) + 0-2 облачных куста. Между кластерами просветы.
//   Роли кластеров:
//     «кольцо»  RingN кластеров вокруг зоны рубки на 8-13 м от забора (вместо старых Decor_Elder_*): кулисы поляны, средние и крупные;
//     «край»    у обрыва (до 13 м) и воды (до 10 м): самые крупные, плотнее; у воды ивы вдвое-втрое чаще, у обрыва облачка;
//     «между»   между тропами и в чаще: средние и поменьше, реже (чем гуще лес, тем реже кулисы: там и так рубимая чаща).
//   Вид выбирается «пятнами настроения»: шум Перлина режется по квантилям на 5 равных по площади зон (розовая, голубая, облачная, мятная, радужная роща),
//   у каждой свой набор весов видов, поэтому лес читается пятнами цвета.
//   Не ставит: на тропе, воде, склоне круче 12°, у обрыва вплотную, в зданиях, у меток лагеря и Дедушки, в зоне рубки (забор + 5.5 м), на поляне PAD_GroveSouth,
//   в полянках хранителей и лёгких хранителей (читает их из сцены), рядом с уже стоящими рубимыми деревьями (венок должен быть чистым). Рубимые деревья НЕ убирает.
//   Хранители и лёгкие хранители, поставленные ПОЗЖЕ (пункты 9 и 13, в «0. Всё сразу» они идут после «4. Лес»), сами убирают кулисы в своей полянке.
// Подлесок и зона рубки обходят венки кулис (ChopZone.PrepareAvoid читает корень ForestWings), «16. Проверка леса» и «15. Замер леса» их учитывают.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class ForestWings
    {
        public const string RootName = "ForestWings";
        public const string NamePrefix = "Wing_";
        const string Dir = "Assets/_Project/Prefabs/Wings";
        const string MeshDir = Dir + "/Meshes";
        const string SlipPath = ForestMats.MatDir + "/TreeTrunk_Slip.asset";
        const int Seed = 6631;

        // ---- сколько (м, если не сказано иное)
        const float ClusterArea = 1500f;         // м² острова на один кластер: плотность ниже, чем у старого фона (стена)
        const int MinClusters = 10, MaxClusters = 48;
        const int MaxTrees = 110, MaxBushes = 50;   // потолки на всё: защита от лишнего на большом острове
        const int RingN = 8;                     // кластеров «кольца» вокруг зоны рубки
        const float FenceMargin = 5.5f;          // от забора ChopZone наружу: ближе не ставим (как у лёгких хранителей)
        const float ZoneNoR = ChopZone.ClearR + 1f, LandmarkNoR = 9f;   // зона рубки (PAD_GroveSouth) и маленькая достопримечательность (PAD_Forest)   // как noGo в ForestBackdrop
        const float KeepGap = 2.5f;              // запас до полянок хранителей сверх радиуса венка
        const float LodNear = 0.18f, LodCull = 0.02f;   // доли высоты экрана: LOD0 тело и венок, LOD1 тело попроще, ниже 2% скрыто

        enum Role { Ring, Edge, Inner }

        static readonly string[] ZoneNames = { "Розовая роща", "Голубая роща", "Облачная роща", "Мятная роща", "Радужная роща" };
        // веса видов по зонам, порядок столбцов: Cloud, Cotton, Willow, Mushroom, Moon, Ribbon (куст ставится отдельно)
        static readonly float[][] ZoneW =
        {
            new[] { 0.15f, 0.55f, 0.08f, 0.00f, 0.00f, 0.22f },   // розовая: сахарная вата, ленты
            new[] { 0.20f, 0.00f, 0.15f, 0.00f, 0.55f, 0.10f },   // голубая: лунные деревья
            new[] { 0.50f, 0.15f, 0.00f, 0.00f, 0.15f, 0.20f },   // облачная: зонтики
            new[] { 0.00f, 0.15f, 0.15f, 0.55f, 0.15f, 0.00f },   // мятная: грибы
            new[] { 0.10f, 0.10f, 0.45f, 0.15f, 0.00f, 0.20f }    // радужная: ивы
        };

        // ============================================================ результат
        public sealed class Result
        {
            public int clusters, trees, bushes;
            public List<Vector3> keep = new List<Vector3>();   // (x, z, радиус венка с запасом): сюда рубимые рощи не ставят
            public string report;
        }

        // радиус чистой полянки вокруг венка по имени и масштабу экземпляра (читают подлесок, проверка леса, ForestSacred/GuardLite)
        public static float ClearRadius(Transform t)
        {
            foreach (var k in WingMeshes.AllKinds)
                if (t.name.StartsWith(NamePrefix + WingMeshes.Id(k))) return WingMeshes.RingR(k) * t.localScale.x + 0.7f;
            return 2.5f;
        }

        // для хранителей и лёгких хранителей (ForestSacred, ForestGuardLite), которые встают ПОСЛЕ кулис: убирает кулисы, чьи венки задевают кольцо хранителя.
        // p: центр хранителя (x, z), r: радиус его кольца, м. Возвращает, сколько кулис убрано.
        public static int ClearAround(Vector2 p, float r)
        {
            var man = GameObject.Find("IslandV2/_Manual");
            if (man == null) return 0;
            var root = man.transform.Find(RootName);
            if (root == null) return 0;
            int n = 0;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var t = root.GetChild(i);
                Vector2 q = new Vector2(t.position.x, t.position.z);
                if (Vector2.Distance(q, p) >= r + ClearRadius(t) - 0.4f) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                n++;
            }
            return n;
        }

        // ============================================================ меню
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/13. Кулисные деревья: сгенерировать", false, 248)]
        static void RunMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Кулисы: останови Play Mode."); return; }
            var rootGo = GameObject.Find("IslandV2");
            if (rootGo == null) { Debug.LogError("Кулисы: в сцене нет IslandV2 (пункт «2. Собрать блокаут»)."); return; }
            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (L == null) { Debug.LogError("Кулисы: нет раскладки " + IslandLayout.AssetPath); return; }
            var f = new IslandField(L);
            var parentGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = parentGo != null ? parentGo.transform : rootGo.transform;

            // отдельный запуск: рубимые деревья уже стоят, кулисы встают в их просветы (ничего не убираем)
            var all = new Dictionary<long, List<Vector2>>();
            foreach (var ri in Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                ForestBackdrop.AddTrunk(all, new Vector2(ri.transform.position.x, ri.transform.position.z));
            var noGo = new List<Vector3>();
            Vector2 w;
            if (AnchorXY(f, L, ChopZone.PadName, out w)) noGo.Add(new Vector3(w.x, w.y, ZoneNoR));
            if (AnchorXY(f, L, "PAD_Forest", out w)) noGo.Add(new Vector3(w.x, w.y, LandmarkNoR));

            var res = Generate(f, L, parent, all, noGo);
            if (res != null) Debug.Log(res.report);
        }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/13a. Кулисные деревья: убрать", false, 249)]
        static void Clear()
        {
            var parentGo = GameObject.Find("IslandV2/_Manual");
            if (parentGo == null) { Debug.LogWarning("Кулисы: нет IslandV2/_Manual."); return; }
            var t = parentGo.transform.Find(RootName);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            Debug.Log("Кулисы: корень " + RootName + " убран.");
        }

        // ============================================================ главное
        // all: уже стоящие рубимые стволы (общий хеш 3 м), свои мы туда тоже добавляем. noGo: (x, z, радиус), сюда кулисы не ставим.
        public static Result Generate(IslandField f, IslandLayout L, Transform parent, Dictionary<long, List<Vector2>> all, List<Vector3> noGo)
        {
            var sb = new StringBuilder("Кулисные деревья:\n");

            // ---- префабы
            var kit = BuildKit(sb);
            if (kit == null) return null;

            // ---- корень (с единственным рантайм-компонентом на весь лес)
            var old = parent.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = NewRoot(parent, RootName);
            root.gameObject.AddComponent<WingForest>();

            var c = new Ctx { f = f, L = L, rng = new System.Random(Seed), root = root, kit = kit, all = all, noGo = noGo };
            c.keep = ForestGroves.ReadKeepOut();   // хранители и лёгкие хранители, если уже стоят в сцене
            Vector2 pad;
            c.hasPad = AnchorXY(f, L, ChopZone.PadName, out pad);
            c.pad = pad;
            c.mood = new Mood(f, Seed);

            int target = Mathf.Clamp(Mathf.RoundToInt(f.Area / ClusterArea), MinClusters, MaxClusters);
            sb.AppendLine("  площадь острова " + f.Area.ToString("0") + " м², целевое число кластеров " + target + " (один на " + ClusterArea.ToString("0") + " м²)");

            // ---- 1. кольцо вокруг зоны рубки
            int nRing = 0;
            if (c.hasPad)
            {
                for (int k = 0; k < RingN && c.clusters < target; k++)
                {
                    for (int t = 0; t < 12; t++)
                    {
                        float a = (k + ((float)c.rng.NextDouble() - 0.5f) * 0.5f) / RingN * Mathf.PI * 2f;
                        float rr = ChopZone.Rb(a) + FenceMargin + 3f + (float)c.rng.NextDouble() * 5f;
                        Vector2 p = pad + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                        if (PlaceCluster(c, p, Role.Ring)) { nRing++; break; }
                    }
                }
            }

            // ---- 2. край и между тропами: кандидаты по сетке 3 м в случайном порядке (детерминированно)
            var cand = new List<Vector2>();
            for (float gx = f.Min.x; gx <= f.Max.x; gx += 3f)
                for (float gz = f.Min.y; gz <= f.Max.y; gz += 3f)
                {
                    if (!f.Inside(gx, gz)) continue;
                    float e = f.EdgeDistance(gx, gz), wd = Mathf.Min(f.WaterDistance(gx, gz), 99f), pd = f.PathDistance(gx, gz);
                    if (e < 4f || wd < 3f || pd < 5f) continue;
                    cand.Add(new Vector2(gx, gz));
                }
            for (int i = cand.Count - 1; i > 0; i--) { int j = c.rng.Next(i + 1); var t = cand[i]; cand[i] = cand[j]; cand[j] = t; }

            int nEdge = 0, nInner = 0;
            for (int i = 0; i < cand.Count && c.clusters < target; i++)
            {
                Vector2 p = cand[i];
                float e = f.EdgeDistance(p.x, p.y), wd = Mathf.Min(f.WaterDistance(p.x, p.y), 99f), fw = f.ForestWeight(p.x, p.y);
                bool edge = e <= 13f || wd <= 10f;
                float accept = edge ? 0.85f : Mathf.Lerp(0.55f, 0.25f, fw);   // в чаще реже: там и так рубимый лес
                if (c.rng.NextDouble() > accept) continue;
                float spacing = edge ? 14f : Mathf.Lerp(17f, 25f, fw);
                if (TooCloseCenter(c, p, spacing)) continue;
                if (PlaceCluster(c, p, edge ? Role.Edge : Role.Inner)) { if (edge) nEdge++; else nInner++; }
            }

            // ---- сводка
            sb.AppendLine("  кластеров " + c.clusters + " из " + target + ": кольцо у зоны рубки " + nRing + ", край/вода " + nEdge + ", между тропами и в чаще " + nInner
                + (c.clusters < target ? " (места по правилам кончились: остров тесный для нерубимых, это нормально)" : ""));
            sb.AppendLine("  деревьев " + c.nTree + " (из " + MaxTrees + "), облачных кустов " + c.nBush + " (из " + MaxBushes + ")");
            foreach (var k in WingMeshes.AllKinds)
            {
                int cnt; c.byKind.TryGetValue(k, out cnt);
                sb.AppendLine("    " + WingMeshes.Title(k) + ": " + cnt + (cnt > 0 ? " (формы 0/1/2: " + c.byVar[(int)k, 0] + "/" + c.byVar[(int)k, 1] + "/" + c.byVar[(int)k, 2] + ")" : ""));
            }
            var zs = new StringBuilder();
            for (int z = 0; z < ZoneNames.Length; z++) zs.Append(ZoneNames[z] + " " + c.byZone[z] + (z < ZoneNames.Length - 1 ? ", " : ""));
            sb.AppendLine("  кластеры по зонам настроения: " + zs);
            sb.AppendLine("  размеры деревьев: до 6 м " + c.sizeBuckets[0] + ", 6-10 м " + c.sizeBuckets[1] + ", выше 10 м " + c.sizeBuckets[2]
                + (c.nTree > 0 ? " (минимум " + c.minH.ToString("0.0") + " м, максимум " + c.maxH.ToString("0.0") + " м)" : ""));
            sb.AppendLine("  ~Tris в LOD0 если бы все были рядом: " + c.trisLod0 + " (венки включены), в кадре будет только то, что видит камера, дальние уйдут в LOD1 и скроются. Точный замер: «15. Замер леса» и Game View в Play Mode.");
            sb.AppendLine("  отказов мест: вода/обрыв/тропа/склон/здания " + c.rejTerrain + ", зона рубки и поляны " + c.rejZone + ", хранители " + c.rejKeep + ", рубимые деревья рядом " + c.rejChop + ", другая кулиса рядом " + c.rejWing);
            sb.AppendLine("  Корень: IslandV2/_Manual/" + RootName + " (покачивание: один компонент WingForest на корне). Двигай, удаляй и дублируй руками: это обычные префабы, рубимые деревья не тронуты.");
            sb.AppendLine("  Дальше: «0. Всё сразу» или по одному: «9. Деревья-хранители», «13. Лёгкие хранители», «2. Подлесок» (обойдёт венки), «3. Проверить блокаут», «16. Проверка леса», «15. Замер леса».");
            if (c.nTree == 0) sb.AppendLine("  [!] ни одного дерева не поставлено: проверь, что в сцене есть IslandV2 и тропы, и смотри отказы выше.");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

            var res = new Result { clusters = c.clusters, trees = c.nTree, bushes = c.nBush, keep = c.keepOut, report = sb.ToString() };
            return res;
        }

        // ============================================================ контекст
        struct Pl
        {
            public Vector2 p; public WingKind kind; public int v; public float s, ring, t;
        }

        sealed class Ctx
        {
            public IslandField f; public IslandLayout L; public System.Random rng; public Transform root; public Kit kit; public Mood mood;
            public Dictionary<long, List<Vector2>> all; public List<Vector3> noGo; public List<Vector3> keep;   // keep: хранители из сцены
            public List<Vector3> keepOut = new List<Vector3>();                                                   // наши венки: для рощ
            public List<Pl> placed = new List<Pl>();
            public List<Vector2> centers = new List<Vector2>();
            public bool hasPad; public Vector2 pad;
            public int clusters, nTree, nBush, trisLod0;
            public int rejTerrain, rejZone, rejKeep, rejChop, rejWing;
            public Dictionary<WingKind, int> byKind = new Dictionary<WingKind, int>();
            public int[,] byVar = new int[7, 3];
            public int[] byZone = new int[5];
            public int[] sizeBuckets = new int[3];
            public float minH = 999f, maxH = 0f;
        }

        static bool TooCloseCenter(Ctx c, Vector2 p, float r)
        {
            for (int i = 0; i < c.centers.Count; i++) if ((c.centers[i] - p).sqrMagnitude < r * r) return true;
            return false;
        }

        // ============================================================ пятна настроения
        sealed class Mood
        {
            readonly float ox, oz;
            readonly float[] cut = new float[4];

            public Mood(IslandField f, int seed)
            {
                ox = seed * 0.0131f + 5.1f; oz = seed * 0.0077f + 9.7f;
                // границы зон по квантилям шума на острове: пять зон почти равной площади, какой бы ни была форма шума
                var vals = new List<float>();
                for (float gx = f.Min.x; gx <= f.Max.x; gx += 6f)
                    for (float gz = f.Min.y; gz <= f.Max.y; gz += 6f)
                        if (f.Inside(gx, gz)) vals.Add(Noise(gx, gz));
                vals.Sort();
                for (int i = 0; i < 4; i++) cut[i] = vals.Count > 0 ? vals[Mathf.Min(vals.Count - 1, (i + 1) * vals.Count / 5)] : (i + 1) * 0.2f;
            }

            float Noise(float x, float z)
            {
                return Mathf.PerlinNoise(x / 26f + ox, z / 26f + oz) * 0.75f + Mathf.PerlinNoise(x / 11f + oz, z / 11f + ox) * 0.25f;
            }

            public int Zone(float x, float z)
            {
                float n = Noise(x, z);
                int zn = 0;
                while (zn < 4 && n >= cut[zn]) zn++;
                return zn;
            }
        }

        static WingKind PickKind(Ctx c, int zone, Vector2 p)
        {
            float wd = Mathf.Min(c.f.WaterDistance(p.x, p.y), 99f), e = c.f.EdgeDistance(p.x, p.y);
            var w = new float[6];
            float sum = 0f;
            for (int i = 0; i < 6; i++)
            {
                w[i] = ZoneW[zone][i];
                if (i == (int)WingKind.Willow && wd < 10f) w[i] = w[i] * 3f + 0.05f;   // ивы тянутся к воде
                if (i == (int)WingKind.Cloud && e < 13f) w[i] = w[i] * 2f + 0.03f;      // облачка на фоне неба у обрыва
                sum += w[i];
            }
            double r = c.rng.NextDouble() * sum;
            for (int i = 0; i < 6; i++) { r -= w[i]; if (r <= 0.0) return (WingKind)i; }
            return WingKind.Cloud;
        }

        // «у обрыва и воды крупные, к чаще средние»: 0..1
        static float SizeT(Ctx c, float x, float z, Role role)
        {
            float d = Mathf.Min(c.f.EdgeDistance(x, z), Mathf.Min(c.f.WaterDistance(x, z), 99f));
            float far = Mathf.Clamp01((d - 5f) / 20f);
            float fw = c.f.ForestWeight(x, z);
            float t = Mathf.Lerp(0.95f, 0.30f, Mathf.Max(far, fw * 0.7f));
            if (role == Role.Ring) t = Mathf.Max(t, 0.55f);
            t += ((float)c.rng.NextDouble() - 0.5f) * 0.2f;
            return Mathf.Clamp01(t);
        }

        static float ScaleFor(WingKind k, float t)
        {
            if (k == WingKind.Bush) return Mathf.Lerp(0.8f, 1.5f, t);
            float h0 = WingMeshes.Height(k);
            return Mathf.Clamp(Mathf.Lerp(0.72f, 1.25f, t), 4.2f / h0, 14f / h0);   // дерево 4-14 м
        }

        // ============================================================ кластер
        static bool PlaceCluster(Ctx c, Vector2 center, Role role)
        {
            if (c.nTree >= MaxTrees) return false;
            int zone = c.mood.Zone(center.x, center.y);
            float t0 = SizeT(c, center.x, center.y, role);
            double roll = c.rng.NextDouble();
            int want = roll < 0.25 ? 1 : roll < 0.70 ? 2 : 3;

            var made = new List<Pl>();
            for (int i = 0; i < want && c.nTree + made.Count < MaxTrees; i++)
            {
                if (i > 0 && made.Count == 0) break;   // первое дерево кластера не нашло место: соседей не к чему привязывать (раньше тут падало IndexOutOfRange)
                for (int tries = 0; tries < 14; tries++)
                {
                    Pl pl = new Pl();
                    Vector2 p;
                    if (i == 0)
                    {
                        float ang = (float)c.rng.NextDouble() * Mathf.PI * 2f, rj = tries * 0.18f;
                        p = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rj;
                    }
                    else p = Vector2.zero;   // ниже, когда известен вид и размер

                    WingKind kind = PickKind(c, zone, i == 0 ? p : center);
                    float t = i == 0 ? t0 : Mathf.Clamp01(t0 + ((float)c.rng.NextDouble() - 0.5f) * 0.8f);
                    if (i > 0)
                    {
                        // соседи в кластере разного размера
                        float tp = made[made.Count - 1].t;
                        if (Mathf.Abs(t - tp) < 0.15f) t = t > 0.5f ? Mathf.Max(0f, t - 0.25f) : Mathf.Min(1f, t + 0.25f);
                    }
                    float s = ScaleFor(kind, t);
                    float ring = WingMeshes.RingR(kind) * s;
                    if (i > 0)
                    {
                        Pl anchor = made[c.rng.Next(made.Count)];
                        float ang = (float)c.rng.NextDouble() * Mathf.PI * 2f;
                        float dist = anchor.ring + ring + 0.5f + (float)c.rng.NextDouble() * 2.2f;   // венки рядом, но не пересекаются; кроны наползают
                        p = anchor.p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                    }
                    pl.p = p; pl.kind = kind; pl.v = c.rng.Next(WingMeshes.Variants); pl.s = s; pl.ring = ring; pl.t = t;
                    if (!Fits(c, pl, made)) continue;
                    made.Add(pl);
                    break;
                }
            }
            if (made.Count == 0) return false;

            // облачные кусты у кластера: подлесок нерубимой группы
            double br = c.rng.NextDouble();
            int nb = br < 0.45 ? 0 : br < 0.80 ? 1 : 2;
            int treesMade = made.Count;
            for (int i = 0; i < nb && c.nBush + CountBush(made) < MaxBushes; i++)
            {
                for (int tries = 0; tries < 10; tries++)
                {
                    Pl anchor = made[c.rng.Next(treesMade)];
                    float s = ScaleFor(WingKind.Bush, (float)c.rng.NextDouble());
                    float ring = WingMeshes.RingR(WingKind.Bush) * s;
                    float ang = (float)c.rng.NextDouble() * Mathf.PI * 2f;
                    float dist = anchor.ring + ring + 0.3f + (float)c.rng.NextDouble() * 1.6f;
                    var pl = new Pl { p = anchor.p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist, kind = WingKind.Bush, v = c.rng.Next(WingMeshes.Variants), s = s, ring = ring, t = 0.5f };
                    if (!Fits(c, pl, made)) continue;
                    made.Add(pl);
                    break;
                }
            }

            // ---- в сцену
            c.clusters++;
            c.centers.Add(center);
            c.byZone[zone]++;
            for (int i = 0; i < made.Count; i++) Spawn(c, made[i]);
            return true;
        }

        static int CountBush(List<Pl> l) { int n = 0; for (int i = 0; i < l.Count; i++) if (l[i].kind == WingKind.Bush) n++; return n; }

        static void Spawn(Ctx c, Pl pl)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(c.kit.prefab[(int)pl.kind, pl.v], c.root);
            Undo.RegisterCreatedObjectUndo(go, "Кулисное дерево");
            int n = c.placed.Count;
            go.name = NamePrefix + WingMeshes.Id(pl.kind) + pl.v + "_" + n;
            go.transform.position = new Vector3(pl.p.x, c.f.Height(pl.p.x, pl.p.y), pl.p.y);
            go.transform.rotation = Quaternion.Euler(0f, (float)c.rng.NextDouble() * 360f, 0f);
            go.transform.localScale = new Vector3(pl.s, pl.s, pl.s);

            c.placed.Add(pl);
            ForestBackdrop.AddTrunk(c.all, pl.p);
            c.keepOut.Add(new Vector3(pl.p.x, pl.p.y, pl.ring + 0.7f));
            int cnt; c.byKind.TryGetValue(pl.kind, out cnt); c.byKind[pl.kind] = cnt + 1;
            c.byVar[(int)pl.kind, pl.v]++;
            c.trisLod0 += c.kit.tris0[(int)pl.kind, pl.v] + c.kit.ringTris[(int)pl.kind];
            if (pl.kind == WingKind.Bush) c.nBush++;
            else
            {
                c.nTree++;
                float h = WingMeshes.Height(pl.kind) * pl.s;
                c.sizeBuckets[h < 6f ? 0 : h < 10f ? 1 : 2]++;
                c.minH = Mathf.Min(c.minH, h); c.maxH = Mathf.Max(c.maxH, h);
            }
        }

        // ============================================================ пригодность места
        static bool Fits(Ctx c, Pl pl, List<Pl> made)
        {
            var f = c.f; var L = c.L;
            float x = pl.p.x, z = pl.p.y, r = pl.ring;
            bool bush = pl.kind == WingKind.Bush;

            if (!f.Inside(x, z)) { c.rejTerrain++; return false; }
            if (f.EdgeDistance(x, z) < r + 1.2f) { c.rejTerrain++; return false; }
            if (f.WaterDistance(x, z) < r + 0.8f || f.IsFord(x, z)) { c.rejTerrain++; return false; }
            if (f.PathDistance(x, z) < r + 1.8f) { c.rejTerrain++; return false; }
            if (f.Slope(x, z) > (bush ? 16f : 12f)) { c.rejTerrain++; return false; }

            var p = pl.p;
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.kind == IslandLayout.AnchorKind.Building) { if (f.RectDistance(a, x, z) < r + 3.5f) { c.rejTerrain++; return false; } }
                else
                {
                    if (a.name == "PAD_Forest" || a.name == "PAD_GroveSouth") continue;   // их закрывают noGo и правило забора
                    float clear = a.name == "PAD_Grandpa" ? 14f : a.name == "SPAWN_Player" ? 6f + r : r + 3.5f;
                    if (Vector2.Distance(f.AnchorWorld(a), p) < clear) { c.rejTerrain++; return false; }
                }
            }
            for (int i = 0; i < L.meadows.Count; i++)
                if (Vector2.Distance(L.meadows[i].pos, p) < L.meadows[i].radius * 0.8f + r * 0.5f) { c.rejTerrain++; return false; }

            // зона рубки: забор и озеленение снаружи
            if (c.hasPad)
            {
                Vector2 d = p - c.pad;
                if (d.magnitude < ChopZone.Rb(Mathf.Atan2(d.y, d.x)) + FenceMargin + r) { c.rejZone++; return false; }
            }
            for (int i = 0; i < c.noGo.Count; i++)
            {
                float dx = c.noGo[i].x - x, dz = c.noGo[i].y - z, rr = c.noGo[i].z + r * 0.5f;
                if (dx * dx + dz * dz < rr * rr) { c.rejZone++; return false; }
            }
            // полянки хранителей и лёгких хранителей, которые уже стоят
            for (int i = 0; i < c.keep.Count; i++)
            {
                float dx = c.keep[i].x - x, dz = c.keep[i].y - z, rr = c.keep[i].z + r + KeepGap;
                if (dx * dx + dz * dz < rr * rr) { c.rejKeep++; return false; }
            }
            // рубимые деревья: венок должен быть чистым
            if (ForestBackdrop.Near(c.all, p, r + 1.0f)) { c.rejChop++; return false; }
            // другие кулисы: венки не пересекаются
            for (int i = 0; i < c.placed.Count; i++)
                if (Vector2.Distance(c.placed[i].p, p) < c.placed[i].ring + r + 0.4f) { c.rejWing++; return false; }
            for (int i = 0; i < made.Count; i++)
                if (Vector2.Distance(made[i].p, p) < made[i].ring + r + 0.4f) { c.rejWing++; return false; }
            return true;
        }

        // ============================================================ префабы и материалы
        sealed class Kit
        {
            public GameObject[,] prefab = new GameObject[7, 3];
            public int[,] tris0 = new int[7, 3];
            public int[] ringTris = new int[7];
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        // материал TreeToon: имя, цвет сверху, цвет снизу, градиент от/до (м, локально), дрожь, рим, сила градиента, уровень света (>0: плоский «светящийся»)
        static Material M(string name, Color top, Color bottom, float g0, float g1, float flutter, float rim, float grad, float flat)
        {
            var m = ForestMats.Get(name, top, bottom, g0, g1, flutter, rim, grad, true);
            if (m == null) return null;
            m.SetFloat("_PosVary", 0.10f);   // шейдер по умолчанию сдвигает цвет на тёплый/холодный по позиции: для пастели это сбило бы цветовой код
            if (flat > 0f) { m.SetFloat("_Level0", flat); m.SetFloat("_Level1", flat); m.SetFloat("_Level2", flat); }
            EditorUtility.SetDirty(m);
            return m;
        }

        // 4 материала по сабмешам, см. шапку WingTreeMeshes.cs. Палитра: только белый, розовый, сиреневый, мятный, голубой (рубимые деревья их не используют)
        static Material[] Mats(WingKind k)
        {
            switch (k)
            {
                case WingKind.Cloud:
                    return new[]
                    {
                        M("Wing_Cloud_Bark",  C(.80f, .74f, .90f), C(.45f, .38f, .62f), 0f, 5.5f, 0f,     .08f, 1.0f, 0f),
                        M("Wing_Cloud_Puff",  C(1.0f, .99f, 1.0f), C(.84f, .87f, 1.0f), 4.5f, 9.5f, .012f, .12f, .8f, 0f),
                        M("Wing_Cloud_Pink",  C(1.0f, .84f, .93f), C(.95f, .70f, .90f), 4.5f, 9.5f, .012f, .12f, .8f, 0f),
                        M("Wing_Cloud_Under", C(.72f, .62f, .96f), C(.55f, .48f, .88f), 4.5f, 9.5f, 0f,     0f,   1.0f, 0f)
                    };
                case WingKind.Cotton:
                    return new[]
                    {
                        M("Wing_Cotton_Bark",  C(.92f, .78f, .86f), C(.55f, .40f, .58f), 0f, 4.5f, 0f,     .08f, 1.0f, 0f),
                        M("Wing_Cotton_Candy", C(1.0f, .76f, .90f), C(.92f, .62f, .88f), 3.5f, 8.5f, .014f, .12f, .9f, 0f),
                        M("Wing_Cotton_Lilac", C(.84f, .72f, 1.0f), C(.74f, .60f, .96f), 3.5f, 8.5f, .014f, .12f, .9f, 0f),
                        M("Wing_Cotton_Under", C(.76f, .52f, .86f), C(.62f, .42f, .80f), 3.5f, 8.5f, 0f,    0f,   1.0f, 0f)
                    };
                case WingKind.Willow:
                    return new[]
                    {
                        M("Wing_Willow_Bark",  C(.88f, .80f, .92f), C(.50f, .42f, .64f), 0f, 6.5f, 0f,    .08f, 1.0f, 0f),
                        M("Wing_Willow_Pink",  C(1.0f, .78f, .88f), C(.96f, .66f, .84f), 2f, 9.5f, .05f,  .10f, .7f, 0f),
                        M("Wing_Willow_Mint",  C(.72f, 1.0f, .88f), C(.56f, .92f, .82f), 2f, 9.5f, .05f,  .10f, .7f, 0f),
                        M("Wing_Willow_Sky",   C(.70f, .88f, 1.0f), C(.58f, .78f, .98f), 2f, 9.5f, .05f,  .10f, .7f, 0f)
                    };
                case WingKind.Mushroom:
                    return new[]
                    {
                        M("Wing_Mushroom_Stem",  C(.98f, .94f, .90f), C(.80f, .74f, .80f), 0f, 3.5f, 0f,  .06f, 1.0f, 0f),
                        M("Wing_Mushroom_Cap",   C(.58f, .96f, .80f), C(.30f, .82f, .68f), 3f, 6f, 0f,    .12f, .9f, 0f),
                        M("Wing_Mushroom_Spot",  C(1.0f, 1.0f, 1.0f), C(.92f, .96f, 1.0f), 0f, 6f, 0f,    .10f, .3f, 0f),
                        M("Wing_Mushroom_Gills", C(.90f, 1.0f, .94f), C(.70f, .90f, .86f), 3f, 6f, 0f,    0f,   1.0f, 0f)
                    };
                case WingKind.Moon:
                    return new[]
                    {
                        M("Wing_Moon_Bark",  C(.86f, .88f, .96f), C(.52f, .54f, .74f), 0f, 5f, 0f,    .08f, 1.0f, 0f),
                        M("Wing_Moon_Crown", C(.72f, .88f, 1.0f), C(.52f, .72f, .98f), 4f, 9.5f, .012f, .12f, .9f, 0f),
                        M("Wing_Moon_Orb",   C(.98f, 1.0f, 1.0f), C(.80f, .96f, 1.0f), 0f, 9f, 0f,     0f,   0f,  1.15f),   // «светятся»: без градиента и теней, свет плоский
                        M("Wing_Moon_Under", C(.55f, .62f, .96f), C(.42f, .48f, .90f), 4f, 9.5f, 0f,   0f,   1.0f, 0f)
                    };
                case WingKind.Ribbon:
                    return new[]
                    {
                        M("Wing_Ribbon_Trunk", C(.88f, .78f, .96f), C(.52f, .42f, .72f), 0f, 8f, 0f,    .08f, 1.0f, 0f),
                        M("Wing_Ribbon_Lilac", C(.84f, .76f, 1.0f), C(.70f, .62f, .98f), 4f, 12f, .012f, .12f, .8f, 0f),
                        M("Wing_Ribbon_Pink",  C(1.0f, .82f, .92f), C(.94f, .70f, .88f), 4f, 12f, .012f, .12f, .8f, 0f),
                        M("Wing_Ribbon_Under", C(.60f, .70f, .98f), C(.46f, .56f, .92f), 4f, 12f, 0f,   0f,   1.0f, 0f)
                    };
                default:   // куст: сабмеш 0 не используется
                    return new[]
                    {
                        M("Wing_Bush_White", C(1.0f, .99f, 1.0f), C(.84f, .87f, 1.0f), 0f, 1.8f, .015f, .12f, .8f, 0f),
                        M("Wing_Bush_White", C(1.0f, .99f, 1.0f), C(.84f, .87f, 1.0f), 0f, 1.8f, .015f, .12f, .8f, 0f),
                        M("Wing_Bush_Pink",  C(1.0f, .80f, .90f), C(.95f, .66f, .86f), 0f, 1.8f, .015f, .12f, .8f, 0f),
                        M("Wing_Bush_Mint",  C(.70f, .98f, .86f), C(.52f, .88f, .80f), 0f, 1.8f, .015f, .12f, .8f, 0f)
                    };
            }
        }

        static Kit BuildKit(StringBuilder sb)
        {
            Material stone, rune, gem;
            ForestSacred.MarkMaterials(out stone, out rune, out gem);   // те же камень и бирюзовые руны, что у хранителей: один язык знака
            if (stone == null || rune == null) { Debug.LogError("Кулисы: не создались материалы знака (проверь шейдер TreeToon: Tools/BuilderSim/Лес/Tree Toon)."); return null; }
            EditorAssets.EnsureFolder(MeshDir);

            var kit = new Kit();
            int trisBody = 0, nBody = 0;
            foreach (var k in WingMeshes.AllKinds)
            {
                var mats = Mats(k);
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == null) { Debug.LogError("Кулисы: не создался материал #" + i + " вида " + WingMeshes.Id(k) + " (проверь шейдер TreeToon)."); return null; }

                string id = WingMeshes.Id(k);
                var ring = ForestMats.SaveMesh(WingMeshes.Wreath(k, "Wing_Wreath_" + id), MeshDir + "/Wing_Wreath_" + id + ".asset");
                kit.ringTris[(int)k] = ring.triangles.Length / 3;
                for (int v = 0; v < WingMeshes.Variants; v++)
                {
                    string nm = id + v;
                    var m0 = ForestMats.SaveMesh(WingMeshes.Body(k, v, 0, "Wing_" + nm + "_LOD0"), MeshDir + "/Wing_" + nm + "_LOD0.asset");
                    var m1 = ForestMats.SaveMesh(WingMeshes.Body(k, v, 1, "Wing_" + nm + "_LOD1"), MeshDir + "/Wing_" + nm + "_LOD1.asset");
                    kit.tris0[(int)k, v] = m0.triangles.Length / 3;
                    trisBody += kit.tris0[(int)k, v]; nBody++;
                    kit.prefab[(int)k, v] = BuildPrefab(k, v, m0, m1, ring, mats, stone, rune);
                }
            }
            AssetDatabase.SaveAssets();
            sb.AppendLine("  префабы: " + Dir + "/Wing_<Вид><0..2>.prefab (21 шт), меши в " + MeshDir + "; среднее ~Tris тела в LOD0 " + (nBody > 0 ? trisBody / nBody : 0));
            return kit;
        }

        static GameObject BuildPrefab(WingKind k, int v, Mesh m0, Mesh m1, Mesh ring, Material[] mats, Material stone, Material rune)
        {
            string name = NamePrefix + WingMeshes.Id(k) + v;
            var root = new GameObject(name);
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                bool shadow = WingMeshes.CastsShadow(k);
                var r0 = AddMesh(visual.transform, "Body_LOD0", m0, mats, shadow);
                var r1 = AddMesh(visual.transform, "Body_LOD1", m1, mats, false);
                var rRing = AddMesh(root.transform, "Ring", ring, new[] { stone, rune }, false);   // венок вне Visual: не качается

                var lg = root.AddComponent<LODGroup>();
                lg.fadeMode = LODFadeMode.None;
                lg.SetLODs(new[] { new LOD(LodNear, new Renderer[] { r0, rRing }), new LOD(LodCull, new Renderer[] { r1 }) });
                lg.RecalculateBounds();

                // сплошной ствол: капсула (у куста нет, как у подлеска)
                float tr = WingMeshes.TrunkR(k), th = WingMeshes.TrunkH(k);
                if (tr > 0f)
                {
                    var col = root.AddComponent<CapsuleCollider>();
                    col.direction = 1; col.radius = tr; col.height = Mathf.Max(th, tr * 2f + 0.01f); col.center = new Vector3(0f, th * 0.5f - 0.1f, 0f);
#if UNITY_6000_0_OR_NEWER
                    var slip = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(SlipPath);
#else
                    var slip = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(SlipPath);
#endif
                    if (slip != null) col.sharedMaterial = slip;
                }
                // тега Resource, ResourceItem, Light, FireSource и своих скриптов нет: срубить нельзя, покачивает общий WingForest на корне
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

        // ============================================================ сцена
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
