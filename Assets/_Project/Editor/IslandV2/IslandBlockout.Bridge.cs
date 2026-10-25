// Assets/_Project/Editor/IslandV2/IslandBlockout.Bridge.cs
// Остров v2: мост через реку (задача «2. мост с освещением»). Часть partial-класса IslandBlockout. Зовётся из IslandBlockout.Build после воды.
// Это ПОЧИНЕННЫЙ деревянный мост, как на референсе: настил из досок с лёгкой аркой, лаги, две опоры в воде, перила с верёвкой, столбики.
// Свет: 6 фонарей на высоких столбиках (по краям и в середине), гирлянда лампочек вдоль перил. Всё на твоей системе огней FireSource/FireLightPool:
//   имитация (emissive + glow) горит у всех фонарей сразу, настоящий Light получают только ближайшие к игроку (бюджет пула 3 из 4), так что мост ничего не стоит по кадрам.
//
// Где стоит: на броде (IslandLayout.crossings[0]), поперёк реки. Ось моста перпендикулярна оси реки. Концы подбираются сами: там, где берег стал ровным.
// Что в сцене (под _Generated):
//   River_Bridge
//     Bridge_Mesh        один меш на общем материале острова (вершинный цвет), 1 draw call
//     Bridge_Colliders   настил (боксы по дуге) и невидимые бортики вдоль перил: упасть с моста нельзя
//     Bridge_Lamps       Lamp_0..5: FireSource + стеклянный меш (материал Mat_BridgeLampGlass)
//     Bridge_Garland     один FireSource только для имитации (glowPoints по перилам)
// Брод под мостом остаётся (глубина русла вокруг нет). Если нужно, чтобы мост был единственным переходом, это отдельная правка поля (скажи).
// Меню 129: пересобрать только мост, не трогая остальной блокаут.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static partial class IslandBlockout
    {
        public const string BridgeName = "River_Bridge";
        public const string LampGlassPath = IslandLayout.AssetFolder + "/Mat_BridgeLampGlass.mat";

        // числа моста (метры)
        const float BrWidth = 3.2f;       // ширина настила (тропа у брода 3 м)
        const float BrArch = 0.55f;       // подъём настила в середине
        const float BrPitch = 0.56f;      // шаг досок вдоль моста
        const float BrPlankT = 0.14f;     // толщина доски
        const float BrRailH = 1.1f;       // верх перил над настилом
        const float BrPostStep = 3.0f;    // шаг столбиков перил
        const float BrLampH = 2.0f;       // высота столбика с фонарём над настилом

        // ---------------------------------------------------------------- план
        sealed class BridgePlan
        {
            public Vector2 a, d, side;     // начало, направление оси, поперёк
            public float len, yA, yB, arch, w;

            public float Y(float t)
            {
                float u = Mathf.Clamp01(t / len);
                return Mathf.Lerp(yA, yB, u) + 4f * arch * u * (1f - u);
            }
            // точка: t вдоль, o поперёк, dy от верха настила
            public Vector3 P(float t, float o, float dy)
            {
                Vector2 q = a + d * t + side * o;
                return new Vector3(q.x, Y(t) + dy, q.y);
            }
            public Vector3 Dir(float t)
            {
                float u = Mathf.Clamp01(t / len);
                float dy = (yB - yA) / len + 4f * arch * (1f - 2f * u) / len;
                return new Vector3(d.x, dy, d.y).normalized;
            }
            public Vector3 Side3 { get { return new Vector3(side.x, 0f, side.y); } }
        }

        // где берег стал ровным: идём от оси реки наружу и ищем место, где высота почти равна высоте дальше
        static float ScanBank(IslandField f, Vector2 c, Vector2 dir, float half)
        {
            float maxO = half + 16f;
            while (maxO > half + 4f && !f.Inside(c.x + dir.x * maxO, c.y + dir.y * maxO)) maxO -= 1f;
            float hRef = f.Height(c.x + dir.x * maxO, c.y + dir.y * maxO);
            for (float o = half + 1.5f; o <= maxO; o += 0.5f)
            {
                float h = f.Height(c.x + dir.x * o, c.y + dir.y * o);
                if (Mathf.Abs(h - hRef) < 0.15f) return o + 1.2f;
            }
            return Mathf.Min(maxO, half + 10f);
        }

        static BridgePlan PlanBridge(IslandField f)
        {
            var L = f.L;
            if (L.crossings == null || L.crossings.Count == 0) return null;
            var pl = f.RiverPolyline;
            if (pl == null || pl.Length < 2) return null;
            Vector2 c = L.crossings[0].pos;

            int bi = 0; float best = float.MaxValue;
            for (int i = 0; i + 1 < pl.Length; i++)
            {
                Vector2 a0 = pl[i], ab = pl[i + 1] - a0;
                float l2 = ab.sqrMagnitude;
                float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(c - a0, ab) / l2);
                float d2 = (a0 + ab * t - c).sqrMagnitude;
                if (d2 < best) { best = d2; bi = i; }
            }
            Vector2 tan = (pl[bi + 1] - pl[bi]).normalized;
            Vector2 nrm = new Vector2(-tan.y, tan.x);
            float half = L.riverWidth * 0.5f;
            float e = Mathf.Max(ScanBank(f, c, nrm, half), ScanBank(f, c, -nrm, half));
            e = Mathf.Clamp(e, half + 4.5f, half + 12f);

            var p = new BridgePlan();
            p.a = c - nrm * e;
            p.d = nrm;
            p.side = new Vector2(-nrm.y, nrm.x);
            p.len = e * 2f;
            p.w = BrWidth;
            p.arch = BrArch;
            Vector2 b = c + nrm * e;
            p.yA = f.Height(p.a.x, p.a.y) + 0.06f;
            p.yB = f.Height(b.x, b.y) + 0.06f;
            return p;
        }

        // ---------------------------------------------------------------- геометрия
        static Color W(float r, float g, float b) { return new Color(r, g, b, 1f).linear; }

        // коробка по осям right/up/fwd (без дна)
        static void OBox(MeshAcc acc, Vector3 c, Vector3 right, Vector3 up, Vector3 fwd, float sx, float sy, float sz, Color col)
        {
            Vector3 r = right * (sx * 0.5f), u = up * (sy * 0.5f), f = fwd * (sz * 0.5f);
            Color top = new Color(Mathf.Min(1f, col.r * 1.12f), Mathf.Min(1f, col.g * 1.12f), Mathf.Min(1f, col.b * 1.12f), 1f);
            acc.Quad(c + u - r - f, c + u + r - f, c + u + r + f, c + u - r + f, top, c);
            acc.Quad(c - r - u - f, c - r - u + f, c - r + u + f, c - r + u - f, col, c);
            acc.Quad(c + r - u - f, c + r - u + f, c + r + u + f, c + r + u - f, col, c);
            acc.Quad(c - r - u + f, c + r - u + f, c + r + u + f, c - r + u + f, col, c);
            acc.Quad(c - r - u - f, c + r - u - f, c + r + u - f, c - r + u - f, col, c);
        }

        // брус между двумя точками, сечение tx x ty
        static void OBeam(MeshAcc acc, Vector3 a, Vector3 b, float tx, float ty, Color col)
        {
            Vector3 fwd = b - a;
            float len = fwd.magnitude;
            if (len < 0.001f) return;
            fwd /= len;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(fwd, right);
            OBox(acc, (a + b) * 0.5f, right, up, fwd, tx, ty, len, col);
        }

        // вертикальный столбик: низ на yBottom, верх на yTop
        static void Post(MeshAcc acc, Vector3 basePos, float yTop, float size, Color col, Color capCol)
        {
            float h = yTop - basePos.y;
            if (h < 0.05f) return;
            OBox(acc, new Vector3(basePos.x, basePos.y + h * 0.5f, basePos.z), Vector3.right, Vector3.up, Vector3.forward, size, h, size, col);
            OBox(acc, new Vector3(basePos.x, yTop + 0.03f, basePos.z), Vector3.right, Vector3.up, Vector3.forward, size * 1.35f, 0.07f, size * 1.35f, capCol);
        }

        // ---------------------------------------------------------------- сборка
        public static int BuildRiverBridge(IslandField f, Transform gen, Material mat)
        {
            var plan = PlanBridge(f);
            if (plan == null)
            {
                Debug.LogWarning("IslandV2: мост не построен: в раскладке нет брода (crossings) или реки.");
                return 0;
            }

            var rng = new System.Random(f.L.seed + 77);
            Color wood = W(0.66f, 0.47f, 0.28f), woodNew = W(0.82f, 0.62f, 0.38f), woodDark = W(0.42f, 0.28f, 0.17f);
            Color beam = W(0.50f, 0.34f, 0.20f), rope = W(0.80f, 0.70f, 0.48f), cap = W(0.58f, 0.42f, 0.26f), bulb = W(1.0f, 0.86f, 0.50f);
            Color lampMetal = W(0.28f, 0.24f, 0.30f);

            var acc = new MeshAcc();
            float hw = plan.w * 0.5f;
            Vector3 side3 = plan.Side3;

            // 1. настил: доски поперёк, часть новые (чинили), часть тёмные
            int nPlanks = Mathf.FloorToInt(plan.len / BrPitch);
            for (int i = 0; i < nPlanks; i++)
            {
                float t = (i + 0.5f) * BrPitch;
                Vector3 fwd = plan.Dir(t);
                Vector3 up = Vector3.Cross(fwd, side3);
                if (up.y < 0f) up = -up;
                up.Normalize();
                double r = rng.NextDouble();
                Color col = r < 0.16 ? woodNew : (r < 0.40 ? woodDark : wood);
                float tint = 0.92f + (float)rng.NextDouble() * 0.16f;
                col = new Color(col.r * tint, col.g * tint, col.b * tint, 1f);
                Vector3 c = plan.P(t, 0f, -BrPlankT * 0.5f);
                OBox(acc, c, side3, up, fwd, plan.w, BrPlankT, BrPitch - 0.06f, col);
            }

            // 2. лаги под настилом (по краям и в середине), звенья по метру следуют дуге
            float[] stringerO = { -hw + 0.18f, 0f, hw - 0.18f };
            int segs = Mathf.CeilToInt(plan.len / 1.0f);
            for (int k = 0; k < stringerO.Length; k++)
                for (int s = 0; s < segs; s++)
                {
                    float t0 = plan.len * s / segs, t1 = plan.len * (s + 1) / segs;
                    OBeam(acc, plan.P(t0, stringerO[k], -BrPlankT - 0.10f), plan.P(t1, stringerO[k], -BrPlankT - 0.10f), 0.22f, 0.22f, beam);
                }

            // 3. опоры в воде: две пары свай с поперечной балкой
            float mid = plan.len * 0.5f;
            float[] pierT = { mid - 3.4f, mid + 3.4f };
            for (int pi = 0; pi < pierT.Length; pi++)
            {
                float t = pierT[pi];
                float deckBottom = plan.Y(t) - BrPlankT - 0.22f;
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    Vector3 top = plan.P(t, sd * (hw - 0.3f), 0f);
                    float bed = f.RiverBedY(top.x, top.z) - 0.6f;
                    OBox(acc, new Vector3(top.x, (bed + deckBottom) * 0.5f, top.z), Vector3.right, Vector3.up, Vector3.forward, 0.36f, deckBottom - bed, 0.36f, woodDark);
                }
                OBeam(acc, plan.P(t, -hw - 0.1f, -BrPlankT - 0.30f), plan.P(t, hw + 0.1f, -BrPlankT - 0.30f), 0.30f, 0.26f, beam);
            }

            // 4. перила: столбики, верхняя жердь, провисающая верёвка
            int nPosts = Mathf.Max(2, Mathf.RoundToInt(plan.len / BrPostStep));
            var postT = new float[nPosts + 1];
            for (int k = 0; k <= nPosts; k++) postT[k] = plan.len * k / nPosts;
            var lampIdx = new HashSet<int> { 0, nPosts / 2, nPosts };
            var bulbPts = new List<Vector3>();
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float o = sd * (hw - 0.12f);
                for (int k = 0; k <= nPosts; k++)
                {
                    float t = postT[k];
                    Vector3 b0 = plan.P(t, o, -BrPlankT);
                    bool lamp = lampIdx.Contains(k);
                    Post(acc, b0, plan.Y(t) + (lamp ? BrLampH : BrRailH + 0.12f), lamp ? 0.24f : 0.20f, woodDark, cap);
                    if (k == nPosts) continue;
                    float t2 = postT[k + 1];
                    // верхняя жердь
                    OBeam(acc, plan.P(t, o, BrRailH), plan.P(t2, o, BrRailH), 0.13f, 0.11f, wood);
                    // верёвка: 4 звена с провисом
                    const int ropeSegs = 4;
                    for (int s = 0; s < ropeSegs; s++)
                    {
                        float u0 = (float)s / ropeSegs, u1 = (float)(s + 1) / ropeSegs;
                        Vector3 p0 = plan.P(Mathf.Lerp(t, t2, u0), o, 0.50f - 0.16f * 4f * u0 * (1f - u0));
                        Vector3 p1 = plan.P(Mathf.Lerp(t, t2, u1), o, 0.50f - 0.16f * 4f * u1 * (1f - u1));
                        OBeam(acc, p0, p1, 0.06f, 0.06f, rope);
                    }
                    // лампочка гирлянды посередине пролёта, висит под жердью
                    Vector3 bp = plan.P((t + t2) * 0.5f, o, BrRailH - 0.14f);
                    OBox(acc, bp, Vector3.right, Vector3.up, Vector3.forward, 0.12f, 0.14f, 0.12f, bulb);
                    bulbPts.Add(bp);
                }
            }

            // 5. фонари: оправа (основание и крыша) в общем меше, стекло отдельным объектом
            var lampPos = new List<Vector3>();
            foreach (int k in lampIdx)
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float t = postT[k];
                    float o = sd * (hw - 0.12f);
                    Vector3 top = plan.P(t, o, BrLampH);
                    OBox(acc, top + Vector3.up * 0.10f, Vector3.right, Vector3.up, Vector3.forward, 0.38f, 0.06f, 0.38f, lampMetal);        // основание
                    OBox(acc, top + Vector3.up * 0.62f, Vector3.right, Vector3.up, Vector3.forward, 0.44f, 0.09f, 0.44f, lampMetal);        // крыша
                    OBox(acc, top + Vector3.up * 0.69f, Vector3.right, Vector3.up, Vector3.forward, 0.24f, 0.08f, 0.24f, lampMetal);        // шишечка
                    lampPos.Add(top + Vector3.up * 0.36f);
                }

            // ---- объекты сцены
            var root = new GameObject(BridgeName);
            Undo.RegisterCreatedObjectUndo(root, "Create " + BridgeName);
            root.transform.SetParent(gen, false);

            var meshGo = NewMeshObject("Bridge_Mesh", root.transform, acc.ToMesh("IslandBridge"), mat, true);

            BuildBridgeColliders(plan, root.transform);
            BuildBridgeLamps(root.transform, lampPos, bulbPts);

            int tris = acc.t.Count / 3;
            Debug.Log("IslandV2: мост через реку построен: длина " + plan.len.ToString("0.0") + " м, ширина " + plan.w.ToString("0.0") + " м, Tris " + tris
                      + ", фонарей " + lampPos.Count + ", лампочек гирлянды " + bulbPts.Count + ". Объект " + BridgeName + " в _Generated.");
            return tris;
        }

        // ---------------------------------------------------------------- коллайдеры
        static void BuildBridgeColliders(BridgePlan plan, Transform parent)
        {
            var holder = new GameObject("Bridge_Colliders");
            Undo.RegisterCreatedObjectUndo(holder, "Create Bridge_Colliders");
            holder.transform.SetParent(parent, false);

            float hw = plan.w * 0.5f;
            int segs = Mathf.CeilToInt(plan.len / 2f);
            for (int s = 0; s < segs; s++)
            {
                float t0 = plan.len * s / segs, t1 = plan.len * (s + 1) / segs;
                Vector3 a = plan.P(t0, 0f, 0f), b = plan.P(t1, 0f, 0f);
                Vector3 fwd = (b - a).normalized;
                float segLen = Vector3.Distance(a, b);
                Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
                Vector3 mid = (a + b) * 0.5f;

                // настил: бокс чуть длиннее звена (нет щелей на изломе дуги), верх на уровне досок
                var deck = new GameObject("Deck_" + s);
                deck.transform.SetParent(holder.transform, false);
                deck.transform.position = mid + Vector3.down * 0.18f;
                deck.transform.rotation = rot;
                var bc = deck.AddComponent<BoxCollider>();
                bc.size = new Vector3(plan.w, 0.36f, segLen + 0.12f);

                // бортики: невидимая стенка вдоль перил, чтобы не упасть
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    var rail = new GameObject("Rail_" + (sd < 0 ? "L" : "R") + "_" + s);
                    rail.transform.SetParent(holder.transform, false);
                    rail.transform.position = mid + plan.Side3 * (sd * (hw - 0.12f)) + Vector3.up * 0.65f;
                    rail.transform.rotation = rot;
                    var rc = rail.AddComponent<BoxCollider>();
                    rc.size = new Vector3(0.22f, 1.3f, segLen + 0.12f);
                }
            }
        }

        // ---------------------------------------------------------------- свет
        static Material GetLampGlassMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(LampGlassPath);
            if (m != null) return m;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) return null;
            IslandLayout.EnsureFolder(IslandLayout.AssetFolder);
            m = new Material(sh) { name = "Mat_BridgeLampGlass" };
            var warm = new Color(1f, 0.82f, 0.45f, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", warm);
            if (m.HasProperty("_Color")) m.SetColor("_Color", warm);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.3f, 1f) * 0.6f);   // FireSource потом сам ведёт яркость через MaterialPropertyBlock
            AssetDatabase.CreateAsset(m, LampGlassPath);
            return m;
        }

        static void BuildBridgeLamps(Transform parent, List<Vector3> lampPos, List<Vector3> bulbPts)
        {
            var lamps = new GameObject("Bridge_Lamps");
            Undo.RegisterCreatedObjectUndo(lamps, "Create Bridge_Lamps");
            lamps.transform.SetParent(parent, false);

            var glass = GetLampGlassMaterial();
            var glassAcc = new MeshAcc();
            glassAcc.Box(new Vector3(0f, -0.17f, 0f), new Vector2(0.26f, 0.26f), 0.34f, 0f, Color.white);   // стекло: низ на -0,17, центр в нуле
            var glassMesh = glassAcc.ToMesh("BridgeLampGlass");

            for (int i = 0; i < lampPos.Count; i++)
            {
                var go = new GameObject("Lamp_" + i);
                Undo.RegisterCreatedObjectUndo(go, "Create Lamp");
                go.transform.SetParent(lamps.transform, false);
                go.transform.position = lampPos[i];

                var gl = new GameObject("Glass");
                gl.transform.SetParent(go.transform, false);
                gl.AddComponent<MeshFilter>().sharedMesh = glassMesh;
                var mr = gl.AddComponent<MeshRenderer>();
                if (glass != null) mr.sharedMaterial = glass;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                var fs = go.AddComponent<FireSource>();
                fs.preset = FlickerPreset.Lantern;
                fs.color = new Color(1f, 0.76f, 0.42f);
                fs.intensity = 2.4f;
                fs.range = 6.5f;
                fs.lightOffset = new Vector3(0f, 0.05f, 0f);
                fs.emissive = new Renderer[] { mr };
                fs.emissiveStrength = 2.6f;
                fs.glow = true;
                fs.glowSize = 1.3f;
                fs.glowPoints = new[] { Vector3.zero };
                EditorUtility.SetDirty(fs);
            }

            // гирлянда: один FireSource только для имитации (настоящий свет ей не нужен), свечение в точках лампочек
            var gar = new GameObject("Bridge_Garland");
            Undo.RegisterCreatedObjectUndo(gar, "Create Bridge_Garland");
            gar.transform.SetParent(parent, false);
            var gfs = gar.AddComponent<FireSource>();
            gfs.preset = FlickerPreset.Garland;
            gfs.color = new Color(1f, 0.86f, 0.55f);
            gfs.simOnly = true;
            gfs.glow = true;
            gfs.glowSize = 0.5f;
            var pts = new Vector3[bulbPts.Count];
            for (int i = 0; i < pts.Length; i++) pts[i] = gar.transform.InverseTransformPoint(bulbPts[i]);
            gfs.glowPoints = pts;
            EditorUtility.SetDirty(gfs);
        }

        // ---------------------------------------------------------------- меню 129
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Мост через реку: пересобрать только мост", false, 129)]
        static void MenuRebuildBridge()
        {
            if (Application.isPlaying) { Debug.LogError("IslandV2: мост собирается вне Play Mode."); return; }
            var rootGo = FindRoot();
            var gen = rootGo != null ? rootGo.transform.Find(GeneratedName) : null;
            if (gen == null) { Debug.LogError("IslandV2: нет IslandV2/_Generated. Сначала «2. Собрать блокаут»."); return; }
            var shader = Shader.Find(ShaderName);
            if (shader == null) { Debug.LogError("IslandV2: не найден шейдер " + ShaderName); return; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Остров v2: пересобрать мост");
            int group = Undo.GetCurrentGroup();

            var old = gen.Find(BridgeName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var layout = IslandLayout.GetOrCreate();
            BuildRiverBridge(new IslandField(layout), gen, GetMaterial(shader));

            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rootGo.scene);
        }
    }
}
