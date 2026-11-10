using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IslandV2
{
    internal static class TWGrovePaths
    {
        const int Side = 45;
        const float Step = 0.7f;
        static Vector2 Position(int id) { return new Vector2(id % Side - Side / 2, id / Side - Side / 2) * Step; }
        static int Nearest(Vector2 p) { return Mathf.Clamp(Mathf.RoundToInt(p.y / Step) + Side / 2, 0, Side - 1) * Side + Mathf.Clamp(Mathf.RoundToInt(p.x / Step) + Side / 2, 0, Side - 1); }

        // A narrow working trail bends around existing trunks. Trees are never moved.
        public static List<Vector2> Route(TWGroveContext x, Vector2 start, Vector2 finish)
        {
            var distance = Enumerable.Repeat(float.PositiveInfinity, Side * Side).ToArray();
            var parent = Enumerable.Repeat(-1, Side * Side).ToArray(); var done = new bool[Side * Side];
            int first = Nearest(start), last = Nearest(finish); distance[first] = 0;
            bool Clear(Vector2 p)
            {
                Vector3 world = x.World(p);
                return p.magnitude < 16.5f && x.trees.All(t => Vector2.Distance(t.point, p) > 1.32f) &&
                    x.buildings.All(b => Vector2.Distance(b.point, p) > b.radius + 0.4f) &&
                    x.field.Inside(world.x, world.z) && !x.field.IsWater(world.x, world.z) &&
                    !x.occupied.Any(f => PointDistance(p, f) < 0.9f);
            }
            if (!Clear(Position(first)) || !Clear(Position(last))) return null;
            for (int iteration = 0; iteration < Side * Side; iteration++)
            {
                int current = -1; float best = float.PositiveInfinity;
                for (int i = 0; i < distance.Length; i++)
                {
                    if (done[i] || float.IsPositiveInfinity(distance[i])) continue;
                    float score = distance[i] + Vector2.Distance(Position(i), Position(last));
                    if (score < best) { best = score; current = i; }
                }
                if (current < 0) return null;
                if (current == last)
                {
                    var points = new List<Vector2>();
                    for (int id = last; id >= 0; id = parent[id]) points.Add(Position(id));
                    points.Reverse(); return points;
                }
                done[current] = true;
                int cx = current % Side, cy = current / Side;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0 && dy == 0) || cx + dx < 0 || cx + dx >= Side || cy + dy < 0 || cy + dy >= Side) continue;
                    int next = (cy + dy) * Side + cx + dx;
                    if (done[next] || !Clear(Position(next))) continue;
                    if (dx != 0 && dy != 0 && (!Clear(Position(cy * Side + cx + dx)) || !Clear(Position((cy + dy) * Side + cx)))) continue;
                    float candidate = distance[current] + Step * (dx != 0 && dy != 0 ? 1.4142136f : 1f);
                    if (candidate < distance[next]) { distance[next] = candidate; parent[next] = current; }
                }
            }
            return null;
        }

        public static float PointDistance(Vector2 p, TWGroveContext.Footprint f)
        {
            float a = f.yaw * Mathf.Deg2Rad; Vector2 d = p - f.point;
            Vector2 q = new Vector2(Mathf.Abs(d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a)), Mathf.Abs(d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a))) - f.half;
            return new Vector2(Mathf.Max(0, q.x), Mathf.Max(0, q.y)).magnitude;
        }

        public static float Distance(Vector2 p, IList<Vector2> line)
        {
            float d = float.MaxValue;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i], v = line[i + 1] - a;
                float t = v.sqrMagnitude < 0.00001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, v) / v.sqrMagnitude);
                d = Mathf.Min(d, Vector2.Distance(p, a + v * t));
            }
            return d;
        }

        public static void Build(TWGroveContext x, Transform gate)
        {
            Vector2 entrance = x.Local(gate.position);
            var main = Route(x, x.Local(gate.TransformPoint(new Vector3(0, 0, 1.7f))), x.hub);
            if (main == null) throw new InvalidOperationException("Нет свободного подхода от арки к центру. Деревья и стройки не перемещались.");
            main.Insert(0, entrance); main.Insert(0, x.Local(gate.TransformPoint(new Vector3(0, 0, -2.5f)))); x.trails.Add(main);
            var rear = Route(x, x.hub, new Vector2(0.7f, 10.5f));
            if (rear != null) x.trails.Add(rear);
            foreach (var item in x.resources.OrderBy(t => x.Local(t.transform.position).y).Take(10))
            {
                Vector2 tree = x.Local(item.transform.position);
                if (tree.magnitude > 13.5f || tree.magnitude < 3.4f) continue;
                Vector2 approach = tree - tree.normalized * 2f;
                Vector2 start = x.trails.SelectMany(line => line).OrderBy(p => (p - approach).sqrMagnitude).First();
                var branch = Route(x, start, approach);
                if (branch != null) x.trails.Add(branch);
            }
        }
    }
}
