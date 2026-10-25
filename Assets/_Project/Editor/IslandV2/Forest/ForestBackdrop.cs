#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestBackdrop.cs (Editor)
// A5 v5 (большой рубимый лес вместо фоновой стены). Фоновых нерубимых деревьев Decor_* больше нет: этот пункт их не создаёт и убирает старый корень ForestDecor.
//   Рубимые деревья (Chop_*) обычные префабы Tree_* с ResourceItem: рубятся, дают брёвна, респаунятся, размер Young/Mature/Old задан forcedSize.
//     1. Поляна PAD_GroveSouth: ~26 деревьев вокруг метки (как раньше), остаётся рабочей полянкой.
//     2. Весь остальной лес: рощи с полянами и проходами, плотнее к чаще (ForestZone.Weight/ForestWeight), у троп и построек пореже (ForestGroves.cs).
//     3. Зона рубки PAD_Forest целиком отдана ChopZone.cs (забор, арки, ZoneTree_*): её собирает этот пункт в конце.
//   Нерубимые деревья теперь только «хранители»: большие (ForestSacred.cs, пункт 9) и лёгкие кулисы по краю и вдоль троп (ForestGuardLite.cs, пункт 13).
// Правило отличия для игрока: рубимые деревья без кольца с рунами и без парящего кристалла, нерубимые с ними (у мелких лёгких кристалл и пастельная крона). Подсветка и «Нажми E» есть только у рабочих.
// A5 v6: нерубимый фон теперь «кулисные деревья» (ForestWings.cs): 7 пастельных видов с венком из камней у корней, кластерами по 1-3 дерева, пятнами «настроения»;
//   краевая кулиса, участки между тропами и кольцо вокруг зоны рубки идут из них. Они встают ПЕРЕД рощами рубимых деревьев (рощи обходят их венки).
//   Старых Decor_* (кедры, дубы) по-прежнему нет. Отдельно кулисы пересоздаёт «13. Кулисные деревья: сгенерировать».
// Меню: Tools/BuilderSim/Лес/Красота леса/4. Лес: расставить рабочие деревья (233), 5. Убрать этот лес (234).
// Результат лежит в IslandV2/_Manual/ForestChop; пункт 4 пересоздаёт корень целиком (Ctrl+Z откатывает). Корень ForestDecor от старых версий удаляется.
// Руками можно всё двигать и удалять. Если хочешь оставить свои правки, перед повторным запуском перетащи нужное из корня в другое место.
// Повторный запуск «4. Лес» отдельно безопасен для хранителей: рощи обходят полянки уже стоящих хранителей и лёгких хранителей (читают их из сцены).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IslandV2
{
    public static class ForestBackdrop
    {
        const string SrcDir = "Assets/_Project/Prefabs/Trees/Generated";
        const string ChopRoot = "ForestChop";
        const string LegacyDecorRoot = "ForestDecor";   // корень старых фоновых Decor_*: только удаляем
        const int Seed = 8801;

        static readonly string[] GladeMarkers = { "PAD_Forest", "PAD_GroveSouth" };
        // радиус и число рабочих деревьев по полянам, в порядке GladeMarkers. PAD_Forest целиком отдан зоне рубки (ChopZone.cs): рабочих деревьев тут 0
        const float ZoneNoTreeR = 1f;          // запас за ChopZone.ClearR, где рощи не растут: у забора остаётся проход
        const float LandmarkNoTreeR = 9f;      // у PAD_Forest (маленькая достопримечательность) рощи не растут: там ровно LandmarkTrees рабочих деревьев
        const int LandmarkTrees = 6;           // PAD_Forest: 5-6 деревьев
        static readonly float[] GladeRs = { 8f, ChopZone.ClearR + 3f };
        static readonly int[] GladeCount = { LandmarkTrees, 0 };   // PAD_Forest 6, PAD_GroveSouth 0 (её деревья ставит ChopZone)
        const float GladeStand = 3.2f, ChopGap = ForestGroves.TreeGap;

        // ============================================================ меню
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/4. Лес: расставить рабочие деревья", false, 233)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Лес: останови Play Mode."); return; }
            var rootGo = GameObject.Find("IslandV2");
            if (rootGo == null) { Debug.LogError("Лес: в сцене нет IslandV2 (пункт «2. Собрать блокаут»)."); return; }
            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (L == null) { Debug.LogError("Лес: нет раскладки " + IslandLayout.AssetPath); return; }
            var f = new IslandField(L);

            // ---- исходные префабы
            var chopSp = new Dictionary<string, List<GameObject>>();
            foreach (var key in new[] { "Cedar", "Birch", "Autumn", "Random", "Oak" }) chopSp[key] = LoadSpecies(key);
            foreach (var kv in chopSp)
                if (kv.Value.Count == 0) { Debug.LogError("Лес: нет префабов Tree_" + kv.Key + "_* в " + SrcDir + ". Сначала Tools/BuilderSim/Лес/Деревья/Build Species."); return; }

            // ---- корни
            var parentGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = parentGo != null ? parentGo.transform : rootGo.transform;
            Remove(parent, ChopRoot); Remove(parent, LegacyDecorRoot); Remove(parent, ForestWings.RootName);
            var chopRoot = NewRoot(parent, ChopRoot);

            var rng = new System.Random(Seed);
            var all = new Dictionary<long, List<Vector2>>();      // все стволы, хеш 3 м
            var sb = new StringBuilder("Лес (A5): ");

            // ---- ограничители: поляны рубки
            var glades = new List<Vector2>();
            var noGo = new List<Vector3>();                      // (x, z, радиус): сюда рощи не заходят
            foreach (var n in GladeMarkers)
            {
                var a = L.FindAnchor(n);
                if (a == null) { sb.Append("[нет якоря " + n + "] "); continue; }
                var w = f.AnchorWorld(a);
                glades.Add(w);
                noGo.Add(new Vector3(w.x, w.y, n == ChopZone.PadName ? ChopZone.ClearR + ZoneNoTreeR : LandmarkNoTreeR));
            }

            // ============================================================ рабочие деревья на полянах (PAD_GroveSouth)
            int nChop = 0;
            for (int g = 0; g < glades.Count; g++)
            {
                Vector2 c = glades[g];
                bool grove = GladeMarkers[g] == ChopZone.PadName;
                int target = GladeCount[Mathf.Min(g, GladeCount.Length - 1)], placed = 0;
                float gladeR = GladeRs[Mathf.Min(g, GladeRs.Length - 1)];
                for (int tries = 0; tries < 2000 && placed < target; tries++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float rad = Mathf.Lerp(GladeStand, gladeR, Mathf.Sqrt((float)rng.NextDouble()));
                    float x = c.x + Mathf.Cos(ang) * rad, z = c.y + Mathf.Sin(ang) * rad;
                    double roll = rng.NextDouble();
                    if (!Ok(f, L, x, z, 2.5f, 2.0f, 1.8f)) continue;
                    if (Near(all, new Vector2(x, z), ChopGap)) continue;
                    string key = grove ? (roll < 0.6 ? "Birch" : roll < 0.85 ? "Random" : "Autumn")
                                       : (roll < 0.45 ? "Random" : roll < 0.7 ? "Birch" : roll < 0.85 ? "Autumn" : "Cedar");
                    var list = chopSp[key];
                    var prefab = list[rng.Next(list.Count)];
                    SpawnTree(prefab, chopRoot, f, x, z, rng, "Chop_" + nChop);
                    Add(all, new Vector2(x, z));
                    placed++; nChop++;
                }
            }

            // ============================================================ кулисные нерубимые деревья (ForestWings.cs): до рощ, чтобы рощи их обходили
            // (дальше хранители и лёгкие хранители в «0. Всё сразу» сами уберут кулисы, задетые их кольцом)
            var wings = ForestWings.Generate(f, L, parent, all, noGo);
            if (wings != null) noGo.AddRange(wings.keep);   // венки кулис: сюда рубимые рощи не ставят

            // ============================================================ большой лес: рощи, поляны, проходы (ForestGroves.cs)
            var groves = ForestGroves.Place(f, L, chopRoot, chopSp, all, noGo, nChop);

            // зона рубки у PAD_Forest: забор, рабочие деревья группами, озеленение (ChopZone.cs). Рабочие деревья этой поляны живут только там
            string zoneReport = ChopZone.Build();
            sb.AppendLine(zoneReport != null ? zoneReport : "[!] зона рубки не собралась (причина в Console выше). Запусти «11. Зона рубки» отдельно.");

            EditorSceneManager.MarkSceneDirty(chopRoot.gameObject.scene);
            sb.AppendLine("рабочих (рубятся): на полянах " + nChop + " + рощи " + groves.trees + " = " + (nChop + groves.trees) + " (без деревьев зоны рубки). Фоновых нерубимых Decor_* больше нет.");
            if (wings != null) sb.Append(wings.report); else sb.AppendLine("[!] кулисные деревья не собрались (причина в Console выше). Запусти «13. Кулисные деревья: сгенерировать» отдельно.");
            sb.Append(groves.report);
            sb.AppendLine("  Рабочие: IslandV2/_Manual/" + ChopRoot + ". Двигай и удаляй руками.");
            sb.AppendLine("  Дальше: «13. Лёгкие хранители: кулисы» (крупные нерубимые акценты с кольцом и кристаллом; в «0. Всё сразу» идёт сам), потом «3. Проверить блокаут» и «16. Проверка леса».");
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/5. Убрать этот лес (рабочие деревья)", false, 234)]
        static void Clear()
        {
            var parentGo = GameObject.Find("IslandV2/_Manual");
            if (parentGo == null) { Debug.LogWarning("Лес: нет IslandV2/_Manual."); return; }
            Remove(parentGo.transform, ChopRoot); Remove(parentGo.transform, LegacyDecorRoot); Remove(parentGo.transform, ForestWings.RootName);
            Debug.Log("Лес: корень ForestChop (и кулисы ForestWings, и старый ForestDecor, если были) убран.");
        }

        // ============================================================ префабы
        static List<GameObject> LoadSpecies(string key)
        {
            var res = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("Tree_" + key + "_ t:Prefab", new[] { SrcDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string nm = System.IO.Path.GetFileNameWithoutExtension(p);
                if (!nm.StartsWith("Tree_" + key + "_")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) res.Add(go);
            }
            res.Sort((a, b) => string.CompareOrdinal(a.name, b.name));   // порядок стабилен: тот же seed = тот же лес
            return res;
        }

        // ============================================================ сцена
        static Transform NewRoot(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static void Remove(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        // рабочее дерево: размер задаёт ResourceItem (по seed позиции или forcedSize), масштаб корня не трогаем
        internal static GameObject SpawnTree(GameObject prefab, Transform parent, IslandField f, float x, float z, System.Random rng, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = new Vector3(x, f.Height(x, z), z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            return go;
        }

        // ============================================================ правила
        // есть ли тут место: в острове, не у воды, не на тропе, не на здании или метке, не на поляне без леса
        internal static bool Ok(IslandField f, IslandLayout L, float x, float z, float edgeMin, float waterMin, float pathMin)
        {
            if (!f.Inside(x, z) || f.EdgeDistance(x, z) < edgeMin) return false;
            if (f.WaterDistance(x, z) < waterMin || f.IsFord(x, z)) return false;
            if (f.PathDistance(x, z) < pathMin) return false;
            if (f.Slope(x, z) > 30f) return false;
            var p = new Vector2(x, z);
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.kind == IslandLayout.AnchorKind.Building) { if (f.RectDistance(a, x, z) < 4f) return false; }
                else
                {
                    float clear = a.name == "PAD_Grandpa" ? 9f : (a.name.StartsWith("PAD_Forest") || a.name == "PAD_GroveSouth") ? GladeStand : 3.5f;
                    if (Vector2.Distance(f.AnchorWorld(a), p) < clear) return false;
                }
            }
            for (int i = 0; i < L.meadows.Count; i++)
                if (Vector2.Distance(L.meadows[i].pos, p) < L.meadows[i].radius) return false;
            return true;
        }

        static long K(int a, int b) { return ((long)a << 32) ^ (uint)b; }
        internal static void AddTrunk(Dictionary<long, List<Vector2>> h, Vector2 p) { Add(h, p); }
        static void Add(Dictionary<long, List<Vector2>> h, Vector2 p)
        {
            long k = K(Mathf.FloorToInt(p.x / 3f), Mathf.FloorToInt(p.y / 3f)); List<Vector2> l;
            if (!h.TryGetValue(k, out l)) { l = new List<Vector2>(); h[k] = l; }
            l.Add(p);
        }
        internal static bool Near(Dictionary<long, List<Vector2>> h, Vector2 p, float r)
        {
            int cx = Mathf.FloorToInt(p.x / 3f), cz = Mathf.FloorToInt(p.y / 3f), c = Mathf.CeilToInt(r / 3f);
            for (int dx = -c; dx <= c; dx++)
                for (int dz = -c; dz <= c; dz++)
                {
                    List<Vector2> l;
                    if (!h.TryGetValue(K(cx + dx, cz + dz), out l)) continue;
                    for (int i = 0; i < l.Count; i++) if ((l[i] - p).sqrMagnitude < r * r) return true;
                }
            return false;
        }
    }
}
#endif
