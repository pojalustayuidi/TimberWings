#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Terrain/LocationGen.Terrain.cs
// Высоты, ассеты (материалы, меши), Terrain + раскраска, маркеры, границы, свет сцены.
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class LocationBuilder
{
    sealed partial class Gen
    {
        // ---------------------------------------------------------- heights

        // ---- Естественный рельеф (единицы мира до worldScale). Правь тут, потом пересобери локацию в Location Builder ----
        // Кольцо холмов по краю: неровный внутренний край, ширина 0.7..1.3 от ringWidth, высота сильно гуляет
        // (низкие луга, пологие седловины и редкие высокие горки), вместо ровного вала по периметру.
        const float NatEdgeWobble = 12f;      // насколько внутренний край кольца гуляет, единиц
        const float NatEdgeWobbleFreq = 0.03f;
        const float NatRingLow = 0.30f;       // низ высоты кольца = ringMin * это
        const float NatRingHigh = 0.75f;      // верх высоты кольца = maxHeight * это
        const float NatRingPow = 1.5f;        // больше = больше низких мест и реже высокие горки
        const float NatRidgeNoise = 0.30f;    // мелкая бугристость гребня (0..1)
        // Пологие всхолмления на открытых лугах (только вверх, плоские зоны не трогаем)
        const float NatRollAmp = 1.1f;        // высота, единиц (х worldScale = метры)
        const float NatRollFreq = 0.02f;

        float Hills01(float x, float z)
        {
            float f = s.hillFrequency;
            float v = 0.55f * Perlin(x * f, z * f, 10f) + 0.30f * Perlin(x * f * 2.1f, z * f * 2.1f, 50f) + 0.15f * Perlin(x * f * 4.3f, z * f * 4.3f, 90f);
            return Mathf.Clamp01((v - 0.30f) / 0.45f);
        }

        float RiverRingMask(float x, float z)
        {
            float hw = s.riverWidth * 0.5f;
            float dx = x - RiverX(z);
            if (s.newRiver)
            {
                // Р3: кромка прорезана на всю ширину русла + бровку, чтобы река уходила за южную кромку, а берега у Dock и моста не упирались в вал
                float wn = RiverHalfW(z) + RiverBankRunAt(dx) + s.riverBrowWidth + 1f;
                return SS(wn, wn + 3f, Mathf.Abs(dx));
            }
            if (s.riverGorge)
            {
                // ущелье: кромка прорезана только узкой щелью по ширине стенок, по бокам остаются скальные «ворота», обойти реку по краю нельзя
                float wg = hw + (dx < 0f ? s.gorgeFarWall : s.gorgeWall) * GorgeFunnel + 1f;
                return SS(wg, wg + 3f, Mathf.Abs(dx));
            }
            float hwb = hw + s.riverBank + s.riverSlope * 0.5f;
            return dx > 0f ? SS(hwb + 1f, hwb + 14f, dx) : SS(hw - 1f, hw + 8f, -dx);
        }

        void ComputeHeights()
        {
            if (Expressive) { ComputeHeightsRelief(); return; }   // LocationGen.Relief.cs
            hu = new float[N * N];
            dFlatArr = new float[N * N];
            float flatExtra = (s.trailFlatWidth - s.trailWidth) * 0.5f;
            float hw = s.riverWidth * 0.5f;
            Vector2 fc = forest != null ? forest.pos : Vector2.zero;

            for (int j = 0; j < N; j++)
            {
                float z = -half + j * cell;
                for (int i = 0; i < N; i++)
                {
                    float x = -half + i * cell;
                    int idx = j * N + i;
                    float dTr = sdTrail[idx] - flatExtra;
                    float dPad = Mathf.Min(sdPad[idx], new Vector2(x, z).magnitude - s.centerRadius);
                    float dFlat = Mathf.Min(dTr, dPad);
                    dFlatArr[idx] = dFlat;
                    float sdF = forest != null ? (new Vector2(x, z) - fc).magnitude - forest.rad : 999f;

                    float h = 0f;
                    if (dFlat > 0f)
                    {
                        float hm = SS(s.hillClear, s.hillClear + s.hillFade, dFlat);
                        if (forest != null) hm *= SS(0f, 10f, sdF);
                        h += s.hillAmplitude * Hills01(x, z) * hm;

                        // пологие всхолмления: разный масштаб, только вверх, у плоских зон и леса сходят на нет
                        float roll = Perlin(x * NatRollFreq, z * NatRollFreq, 600f) * 0.65f + Perlin(x * NatRollFreq * 2.7f, z * NatRollFreq * 2.7f, 640f) * 0.35f;
                        float rollM = SS(4f, 16f, dFlat);
                        if (forest != null) rollM *= SS(0f, 10f, sdF);
                        h += NatRollAmp * Mathf.Clamp01((roll - 0.35f) / 0.65f) * rollM;

                        // кольцо по краю: неровное
                        float wob = (Perlin(x * NatEdgeWobbleFreq, z * NatEdgeWobbleFreq, 500f) - 0.5f) * 2f * NatEdgeWobble;
                        float edge = half - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) + wob;
                        float ringW = s.ringWidth * (0.7f + 0.6f * Perlin(x * 0.03f, z * 0.03f, 520f));
                        float rt = 1f - Mathf.Clamp01(edge / ringW);
                        float prof = rt * rt * (3f - 2f * rt);
                        float nr = Perlin(x * 0.05f, z * 0.05f, 200f);
                        float rb = Mathf.Lerp(s.ringMin * NatRingLow, s.maxHeight * NatRingHigh, Mathf.Pow(nr, NatRingPow));
                        rb *= 1f + NatRidgeNoise * (Perlin(x * 0.11f, z * 0.11f, 260f) * 2f - 1f);
                        float rm = SS(6f, 18f, dFlat) * RiverRingMask(x, z);
                        if (forest != null) rm *= SS(0f, 6f, sdF);
                        h += rb * prof * rm;

                        if (forest != null)
                        {
                            Vector2 rel = new Vector2(x, z) - fc;
                            float r = rel.magnitude;
                            float g = Mathf.Exp(-Mathf.Pow((r - (forest.rad + 5f)) / 4.5f, 2f));
                            float along = Vector2.Dot(rel, forest.fwd);
                            float lateral = Mathf.Abs(Vector2.Dot(rel, forest.right));
                            float gate = along > 0f ? SS(6f, 16f, lateral) : 1f;
                            h += s.forestRidgeHeight * (0.75f + 0.5f * Perlin(x * 0.07f, z * 0.07f, 250f)) * g * gate * SS(8f, 20f, dFlat);

                            if (sdF < 0f)
                                h += s.forestRelief * (Perlin(x * 0.08f, z * 0.08f, 300f) * 2f - 1f) * SS(0f, 5f, dFlat) * (1f - SS(-6f, 0f, sdF));
                        }

                        float cap = 1.5f + Mathf.Max(0f, dFlat - 10.5f) * 0.9f;
                        if (h > cap) h = cap;
                        if (h > s.maxHeight) h = s.maxHeight;
                    }

                    h = CarveRiver(x, z, h, 1f - SS(0f, 4f, dFlat), 1f - SS(0f, 0.8f, dPad));
                    hu[idx] = h;
                }
            }

            // сглаживание
            var tmp = new float[N * N];
            for (int pass = 0; pass < s.blurPasses; pass++)
            {
                for (int j = 0; j < N; j++)
                {
                    int jm = Mathf.Max(j - 1, 0), jp = Mathf.Min(j + 1, N - 1);
                    for (int i = 0; i < N; i++)
                    {
                        int im = Mathf.Max(i - 1, 0), ip = Mathf.Min(i + 1, N - 1);
                        float sum = hu[jm * N + im] + 2f * hu[jm * N + i] + hu[jm * N + ip]
                                  + 2f * hu[j * N + im] + 4f * hu[j * N + i] + 2f * hu[j * N + ip]
                                  + hu[jp * N + im] + 2f * hu[jp * N + i] + hu[jp * N + ip];
                        tmp[j * N + i] = sum / 16f;
                    }
                }
                var sw = hu; hu = tmp; tmp = sw;
            }

            // жёсткое выравнивание зон (Y = 0 точно)
            for (int idx = 0; idx < hu.Length; idx++)
            {
                float d = dFlatArr[idx];
                if (d <= 0.4f) hu[idx] = 0f;
                else if (d < 2f) hu[idx] = Mathf.Lerp(0f, hu[idx], SS(0.4f, 2f, d));
            }

            float mn = float.MaxValue, mx = float.MinValue;
            for (int idx = 0; idx < hu.Length; idx++)
            {
                if (hu[idx] < mn) mn = hu[idx];
                if (hu[idx] > mx) mx = hu[idx];
            }
            minM = mn * ws;
            hmM = Mathf.Max(mx * ws - minM, 1f);
        }

        // ---------------------------------------------------------- assets

        Texture2D MakeTex(string name, Color c)
        {
            string path = AssetDir + "/Textures/" + name + ".asset";
            touched.Add(path);
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null)
            {
                t = new Texture2D(4, 4, TextureFormat.RGBA32, true, false) { name = name };
                AssetDatabase.CreateAsset(t, path);
            }
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            t.Apply(true);
            EditorUtility.SetDirty(t);
            return t;
        }

        TerrainLayer MakeLayer(string name, Color c)
        {
            string path = AssetDir + "/Layer_" + name + ".terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (l == null)
            {
                l = new TerrainLayer { name = "Layer_" + name };
                AssetDatabase.CreateAsset(l, path);
            }
            l.diffuseTexture = MakeTex("Tex_" + name, c);
            l.tileSize = new Vector2(8f, 8f);
            l.smoothness = 0f;
            l.metallic = 0f;
            EditorUtility.SetDirty(l);
            return l;
        }

        Material MakeMat(string name, Color c, float smooth, float emission)
        {
            string path = AssetDir + "/Materials/" + name + ".mat";
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) throw new Exception("Не найден шейдер 'Universal Render Pipeline/Lit'. Проект должен быть на URP.");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = sh;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        // материалы окружения создаются лениво: только те, что реально попали в меши
        void CreateMaterials()
        {
            envMats = new Material[MatNames.Length];
        }

        Material EnvMat(int i)
        {
            if (envMats[i] != null) return envMats[i];
            float em = i == M_LANTERN ? 1.6f : 0f;
            envMats[i] = MakeMat("Mat_" + MatNames[i], s.envColors[i], 0.1f, em);
            return envMats[i];
        }

        Mesh SaveMesh(Mesh m, string name)
        {
            string path = AssetDir + "/Meshes/" + name + ".asset";
            touched.Add(path);
            m.name = name;
            var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (ex != null)
            {
                EditorUtility.CopySerialized(m, ex);
                UnityEngine.Object.DestroyImmediate(m);
                EditorUtility.SetDirty(ex);
                return ex;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        void CleanupMeshes()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { AssetDir + "/Meshes" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!touched.Contains(p)) AssetDatabase.DeleteAsset(p);
            }
        }

        // собирает батч в один меш-объект; false если батч пуст
        bool FinishMesh(GameObject go, MeshBatch mb, string meshName, bool shadows)
        {
            var used = new System.Collections.Generic.List<int>();
            var mesh = mb.Build(meshName, used);
            if (mesh == null) return false;
            mesh = SaveMesh(mesh, meshName);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mats = new Material[used.Count];
            for (int i = 0; i < mats.Length; i++) mats[i] = EnvMat(used[i]);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            MarkStatic(go);
            return true;
        }

        // ---------------------------------------------------------- terrain

        void BuildTerrain()
        {
            string tdPath = AssetDir + "/Terrain.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath);
            if (td == null)
            {
                td = new TerrainData { name = "LocationTerrain" };
                AssetDatabase.CreateAsset(td, tdPath);
            }

            float S = s.terrainSize * ws;
            td.heightmapResolution = N;
            td.size = new Vector3(S, hmM, S);
            var hm = new float[N, N];
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                    hm[j, i] = Mathf.Clamp01((hu[j * N + i] * ws - minM) / hmM);
            td.SetHeights(0, 0, hm);

            var layerList = new System.Collections.Generic.List<TerrainLayer>
            {
                MakeLayer("GrassA", s.grassA), MakeLayer("GrassB", s.grassB), MakeLayer("Trail", s.trailColor),
                MakeLayer("Pad", s.padColor), MakeLayer("Beach", s.beachColor), MakeLayer("Forest", s.forestColor),
                MakeLayer("TrailDark", s.trailDarkColor)
            };
            if (Expressive)   // порядок = L_ROCK, L_SOIL, L_CREST, L_WET (LocationGen.Relief.cs)
            {
                layerList.Add(MakeLayer("Rock", s.rockColor));
                layerList.Add(MakeLayer("Soil", s.soilColor));
                layerList.Add(MakeLayer("Crest", s.crestColor));
                layerList.Add(MakeLayer("Wet", s.wetColor));
            }
            var layers = layerList.ToArray();
            td.terrainLayers = layers;
            td.alphamapResolution = s.alphaRes;
            td.baseMapResolution = Mathf.Clamp(s.alphaRes * 2, 256, 2048);
            td.SetAlphamaps(0, 0, BuildSplat());
            EditorUtility.SetDirty(td);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(-S * 0.5f, minM, -S * 0.5f);
            var t = go.GetComponent<Terrain>();
            t.drawInstanced = true;
            t.heightmapPixelError = 6f;
            // слои — плоские цвета, поэтому везде рисуем готовый basemap: один проход вместо двух (7 слоёв)
            t.basemapDistance = 1f;
            t.drawTreesAndFoliage = false;
        }

        float[,,] BuildSplat()
        {
            int L = LayerCount;   // 7, а в «Выразительном» рельефе 11 (+ скала, земля, гребень, влажный)
            int A = s.alphaRes;
            var am = new float[A, A, L];
            float ca = s.terrainSize / A;
            float hw = s.riverWidth * 0.5f;
            float cr = s.centerRadius;
            Vector2 fc = forest != null ? forest.pos : Vector2.zero;
            var w = new float[L];
            // сглаженная высота: от неё считаем «гребень / ложбина»
            float[] lowU = Expressive ? BlurField(hu, Mathf.Max(1, Mathf.RoundToInt(PaintLowpassU / cell))) : null;

            for (int j = 0; j < A; j++)
            {
                float z = -half + (j + 0.5f) * ca;
                for (int i = 0; i < A; i++)
                {
                    float x = -half + (i + 0.5f) * ca;
                    float sdT = FieldAt(sdTrail, x, z);
                    float sdP = FieldAt(sdPad, x, z);
                    float rC = Mathf.Sqrt(x * x + z * z);

                    float n1 = Perlin(x * 0.04f, z * 0.04f, 400f);
                    float n2 = Perlin(x * 0.13f, z * 0.13f, 450f);
                    float b = SS(0.48f, 0.68f, n1 * 0.75f + n2 * 0.25f) * 0.8f;
                    b *= SS(cr - 1.5f, cr + 0.5f, rC);
                    w[0] = 1f - b; w[1] = b;
                    for (int k = 2; k < L; k++) w[k] = 0f;

                    if (Expressive) ReliefPaintA(w, x, z, lowU);   // светлая трава на гребнях, влажный оттенок в низинах

                    if (forest != null)
                    {
                        float sdF = (new Vector2(x, z) - fc).magnitude - forest.rad;
                        Over(w, 5, 1f - SS(-3f, 5f, sdF));
                    }

                    if (Expressive) ReliefPaintB(w, x, z);          // тёмная земля и скала на крутых склонах (в том числе под лесом)

                    float dxA = Mathf.Abs(x - RiverX(z)) + (Perlin(x * 0.2f, z * 0.2f, 500f) - 0.5f) * 1.2f;
                    if (s.riverGorge)
                    {
                        // ущелье: песок/галька только на дне, стенки скалой, бровка остаётся травой
                        float rmask = RiverMask(z);
                        Over(w, 4, (1f - SS(hw * 0.8f, hw + 0.6f, dxA)) * rmask);
                        if (Expressive) Over(w, L_ROCK, SS(hw + 0.4f, hw + 0.9f, dxA) * (1f - SS(hw + s.gorgeWall + 0.3f, hw + s.gorgeWall + 1.4f, dxA)) * rmask * 0.95f);
                    }
                    else
                    Over(w, 4, (1f - SS(hw + s.riverBank + s.riverSlope * 0.5f - 0.5f, hw + s.riverBank + s.riverSlope * 0.5f + 1.8f, dxA)) * RiverMask(z));   // песчано-галечная полоса берега


                    // площадки: рваный по шуму край, чтобы не было «прямоугольника»
                    float padN = (Perlin(x * 0.35f, z * 0.35f, 470f) - 0.5f) * 0.7f;
                    float rim = 1f - SS(0f, 0.7f, Mathf.Abs(rC - (cr + 0.1f)));
                    Over(w, 3, rim * 0.85f);
                    Over(w, 3, 1f - SS(-0.2f, 0.5f, sdP + padN));

                    // тропа
                    float cMask = SS(cr - 0.8f, cr + 0.2f, rC);
                    float tm = (1f - SS(0f, 0.5f, sdT)) * cMask;
                    Over(w, 2, tm);

                    // тёмная кромка и колеи
                    float padFree = SS(0.2f, 1.0f, sdP);
                    float edge = SS(-0.55f, -0.15f, sdT) * (1f - SS(0.1f, 0.45f, sdT)) * cMask * padFree;
                    Over(w, 6, edge * 0.75f);
                    float tc = FieldAt(tCenter, x, z);
                    float inside = 1f - SS(-0.8f, -0.4f, sdT);
                    float rut = (1f - SS(0.04f, 0.15f, Mathf.Abs(tc - 0.6f))) * inside
                                * SS(0.35f, 0.6f, Perlin(x * 0.5f, z * 0.5f, 520f)) * cMask * padFree;
                    Over(w, 6, rut * 0.5f);

                    float sum = 0f;
                    for (int k = 0; k < L; k++) sum += w[k];
                    if (sum < 1e-5f) { w[0] = 1f; sum = 1f; }
                    for (int k = 0; k < L; k++) am[j, i, k] = w[k] / sum;
                }
            }
            return am;
        }

        static void Over(float[] w, int idx, float a)
        {
            a = Mathf.Clamp01(a);
            if (a <= 0f) return;
            for (int k = 0; k < w.Length; k++) w[k] *= 1f - a;
            w[idx] += a;
        }

        // ---------------------------------------------------------- markers

        GameObject Mk(string name, Transform parent, Vector3 localPos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            return go;
        }

        static Quaternion Look(Vector2 f)
        {
            return Quaternion.LookRotation(new Vector3(f.x, 0f, f.y), Vector3.up);
        }

        void BuildMarkers()
        {
            var padsT = new GameObject("Pads").transform;
            padsT.SetParent(root, false);
            var markers = new GameObject("Markers").transform;
            markers.SetParent(root, false);

            foreach (var p in pads)
            {
                float y = p.round ? HeightU(p.pos.x, p.pos.y) : 0f;
                Mk("PAD_" + p.def.name, padsT, W(p.pos, y), Look(p.fwd));
            }

            Mk("CAMPFIRE_Point", markers, Vector3.zero, Quaternion.identity);
            Mk("SPAWN_Player", markers, W(new Vector2(0f, -4.2f), 0f), Quaternion.LookRotation(Vector3.forward, Vector3.up));

            if (forest == null) return;

            Vector2 gate = forest.entry;
            Mk("ForestGate", markers, W(gate, HeightU(gate.x, gate.y)), Look(-forest.fwd));
        }

        // ---------------------------------------------------------- scene setup

        void SetupScene()
        {
            Light sun = null;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }

            if (sun == null)
            {
                var go = new GameObject("Directional Light");
                Undo.RegisterCreatedObjectUndo(go, "Build Location");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.95f, 0.82f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.8f;
                go.transform.rotation = Quaternion.Euler(50f, -32f, 0f);
                RenderSettings.sun = sun;
            }

            if (!s.setupLighting) return;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.76f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.62f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.30f, 0.26f);
            RenderSettings.fog = s.useFog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.84f, 0.95f);
            RenderSettings.fogStartDistance = 120f * ws;
            RenderSettings.fogEndDistance = 360f * ws;

            var sky = Shader.Find("Skybox/Procedural");
            if (sky != null)
            {
                string path = AssetDir + "/Materials/Mat_Sky.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(sky) { name = "Mat_Sky" };
                    AssetDatabase.CreateAsset(m, path);
                }
                else m.shader = sky;
                EditorUtility.SetDirty(m);
                RenderSettings.skybox = m;
            }
            DynamicGI.UpdateEnvironment();
        }
    }
}
#endif