#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Forest/ForestGlades.cs (Editor)
// A5: поляны рубки. Вокруг маркеров PAD_Forest (6, 44) и PAD_GroveSouth (10, -50) лес РАЗРЕЖЕН, а не вырезан: деревья стоят реже (рубить есть что),
// в самом центре свободный пятачок, где стоит игрок. Работает через карту плотности ForestDensityGen (Eval): множитель плотности по расстоянию до маркера.
//
//   ForestGlade        строка списка settings.gladeList: имя маркера, радиус, мягкий край, во сколько раз реже в центре (thin), свободный круг (stand)
//   ForestGladeInst    то же, но уже с координатами маркера (маркер ищется по имени в сцене при каждой генерации, поэтому переезд метки учитывается сам)
//   ForestSettings     (partial) поля choppingGlades, gladeList, useIslandV2Meadows; GatherGlades, AddIslandMeadows, GladesReport
//   ForestGlades       меню «Tools/BuilderSim/Лес/Поляны рубки: применить A5»: зона берёзовой рощи у PAD_GroveSouth + густая чаща в «карманах» змейки к мосту
// Кроме поляны, генератору нужна лесная зона (ForestZone): деревья ставятся только внутри зон. PAD_Forest должен лежать в главной зоне леса,
// для PAD_GroveSouth меню создаёт отдельную круглую зону Birch («Южная роща»).
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[Serializable]
public class ForestGlade
{
    [Tooltip("Имя маркера в сцене (Island v2: Markers/PAD_Forest, Markers/PAD_GroveSouth)")]
    public string marker = "PAD_Forest";
    [Tooltip("Радиус поляны, м")]
    [Min(3f)] public float radius = 14f;
    [Tooltip("Ширина мягкого края внутрь радиуса, м")]
    [Min(0f)] public float feather = 6f;
    [Tooltip("Плотность леса в центре поляны относительно обычной: 0.3 = в три раза реже, 1 = без разрежения")]
    [Range(0f, 1f)] public float thin = 0.3f;
    [Tooltip("Свободный круг вокруг маркера, где деревьев нет совсем (место игрока), м")]
    [Min(0f)] public float stand = 2.5f;
}

public struct ForestGladeInst
{
    public string name;
    public Vector2 c;
    public float y, r, feather, stand, thin;
}

public partial class ForestSettings
{
    [Header("Поляны рубки (A5)")] [DensityOnly]
    [Tooltip("Разрежать лес вокруг маркеров из списка ниже. Выключено = поляны рубки не учитываются")]
    public bool choppingGlades = true;
    [DensityOnly] [Tooltip("Поляны рубки: по одной строке на маркер. Маркера нет в сцене = строка пропускается")]
    public List<ForestGlade> gladeList = new List<ForestGlade>
    {
        // PAD_Forest: маленькая достопримечательность, вокруг почти пусто (там 5-6 рабочих деревьев, ForestBackdrop)
        new ForestGlade { marker = "PAD_Forest", radius = 10f, feather = 4f, thin = 0.10f, stand = 2.5f },
        // PAD_GroveSouth: основная зона рубки, лес вокруг НЕ разрежаем (thin = 1), внутри ClearR всё ставит ChopZone
        new ForestGlade { marker = "PAD_GroveSouth", radius = 12f, feather = 5f, thin = 1f, stand = 2.5f }
    };
    [DensityOnly] [Tooltip("Поляны блокаута острова v2 (список meadows в IslandLayout: лагерь, «Дедушка», поляна и берег Гуннара, участок B) считать полянами без деревьев и в настоящем лесу")]
    public bool useIslandV2Meadows = true;

    public List<ForestGladeInst> GatherGlades()
    {
        var res = new List<ForestGladeInst>();
        if (!choppingGlades || gladeList == null) return res;
        for (int i = 0; i < gladeList.Count; i++)
        {
            var g = gladeList[i];
            if (g == null || string.IsNullOrEmpty(g.marker)) continue;
            var go = GameObject.Find(g.marker);
            if (go == null) continue;
            Vector3 p = go.transform.position;
            float r = Mathf.Max(3f, g.radius);
            res.Add(new ForestGladeInst { name = g.marker, c = new Vector2(p.x, p.z), y = p.y, r = r, feather = Mathf.Min(Mathf.Max(0f, g.feather), r), stand = Mathf.Max(0f, g.stand), thin = Mathf.Clamp01(g.thin) });
        }
        return res;
    }

