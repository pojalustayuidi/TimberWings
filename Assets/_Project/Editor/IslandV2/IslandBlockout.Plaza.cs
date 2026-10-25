// Assets/_Project/Editor/IslandV2/IslandBlockout.Plaza.cs
// Остров v2: центральная площадка лагеря вокруг костра в мультяшном стиле референса. Часть partial-класса IslandBlockout.
// Зовётся из IslandBlockout.Build после моста. Центр = метка CAMPFIRE_Point (IslandField.PlazaCenter), радиус = IslandLayout.plazaRadius.
//
// Что строится (один меш Plaza_Mesh на общем материале острова, 1 draw call; плоская раскраска вершин, как весь остров):
//   1. Мощение: кольца плит-«клиньев» от радиуса 1,5 до края, плиты кремовые, чуть лавандовые и песочные, швы между ними, как на референсе.
//      Центр (радиус < 1,5) остаётся землёй: там стоит костёр, который строит игрок (CampfireBuilder).
//   2. Бордюр: каменные блоки лавандового цвета по краю площадки, часть крупнее (валуны). В местах, где входят тропы, блоков нет: проходы свободны.
//   3. Флагшток с фиолетовым вымпелом и маленьким жёлтым флажком (как на референсе), невысокий коллайдер на столбе.
//   4. Четыре фонаря на столбах по краю и гирлянда лампочек между ними над площадкой (FireSource: огонь имитацией, настоящий Light у ближайших).
//   5. Цветы и кустики травы вокруг бордюра (мелкие, без коллайдеров), не на тропах.
// Что НЕ трогает: сам костёр (его строит и ведёт CampfireBuilder / BuildingController), покраску земли, тропы, коллайдеры земли.
// Площадка постоянная (не зависит от этапов стройки костра). Меню 130: пересобрать только площадку.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static partial class IslandBlockout
    {
        public const string PlazaName = "CampPlaza";

        // ---------------------------------------------------------------- плита-клин
        static void PlazaSlab(MeshAcc acc, IslandField f, Vector2 c, float r0, float r1, float a0, float a1, Color col)
        {
            float am = (a0 + a1) * 0.5f, rm = (r0 + r1) * 0.5f;
            float h = f.Height(c.x + Mathf.Cos(am) * rm, c.y + Mathf.Sin(am) * rm);
            float yT = h + 0.07f, yB = h - 0.28f;
            Vector2[] p = new Vector2[4];
            p[0] = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r0;
            p[1] = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r1;
            p[2] = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1;
            p[3] = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r0;
            Vector3[] t = new Vector3[4], b = new Vector3[4];
            for (int i = 0; i < 4; i++) { t[i] = new Vector3(p[i].x, yT, p[i].y); b[i] = new Vector3(p[i].x, yB, p[i].y); }
            Vector3 inside = new Vector3((p[0].x + p[2].x) * 0.5f, (yT + yB) * 0.5f, (p[0].y + p[2].y) * 0.5f);
            acc.Quad(t[0], t[1], t[2], t[3], col, inside);
            Color side = new Color(col.r * 0.80f, col.g * 0.78f, col.b * 0.80f, 1f);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                acc.Quad(b[i], b[j], t[j], t[i], side, inside);
            }
        }

        static Color Vary(Color c, float amt, System.Random rng)
        {
            float k = 1f + ((float)rng.NextDouble() - 0.5f) * 2f * amt;
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
        }

        static float TangentYaw(float a) { return Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg; }

        // угол, свободный от тропы: если на радиусе r тропа ближе minPath, сдвигаем по 8 градусов
        static float FreeAngle(IslandField f, Vector2 c, float r, float angDeg, float minPath)
        {
            for (int k = 0; k < 12; k++)
            {
                float a = (angDeg + k * 8f) * Mathf.Deg2Rad;
                if (f.PathAxisDistance(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r) >= minPath) return a;
            }
            return angDeg * Mathf.Deg2Rad;
        }

        // ---------------------------------------------------------------- сборка
        public static int BuildCampPlaza(IslandField f, Transform gen, Material mat)
        {
            var L = f.L;
            Vector2 c = f.PlazaCenter;
            float R = Mathf.Max(2.5f, L.plazaRadius);
            var rng = new System.Random(L.seed + 31);
            var acc = new MeshAcc();

            Color cream = W(0.93f, 0.88f, 0.76f), lav = W(0.84f, 0.79f, 0.88f), sand = W(0.88f, 0.78f, 0.60f), inner = W(0.80f, 0.70f, 0.56f);
            Color woodDark = W(0.42f, 0.28f, 0.17f), woodCap = W(0.58f, 0.42f, 0.26f), rope = W(0.80f, 0.70f, 0.48f);
            Color lampMetal = W(0.28f, 0.24f, 0.30f), bulb = W(1.0f, 0.86f, 0.50f);

            // 1. мощение кольцами, кладка «в разбежку»
            float rIn = 1.5f, rOut = R - 0.45f;
            int rings = Mathf.Max(2, Mathf.RoundToInt((rOut - rIn) / 1.05f));
            float ringW = (rOut - rIn) / rings;
            for (int i = 0; i < rings; i++)
            {
                float r0 = rIn + i * ringW, r1 = r0 + ringW, rm = (r0 + r1) * 0.5f;
                int n = Mathf.Max(7, Mathf.RoundToInt(2f * Mathf.PI * rm / 1.25f));
                float off = (i % 2) * 0.5f;
                for (int k = 0; k < n; k++)
                {
                    float a0 = (k + off) * Mathf.PI * 2f / n, a1 = (k + 1 + off) * Mathf.PI * 2f / n;
                    float gap = 0.05f / rm;
                    double r = rng.NextDouble();
                    Color col = i == 0 ? inner : (r < 0.58 ? cream : (r < 0.84 ? lav : sand));
                    PlazaSlab(acc, f, c, r0 + 0.03f, r1 - 0.03f, a0 + gap, a1 - gap, Vary(col, 0.05f, rng));
                }
            }

            // 2. бордюр из камней, проходы у троп свободны
            Color[] rimCols = { W(0.72f, 0.66f, 0.78f), W(0.80f, 0.75f, 0.84f), W(0.66f, 0.62f, 0.74f) };
            int ns = Mathf.RoundToInt(2f * Mathf.PI * R / 0.8f);
            int stones = 0;
            for (int i = 0; i < ns; i++)
            {
                float a = (i + (float)rng.NextDouble() * 0.3f) * Mathf.PI * 2f / ns;
                float rr = R - 0.05f + ((float)rng.NextDouble() - 0.5f) * 0.24f;
                float x = c.x + Mathf.Cos(a) * rr, z = c.y + Mathf.Sin(a) * rr;
                if (f.PathAxisDistance(x, z) < 1.9f) continue;
                bool big = (i % 7) == 3;
                Vector2 size = big ? new Vector2(1.0f + (float)rng.NextDouble() * 0.25f, 0.8f) : new Vector2(0.62f + (float)rng.NextDouble() * 0.2f, 0.5f + (float)rng.NextDouble() * 0.12f);
                float hgt = big ? 0.55f + (float)rng.NextDouble() * 0.15f : 0.26f + (float)rng.NextDouble() * 0.14f;
                float yaw = TangentYaw(a) + ((float)rng.NextDouble() - 0.5f) * 14f;
                float y = f.Height(x, z) - 0.12f;
                acc.Box(new Vector3(x, y, z), size, hgt + 0.12f, yaw, Vary(rimCols[rng.Next(rimCols.Length)], 0.05f, rng));
                stones++;
            }

            // 3. флагшток: фиолетовый вымпел и маленький жёлтый флажок
            float flagA = FreeAngle(f, c, 4.2f, 335f, 2.6f);
            Vector2 fp2 = c + new Vector2(Mathf.Cos(flagA), Mathf.Sin(flagA)) * 4.2f;
            float fy = f.Height(fp2.x, fp2.y);
            Vector3 poleBase = new Vector3(fp2.x, fy - 0.15f, fp2.y);
            Post(acc, poleBase, fy + 4.6f, 0.18f, woodDark, woodCap);
            OBox(acc, new Vector3(fp2.x, fy + 4.78f, fp2.y), Vector3.right, Vector3.up, Vector3.forward, 0.24f, 0.24f, 0.24f, W(0.95f, 0.78f, 0.30f));
            for (int k = 0; k < 4; k++)   // камни у основания
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                acc.Box(new Vector3(fp2.x + Mathf.Cos(a) * 0.38f, fy - 0.1f, fp2.y + Mathf.Sin(a) * 0.38f), new Vector2(0.34f, 0.3f), 0.32f, TangentYaw(a), rimCols[k % rimCols.Length]);
            }
            Vector3 wind = new Vector3(0.95f, 0f, 0.3f).normalized;
            Vector3 pole = new Vector3(fp2.x, fy, fp2.y);
            PlazaPennant(acc, pole + Vector3.up * 4.35f, wind, 1.7f, 0.85f, W(0.62f, 0.32f, 0.84f));
            PlazaPennant(acc, pole + Vector3.up * 4.62f, wind, 0.65f, 0.28f, W(0.98f, 0.82f, 0.30f));

            // 4. фонари по краю и гирлянда между ними
            float[] lampAng = { 48f, 160f, 222f, 310f };
            float lampR = R + 0.35f;
            var lampTop = new List<Vector3>();
            var lampGlass = new List<Vector3>();
            for (int i = 0; i < lampAng.Length; i++)
            {
                float a = FreeAngle(f, c, lampR, lampAng[i], 2.4f);
                float x = c.x + Mathf.Cos(a) * lampR, z = c.y + Mathf.Sin(a) * lampR;
                float y = f.Height(x, z);
                Vector3 b0 = new Vector3(x, y - 0.15f, z);
                float topY = y + 2.25f;
                Post(acc, b0, topY, 0.22f, woodDark, woodCap);
                Vector3 top = new Vector3(x, topY, z);
                OBox(acc, top + Vector3.up * 0.10f, Vector3.right, Vector3.up, Vector3.forward, 0.38f, 0.06f, 0.38f, lampMetal);
                OBox(acc, top + Vector3.up * 0.62f, Vector3.right, Vector3.up, Vector3.forward, 0.44f, 0.09f, 0.44f, lampMetal);
                OBox(acc, top + Vector3.up * 0.69f, Vector3.right, Vector3.up, Vector3.forward, 0.24f, 0.08f, 0.24f, lampMetal);
                lampTop.Add(new Vector3(x, topY - 0.05f, z));
                lampGlass.Add(top + Vector3.up * 0.36f);
            }
            var bulbPts = new List<Vector3>();
            for (int i = 0; i < lampTop.Count; i++)
            {
                Vector3 A = lampTop[i], B = lampTop[(i + 1) % lampTop.Count];
                int m = Mathf.Max(3, Mathf.CeilToInt(Vector3.Distance(A, B) / 1.5f));
                Vector3 prev = A;
                for (int j = 1; j <= m; j++)
                {
                    float t = (float)j / m;
                    Vector3 p = Vector3.Lerp(A, B, t);
                    p.y -= 0.40f * 4f * t * (1f - t);
                    OBeam(acc, prev, p, 0.035f, 0.035f, rope);
                    if (j < m)
                    {
                        OBox(acc, p + Vector3.down * 0.07f, Vector3.right, Vector3.up, Vector3.forward, 0.11f, 0.14f, 0.11f, bulb);
                        bulbPts.Add(p + Vector3.down * 0.07f);
                    }
                    prev = p;
                }
            }

            // 5. цветы и кустики вокруг бордюра
            Color[] flowers = { W(1f, 1f, 0.95f), W(1f, 0.85f, 0.25f), W(0.95f, 0.55f, 0.75f), W(0.65f, 0.55f, 0.95f) };
            Color grass = W(0.36f, 0.68f, 0.24f), grassLight = W(0.56f, 0.80f, 0.28f);
            for (int i = 0; i < 40; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float rr = R + 0.7f + (float)rng.NextDouble() * 1.9f;
                float x = c.x + Mathf.Cos(a) * rr, z = c.y + Mathf.Sin(a) * rr;
                if (!f.Inside(x, z) || f.PathAxisDistance(x, z) < 2.2f || f.IsWater(x, z)) continue;
                float y = f.Height(x, z);
                Vector3 b = new Vector3(x, y - 0.03f, z);
                acc.Cone(b, 0.15f + (float)rng.NextDouble() * 0.08f, 0.28f + (float)rng.NextDouble() * 0.16f, rng.NextDouble() < 0.5 ? grass : grassLight);
                if (rng.NextDouble() < 0.6)
                {
                    float fx = x + ((float)rng.NextDouble() - 0.5f) * 0.3f, fz = z + ((float)rng.NextDouble() - 0.5f) * 0.3f;
                    OBox(acc, new Vector3(fx, y + 0.30f, fz), Vector3.right, Vector3.up, Vector3.forward, 0.10f, 0.10f, 0.10f, flowers[rng.Next(flowers.Length)]);
                }
            }

            // ---- объекты сцены
            var root = new GameObject(PlazaName);
            Undo.RegisterCreatedObjectUndo(root, "Create " + PlazaName);
            root.transform.SetParent(gen, false);
            NewMeshObject("Plaza_Mesh", root.transform, acc.ToMesh("IslandPlaza"), mat, true);

            var flagCol = new GameObject("Plaza_FlagCollider");
            Undo.RegisterCreatedObjectUndo(flagCol, "Create Plaza_FlagCollider");
            flagCol.transform.SetParent(root.transform, false);
            flagCol.transform.position = pole;
            var cap = flagCol.AddComponent<CapsuleCollider>();
            cap.radius = 0.16f; cap.height = 4.4f; cap.center = new Vector3(0f, 2.2f, 0f);

            BuildPlazaLights(root.transform, lampGlass, bulbPts);

            int tris = acc.t.Count / 3;
            Debug.Log("IslandV2: площадка лагеря построена: радиус " + R.ToString("0.0") + " м, Tris " + tris + ", камней бордюра " + stones + ", фонарей " + lampGlass.Count
                      + ", лампочек гирлянды " + bulbPts.Count + ". Объект " + PlazaName + " в _Generated.");
            return tris;
        }

        // вымпел: двусторонний флаг-«ласточкин хвост», слегка изогнут по ветру. base = точка на столбе (верх флага)
        static void PlazaPennant(MeshAcc acc, Vector3 top, Vector3 dir, float length, float height, Color col)
        {
            Vector3 up = Vector3.up;
            Vector3 mid = top + dir * (length * 0.5f) + up * (-height * 0.5f + 0.05f);
            Vector3 p0 = top;                                                  // верх у столба
            Vector3 p1 = top + dir * (length * 0.55f) + up * 0.05f;            // верхняя кромка в середине (приподнята)
            Vector3 p2 = top + dir * length + up * (-0.05f);                   // верхний хвост
            Vector3 p3 = top + dir * (length * 0.78f) + up * (-height * 0.5f); // вырез ласточкиного хвоста
            Vector3 p4 = top + dir * length + up * (-height + 0.05f);          // нижний хвост
            Vector3 p5 = top + dir * (length * 0.5f) + up * (-height - 0.03f); // нижняя кромка в середине
            Vector3 p6 = top + up * (-height);                                 // низ у столба
            Vector3[] poly = { p0, p1, p2, p3, p4, p5, p6 };
            Vector3 normal = Vector3.Cross(dir, up).normalized;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector3 a = poly[i], b = poly[(i + 1) % poly.Length];
                acc.Tri(mid, a, b, col, mid + normal * 0.1f);                  // лицевая сторона
                acc.Tri(mid, a, b, col, mid - normal * 0.1f);                  // обратная
            }
        }

        // ---------------------------------------------------------------- свет площадки
        static void BuildPlazaLights(Transform parent, List<Vector3> lampPos, List<Vector3> bulbPts)
        {
            var lamps = new GameObject("Plaza_Lamps");
            Undo.RegisterCreatedObjectUndo(lamps, "Create Plaza_Lamps");
            lamps.transform.SetParent(parent, false);

            var glass = GetLampGlassMaterial();
            var glassAcc = new MeshAcc();
            glassAcc.Box(new Vector3(0f, -0.17f, 0f), new Vector2(0.26f, 0.26f), 0.34f, 0f, Color.white);
            var glassMesh = glassAcc.ToMesh("PlazaLampGlass");

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
                fs.range = 7f;
                fs.lightOffset = new Vector3(0f, 0.05f, 0f);
                fs.emissive = new Renderer[] { mr };
                fs.emissiveStrength = 2.6f;
                fs.glow = true;
                fs.glowSize = 1.3f;
                fs.glowPoints = new[] { Vector3.zero };
                EditorUtility.SetDirty(fs);
            }

            if (bulbPts.Count == 0) return;
            var gar = new GameObject("Plaza_Garland");
            Undo.RegisterCreatedObjectUndo(gar, "Create Plaza_Garland");
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

        // ---------------------------------------------------------------- меню 130
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Площадка лагеря: пересобрать только площадку", false, 130)]
        static void MenuRebuildPlaza()
        {
            if (Application.isPlaying) { Debug.LogError("IslandV2: площадка собирается вне Play Mode."); return; }
            var rootGo = FindRoot();
            var gen = rootGo != null ? rootGo.transform.Find(GeneratedName) : null;
            if (gen == null) { Debug.LogError("IslandV2: нет IslandV2/_Generated. Сначала «2. Собрать блокаут»."); return; }
            var shader = Shader.Find(ShaderName);
            if (shader == null) { Debug.LogError("IslandV2: не найден шейдер " + ShaderName); return; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Остров v2: пересобрать площадку");
            int group = Undo.GetCurrentGroup();

            var old = gen.Find(PlazaName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            BuildCampPlaza(new IslandField(IslandLayout.GetOrCreate()), gen, GetMaterial(shader));

            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rootGo.scene);
        }
    }
}
