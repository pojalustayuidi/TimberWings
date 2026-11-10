using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IslandV2
{
    public static class TWRiverValidation
    {
        internal static void CheckMesh(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount < 3 || mesh.triangles.Length == 0)
                throw new InvalidOperationException("Пустой меш речной зоны.");
            var v = mesh.vertices; var t = mesh.triangles;
            foreach (Vector3 p in v)
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z)) throw new InvalidOperationException(mesh.name + ": NaN/Infinity.");
            for (int i = 0; i < t.Length; i += 3)
            {
                if (t[i] < 0 || t[i + 1] < 0 || t[i + 2] < 0 || t[i] >= v.Length || t[i + 1] >= v.Length || t[i + 2] >= v.Length)
                    throw new InvalidOperationException(mesh.name + ": неверный индекс.");
                if (Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).sqrMagnitude < 1e-12f)
                    throw new InvalidOperationException(mesh.name + ": вырожденный треугольник.");
            }
        }

        internal static void CheckRibbon(Mesh mesh, int lipRow)
        {
            CheckMesh(mesh);
            int cols = TWRiverWater.Columns;
            int rows = mesh.vertexCount / cols;
            if (mesh.vertexCount % cols != 0 || lipRow < 1 || lipRow >= rows - 2)
                throw new InvalidOperationException("Неверная сетка общего перегиба.");
            var v = mesh.vertices; var uv = mesh.uv; var n = mesh.normals; var colors = mesh.colors;
            if (uv.Length != v.Length || colors.Length != v.Length || n.Length != v.Length)
                throw new InvalidOperationException("Не полные UV/нормали/маски воды.");
            if (mesh.triangles.Length != (rows - 1) * (cols - 1) * 6)
                throw new InvalidOperationException("Лента разорвана: не все соседние ряды соединены.");
            for (int row = 1; row < rows; row++)
            {
                if (uv[row * cols].y <= uv[(row - 1) * cols].y)
                    throw new InvalidOperationException("UV течения не идут непрерывно вниз по реке.");
                if (row <= lipRow && n[row * cols + cols / 2].y < 0.1f)
                    throw new InvalidOperationException("Перевёрнуты нормали поверхности реки.");
            }
            for (int col = 0; col < cols; col++)
                if (Mathf.Abs(colors[lipRow * cols + col].b - 1f) > 0.0001f)
                    throw new InvalidOperationException("Перегиб воды не должен растворяться.");
            for (int row = lipRow + 1; row < rows; row++)
                if (v[row * cols].y >= v[(row - 1) * cols].y)
                    throw new InvalidOperationException("Водопад должен непрерывно опускаться.");
        }

        internal static void CheckClosed(Mesh mesh)
        {
            var edges = new Dictionary<ulong, int>();
            var direction = new Dictionary<ulong, int>();
            var positions = new Dictionary<Vector3, uint>();
            var vertices = mesh.vertices;
            var welded = new uint[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!positions.TryGetValue(vertices[i], out uint index))
                {
                    index = (uint)positions.Count;
                    positions.Add(vertices[i], index);
                }
                welded[i] = index;
            }
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    uint a = welded[triangles[i + k]], b = welded[triangles[i + (k + 1) % 3]];
                    ulong key = ((ulong)System.Math.Min(a, b) << 32) | System.Math.Max(a, b);
                    edges.TryGetValue(key, out int count); edges[key] = count + 1;
                    direction.TryGetValue(key, out int winding); direction[key] = winding + (a < b ? 1 : -1);
                }
            foreach (var edge in edges)
                if (edge.Value != 2 || direction[edge.Key] != 0)
                    throw new InvalidOperationException("Скала содержит незакрытый край или перевёрнутую грань: " + mesh.name);
        }

        internal static void CheckInstalled(RiverArtState state)
        {
            if (!state.gameObject.activeInHierarchy) throw new InvalidOperationException("Новый речной комплект выключен.");
            if (state.revision != TWRiverSpec.Revision) throw new InvalidOperationException("Установлена старая ревизия речной зоны.");
            var water = state.transform.Find("Water_Source_To_Clouds");
            if (water == null) throw new InvalidOperationException("Нет нового полотна воды.");
            var renderer = water.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || renderer.sharedMaterial == null ||
                renderer.sharedMaterial.shader.name != "Project/IslandV2/TW_RiverRibbon")
                throw new InvalidOperationException("Новый меш воды / материал выключен или потерян.");
            CheckRibbon(water.GetComponent<MeshFilter>().sharedMesh, state.lipRow);
            foreach (var filter in state.GetComponentsInChildren<MeshFilter>())
                if (AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(state.generatedFolder + "/", StringComparison.Ordinal)) CheckMesh(filter.sharedMesh);
            if (state.bridge == null) throw new InvalidOperationException("Потеряна ссылка на речной мост.");
            Matrix4x4 current = state.bridge.localToWorldMatrix;
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(current[i] - state.bridgeMatrix[i]) > 0.0001f)
                    throw new InvalidOperationException("Transform моста изменился после установки.");
            foreach (var entry in state.renderers)
                if (entry.renderer != null && entry.renderer.enabled) throw new InvalidOperationException("Исходное полотно воды снова включено: будет двойная поверхность.");
            foreach (var entry in state.objects)
                if (entry.target != null && entry.target.activeSelf) throw new InvalidOperationException("Старый речной слой включён: " + entry.target.name);
            var source = state.transform.Find("Source_Rock");
            var collider = source != null ? source.GetComponent<MeshCollider>() : null;
            if (collider == null || collider.sharedMesh == null || !collider.enabled || collider.isTrigger)
                throw new InvalidOperationException("Скальный исток должен иметь твёрдый MeshCollider.");
            CheckClosed(collider.sharedMesh);
            if (source.GetComponent<MeshRenderer>().sharedMaterials.Length != collider.sharedMesh.subMeshCount)
                throw new InvalidOperationException("Не все каменные поверхности имеют материал.");
            if (collider.bounds.min.y > state.source.y - 10f)
                throw new InvalidOperationException("Основание истока не закрывает глубину острова.");
            var ends = state.transform.Find("Waterfall_Rock_Edges");
            var endCollider = ends != null ? ends.GetComponent<MeshCollider>() : null;
            if (endCollider == null || endCollider.sharedMesh == null || !endCollider.enabled || endCollider.isTrigger)
                throw new InvalidOperationException("Нет твёрдых скальных краёв водопада.");
            CheckClosed(endCollider.sharedMesh);
            var lamps = state.GetComponentsInChildren<FireSource>();
            if (lamps.Length == 0) throw new InvalidOperationException("Не установлены береговые фонари.");
            foreach (var lamp in lamps)
                if (lamp.emissive == null || lamp.emissive.Length == 0 || lamp.range <= 0f)
                    throw new InvalidOperationException("Фонарь не подключён к свету / свечению.");
            // Cast through both actual rock flanks, not just a component-presence check.
            Physics.SyncTransforms();
            Bounds bounds = collider.bounds;
            Vector3 basePoint = bounds.center;
            basePoint.y = Mathf.Lerp(bounds.min.y, bounds.max.y, 0.55f);
            int hits = 0;
            foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                var ray = new Ray(basePoint + direction * (bounds.extents.magnitude + 1f), -direction);
                if (collider.Raycast(ray, out _, bounds.size.magnitude + 2f)) hits++;
            }
            if (hits < 2) throw new InvalidOperationException("Коллизия скалы не закрывает два фланга. Проверь MeshCollider.");
        }

        static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }

        // No scene objects/assets are created; suitable for -executeMethod as well.
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/RIVER ART/6. Self-test геометрии (без изменения сцены)", false, 186)]
        public static void SelfTest()
        {
            var layout = ScriptableObject.CreateInstance<IslandLayout>();
            try
            {
                layout.ResetToDefaults();
                int tested = 0;
                foreach (float width in new[] { 5f, 10f, 18f })
                    foreach (bool reverse in new[] { false, true })
                    {
                        layout.riverWidth = width;
                        var f = new IslandField(layout);
                        var points = new List<Vector2>(f.RiverInsidePieces()[0]);
                        if (reverse) points.Reverse();
                        var x = new TWRiverCtx { L = layout, f = f, ax = new TWRiverAxis(points, 0.7f), half = width * 0.5f,
                            hasSource = true, pal = new TWRiverPalette(layout), seed = 4004, rockSide = 1 };
                        var result = TWRiverWater.Build(x, 18f);
                        try { CheckRibbon(result.mesh, result.lipRow); tested++; }
                        finally { Object.DestroyImmediate(result.mesh); }
                        Mesh source = TWRiverSource.Build(x);
                        try { CheckMesh(source); CheckClosed(source); }
                        finally { Object.DestroyImmediate(source); }
                        Mesh ends = TWRiverSource.OutletBanks(x, 22f);
                        try { CheckMesh(ends); CheckClosed(ends); }
                        finally { Object.DestroyImmediate(ends); }
                        Mesh shore = TWRiverShore.Build(x);
                        try { CheckMesh(shore); }
                        finally { Object.DestroyImmediate(shore); }
                    }
                if (TWRiverSpec.Smooth(3f, 7f, 3f) != 0f || TWRiverSpec.Smooth(3f, 7f, 7f) != 1f)
                    throw new InvalidOperationException("Неверная нормализация SmoothStep.");
                Debug.Log("RIVER ART SELF-TEST: PASS, " + tested + " вариантов ширины/направления. Общий перегиб, UV, нормали, индексы и падение.");
            }
            finally { Object.DestroyImmediate(layout); }
        }

        public static void BatchSelfTest()
        {
            try { SelfTest(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
