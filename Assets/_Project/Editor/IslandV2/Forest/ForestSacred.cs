#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestSacred.cs (Editor)  НОВЫЙ ФАЙЛ
// «Деревья-хранители»: нерубимые деревья-достопримечательности, которые добавляют лесу вайба и читаются как «это не рубим» без единого слова.
// Меню: Tools/BuilderSim/Лес/Красота леса/9. Деревья-хранители: собрать и расставить (238), 10. Убрать деревья-хранители (239).
// Безопасно жать повторно: корень SacredTrees в IslandV2/_Manual пересоздаётся целиком, префабы перезаписываются. Кнопка «0. Всё сразу» запускает пункт сама.
//
// 5 видов (модели процедурные, SacredTreeMeshes.cs; материалы TreeToon своего масштаба, пастельная палитра, не похожая на рубимые деревья):
//   Облачное   пушистая крона-облако, белое с розовым и сиреневым низом, рядом плавают облачка-спутники; стоит у обрыва, на фоне неба
//   Цветущее   розовая крона-«сакура», ковёр лепестков на земле, лепестки летят; у воды и вдоль троп
//   Кристальное  мятная воздушная крона, кристаллы из корней и ствола, ночью светятся и искрят; в глубине леса
//   Фонарное   сине-мятная крона с гирляндой тёплых фонариков, настоящий свет ночью; у тропы к поляне рубки и у входа в лес
//   Сонный страж  толстый ствол с закрытыми глазками и улыбкой, крона-купол с лаймовым мхом, «дышит»; в чаще
//
// ЕДИНЫЙ ЗНАК «НЕ РУБИТСЯ» (у каждого вида одинаково): каменное кольцо с бирюзовыми светящимися рунами вокруг корней + парящий бирюзовый кристалл над кроной.
// Рубимые деревья этого не имеют никогда. На префабе нет тега Resource и нет ResourceItem (срубить нельзя технически), зато есть LandmarkTree:
// подойти и нажать E = «погладить», тост с репликой (у каждого вида свои). Ствол сплошной (CapsuleCollider), по кольцу ходить можно.
//
// Расстановка (9 деревьев, детерминировано по seed): ищет по сетке 2.5 м места по правилам вида (обрыв, вода, тропа, вес леса, расстояние до меток),
//   не ближе 16 м друг к другу (одного вида 30 м), не на тропе и воде, не у зданий, не ближе 19 м к поляне рубки PAD_Forest (поляна остаётся рабочей),
//   не ближе 14 м к Дедушке. Обычные деревья (рубимые, а также лёгкие хранители GuardianLite) в радиусе 3.6 м от хранителя убираются: ему нужна полянка под кольцо.
//   Каждое дерево смотрит лицом на ближайшую тропу (у сонного стража там лицо). Не нашлось место для вида = пропущено, строка в отчёте.
// Порядок: после «4. Лес: расставить рабочие деревья» и до «2. Подлесок» (кнопка «0. Всё сразу» делает это сама; подлесок обходит кольца хранителей).
// Если потом перезапустишь «4. Лес» отдельно, он поставит фоновые деревья заново и может зацепить хранителей: нажми пункт 9 ещё раз.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class ForestSacred
    {
        const string Dir = "Assets/_Project/Prefabs/Sacred";
        const string RootName = "SacredTrees";
        const string SlipPath = ForestMats.MatDir + "/TreeTrunk_Slip.asset";
        const int Seed = 7717;
        const float ClearR = 3.6f;                 // вокруг хранителя убираем обычный лес, м
        const float SpaceAll = 16f, SpaceSame = 30f;

        // сколько и в каком порядке ставим: сначала самые «капризные» виды
        static readonly SacredKind[] Plan =
        {
            SacredKind.Cloud, SacredKind.Sleeper, SacredKind.Crystal, SacredKind.Lantern, SacredKind.Blossom,
            SacredKind.Cloud, SacredKind.Lantern, SacredKind.Blossom, SacredKind.Crystal
        };

        // ============================================================ тексты по видам
        sealed class Spec
        {
            public SacredKind kind;
            public string id, title, prompt;
            public Color toast;
            public string[] lines;
        }

        static Spec SpecOf(SacredKind k)
        {
            switch (k)
            {
                case SacredKind.Cloud:
                    return new Spec
                    {
                        kind = k, id = "Cloud", title = "Облачное дерево", prompt = "погладить облачко", toast = new Color(0.78f, 0.85f, 1f),
                        lines = new[] { "ПУХ-ПУХ... Я ОБЛАКО, МЕНЯ НЕ РУБЯТ", "Я ВЫРОСЛО ВЫШЕ ВСЕХ, ЧТОБЫ ЛОВИТЬ ТУЧКИ", "МЕНЯ НЕ РУБЯТ, МЕНЯ ОБНИМАЮТ", "ТИХО... СЛЫШНО, КАК ПЛЫВУТ ОБЛАКА", "ТУТ САМЫЙ МЯГКИЙ ВЕТЕР НА ОСТРОВЕ" }
                    };
                case SacredKind.Blossom:
                    return new Spec
                    {
                        kind = k, id = "Blossom", title = "Цветущее дерево", prompt = "понюхать цветы", toast = new Color(1f, 0.72f, 0.84f),
                        lines = new[] { "ЛЕПЕСТКИ ТОЛЬКО ДЛЯ ТЕБЯ", "ВЕСНА У МЕНЯ ВСЕГДА", "ЦВЕТЫ НЕ РУБЯТ, ИМИ ЛЮБУЮТСЯ", "ЗАГАДАЙ ЖЕЛАНИЕ, ПОКА ЛЕТЯТ ЛЕПЕСТКИ", "ОСТАНОВИСЬ НА МИНУТКУ" }
                    };
                case SacredKind.Crystal:
                    return new Spec
                    {
                        kind = k, id = "Crystal", title = "Кристальное дерево", prompt = "прислушаться к кристаллам", toast = new Color(0.60f, 0.90f, 1f),
                        lines = new[] { "ЗВЕНИТ... ЭТО ПЕСНЯ КАМНЯ", "Я РАСТУ ИЗ СВЕТА, МЕНЯ НЕ СРУБИТЬ", "КРИСТАЛЛЫ ХРАНЯТ ЭХО ОСТРОВА", "НОЧЬЮ Я СВЕЧУСЬ ЯРЧЕ" }
                    };
                case SacredKind.Lantern:
                    return new Spec
                    {
                        kind = k, id = "Lantern", title = "Фонарное дерево", prompt = "согреться у огоньков", toast = new Color(1f, 0.82f, 0.50f),
                        lines = new[] { "ЭТИ ОГОНЬКИ ЗАЖЖЕНЫ ДЛЯ ТЕБЯ", "НЕ ТОПОРОМ, А ТЁПЛЫМ СЛОВОМ", "ПУТНИК, ТЫ НЕ ОДИН В ЛЕСУ", "Я ПОКАЖУ ДОРОГУ ДОМОЙ", "ПОСИДИ, ПОГРЕЙСЯ" }
                    };
                default:
                    return new Spec
                    {
                        kind = SacredKind.Sleeper, id = "Sleeper", title = "Сонный страж", prompt = "тихонько погладить", toast = new Color(0.60f, 0.95f, 0.75f),
                        lines = new[] { "ХРРР... ПШШШ... ЕЩЁ ПЯТЬ МИНУТОК", "НЕ БУДИ... Я СПЛЮ УЖЕ СТО ЛЕТ", "ЭТО ЛЕС ВО СНЕ, ТИХО-ТИХО", "МНЕ СНИТСЯ ДОЖДИК... ХОРОШО", "МЕНЯ ТОЧНО НЕ РУБЯТ: Я НЕ ПРОСНУЛСЯ" }
                    };
            }
        }

        // ============================================================ материалы
        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        // имя, цвет сверху, цвет снизу, высота градиента от (м), до (м), дрожь листвы, рим, сила градиента
        internal static Material[] Mats(SacredKind k)
        {
            switch (k)
            {
                case SacredKind.Cloud:
                    return new[]
                    {
                        ForestMats.Get("Sacred_Cloud_Bark",  C(0.74f, 0.66f, 0.74f), C(0.34f, 0.28f, 0.40f), 0f, 4f, 0f,     0.08f, 1.0f, true),
                        ForestMats.Get("Sacred_Cloud_Puff",  C(1.00f, 0.98f, 1.00f), C(0.84f, 0.88f, 1.00f), 3f, 8f, 0.012f, 0.12f, 0.8f, true),
                        ForestMats.Get("Sacred_Cloud_Pink",  C(1.00f, 0.84f, 0.92f), C(0.94f, 0.70f, 0.88f), 3f, 8f, 0.012f, 0.12f, 0.8f, true),
                        ForestMats.Get("Sacred_Cloud_Under", C(0.62f, 0.68f, 0.92f), C(0.46f, 0.52f, 0.82f), 3f, 8f, 0f,     0.00f, 1.0f, true)
                    };
                case SacredKind.Blossom:
                    return new[]
                    {
                        ForestMats.Get("Sacred_Blossom_Bark",  C(0.48f, 0.32f, 0.36f), C(0.20f, 0.12f, 0.16f), 0f, 4f, 0f,     0.06f, 1.0f, true),
                        ForestMats.Get("Sacred_Blossom_Light", C(1.00f, 0.82f, 0.90f), C(0.96f, 0.64f, 0.80f), 3f, 7f, 0.020f, 0.12f, 0.9f, true),
                        ForestMats.Get("Sacred_Blossom_Deep",  C(1.00f, 0.58f, 0.76f), C(0.88f, 0.38f, 0.62f), 3f, 7f, 0.020f, 0.12f, 0.9f, true),
                        ForestMats.Get("Sacred_Blossom_Under", C(0.62f, 0.30f, 0.50f), C(0.34f, 0.14f, 0.30f), 3f, 7f, 0f,     0.00f, 1.0f, true)
                    };
                case SacredKind.Crystal:
                    return new[]
                    {
                        ForestMats.Get("Sacred_Crystal_Bark",  C(0.78f, 0.74f, 0.90f), C(0.40f, 0.38f, 0.58f), 0f, 4f, 0f,     0.10f, 1.0f, true),
                        ForestMats.Get("Sacred_Crystal_Crown", C(0.66f, 0.96f, 0.88f), C(0.30f, 0.72f, 0.74f), 3f, 7f, 0.012f, 0.12f, 0.9f, true),
                        ForestMats.Get("Sacred_Crystal_Gem",   C(0.62f, 0.86f, 1.00f), C(0.62f, 0.50f, 1.00f), 0f, 2.5f, 0f,   0.00f, 0.6f, true),
                        ForestMats.Get("Sacred_Crystal_Under", C(0.18f, 0.46f, 0.56f), C(0.08f, 0.24f, 0.36f), 3f, 7f, 0f,     0.00f, 1.0f, true)
                    };
                case SacredKind.Lantern:
                    return new[]
                    {
                        ForestMats.Get("Sacred_Lantern_Bark",  C(0.58f, 0.42f, 0.30f), C(0.24f, 0.16f, 0.12f), 0f, 4f, 0f,     0.06f, 1.0f, true),
                        ForestMats.Get("Sacred_Lantern_Crown", C(0.46f, 0.82f, 0.78f), C(0.12f, 0.40f, 0.46f), 3f, 7f, 0.014f, 0.12f, 0.9f, true),
                        ForestMats.Get("Sacred_Lantern_Glow",  C(1.00f, 0.88f, 0.52f), C(1.00f, 0.66f, 0.34f), 0f, 4f, 0f,     0.00f, 0.0f, true),
                        ForestMats.Get("Sacred_Lantern_Under", C(0.08f, 0.28f, 0.34f), C(0.03f, 0.13f, 0.20f), 3f, 7f, 0f,     0.00f, 1.0f, true)
                    };
                default:
                    return new[]
                    {
                        ForestMats.Get("Sacred_Sleeper_Bark",  C(0.62f, 0.46f, 0.38f), C(0.24f, 0.16f, 0.14f), 0f, 4f, 0f,     0.06f, 1.0f, true),
                        ForestMats.Get("Sacred_Sleeper_Crown", C(0.30f, 0.68f, 0.50f), C(0.06f, 0.30f, 0.30f), 3f, 8f, 0.012f, 0.12f, 0.9f, true),
                        ForestMats.Get("Sacred_Sleeper_Moss",  C(0.78f, 0.94f, 0.38f), C(0.34f, 0.62f, 0.22f), 3f, 8f, 0.016f, 0.12f, 0.9f, true),
                        ForestMats.Get("Sacred_Sleeper_Dark",  C(0.30f, 0.16f, 0.26f), C(0.14f, 0.07f, 0.14f), 0f, 8f, 0f,     0.00f, 1.0f, true)
                    };
            }
        }

        // общий знак «не рубится» (камень кольца, руны, кристалл): один набор материалов на хранителей и лёгких хранителей
        internal static void MarkMaterials(out Material stone, out Material rune, out Material gem)
        {
            stone = ForestMats.Get("Sacred_Stone", C(0.80f, 0.78f, 0.92f), C(0.44f, 0.42f, 0.62f), 0f, 1.2f, 0f, 0.16f, 1f, true);
            rune = ForestMats.Get("Sacred_Rune", C(0.55f, 1.00f, 0.88f), C(0.30f, 0.88f, 0.82f), 0f, 1.2f, 0f, 0f, 0f, true);
            gem = ForestMats.Get("Sacred_Gem", C(0.70f, 1.00f, 0.95f), C(0.40f, 0.90f, 0.90f), 0f, 1.6f, 0f, 0f, 0f, true);
        }

        // ============================================================ меню
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/9. Деревья-хранители: собрать и расставить", false, 238)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Хранители: останови Play Mode."); return; }
            var rootGo = GameObject.Find("IslandV2");
            if (rootGo == null) { Debug.LogError("Хранители: в сцене нет IslandV2 (пункт «2. Собрать блокаут»)."); return; }
            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (L == null) { Debug.LogError("Хранители: нет раскладки " + IslandLayout.AssetPath); return; }
            var f = new IslandField(L);
            var sb = new StringBuilder("Деревья-хранители:\n");

            // ---- общий знак: кольцо и кристалл
            EditorAssets.EnsureFolder(Dir);
            Material stone, rune, gemMat;
            MarkMaterials(out stone, out rune, out gemMat);
            if (stone == null || rune == null || gemMat == null) { Debug.LogError("Хранители: не создались материалы (проверь шейдер TreeToon: Tools/BuilderSim/Лес/Tree Toon)."); return; }
            var markMesh = ForestMats.SaveMesh(SacredMeshes.Mark("Sacred_Mark"), Dir + "/Sacred_Mark.asset");
            var gemMesh = ForestMats.SaveMesh(SacredMeshes.Gem("Sacred_Gem"), Dir + "/Sacred_Gem.asset");

            // ---- префабы пяти видов
            var prefabs = new Dictionary<SacredKind, GameObject>();
            foreach (SacredKind k in System.Enum.GetValues(typeof(SacredKind)))
            {
                var pf = BuildPrefab(SpecOf(k), stone, rune, gemMat, markMesh, gemMesh);
                if (pf == null) return;
                prefabs[k] = pf;
            }
            AssetDatabase.SaveAssets();
            sb.AppendLine("  префабы: " + Dir + "/Sacred_*.prefab (5 видов)");

            // ---- корень в сцене
            var parentGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = parentGo != null ? parentGo.transform : rootGo.transform;
            RemoveRoot(parent);
            var root = NewRoot(parent, RootName);

            // ---- что уже растёт: рубимые и фоновые деревья (их придётся подвинуть)
            var trees = new List<Transform>();
            foreach (var ri in Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) trees.Add(ri.transform);
            var decorRoot = GameObject.Find("IslandV2/_Manual/ForestDecor");   // корень старых версий, если остался
            if (decorRoot != null) foreach (Transform t in decorRoot.transform) trees.Add(t);
            var liteRoot = GameObject.Find("IslandV2/_Manual/" + ForestGuardLite.RootName);
            if (liteRoot != null) foreach (Transform t in liteRoot.transform) trees.Add(t);

            // ---- кандидаты по сетке
            var cands = new List<Cand>();
            Vector2 gp, glade, gate;
            bool hasG = AnchorXY(f, L, "PAD_Grandpa", out gp);
            bool hasGl = AnchorXY(f, L, ChopZone.PadName, out glade);
            bool hasGt = AnchorXY(f, L, "ForestGate", out gate);
            for (float gx = f.Min.x; gx <= f.Max.x; gx += 2.5f)
            {
                for (float gz = f.Min.y; gz <= f.Max.y; gz += 2.5f)
                {
                    if (!ValidBase(f, L, gx, gz)) continue;
                    var p = new Vector2(gx, gz);
                    cands.Add(new Cand
                    {
                        x = gx, z = gz,
                        e = f.EdgeDistance(gx, gz),
                        pd = f.PathDistance(gx, gz),
                        wd = Mathf.Min(f.WaterDistance(gx, gz), 99f),
                        fw = f.ForestWeight(gx, gz),
                        dGrandpa = hasG ? Vector2.Distance(p, gp) : 999f,
                        dGlade = hasGl ? Vector2.Distance(p, glade) : 999f,
                        dGate = hasGt ? Vector2.Distance(p, gate) : 999f,
                        dCamp = p.magnitude
                    });
                }
            }
            sb.AppendLine("  подходящих мест на сетке: " + cands.Count);

            // ---- выбор мест
            var rng = new System.Random(Seed);
            var placed = new List<KeyValuePair<Vector2, SacredKind>>();
            var misses = new List<SacredKind>();
            int removed = 0;
            var counts = new Dictionary<SacredKind, int>();
            for (int pi = 0; pi < Plan.Length; pi++)
            {
                SacredKind kind = Plan[pi];
                var ranked = new List<KeyValuePair<float, int>>();
                for (int ci = 0; ci < cands.Count; ci++)
                {
                    float sc = Score(kind, cands[ci]);
                    if (sc < -1e8f) continue;
                    ranked.Add(new KeyValuePair<float, int>(sc + (float)rng.NextDouble() * 1.2f, ci));
                }
                ranked.Sort((a, b) => b.Key.CompareTo(a.Key));

                bool done = false;
                for (int ri2 = 0; ri2 < ranked.Count && !done; ri2++)
                {
                    var c = cands[ranked[ri2].Value];
                    var cp = new Vector2(c.x, c.z);
                    if (TooClose(placed, cp, kind)) continue;

                    int n; counts.TryGetValue(kind, out n);
                    counts[kind] = n + 1;
                    removed += ClearTrees(trees, cp);
                    removed += ForestWings.ClearAround(cp, 3.0f);   // кулисы с венком у кольца хранителя тоже уходят
                    Spawn(prefabs[kind], root, f, L, c.x, c.z, rng, "Sacred_" + SpecOf(kind).id + "_" + n);
                    placed.Add(new KeyValuePair<Vector2, SacredKind>(cp, kind));
                    sb.AppendLine("  " + SpecOf(kind).title + ": (" + c.x.ToString("0.#") + ", " + c.z.ToString("0.#") + ")");
                    done = true;
                }
                if (!done) misses.Add(kind);
            }

            foreach (var m in misses) sb.AppendLine("  [!] не нашлось места для вида «" + SpecOf(m).title + "» (правила вида слишком строгие для текущего острова)");
            sb.AppendLine("  поставлено " + placed.Count + " из " + Plan.Length + ", убрано обычных деревьев вокруг: " + removed);
            sb.AppendLine("  Корень: IslandV2/_Manual/" + RootName + ". Двигай, удаляй и дублируй руками, это обычные префабы.");
            sb.AppendLine("  Дальше: «2. Подлесок» ещё раз (обойдёт кольца), «3. Проверить блокаут» (проходимость), «4. замер Batches/Tris».");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/10. Убрать деревья-хранители", false, 239)]
        static void Clear()
        {
            var parentGo = GameObject.Find("IslandV2/_Manual");
            if (parentGo == null) { Debug.LogWarning("Хранители: нет IslandV2/_Manual."); return; }
            RemoveRoot(parentGo.transform);
            Debug.Log("Хранители: корень " + RootName + " убран (убранные ими обычные деревья вернёт «4. Лес: расставить рабочие деревья»).");
        }

        // ============================================================ префаб
        static GameObject BuildPrefab(Spec sp, Material stone, Material rune, Material gemMat, Mesh markMesh, Mesh gemMesh)
        {
            var mats = Mats(sp.kind);
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == null) { Debug.LogError("Хранители: не создался материал #" + i + " вида " + sp.id + " (проверь шейдер TreeToon)."); return null; }

            var m0 = ForestMats.SaveMesh(SacredMeshes.Body(sp.kind, 0, "Sacred_" + sp.id + "_LOD0"), Dir + "/Sacred_" + sp.id + "_LOD0.asset");
            var m1 = ForestMats.SaveMesh(SacredMeshes.Body(sp.kind, 1, "Sacred_" + sp.id + "_LOD1"), Dir + "/Sacred_" + sp.id + "_LOD1.asset");

            var root = new GameObject("Sacred_" + sp.id);
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var r0 = AddMesh(visual.transform, "Body_LOD0", m0, mats, true);
                var r1 = AddMesh(visual.transform, "Body_LOD1", m1, mats, true);

                // единый знак: кольцо и кристалл вне LODGroup (маленькие, видны всегда)
                var rMark = AddMesh(root.transform, "Mark", markMesh, new[] { stone, rune }, false);
                var rGem = AddMesh(root.transform, "Gem", gemMesh, new[] { gemMat }, false);
                float gemY = SacredMeshes.GemY(sp.kind);
                rGem.transform.localPosition = new Vector3(0f, gemY, 0f);

                var lg = root.AddComponent<LODGroup>();
                lg.fadeMode = LODFadeMode.None;
                lg.SetLODs(new[] { new LOD(0.07f, new Renderer[] { r0 }), new LOD(0.01f, new Renderer[] { r1 }) });
                lg.RecalculateBounds();

                // свет: руны у всех, остальное по виду
                AddFire(root.transform, "Light_Runes", Vector3.zero, FlickerPreset.Garland, new Color(0.45f, 1f, 0.85f), 1f, 4f, true, 0.7f, SacredMeshes.RuneSpots());
                if (sp.kind == SacredKind.Crystal)
                {
                    AddFire(root.transform, "Light_Crystals", Vector3.zero, FlickerPreset.Garland, new Color(0.65f, 0.85f, 1f), 1f, 4f, true, 0.9f, SacredMeshes.CrystalSpots());
                    AddFire(root.transform, "Light_CrystalMain", new Vector3(0f, 1.4f, 0f), FlickerPreset.Steady, new Color(0.60f, 0.80f, 1f), 1.6f, 6.5f, false, 1.3f, null);
                }
                else if (sp.kind == SacredKind.Lantern)
                {
                    AddFire(root.transform, "Light_Lanterns", Vector3.zero, FlickerPreset.Lantern, new Color(1f, 0.80f, 0.45f), 1f, 4f, true, 0.8f, SacredMeshes.LanternSpots().ToArray());
                    AddFire(root.transform, "Light_LanternMain", new Vector3(0f, 3.2f, 0f), FlickerPreset.Lantern, new Color(1f, 0.78f, 0.45f), 2.4f, 8f, false, 1.4f, null);
                }

                // сплошной ствол
                var col = root.AddComponent<CapsuleCollider>();
                float tr = SacredMeshes.TrunkR(sp.kind), th = SacredMeshes.TrunkH(sp.kind);
                col.direction = 1; col.radius = tr; col.height = Mathf.Max(th, tr * 2f + 0.01f); col.center = new Vector3(0f, th * 0.5f - 0.1f, 0f);
