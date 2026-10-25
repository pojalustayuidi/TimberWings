// Assets/_Project/Scripts/Forest/MysticGlade/MysticGladeBuilder.Props.cs (НЕ в Editor)
// Реквизит поляны: рунные плиты, менгиры, кристальные ворота, скамейки, кристаллы у корней, светящиеся грибы.
using UnityEngine;

public partial class MysticGladeBuilder
{
    // ---------------------------------------------------------------- кристаллы (светятся: меш glowCrystal + ореол в вершине)
    void Crystal(Vector3 b, float h, Color col)
    {
        int n = glowCrystal.rng.Next(3, 6);
        for (int i = 0; i < n; i++)
        {
            float a = i * 2.4f;
            float lean = i == 0 ? 0f : glowCrystal.Range(12f, 26f);
            float hh = i == 0 ? h : h * glowCrystal.Range(0.45f, 0.8f);
            float w = hh * 0.16f;
            float off = i == 0 ? 0f : 0.12f * h;
            glowCrystal.Push(b + new Vector3(Mathf.Cos(a) * off, 0f, Mathf.Sin(a) * off),
                             Quaternion.Euler(lean * Mathf.Sin(a), 0f, -lean * Mathf.Cos(a)));
            glowCrystal.Octa(new Vector3(0f, hh, 0f), new Vector3(w, hh, w), glowCrystal.Jit(col, 0.06f), 0.03f);
            glowCrystal.Pop();
        }
        crystalPts.Add(b + Vector3.up * h * 1.1f);
    }

    Color RandCrystal() { return MysticPal.Crystals[glowCrystal.rng.Next(MysticPal.Crystals.Length)]; }

    // ---------------------------------------------------------------- кольцо рунных плит вокруг дерева
    void BuildRuneRing()
    {
        int n = 18;
        for (int i = 0; i < n; i++)
        {
            float deg = i * 360f / n;
            Vector2 p = Pos(deg, runeRadius);
            float y = Y(p.x, p.y) + 0.02f;
            float yaw = deg + props.Range(-10f, 10f);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            props.Push(new Vector3(p.x, y, p.y), rot);
            props.Blob(new Vector3(0f, 0.05f, 0f), new Vector3(0.62f, 0.11f, 0.46f), i % 2 == 0 ? MysticPal.Stone : MysticPal.StoneDark, 0.10f, 0.06f);
            props.Pop();
            glowRune.Push(new Vector3(p.x, y, p.y), rot);
            glowRune.Octa(new Vector3(0f, 0.14f, 0f), new Vector3(0.15f, 0.03f, 0.15f), i % 3 == 0 ? MysticPal.Violet : MysticPal.Cyan, 0.03f);
            glowRune.Pop();
        }
        Occupy(0f, 0f, runeRadius + 0.7f);   // внутри кольца ничего лишнего не ставим
    }

    // ---------------------------------------------------------------- менгиры между скамейками (проход к входу свободен)
    void BuildMenhirs()
    {
        int n = 6;
        for (int i = 1; i < n; i++)
        {
            Vector2 p = Pos(entryDeg + 360f / n * i, benchRadius + 3.5f);
            Menhir(p.x, p.y);
        }
    }

    void Menhir(float x, float z)
    {
        float y = Y(x, z);
        float h = props.Range(2.6f, 3.6f);
        props.Cylinder(new Vector3(x, y - 0.1f, z), 0.85f, 0.55f, h, 6, props.Jit(MysticPal.Stone, 0.05f), null, null, true, true, 0.04f);
        props.Blob(new Vector3(x, y + h, z), new Vector3(0.6f, 0.42f, 0.6f), MysticPal.Stone, 0.15f, 0.08f);
        props.Blob(new Vector3(x + 0.8f, y + 0.1f, z + 0.3f), new Vector3(0.45f, 0.30f, 0.40f), MysticPal.StoneDark, 0.15f, 0.08f);
        props.Blob(new Vector3(x - 0.6f, y + 0.08f, z - 0.6f), new Vector3(0.35f, 0.24f, 0.35f), MysticPal.Stone, 0.15f, 0.08f);

        Crystal(new Vector3(x, y + h + 0.25f, z), 0.9f, RandCrystal());

        // руны на стороне, обращённой к дереву
        Vector3 toC = new Vector3(-x, 0f, -z).normalized;
        for (int k = 0; k < 3; k++)
        {
            float yy = 0.9f + k * 0.55f;
            float rad = Mathf.Lerp(0.85f, 0.55f, yy / h) + 0.04f;
            Vector3 pos = new Vector3(x, y + yy, z) + toC * rad;
            glowRune.Push(pos, Quaternion.LookRotation(toC));
            glowRune.Box(Vector3.zero, new Vector3(k == 1 ? 0.34f : 0.12f, 0.1f, 0.03f), MysticPal.Cyan, 0f);
            glowRune.Pop();
        }
        AddCapsuleCollider("Col_Menhir", new Vector3(x, y + 1.4f, z), 0.7f, 2.8f);
        Occupy(x, z, 1.4f);
    }

