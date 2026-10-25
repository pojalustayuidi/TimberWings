// Assets/_Project/Editor/IslandV2/IslandZones.cs
// Остров v2, задачи A3 и A4: новые якоря зон и тропы в IslandLayout.asset. Меню: Tools/BuilderSim/Локация/Остров v2/Зоны: применить A3-A4 (приоритет 132).
// Ассет IslandLayout бинарный, руками в тексте не правится, поэтому изменения делает этот скрипт. Он безопасен к повторному запуску:
// якоря и тропы с этими именами заменяются, остальное в раскладке (река, цвета, здания из первой версии) не трогается.
//
// Что делает:
//   A3  якоря: PAD_SiteA, PAD_SiteB (стройплощадки, тонкие плиты без коллизии), PAD_PetCase, PAD_Worker1..6, PAD_Grandpa (кедр), PAD_Lighthouse,
//       PAD_GunnarGlade, PAD_GunnarShore, PAD_GroveSouth; PAD_Forest переезжает на поляну рубки (6, 44); поляны без леса у лагеря и у «Дедушки».
//       Коробки BLK_* для них строит обычная сборка (IslandBlockout.Props менять не нужно), проходимость и места проверяет IslandCheck.
//   брод: halfLength зоны брода поднимается до полуширины реки + 1,5 м, иначе вода по краям брода режет проходимость (см. отчёт IslandCheck)
//   A4  тропы: «Роща», «Маяк», «Стройплощадка» (новые), «Мост» переписана змейкой (около 95 м, вместе с «Рынком» около 130 м, 33 с при 4 м/с), без симметрии.
// Если якорь у края (Дедушка, маяк, роща) вышел бы ближе нужного к краю острова, он сдвигается к центру, это пишется в Console.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class IslandZones
    {
        const string BuildMenu = "Tools/BuilderSim/Локация/Остров v2/2. Собрать блокаут";
        static readonly Vector2 Zero = Vector2.zero;

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Зоны: применить A3-A4 (якоря и тропы)", false, 132)]
        static void Menu()
        {
            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (L == null) { Debug.LogError("IslandZones: нет раскладки " + IslandLayout.AssetPath + " (пункт 1 создаст)."); return; }
            Undo.RecordObject(L, "Остров v2: зоны A3-A4");
            string report = ApplyData(L);
            EditorUtility.SetDirty(L);
            AssetDatabase.SaveAssets();
            Debug.Log(report);
            if (EditorUtility.DisplayDialog("Зоны A3-A4", "Якоря и тропы записаны в IslandLayout.asset (подробности в Console).\nПересобрать блокаут сейчас? После него запусти пункт 3 (проверка).", "Пересобрать", "Позже"))
                EditorApplication.ExecuteMenuItem(BuildMenu);
        }

        static float Yaw(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }

        static IslandLayout.Anchor Make(string n, IslandLayout.AnchorKind k, float x, float z, float fx, float fz, float h, float flat, float gray, bool blocks, float approach)
        {
            var a = new IslandLayout.Anchor
            {
                name = n, kind = k, pos = new Vector2(x, z), footprint = new Vector2(fx, fz), height = h,
                flatRadius = flat, gray = gray, blocksWalk = blocks, approachDist = approach
            };
            a.yawDeg = Yaw(a.pos, Zero);   // лицом к костру
            return a;
        }

        static void Upsert(IslandLayout L, IslandLayout.Anchor a, StringBuilder sb)
        {
            for (int i = 0; i < L.anchors.Count; i++)
            {
                if (L.anchors[i].name != a.name) continue;
                L.anchors[i] = a;
                sb.AppendLine("  якорь обновлён: " + a.name + " (" + a.pos.x.ToString("0.#") + ", " + a.pos.y.ToString("0.#") + ")");
                return;
            }
            L.anchors.Add(a);
            sb.AppendLine("  якорь добавлен: " + a.name + " (" + a.pos.x.ToString("0.#") + ", " + a.pos.y.ToString("0.#") + ")");
        }

        static void UpsertPath(IslandLayout L, string name, float width, string endAnchor, Vector2[] pts, StringBuilder sb)
        {
            var pl = new IslandLayout.PathLine { name = name, width = width, endAnchor = endAnchor };
            pl.points.AddRange(pts);
            for (int i = 0; i < L.paths.Count; i++)
            {
                if (L.paths[i].name != name) continue;
                L.paths[i] = pl;
                sb.AppendLine("  тропа обновлена: «" + name + "», опорных точек " + pts.Length);
                return;
            }
            L.paths.Add(pl);
            sb.AppendLine("  тропа добавлена: «" + name + "», опорных точек " + pts.Length);
        }

        static void UpsertMeadow(IslandLayout L, Vector2 pos, float radius, string label, StringBuilder sb)
        {
            for (int i = 0; i < L.meadows.Count; i++)
            {
                if (Vector2.Distance(L.meadows[i].pos, pos) > 1.5f) continue;
                L.meadows[i].pos = pos; L.meadows[i].radius = radius;
                sb.AppendLine("  поляна обновлена: " + label + " (" + pos.x.ToString("0.#") + ", " + pos.y.ToString("0.#") + "), радиус " + radius.ToString("0"));
                return;
            }
            L.meadows.Add(new IslandLayout.Meadow { pos = pos, radius = radius });
            sb.AppendLine("  поляна добавлена: " + label + " (" + pos.x.ToString("0.#") + ", " + pos.y.ToString("0.#") + "), радиус " + radius.ToString("0"));
        }

        // Применяет все изменения к раскладке в памяти и возвращает отчёт. Ассет сохраняет вызывающий.
        public static string ApplyData(IslandLayout L)
        {
            var sb = new StringBuilder("IslandZones: зоны и тропы (A3-A4)\n");
            const IslandLayout.AnchorKind B = IslandLayout.AnchorKind.Building, M = IslandLayout.AnchorKind.MarkerOnly;

            // ---------------- A3: якоря
            // стройплощадки: тонкая плита без коллизии (видно место, ходить можно); сюда встают здания из заказов
            Upsert(L, Make("PAD_SiteA", B, 26f, 19f, 9f, 8f, 0.15f, 8f, 0.80f, false, 2.5f), sb);
            Upsert(L, Make("PAD_SiteB", B, 24f, -6f, 7f, 6f, 0.15f, 6f, 0.80f, false, 2.5f), sb);
            // кейс питомцев у палатки
            Upsert(L, Make("PAD_PetCase", B, -17f, -31f, 1.6f, 1.2f, 1.1f, 3f, 0.70f, true, 1.5f), sb);
            // 6 мест рабочих: дуга радиусом 7 м вокруг палатки (-22, -28) с юга и запада, лицом к палатке
            Vector2 tent = new Vector2(-22f, -28f);
            float[] ang = { 100f, 135f, 170f, 205f, 240f, 275f };
            for (int i = 0; i < ang.Length; i++)
            {
                float r = ang[i] * Mathf.Deg2Rad;
                var w = Make("PAD_Worker" + (i + 1), M, Mathf.Round((tent.x + Mathf.Cos(r) * 7f) * 10f) / 10f, Mathf.Round((tent.y + Mathf.Sin(r) * 7f) * 10f) / 10f, 1f, 1f, 0f, 2.5f, 0.5f, false, 0f);
                w.yawDeg = Yaw(w.pos, tent);
                Upsert(L, w, sb);
            }
            // «Дедушка» (кедр, нельзя срубить, виден с любого места) и мёртвый маяк
            Upsert(L, Make("PAD_Grandpa", B, 8f, 54f, 5f, 5f, 34f, 6f, 0.36f, true, 3f), sb);
            Upsert(L, Make("PAD_Lighthouse", B, 58f, 40f, 5f, 5f, 15f, 8f, 0.72f, true, 2.5f), sb);
            // поляна Гуннара, его берег, южная роща (метки; поляны без леса для первых двух уже есть в раскладке)
            Upsert(L, Make("PAD_GunnarGlade", M, -30f, -8f, 1f, 1f, 0f, 5f, 0.5f, false, 0f), sb);
            Upsert(L, Make("PAD_GunnarShore", M, -64f, 8f, 1f, 1f, 0f, 5f, 0.5f, false, 0f), sb);
            Upsert(L, Make("PAD_GroveSouth", M, 10f, -50f, 1f, 1f, 0f, 8f, 0.5f, false, 0f), sb);

            // поляна рубки: PAD_Forest с (6, 48) на (6, 44), как в таблице зон
            var forest = L.FindAnchor("PAD_Forest");
            if (forest != null)
            {
                sb.AppendLine("  PAD_Forest: (" + forest.pos.x.ToString("0.#") + ", " + forest.pos.y.ToString("0.#") + ") -> (6, 44)");
                forest.pos = new Vector2(6f, 44f);
                forest.yawDeg = Yaw(forest.pos, Zero);
            }

            // ---------------- края: якоря у границы острова сдвигаем внутрь
            var f0 = new IslandField(L);
            foreach (var n in new[] { "PAD_Grandpa", "PAD_Lighthouse", "PAD_GroveSouth" })
            {
                var a = L.FindAnchor(n);
                float need = 3f + (a.kind == B ? a.footprint.magnitude * 0.5f : 0f);
                Vector2 p = a.pos;
                int steps = 0;
                while (f0.EdgeDistance(p.x, p.y) < need && steps < 30) { p += (L.centerOffset - p).normalized; steps++; }
                if (steps > 0)
                {
                    sb.AppendLine("  [!] " + n + " стоял бы в " + f0.EdgeDistance(a.pos.x, a.pos.y).ToString("0.0") + " м от края (нужно " + need.ToString("0.0") + "): сдвинут с (" + a.pos.x.ToString("0.#") + ", " + a.pos.y.ToString("0.#") + ") на (" + p.x.ToString("0.#") + ", " + p.y.ToString("0.#") + ")");
                    a.pos = p; a.yawDeg = Yaw(p, Zero);
                }
            }

            // ---------------- поляны без леса (блокаут): лагерь с рабочими, площадка вокруг «Дедушки»
            UpsertMeadow(L, new Vector2(-24f, -29f), 10f, "Лагерь", sb);
            var gp = L.FindAnchor("PAD_Grandpa");
            UpsertMeadow(L, gp.pos + (L.centerOffset - gp.pos).normalized * 2.5f, 7f, "у «Дедушки»", sb);

            // ---------------- A4: тропы
            var lh = L.FindAnchor("PAD_Lighthouse");
            Vector2 lhEnd = lh.pos + IslandField.Forward(lh.yawDeg) * (lh.footprint.y * 0.5f + lh.approachDist);   // подход к двери
            // «Мост»: змейка от рынка (35, 11) на восток; три петли, расстояние между ветками не меньше 12 м, конец встаёт на DEFERRED_Bridge
            UpsertPath(L, "Мост", 3.2f, "DEFERRED_Bridge", new[]
            {
                new Vector2(35f, 11f), new Vector2(39f, 2f), new Vector2(48f, -9f), new Vector2(59f, -10f), new Vector2(65f, -3f),
                new Vector2(58f, 4f), new Vector2(50f, 8f), new Vector2(54f, 16f), new Vector2(64f, 16f)
            }, sb);
            // «Маяк»: ответвление от последней петли моста (64, 16) к двери маяка
            UpsertPath(L, "Маяк", 2.8f, "", new[]
            {
                new Vector2(64f, 16f), new Vector2(63f, 25f), lhEnd + new Vector2(3f, -8f), lhEnd
            }, sb);
            // «Стройплощадка»: от «Рынка» (12, 3) через участок B (24, -6), пересечение с «Рынком» и вверх к участку A
            UpsertPath(L, "Стройплощадка", 2.8f, "PAD_SiteA", new[]
            {
                new Vector2(12f, 3f), new Vector2(18f, -1f), new Vector2(24f, -6f), new Vector2(28f, -2f), new Vector2(28f, 6f), new Vector2(27f, 13f)
            }, sb);
            // «Роща»: ответвление от «Пруда» (8, -12) на юг к южной роще
            UpsertPath(L, "Роща", 3.0f, "PAD_GroveSouth", new[]
            {
                new Vector2(8f, -12f), new Vector2(10f, -22f), new Vector2(8f, -32f), new Vector2(10f, -42f)
            }, sb);

            // ---------------- брод: зона брода должна перекрывать реку от берега до берега
            // (иначе по краям брода остаётся полоска воды, берега не соединены и проходимость до западного берега обрывается)
            foreach (var cr in L.crossings)
            {
                float need = L.riverWidth * 0.5f + 1.5f;
                if (cr.halfLength >= need) continue;
                sb.AppendLine("  брод (" + cr.pos.x.ToString("0") + ", " + cr.pos.y.ToString("0") + "): halfLength " + cr.halfLength.ToString("0.#") + " -> " + need.ToString("0.#") + " (река шириной " + L.riverWidth.ToString("0") + " м)");
                cr.halfLength = need;
            }

            // ---------------- отчёт: тропы у края, длина пути до моста
            var f = new IslandField(L);
            for (int i = 0; i < L.paths.Count; i++)
            {
                string pn = L.paths[i].name;
                if (pn != "Мост" && pn != "Маяк" && pn != "Роща" && pn != "Стройплощадка") continue;
                var pl = f.PathPolyline(i);
                float minEdge = float.MaxValue, len = 0f;
                for (int k = 0; k < pl.Length; k++)
                {
                    minEdge = Mathf.Min(minEdge, f.EdgeDistance(pl[k].x, pl[k].y));
                    if (k > 0) len += Vector2.Distance(pl[k - 1], pl[k]);
                }
                sb.AppendLine("  «" + pn + "»: длина " + len.ToString("0") + " м, ближе всего к краю " + minEdge.ToString("0.0") + " м" + (minEdge < 3f ? "  [!] у самого края, проверь на карте" : ""));
            }
            var br = L.FindAnchor("DEFERRED_Bridge");
            if (br != null)
            {
                Vector2 bp = f.AnchorWorld(br);
                sb.AppendLine("  DEFERRED_Bridge стоит в (" + bp.x.ToString("0.#") + ", " + bp.y.ToString("0.#") + "), в таблице зон (74, 10); метка считается по контуру, угол " + br.edgeAngleDeg.ToString("0.#") + "°, отступ " + br.edgeInset.ToString("0.#") + " м");
            }
            sb.AppendLine("Дальше: пункт «2. Собрать блокаут», потом «3a» (отчёт после A3-A4) и сравни с базой из A1.");
            return sb.ToString();
        }
    }
}