#if UNITY_6000_0_OR_NEWER
                var slip = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(SlipPath);
#else
                var slip = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(SlipPath);
#endif
                if (slip != null) col.sharedMaterial = slip;

                // E: «погладить». Тега Resource и ResourceItem нет, срубить нельзя
                var lm = root.AddComponent<LandmarkTree>();
                lm.title = sp.title;
                lm.caption = "ДРЕВО-ХРАНИТЕЛЬ";
                lm.toastColor = sp.toast;
                lm.greetings = sp.lines;
                lm.prompt = sp.prompt;
                lm.radius = 4.8f;
                lm.promptHeight = 2.3f;

                var vibe = root.AddComponent<SacredVibe>();
                vibe.kind = sp.kind;
                vibe.visual = visual.transform;
                vibe.gem = rGem.transform;
                vibe.gemHeight = gemY;
                vibe.runes = rMark;
                vibe.runeMaterialIndex = 1;
                vibe.gemRenderer = rGem;
                vibe.gemMaterialIndex = 0;
                bool selfLit = sp.kind == SacredKind.Crystal || sp.kind == SacredKind.Lantern;
                vibe.glowRenderer = selfLit ? r0 : null;
                vibe.glowMaterialIndex = 2;
                vibe.crownHeight = SacredMeshes.CrownY(sp.kind);
                vibe.crownRadius = SacredMeshes.CrownR(sp.kind);
                vibe.swayDeg = sp.kind == SacredKind.Sleeper ? 0.35f : 0.7f;
                vibe.breathe = sp.kind == SacredKind.Sleeper ? 0.012f : 0f;

                return PrefabUtility.SaveAsPrefabAsset(root, Dir + "/Sacred_" + sp.id + ".prefab");
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

        static void AddFire(Transform parent, string name, Vector3 local, FlickerPreset preset, Color color, float intensity, float range, bool simOnly, float glowSize, Vector3[] glowPoints)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var fs = go.AddComponent<FireSource>();
            fs.preset = preset;
            fs.color = color;
            fs.intensity = intensity;
            fs.range = range;
            fs.simOnly = simOnly;
            fs.glow = true;
            fs.glowSize = glowSize;
            if (glowPoints != null) fs.glowPoints = glowPoints;
        }

        // ============================================================ места
        struct Cand { public float x, z, e, pd, wd, fw, dGrandpa, dGlade, dGate, dCamp; }

        static bool AnchorXY(IslandField f, IslandLayout L, string n, out Vector2 p)
        {
            var a = L.FindAnchor(n);
            if (a == null) { p = Vector2.zero; return false; }
            p = f.AnchorWorld(a);
            return true;
        }

        // базовые правила для любого вида: внутри острова, не у обрыва вплотную, не вода, не тропа, не крутой склон, не у зданий и меток
        static bool ValidBase(IslandField f, IslandLayout L, float x, float z)
        {
            if (!f.Inside(x, z) || f.EdgeDistance(x, z) < 4.5f) return false;
            if (f.WaterDistance(x, z) < 3.5f || f.IsFord(x, z)) return false;
            if (f.PathDistance(x, z) < 3.8f) return false;
            if (f.Slope(x, z) > 14f) return false;
            var p = new Vector2(x, z);
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.kind == IslandLayout.AnchorKind.Building) { if (f.RectDistance(a, x, z) < 7f) return false; }
                else
                {
                    float clear = a.name == "PAD_Grandpa" ? 14f : a.name == "PAD_Forest" ? 9f : a.name == "PAD_GroveSouth" ? 19f : a.name == "SPAWN_Player" ? 6f : 5f;
                    if (Vector2.Distance(f.AnchorWorld(a), p) < clear) return false;
                }
            }
            return true;
        }

        // правила вида: больше = лучше, очень маленькое число = не подходит
        static float Score(SacredKind k, Cand c)
        {
            const float no = -1e9f;
            switch (k)
            {
                case SacredKind.Cloud:       // у обрыва на фоне неба, недалеко от тропы
                    if (c.e < 5f || c.e > 12f || c.pd > 30f || c.fw > 0.8f) return no;
                    return 10f - Mathf.Abs(c.e - 7.5f) - 0.1f * c.pd;
                case SacredKind.Sleeper:     // в чаще
                    if (c.fw < 0.5f || c.e < 10f || c.pd < 6f || c.pd > 24f || c.dGrandpa < 20f) return no;
                    return c.fw * 4f + 0.05f * c.e - 0.15f * Mathf.Abs(c.pd - 10f);
                case SacredKind.Crystal:     // в глубине леса, но не у самой тропы
                    if (c.fw < 0.4f || c.e < 9f || c.pd < 5f || c.pd > 22f) return no;
                    return c.fw * 3f - 0.1f * Mathf.Abs(c.pd - 9f);
                case SacredKind.Lantern:     // вдоль тропы к входу в лес и к поляне рубки
                    float dn = Mathf.Min(c.dGate, c.dGlade);
                    if (c.pd < 3.8f || c.pd > 8f || c.dGlade < 19f || dn > 30f) return no;
                    return 6f - Mathf.Abs(c.pd - 4.8f) - 0.1f * dn;
                default:                     // цветущее: у воды или вдоль троп, подальше от лагеря
                    bool byWater = c.wd >= 3.5f && c.wd <= 14f;
                    bool byPath = c.pd >= 3.8f && c.pd <= 7f;
                    if ((!byWater && !byPath) || c.dCamp < 18f || c.fw > 0.7f) return no;
                    return (byWater ? 5f - 0.3f * Mathf.Abs(c.wd - 7f) : 0f) + (byPath ? 2f : 0f);
            }
        }

        static bool TooClose(List<KeyValuePair<Vector2, SacredKind>> placed, Vector2 p, SacredKind k)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                float d = Vector2.Distance(placed[i].Key, p);
                if (d < SpaceAll) return true;
                if (placed[i].Value == k && d < SpaceSame) return true;
            }
            return false;
        }

        // убирает обычные деревья (рубимые и фоновые) рядом; возвращает сколько убрано
        static int ClearTrees(List<Transform> trees, Vector2 p)
        {
            int n = 0;
            for (int i = trees.Count - 1; i >= 0; i--)
            {
                var t = trees[i];
                if (t == null) { trees.RemoveAt(i); continue; }
                Vector2 q = new Vector2(t.position.x, t.position.z);
                if ((q - p).sqrMagnitude >= ClearR * ClearR) continue;
                var inst = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                GameObject target = inst != null ? inst : t.gameObject;
                trees.RemoveAt(i);
                Undo.DestroyObjectImmediate(target);
                n++;
            }
            return n;
        }

        // ============================================================ сцена
        static void Spawn(GameObject prefab, Transform parent, IslandField f, IslandLayout L, float x, float z, System.Random rng, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.name = name;
            go.transform.position = new Vector3(x, f.Height(x, z), z);
            // лицом к ближайшей тропе: у сонного стража там лицо, у остальных красивый «фасад»
            Vector2 target = NearestPathPoint(f, L, new Vector2(x, z));
            Vector2 d = target - new Vector2(x, z);
            float yaw = d.sqrMagnitude > 0.01f ? Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg : (float)rng.NextDouble() * 360f;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float sc = Mathf.Lerp(0.95f, 1.10f, (float)rng.NextDouble());
            go.transform.localScale = new Vector3(sc, sc, sc);
        }

        internal static Vector2 NearestPathPoint(IslandField f, IslandLayout L, Vector2 p)
        {
            float best = float.MaxValue;
            Vector2 bp = Vector2.zero;
            for (int i = 0; i < L.paths.Count; i++)
            {
                var pl = f.PathPolyline(i);
                if (pl == null) continue;
                for (int j = 0; j < pl.Length; j++)
                {
                    float d = (pl[j] - p).sqrMagnitude;
                    if (d < best) { best = d; bp = pl[j]; }
                }
            }
            return best < float.MaxValue ? bp : Vector2.zero;
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

        static void RemoveRoot(Transform parent)
        {
            var t = parent.Find(RootName);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }
    }
}
#endif
