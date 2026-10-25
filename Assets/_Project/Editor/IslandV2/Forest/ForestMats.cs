#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestMats.cs (Editor)
// Общие помощники для «красоты леса» (A5): материалы TreeToon своего масштаба и сохранение мешей.
// Библиотека TreeToonSetup рассчитана на деревья 5-8 м (градиент по высоте 1.6-8 м), для кустов и кедра-великана нужны свои числа.
// Материалы лежат в Assets/_Project/Prefabs/Trees/Generated/Materials (рядом с остальными) и создаются один раз;
// force = true пересоздаёт числа у уже существующих (нужно, если поменял цвета в коде).
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class ForestMats
    {
        public const string MatDir = "Assets/_Project/Prefabs/Trees/Generated/Materials";

        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        // имя, цвет сверху, цвет снизу, высота градиента от (м), до (м), дрожь листвы, рим, сила градиента
        public static Material Get(string name, Color top, Color bottom, float g0, float g1, float flutter, float rim, float gradStrength, bool force)
        {
            Shader sh = TreeToonSetup.LoadShader();
            if (sh == null) return null;
            EditorAssets.EnsureFolder(MatDir);
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = false;
            if (m == null)
            {
                m = new Material(sh) { name = name };
                AssetDatabase.CreateAsset(m, path);
                created = true;
            }
            else if (m.shader != sh) { m.shader = sh; created = true; }
            m.enableInstancing = true;
            if (created || force)
            {
                m.SetColor("_BaseColor", top);
                m.SetColor("_BottomColor", bottom);
                m.SetFloat("_GradMin", g0);
                m.SetFloat("_GradMax", g1);
                m.SetFloat("_GradStrength", gradStrength);
                m.SetFloat("_Flutter", flutter);
                m.SetFloat("_RimStrength", rim);
                m.SetFloat("_Step1", 0.55f);
                m.SetFloat("_Step2", 0.82f);
                m.SetFloat("_StepSoft", 0.05f);
                m.SetFloat("_Level0", 0.42f);
                m.SetFloat("_Level1", 0.72f);
                m.SetFloat("_Level2", 0.98f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- набор «Дедушки» v4 «Cloud Candy»: вековое дерево 31 м в сладких пастельных тонах (референс: облачная крона лаванда / розовый / мята).
        // Цвета НЕ золотые (золото = бонусное дерево GoldenTreeFx). Градиент кроны 8-31 м: низ глубже, верх светлее, поэтому облака объёмные.
        // 0 кора (тёплая лилово-серая, у корней темнее), 1 то же (запас сабмеша), 2 тёмное дупло
        public static Material[] Grandpa(bool force)
        {
            var bark = Get("Grandpa_Bark", C(0.74f, 0.62f, 0.68f), C(0.30f, 0.21f, 0.30f), 0f, 13f, 0.000f, 0.08f, 1f, force);
            return new[]
            {
                bark,
                bark,
                Get("Grandpa_Hollow", C(0.22f, 0.14f, 0.30f), C(0.07f, 0.04f, 0.13f), 0f, 5f, 0.000f, 0.00f, 1f, force)
            };
        }

        // крона Дедушки: 0 лаванда, 1 розовый, 2 мята, 3 сливочный (светлые «взбитые» макушки)
        public static Material[] GrandpaCrown(bool force)
        {
            return new[]
            {
                Get("Grandpa_CrownLavender", C(0.80f, 0.70f, 0.98f), C(0.48f, 0.38f, 0.78f),  8f, 31f, 0.012f, 0.14f, 1f, force),
                Get("Grandpa_CrownPink",     C(1.00f, 0.77f, 0.90f), C(0.76f, 0.46f, 0.72f),  8f, 31f, 0.012f, 0.14f, 1f, force),
                Get("Grandpa_CrownMint",     C(0.72f, 0.97f, 0.88f), C(0.36f, 0.68f, 0.74f),  8f, 31f, 0.012f, 0.14f, 1f, force),
                Get("Grandpa_CrownCream",    C(1.00f, 0.97f, 0.98f), C(0.80f, 0.74f, 0.92f),  8f, 31f, 0.010f, 0.16f, 0.9f, force)
            };
        }

        // «тайна» Дедушки v4: 0 жемчужное сухое дерево (сучья, обломанная ветка), 1 светящиеся кристаллы (бирюза-лаванда, БЕЗ градиента: светятся ровно),
        // 2 бледно-мятный лишайник-«борода», 3 старые камни (кэрны у дупла)
        public static Material[] GrandpaMystic(bool force)
        {
            return new[]
            {
                Get("Grandpa_Dead",    C(0.90f, 0.86f, 0.94f), C(0.46f, 0.40f, 0.52f),  0f, 31f, 0.000f, 0.10f, 0.8f, force),
                Get("Grandpa_Crystal", C(0.74f, 0.96f, 1.00f), C(0.58f, 0.78f, 1.00f),  0f, 31f, 0.000f, 0.00f, 0f,   force),
                Get("Grandpa_Beard",   C(0.80f, 0.96f, 0.86f), C(0.44f, 0.68f, 0.62f),  0f, 31f, 0.030f, 0.10f, 0.8f, force),
                Get("Grandpa_Stone",   C(0.76f, 0.74f, 0.84f), C(0.38f, 0.37f, 0.50f),  0f,  1.6f, 0.000f, 0.12f, 1f, force)
            };
        }

        // ---- поляна: магия (руны на камнях, светящиеся грибы), мшистые камни
        public static Material GroveRune(bool f)      { return Get("Grove_Rune",      C(0.55f, 1.00f, 0.88f), C(0.30f, 0.88f, 0.82f), 0f, 3.5f, 0f,    0.00f, 0.00f, f); }
        public static Material GroveShroomCap(bool f) { return Get("Grove_ShroomCap", C(0.62f, 0.98f, 1.00f), C(0.36f, 0.72f, 0.96f), 0f, 1.0f, 0f,    0.06f, 0.30f, f); }
        public static Material GroveShroomStem(bool f){ return Get("Grove_ShroomStem",C(0.96f, 0.93f, 0.84f), C(0.74f, 0.70f, 0.62f), 0f, 1.0f, 0f,    0.04f, 0.50f, f); }
        public static Material GroveStoneMoss(bool f) { return Get("Grove_StoneMoss", C(0.72f, 0.74f, 0.70f), C(0.32f, 0.35f, 0.34f), 0f, 3.2f, 0f,    0.12f, 1.00f, f); }

        // ---- поляна рубки (A5-красота): пеньки, поленница, тёплые огоньки, папоротник
        public static Material GladeLog(bool f)       { return Get("Glade_Log",       C(0.70f, 0.50f, 0.30f), C(0.34f, 0.22f, 0.14f), 0f, 1.6f, 0f,    0.06f, 0.60f, f); }
        public static Material GladeLogEnd(bool f)    { return Get("Glade_LogEnd",    C(0.96f, 0.80f, 0.54f), C(0.84f, 0.64f, 0.40f), 0f, 1.6f, 0f,    0.02f, 0.20f, f); }
        public static Material GladeFern(bool f)      { return Get("Glade_Fern",      C(0.50f, 0.84f, 0.30f), C(0.12f, 0.42f, 0.18f), 0f, 1.0f, 0.04f, 0.10f, 1.00f, f); }

        // мелкие детали Дедушки: 0 мятный мох и лианы, 1 жемчужное цветение, 2 розовые грибки (candy), 3 тёплый (фонарики, лампочки, светлые грибы)
        public static Material[] GrandpaDetail(bool force)
        {
            return new[]
            {
                Get("Grandpa_Moss",      C(0.70f, 0.94f, 0.62f), C(0.26f, 0.56f, 0.40f),  0f, 12f, 0.010f, 0.08f, 0.7f, force),
                Get("Grandpa_Blossom",   C(1.00f, 0.98f, 0.96f), C(0.94f, 0.80f, 0.92f),  8f, 31f, 0.020f, 0.12f, 0.9f, force),
                Get("Grandpa_ShroomRed", C(1.00f, 0.62f, 0.80f), C(0.78f, 0.34f, 0.62f),  0f,  5f, 0.000f, 0.10f, 0.8f, force),
                Get("Grandpa_Warm",      C(1.00f, 0.90f, 0.58f), C(1.00f, 0.74f, 0.42f),  0f, 30f, 0.000f, 0.00f, 0f,   force)
            };
        }

        // ---- поляна Дедушки: дерево, камень, железо, тёплое стекло фонарей
        public static Material GroveWood(bool f)     { return Get("Grove_Wood",     C(0.74f, 0.54f, 0.34f), C(0.36f, 0.24f, 0.16f), 0f, 2.6f, 0f, 0.06f, 0.55f, f); }
        public static Material GroveWoodDark(bool f) { return Get("Grove_WoodDark", C(0.46f, 0.32f, 0.21f), C(0.20f, 0.13f, 0.09f), 0f, 3.0f, 0f, 0.04f, 0.60f, f); }
        public static Material GroveIron(bool f)     { return Get("Grove_Iron",     C(0.34f, 0.33f, 0.42f), C(0.14f, 0.14f, 0.20f), 0f, 3.0f, 0f, 0.10f, 0.70f, f); }
        public static Material GroveWarm(bool f)     { return Get("Grove_Warm",     C(1.00f, 0.90f, 0.56f), C(1.00f, 0.74f, 0.40f), 0f, 3.0f, 0f, 0.00f, 0.00f, f); }

        // ---- подлесок: кусты, цветы, камни (метры, высота кустов до ~1.2 м)
        public static Material BushLime(bool f)  { return Get("Under_BushLime",  C(0.66f, 0.88f, 0.24f), C(0.16f, 0.46f, 0.14f), 0f, 1.1f, 0.020f, 0.12f, 1f, f); }
        public static Material BushDark(bool f)  { return Get("Under_BushDark",  C(0.26f, 0.64f, 0.42f), C(0.05f, 0.26f, 0.22f), 0f, 1.1f, 0.015f, 0.10f, 1f, f); }
        public static Material BushUnder(bool f) { return Get("Under_BushUnder", C(0.14f, 0.40f, 0.12f), C(0.03f, 0.15f, 0.09f), 0f, 1.1f, 0.000f, 0.00f, 1f, f); }
        public static Material Stem(bool f)      { return Get("Under_Stem",      C(0.40f, 0.74f, 0.22f), C(0.12f, 0.40f, 0.12f), 0f, 0.5f, 0.030f, 0.08f, 1f, f); }
        public static Material FlowerPink(bool f)   { return Get("Under_FlowerPink",   C(1.00f, 0.56f, 0.72f), C(0.90f, 0.40f, 0.60f), 0f, 1f, 0.02f, 0.10f, 0f, f); }
        public static Material FlowerWhite(bool f)  { return Get("Under_FlowerWhite",  C(1.00f, 0.98f, 0.92f), C(0.88f, 0.86f, 0.80f), 0f, 1f, 0.02f, 0.10f, 0f, f); }
        public static Material FlowerYellow(bool f) { return Get("Under_FlowerYellow", C(1.00f, 0.86f, 0.18f), C(0.92f, 0.70f, 0.10f), 0f, 1f, 0.02f, 0.10f, 0f, f); }
        public static Material FlowerPurple(bool f) { return Get("Under_FlowerPurple", C(0.72f, 0.52f, 0.95f), C(0.58f, 0.40f, 0.82f), 0f, 1f, 0.02f, 0.10f, 0f, f); }
        public static Material Rock(bool f)      { return Get("Under_Rock",      C(0.78f, 0.74f, 0.88f), C(0.46f, 0.43f, 0.62f), 0f, 1.3f, 0.000f, 0.16f, 1f, f); }

        // ---- сохранение меша как ассета по пути (тот же путь = тот же GUID)
        public static Mesh SaveMesh(Mesh m, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                EditorUtility.CopySerialized(m, old);
                Object.DestroyImmediate(m);
                EditorUtility.SetDirty(old);
                return old;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
#endif