    // ---------------------------------------------------------------- ворота: два кристальных столба у входа
    void BuildGate()
    {
        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 p = Pos(entryDeg + s * 13f, benchRadius + 3.5f);
            float y = Y(p.x, p.y);
            props.Cylinder(new Vector3(p.x, y - 0.1f, p.y), 0.5f, 0.38f, 3.4f, 7, MysticPal.Wood, null, null, true, true, 0.04f);
            props.Cylinder(new Vector3(p.x, y + 0.9f, p.y), 0.56f, 0.56f, 0.12f, 7, MysticPal.WoodLight, null, null, true, true, 0.02f);
            props.Cylinder(new Vector3(p.x, y + 2.4f, p.y), 0.46f, 0.46f, 0.12f, 7, MysticPal.WoodLight, null, null, true, true, 0.02f);
            props.Blob(new Vector3(p.x + 0.5f, y + 0.1f, p.y + 0.2f), new Vector3(0.4f, 0.28f, 0.36f), MysticPal.Stone, 0.15f, 0.08f);
            Crystal(new Vector3(p.x, y + 3.45f, p.y), 1.1f, s < 0 ? MysticPal.Violet : MysticPal.Cyan);
            AddCapsuleCollider("Col_GatePost", new Vector3(p.x, y + 1.7f, p.y), 0.55f, 3.4f);
            Occupy(p.x, p.y, 1.3f);
        }
    }

    // ---------------------------------------------------------------- скамейки: лицом к дереву, между ними просветы
    void BuildBenches()
    {
        float step = 360f / Mathf.Max(1, benchCount);
        for (int i = 0; i < benchCount; i++)
        {
            Vector2 p = Pos(entryDeg + step * 0.5f + step * i, benchRadius);
            Bench(p.x, p.y);
        }
    }

    void Bench(float x, float z)
    {
        float y = Y(x, z);
        Vector3 toC = new Vector3(-x, 0f, -z).normalized;
        Quaternion rot = Quaternion.LookRotation(toC);   // локальный +Z смотрит на дерево
        Color wood = MysticPal.Wood, woodL = MysticPal.WoodLight;

        props.Push(new Vector3(x, y, z), rot);
        for (int s = -1; s <= 1; s += 2)
        {
            props.Box(new Vector3(s * 0.85f, 0.22f, 0f), new Vector3(0.22f, 0.44f, 0.55f), MysticPal.Stone, 0.04f);          // каменные опоры
            props.Box(new Vector3(s * 0.85f, 0.62f, -0.28f), new Vector3(0.10f, 0.50f, 0.10f), wood, 0.03f);                 // стойки спинки
        }
        for (int k = -1; k <= 1; k++)
            props.Box(new Vector3(0f, 0.47f, k * 0.19f), new Vector3(2.1f, 0.07f, 0.17f), k == 0 ? woodL : wood, 0.03f);    // сиденье
        props.Push(new Vector3(0f, 0.74f, -0.31f), Quaternion.Euler(-12f, 0f, 0f));
        props.Box(new Vector3(0f, 0.00f, 0f), new Vector3(2.1f, 0.14f, 0.06f), wood, 0.03f);                                 // спинка
        props.Box(new Vector3(0f, 0.20f, 0f), new Vector3(2.1f, 0.14f, 0.06f), woodL, 0.03f);
        props.Pop();
        props.Pop();

        // по маленькому кристаллу на концах
        for (int s = -1; s <= 1; s += 2)
            Crystal(new Vector3(x, y, z) + rot * new Vector3(s * 0.98f, 0.46f, -0.28f), 0.26f, s < 0 ? MysticPal.Pink : MysticPal.Cyan);

        AddBoxCollider("Col_Bench", new Vector3(x, y + 0.5f, z), new Vector3(2.2f, 1.0f, 0.9f), Mathf.Atan2(toC.x, toC.z) * Mathf.Rad2Deg);
        Occupy(x, z, 1.3f);
    }

    // ---------------------------------------------------------------- кристаллы у корней рубимых деревьев
    void BuildTreeRootCrystals()
    {
        int n = Mathf.RoundToInt(8 * scatterDensity);
        float x, z;
        for (int i = 0; i < n; i++)
        {
            if (!Spot(treeRadius - 3f, treeRadius + 3f, 1.3f, out x, out z)) continue;
            float y = Y(x, z);
            props.Blob(new Vector3(x, y + 0.12f, z), new Vector3(0.55f, 0.28f, 0.45f), MysticPal.Stone, 0.15f, 0.08f);
            Crystal(new Vector3(x, y + 0.1f, z), props.Range(0.5f, 0.9f), RandCrystal());
            Occupy(x, z, 0.9f);
        }
    }

    // ---------------------------------------------------------------- светящиеся грибы
    void BuildShrooms()
    {
        int n = Mathf.RoundToInt(16 * scatterDensity);
        float x, z;
        for (int i = 0; i < n; i++)
        {
            if (!Spot(runeRadius + 1.2f, treeRadius + 1.5f, 0.9f, out x, out z)) continue;
            bool pink = props.Rand() < 0.55f;
            int m = props.rng.Next(3, 6);
            for (int k = 0; k < m; k++)
            {
                float sx = x + props.Range(-0.3f, 0.3f), sz = z + props.Range(-0.3f, 0.3f);
                float y = Y(sx, sz), s = props.Range(0.7f, 1.3f) * (k == 0 ? 1.4f : 1f);
                float h = 0.28f * s;
                props.Cylinder(new Vector3(sx, y - 0.02f, sz), 0.05f * s, 0.035f * s, h, 5, MysticPal.Cream, null, null, false, false, 0.04f);
                float cap = 0.15f * s;
                glowShroom.Blob(new Vector3(sx, y + h, sz), new Vector3(cap, cap * 0.55f, cap), pink ? MysticPal.Pink : MysticPal.Cyan, 0.10f, 0.06f);
                if (k == 0) shroomPts.Add(new Vector3(sx, y + h + cap * 0.4f, sz));
            }
            Occupy(x, z, 0.7f);
        }
    }
}
