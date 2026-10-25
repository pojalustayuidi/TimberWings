#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/GrandpaGrove.cs (Editor)
// A5 v3: поляна Дедушки (древний хранитель). Добавлено в v3: кольцо камней-стражей с кристаллами (9 штук, у входа два «врата»),
// поваленные мшистые брёвна с грибами, кольцо светящихся грибов («ведьмино кольцо»), подношения у дупла (свечи, букет), цветочные островки,
// мшистый ковёр вокруг корней. Старое (лавки, фонари, пикник, указатель) остаётся.
// A5 v2: поляна Дедушки. Tools/BuilderSim/Лес/Красота леса/7. Поляна Дедушки: лавки, фонари, уют (приоритет 236). Безопасно жать повторно.
// Ставит в IslandV2/_Manual корень «GrandpaGrove» (пересоздаётся целиком) в точке Дедушки, в его системе координат:
//   +Z = в сторону входа (на PAD_Forest), дупло Дедушки смотрит туда же. Всё лежит внутри свободного круга ~9 м (ForestBackdrop держит 9 м без леса).
//
//   • дорожка из плоских камней от входа к дуплу, мох на камнях, каменная ступенька у дупла;
//   • две дуговые лавки вокруг Дедушки (спинка снаружи, сидишь лицом к стволу), между ними и качелями остаются проходы;
//   • две парковые лавки у дорожки (подушка на одной), 4 столба-фонаря с тёплыми фонариками, 4 гирлянды цветных флажков между ними;
//   • пикник: пеньковый стол, 3 табурета, чайник и чашки; плед с корзинкой и подушками;
//   • указатель-стрелки, рунный камень со светом, скворечник;
//   • свет: каждый фонарь и руны = FireSource (ночью светятся, рядом с игроком дают настоящий свет через FireLightPool).
// Три меша на поляну (мебель, «уют», земля) и 20-30 коллайдеров-коробок: draw call'ов немного.
// Каждая вещь проверяется: внутри острова, не ближе 1.2 м к краю, не в воде. Не прошла = пропущена (счёт в отчёте).
// Проходимость: дорожка шириной >2 м свободна, у лавок есть проходы. После пункта запусти «Остров v2 → 3. Проверить блокаут».
// Порядок: сначала «1. Дедушка», потом этот пункт (берёт позицию и разворот живого «Grandpa» из _Manual).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class GrandpaGrove
    {
        const string Dir = "Assets/_Project/Prefabs/Landmarks";
        const string RootName = "GrandpaGrove";

        static TreeMB A, B, C, D;       // A мебель: 0 дерево, 1 тёмное дерево, 2 камень, 3 железо
                                        // B уют: 0 тёплое стекло, 1 розовый, 2 жёлтый, 3 лайм
                                        // C земля: 0 камень, 1 мох, 2 белый, 3 жёлтый
                                        // D магия: 0 руна (бирюза), 1 шляпка светящегося гриба, 2 ножка гриба, 3 древний мшистый камень
        static IslandField F;
        static Transform R;
        static int skipped, placed, seed;
        static readonly List<LightSpot> lights = new List<LightSpot>();

        struct LightSpot { public Vector3 p; public bool rune, shroom, candle; }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/7. Поляна Дедушки: лавки, фонари, уют", false, 236)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Поляна: останови Play Mode."); return; }
            var manualGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = manualGo != null ? manualGo.transform : null;

            Vector3 pos; float yaw;
            var g = parent != null ? parent.Find("Grandpa") : null;
            if (g != null) { pos = g.position; yaw = g.eulerAngles.y; }
            else
            {
                var pad = GameObject.Find("PAD_Grandpa");
                if (pad == null) { Debug.LogError("Поляна: нет ни «Grandpa» в _Manual, ни метки PAD_Grandpa. Сначала «1. Дедушка»."); return; }
                pos = pad.transform.position; yaw = GrandpaTree.FacingYaw(pos);
            }

            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            F = L != null ? new IslandField(L) : null;

            var wood = ForestMats.GroveWood(false); var woodD = ForestMats.GroveWoodDark(false);
            var stone = ForestMats.Rock(false); var iron = ForestMats.GroveIron(false);
            var warm = ForestMats.GroveWarm(false); var pink = ForestMats.FlowerPink(false);
            var yellow = ForestMats.FlowerYellow(false); var lime = ForestMats.BushLime(false);
            var white = ForestMats.FlowerWhite(false);
            var mRune = ForestMats.GroveRune(false); var mCap = ForestMats.GroveShroomCap(false);
            var mStem = ForestMats.GroveShroomStem(false); var mOld = ForestMats.GroveStoneMoss(false);
            foreach (var m in new[] { wood, woodD, stone, iron, warm, pink, yellow, lime, white, mRune, mCap, mStem, mOld })
                if (m == null) { Debug.LogError("Поляна: не создался материал (проверь шейдер TreeToon: Лес → Tree Toon)."); return; }

            // ---- корень
            var old = parent != null ? parent.Find(RootName) : null;
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var rootGo = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootGo, "Поляна Дедушки");
            if (parent != null) rootGo.transform.SetParent(parent, false);
            rootGo.transform.position = pos;
            rootGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            R = rootGo.transform;

            A = new TreeMB(); B = new TreeMB(); C = new TreeMB(); D = new TreeMB();
            lights.Clear(); skipped = 0; placed = 0; seed = 1000;

            // ---- вещи
            StonePath();
            ArcBench(-112f, -52f);
            ArcBench(100f, 148f);
            ParkBench(-4.0f, 6.0f, 0f, 3.0f, false);
            ParkBench(4.1f, 7.6f, 0f, 3.0f, true);
            var posts = new[] { new Vector2(-2.1f, 7.6f), new Vector2(2.2f, 6.4f), new Vector2(-2.0f, 4.2f), new Vector2(2.3f, 3.4f) };
            var tops = new List<Vector3?>();
            foreach (var p in posts) tops.Add(LanternPost(p.x, p.y));
            Strand(tops[0], tops[1], 0); Strand(tops[2], tops[3], 1); Strand(tops[0], tops[2], 2); Strand(tops[1], tops[3], 0);
            Picnic(-5.3f, 5.4f);
            Blanket(5.4f, 5.0f);
            RuneStone(-2.0f, 2.9f);
            Signpost(1.8f, 9.2f);
            Birdhouse(-3.9f, 8.0f);

            // ---- v3: старость и тайна
            MossCarpet();
            float[] ringPhi = { 75f, 100f, 140f, 180f, -140f, -100f, -75f };
            float[] ringH = { 1.9f, 2.4f, 2.1f, 2.8f, 2.2f, 2.5f, 1.8f };
            for (int i = 0; i < ringPhi.Length; i++)
            {
                float rr = 8.6f + 0.5f * (Rnd(700 + i) - 0.5f);
                float rad = ringPhi[i] * Mathf.Deg2Rad;
                StandingStone(Mathf.Sin(rad) * rr, Mathf.Cos(rad) * rr, ringH[i], 0.55f + 0.18f * Rnd(710 + i), i % 2 == 0, i);
            }
            StandingStone(-2.8f, 9.0f, 2.7f, 0.62f, true, 11);     // «врата» у входа на поляну
            StandingStone(3.3f, 9.0f, 2.5f, 0.58f, true, 12);
            FallenLog(5.9f, -4.1f, 35f, 3.4f, 0.44f, 21);          // за правой дуговой лавкой
            FallenLog(-6.06f, -3.5f, -30f, 2.8f, 0.36f, 22);       // за левой
            FairyRing(0.5f, -6.0f, 1.2f, 11);                      // свободный сектор позади, между корнями-арками
            FairyRing(-7.0f, 3.8f, 0.7f, 6);
            Offerings();
            FlowerDrift(0.4f, -3.6f, 1.0f, 9, 0);
            FlowerDrift(7.3f, 1.0f, 1.0f, 8, 1);
            FlowerDrift(-7.2f, 0.6f, 0.9f, 8, 2);

            // ---- меши
            EditorAssets.EnsureFolder(Dir);
            var matsA = new[] { wood, woodD, stone, iron };
            var matsB = new[] { warm, pink, yellow, lime };
            var matsC = new[] { stone, lime, white, yellow };
            AddMesh("Furniture", ForestMats.SaveMesh(A.Build("Grove_Furniture"), Dir + "/Grove_Furniture.asset"), matsA, true);
            AddMesh("Cozy", ForestMats.SaveMesh(B.Build("Grove_Cozy"), Dir + "/Grove_Cozy.asset"), matsB, true);
            AddMesh("Ground", ForestMats.SaveMesh(C.Build("Grove_Ground"), Dir + "/Grove_Ground.asset"), matsC, false);
            var matsD = new[] { mRune, mCap, mStem, mOld };
            AddMesh("Magic", ForestMats.SaveMesh(D.Build("Grove_Magic"), Dir + "/Grove_Magic.asset"), matsD, true);

            // ---- свет
            int li = 0;
            foreach (var s in lights)
            {
                string nm = s.rune ? "Light_Rune" : s.shroom ? "Light_Shroom" : s.candle ? "Light_Candle" : "Light_Lantern";
                var go = new GameObject(nm + (li++));
                go.transform.SetParent(R, false);
                go.transform.localPosition = s.p;
                var fs = go.AddComponent<FireSource>();
                fs.glow = true;
                if (s.rune) { fs.preset = FlickerPreset.Garland; fs.color = new Color(0.55f, 1f, 0.85f); fs.simOnly = true; fs.glowSize = 1.2f; fs.intensity = 1f; fs.range = 4f; }
                else if (s.shroom) { fs.preset = FlickerPreset.Garland; fs.color = new Color(0.45f, 0.85f, 1f); fs.simOnly = true; fs.glowSize = 0.75f; fs.intensity = 0.8f; fs.range = 2.5f; }
                else if (s.candle) { fs.preset = FlickerPreset.Lantern; fs.color = new Color(1f, 0.70f, 0.35f); fs.simOnly = true; fs.glowSize = 0.55f; fs.intensity = 1f; fs.range = 2.5f; }
                else { fs.preset = FlickerPreset.Lantern; fs.color = new Color(1f, 0.78f, 0.45f); fs.simOnly = false; fs.glowSize = 1.3f; fs.intensity = 2.2f; fs.range = 6f; }
            }

            EditorSceneManager.MarkSceneDirty(R.gameObject.scene);
            Selection.activeGameObject = rootGo;
            var sb = new StringBuilder("Поляна Дедушки: корень " + RootName + " в (" + pos.x.ToString("0.#") + ", " + pos.z.ToString("0.#") + "), разворот " + yaw.ToString("0") + "°.\n");
            sb.AppendLine("  поставлено вещей: " + placed + ", пропущено (край острова, вода): " + skipped + ", светящихся точек: " + lights.Count);
            sb.AppendLine("  меши: Furniture/Cozy/Ground/Magic в " + Dir + ", коллайдеры-коробки у лавок, столбов, стола, камней-стражей, брёвен.");
            sb.AppendLine("  Дальше: «Остров v2 → 3. Проверить блокаут», посмотри вечером в Play Mode (фонари, светлячки). Двигай вещи руками: у каждой свой коллайдер-коробка.");
            Debug.Log(sb.ToString());
        }

        // ============================================================ помощники
        static bool GY(float x, float z, out float y)
        {
            Vector3 w = R.TransformPoint(new Vector3(x, 0f, z));
            if (F != null)
            {
                if (!F.Inside(w.x, w.z) || F.EdgeDistance(w.x, w.z) < 1.2f || F.IsWater(w.x, w.z)) { y = 0f; return false; }
                y = F.Height(w.x, w.z) - R.position.y;
                return true;
            }
            RaycastHit h;
            if (Physics.Raycast(new Vector3(w.x, w.y + 60f, w.z), Vector3.down, out h, 200f, ~0, QueryTriggerInteraction.Ignore)) { y = h.point.y - R.position.y; return true; }
            y = 0f; return true;
        }

        static void Col(string name, Vector3 local, Quaternion q, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(R, false);
            go.transform.localPosition = local;
            go.transform.localRotation = q;
            go.AddComponent<BoxCollider>().size = size;
        }

        static void AddMesh(string name, Mesh mesh, Material[] mats, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(R, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            int subs = Mathf.Max(1, mesh.subMeshCount);
            var arr = new Material[subs];
            for (int i = 0; i < subs; i++) arr[i] = mats[Mathf.Min(i, mats.Length - 1)];
            mr.sharedMaterials = arr;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        static float Rnd(int k) { return TreeMB.H(seed, k); }

        // ============================================================ дорожка
        static void StonePath()
        {
            var way = new[] { new Vector2(-1.1f, 9.8f), new Vector2(-0.7f, 8.2f), new Vector2(0.3f, 6.8f), new Vector2(0.5f, 5.4f), new Vector2(-0.2f, 4.2f), new Vector2(0.1f, 3.0f) };
            int n = 0;
            for (int s = 0; s + 1 < way.Length; s++)
            {
                float len = Vector2.Distance(way[s], way[s + 1]);
                int cnt = Mathf.Max(1, Mathf.RoundToInt(len / 0.85f));
                for (int i = 0; i < cnt; i++)
                {
                    Vector2 p = Vector2.Lerp(way[s], way[s + 1], i / (float)cnt);
                    Vector2 side = new Vector2(-(way[s + 1] - way[s]).y, (way[s + 1] - way[s]).x).normalized;
                    p += side * ((Rnd(n) - 0.5f) * 0.7f);
                    float y;
                    if (!GY(p.x, p.y, out y)) { skipped++; n++; continue; }
                    seed += 3;
                    C.Blob(0, new Vector3(p.x, y + 0.03f, p.y), new Vector3(0.46f + 0.2f * Rnd(n + 1), 0.07f, 0.38f + 0.18f * Rnd(n + 2)), seed, 0, 0.35f, -1);
                    if (n % 3 == 1) C.Blob(1, new Vector3(p.x + 0.12f, y + 0.07f, p.y - 0.08f), new Vector3(0.20f, 0.05f, 0.17f), seed + 1, 0, 0.35f, -1);
                    n++; placed++;
                }
            }
            // ступенька у дупла
            float yy;
            if (GY(0.1f, 2.55f, out yy)) { C.Blob(0, new Vector3(0.1f, yy + 0.04f, 2.55f), new Vector3(0.95f, 0.10f, 0.55f), seed + 50, 1, 0.25f, -1); placed++; }
        }

        // ============================================================ лавки
        // дуговая лавка вокруг Дедушки: сидишь лицом к стволу, спинка снаружи. φ от +Z к +X
        static void ArcBench(float a0, float a1)
        {
            const float r = 5.1f;
            int n = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / 14f));
            for (int i = 0; i < n; i++)
            {
                float phi = Mathf.Lerp(a0, a1, (i + 0.5f) / n);
                float rad = phi * Mathf.Deg2Rad;
                Vector3 rv = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                Vector3 tv = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
                float y;
                Vector3 p = rv * r;
                if (!GY(p.x, p.z, out y)) { skipped++; continue; }
                p.y = y;
                Quaternion q = Quaternion.Euler(0f, phi, 0f);
                GroveGeo.Box(A, 0, p + Vector3.up * 0.50f, new Vector3(0.72f, 0.035f, 0.26f), q);
                for (int s = -1; s <= 1; s += 2)
                {
                    GroveGeo.Box(A, 1, p + tv * (s * 0.55f) + Vector3.up * 0.22f, new Vector3(0.05f, 0.26f, 0.22f), q);
                    GroveGeo.Box(A, 1, p + rv * 0.27f + tv * (s * 0.62f) + Vector3.up * 0.55f, new Vector3(0.04f, 0.36f, 0.04f), q);
                }
                GroveGeo.Box(A, 0, p + rv * 0.27f + Vector3.up * 0.80f, new Vector3(0.72f, 0.10f, 0.025f), q);
                Col("Col_ArcBench", p + Vector3.up * 0.4f, q, new Vector3(1.5f, 0.8f, 0.7f));
                placed++;
            }
        }

        // парковая лавка: сиденье из досок, спинка, подлокотники; смотрит на точку (lx, lz)
        static void ParkBench(float x, float z, float lx, float lz, bool pillow)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            float yaw = Mathf.Atan2(-(lx - x), -(lz - z)) * Mathf.Rad2Deg;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 p = new Vector3(x, y, z);
            for (int i = -1; i <= 1; i++)
                GroveGeo.Box(A, 0, p + q * new Vector3(0f, 0.46f, i * 0.17f), new Vector3(0.95f, 0.03f, 0.075f), q);
            GroveGeo.Box(A, 0, p + q * new Vector3(0f, 0.78f, 0.31f), new Vector3(0.95f, 0.07f, 0.022f), q);
            GroveGeo.Box(A, 0, p + q * new Vector3(0f, 0.96f, 0.31f), new Vector3(0.95f, 0.07f, 0.022f), q);
            for (int s = -1; s <= 1; s += 2)
            {
                GroveGeo.Box(A, 1, p + q * new Vector3(s * 0.82f, 0.20f, 0f), new Vector3(0.06f, 0.30f, 0.30f), q);
                GroveGeo.Box(A, 1, p + q * new Vector3(s * 0.84f, 0.66f, 0.31f), new Vector3(0.045f, 0.36f, 0.04f), q);
                GroveGeo.Box(A, 1, p + q * new Vector3(s * 0.90f, 0.63f, 0f), new Vector3(0.045f, 0.025f, 0.27f), q);
            }
            if (pillow) { seed += 2; B.Blob(1, p + q * new Vector3(0.55f, 0.56f, 0.05f), new Vector3(0.28f, 0.09f, 0.20f), seed, 0, 0.2f, -1); }
            Col("Col_ParkBench", p + q * new Vector3(0f, 0.45f, 0.05f), q, new Vector3(2.0f, 0.9f, 0.75f));
            placed++;
        }

        // ============================================================ столб-фонарь; возвращает верх столба (для флажков)
        static Vector3? LanternPost(float x, float z)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return null; }
            float s = x > 0f ? -1f : 1f;      // рука фонаря смотрит на дорожку
            Vector3 p = new Vector3(x, y, z);
            GroveGeo.Box(A, 1, p + Vector3.up * 1.0f, new Vector3(0.06f, 1.12f, 0.06f), 0f);
            GroveGeo.Box(A, 2 + 1, p + Vector3.up * 0.06f, new Vector3(0.10f, 0.06f, 0.10f), 0f);    // железная пята
            GroveGeo.Box(A, 3, p + new Vector3(s * 0.20f, 2.06f, 0f), new Vector3(0.22f, 0.025f, 0.025f), 0f);   // рука
            Vector3 lp = p + new Vector3(s * 0.36f, 1.80f, 0f);
            seed += 2;
            B.Blob(0, lp, new Vector3(0.17f, 0.22f, 0.17f), seed, 0, 0.1f, -1);       // тёплое стекло
            A.Blob(3, lp + Vector3.up * 0.27f, new Vector3(0.20f, 0.06f, 0.20f), seed + 1, 0, 0.1f, -1);   // крыша фонаря
            lights.Add(new LightSpot { p = lp, rune = false });
            Col("Col_LanternPost", p + Vector3.up * 1.1f, Quaternion.identity, new Vector3(0.25f, 2.2f, 0.25f));
            placed++;
            return p + Vector3.up * 2.08f;
        }

        // гирлянда флажков между двумя столбами (мягкая дуга, цвета розовый/жёлтый/лайм)
        static void Strand(Vector3? a, Vector3? b, int shift)
        {
            if (!a.HasValue || !b.HasValue) { skipped++; return; }
            Vector3 pa = a.Value, pb = b.Value;
            const int N = 12;
            var rope = new Vector3[N + 1]; var rr = new float[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                rope[i] = Vector3.Lerp(pa, pb, t) + Vector3.down * (0.55f * 4f * t * (1f - t));
                rr[i] = 0.012f;
            }
            A.Tube(1, rope, rr, 3, false);
            Vector3 ad = (pb - pa); ad.y = 0f; ad.Normalize();
            Vector3 nrm = Vector3.Cross(ad, Vector3.up);
            for (int k = 1; k < N; k++)
            {
                Vector3 c = rope[k];
                Vector3 v0 = c - ad * 0.15f, v1 = c + ad * 0.15f, v2 = c + Vector3.down * 0.36f;
                int sub = 1 + ((k + shift) % 3);
                B.Tri(sub, v0, v1, v2, c + nrm * 1f);
                B.Tri(sub, v0, v1, v2, c - nrm * 1f);
            }
            placed++;
        }

        // ============================================================ пикник
        static void Stump(float x, float z, float r, float h, string colName)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            A.Tube(1, new[] { new Vector3(x, y - 0.12f, z), new Vector3(x, y + h, z) }, new[] { r, r * 0.97f }, 9, false);
            A.Cap(0, new Vector3(x, y + h + 0.004f, z), r * 0.97f, 9);
            Col(colName, new Vector3(x, y + h * 0.5f, z), Quaternion.identity, new Vector3(r * 1.7f, h, r * 1.7f));
        }

        static void Picnic(float x, float z)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            Stump(x, z, 0.62f, 0.62f, "Col_Table");
            for (int i = 0; i < 3; i++)
            {
                float a = (200f + i * 120f) * Mathf.Deg2Rad;
                Stump(x + Mathf.Sin(a) * 1.25f, z + Mathf.Cos(a) * 1.25f, 0.28f, 0.38f, "Col_Stool");
            }
            seed += 4;
            B.Blob(0, new Vector3(x - 0.1f, y + 0.77f, z + 0.05f), new Vector3(0.15f, 0.13f, 0.15f), seed, 0, 0.15f, -1);   // чайник
            B.Blob(0, new Vector3(x - 0.1f, y + 0.92f, z + 0.05f), new Vector3(0.06f, 0.04f, 0.06f), seed + 1, 0, 0.15f, -1);   // крышка
            B.Blob(2, new Vector3(x + 0.25f, y + 0.68f, z - 0.12f), new Vector3(0.06f, 0.05f, 0.06f), seed + 2, 0, 0.15f, -1);  // чашки
            B.Blob(1, new Vector3(x + 0.12f, y + 0.68f, z + 0.25f), new Vector3(0.06f, 0.05f, 0.06f), seed + 3, 0, 0.15f, -1);
            placed++;
        }

        static void Blanket(float x, float z)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            Quaternion q = Quaternion.Euler(0f, 20f, 0f);
            Vector3 p = new Vector3(x, y, z);
            GroveGeo.Box(B, 1, p + Vector3.up * 0.02f, new Vector3(0.95f, 0.015f, 0.75f), q);
            for (int i = -1; i <= 1; i++)
                GroveGeo.Box(B, 2, p + q * new Vector3(0f, 0.034f, i * 0.40f), new Vector3(0.95f, 0.012f, 0.07f), q);
            seed += 3;
            A.Blob(1, p + q * new Vector3(0.45f, 0.16f, 0.15f), new Vector3(0.28f, 0.20f, 0.24f), seed, 0, 0.2f, -1);    // корзинка
            B.Blob(3, p + q * new Vector3(-0.50f, 0.10f, -0.20f), new Vector3(0.30f, 0.09f, 0.24f), seed + 1, 0, 0.2f, -1);  // подушки
            B.Blob(1, p + q * new Vector3(-0.15f, 0.10f, -0.45f), new Vector3(0.28f, 0.09f, 0.22f), seed + 2, 0, 0.2f, -1);
            placed++;
        }

        // ============================================================ камень с рунами, указатель, скворечник
        static void RuneStone(float x, float z)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            seed += 4;
            A.Blob(2, new Vector3(x, y + 0.80f, z), new Vector3(0.50f, 0.85f, 0.34f), seed, 1, 0.30f, -1);
            A.Blob(2, new Vector3(x + 0.55f, y + 0.18f, z + 0.20f), new Vector3(0.30f, 0.22f, 0.28f), seed + 1, 0, 0.45f, -1);
            A.Blob(2, new Vector3(x - 0.45f, y + 0.14f, z - 0.15f), new Vector3(0.26f, 0.18f, 0.24f), seed + 2, 0, 0.45f, -1);
            C.Blob(1, new Vector3(x, y + 0.10f, z + 0.25f), new Vector3(0.55f, 0.08f, 0.35f), seed + 3, 0, 0.35f, -1);   // мох у подножия
            GrandpaTree.CrystalCluster(D, 0, new Vector3(x + 0.05f, y + 1.15f, z + 0.28f), Vector3.forward, 4, 0.85f, 830);   // v4: вместо рун на камне растут кристаллы
            lights.Add(new LightSpot { p = new Vector3(x, y + 1.0f, z + 0.42f), rune = true });
            Col("Col_RuneStone", new Vector3(x, y + 0.7f, z), Quaternion.identity, new Vector3(0.9f, 1.4f, 0.65f));
            placed++;
        }

        static void Arrow(Vector3 post, float h, Vector3 target)
        {
            Vector3 d = new Vector3(target.x - post.x, 0f, target.z - post.z).normalized;
            float yaw = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 c = post + Vector3.up * h + d * 0.46f;
            GroveGeo.Box(A, 0, c, new Vector3(0.46f, 0.085f, 0.025f), q);
            GroveGeo.Box(A, 0, c + d * 0.46f, new Vector3(0.085f, 0.085f, 0.025f), q * Quaternion.Euler(0f, 0f, 45f));
        }

        static void Signpost(float x, float z)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            Vector3 p = new Vector3(x, y, z);
            GroveGeo.Box(A, 1, p + Vector3.up * 1.05f, new Vector3(0.06f, 1.15f, 0.06f), 0f);
            Arrow(p, 1.80f, Vector3.zero);                       // к Дедушке
            Arrow(p, 1.42f, new Vector3(3.8f, 0f, 2.6f));        // к качелям
            Col("Col_Signpost", p + Vector3.up * 1.0f, Quaternion.identity, new Vector3(0.25f, 2.0f, 0.25f));
            placed++;
        }

        static void Birdhouse(float x, float z)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            Vector3 p = new Vector3(x, y, z);
            GroveGeo.Box(A, 1, p + Vector3.up * 0.85f, new Vector3(0.05f, 0.95f, 0.05f), 0f);
            GroveGeo.Box(A, 0, p + Vector3.up * 1.88f, new Vector3(0.17f, 0.17f, 0.15f), 0f);
            for (int s = -1; s <= 1; s += 2)
                GroveGeo.Box(A, 1, p + new Vector3(s * 0.10f, 2.12f, 0f), new Vector3(0.14f, 0.012f, 0.19f), Quaternion.Euler(0f, 0f, -s * 38f));
            Col("Col_Birdhouse", p + Vector3.up * 1.0f, Quaternion.identity, new Vector3(0.2f, 2.0f, 0.2f));
            placed++;
        }

        // ============================================================ v3: камни-стражи, брёвна, грибы, подношения, цветы
        // v4 «Cloud Candy»: рун больше нет. На камне растёт маленькая группа светящихся кристаллов (как у корней Дедушки).
        // c точка на боку камня, inward единичный вектор от камня к центру поляны, sz размер, kind разброс формы 0..2
        static void Rune(Vector3 c, Vector3 inward, float sz, int kind)
        {
            GrandpaTree.CrystalCluster(D, 0, c - Vector3.up * (sz * 0.9f), inward, 3, sz * 1.9f, 800 + kind * 5);
        }

        // древний мшистый камень-страж. h высота, w радиус в плане. Руна смотрит на Дедушку
        static void StandingStone(float x, float z, float h, float w, bool rune, int sd)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            Vector3 inward = new Vector3(-x, 0f, -z).normalized;
            Vector3 tang = Vector3.Cross(Vector3.up, inward).normalized;
            Vector3 p = new Vector3(x, y, z);
            seed += 6;
            D.Blob(3, p + Vector3.up * (h * 0.5f), new Vector3(w, h * 0.5f, w), seed, 1, 0.28f, -1);
            D.Blob(3, p + Vector3.up * (h * 0.93f) + tang * (w * 0.15f), new Vector3(w * 0.72f, h * 0.13f, w * 0.72f), seed + 1, 0, 0.35f, -1);   // «шапка»
            D.Blob(3, p + tang * (w * 1.05f) + inward * (-0.1f) + Vector3.up * 0.14f, new Vector3(w * 0.55f, 0.17f, w * 0.5f), seed + 2, 0, 0.4f, -1);   // валун у основания
            C.Blob(1, p + Vector3.up * 0.10f, new Vector3(w * 1.55f, 0.12f, w * 1.3f), seed + 3, 0, 0.35f, -1);                                      // мох у подножия
            C.Blob(1, p + Vector3.up * (h * 0.30f) + tang * (w * 0.2f), new Vector3(w * 0.55f, h * 0.20f, w * 0.55f), seed + 4, 0, 0.35f, -1);       // мох на боку
            if (rune)
            {
                Vector3 rc = p + Vector3.up * (h * 0.55f) + inward * (w * 0.96f);
                Rune(rc, inward, 0.30f, sd % 3);
                lights.Add(new LightSpot { p = rc + inward * 0.12f, rune = true });
            }
            Col("Col_StandingStone", p + Vector3.up * (h * 0.5f), Quaternion.identity, new Vector3(w * 1.8f, h, w * 1.6f));
            placed++;
        }

        // гриб: ножка + шляпка. glow = горит бирюзой ночью (огоньков немного, FireLightPool всё равно берёт ближайшие)
        static void Mushroom(float x, float z, float h, float capR, bool glow)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            seed += 3;
            Vector3 p = new Vector3(x, y, z);
            D.Tube(2, new[] { p + Vector3.down * 0.03f, p + new Vector3(0.01f, h, 0.01f) }, new[] { h * 0.24f, h * 0.17f }, 5, false);
            D.Blob(1, p + new Vector3(0.01f, h + capR * 0.12f, 0.01f), new Vector3(capR, capR * 0.55f, capR), seed, 0, 0.25f, -1);
            if (glow) lights.Add(new LightSpot { p = p + new Vector3(0f, h + capR * 0.4f, 0f), shroom = true });
        }

        // «ведьмино кольцо»: светящиеся грибы по кругу. Не мешают проходу: низкие, без коллайдеров
        static void FairyRing(float cx, float cz, float r, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + (Rnd(800 + i) - 0.5f) * 0.3f;
                float rr = r * (0.9f + 0.2f * Rnd(820 + i));
                float h = 0.14f + 0.20f * Rnd(840 + i);
                Mushroom(cx + Mathf.Sin(a) * rr, cz + Mathf.Cos(a) * rr, h, 0.12f + 0.10f * Rnd(860 + i), i % 3 == 0);
            }
            float y;
            if (GY(cx, cz, out y)) { C.Blob(1, new Vector3(cx, y + 0.03f, cz), new Vector3(r * 0.95f, 0.04f, r * 0.95f), seed + 7, 1, 0.3f, -1); placed++; }   // мшистая «подушка» внутри кольца
        }

        // поваленное мшистое бревно с грибами. yawDeg направление, len длина, r радиус
        static void FallenLog(float x, float z, float yawDeg, float len, float r, int sd)
        {
            float a = yawDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            float x0 = x - dir.x * len * 0.5f, z0 = z - dir.z * len * 0.5f, x1 = x + dir.x * len * 0.5f, z1 = z + dir.z * len * 0.5f;
            float y0, y1;
            if (!GY(x0, z0, out y0) || !GY(x1, z1, out y1) || !GY(x, z, out y0)) { skipped++; return; }
            GY(x0, z0, out y0); GY(x1, z1, out y1);
            seed += 5;
            var pts = new Vector3[4]; var rad = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                pts[i] = new Vector3(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t) + r * 0.85f + 0.05f * Mathf.Sin(t * 5f + sd), Mathf.Lerp(z0, z1, t));
                rad[i] = r * (1.0f + 0.05f * Mathf.Sin(t * 7f + sd) - 0.12f * t);
            }
            A.Tube(1, pts, rad, 8, true);
            A.Tube(1, new[] { pts[1], pts[0] }, new[] { rad[1], rad[0] }, 8, true);   // торец у начала
            // сучки и мох
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            A.Tube(1, new[] { pts[1] + side * (r * 0.8f), pts[1] + side * (r * 1.7f) + Vector3.up * 0.18f }, new[] { 0.07f, 0.03f }, 4, false);
            for (int i = 0; i < 3; i++)
            {
                float t = 0.2f + 0.3f * i;
                Vector3 c = Vector3.Lerp(pts[0], pts[3], t) + Vector3.up * (r * 0.92f);
                C.Blob(1, c, new Vector3(len * 0.17f, r * 0.30f, r * 0.75f), seed + i, 0, 0.35f, -1);
            }
            // шляпки-грибы сбоку (одна светится)
            Vector3 mp = Vector3.Lerp(pts[0], pts[3], 0.62f) + side * (r * 0.95f);
            D.Blob(1, mp + Vector3.up * 0.15f, new Vector3(0.20f, 0.10f, 0.20f), seed + 9, 0, 0.25f, -1);
            D.Blob(1, mp + side * 0.10f + Vector3.up * 0.30f, new Vector3(0.14f, 0.08f, 0.14f), seed + 10, 0, 0.25f, -1);
            lights.Add(new LightSpot { p = mp + Vector3.up * 0.28f, shroom = true });
            Col("Col_FallenLog", Vector3.Lerp(pts[0], pts[3], 0.5f), Quaternion.Euler(0f, yawDeg, 0f), new Vector3(r * 2f, r * 2f, len));
            placed++;
        }

        // подношения у входа в дупло: две свечи и букет
        static void Candle(float x, float z, float h)
        {
            float y;
            if (!GY(x, z, out y)) { skipped++; return; }
            seed += 2;
            Vector3 p = new Vector3(x, y + 0.06f, z);
            C.Blob(0, p, new Vector3(0.14f, 0.04f, 0.14f), seed, 0, 0.3f, -1);                    // каменная плошка
            C.Tube(2, new[] { p, p + Vector3.up * h }, new[] { 0.05f, 0.045f }, 6, false);        // свеча
            B.Blob(0, p + Vector3.up * (h + 0.07f), new Vector3(0.035f, 0.065f, 0.035f), seed + 1, 0, 0.1f, -1);   // пламя
            lights.Add(new LightSpot { p = p + Vector3.up * (h + 0.1f), candle = true });
        }

        static void Offerings()
        {
            Candle(-0.8f, 2.85f, 0.20f);
            Candle(1.0f, 2.75f, 0.14f);
            float y;
            if (GY(0.15f, 2.85f, out y))
            {
                for (int i = 0; i < 7; i++)
                {
                    float a = i / 7f * Mathf.PI * 2f;
                    Vector3 c = new Vector3(0.15f + Mathf.Sin(a) * 0.16f, y + 0.17f + 0.04f * (i % 2), 2.85f + Mathf.Cos(a) * 0.12f);
                    seed += 2;
                    if (i % 3 == 0) B.Blob(2, c, new Vector3(0.09f, 0.07f, 0.09f), seed, 0, 0.25f, -1);
                    else if (i % 3 == 1) B.Blob(1, c, new Vector3(0.10f, 0.07f, 0.10f), seed, 0, 0.25f, -1);
                    else C.Blob(2, c, new Vector3(0.09f, 0.07f, 0.09f), seed, 0, 0.25f, -1);
                }
                B.Blob(3, new Vector3(0.15f, y + 0.10f, 2.85f), new Vector3(0.22f, 0.08f, 0.2f), seed + 20, 0, 0.3f, -1);   // зелень букета
                placed++;
            }
        }

        // цветочный островок: цветки трёх цветов вперемешку, kind задаёт, какого больше (0 розовые, 1 жёлтые, 2 белые)
        static void FlowerDrift(float cx, float cz, float r, int n, int kind)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Rnd(900 + i + kind * 40) * Mathf.PI * 2f;
                float rr = r * Mathf.Sqrt(Rnd(930 + i + kind * 40));
                float x = cx + Mathf.Sin(a) * rr, z = cz + Mathf.Cos(a) * rr;
                float y;
                if (!GY(x, z, out y)) { skipped++; continue; }
                seed += 2;
                float roll = Rnd(960 + i + kind * 40);
                int pick = roll < 0.55f ? kind : (kind + 1 + (roll < 0.8f ? 0 : 1)) % 3;
                Vector3 c = new Vector3(x, y + 0.20f, z);
                float k = 0.8f + 0.5f * Rnd(990 + i);
                if (pick == 0) B.Blob(1, c, new Vector3(0.12f, 0.08f, 0.12f) * k, seed, 0, 0.25f, -1);
                else if (pick == 1) B.Blob(2, c, new Vector3(0.12f, 0.08f, 0.12f) * k, seed, 0, 0.25f, -1);
                else C.Blob(2, c, new Vector3(0.12f, 0.08f, 0.12f) * k, seed, 0, 0.25f, -1);
                C.Tube(1, new[] { new Vector3(x, y - 0.02f, z), new Vector3(x, y + 0.16f, z) }, new[] { 0.025f, 0.02f }, 3, false);   // стебелёк
                C.Blob(1, new Vector3(x + 0.1f, y + 0.07f, z), new Vector3(0.12f, 0.04f, 0.07f), seed + 1, 0, 0.3f, -1);               // листок
            }
            placed++;
        }

        // мшистый ковёр вокруг корней Дедушки: сзади и по бокам (спереди дорожка)
        static void MossCarpet()
        {
            float[] phi = { 70f, 100f, 130f, 160f, 200f, 230f, 260f, 290f };
            for (int i = 0; i < phi.Length; i++)
            {
                float a = phi[i] * Mathf.Deg2Rad;
                float r = 3.9f + 0.7f * Rnd(1100 + i);
                float x = Mathf.Sin(a) * r, z = Mathf.Cos(a) * r, y;
                if (!GY(x, z, out y)) { skipped++; continue; }
                seed += 2;
                C.Blob(1, new Vector3(x, y + 0.03f, z), new Vector3(1.5f + 0.4f * Rnd(1120 + i), 0.05f, 1.15f + 0.3f * Rnd(1140 + i)), seed, 1, 0.3f, -1);
            }
            placed++;
        }
    }
}
#endif
