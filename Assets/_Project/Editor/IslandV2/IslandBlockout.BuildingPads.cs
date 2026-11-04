// Assets/_Project/Editor/IslandV2/IslandBlockout.BuildingPads.cs
// ISL-017: декоративная окраска оснований зданий. Нет новых материалов, мешей или коллайдеров.
// Кэш строится ОДИН раз из сохранённого IslandLayout и реальных активных зданий в сцене.
// Пиксельный метод используется только при Editor-запекании IslandV2_GroundColor/Mask.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static partial class IslandBlockout
    {
        struct ISL017Pad
        {
            public Vector2 center;
            public Vector2 half;
            public Vector2 forward;
            public Vector2 right;
            public string name;
        }

        static readonly List<ISL017Pad> isl017Pads = new List<ISL017Pad>(8);
        static ISL017BuildingPadSettings isl017Settings;

        static bool ISL017Supported(string name)
        {
            return name == "PAD_Hut" || name == "PAD_Market" ||
                   name == "PAD_Tent" || name == "PAD_QuestBoard";
        }

        static void ISL017Add(Vector2 center, Vector2 footprint, float yaw, string name)
        {
            var forward = IslandField.Forward(yaw);
            isl017Pads.Add(new ISL017Pad
            {
                center = center,
                half = Vector2.Max(footprint * 0.5f, new Vector2(0.25f, 0.25f)),
                forward = forward,
                right = new Vector2(forward.y, -forward.x),
                name = name
            });
        }

        // Вызывается перед каждой перекраской, не во внутреннем пиксельном цикле.
        static void PrepareBuildingPadContexts(IslandField field)
        {
            isl017Pads.Clear();
            isl017Settings = ISL017BuildingPadSettings.GetExisting();
            if (isl017Settings == null || !isl017Settings.enabled ||
                isl017Settings.strength <= 0.0001f) return;

            var layout = field.L;
            for (int i = 0; i < layout.anchors.Count; i++)
            {
                var a = layout.anchors[i];
                if (a == null || a.kind != IslandLayout.AnchorKind.Building || !ISL017Supported(a.name))
                    continue;
                ISL017Add(field.AnchorWorld(a), a.footprint, a.yawDeg, a.name);
            }

            // В текущем аудите CampTent оказался далеко от PAD_Tent. Учитываем такой случай,
            // но НЕ подменяем сохранённую географию и не меняем Transform или ссылки.
            if (isl017Settings.includeSceneCampTent)
            {
                var anchorsTent = layout.FindAnchor("PAD_Tent");
                var tentAnchor = anchorsTent != null ? field.AnchorWorld(anchorsTent) : Vector2.zero;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (t.name != "CampTent" || !t.gameObject.activeInHierarchy) continue;
                    var p = new Vector2(t.position.x, t.position.z);
                    if (anchorsTent == null || Vector2.Distance(tentAnchor, p) > 7f)
                    {
                        // Фактическая площадка CampTent около 3.0 x 2.76 м в ISL-015 audit.
                        // Не читаем bounds декора/фонарей, чтобы площадка не разрасталась.
                        ISL017Add(p, new Vector2(3.0f, 2.8f), t.eulerAngles.y, "CampTent");
                    }
                    break;
                }
            }
        }

        // Возвращает изменённый цвет и долю подавления мелких "посыпок".
        // На дорогах итоговую окраску позже перезапишет существующее смешивание ISL-012.
        static Color ApplyBuildingPadDetails(Color baseColor, float x, float z, out float clean)
        {
            clean = 0f;
            var settings = isl017Settings;
            if (settings == null || !settings.enabled || isl017Pads.Count == 0 ||
                settings.strength <= 0.0001f) return baseColor;

            var p = new Vector2(x, z);
            float seedX = settings.seed * 0.01917f;
            float seedZ = settings.seed * 0.01371f;
            float scale = Mathf.Max(0.3f, settings.gravelScale);
            for (int i = 0; i < isl017Pads.Count; i++)
            {
                var a = isl017Pads[i];
                var d = p - a.center;
                float lx = Vector2.Dot(d, a.right);
                float lz = Vector2.Dot(d, a.forward);
                float bx = Mathf.Max(Mathf.Abs(lx) - a.half.x, 0f);
                float bz = Mathf.Max(Mathf.Abs(lz) - a.half.y, 0f);
                float outside = Mathf.Sqrt(bx * bx + bz * bz);
                float maxReach = settings.apronReach + 0.85f;
                float frontDistance = lz - a.half.y;
                bool nearApproach = frontDistance >= -0.25f &&
                                    frontDistance <= settings.approachLength + 0.35f &&
                                    Mathf.Abs(lx) <= settings.approachWidth * 0.5f + 0.30f;
                if (outside > maxReach && !nearApproach) continue;

                // Мягкий гравийный воротник, без чёткой прямоугольной рамки.
                // Декор главным образом СНАРУЖИ footprint, не под самим зданием.
                float ring = IslandField.Step(0.04f, 0.40f, outside) *
                             (1f - IslandField.Step(settings.apronReach * 0.60f, maxReach, outside));
                float broad = Mathf.PerlinNoise(x / 2.7f + seedX, z / 2.7f - seedZ);
                ring *= Mathf.Lerp(1f, IslandField.Step(0.30f, 0.78f, broad), settings.softEdgeVariation);
                float speckle = Mathf.PerlinNoise(x / scale + seedX + 11f, z / scale - seedZ + 7f);
                float fine = IslandField.Step(0.53f, 0.87f, speckle);
                Color gravel = Color.Lerp(settings.gravelLight, settings.gravelDark, fine * 0.65f);
                float gravelAlpha = ring * settings.gravelStrength * settings.strength * 0.40f;
                baseColor = Color.Lerp(baseColor, gravel, gravelAlpha);

                // Светлый ровный подход снаружи передней грани: ноль физической толщины.
                float front = lz - a.half.y;
                float frontBand = IslandField.Step(-0.25f, 0.08f, front) *
                                  (1f - IslandField.Step(settings.approachLength * 0.62f,
                                                          settings.approachLength + 0.35f, front));
                float sideBand = 1f - IslandField.Step(settings.approachWidth * 0.35f,
                                                       settings.approachWidth * 0.5f + 0.30f, Mathf.Abs(lx));
                float approach = frontBand * sideBand;
                float apronBlend = approach * settings.approachStrength * settings.strength * 0.55f;
                baseColor = Color.Lerp(baseColor, settings.approachColor, apronBlend);

                float cleaned = Mathf.Max(ring * 0.25f, approach);
                clean = Mathf.Max(clean, cleaned * settings.cleanGround * settings.strength);
            }
            return baseColor;
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-017/1. Открыть настройки оснований", false, 1710)]
        static void MenuISL017Settings()
        {
            var s = ISL017BuildingPadSettings.GetOrCreate();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-017/2. Включить основания", false, 1711)]
        static void MenuISL017Enable()
        {
            var s = ISL017BuildingPadSettings.GetOrCreate();
            Undo.RecordObject(s, "ISL-017: включить основания зданий");
            s.enabled = true;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-017: эффект включён. Теперь выполни Candy-земля: перекрасить (без пересборки).");
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-017/3. Выключить основания (откат)", false, 1712)]
        static void MenuISL017Disable()
        {
            var s = ISL017BuildingPadSettings.GetExisting();
            if (s == null) return;
            Undo.RecordObject(s, "ISL-017: выключить основания зданий");
            s.enabled = false;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-017: эффект выключен. Для возврата прежней окраски выполни Candy-земля: перекрасить (без пересборки).");
        }
    }
}
