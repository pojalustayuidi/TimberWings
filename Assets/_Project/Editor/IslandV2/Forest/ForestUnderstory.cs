#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestUnderstory.cs (Editor)
// A5: подлесок в стиле референсов — круглые кусты (лаймовые, тёмно-бирюзовые, цветущие), кустики цветов (белые, жёлтые, сиреневые, розовые)
// и сиреневатые гранёные камни. Без коллайдеров, поэтому проходимость и IslandCheck не меняются.
// Меню: Tools/BuilderSim/Лес/Красота леса/2. Подлесок: кусты, цветы, камни (231) и «Убрать подлесок» (232).
//
// Как расставляет (детерминировано, по seed):
//   - реальные деревья берёт из сцены (все ResourceItem; если лес ещё не сгенерирован, берёт заглушки IslandField.TreePoints());
//     кусты чаще на опушке и в лесу, цветы на полянах (meadows раскладки), вдоль троп и у костра, камни у воды и у обрыва;
//   - не ставит на воду, тропы, площадки зданий, крутые склоны, ближе 1.8 м к краю, вплотную к стволам.
//   - всё склеивается в куски по сетке 28 м: на кусок и вид один меш и один рендерер (десятки draw call, не сотни).
// Результат: IslandV2/_Manual/Understory (генератор блокаута и леса его не трогает; пункт 231 пересоздаёт его целиком).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class ForestUnderstory
    {
        const string RootName = "Understory";
        const float Cell = 28f;
        const int Seed = 5151;

        struct Inst { public Mesh mesh; public Matrix4x4 m; }

        sealed class Kind
        {
            public string name; public Mesh[] meshes; public Material[] mats; public bool shadows;
            public readonly List<Vector3> pts = new List<Vector3>();
            public readonly Dictionary<long, List<Vector2>> hash = new Dictionary<long, List<Vector2>>();
        }

        // ============================================================ меши
        static Mesh BushMesh(int variant, int seed)
        {
            var b = new TreeMB();
            b.Tube(0, new[] { new Vector3(0f, -0.05f, 0f), new Vector3(0.04f, 0.22f, 0.02f) }, new[] { 0.05f, 0.04f }, 4, false);   // веточка у земли, чтобы сабмеш 0 не был пустым
            float k = 0.85f + 0.3f * TreeMB.H(seed, 1);
            b.Blob(1, new Vector3(0f, 0.42f, 0f), new Vector3(0.85f, 0.62f, 0.85f) * k, seed, 1, 0.30f, 2);
            b.Blob(1, new Vector3(0.58f, 0.30f, 0.25f), new Vector3(0.50f, 0.40f, 0.50f) * k, seed + 1, 1, 0.30f, 2);
            b.Blob(1, new Vector3(-0.48f, 0.28f, -0.32f), new Vector3(0.45f, 0.36f, 0.45f) * k, seed + 2, 1, 0.30f, 2);
            if (variant == 2)   // цветущий: розовые бусины по поверхности (сабмеш 3)
            {
                for (int i = 0; i < 9; i++)
                {
                    float a = TreeMB.H(seed + 7, i) * Mathf.PI * 2f;
                    float up = 0.35f + 0.55f * TreeMB.H(seed + 9, i);
                    float r = 0.78f * Mathf.Sqrt(1f - up * up) * k;
                    b.Blob(3, new Vector3(Mathf.Cos(a) * r, 0.18f + up * 0.62f * k, Mathf.Sin(a) * r), new Vector3(0.10f, 0.08f, 0.10f), seed + 20 + i, 0, 0.3f, -1);
                }
            }
            return b.Build("Bush_" + variant + "_" + seed);
        }

        static Mesh TuftMesh(int seed)
        {
            var b = new TreeMB();
            for (int i = 0; i < 3; i++)   // листики
            {
                float a = i * 2.1f + TreeMB.H(seed, i) * 1f;
                b.Tier(new Vector3(Mathf.Cos(a) * 0.18f, 0f, Mathf.Sin(a) * 0.18f), 0.22f, 0.20f, 6, 0f, 0f, seed + i, false, false, 0, 0, 0);
            }
            for (int i = 0; i < 6; i++)   // стебельки с бутонами
            {
                float a = TreeMB.H(seed + 3, i) * Mathf.PI * 2f;
                float r = 0.08f + 0.40f * TreeMB.H(seed + 5, i);
                float h = 0.26f + 0.20f * TreeMB.H(seed + 7, i);
                var p0 = new Vector3(Mathf.Cos(a) * r, -0.02f, Mathf.Sin(a) * r);
                var p1 = p0 + new Vector3(0.03f, h, 0.02f);
                b.Tube(0, new[] { p0, p1 }, new[] { 0.018f, 0.012f }, 3, false);
                b.Blob(1, p1 + Vector3.up * 0.04f, new Vector3(0.10f, 0.075f, 0.10f), seed + 11 + i, 0, 0.3f, -1);
            }
            return b.Build("Tuft_" + seed);
        }

        static Mesh RockMesh(int variant, int seed)
        {
            var b = new TreeMB();
            if (variant == 0) b.Blob(0, new Vector3(0f, 0.20f, 0f), new Vector3(0.80f, 0.52f, 0.70f), seed, 0, 0.55f, -1);
            else
            {
                b.Blob(0, new Vector3(0f, 0.20f, 0f), new Vector3(0.70f, 0.48f, 0.62f), seed, 0, 0.55f, -1);
                b.Blob(0, new Vector3(0.78f, 0.12f, 0.22f), new Vector3(0.44f, 0.30f, 0.40f), seed + 1, 0, 0.55f, -1);
                b.Blob(0, new Vector3(-0.30f, 0.10f, 0.66f), new Vector3(0.32f, 0.22f, 0.30f), seed + 2, 0, 0.55f, -1);
            }
            return b.Build("Rock_" + variant + "_" + seed);
        }

        // ============================================================ склейка
        static Mesh Combine(List<Inst> list, string name)
        {
            int subs = list[0].mesh.subMeshCount;
            var parts = new CombineInstance[subs];
            var temps = new List<Mesh>();
            for (int s = 0; s < subs; s++)
            {
                var cis = new CombineInstance[list.Count];
                for (int i = 0; i < list.Count; i++) cis[i] = new CombineInstance { mesh = list[i].mesh, subMeshIndex = s, transform = list[i].m };
                var tmp = new Mesh { indexFormat = IndexFormat.UInt32 };
                tmp.CombineMeshes(cis, true, true);
                temps.Add(tmp);
                parts[s] = new CombineInstance { mesh = tmp, subMeshIndex = 0, transform = Matrix4x4.identity };
            }
            var res = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            res.CombineMeshes(parts, false, false);
            for (int i = 0; i < temps.Count; i++) Object.DestroyImmediate(temps[i]);
            res.RecalculateBounds();
            return res;
        }

        // ============================================================ меню
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/2. Подлесок: кусты, цветы, камни", false, 231)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Подлесок: останови Play Mode."); return; }
            var rootGo = GameObject.Find("IslandV2");
            if (rootGo == null) { Debug.LogError("Подлесок: в сцене нет корня IslandV2 (собери блокаут: пункт «2. Собрать блокаут»)."); return; }
            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (L == null) { Debug.LogError("Подлесок: нет раскладки " + IslandLayout.AssetPath); return; }

            var bark = TreeToonSetup.Library("Tree_Bark_Oak");
            if (bark == null) { Debug.LogError("Подлесок: нет шейдера TreeToon / материала коры."); return; }
            var f = new IslandField(L);
            ChopZone.PrepareAvoid();   // зона рубки озеленена отдельно, вокруг деревьев-хранителей нужна чистая полянка

            // ---- виды и материалы
            var lime = ForestMats.BushLime(false); var dark = ForestMats.BushDark(false); var under = ForestMats.BushUnder(false);
            var pink = ForestMats.FlowerPink(false); var white = ForestMats.FlowerWhite(false); var yellow = ForestMats.FlowerYellow(false); var purple = ForestMats.FlowerPurple(false);
            var stem = ForestMats.Stem(false); var rock = ForestMats.Rock(false);

            var bushes = new[]
            {
                new Kind { name = "Bush_Lime",    shadows = true, meshes = new[] { BushMesh(0, 11), BushMesh(0, 12), BushMesh(0, 13) }, mats = new[] { bark, lime, under } },
                new Kind { name = "Bush_Dark",    shadows = true, meshes = new[] { BushMesh(1, 21), BushMesh(1, 22) },                   mats = new[] { bark, dark, under } },
                new Kind { name = "Bush_Flower",  shadows = true, meshes = new[] { BushMesh(2, 31), BushMesh(2, 32) },                   mats = new[] { bark, lime, under, pink } }
            };
            var flowers = new[]
            {
                new Kind { name = "Tuft_White",  shadows = false, meshes = new[] { TuftMesh(41), TuftMesh(42) }, mats = new[] { stem, white } },
                new Kind { name = "Tuft_Yellow", shadows = false, meshes = new[] { TuftMesh(43), TuftMesh(44) }, mats = new[] { stem, yellow } },
                new Kind { name = "Tuft_Purple", shadows = false, meshes = new[] { TuftMesh(45), TuftMesh(46) }, mats = new[] { stem, purple } },
                new Kind { name = "Tuft_Pink",   shadows = false, meshes = new[] { TuftMesh(47), TuftMesh(48) }, mats = new[] { stem, pink } }
            };
            var rocks = new[] { new Kind { name = "Rock", shadows = true, meshes = new[] { RockMesh(0, 51), RockMesh(0, 52), RockMesh(1, 53) }, mats = new[] { rock } } };

            // ---- деревья сцены в хеш 2 м
            var trees = new List<Vector2>();
            foreach (var ri in Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                trees.Add(new Vector2(ri.transform.position.x, ri.transform.position.z));
            string treeSrc = "деревьев в сцене " + trees.Count;
            if (trees.Count < 20) { trees = f.TreePoints(); treeSrc = "лес ещё не сгенерирован, взял заглушки раскладки (" + trees.Count + ")"; }
            var th = new Dictionary<long, List<Vector2>>();
            foreach (var t in trees) AddHash(th, t, 2f);

            var rng = new System.Random(Seed);
            var sb = new StringBuilder("Подлесок (A5): " + treeSrc + "\n");

            // ---- кусты
            int nb = Scatter(f, L, rng, th, 2.4f, 2.1f, 0.9f, 260, bushes, (d, inMeadow, pathEdge, wd, ed) =>
            {
                float p = 0.05f + 0.34f * Mathf.Sqrt(d) + (pathEdge ? 0.07f : 0f);
                return inMeadow ? p * 0.2f : p;
            });
            // ---- цветы
            int nf = Scatter(f, L, rng, th, 1.7f, 0.9f, 0.5f, 520, flowers, (d, inMeadow, pathEdge, wd, ed) =>
            {
                if (d > 0.85f) return 0f;
                float p = 0.05f * (1f - d);
                if (inMeadow) p += 0.55f;
                if (pathEdge) p += 0.28f * (1f - d);
                return p;
            });
            // ---- камни
            int nr = Scatter(f, L, rng, th, 3f, 5f, 1.0f, 80, rocks, (d, inMeadow, pathEdge, wd, ed) =>
            {
                float p = 0.02f + 0.02f * d;
                if (wd >= 0.2f && wd <= 3.5f) p += 0.35f;
                if (ed < 5.5f) p += 0.16f;
                return inMeadow ? p * 0.3f : p;
            }, true);

            // ---- сцена: пересоздать корень
            var parentGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = parentGo != null ? parentGo.transform : rootGo.transform;
            var oldRoot = parent.Find(RootName);
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot.gameObject);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Подлесок");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity; root.transform.localScale = Vector3.one;
            Matrix4x4 toLocal = root.transform.worldToLocalMatrix;

            var rr = new System.Random(Seed + 1);
            int tris = 0, chunks = 0;
            var all = new List<Kind>(); all.AddRange(bushes); all.AddRange(flowers); all.AddRange(rocks);
            foreach (var k in all)
            {
                var cells = new Dictionary<long, List<Inst>>();
                for (int i = 0; i < k.pts.Count; i++)
                {
                    Vector3 p = k.pts[i];
                    var mesh = k.meshes[rr.Next(k.meshes.Length)];
                    float sc = k == rocks[0] ? 0.6f + 1.0f * (float)rr.NextDouble() : 0.8f + 0.55f * (float)rr.NextDouble();
                    var m = toLocal * Matrix4x4.TRS(p, Quaternion.Euler(0f, (float)rr.NextDouble() * 360f, 0f), new Vector3(sc, sc, sc));
                    long key = ((long)Mathf.FloorToInt(p.x / Cell) << 32) ^ (uint)Mathf.FloorToInt(p.z / Cell);
                    List<Inst> l; if (!cells.TryGetValue(key, out l)) { l = new List<Inst>(); cells[key] = l; }
                    l.Add(new Inst { mesh = mesh, m = m });
                }
                foreach (var kv in cells)
                {
                    var combined = Combine(kv.Value, k.name + "_chunk");
                    var go = new GameObject(k.name + "_" + chunks);
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = combined;
                    var mr = go.AddComponent<MeshRenderer>();
                    int subs = combined.subMeshCount;
                    var arr = new Material[subs];
                    for (int s = 0; s < subs; s++) arr[s] = k.mats[Mathf.Min(s, k.mats.Length - 1)];
                    mr.sharedMaterials = arr;
                    mr.shadowCastingMode = k.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    mr.receiveShadows = true;
                    for (int s = 0; s < subs; s++) tris += (int)combined.GetIndexCount(s) / 3;
                    chunks++;
                }
            }
            foreach (var k in all) foreach (var m in k.meshes) Object.DestroyImmediate(m);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);

            sb.AppendLine("  кустов " + nb + ", кустиков цветов " + nf + ", камней " + nr + "; кусков-мешей " + chunks + ", треугольников " + tris);
            sb.AppendLine("  лежит в " + parent.name + "/" + RootName + ", коллайдеров нет. Пересоздать: пункт 2 ещё раз; убрать: пункт 3.");
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/3. Убрать подлесок", false, 232)]
        static void Clear()
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == RootName && t.parent != null && t.parent.name == "_Manual") { Undo.DestroyObjectImmediate(t.gameObject); n++; }
            Debug.Log("Подлесок: убрано корней " + n);
        }

        // ============================================================ расстановка
        delegate float Prob(float treeDensity, bool inMeadow, bool pathEdge, float waterDist, float edgeDist);

        static long HKey(Vector2 p, float cs) { return ((long)Mathf.FloorToInt(p.x / cs) << 32) ^ (uint)Mathf.FloorToInt(p.y / cs); }
        static void AddHash(Dictionary<long, List<Vector2>> h, Vector2 p, float cs)
        {
            long k = HKey(p, cs); List<Vector2> l;
            if (!h.TryGetValue(k, out l)) { l = new List<Vector2>(); h[k] = l; }
            l.Add(p);
        }
        static bool AnyWithin(Dictionary<long, List<Vector2>> h, Vector2 p, float cs, float r)
        {
            int c = Mathf.CeilToInt(r / cs);
            int cx = Mathf.FloorToInt(p.x / cs), cz = Mathf.FloorToInt(p.y / cs);
            for (int dx = -c; dx <= c; dx++)
                for (int dz = -c; dz <= c; dz++)
                {
                    List<Vector2> l;
                    if (!h.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out l)) continue;
                    for (int i = 0; i < l.Count; i++) if ((l[i] - p).sqrMagnitude < r * r) return true;
                }
            return false;
        }
        static int CountWithin(Dictionary<long, List<Vector2>> h, Vector2 p, float cs, float r)
        {
            int c = Mathf.CeilToInt(r / cs), n = 0;
            int cx = Mathf.FloorToInt(p.x / cs), cz = Mathf.FloorToInt(p.y / cs);
            for (int dx = -c; dx <= c; dx++)
                for (int dz = -c; dz <= c; dz++)
                {
                    List<Vector2> l;
                    if (!h.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out l)) continue;
                    for (int i = 0; i < l.Count; i++) if ((l[i] - p).sqrMagnitude < r * r) n++;
                }
            return n;
        }

        // kinds: виды одной группы (кусты / цветы / камни); вид точки выбирается жребием, минимальное расстояние между точками группы = minGap
        static int Scatter(IslandField f, IslandLayout L, System.Random rng, Dictionary<long, List<Vector2>> trees, float step, float minGap, float treeClear, int cap,
                           Kind[] kinds, Prob prob, bool allowNearWater = false)
        {
            var groupHash = new Dictionary<long, List<Vector2>>();
            int placed = 0;
            for (float gx = f.Min.x; gx <= f.Max.x && placed < cap; gx += step)
            {
                for (float gz = f.Min.y; gz <= f.Max.y && placed < cap; gz += step)
                {
                    float x = gx + ((float)rng.NextDouble() - 0.5f) * step * 0.9f;
                    float z = gz + ((float)rng.NextDouble() - 0.5f) * step * 0.9f;
                    float roll = (float)rng.NextDouble();       // кидаем всегда: набор случайных чисел не зависит от отбора
                    int pick = rng.Next(kinds.Length);
                    if (!f.Inside(x, z)) continue;
                    float ed = f.EdgeDistance(x, z);
                    if (ed < 1.8f) continue;
                    float wd = f.WaterDistance(x, z);
                    if (wd < (allowNearWater ? 0.2f : 0.5f)) continue;
                    if (f.IsFord(x, z)) continue;
                    float pd = f.PathDistance(x, z);
                    if (pd < 0.5f) continue;
                    Vector2 p2 = new Vector2(x, z);
                    if (OnBuilding(f, L, x, z)) continue;
                    if (ChopZone.Avoid(x, z)) continue;

                    float d = Mathf.Clamp01(CountWithin(trees, p2, 2f, 6f) / 8f);
                    bool inMeadow = false;
                    for (int i = 0; i < L.meadows.Count; i++)
                        if (Vector2.Distance(L.meadows[i].pos, p2) < L.meadows[i].radius * 1.15f) { inMeadow = true; break; }
                    bool pathEdge = pd < 2.6f;
                    if (roll > prob(d, inMeadow, pathEdge, wd, ed)) continue;

                    if (AnyWithin(trees, p2, 2f, treeClear)) continue;
                    if (AnyWithin(groupHash, p2, 2f, minGap)) continue;
                    if (f.Slope(x, z) > 28f) continue;

                    kinds[pick].pts.Add(new Vector3(x, f.Height(x, z), z));
                    AddHash(groupHash, p2, 2f);
                    placed++;
                }
            }
            return placed;
        }

        static bool OnBuilding(IslandField f, IslandLayout L, float x, float z)
        {
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.kind == IslandLayout.AnchorKind.Building) { if (f.RectDistance(a, x, z) < 1.4f) return true; }
                else if (a.name == "CAMPFIRE_Point" && Vector2.Distance(f.AnchorWorld(a), new Vector2(x, z)) < 3.5f) return true;   // каменный круг костра
            }
            return false;
        }
    }
}
#endif