    string GladesReport(DCtx d)
    {
        if (!choppingGlades) return "выключены";
        var sb = new StringBuilder();
        var found = new HashSet<string>();
        if (d.glades != null)
            for (int i = 0; i < d.glades.Count; i++)
            {
                found.Add(d.glades[i].name);
                sb.Append((sb.Length > 0 ? ", " : "") + d.glades[i].name + " R=" + d.glades[i].r.ToString("0.#") + " м");
            }
        if (gladeList != null)
            for (int i = 0; i < gladeList.Count; i++)
                if (gladeList[i] != null && !string.IsNullOrEmpty(gladeList[i].marker) && !found.Contains(gladeList[i].marker))
                    sb.Append((sb.Length > 0 ? ", " : "") + "нет маркера " + gladeList[i].marker);
        return sb.Length > 0 ? sb.ToString() : "нет";
    }

    void AddIslandMeadows(List<ForestMaskInst> res, float refY)
    {
        if (!useIslandV2Meadows || GameObject.Find("IslandV2") == null) return;
        var layout = AssetDatabase.LoadAssetAtPath<IslandV2.IslandLayout>(IslandV2.IslandLayout.AssetPath);
        if (layout == null) return;
        for (int i = 0; i < layout.meadows.Count; i++)
        {
            var m = layout.meadows[i];
            res.Add(new ForestMaskInst { kind = ForestMaskKind.Clearing, c = new Vector3(m.pos.x, refY, m.pos.y), r = m.radius, feather = m.radius * 0.35f, strength = 1f });
        }
    }
}

public static class ForestGlades
{
    const string ZonesRootName = "Forest Zones";
    const string SouthZoneName = "Birch_South";

    // центры «карманов» между ветками змейки к мосту (тропа «Мост» из IslandZones): там чаща, чтобы ветки нельзя было срезать
    static readonly Vector2[] SnakePockets = { new Vector2(56f, -2f), new Vector2(58f, 10f) };

    [MenuItem("Tools/BuilderSim/Лес/Поляны рубки: применить A5", false, 201)]
    static void Apply()
    {
        var sb = new StringBuilder("ForestGlades (A5)\n");
        var s = ForestSettings.Load();
        Undo.RecordObject(s, "Лес: поляны рубки A5");

        s.useDensityMap = true;
        s.choppingGlades = true;
        s.useIslandV2Meadows = true;
        if (s.gladeList == null) s.gladeList = new List<ForestGlade>();
        EnsureGlade(s, "PAD_Forest", 10f, 4f, 0.10f, sb);
        EnsureGlade(s, "PAD_GroveSouth", 12f, 5f, 1f, sb);

        // густая чаща в карманах змейки (маски карты плотности, список settings.masks)
        if (s.masks == null) s.masks = new List<ForestMask>();
        foreach (var c in SnakePockets)
        {
            bool have = false;
            for (int i = 0; i < s.masks.Count && !have; i++)
                if (s.masks[i] != null && s.masks[i].kind == ForestMaskKind.Thicket && Vector2.Distance(s.masks[i].center, c) < 2f) have = true;
            if (have) { sb.AppendLine("  чаща в кармане (" + c.x.ToString("0") + ", " + c.y.ToString("0") + ") уже есть"); continue; }
            s.masks.Add(new ForestMask { kind = ForestMaskKind.Thicket, center = c, radius = 5f, feather = 3f, strength = 1f });
            sb.AppendLine("  добавлена чаща в кармане змейки (" + c.x.ToString("0") + ", " + c.y.ToString("0") + "), R 5 м");
        }
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssets();

        // PAD_Forest должен лежать в главной лесной зоне
        Vector3 pf;
        if (TryMarker("PAD_Forest", out pf))
        {
            var z = ForestZone.At(pf);
            if (z == null) sb.AppendLine("  [!] PAD_Forest (" + pf.x.ToString("0") + ", " + pf.z.ToString("0") + ") вне всех лесных зон: деревьев на поляне не будет. Подвинь или растяни зону «Лес» (ForestZone, биом Forest) так, чтобы маркер был внутри");
            else sb.AppendLine("  PAD_Forest лежит в лесной зоне «" + z.Map.zoneName + "» (" + z.biome + ")");
        }
        else sb.AppendLine("  [!] в сцене нет маркера PAD_Forest (собери блокаут острова v2, пункт 2)");

        sb.AppendLine("  " + EnsureSouthZone());
        sb.AppendLine("Дальше: Tools → BuilderSim → Лес → сгенерировать деревья; в Scene View жёлтые круги = поляны рубки, зелёные = чаща.");
        Debug.Log(sb.ToString());
    }

