#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/IslandBlockout.PlazaCandy.cs (Editor)
// Остров v2, Candy-центр: только ЦЕНТР площадки (мозаика вокруг костра). Часть partial-класса IslandBlockout.
// Всё остальное на площадке (бордюр, флагшток, фонари, гирлянда, цветы) остаётся из IslandBlockout.Plaza.cs без изменений.
//
// ВЕРСИЯ 2 («центр читается»): раньше вокруг огня лежали десятки мелких пастельных деталей (жемчуг, 12 лепестков с сердечками, 12 лучей),
// они спорили с костром и давали шум. Теперь композиция одна и ведёт взгляд к огню, от тёмного к светлому:
//   тёмная чаша костра (её строит CandyCampfire.HearthGround) -> тёплый обод из «кирпичиков» -> солнце из двух звёзд
//   (контур терракота, 12 золотых лучей, внутри персиковая звезда) -> 12 жемчужин на кончиках внутренней звезды -> венок из облачков по краю.
// Лавки и пни стоят прямо на солнце и читаются на нём, поэтому подушки на лавках остаются пастельными.
//   1. обод ямы: 28 плоских кирпичиков r=1,62, терракота и кремовый по очереди (закрывает стык земли и плит);
//   2. солнце: терракотовый контур (r 3,45 / 2,15), золотая звезда на 12 лучей (r 3,33 / 2,05), персиковая звезда поменьше (r 2,55 / 1,78, повёрнута на 15 градусов);
//   3. жемчужины на кончиках персиковой звезды (r 2,62);
//   4. венок из 24 облачных кружков по краю мозаики (r 3,82) и белых жемчужинок между ними;
//   5. четыре золотые блёстки по сторонам света на краю площади (южная под ногами у игрока, там, где он встаёт при стройке).
// Один меш PlazaCandy_Mesh, один материал Candy_Camp (как у костра, 1 draw call), коллайдеров нет.
//
// Подключение (одна строка в IslandBlockout.cs, в Build, сразу после «int plazaTris = BuildCampPlaza(field, gen, mat);»):
//     BuildPlazaCandy(field, gen);
// Без неё мозаика собирается только из меню, а «2. Собрать блокаут» пересоздаёт _Generated и её сотрёт.
//
// Меню:
//   Tools/BuilderSim/Локация/Остров v2/Центр Candy: пересобрать только мозаику                (135)
//   Tools/BuilderSim/Локация/Остров v2/Центр Candy: мозаика + костёр (всё сразу)             (136)
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static partial class IslandBlockout
    {
        public const string PlazaCandyName = "CampPlazaCandy";

        static readonly Color pcCream = SoftMesh.C(0xFFF1CF), pcWhite = SoftMesh.C(0xFFFAF0), pcGold = SoftMesh.C(0xF3C77A),
            pcTerra = SoftMesh.C(0xD98C5F), pcPeach = SoftMesh.C(0xFFCFA6), pcOutline = SoftMesh.C(0xC98458);
        static readonly Color[] pcPastel = { SoftMesh.C(0xFFB5CC), SoftMesh.C(0xCDB8F0), SoftMesh.C(0xFFC9A8), SoftMesh.C(0xA6E8CB) };

        static Vector3 PcPolar(IslandField f, Vector2 c, float r, float a, float dy)
        {
            float x = c.x + Mathf.Cos(a) * r, z = c.y + Mathf.Sin(a) * r;
            return new Vector3(x, f.Height(x, z) + dy, z);
        }

        // плоская звезда-КОЛЬЦО на земле: снаружи лучи, внутри круглая дырка радиуса rHole (там тёмная чаша костра), лицом вверх
        static void PcStar(SoftMesh s, IslandField f, Vector2 c, int points, float rOut, float rIn, float rHole, float rot, float dy, Color col)
        {
            int n = points * 2;
            var outer = new Vector3[n];
            var inner = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = rot + Mathf.PI * i / points;
                outer[i] = PcPolar(f, c, (i & 1) == 0 ? rOut : rIn, a, dy);
                inner[i] = PcPolar(f, c, rHole, a, dy);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                s.Quad(inner[i], outer[i], outer[j], inner[j], col, Vector3.up);
            }
        }

        // ---------------------------------------------------------------- сборка
        public static int BuildPlazaCandy(IslandField f, Transform gen)
        {
            Vector2 c = f.PlazaCenter;
            var s = new SoftMesh();
            int parts = PcMosaic(s, f, c);

            var mat = CandyCampfire.CampMaterial();
            var root = new GameObject(PlazaCandyName);
            Undo.RegisterCreatedObjectUndo(root, "Create " + PlazaCandyName);
            root.transform.SetParent(gen, false);
            NewMeshObject("PlazaCandy_Mesh", root.transform, s.ToMesh("IslandPlazaCandy"), mat, true);

            Debug.Log("IslandV2: мозаика центра Candy построена: Tris " + s.Tris + ", элементов " + parts + ". Объект " + PlazaCandyName + " в _Generated.");
            return s.Tris;
        }

        // Всё лежит чуть выше плит (плиты +0.07): звёзды +0.088..0.100, кирпичики и кружки толщиной ~0.05.
        static int PcMosaic(SoftMesh s, IslandField f, Vector2 c)
        {
            int n = 0;
            const float deg = Mathf.PI / 180f;

            // 1. обод костровой ямы: плоские кирпичики, длинной стороной вдоль окружности
            const int nB = 28;
            for (int i = 0; i < nB; i++)
            {
                float a = (i + 0.5f) / nB * Mathf.PI * 2f;
                s.Puff(PcPolar(f, c, 1.62f, a, 0.085f), new Vector3(0.20f, 0.05f, 0.105f), (i & 1) == 0 ? pcTerra : pcCream, -a * Mathf.Rad2Deg + 90f, 8, 3, 0.2f);
                n++;
            }

            // 2. солнце: контур, золотая звезда на 12 лучей, внутри персиковая
            PcStar(s, f, c, 12, 3.45f, 2.15f, 1.66f, 0f, 0.084f, pcOutline);
            PcStar(s, f, c, 12, 3.33f, 2.05f, 1.66f, 0f, 0.090f, pcGold);
            PcStar(s, f, c, 12, 2.55f, 1.78f, 1.70f, 15f * deg, 0.097f, pcPeach);
            n += 3;

            // 3. жемчужины на кончиках персиковой звезды
            for (int k = 0; k < 12; k++)
            {
                float a = (15f + k * 30f) * deg;
                s.Puff(PcPolar(f, c, 2.62f, a, 0.10f), new Vector3(0.09f, 0.055f, 0.09f), pcWhite, 0f, 6, 3, 0.2f);
                n++;
            }

            // 4. венок по краю мозаики: цветные облачные кружки и белые жемчужинки между ними
            const int nC = 24;
            for (int i = 0; i < nC; i++)
            {
                float a = (i + 0.5f) / nC * Mathf.PI * 2f;
                s.Puff(PcPolar(f, c, 3.82f, a, 0.085f), new Vector3(0.26f, 0.05f, 0.26f), pcPastel[i % 4], 0f, 9, 3, 0.25f);
                s.Puff(PcPolar(f, c, 3.82f, a + Mathf.PI / nC, 0.085f), new Vector3(0.07f, 0.055f, 0.07f), pcWhite, 0f, 6, 3, 0.2f);
                n += 2;
            }

            // 5. золотые блёстки по сторонам света (плоские, на 1,5 см выше плит)
            for (int k = 0; k < 4; k++)
            {
                float a = k * 90f * deg;
                s.Sparkle(PcPolar(f, c, 4.30f, a, 0.085f), Vector3.right, Vector3.forward, 0.34f, pcGold);
                n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- меню
        static bool RebuildPlazaCandy()
        {
            if (Application.isPlaying) { Debug.LogError("IslandV2: мозаика центра собирается вне Play Mode."); return false; }
            var rootGo = FindRoot();
            var gen = rootGo != null ? rootGo.transform.Find(GeneratedName) : null;
            if (gen == null) { Debug.LogError("IslandV2: нет IslandV2/_Generated. Сначала «2. Собрать блокаут»."); return false; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Остров v2: мозаика центра Candy");
            int group = Undo.GetCurrentGroup();

            var old = gen.Find(PlazaCandyName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            BuildPlazaCandy(new IslandField(IslandLayout.GetOrCreate()), gen);

            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rootGo.scene);
            return true;
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Центр Candy: пересобрать только мозаику", false, 135)]
        static void MenuRebuildPlazaCandy() { RebuildPlazaCandy(); }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Центр Candy: мозаика + костёр (всё сразу)", false, 136)]
        static void MenuCandyCenterAll()
        {
            if (!RebuildPlazaCandy()) return;
            CandyCampfire.Build(true);
        }
    }
}
#endif
