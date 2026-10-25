// Assets/_Project/Editor/IslandV2/IslandCheck.cs
// Остров v2, этап 1: проверка блокаута. Меню (приоритеты):
//   3. Проверить блокаут (контракт и проходимость)          122   работает вне Play Mode
//   4. Замер Batches и Tris блокаута (Play Mode)             123   нужен Play Mode, окно Game видно
// Части этапа 1: IslandLayout.cs, IslandField.cs, IslandBlockout.cs (+.Ground, +.Props), IslandCheck.cs (этот файл).
//
// Что проверяет пункт 3:
//   КОНТРАКТ  корень IslandV2 и дети _Generated / _Manual / Markers; все метки из раскладки есть и стоят на своих местах;
//             CAMPFIRE_Point ровно в (0,0,0), такой метки в сцене одна; Terrain один (чужих активных нет); площадь острова в допуске.
//   МЕСТО     здания и метки внутри острова, на суше, на ровном месте; коробки зданий не налезают друг на друга; тропы не лезут в воду вне брода.
//   ФИЗИКА    лучи вниз: на метках есть земля нужной высоты (Terrain-коллайдер), за краем острова земли нет (по контуру).
//   УКЛОН     доля клеток круче допустимого уклона.
//   ПРОХОД    заливка по сетке 1 м от SPAWN_Player: доходит ли игрок до подхода к каждому зданию, ForestGate, PAD_Forest, DEFERRED_Bridge, левого берега за бродом;
//             доля достижимой земли и самые большие недостижимые «карманы» (с координатами).
// Правила воды: вода (река вне брода, пруд) считается непроходимой, если включён waterBlocksWalk. Физически блокаут воду не держит: русло мелкое, Terrain сплошной.
//   ЗОНЫ      (A3) все якоря 16 зон есть, 6 мест рабочих рядом с палаткой, кейс питомцев рядом.   ТРОПЫ (A4) длины, время до моста, примыкание.   ДИСТАНЦИИ расстояние ходьбы до зон.  Код: IslandCheck.Zones.cs
// Результат: Console и буфер обмена.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static partial class IslandCheck
    {
        static int ok, warn, fail;
        static StringBuilder sb;

        static void Ok(string s) { ok++; sb.AppendLine("  [ок] " + s); }
        static void Warn(string s) { warn++; sb.AppendLine("  [!] " + s); }
        static void Fail(string s) { fail++; sb.AppendLine("  [НЕТ] " + s); }

        static void Finish()
        {
            sb.AppendLine("Итого: ок " + ok + ", предупреждений " + warn + ", провалов " + fail + ".");
            if (fail > 0) Debug.LogError(sb.ToString()); else if (warn > 0) Debug.LogWarning(sb.ToString()); else Debug.Log(sb.ToString());
            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        // ================================================================ пункт 3
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/3. Проверить блокаут (контракт и проходимость)", false, 122)]
        static void Run()
        {
            ok = warn = fail = 0;
            sb = new StringBuilder("IslandV2: проверка блокаута (этап 1)\n");

            var layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (layout == null) { Fail("нет раскладки " + IslandLayout.AssetPath + " (пункт 1 создаст)"); Finish(); return; }
            Ok("раскладка найдена");
            if (Shader.Find(IslandBlockout.ShaderName) != null) Ok("шейдер " + IslandBlockout.ShaderName + " найден");
            else Fail("шейдер " + IslandBlockout.ShaderName + " не найден (Shaders/IslandV2/IslandVertexColor.shader)");

            var f = new IslandField(layout);
            CheckArea(f);

            var rootGo = IslandBlockout.FindRoot();
            if (rootGo == null) { Fail("в сцене нет корня IslandV2 (пункт 2 соберёт блокаут)"); Finish(); return; }
            Transform root = rootGo.transform;

            CheckStructure(root);
            CheckMarkers(f, root);
            CheckZones(f);          // A3: якоря всех зон на месте (IslandCheck.Zones.cs)
            CheckForeign(root);
            CheckCliff(f, root);
            CheckRiver(f, root);
            CheckPlacement(f);
            CheckPaths(f);
            CheckTrails(f);         // A4: длины троп, время до моста, примыкание троп (IslandCheck.Zones.cs)
            CheckPhysics(f);
            CheckWalk(f, root);
            Finish();
        }

        // ---------------------------------------------------------------- площадь
        static void CheckArea(IslandField f)
        {
            float target = f.L.targetArea, tol = f.L.areaTolerance;
            float dev = Mathf.Abs(f.Area - target) / Mathf.Max(1f, target);
            string s = "площадь острова " + f.Area.ToString("0") + " м² (цель " + target.ToString("0") + ", допуск ±" + (tol * 100f).ToString("0.#") + "%)";
            if (dev <= tol) Ok(s); else Fail(s + ": вне допуска");
        }

        // ---------------------------------------------------------------- структура
        static void CheckStructure(Transform root)
        {
            foreach (var n in new[] { IslandBlockout.GeneratedName, IslandBlockout.ManualName, IslandBlockout.MarkersName })
            {
                if (root.Find(n) != null) Ok("IslandV2/" + n + " есть"); else Fail("нет IslandV2/" + n);
            }
            var gen = root.Find(IslandBlockout.GeneratedName);
            if (gen == null) return;
            var terrains = gen.GetComponentsInChildren<Terrain>(true);
            if (terrains.Length == 1) Ok("Terrain-коллайдер один");
            else Fail("Terrain под _Generated: " + terrains.Length + " (нужен один)");
            if (terrains.Length == 1)
            {
                if (!terrains[0].drawHeightmap) Ok("Terrain невидим (drawHeightmap выключен)");
                else Warn("Terrain видим: должен быть невидим, землю рисуют меш-чанки");
                if (terrains[0].GetComponent<TerrainCollider>() == null) Fail("у Terrain нет TerrainCollider");
            }
            int chunks = 0;
            foreach (var mr in gen.GetComponentsInChildren<MeshRenderer>(true)) if (mr.name.StartsWith("Ground_Chunk_")) chunks++;
            if (chunks > 0) Ok("видимых чанков земли " + chunks); else Fail("нет чанков земли Ground_Chunk_*");
        }

        // ---------------------------------------------------------------- метки
        static void CheckMarkers(IslandField f, Transform root)
        {
            var markers = root.Find(IslandBlockout.MarkersName);
            if (markers == null) return;
            foreach (var a in f.L.anchors)
            {
                var m = markers.Find(a.name);
                if (m == null) { Fail("нет метки " + a.name + " в Markers"); continue; }
                Vector2 p = f.AnchorWorld(a);
                float y = a.name == "CAMPFIRE_Point" ? 0f : f.Height(p.x, p.y);
                float dh = Vector2.Distance(new Vector2(m.position.x, m.position.z), p), dy = Mathf.Abs(m.position.y - y);
                if (dh > 0.05f || dy > 0.05f) Warn("метка " + a.name + " сдвинута от раскладки на " + dh.ToString("0.00") + " м по плану и " + dy.ToString("0.00") + " м по высоте (двигал руками? пункт 2 вернёт на место)");
                else Ok("метка " + a.name + " на месте (" + m.position.x.ToString("0.0") + ", " + m.position.y.ToString("0.0") + ", " + m.position.z.ToString("0.0") + ")");
            }
            var camp = markers.Find("CAMPFIRE_Point");
            if (camp != null)
            {
                if (camp.position.sqrMagnitude < 1e-4f) Ok("CAMPFIRE_Point ровно в (0,0,0)");
                else Fail("CAMPFIRE_Point не в нуле: " + camp.position + ". В IslandLayout.asset у метки CAMPFIRE_Point должен быть тип Building и pos (0,0) (EdgePoint даёт позицию на краю). Пункт 2 «Собрать блокаут» теперь чинит это сам");
            }
            foreach (var n in new[] { "CAMPFIRE_Point", "SPAWN_Player", "ForestGate", "DEFERRED_Bridge" })
            {
                int cnt = 0;
                foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (tr.name == n) cnt++;
                if (cnt == 1) Ok("метка " + n + " в сцене одна"); else if (cnt > 1) Fail("метка " + n + " в сцене " + cnt + " раза (старая локация не отключена?)");
            }
        }

        // ---------------------------------------------------------------- обрыв (этап 4)
        static void CheckCliff(IslandField f, Transform root)
        {
            if (!f.L.buildCliff) return;
            var gen = root.Find(IslandBlockout.GeneratedName);
            var cliff = gen != null ? gen.Find(IslandCliff.ObjectName) : null;
            if (cliff == null) { Fail("нет обрыва _Generated/Cliff (пункт 2 «Собрать блокаут» построит)"); return; }
            var mf = cliff.GetComponent<MeshFilter>();
            var mr = cliff.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null) { Fail("у Cliff нет меша"); return; }
            int tris = mf.sharedMesh.triangles.Length / 3;
            if (tris > 0 && tris < 12000) Ok("обрыв: " + tris + " tris, один рендерер"); else Warn("обрыв: " + tris + " tris (ожидали 1000-12000)");
            if (mr == null || mr.sharedMaterial == null || mr.sharedMaterial.shader == null || mr.sharedMaterial.shader.name != IslandCliff.RockShaderName)
                Fail("у Cliff нет материала на шейдере " + IslandCliff.RockShaderName);
            else Ok("материал обрыва на " + IslandCliff.RockShaderName);
            var data = root.GetComponent<IslandV2Data>();
            if (data == null) Fail("на IslandV2 нет IslandV2Data (пункт 2 повесит)");
            else if (data.outline == null || data.outline.Count < 3) Fail("IslandV2Data: контур пустой (пункт 2 заполнит)");
            else if (data.rockMaterial == null) Warn("IslandV2Data: не задан материал скалы (пункт 2 задаст)");
            else Ok("IslandV2Data заполнена: контур " + data.outline.Count + " точек, глубина " + data.depth.ToString("0.0") + " м");
            float lowest = mf.sharedMesh.bounds.min.y;
            if (lowest > -3f) Warn("низ обрыва всего " + lowest.ToString("0.0") + " м, ожидали глубже (cliffWall + cliffTaper)");
        }

        // ---------------------------------------------------------------- река (этап E)
        static void CheckRiver(IslandField f, Transform root)
        {
            var L = f.L;
            if (f.RiverPolyline.Length < 2) return;

            // 1. стены русла крутые нарочно (под мост), но у брода откос должен быть проходимым: смотрим поперёк реки в 8 м от каждого брода
            var pl = f.RiverPolyline;
            for (int ci = 0; ci < L.crossings.Count; ci++)
            {
                float worst = 0f; Vector2 worstAt = Vector2.zero;
                for (int i = 0; i + 1 < pl.Length; i++)
                {
                    Vector2 a = pl[i], b = pl[i + 1];
                    if (Vector2.Distance(a, L.crossings[ci].pos) > 8f) continue;
                    Vector2 t = (b - a).normalized, n = new Vector2(-t.y, t.x);
                    for (float d = L.riverWidth * 0.5f + 0.3f; d < L.riverWidth * 0.5f + 14f; d += 0.5f)
                    {
                        for (int sgn = -1; sgn <= 1; sgn += 2)
                        {
                            Vector2 p = a + n * (d * sgn);
                            if (!f.Inside(p.x, p.y) || f.FordWeight(p.x, p.y) < 0.9f) continue;
                            float sl = f.Slope(p.x, p.y);
                            if (sl > worst) { worst = sl; worstAt = p; }
                        }
                    }
                }
                string ws = "откос у брода «" + L.crossings[ci].pos + "»: самый крутой " + worst.ToString("0") + "° в (" + worstAt.x.ToString("0") + ", " + worstAt.y.ToString("0") + "), допустимо " + L.maxWalkSlopeDeg.ToString("0") + "°";
                if (worst <= L.maxWalkSlopeDeg) Ok(ws); else Warn(ws + " (увеличь riverFordRun)");
            }

            // 2. вода течёт вниз: поверхность у устья ниже, чем у истока
            float sIn = f.RiverSurfaceY(0f), sOut = f.RiverSurfaceY(1f);
            if (sOut < sIn - 0.01f || L.riverDrop <= 0.01f) Ok("вода течёт вниз: " + sIn.ToString("0.00") + " м у истока, " + sOut.ToString("0.00") + " м у устья");
            else Fail("поверхность реки не опускается к устью");

            // 3. брод остаётся бродом: вода на нём мельче порога плавания
            for (int i = 0; i < L.crossings.Count; i++)
            {
                var c = L.crossings[i];
                float fd = f.RiverWaterDepthAt(1f, 0f);
                if (fd < 0.9f) Ok("брод «" + c.pos + "»: вода " + fd.ToString("0.00") + " м, переходим вброд");
                else Fail("брод «" + c.pos + "»: вода " + fd.ToString("0.00") + " м, глубже порога плавания (уменьши fordDepthShare)");
            }

            // 4. вода в сцене
            if (!L.buildRiverWater) return;
            var gen = root.Find(IslandBlockout.GeneratedName);
            var water = gen != null ? gen.Find(IslandRiverWater.ObjectName) : null;
            if (water == null) { Fail("нет воды _Generated/" + IslandRiverWater.ObjectName + " (пункт 2 «Собрать блокаут» построит)"); return; }
            var mf = water.GetComponent<MeshFilter>(); var mr = water.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null) { Fail("у River_Water нет меша"); return; }
            if (mr == null || mr.sharedMaterial == null || mr.sharedMaterial.shader == null || mr.sharedMaterial.shader.name != IslandRiverWater.WaterShaderName)
                Fail("у River_Water нет материала на шейдере " + IslandRiverWater.WaterShaderName);
            else Ok("вода: " + (mf.sharedMesh.triangles.Length / 3) + " tris, один рендерер, материал на " + IslandRiverWater.WaterShaderName);
            var vols = gen.GetComponentsInChildren<WaterVolume>(true);
            if (vols.Length > 0) Ok("объёмов воды для плавания: " + vols.Length); else Fail("нет WaterVolume под _Generated (плавание и утопление не заработают)");
            int outlets = 0;
            foreach (var e in f.RiverEdges) if (e.isOutlet) outlets++;
            if (outlets > 0) Ok("выходов реки с острова (водопадов): " + outlets); else Warn("река не уходит с острова, водопада нет");
        }

        static void CheckForeign(Transform root)
        {
            var foreign = IslandBlockout.FindForeign(root);
            if (foreign.Count == 0) Ok("объектов старой локации вне IslandV2 нет (Terrain, CAMPFIRE_Point, FloatingIsland)");
            else Fail("в сцене активны объекты старой локации: " + string.Join("; ", foreign));
        }

        // ---------------------------------------------------------------- место
        static void CheckPlacement(IslandField f)
        {
            var L = f.L;
            var buildings = new List<IslandLayout.Anchor>();
            foreach (var a in L.anchors)
            {
                Vector2 p = f.AnchorWorld(a);
                float edge = f.EdgeDistance(p.x, p.y);
                if (edge < 1f) Fail(a.name + ": " + (edge < 0f ? "вне острова" : "в " + edge.ToString("0.0") + " м от края"));
                if (f.IsWater(p.x, p.y)) Fail(a.name + ": стоит в воде");
                if (a.kind != IslandLayout.AnchorKind.Building) continue;
                buildings.Add(a);
                // углы коробки: внутри острова, на суше, на ровном месте
                Vector2 fw = IslandField.Forward(a.yawDeg), rt = new Vector2(fw.y, -fw.x);
                float maxDy = 0f; bool badCorner = false;
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                    Vector2 c = p + rt * (sx * a.footprint.x * 0.5f) + fw * (sz * a.footprint.y * 0.5f);
                    if (f.EdgeDistance(c.x, c.y) < 0.5f || f.IsWater(c.x, c.y)) badCorner = true;
                    maxDy = Mathf.Max(maxDy, Mathf.Abs(f.Height(c.x, c.y) - f.Height(p.x, p.y)));
                }
                if (badCorner) Fail(a.name + ": угол коробки за краем острова или в воде");
                if (maxDy > 0.2f) Warn(a.name + ": под коробкой перепад высоты " + maxDy.ToString("0.00") + " м (площадка должна быть ровной, увеличь flatRadius)");
                else Ok(a.name + ": площадка ровная (перепад " + maxDy.ToString("0.00") + " м), внутри острова, на суше");
            }
            for (int i = 0; i < buildings.Count; i++)
            {
                for (int j = i + 1; j < buildings.Count; j++)
                {
                    var a = buildings[i]; var b = buildings[j];
                    Vector2 pa = f.AnchorWorld(a), pb = f.AnchorWorld(b);
                    float ra = a.footprint.magnitude * 0.5f, rb = b.footprint.magnitude * 0.5f;
                    if (Vector2.Distance(pa, pb) < ra + rb - 0.01f && f.RectDistance(a, pb.x, pb.y) < 0.01f)
                        Fail("коробки " + a.name + " и " + b.name + " налезают друг на друга");
                }
            }
        }

        static void CheckPaths(IslandField f)
        {
            for (int i = 0; i < f.L.paths.Count; i++)
            {
                var pl = f.PathPolyline(i);
                int outside = 0, water = 0;
                for (int k = 0; k < pl.Length; k++)
                {
                    if (f.EdgeDistance(pl[k].x, pl[k].y) < 0.5f) outside++;
                    if (f.IsWater(pl[k].x, pl[k].y)) water++;
                }
                string n = "тропа «" + f.L.paths[i].name + "»";
                if (outside > 0) Fail(n + ": " + outside + " точек у края или за краем острова");
                else if (water > 0) Fail(n + ": " + water + " точек в воде вне брода");
                else Ok(n + " целиком на суше внутри острова");
            }
        }

        // ---------------------------------------------------------------- физика
        static void CheckPhysics(IslandField f)
        {
            Physics.SyncTransforms();
            int bad = 0;
            foreach (var a in f.L.anchors)
            {
                Vector2 p = f.AnchorWorld(a);
                RaycastHit hit;
                if (!Physics.Raycast(new Vector3(p.x, 60f, p.y), Vector3.down, out hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                { Fail("луч вниз на " + a.name + " ничего не нашёл: нет коллайдера земли"); bad++; continue; }
                float want = a.name == "CAMPFIRE_Point" ? 0f : f.Height(p.x, p.y);
                bool isTerrain = hit.collider is TerrainCollider;
                // у коробок здания луч сверху попадает на крышу: ищем именно Terrain отдельно
                if (!isTerrain)
                {
                    var hits = Physics.RaycastAll(new Vector3(p.x, 60f, p.y), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
                    hit = default(RaycastHit); bool found = false;
                    for (int i = 0; i < hits.Length; i++) if (hits[i].collider is TerrainCollider) { hit = hits[i]; found = true; break; }
                    if (!found) { Fail("под " + a.name + " нет Terrain-коллайдера"); bad++; continue; }
                }
                float dy = Mathf.Abs(hit.point.y - want);
                if (dy > 0.25f) { Fail("на " + a.name + " коллайдер земли на высоте " + hit.point.y.ToString("0.00") + ", а по раскладке " + want.ToString("0.00")); bad++; }
            }
            if (bad == 0) Ok("на всех метках есть коллайдер земли нужной высоты");

            // край: изнутри земля есть, снаружи нет
            int n = 32, inMiss = 0, outHit = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 c = f.Contour[i * f.Contour.Count / n];
                Vector2 center = f.L.centerOffset;
                Vector2 dir = (c - center).normalized;
                Vector2 pin = c - dir * 1.5f, pout = c + dir * 1.5f;
                if (!TerrainAt(pin)) inMiss++;
                if (TerrainAt(pout)) outHit++;
            }
            if (inMiss == 0) Ok("у края изнутри (1.5 м) земля есть во всех " + n + " точках"); else Fail("у края изнутри земли нет в " + inMiss + " из " + n + " точек");
            if (outHit == 0) Ok("за краем (1.5 м) земли нет во всех " + n + " точках: игрок упадёт, как задумано");
            else Fail("за краем земля есть в " + outHit + " из " + n + " точек. Если почти везде, дыры Terrain вышли наоборот: включи terrainHolesInverted в раскладке и пересобери");
        }

        static bool TerrainAt(Vector2 p)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, 60f, p.y), Vector3.down, 140f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++) if (hits[i].collider is TerrainCollider) return true;
            return false;
        }

        // ---------------------------------------------------------------- проходимость
        sealed class Grid
        {
            public float ox, oz;
            public int nx, nz;
            public bool[] walk;
            public int[] label;   // 0 не помечено, 1 достижимо, 2 и больше: недостижимые карманы
            public int Idx(int i, int j) { return j * nx + i; }
        }

        static void CheckWalk(IslandField f, Transform root)
        {
            var L = f.L;
            var g = new Grid();
            g.ox = Mathf.Floor(f.Min.x) - 1f; g.oz = Mathf.Floor(f.Min.y) - 1f;
            g.nx = Mathf.CeilToInt(f.Max.x - g.ox) + 2; g.nz = Mathf.CeilToInt(f.Max.y - g.oz) + 2;
            g.walk = new bool[g.nx * g.nz];
            g.label = new int[g.nx * g.nz];

            // препятствия
            var blockers = new List<IslandLayout.Anchor>();
            foreach (var a in L.anchors) if (a.kind == IslandLayout.AnchorKind.Building && a.blocksWalk) blockers.Add(a);
            var trees = f.TreePoints();
            var treeHash = new Dictionary<long, List<Vector2>>();
            foreach (var t in trees)
            {
                long key = Key(Mathf.FloorToInt(t.x / 4f), Mathf.FloorToInt(t.y / 4f));
                List<Vector2> list;
                if (!treeHash.TryGetValue(key, out list)) { list = new List<Vector2>(); treeHash[key] = list; }
                list.Add(t);
            }
            var gen = root.Find(IslandBlockout.GeneratedName);
            var holder = gen != null ? gen.Find("BLK_TreeColliders") : null;
            if (holder != null && holder.childCount != trees.Count)
                Warn("в сцене " + holder.childCount + " коллайдеров деревьев, по раскладке " + trees.Count + ": блокаут собран с другими числами, пересобери (пункт 2), иначе проходимость считается по новой раскладке");

            float pr = L.playerRadius;
            int walkable = 0, steep = 0, landCells = 0, riverWall = 0;
            for (int j = 0; j < g.nz; j++)
            {
                for (int i = 0; i < g.nx; i++)
                {
                    float x = g.ox + i, z = g.oz + j;
                    if (f.EdgeDistance(x, z) < 0.6f) continue;
                    landCells++;
                    if (L.waterBlocksWalk && f.IsWater(x, z)) continue;
                    if (f.Slope(x, z) > L.maxWalkSlopeDeg)
                    {
                        if (f.RiverDistance(x, z) < L.riverWidth * 0.5f + 16f) riverWall++;   // стены русла крутые нарочно (E)
                        else steep++;
                        continue;
                    }
                    bool blocked = false;
                    for (int b = 0; b < blockers.Count && !blocked; b++) if (f.RectDistance(blockers[b], x, z) < pr) blocked = true;
                    if (!blocked) blocked = NearTree(treeHash, x, z, L.trunkRadius + pr);
                    if (blocked) continue;
                    g.walk[g.Idx(i, j)] = true;
                    walkable++;
                }
            }
            float steepShare = landCells > 0 ? (float)steep / landCells : 0f;
            string ss = "круче " + L.maxWalkSlopeDeg.ToString("0") + "° " + (steepShare * 100f).ToString("0.0") + "% земли (" + steep + " клеток по 1 м)";
            if (steepShare <= 0.02f) Ok(ss); else Warn(ss + ": уменьши hillAmp или увеличь hillScale");
            sb.AppendLine("  земли (клеток 1 м): " + landCells + ", проходимых " + walkable + ", деревьев-препятствий " + trees.Count + ", стен русла " + riverWall);

            // старт
            var spawnA = L.FindAnchor("SPAWN_Player");
            Vector2 sp = spawnA != null ? f.AnchorWorld(spawnA) : Vector2.zero;
            int si, sj;
            if (!Nearest(g, sp, 3f, out si, out sj)) { Fail("рядом с SPAWN_Player нет проходимой земли: заливку не начать"); return; }

            Flood(g, si, sj, 1);
            int reached = 0;
            for (int k = 0; k < g.label.Length; k++) if (g.label[k] == 1) reached++;
            float share = walkable > 0 ? (float)reached / walkable : 0f;
            string rs = "от SPAWN_Player достижимо " + reached + " из " + walkable + " проходимых клеток (" + (share * 100f).ToString("0.0") + "%, нужно не меньше " + (L.minReachableShare * 100f).ToString("0") + "%)";
            if (share >= L.minReachableShare) Ok(rs); else Fail(rs);

            // подходы
            foreach (var a in L.anchors)
            {
                if (a.name == "SPAWN_Player") continue;
                Vector2 target = Approach(f, a);
                int ci, cj;
                if (!Nearest(g, target, 2.0f, out ci, out cj)) { Fail("подход к " + a.name + " (" + target.x.ToString("0.0") + ", " + target.y.ToString("0.0") + "): рядом нет проходимой земли"); continue; }
                if (g.label[g.Idx(ci, cj)] == 1) Ok("до " + a.name + " можно дойти");
                else Fail("до " + a.name + " дойти нельзя (подход " + target.x.ToString("0.0") + ", " + target.y.ToString("0.0") + "): отрезан лесом, водой или склоном");
            }
            ReportDistances(f, g, si, sj);   // A3/A4: расстояния ходьбы до зон (IslandCheck.Zones.cs)
            foreach (var m in L.meadows)
            {
                int ci, cj;
                if (!Nearest(g, m.pos, 3f, out ci, out cj)) { Warn("поляна в (" + m.pos.x.ToString("0") + ", " + m.pos.y.ToString("0") + "): нет проходимой земли в центре"); continue; }
                if (g.label[g.Idx(ci, cj)] == 1) Ok("до поляны (" + m.pos.x.ToString("0") + ", " + m.pos.y.ToString("0") + ") можно дойти");
                else Fail("до поляны (" + m.pos.x.ToString("0") + ", " + m.pos.y.ToString("0") + ") дойти нельзя (за рекой без брода?)");
            }

            // карманы
            var pockets = new List<KeyValuePair<int, Vector2>>();
            int lab = 2;
            for (int j = 0; j < g.nz; j++)
            {
                for (int i = 0; i < g.nx; i++)
                {
                    int k = g.Idx(i, j);
                    if (!g.walk[k] || g.label[k] != 0) continue;
                    Vector2 sum; int size = Flood(g, i, j, lab, out sum);
                    pockets.Add(new KeyValuePair<int, Vector2>(size, sum / Mathf.Max(1, size)));
                    lab++;
                }
            }
            pockets.Sort((a, b) => b.Key.CompareTo(a.Key));
            if (pockets.Count == 0) Ok("недостижимых карманов нет");
            else
            {
                sb.AppendLine("  недостижимых карманов: " + pockets.Count + ", самые большие:");
                for (int i = 0; i < pockets.Count && i < 3; i++)
                    sb.AppendLine("    " + pockets[i].Key + " клеток около (" + (g.ox + pockets[i].Value.x).ToString("0") + ", " + (g.oz + pockets[i].Value.y).ToString("0") + ")");
                int big = pockets[0].Key;
                if (big >= 80) Warn("есть недостижимый карман в " + big + " клеток: сделай там проход (тропа, поляна) или убери лес");
            }
        }

        static Vector2 Approach(IslandField f, IslandLayout.Anchor a)
        {
            Vector2 c = f.AnchorWorld(a);
            if (a.kind == IslandLayout.AnchorKind.Building && a.approachDist > 0f)
                return c + IslandField.Forward(a.yawDeg) * (a.footprint.y * 0.5f + a.approachDist);
            return c;
        }

        static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

        static bool NearTree(Dictionary<long, List<Vector2>> hash, float x, float z, float r)
        {
            int cx = Mathf.FloorToInt(x / 4f), cz = Mathf.FloorToInt(z / 4f);
            float r2 = r * r;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<Vector2> list;
                    if (!hash.TryGetValue(Key(cx + dx, cz + dz), out list)) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        float ddx = list[i].x - x, ddz = list[i].y - z;
                        if (ddx * ddx + ddz * ddz < r2) return true;
                    }
                }
            }
            return false;
        }

        static bool Nearest(Grid g, Vector2 p, float radius, out int bi, out int bj)
        {
            bi = bj = 0;
            float best = float.MaxValue;
            int r = Mathf.CeilToInt(radius);
            int ci = Mathf.RoundToInt(p.x - g.ox), cj = Mathf.RoundToInt(p.y - g.oz);
            for (int j = cj - r; j <= cj + r; j++)
            {
                for (int i = ci - r; i <= ci + r; i++)
                {
                    if (i < 0 || j < 0 || i >= g.nx || j >= g.nz || !g.walk[g.Idx(i, j)]) continue;
                    float d = new Vector2(g.ox + i - p.x, g.oz + j - p.y).magnitude;
                    if (d <= radius && d < best) { best = d; bi = i; bj = j; }
                }
            }
            return best < float.MaxValue;
        }

        static void Flood(Grid g, int si, int sj, int lab)
        {
            Vector2 s;
            Flood(g, si, sj, lab, out s);
        }

        // заливка в 8 направлениях, по диагонали не режем углы препятствий; возвращает число клеток и сумму координат клеток (в клетках сетки)
        static int Flood(Grid g, int si, int sj, int lab, out Vector2 sum)
        {
            sum = Vector2.zero;
            int count = 0;
            var q = new Queue<int>();
            g.label[g.Idx(si, sj)] = lab;
            q.Enqueue(g.Idx(si, sj));
            while (q.Count > 0)
            {
                int k = q.Dequeue();
                int i = k % g.nx, j = k / g.nx;
                count++;
                sum += new Vector2(i, j);
                for (int dj = -1; dj <= 1; dj++)
                {
                    for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= g.nx || nj >= g.nz) continue;
                        int nk = g.Idx(ni, nj);
                        if (!g.walk[nk] || g.label[nk] != 0) continue;
                        if (di != 0 && dj != 0 && (!g.walk[g.Idx(i + di, j)] || !g.walk[g.Idx(i, j + dj)])) continue;
                        g.label[nk] = lab;
                        q.Enqueue(nk);
                    }
                }
            }
            return count;
        }

        // ================================================================ пункт 4: замер в Play Mode
        static int frames, step;
        static bool probing;
        static int aBatches, aTris, aSetPass, bBatches, bTris, bSetPass;
        static List<MeshRenderer> probed = new List<MeshRenderer>();

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/4. Замер Batches и Tris блокаута (Play Mode)", false, 123)]
        static void RunProbe()
        {
            ok = warn = fail = 0;
            sb = new StringBuilder("IslandV2: замер блокаута (этап 1)\n");
            if (!Application.isPlaying)
            {
                sb.AppendLine("  Запусти Play Mode (окно Game видно, игрок на острове) и вызови пункт ещё раз.");
                Finish();
                return;
            }
            if (probing) return;
            var rootGo = IslandBlockout.FindRoot();
            var gen = rootGo != null ? rootGo.transform.Find(IslandBlockout.GeneratedName) : null;
            if (gen == null) { Fail("в игре нет IslandV2/_Generated"); Finish(); return; }
            probed.Clear();
            foreach (var mr in gen.GetComponentsInChildren<MeshRenderer>(true)) if (mr.enabled) probed.Add(mr);
            probing = true; frames = 0; step = 0;
            EditorApplication.update += Tick;
            Debug.Log("IslandV2: замер блокаута пошёл (~70 кадров), подожди.");
        }

        static void SetProbed(bool on)
        {
            for (int i = 0; i < probed.Count; i++) if (probed[i] != null) probed[i].enabled = on;
        }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; probing = false; return; }
            frames++;
            if (step == 0 && frames >= 45)
            {
                aBatches = UnityStats.batches; aTris = UnityStats.triangles; aSetPass = UnityStats.setPassCalls;
                SetProbed(false);   // выключаем только рендеры: коллайдеры остаются, игрок не провалится
                step = 1; frames = 0;
            }
            else if (step == 1 && frames >= 20)
            {
                bBatches = UnityStats.batches; bTris = UnityStats.triangles; bSetPass = UnityStats.setPassCalls;
                SetProbed(true);
                EditorApplication.update -= Tick; probing = false;
                EvaluateProbe();
            }
        }

        static void EvaluateProbe()
        {
            var layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (layout == null) { Fail("нет раскладки"); Finish(); return; }
            LightQualityLevel q = GameSettings.LightQuality;
            var b = layout.GetBudget(q);
            string pname = q == LightQualityLevel.Low ? "Mobile Low" : q == LightQualityLevel.Medium ? "Mobile High" : "ПК";
            sb.AppendLine("  профиль: " + pname + " (качество света " + q + ")");
            sb.AppendLine("  с блокаутом: Batches " + aBatches + ", SetPass " + aSetPass + ", Tris " + aTris);
            sb.AppendLine("  без блокаута: Batches " + bBatches + ", SetPass " + bSetPass + ", Tris " + bTris);
            int dB = aBatches - bBatches, dT = aTris - bTris;
            if (dB <= b.maxExtraBatches) Ok("блокаут добавил " + dB + " Batches (лимит " + b.maxExtraBatches + ")"); else Fail("блокаут добавил " + dB + " Batches, лимит " + b.maxExtraBatches + ": увеличь chunkCells или проверь, что у заглушек один материал");
            if (dT <= b.maxExtraTris) Ok("блокаут добавил " + dT + " Tris (лимит " + b.maxExtraTris + ")"); else Fail("блокаут добавил " + dT + " Tris, лимит " + b.maxExtraTris + ": увеличь meshCell или treeSpacing");
            int chunks = 0;
            for (int i = 0; i < probed.Count; i++) if (probed[i] != null && probed[i].name.StartsWith("Ground_Chunk_")) chunks++;
            if (chunks <= b.maxGroundChunks) Ok("чанков земли " + chunks + " (лимит " + b.maxGroundChunks + ")"); else Fail("чанков земли " + chunks + ", лимит " + b.maxGroundChunks);
            sb.AppendLine("  Замер зависит от камеры: сделай его с игровой камеры и один раз сверху (OrbitCamera). Тени и пост-эффекты в замер входят как есть.");
            Finish();
        }
    }
}
