// Assets/_Project/Editor/IslandV2/IslandBlockout.CandyGround.cs
// Остров v2: Candy-земля (сахарный, мультяшный, воздушный вид). Часть partial-класса IslandBlockout. Числа и цвета: IslandCandy.asset (класс IslandCandy).
//
// Зачем: старая раскраска давала резкие контуры пятен травы и тропу-ленту одной ширины без объёма. Здесь:
//   * трава: плавный градиент мята -> зелень -> лайм -> солнечный по двум шумам, мягкая зернистость;
//   * лес: светлая мятная подстилка с «солнечными зайчиками», а не тёмное пятно;
//   * тропа: кремовая лента с персиковым краем, белой сахарной кромкой, живым краем (шум по ширине) и лёгкими розоватыми разводами;
//   * контактная тень: вокруг тропы трава чуть уходит в холодную мяту (цветная «окклюзия»), тропа выглядит приподнятой;
//   * сахарный песок по берегу воды, мягкие площадки зданий;
//   * маска _GroundMask (RGBA): R,G = уклон «подушки» тропы (по нему шейдер наклоняет нормаль), B = плотность посыпки, A = глянец.
// Геометрия, коллайдеры, Height(), проходимость, деревья и здания НЕ меняются: меняются только текстуры и шейдер.
//
// Методы:
//   CandyPixel(field, settings, x, z)         один пиксель: цвет (sRGB), высота «подушки» 0..1, глянец, плотность посыпки
//   MenuCandyRepaint()                        меню 133: перезапечь текстуры земли без пересборки блокаута
//   MenuCandySettings()                       меню 134: выделить ассет настроек IslandCandy
// Запекание текстур (цвет и маска) и запись в материал: IslandBlockout.GroundTex.cs (BakeGroundTexture).
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static partial class IslandBlockout
    {
        struct CandyPix
        {
            public Color color;      // sRGB, альфа 1
            public float height;     // «подушка» тропы 0..1 (для уклона в маске)
            public float gloss;      // 0..1 глянец
            public float sprinkle;   // 0..1 где можно сыпать посыпку
        }

        static Color CScale(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

        static CandyPix CandyPixel(IslandField f, IslandCandy s, float x, float z)
        {
            var L = f.L;
            var o = new CandyPix();

            // 1. трава: плавный градиент по двум шумам разного размера
            float gs = Mathf.Max(6f, s.grassScale);
            float n1 = Mathf.PerlinNoise(x / gs + 7.1f, z / gs + 3.3f);
            float n2 = Mathf.PerlinNoise(x / (gs * 0.4f) + 21.7f, z / (gs * 0.4f) + 9.4f);
            float t = Mathf.Clamp01(0.5f + (n1 * 0.75f + n2 * 0.25f - 0.5f) * s.grassContrast);
            Color c = s.GrassAt(t);
            float grain = 1f + (Mathf.PerlinNoise(x * 1.15f + 41.3f, z * 1.15f + 17.9f) - 0.5f) * 2f * s.grassGrain;
            c = CScale(c, grain);

            // 2. лес: светлая мятная подстилка, внутри «солнечные зайчики»
            float fw = f.ForestWeight(x, z) * s.forestStrength;
            c = Color.Lerp(c, s.forestFloor, fw);
            if (fw > 0.01f)
                c = CScale(c, 1f + (Mathf.PerlinNoise(x * 0.45f + 3.1f, z * 0.45f + 8.2f) - 0.5f) * 0.18f * fw);

            // 3. сахарный берег (WaterDistance > 0 снаружи воды; в воде и на броде берега нет)
            float wd = f.WaterDistance(x, z);
            if (wd > 0f && wd < s.beachWidth + 1.2f)
            {
                float beach = (1f - IslandField.Step(s.beachWidth * 0.55f, s.beachWidth + 1.2f, wd)) * s.beachStrength;
                c = Color.Lerp(c, s.beachColor, beach);
            }

            // 4. площадки зданий: мягкое тёплое пятно
            float pad = 0f;
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.kind != IslandLayout.AnchorKind.Building) continue;
                if (a.name == "CAMPFIRE_Point") continue;   // у костра своя площадка ниже
                float rd = f.RectDistance(a, x, z);
                pad = Mathf.Max(pad, 1f - IslandField.Step(0.3f, 2.4f, rd));
            }
            c = Color.Lerp(c, s.padColor, pad * s.padStrength);

            // 5. тропа: edge < 0 на тропе; на развилках расширяется (как раньше); край «дышит» шумом
            float d1, d2;
            f.PathEdgeNearest(x, z, out d1, out d2);
            float junction = 1f - IslandField.Step(0f, Mathf.Max(0.1f, L.pathJunctionReach), Mathf.Max(0f, d2));
            float baseEdge = d1 - L.pathJunctionWiden * junction;
            // Шум действует только у ВИДИМОЙ кромки. Центр тропы и игровая раскладка не меняются.
            float edgeNoiseBand = 1f - IslandField.Step(0.45f, 1.6f, Mathf.Abs(baseEdge));
            float wob = (Mathf.PerlinNoise(x * 0.21f + 5.7f, z * 0.21f + 1.9f) - 0.5f) * s.pathWobble
                      + (Mathf.PerlinNoise(x * 0.7f + 12.1f, z * 0.7f + 4.4f) - 0.5f) * s.pathWobble * s.pathFineNoise;
            float edge = baseEdge + wob * edgeNoiseBand;

            // 5а. контактная тень на траве вокруг тропы: холодная мята вместо серого затемнения
            float contact = IslandField.Step(-0.2f, 0.35f, edge) * (1f - IslandField.Step(0.35f, 0.35f + s.contactWidth, edge)) * s.contactShadow;
            c = Color.Lerp(c, s.contactColor, contact);

            // 5б. сама лента: центр кремовый, к краю персиковый, розоватые разводы, белая кромка
            float feather = Mathf.Clamp(s.pathFeather, 0.25f, 1.25f);
            float cover = 1f - IslandField.Step(-feather * 0.65f, feather * 0.35f, edge);
            float toEdge = IslandField.Step(-1.1f, -0.1f, edge);
            Color pc = Color.Lerp(s.pathCream, s.pathEdge, toEdge);
            float blush = IslandField.Step(0.62f, 0.80f, Mathf.PerlinNoise(x / 9f + 31.4f, z / 9f + 2.7f));
            pc = Color.Lerp(pc, s.pathBlush, blush * s.pathBlushStrength);
            float rimBand = 1f - IslandField.Step(0.05f, 0.5f, Mathf.Abs(edge + 0.25f));
            pc = Color.Lerp(pc, s.pathRim, rimBand * s.pathRimStrength);
            pc = CScale(pc, 1f + (Mathf.PerlinNoise(x * 0.9f + 5.2f, z * 0.9f + 8.8f) - 0.5f) * 0.05f);
            c = Color.Lerp(c, pc, cover);

            // «подушка» тропы: 1 на ленте, мягко до 0 за краем (по ней шейдер наклоняет нормаль)
            float h = 1f - IslandField.Step(-0.5f * s.pathPillow, 0.5f * s.pathPillow + 0.15f, edge);

            // 6. круглая площадка у костра (под мощением, видна у самого костра и по краю)
            float pd = Vector2.Distance(new Vector2(x, z), f.PlazaCenter);
            float R = Mathf.Max(1f, L.plazaRadius);
            float ring = IslandField.Step(R - L.plazaRimWidth - 0.2f, R - L.plazaRimWidth + 0.15f, pd);
            Color stone = Color.Lerp(s.plazaStone, s.plazaRim, ring);
            float plaza = 1f - IslandField.Step(R - 0.15f, R + 0.2f, pd);
            c = Color.Lerp(c, stone, plaza);
            h *= 1f - plaza;

            // 7. брод и стены русла (как в старой раскраске)
            float rdist = f.RiverDistance(x, z);
            float half = L.riverWidth * 0.5f;
            float fw2 = f.FordWeight(x, z);
            float inRiver = 1f - IslandField.Step(half - 0.5f, half + 0.5f, rdist);
            c = Color.Lerp(c, s.fordSand, inRiver * fw2);
            h *= 1f - inRiver;
            if (rdist < half + 16f && rdist > half * 0.85f)
            {
                float sl = f.Slope(x, z);
                float wallW = IslandField.Step(24f, 42f, sl);
                if (wallW > 0.001f)
                {
                    float dy = f.Height(x, z) - f.RiverSurfaceAt(x, z);
                    Color rock = Color.Lerp(L.riverWallColor, L.riverWallWet, 1f - IslandField.Step(0.4f, 1.8f, dy));
                    c = Color.Lerp(c, rock, wallW);
                }
            }

            // 8. кромка обрыва: чуть темнее, чтобы шов с обрывом не светился (слабее, чем раньше: воздушнее)
            float ed = f.EdgeDistance(x, z);
            if (ed < L.edgeDarkWidth)
            {
                float k = 1f - IslandField.Step(0f, Mathf.Max(0.5f, L.edgeDarkWidth), Mathf.Max(0f, ed));
                c = CScale(c, 1f - L.edgeDarken * s.edgeDarkScale * k);
            }

            // 9. вода под водяными мешами (русло без брода и пруд)
            c = Color.Lerp(c, L.waterColor, inRiver * (1f - fw2));
            float pv = f.PondValue(x, z);
            float pondW = 1f - IslandField.Step(0.9f, 1.08f, pv);
            c = Color.Lerp(c, L.waterColor, pondW);
            float water = Mathf.Max(inRiver * (1f - fw2), pondW);

            // маска: глянец и где можно сыпать посыпку
            o.color = new Color(c.r, c.g, c.b, 1f);
            o.height = h * (1f - water);
            o.gloss = Mathf.Lerp(s.glossGrass, s.glossPath, o.height) * (1f - water);
            float spr = 1f;
            spr *= 1f - fw * 0.6f;
            spr *= 1f - pad * s.padStrength * 0.8f;
            spr *= 1f - plaza;
            spr *= 1f - water;
            spr *= 1f - inRiver * fw2;
            spr *= IslandField.Step(0.5f, 2.5f, Mathf.Max(0f, ed));
            spr *= Mathf.Lerp(1f, 0.6f, o.height);
            o.sprinkle = Mathf.Clamp01(spr);
            return o;
        }

        // ================================================================ меню
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Candy-земля: перекрасить (без пересборки)", false, 133)]
        static void MenuCandyRepaint()
        {
            if (Application.isPlaying) { Debug.LogError("IslandV2: Candy-землю перекрашиваем вне Play Mode."); return; }
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("IslandV2: не найден шейдер " + ShaderName + " (файл Shaders/IslandV2/IslandVertexColor.shader). Положи новую версию шейдера в проект и дождись компиляции.");
                return;
            }
            var layout = IslandLayout.GetOrCreate();
            var field = new IslandField(layout);
            if (field.Contour.Count < 3) { Debug.LogError("IslandV2: контур пустой, проверь раскладку."); return; }

            Material mat = GetMaterial(shader);
            if (!BakeGroundTexture(field, mat)) return;  // отмена не является успешной перекраской
            AssetDatabase.SaveAssets();
            Debug.Log("IslandV2: Candy-земля перекрашена. Геометрию пересобирать не нужно: материал общий, сцена обновится сама. Ползунки вида: материал Mat_IslandV2 (" + MaterialPath + "), цвета: " + IslandCandy.AssetPath + ".");
        }

        // ISL-006: пресет затрагивает ТОЛЬКО вид тропы в IslandCandy.asset.
        // Ни IslandLayout, ни меши, ни TerrainData, ни расстановка объектов не меняются.
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Candy-земля: ISL-006 пресет тропинок", false, 135)]
        static void MenuIsl006PathPreset()
        {
            if (Application.isPlaying) { Debug.LogError("ISL-006: сначала выйди из Play Mode."); return; }
            var s = IslandCandy.GetOrCreate();
            if (!s.candyOn)
            {
                Debug.LogError("ISL-006: IslandCandy.candyOn выключен. Пресет не применён: сначала включи Candy-землю осознанно, затем повтори.");
                return;
            }

            Color cream, edge, blush, rim, contact;
            if (!ColorUtility.TryParseHtmlString("#F8E7BE", out cream) ||
                !ColorUtility.TryParseHtmlString("#EDCB9D", out edge) ||
                !ColorUtility.TryParseHtmlString("#F6D4C7", out blush) ||
                !ColorUtility.TryParseHtmlString("#F9E9C9", out rim) ||
                !ColorUtility.TryParseHtmlString("#6FC997", out contact))
            {
                Debug.LogError("ISL-006: не удалось прочитать цвета пресета. Настройки не изменены.");
                return;
            }

            Undo.RecordObject(s, "ISL-006: пресет центральных тропинок");
            s.pathCream = cream;
            s.pathEdge = edge;
            s.pathBlush = blush;
            s.pathRim = rim;
            s.pathRimStrength = 0.30f;
            s.pathFeather = 0.75f;
            s.pathWobble = 0.50f;
            s.pathFineNoise = 0.18f;
            s.pathBlushStrength = 0.12f;
            s.contactColor = contact;
            s.contactShadow = 0.18f;
            s.contactWidth = 0.9f;
            s.pathPillow = 1.0f;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-006: параметры центральных тропинок записаны в " + IslandCandy.AssetPath +
                      ". Теперь запусти «Candy-земля: перекрасить (без пересборки)» (меню 133). География не менялась.");
            Selection.activeObject = s;
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Candy-земля: настройки (цвета)", false, 134)]
        static void MenuCandySettings()
        {
            var s = IslandCandy.GetOrCreate();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }
    }
}
