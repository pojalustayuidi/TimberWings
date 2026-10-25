#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestStats.cs (Editor)  НОВЫЙ ФАЙЛ
// Проверка и замер леса после «большого леса + лёгких хранителей». Только читает сцену (кроме скринов: они пишут PNG в Library/ForestShots).
// Меню: Tools/BuilderSim/Лес/Красота леса/
//   15. Замер леса (сравнить с «до»)     (244)  сколько чего в сцене и сколько треугольников, плюс Batches/Tris из Game View (UnityStats), если он открыт
//   15a. Запомнить замер как «до»        (245)  сохраняет текущий замер в EditorPrefs; следующий «15» покажет разницу. Делай «до» на старой версии леса
//   16. Проверка леса                    (246)  нет Decor_*, нет нерубимых деревьев в зоне рубки и на полянках, нет рубимых в полянках хранителей,
//                                               никто не стоит на тропе, воде и зданиях, нет Light на деревьях рощ и лёгких хранителей
//   17. Скрины леса                      (247)  вид сверху и несколько видов с земли (старт, зона рубки, крупные лёгкие хранители, две кулисы) в Library/ForestShots
// Оговорка по треугольникам: у рубимых деревьев модель подменяется при запуске игры (ResourceItem.useGeneratedModel, TreeMeshFactory), поэтому цифры
// «Tris, оценка по мешам сцены» для них приблизительные. Точные Batches/Tris даёт только Game View в Play Mode (UnityStats), см. сводку.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class ForestStats
    {
        const string Key = "BuilderSim.ForestStats.Before";
        static readonly string[] Roots = { "ForestChop", "ChopZone", "SacredTrees", ForestGuardLite.RootName, ForestWings.RootName, "Understory", "ForestDecor" };

        // ============================================================ замер
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/15. Замер леса (сравнить с «до»)", false, 244)]
        static void Measure()
        {
            string now = Snapshot();
            var sb = new StringBuilder(now);
            string before = EditorPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(before)) sb.AppendLine("\n--- «до» (запомнено раньше) ---\n" + before);
            else sb.AppendLine("\n«До» не запомнено: на старой версии леса нажми «15a. Запомнить замер как «до»», потом пересобери лес и нажми «15».");
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/15a. Запомнить замер как «до»", false, 245)]
        static void Remember()
        {
            string s = Snapshot();
            EditorPrefs.SetString(Key, s);
            Debug.Log("Замер сохранён как «до»:\n" + s);
        }

        static string Snapshot()
        {
            var sb = new StringBuilder("Замер леса:\n");
            var man = GameObject.Find("IslandV2/_Manual");
            long totalTris = 0; int totalRend = 0;
            foreach (var rn in Roots)
            {
                var t = man != null ? man.transform.Find(rn) : null;
                if (t == null) continue;
                int inst = rn == "ChopZone" ? 1 : t.childCount;
                long tris = 0; int rend = 0, cols = 0, lights = 0, lods = 0;
                CountRoot(t, out tris, out rend, out cols, out lights, out lods);
                totalTris += tris; totalRend += rend;
                sb.AppendLine("  " + rn + ": объектов " + inst + ", рендереров " + rend + ", ~Tris (LOD0) " + tris + ", коллайдеров " + cols + ", Light " + lights + ", LODGroup " + lods);
            }
            sb.AppendLine("  итого по корням леса: рендереров " + totalRend + ", ~Tris " + totalTris);
            int chop = 0; foreach (var ri in Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) chop++;
            sb.AppendLine("  рубимых деревьев (ResourceItem) в сцене: " + chop);
            sb.AppendLine("  Game View (UnityStats, последний кадр): Batches " + UnityStats.batches + ", SetPass " + UnityStats.setPassCalls + ", Tris " + UnityStats.triangles
                + (Application.isPlaying ? "" : "  [не Play Mode: цифры могут быть нулевыми или старыми; точный замер в Play с игровой камеры]"));
            return sb.ToString();
        }

        // считаем рендереры в LOD0 (если у объекта LODGroup, берём только первый уровень)
        static void CountRoot(Transform root, out long tris, out int rend, out int cols, out int lights, out int lods)
        {
            tris = 0; rend = 0; cols = 0; lights = 0; lods = 0;
            var skip = new HashSet<Renderer>();
            foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
            {
                lods++;
                var l = lg.GetLODs();
                for (int i = 1; i < l.Length; i++)
                    foreach (var r in l[i].renderers) if (r != null) skip.Add(r);
                if (l.Length > 0) foreach (var r in l[0].renderers) if (r != null) skip.Remove(r);   // то, что есть в LOD0, считаем
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<Renderer>();
                if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;
                if (skip.Contains(mr)) continue;
                rend++;
                tris += mf.sharedMesh.triangles.Length / 3;
            }
            cols = root.GetComponentsInChildren<Collider>(true).Length;
            lights = root.GetComponentsInChildren<Light>(true).Length;
        }

        // ============================================================ проверка
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/16. Проверка леса", false, 246)]
        static void Check()
        {
            var rootGo = GameObject.Find("IslandV2");
            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (rootGo == null || L == null) { Debug.LogError("Проверка леса: нет IslandV2 или раскладки."); return; }
            var f = new IslandField(L);
            var sb = new StringBuilder("Проверка леса:\n");
            int fails = 0;

            // 1. Ни одного Decor_*
            int decor = 0; var decorNames = new List<string>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Decor_")) { decor++; if (decorNames.Count < 5) decorNames.Add(t.name); }
            Report(sb, ref fails, decor == 0, "нет объектов Decor_*", "найдено Decor_*: " + decor + " (" + string.Join(", ", decorNames.ToArray()) + "). Запусти «4. Лес» ещё раз");

            // 2. Нерубимые деревья внутри зоны рубки
            var pad = GameObject.Find(ChopZone.PadName);
            var nonChop = new List<Transform>();
            var man = GameObject.Find("IslandV2/_Manual");
            if (man != null)
                foreach (var rn in new[] { "SacredTrees", ForestGuardLite.RootName, ForestWings.RootName })
                {
                    var t = man.transform.Find(rn);
                    if (t != null) for (int i = 0; i < t.childCount; i++) nonChop.Add(t.GetChild(i));
                }
            if (pad != null)
            {
                Vector2 pc = new Vector2(pad.transform.position.x, pad.transform.position.z);
                int inZone = 0; var names = new List<string>();
                foreach (var t in nonChop)
                {
                    Vector2 d = new Vector2(t.position.x, t.position.z) - pc;
                    if (d.magnitude < ChopZone.Rb(Mathf.Atan2(d.y, d.x)) + 1f) { inZone++; if (names.Count < 5) names.Add(t.name); }
                }
                Report(sb, ref fails, inZone == 0, "внутри ChopZone нет нерубимых деревьев", "нерубимых внутри забора: " + inZone + " (" + string.Join(", ", names.ToArray()) + ")");
            }
            else sb.AppendLine("  [?] нет " + ChopZone.PadName + ": зону рубки не проверить");

            // 3. Рубимые в полянках нерубимых (кольцо должно читаться)
            int crowd = 0; var crowdNames = new List<string>();
            var items = new List<ResourceItem>(Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            foreach (var g in nonChop)
            {
                var lg = g.GetComponent<LiteGuardian>();
                float r = lg != null ? lg.clearRadius : g.name.StartsWith(ForestWings.NamePrefix) ? ForestWings.ClearRadius(g) : 3.6f;
                foreach (var ri in items)
                {
                    float dx = ri.transform.position.x - g.position.x, dz = ri.transform.position.z - g.position.z;
                    if (dx * dx + dz * dz < r * r * 0.9f) { crowd++; if (crowdNames.Count < 5) crowdNames.Add(ri.name + "@" + g.name); }
                }
            }
            Report(sb, ref fails, crowd == 0, "в полянках хранителей чисто (рубимых деревьев нет)", "рубимые в полянках хранителей: " + crowd + " (" + string.Join(", ", crowdNames.ToArray()) + "). Запусти «13» или «9» ещё раз");

            // 4. Деревья на тропе, воде, зданиях
            int bad = 0; var badNames = new List<string>();
            var all = new List<Transform>();
            foreach (var ri in items) all.Add(ri.transform);
            all.AddRange(nonChop);
            foreach (var t in all)
            {
                float x = t.position.x, z = t.position.z;
                bool onPath = f.PathDistance(x, z) < 1.2f, onWater = f.IsWater(x, z) || f.WaterDistance(x, z) < 0.5f, onBld = false;
                for (int i = 0; i < L.anchors.Count && !onBld; i++)
                    if (L.anchors[i].kind == IslandLayout.AnchorKind.Building && f.RectDistance(L.anchors[i], x, z) < 1.5f) onBld = true;
                if (onPath || onWater || onBld) { bad++; if (badNames.Count < 8) badNames.Add(t.name + (onPath ? "(тропа)" : onWater ? "(вода)" : "(здание)")); }
            }
            Report(sb, ref fails, bad == 0, "деревья не стоят на тропах, воде и зданиях (" + all.Count + " проверено)", "на тропе/воде/здании: " + bad + " (" + string.Join(", ", badNames.ToArray()) + ")");

            // 5. Light на деревьях рощ и лёгких хранителей, ResourceItem на лёгких
            int lights = 0, lr = 0;
            if (man != null)
            {
                var chop = man.transform.Find("ForestChop");
                if (chop != null) lights += chop.GetComponentsInChildren<Light>(true).Length;
                var lite = man.transform.Find(ForestGuardLite.RootName);
                if (lite != null)
                {
                    lights += lite.GetComponentsInChildren<Light>(true).Length;
                    lights += lite.GetComponentsInChildren<FireSource>(true).Length;
                    lr = lite.GetComponentsInChildren<ResourceItem>(true).Length;
                }
                var wg = man.transform.Find(ForestWings.RootName);
                if (wg != null)
                {
                    lights += wg.GetComponentsInChildren<Light>(true).Length;
                    lights += wg.GetComponentsInChildren<FireSource>(true).Length;
                    lr += wg.GetComponentsInChildren<ResourceItem>(true).Length;
                }
            }
            Report(sb, ref fails, lights == 0 && lr == 0, "на деревьях рощ, лёгких хранителей и кулис нет Light и FireSource, у нерубимых нет ResourceItem", "Light/FireSource: " + lights + ", ResourceItem у лёгких: " + lr);

            sb.AppendLine(fails == 0 ? "Итог: всё в порядке. Проходимость отдельно: «Остров v2 → 3. Проверить блокаут»." : "Итог: проблем " + fails + ". Блокаут (проходимость) этот пункт не заменяет.");
            if (fails == 0) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
        }

        static void Report(StringBuilder sb, ref int fails, bool ok, string good, string bad)
        {
            if (ok) sb.AppendLine("  [ок] " + good);
            else { sb.AppendLine("  [!] " + bad); fails++; }
        }

        // ============================================================ скрины
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/17. Скрины леса (сверху и с земли)", false, 247)]
        static void Shots()
        {
            var rootGo = GameObject.Find("IslandV2");
            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (rootGo == null || L == null) { Debug.LogError("Скрины леса: нет IslandV2 или раскладки."); return; }
            var f = new IslandField(L);
            string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "Library/ForestShots");
            System.IO.Directory.CreateDirectory(dir);
            var camGo = new GameObject("_ForestShotCam") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var written = new List<string>();
            try
            {
                var cam = camGo.AddComponent<Camera>();
                var main = Camera.main;
                if (main != null) cam.CopyFrom(main);
                cam.targetTexture = rt;
                cam.enabled = false;
                cam.farClipPlane = 600f;

                // сверху: орто на весь остров
                Vector2 c = (f.Min + f.Max) * 0.5f;
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max((f.Max.x - f.Min.x) / (2f * cam.aspect), (f.Max.y - f.Min.y) * 0.5f) * 1.05f;   // весь остров в кадре
                camGo.transform.SetPositionAndRotation(new Vector3(c.x, 250f, c.y), Quaternion.Euler(90f, 0f, 0f));
                written.Add(Shot(cam, rt, dir, "top"));

                // с земли: старт, зона рубки, крупные лёгкие хранители
                cam.orthographic = false; cam.fieldOfView = 55f;
                var spots = new List<KeyValuePair<string, Vector3>>();
                string[] anchors = { "SPAWN_Player", ChopZone.PadName };
                foreach (var an in anchors)
                {
                    var go = GameObject.Find(an);
                    if (go != null) spots.Add(new KeyValuePair<string, Vector3>(an, go.transform.position));
                }
                var lite = GameObject.Find("IslandV2/_Manual/" + ForestGuardLite.RootName);
                int nl = 0;
                if (lite != null)
                    foreach (Transform t in lite.transform)
                        if (t.name.StartsWith("Lite_") && nl < 3) { spots.Add(new KeyValuePair<string, Vector3>(t.name, t.position)); nl++; }
                // две кулисы: ближе к земле, чтобы было видно венок и пастель
                var wg = GameObject.Find("IslandV2/_Manual/" + ForestWings.RootName);
                int nw = 0;
                if (wg != null)
                    for (int wi = 0; wi < wg.transform.childCount && nw < 2; wi += Mathf.Max(1, wg.transform.childCount / 2))
                    {
                        var t = wg.transform.GetChild(wi);
                        if (t.name.StartsWith("Wing_Bush")) continue;
                        spots.Add(new KeyValuePair<string, Vector3>(t.name, t.position)); nw++;
                    }
                foreach (var s in spots)
                {
                    Vector2 target = ForestSacred.NearestPathPoint(f, L, new Vector2(s.Value.x, s.Value.z));
                    Vector3 toPath = new Vector3(target.x - s.Value.x, 0f, target.y - s.Value.z);
                    Vector3 dirV = toPath.sqrMagnitude > 1f ? toPath.normalized : Vector3.back;
                    bool guard = s.Key.StartsWith("Lite_") || s.Key.StartsWith(ForestWings.NamePrefix);
                    Vector3 pos = s.Value + (guard ? dirV * 11f : Vector3.zero) + Vector3.up * 2.2f;
                    Vector3 look = guard ? s.Value + Vector3.up * 4.5f : s.Value + (-dirV) * 12f + Vector3.up * 2f;
                    camGo.transform.position = pos;
                    camGo.transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
                    written.Add(Shot(cam, rt, dir, s.Key));
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                rt.Release(); Object.DestroyImmediate(rt);
            }
            Debug.Log("Скрины леса: " + written.Count + " файлов в " + dir + "\n  " + string.Join("\n  ", written.ToArray())
                + "\n  Камера временная, копирует настройки Camera.main. Если картинка чёрная или без неба, смотри сцену через Scene View: это ограничение рендера вне Play Mode.");
        }

        static string Shot(Camera cam, RenderTexture rt, string dir, string name)
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string path = System.IO.Path.Combine(dir, name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }
    }
}
#endif
