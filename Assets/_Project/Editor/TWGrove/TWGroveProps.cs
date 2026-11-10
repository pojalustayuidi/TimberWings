using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IslandV2
{
    internal static class TWGroveProps
    {
        static readonly Color White = Color.white;

        static void Box(TWGroveModel m, string mat, Vector3 c, Vector3 size, float yaw = 0f)
        { m.Part(mat).Box(c, size, Quaternion.Euler(0, yaw, 0), White); }
        static void Beam(TWGroveModel m, string mat, Vector3 a, Vector3 b, float radius)
        { m.Part(mat).Cyl(a, b, radius, radius * 0.94f, White, 7); }
        static void Stone(TWGroveModel m, Vector3 p, Vector3 size, float yaw)
        { m.Part("Stone").Puff(p, size, White, yaw, 7, 3, 0.12f); }
        static void Moss(TWGroveModel m, Vector3 p, Vector3 size, float yaw)
        { m.Part("Leaf").Puff(p, size, White, yaw, 7, 3, 0.1f); }

        static void Log(TWGroveModel m, Vector3 a, Vector3 b, float r)
        {
            Vector3 dir = (b - a).normalized;
            m.Part("Bark").Cyl(a, b, r, r * 0.94f, White, 9);
            foreach (var end in new[] { a - dir * 0.004f, b + dir * 0.004f })
            {
                m.Part("EndGrain").Cyl(end - dir * 0.005f, end + dir * 0.005f, r * 0.85f, r * 0.85f, White, 9);
                // Nested end-grain disks keep the rings legible from the game camera.
                m.Part("Wood").Cyl(end - dir * 0.008f, end + dir * 0.008f, r * 0.54f, r * 0.54f, White, 9);
                m.Part("EndGrain").Cyl(end - dir * 0.012f, end + dir * 0.012f, r * 0.44f, r * 0.44f, White, 9);
                m.Part("Wood").Cyl(end - dir * 0.015f, end + dir * 0.015f, r * 0.13f, r * 0.13f, White, 7);
            }
        }

        static void Axe(TWGroveModel m, Vector3 p, float scale = 1f)
        {
            Beam(m, "Wood", p, p + new Vector3(0.18f, 1.05f, 0) * scale, 0.045f * scale);
            m.Part("Metal").Puff(p + new Vector3(0.23f, 0.9f, 0) * scale, new Vector3(0.29f, 0.17f, 0.055f) * scale, White, 0, 5, 3, 0);
        }

        static void Wing(TWGroveModel m, Vector3 p, float size, int side)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 a = p + new Vector3(side * (0.05f + i * 0.09f), 0.07f - i * 0.07f, 0);
                Vector3 b = a + new Vector3(side * (0.37f - i * 0.04f), 0.2f, 0) * size;
                Beam(m, "EndGrain", a, b, 0.065f * size);
            }
        }

        static void Rope(TWGroveModel m, Vector3 a, Vector3 b, float sag)
        {
            Vector3 last = a;
            for (int i = 1; i <= 16; i++)
            {
                float t = i / 16f; Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * (4 * sag * t * (1 - t));
                Beam(m, "Rope", last, p, 0.022f); last = p;
            }
        }

        static void Collider(GameObject parent, Vector3 centre, Vector3 size, bool navigation = true)
        {
            var c = parent.AddComponent<BoxCollider>(); c.center = centre; c.size = size;
            if (!navigation) return;
            var obstacle = parent.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = centre; obstacle.size = size; obstacle.carving = true; obstacle.carveOnlyStationary = true;
        }

        static void Lamp(TWGroveAssets assets, Transform parent, string key, Vector3 p, bool simulation = false)
        {
            var m = new TWGroveModel();
            Box(m, "DarkWood", new Vector3(0, 0.22f, 0), new Vector3(0.4f, 0.07f, 0.4f));
            Box(m, "DarkWood", new Vector3(0, -0.22f, 0), new Vector3(0.4f, 0.07f, 0.4f));
            for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2)
                Beam(m, "Metal", new Vector3(a * 0.16f, -0.21f, b * 0.16f), new Vector3(a * 0.16f, 0.22f, b * 0.16f), 0.024f);
            Beam(m, "Metal", new Vector3(0, 0.22f, 0), new Vector3(0, 0.44f, 0), 0.03f);
            var go = m.Finish(assets, parent, key + "_Frame", parent.TransformPoint(p), parent.rotation);
            var glow = new TWGroveModel(); Box(glow, "Glass", Vector3.zero, new Vector3(0.27f, 0.33f, 0.27f));
            var glass = glow.Finish(assets, go.transform, key + "_WarmGlass", go.transform.position, go.transform.rotation);
            var fire = go.AddComponent<FireSource>(); fire.preset = FlickerPreset.Lantern; fire.intensity = 1.45f; fire.range = 4.6f;
            fire.color = new Color(1, 0.76f, 0.42f); fire.glowSize = 0.55f; fire.simOnly = simulation;
            fire.emissive = new[] { glass.GetComponent<Renderer>() };
        }

        static void Story(GameObject go, string title, string prompt, params string[] lines)
        {
            var story = go.AddComponent<GroveChopBlock>(); story.title = title; story.prompt = prompt; story.lines = lines;
            story.radius = 1.9f; story.toastColor = new Color(0.8f, 0.9f, 0.72f);
        }

        internal static GameObject Finish(TWGroveContext x, TWGroveAssets assets, TWGroveModel m, string key, string name, Vector3 p, Quaternion q,
            Vector2 half, float h, bool blocks, string description)
        {
            var go = m.Finish(assets, x.root, key, p, q); go.name = name;
            var handle = go.AddComponent<GroveArtHandle>(); handle.key = key; handle.description = description;
            handle.halfSize = half; handle.height = h; handle.blocksWalking = blocks;
            handle.defaultPosition = go.transform.position; handle.defaultRotation = go.transform.rotation; handle.defaultScale = go.transform.localScale;
            return go;
        }

        public static GameObject Gate(TWGroveContext x, TWGroveAssets assets)
        {
            if (Reuse(x, "Gate", out GameObject keptGate)) return keptGate;
            Vector2 half = new Vector2(2.95f, 0.65f);
            if (!x.Place(new Vector2(0, -12.1f), half, 0, 4.6f, out Vector3 p, out Quaternion q, true))
                throw new InvalidOperationException("Не найдено свободное место для входной арки. Старые деревья сохранены.");
            var m = new TWGroveModel();
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 foot = new Vector3(side * 2.45f, -0.13f, 0);
                Beam(m, "DarkWood", foot, foot + new Vector3(side * 0.07f, 3.65f, 0), 0.24f);
                Stone(m, foot + Vector3.up * 0.19f, new Vector3(0.43f, 0.27f, 0.43f), side * 13);
                Beam(m, "Wood", new Vector3(side * 2.44f, 2.5f, 0), new Vector3(side * 1.56f, 3.47f, 0), 0.11f);
                for (int i = 0; i < 4; i++)
                    Box(m, "Rope", new Vector3(side * 2.45f, 2.42f + i * 0.055f, -0.01f), new Vector3(0.52f, 0.035f, 0.53f));
                Beam(m, "Wood", new Vector3(side * 2.45f, 3.12f, -0.03f), new Vector3(side * 1.96f, 3.12f, -0.35f), 0.065f);
            }
            Beam(m, "Wood", new Vector3(-2.93f, 3.45f, 0), new Vector3(0, 3.79f, 0), 0.27f);
            Beam(m, "Wood", new Vector3(0, 3.79f, 0), new Vector3(2.93f, 3.49f, 0), 0.25f);
            for (int i = 0; i < 11; i++)
            {
                float a = i / 10f;
                Moss(m, new Vector3(Mathf.Lerp(-2.9f, 2.9f, a), 3.69f + 0.22f * Mathf.Sin(a * Mathf.PI), 0), new Vector3(0.43f, 0.14f, 0.42f), i * 29);
            }
            Box(m, "DarkWood", new Vector3(0, 3.4f, -0.26f), new Vector3(1.18f, 0.62f, 0.15f));
            Axe(m, new Vector3(-0.08f, 3.13f, -0.39f), 0.53f);
            Wing(m, new Vector3(-0.12f, 3.43f, -0.4f), 0.85f, -1); Wing(m, new Vector3(0.18f, 3.43f, -0.4f), 0.85f, 1);
            var go = Finish(x, assets, m, "Gate", "01_Арка_Крылатый_топор", p, q, half, 4.2f, true, "Главный вход. Проход 4,4 м; двигай арку целиком вместе с фонарями.");
            foreach (int side in new[] { -1, 1 })
            {
                var post = new GameObject("Столб_" + side); post.transform.SetParent(go.transform, false);
                Collider(post, new Vector3(side * 2.45f, 1.73f, 0), new Vector3(0.49f, 3.65f, 0.49f));
                Lamp(assets, go.transform, "GateLamp_" + side, new Vector3(side * 1.97f, 2.59f, -0.35f), side > 0);
            }
            Story(go, "Южная роща", "осмотреть знак", "Крылатый топор. Здесь рубят с умом, а не с размаху.", "На обороте зарубка: «После третьего звона проверь старый камень».");
            // The arch is open in the middle; reserve its posts, never a solid wall across the entrance.
            x.occupied.RemoveAt(x.occupied.Count - 1);
            Vector2 centre = x.Local(p);
            foreach (int side in new[] { -1, 1 }) x.occupied.Add(new TWGroveContext.Footprint { point = centre + new Vector2(side * 2.45f, 0), half = new Vector2(0.34f, 0.34f) });
            return go;
        }

        public static void Build(TWGroveContext x, TWGroveAssets assets)
        {
            Place(x, assets, "Shed", "02_Навес_мастерская", new Vector2(-6.5f, 3.4f), new Vector2(1.9f, 1.45f), 35, 3.5f, Shed,
                go => { Collider(go, new Vector3(0, 0.65f, 0.35f), new Vector3(3.1f, 1.3f, 1.3f)); Lamp(assets, go.transform, "WorkshopLamp", new Vector3(1.25f, 2.25f, -0.9f)); });
            Place(x, assets, "Cart", "03_Тележка_с_брёвнами", new Vector2(6f, -2.1f), new Vector2(1.15f, 1.55f), -25, 1.8f, Cart,
                go => Collider(go, new Vector3(0, 0.7f, -0.1f), new Vector3(1.9f, 1.35f, 2.3f)));
            Place(x, assets, "AxeStump", "04_Рабочий_пень_с_топором", new Vector2(-4.8f, -3.9f), new Vector2(0.85f, 0.85f), -15, 1.7f, Stump,
                go => { Collider(go, new Vector3(0, 0.37f, 0), new Vector3(1.05f, 0.74f, 1.05f)); Story(go, "Пень мастера", "проверить топор", "Заточен. Следующий герой пусть принесёт свой топор.", "На рукояти три зарубки — как на старом камне в глубине рощи."); });
            Place(x, assets, "LogRack", "05_Поленница_на_козлах", new Vector2(6.7f, 5.5f), new Vector2(1.75f, 0.9f), -24, 1.8f, LogRack,
                go => Collider(go, new Vector3(0, 0.7f, 0), new Vector3(3.3f, 1.4f, 1.45f)));
            Place(x, assets, "Bench", "06_Лавка_перекур", new Vector2(-3.5f, 5.6f), new Vector2(1.2f, 0.6f), 148, 1.2f, Bench,
                go => Collider(go, new Vector3(0, 0.4f, 0), new Vector3(2.2f, 0.8f, 0.7f)));
            Place(x, assets, "OldMark", "07_Камень_трёх_зарубок", new Vector2(3.1f, 10.7f), new Vector2(0.85f, 0.7f), 175, 1.4f, OldMark,
                go => { Collider(go, new Vector3(0, 0.55f, 0), new Vector3(1.1f, 1.1f, 0.65f)); Story(go, "Три зарубки", "разглядеть метки", "Под мхом — крыло, песочные часы и три зарубки.", "Камень чуть тёплый. На табличке: «Лес помнит время, которого у нас не было»."); });
            Place(x, assets, "DuckStool", "08_Пень_который_не_рубят", new Vector2(8.2f, 7.5f), new Vector2(0.65f, 0.65f), -60, 1.1f, DuckStool,
                go => { Collider(go, new Vector3(0, 0.35f, 0), new Vector3(0.75f, 0.7f, 0.75f)); Story(go, "Служебный пень", "прочитать записку", "«НЕ РУБИТЬ. Это стул. — Мира»", "Деревянная утка выглядит очень занятой. Видимо, начальник участка."); });
            Place(x, assets, "LostClock", "09_Потерянная_стрелка", new Vector2(-9.5f, 5.8f), new Vector2(0.55f, 0.55f), 32, 0.8f, LostClock,
                go => Story(go, "Чужая минута", "осмотреть находку", "Медная стрелка в корнях. Тот же знак, что на арке и у костра.", "Она двигается только тогда, когда на неё не смотришь. Наверное."));
            Border(x, assets);
        }

        static void Place(TWGroveContext x, TWGroveAssets assets, string key, string name, Vector2 desired, Vector2 half, float yaw, float height,
            Func<TWGroveModel> model, Action<GameObject> finish)
        {
            if (Reuse(x, key, out GameObject kept)) return;
            if (!x.Place(desired, half, yaw, height, out Vector3 p, out Quaternion q))
            { Debug.LogWarning("GROVE ART: «" + name + "» пропущен: нет безопасного места между деревьями и стройками."); return; }
            var go = Finish(x, assets, model(), key, name, p, q, half, height, key != "LostClock", "Отдельный объект: свободно перемещается, Play Mode сохраняет его положение.");
            finish(go); x.earthPatches.Add(new TWGroveContext.Circle { point = x.Local(p), radius = Mathf.Min(2.3f, half.magnitude + 0.4f) });
        }

        internal static bool Reuse(TWGroveContext x, string key, out GameObject go)
        {
            go = null;
            if (!x.keep.TryGetValue(key, out GroveArtHandle handle) || handle == null) return false;
            go = handle.gameObject;
            // Keep the actual edited object, including custom children/components,
            // rather than recreating it and only copying a transform.
            Undo.SetTransformParent(go.transform, x.root, "Preserve manually edited grove prop");
            Vector2 p = x.Local(go.transform.position); Vector3 scale = go.transform.lossyScale;
            float yaw = (Quaternion.Inverse(x.rotation) * go.transform.rotation).eulerAngles.y;
            Vector2 half = Vector2.Scale(handle.halfSize, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            if (key == "Gate")
                foreach (int side in new[] { -1, 1 })
                    x.occupied.Add(new TWGroveContext.Footprint { point = x.Local(go.transform.TransformPoint(new Vector3(side * 2.45f, 0, 0))), half = new Vector2(0.34f, 0.34f), yaw = yaw });
            else if (handle.blocksWalking && go.activeSelf) x.occupied.Add(new TWGroveContext.Footprint { point = p, half = half, yaw = yaw });
            return true;
        }

        static TWGroveModel Shed()
        {
            var m = new TWGroveModel();
            foreach (int s in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                Beam(m, "DarkWood", new Vector3(s * 1.55f, -0.1f, z * 1.05f), new Vector3(s * 1.55f, z < 0 ? 2.65f : 2.35f, z * 1.05f), 0.12f);
            for (int i = 0; i < 12; i++)
                m.Part("Wood").Box(new Vector3(-1.78f + i * 0.325f, 2.63f, 0), new Vector3(0.30f, 0.13f, 2.65f), Quaternion.Euler(7, 0, 0), White);
            Beam(m, "DarkWood", new Vector3(-1.68f, 2.52f, -1.07f), new Vector3(1.68f, 2.52f, -1.07f), 0.13f);
            Beam(m, "Wood", new Vector3(-1.55f, 1.8f, 1.05f), new Vector3(-0.8f, 2.35f, 1.05f), 0.07f);
            Beam(m, "Wood", new Vector3(1.55f, 1.8f, 1.05f), new Vector3(0.8f, 2.35f, 1.05f), 0.07f);
            for (int i = 0; i < 5; i++) Box(m, "Wood", new Vector3(0, 0.95f, -0.32f + i * 0.19f), new Vector3(2.65f, 0.1f, 0.17f));
            foreach (int s in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 }) Box(m, "DarkWood", new Vector3(s, 0.43f, 0.1f + z * 0.3f), new Vector3(0.16f, 0.96f, 0.16f));
            for (int i = 0; i < 6; i++) Box(m, "EndGrain", new Vector3(-0.85f, 0.16f + i * 0.095f, 0.95f), new Vector3(0.9f, 0.075f, 0.48f), -5);
            Box(m, "Teal", new Vector3(0.9f, 1.07f, 0.22f), new Vector3(0.6f, 0.045f, 0.45f), 12);
            Axe(m, new Vector3(-0.9f, 1.02f, 0.6f), 0.55f);
            Box(m, "DarkWood", new Vector3(0, 1.72f, 1.05f), new Vector3(1.3f, 0.7f, 0.09f));
            for (int i = 0; i < 3; i++) Beam(m, "EndGrain", new Vector3(-0.4f + i * 0.35f, 1.55f, 0.98f), new Vector3(-0.3f + i * 0.35f, 1.9f, 0.98f), 0.035f);
            Moss(m, new Vector3(-1.2f, 2.8f, 0.8f), new Vector3(0.65f, 0.1f, 0.4f), 8);
            return m;
        }

        static TWGroveModel Cart()
        {
            var m = new TWGroveModel();
            for (int i = 0; i < 5; i++) Box(m, "Wood", new Vector3(-0.68f + i * 0.34f, 0.62f, -0.22f), new Vector3(0.3f, 0.15f, 1.85f));
            foreach (int side in new[] { -1, 1 })
            {
                for (int row = 0; row < 2; row++) Box(m, "Wood", new Vector3(side * 0.8f, 0.85f + row * 0.25f, -0.22f), new Vector3(0.1f, 0.19f, 1.85f));
                foreach (int z in new[] { -1, 1 }) Beam(m, "DarkWood", new Vector3(side * 0.84f, 0.5f, z * 0.78f - 0.2f), new Vector3(side * 0.84f, 1.3f, z * 0.78f - 0.2f), 0.055f);
                foreach (int z in new[] { -1, 1 })
                {
                    Vector3 centre = new Vector3(side * 0.98f, 0.4f, z * 0.66f - 0.2f);
                    m.Part("DarkWood").Cyl(centre - Vector3.right * 0.08f, centre + Vector3.right * 0.08f, 0.4f, 0.4f, White, 12);
                    m.Part("EndGrain").Cyl(centre + Vector3.right * (side * 0.085f), centre + Vector3.right * (side * 0.1f), 0.27f, 0.27f, White, 12);
                    for (int j = 0; j < 6; j++)
                    {
                        float a = j * Mathf.PI / 3;
                        Beam(m, "Wood", centre + Vector3.right * (side * 0.11f), centre + new Vector3(side * 0.11f, Mathf.Sin(a) * 0.33f, Mathf.Cos(a) * 0.33f), 0.03f);
                    }
                }
                Beam(m, "DarkWood", new Vector3(side * 0.47f, 0.49f, 0.6f), new Vector3(side * 0.47f, 0.65f, 1.5f), 0.065f);
            }
            for (int i = 0; i < 3; i++) Log(m, new Vector3(-0.5f + i * 0.48f, 0.94f, -1.1f), new Vector3(-0.5f + i * 0.48f, 0.94f, 0.55f), 0.23f);
            Log(m, new Vector3(-0.23f, 1.32f, -1.1f), new Vector3(-0.23f, 1.32f, 0.65f), 0.22f);
            return m;
        }

        static TWGroveModel Stump()
        {
            var m = new TWGroveModel(); Log(m, new Vector3(0, -0.08f, 0), new Vector3(0, 0.72f, 0), 0.56f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 0.4f; Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Beam(m, "Bark", p * 0.73f, p * 0.25f + Vector3.up * 0.4f, 0.13f);
            }
            Axe(m, new Vector3(-0.1f, 0.65f, 0), 0.8f);
            for (int i = 0; i < 9; i++) Box(m, "Chips", new Vector3(Mathf.Cos(i * 2.4f) * 0.74f, 0.035f, Mathf.Sin(i * 2.4f) * 0.7f), new Vector3(0.14f, 0.025f, 0.05f), i * 43);
            return m;
        }

        static TWGroveModel LogRack()
        {
            var m = new TWGroveModel();
            foreach (int side in new[] { -1, 1 })
            {
                Beam(m, "DarkWood", new Vector3(side * 1.1f, 0, -0.7f), new Vector3(side * 1.1f, 1.05f, 0.5f), 0.09f);
                Beam(m, "DarkWood", new Vector3(side * 1.1f, 0, 0.7f), new Vector3(side * 1.1f, 1.05f, -0.5f), 0.09f);
            }
            for (int row = 0; row < 3; row++) for (int i = 0; i < 3 - row; i++)
            {
                float z = (i - (2 - row) * 0.5f) * 0.46f;
                Log(m, new Vector3(-1.55f, 0.47f + row * 0.4f, z), new Vector3(1.55f, 0.47f + row * 0.4f, z), 0.24f);
            }
            Rope(m, new Vector3(-0.65f, 0.55f, -0.6f), new Vector3(-0.65f, 0.55f, 0.6f), -0.88f);
            return m;
        }

        static TWGroveModel Bench()
        {
            var m = new TWGroveModel();
            for (int i = 0; i < 3; i++) Box(m, "Wood", new Vector3(0, 0.62f, -0.25f + i * 0.22f), new Vector3(2.2f, 0.13f, 0.2f));
            foreach (int side in new[] { -1, 1 }) Box(m, "DarkWood", new Vector3(side * 0.75f, 0.28f, 0), new Vector3(0.28f, 0.62f, 0.55f));
            // The forgotten lunch is a quiet lived-in detail.
            m.Part("Cup").Cyl(new Vector3(-0.8f, 0.7f, 0), new Vector3(-0.8f, 0.93f, 0), 0.10f, 0.12f, White, 8);
            m.Part("DarkWood").Cyl(new Vector3(-0.8f, 0.931f, 0), new Vector3(-0.8f, 0.938f, 0), 0.09f, 0.09f, White, 8);
            return m;
        }

        static TWGroveModel OldMark()
        {
            var m = new TWGroveModel(); Stone(m, new Vector3(0, 0.52f, 0), new Vector3(0.66f, 0.72f, 0.43f), 9);
            Moss(m, new Vector3(-0.21f, 1.09f, 0), new Vector3(0.48f, 0.16f, 0.35f), 12);
            for (int i = 0; i < 3; i++) Beam(m, "DarkWood", new Vector3(-0.2f + i * 0.19f, 0.55f, -0.397f), new Vector3(-0.14f + i * 0.19f, 0.83f, -0.397f), 0.026f);
            Wing(m, new Vector3(0, 0.35f, -0.43f), 0.45f, -1); Wing(m, new Vector3(0, 0.35f, -0.43f), 0.45f, 1);
            return m;
        }

        static TWGroveModel DuckStool()
        {
            var m = new TWGroveModel(); Log(m, new Vector3(0, -0.04f, 0), new Vector3(0, 0.65f, 0), 0.39f);
            m.Part("EndGrain").Puff(new Vector3(0, 0.82f, 0), new Vector3(0.23f, 0.15f, 0.29f), White, 0, 7, 4, 0);
            m.Part("EndGrain").Puff(new Vector3(0, 1.03f, -0.17f), new Vector3(0.13f, 0.13f, 0.13f), White, 0, 7, 4, 0);
            Box(m, "Wood", new Vector3(0, 1.01f, -0.31f), new Vector3(0.15f, 0.055f, 0.14f));
            foreach (int side in new[] { -1, 1 }) m.Part("Metal").Puff(new Vector3(side * 0.11f, 1.06f, -0.2f), Vector3.one * 0.018f, White, 0, 5, 3, 0);
            Box(m, "Cup", new Vector3(0.42f, 0.47f, -0.15f), new Vector3(0.32f, 0.22f, 0.035f), -10);
            return m;
        }

        static TWGroveModel LostClock()
        {
            var m = new TWGroveModel(); Stone(m, new Vector3(0, 0.18f, 0), new Vector3(0.48f, 0.22f, 0.4f), 28);
            Beam(m, "EndGrain", new Vector3(-0.23f, 0.43f, -0.1f), new Vector3(0.31f, 0.44f, 0.15f), 0.036f);
            m.Part("EndGrain").Star(new Vector3(0.3f, 0.45f, 0.15f), Vector3.right, Vector3.forward, 0.12f, 0.06f, 4, White);
            Moss(m, new Vector3(-0.28f, 0.35f, 0.08f), new Vector3(0.23f, 0.08f, 0.25f), 0);
            return m;
        }

        static void Border(TWGroveContext x, TWGroveAssets assets)
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = (i * 30f + 9f) * Mathf.Deg2Rad;
                Vector2 desired = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (15.8f + 0.5f * Mathf.Sin(i * 2.1f));
                if (desired.y < -11f && Mathf.Abs(desired.x) < 5f) continue;
                Vector2 half = new Vector2(1.12f, 0.63f);
                float yaw = 90 - angle * Mathf.Rad2Deg;
                if (Reuse(x, "Border_" + i, out GameObject kept)) continue;
                if (!x.Place(desired, half, yaw, 1.25f, out Vector3 p, out Quaternion q)) continue;
                var m = new TWGroveModel();
                if (i % 3 == 0) { Log(m, new Vector3(-0.9f, 0.25f, 0), new Vector3(0.9f, 0.25f, 0), 0.24f); Moss(m, new Vector3(-0.3f, 0.5f, 0), new Vector3(0.6f, 0.09f, 0.2f), 4); }
                else { Stone(m, new Vector3(-0.3f, 0.24f, 0), new Vector3(0.65f, 0.4f, 0.48f), i * 21); Moss(m, new Vector3(0.43f, 0.29f, 0.03f), new Vector3(0.6f, 0.5f, 0.53f), i * 17); }
                for (int k = 0; k < 4; k++)
                {
                    Vector3 centre = new Vector3(-0.75f + k * 0.47f, 0.03f, 0.33f + 0.12f * Mathf.Sin(k));
                    for (int j = 0; j < 3; j++)
                    {
                        float a = j * Mathf.PI * 2f / 3;
                        m.Part("LeafLight").Teardrop(centre, 0.35f + k * 0.04f, 0.07f, new Vector3(Mathf.Cos(a) * 0.25f, 0, Mathf.Sin(a) * 0.25f), White, White, 5, 3);
                    }
                    if (k % 2 == 0)
                    {
                        Beam(m, "Leaf", centre, centre + Vector3.up * 0.45f, 0.018f);
                        m.Part(i % 2 == 0 ? "CreamFlower" : "PurpleFlower").Puff(centre + Vector3.up * 0.46f, Vector3.one * 0.085f, White, 0, 5, 3, 0);
                    }
                }
                Finish(x, assets, m, "Border_" + i, "20_Опушка_" + (i + 1).ToString("00"), p, q, half, 1f, false, "Мягкая граница рощи без сплошного забора. Низкий декор без коллизии.");
            }
        }
    }
}
