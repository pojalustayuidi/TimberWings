// Assets/_Project/Editor/IslandV2/IslandBlockout.Props.cs
// Остров v2, этап 1: серые заглушки и метки. Часть partial-класса IslandBlockout (остальные части: IslandBlockout.cs, IslandBlockout.Ground.cs).
//
// Методы:
//   BuildBuildings(field, gen, mat)             серые коробки BLK_Hut, BLK_Market, BLK_Tent, BLK_QuestBoard, BLK_Campfire: меш + BoxCollider, статический батчинг
//   BuildTrees(field, gen, mat, out count)      лес-заглушки: ВСЕ деревья одним мешем (BLK_Trees, 1 draw call) + капсулы-коллайдеры на пустых детях BLK_TreeColliders (без рендера)
//   BuildMarkers(field, markers)                метки в Markers: создаёт недостающие, существующие переставляет на место из раскладки
//   MeshAcc                                     маленький сборщик мешей с плоскими гранями (грань сама поворачивается наружу от внутренней точки)
//   BlkName(anchorName)                         имя коробки из имени метки (PAD_Hut -> BLK_Hut)
// Настоящие здания потом ставят билдеры «Здания/*» на метки PAD_*, коробку BLK_* при этом убирают.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static partial class IslandBlockout
    {
        // ================================================================ сборщик мешей
        sealed class MeshAcc
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Color> c = new List<Color>();
            public readonly List<int> t = new List<int>();

            // треугольник, лицом наружу от точки inside
            public void Tri(Vector3 a, Vector3 b, Vector3 d, Color col, Vector3 inside)
            {
                Vector3 n = Vector3.Cross(b - a, d - a);
                Vector3 mid = (a + b + d) / 3f;
                if (Vector3.Dot(n, mid - inside) < 0f) { Vector3 tp = b; b = d; d = tp; }
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(d);
                c.Add(col); c.Add(col); c.Add(col);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col, Vector3 inside)
            {
                Tri(a, b, d, col, inside);
                Tri(a, d, e, col, inside);
            }

            // коробка с низом на y = base, повёрнута на yaw, без дна
            public void Box(Vector3 center, Vector2 size, float h, float yawDeg, Color col)
            {
                Quaternion q = Quaternion.Euler(0f, yawDeg, 0f);
                float hx = size.x * 0.5f, hz = size.y * 0.5f;
                Vector3[] b = new Vector3[4], tp = new Vector3[4];
                float[] sx = { -1f, -1f, 1f, 1f }, sz = { -1f, 1f, 1f, -1f };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 lo = new Vector3(sx[i] * hx, 0f, sz[i] * hz);
                    b[i] = center + q * lo;
                    tp[i] = center + q * (lo + Vector3.up * h);
                }
                Vector3 inside = center + Vector3.up * (h * 0.5f);
                Color topCol = new Color(Mathf.Min(1f, col.r * 1.12f), Mathf.Min(1f, col.g * 1.12f), Mathf.Min(1f, col.b * 1.12f), 1f);
                Quad(tp[0], tp[1], tp[2], tp[3], topCol, inside);
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    Quad(b[i], b[j], tp[j], tp[i], col, inside);
                }
            }

            // шестигранная призма (ствол), низ на y = base
            public void Prism(Vector3 baseCenter, float radius, float h, Color col)
            {
                const int n = 6;
                Vector3 inside = baseCenter + Vector3.up * (h * 0.5f);
                for (int i = 0; i < n; i++)
                {
                    float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                    Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                    Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                    Quad(p0, p1, p1 + Vector3.up * h, p0 + Vector3.up * h, col, inside);
                }
            }

            // шестигранный конус (крона): основание радиуса radius на y = base, вершина выше на h
            public void Cone(Vector3 baseCenter, float radius, float h, Color col)
            {
                const int n = 6;
                Vector3 apex = baseCenter + Vector3.up * h;
                Vector3 inside = baseCenter + Vector3.up * (h * 0.3f);
                for (int i = 0; i < n; i++)
                {
                    float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                    Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                    Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                    Tri(p0, p1, apex, col, inside);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetColors(c);
                m.SetTriangles(t, 0);
                m.RecalculateNormals();   // вершины не общие: грани плоские
                m.RecalculateBounds();
                return m;
            }
        }

        static Color Gray(float g) { return new Color(g, g, g, 1f).linear; }

        public static string BlkName(string anchorName)
        {
            if (anchorName == "CAMPFIRE_Point") return "BLK_Campfire";
            return "BLK_" + anchorName.Replace("PAD_", "");
        }

        static GameObject NewMeshObject(string name, Transform parent, Mesh mesh, Material mat, bool castShadow)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ================================================================ здания-коробки
        static int BuildBuildings(IslandField f, Transform gen, Material mat)
        {
            int tris = 0;
            var L = f.L;
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.kind != IslandLayout.AnchorKind.Building) continue;
                Vector2 p = f.AnchorWorld(a);
                float y = f.Height(p.x, p.y);
                var acc = new MeshAcc();
                // меш в локальных координатах: объект стоит на метке и повёрнут на yaw здания, коллайдер и вид совпадают
                acc.Box(Vector3.zero, a.footprint, Mathf.Max(0.1f, a.height), 0f, Gray(a.gray));
                var go = NewMeshObject(BlkName(a.name), gen, acc.ToMesh(BlkName(a.name)), mat, true);
                go.transform.position = new Vector3(p.x, y, p.y);
                go.transform.rotation = Quaternion.Euler(0f, a.yawDeg, 0f);
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, a.height * 0.5f, 0f);
                box.size = new Vector3(a.footprint.x, a.height, a.footprint.y);
                box.enabled = a.blocksWalk;
                tris += acc.t.Count / 3;
            }
            return tris;
        }

        // ================================================================ лес-заглушки
        static int BuildTrees(IslandField f, Transform gen, Material mat, out int count)
        {
            var L = f.L;
            var pts = f.TreePoints();
            count = pts.Count;
            var acc = new MeshAcc();
            var holder = new GameObject("BLK_TreeColliders");
            Undo.RegisterCreatedObjectUndo(holder, "Create BLK_TreeColliders");
            holder.transform.SetParent(gen, false);

            float h = Mathf.Max(2f, L.treeHeight);
            float trunkH = h * 0.28f;
            Color trunkCol = Gray(L.treeGray * 0.7f), crownCol = Gray(L.treeGray);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = pts[i];
                float y = f.Height(p.x, p.y);
                Vector3 b = new Vector3(p.x, y, p.y);
                acc.Prism(b, L.trunkRadius * 0.7f, trunkH, trunkCol);
                acc.Cone(b + Vector3.up * (trunkH * 0.7f), L.crownRadius, h - trunkH * 0.7f, crownCol);

                var cgo = new GameObject("t" + i);
                cgo.transform.SetParent(holder.transform, false);
                cgo.transform.position = b + Vector3.up * (h * 0.4f);
                var cap = cgo.AddComponent<CapsuleCollider>();
                cap.radius = L.trunkRadius;
                cap.height = h * 0.8f;
            }
            if (pts.Count == 0) return 0;
            NewMeshObject("BLK_Trees", gen, acc.ToMesh("BLK_Trees"), mat, false);
            return acc.t.Count / 3;
        }

        // ================================================================ метки
        static void BuildMarkers(IslandField f, Transform markers)
        {
            var L = f.L;
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                Vector2 p = f.AnchorWorld(a);
                float y = a.name == "CAMPFIRE_Point" ? 0f : f.Height(p.x, p.y);
                Vector2 fw = IslandField.Forward(a.yawDeg);
                Quaternion rot = Quaternion.LookRotation(new Vector3(fw.x, 0f, fw.y), Vector3.up);

                Transform m = markers.Find(a.name);
                if (m == null)
                {
                    var go = new GameObject(a.name);
                    Undo.RegisterCreatedObjectUndo(go, "Create marker " + a.name);
                    go.transform.SetParent(markers, false);
                    m = go.transform;
                }
                else Undo.RecordObject(m, "Move marker " + a.name);
                m.position = new Vector3(p.x, y, p.y);
                m.rotation = rot;
            }
        }
    }
}
