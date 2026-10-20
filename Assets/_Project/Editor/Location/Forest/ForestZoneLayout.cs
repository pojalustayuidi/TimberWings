#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Forest/ForestZoneLayout.cs (Editor)
// ЕДИНСТВЕННОЕ место, где решается, ГДЕ стоят зоны леса. Раньше было два разных алгоритма
// (Bootstrap по границам деревьев и ForestZoneTools.LayoutInside), и они расходились.
//
// Правило: главная зона = зона леса (ForestZone Forest). Cedar, Birch и Autumn раскладываются ВНУТРИ неё:
//   - три круга по кольцу на половине радиуса, они не пересекаются друг с другом и не вылезают за край леса;
//   - Cedar (тёмный) в глубине, напротив входа; Birch и Autumn по бокам от входной тропы;
//   - вход берётся из маркера «ForestGate» (его делает LocationBuilder), иначе направление на центр острова.
// Зона прямоугольная или круглая, повёрнутая или нет: кольцо вписывается в эллипс зоны, радиусы считаются от меньшей стороны.
//
// Зона леса ищется через FindForestMapZone(): MapZone площадки леса из LocationBuilder («Кедровый Бор»).
using UnityEngine;

public static class ForestZoneLayout
{
    public struct Slot { public Vector3 pos; public float radius; }

    // кольцо центров (доля радиуса), углы и размеры кругов: Cedar, Birch, Autumn
    const float Ring = 0.5f;
    static readonly float[] AngleDeg = { 0f, 125f, -118f };
    static readonly float[] RadiusK = { 0.42f, 0.36f, 0.34f };

    static readonly string[] ForestWords = { "forest", "woods", "wood", "лес", "бор" };
    static readonly string[] SkipWords = { "dev", "дев", "тест", "test", "debug", "отлад" };

    // слоты Cedar [0], Birch [1], Autumn [2] внутри зоны master
    public static Slot[] Compute(MapZone master, Vector3 entryDir)
    {
        bool circle = master.shape == MapZone.Shape.Circle;
        float rx = circle ? master.radius : master.size.x * 0.5f;
        float rz = circle ? master.radius : master.size.y * 0.5f;
        float reff = Mathf.Min(rx, rz);
        float yaw = circle ? 0f : master.transform.eulerAngles.y;   // круг от поворота не зависит

        Vector3 el = Quaternion.Euler(0f, -yaw, 0f) * entryDir;
        el.y = 0f;
        if (el.sqrMagnitude < 1e-4f) el = Vector3.back;
        float phi0 = Mathf.Atan2(-el.z, -el.x);   // «вглубь» = от входа

        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        var res = new Slot[3];
        for (int i = 0; i < 3; i++)
        {
            float phi = phi0 + AngleDeg[i] * Mathf.Deg2Rad;
            Vector3 local = new Vector3(rx * Ring * Mathf.Cos(phi), 0f, rz * Ring * Mathf.Sin(phi));
            res[i].pos = master.transform.position + q * local;
            res[i].radius = Mathf.Max(2f, reff * RadiusK[i]);
        }
        return res;
    }

    // направление от точки к входу в лес (плоское, единичное)
    public static Vector3 EntryDir(Vector3 from)
    {
        var gate = GameObject.Find("ForestGate");
        Vector3 target = gate != null ? gate.transform.position : Vector3.zero;
        Vector3 d = target - from;
        d.y = 0f;
        return d.sqrMagnitude < 1f ? Vector3.back : d.normalized;
    }

    static bool HasWord(string s, string[] words, bool exact)
    {
        if (string.IsNullOrEmpty(s)) return false;
        string[] parts = s.ToLowerInvariant().Split(new[] { ' ', '_', '-', '.', '(', ')' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
            for (int k = 0; k < words.Length; k++)
            {
                if (parts[i] == words[k]) return true;
                if (!exact && parts[i].StartsWith(words[k])) return true;
            }
        return false;
    }

    // MapZone площадки леса (без ForestZone): по слову «лес / бор / forest» в имени зоны или объекта, целым словом
    // (так «забор» и «лесопилка» не подходят). Предпочитает зоны из группы «Zones», затем самую большую
    public static MapZone FindForestMapZone()
    {
        var all = Object.FindObjectsByType<MapZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        MapZone best = null; float bestScore = -1f;
        for (int i = 0; i < all.Length; i++)
        {
            var mz = all[i];
            if (mz == null || !mz.gameObject.scene.IsValid() || mz.GetComponent<ForestZone>() != null) continue;
            string full = mz.zoneName + " " + mz.gameObject.name;
            if (HasWord(full, SkipWords, false)) continue;
            if (!HasWord(full, ForestWords, false)) continue;
            float score = mz.Area + (mz.transform.parent != null && mz.transform.parent.name == "Zones" ? 100000f : 0f);
            if (score > bestScore) { bestScore = score; best = mz; }
        }
        return best;
    }
}
#endif