    static void EnsureGlade(ForestSettings s, string marker, float radius, float feather, float thin, StringBuilder sb)
    {
        for (int i = 0; i < s.gladeList.Count; i++)
            if (s.gladeList[i] != null && s.gladeList[i].marker == marker)
            {
                // строка уже есть в ассете: приводим к новым значениям (иначе старые 14 м / 0.30 / 0.35 остались бы)
                var g = s.gladeList[i];
                g.radius = radius; g.feather = feather; g.thin = thin;
                sb.AppendLine("  поляна " + marker + " обновлена: R " + radius.ToString("0.#") + " м, thin " + thin.ToString("0.##"));
                return;
            }
        s.gladeList.Add(new ForestGlade { marker = marker, radius = radius, feather = feather, thin = thin, stand = 2.5f });
        sb.AppendLine("  добавлена поляна рубки " + marker);
    }

    static bool TryMarker(string name, out Vector3 p)
    {
        var go = GameObject.Find(name);
        p = go != null ? go.transform.position : Vector3.zero;
        return go != null;
    }

    // отдельная круглая зона берёзового леса вокруг PAD_GroveSouth (зон биома Birch может быть несколько: генератор берёт все)
    static string EnsureSouthZone()
    {
        Vector3 p;
        if (!TryMarker("PAD_GroveSouth", out p)) return "[!] в сцене нет маркера PAD_GroveSouth (собери блокаут острова v2, пункт 2): зону южной рощи не создал";
        var at = ForestZone.At(p);
        if (at != null) return "PAD_GroveSouth уже лежит в лесной зоне «" + at.Map.zoneName + "» (" + at.biome + "), новую не создаю";

        float radius = 16f;
        var layout = AssetDatabase.LoadAssetAtPath<IslandV2.IslandLayout>(IslandV2.IslandLayout.AssetPath);
        if (layout != null)
        {
            var f = new IslandV2.IslandField(layout);
            radius = Mathf.Clamp(f.EdgeDistance(p.x, p.z) - 2f, 8f, 18f);   // зона не вылезает за край острова
        }

        var root = GameObject.Find(ZonesRootName);
        if (root == null)
        {
            root = new GameObject(ZonesRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create " + ZonesRootName);
        }
        var go = new GameObject(SouthZoneName);
        Undo.RegisterCreatedObjectUndo(go, "Create south grove zone");
        go.transform.SetParent(root.transform, false);
        go.transform.position = p;
        var fz = Undo.AddComponent<ForestZone>(go);
        fz.biome = ForestBiome.Birch;
        var mz = fz.Map;
        mz.zoneName = "Южная роща"; mz.caption = "СВЕТЛЫЙ ЛЕС"; mz.priority = 20; mz.color = ForestZones.BiomeColor(ForestBiome.Birch); mz.announce = true;
        mz.shape = MapZone.Shape.Circle; mz.radius = radius;
        EditorUtility.SetDirty(fz);
        EditorSceneManager.MarkSceneDirty(go.scene);
        return "создана лесная зона Birch «Южная роща» R " + radius.ToString("0.#") + " м в (" + p.x.ToString("0") + ", " + p.z.ToString("0") + ")";
    }
}
#endif
