using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IslandV2.TWSpecialTreeArt;
using static IslandV2.TWRuneSoilArt;

namespace IslandV2
{
    internal sealed class TWSpecialTreePlacement
    {
        internal readonly struct GroveSite
        {
            internal readonly Pose Pose;
            internal readonly float Side;
            internal readonly Vector3 Toe;

            internal GroveSite(Pose pose, float side, Vector3 toe)
            {
                Pose = pose;
                Side = side;
                Toe = toe;
            }
        }

        readonly Scene scene;
        readonly Transform[] ignored;
        const float TerraceLift = .13f;
        const float MaxGroundSpan = .72f;
        const float MaxRampRise = .65f;
        static readonly float[] AcrossRamp = { -EntryHalfWidth, 0f, EntryHalfWidth };
        static readonly float[] GroveSides = { 1f, -1f };
        static readonly float[] GroveDepths = { -.3f, 1.6f, -2.2f, 3.35f };
        static readonly float[] GroveOffsets = { 8.80f, 9.65f, 10.5f, 11.65f, 12.5f };
        static readonly float[] GoldenDistances = { 0f, 4.4f, 7.8f, 11.4f, 15.3f, 20f };

        internal TWSpecialTreePlacement(Scene scene, params Transform[] ignored)
        {
            this.scene = scene;
            this.ignored = ignored;
            Physics.SyncTransforms();
        }

        internal GroveSite Grove(RuneForge forge, Transform chosen, Transform existing)
        {
            Quaternion rotation = Quaternion.Euler(0f, forge.transform.eulerAngles.y, 0f);
            if (chosen != null)
            {
                if (!AtSide(forge, chosen.position))
                    throw new InvalidOperationException("Выбранная точка должна быть сбоку от кузницы, в 8–13 м от её центра.");
                if (TryGrove(forge, chosen.position, rotation, out var placed)) return placed;
                throw new InvalidOperationException("В выбранной точке не помещается площадка с пологим входом и свободным подходом.");
            }
            if (existing != null && AtSide(forge, existing.position)
                && TryGrove(forge, existing.position, rotation, out var previous)) return previous;

            foreach (float offset in GroveOffsets)
            foreach (float z in GroveDepths)
            foreach (float side in GroveSides)
            {
                Vector3 position = forge.transform.TransformPoint(new Vector3(side * offset, 0f, z));
                if (TryGrove(forge, position, rotation, out var placed)) return placed;
            }
            throw new InvalidOperationException("Рядом с этой кузницей не нашлось свободной площадки. Выбери ровную точку сбоку или передвинь кузницу и повтори запекание.");
        }

        static bool AtSide(RuneForge forge, Vector3 position)
        {
            Vector3 local = forge.transform.InverseTransformPoint(position);
            float distance = Mathf.Abs(local.x);
            return distance >= 8.15f && distance <= 13f && Mathf.Abs(local.z) <= 5f;
        }

        bool TryGrove(RuneForge forge, Vector3 at, Quaternion rotation, out GroveSite site)
        {
            site = default;
            var initial = new Pose(at, rotation);
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            bool Sample(Vector3 local)
            {
                if (!TryGround(World(initial, local), out float height)) return false;
                bottom = Mathf.Min(bottom, height);
                top = Mathf.Max(top, height);
                return top - bottom <= MaxGroundSpan;
            }

            var border = Outline();
            for (int i = 0; i < border.Length; i++)
                if (!Sample(border[i]) || !Sample((border[i] + border[(i + 1) % border.Length]) * .5f)) return false;
            for (int x = -3; x <= 3; x++)
            for (int z = -4; z <= 4; z++)
            {
                var local = new Vector3(x, 0f, z);
                if (Contains(local) && !Sample(local)) return false;
            }

            at.y = top + TerraceLift;
            var pose = new Pose(at, rotation);
            if (Blocked(at + Vector3.up * 1.85f, new Vector3(HalfWidth, 1.90f, HalfLength), rotation)) return false;

            float side = Mathf.Sign(forge.transform.InverseTransformPoint(at).x);
            if (!TryRamp(pose, side, out Vector3 toe)) return false;

            var candidate = new GroveSite(pose, side, toe);
            Vector3[] walk = PathTo(forge, candidate);
            float previousHeight = float.NaN;
            for (int i = 0; i < walk.Length; i++)
            {
                if (!TryGround(walk[i], out float ground)) return false;
                if (i > 0 && Mathf.Abs(ground - previousHeight) > .35f) return false;
                Vector3 capsule = walk[i];
                capsule.y = ground + .84f;
                if (Blocked(capsule, new Vector3(.39f, .54f, .39f), Quaternion.identity)) return false;
                previousHeight = ground;
            }
            if (Mathf.Abs(previousHeight - World(pose, toe).y) > .22f) return false;

            site = candidate;
            return true;
        }

        bool TryRamp(Pose pose, float side, out Vector3 toe)
        {
            Vector3 lip = EntryLip(side);
            toe = EntryFoot(side);
            float near = float.PositiveInfinity;
            float far = float.NegativeInfinity;
            foreach (float width in AcrossRamp)
            {
                if (!TryGround(World(pose, toe + Vector3.forward * width), out float ground)) return false;
                near = Mathf.Min(near, ground);
                far = Mathf.Max(far, ground);
            }
            toe.y = far - pose.position.y + .025f;
            if (far - near > .15f || Mathf.Abs(toe.y) > MaxRampRise) return false;

            for (int i = 1; i <= 5; i++)
            {
                Vector3 top = Vector3.Lerp(toe, lip, i / 5f);
                foreach (float width in AcrossRamp)
                {
                    Vector3 at = World(pose, top + Vector3.forward * width);
                    if (!TryGround(at, out float ground) || ground > at.y - .015f) return false;
                }
            }
            Vector3 middle = World(pose, (lip + toe) * .5f);
            return !Blocked(middle + Vector3.up * .87f, new Vector3(RampRun * .5f, .75f, EntryHalfWidth), pose.rotation);
        }

        internal Vector3 Golden(Transform anchor, Vector3 fallback, Vector3? previous)
        {
            Vector3 center = anchor != null ? anchor.position : fallback;
            foreach (float radius in GoldenDistances)
            {
                int divisions = radius > 0f ? 12 : 1;
                for (int i = 0; i < divisions; i++)
                {
                    Vector3 position = center + Heading(i * 30f + 15f) * radius;
                    if (previous.HasValue && Flat(previous.Value - position).sqrMagnitude < 12f * 12f) continue;
                    if (!TryGround(position, out position.y)) continue;
                    if (Blocked(position + Vector3.up * 2f, new Vector3(1.65f, 1.72f, 1.65f), Quaternion.identity)) continue;
                    if (FallLane(position)) return position;
                }
            }
            throw new InvalidOperationException("Нет свободного места для золотого дерева около " + (anchor != null ? anchor.name : center.ToString()) + ".");
        }

        bool FallLane(Vector3 root)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector3 vector = Heading(i * 30f);
                Vector3 side = Vector3.Cross(Vector3.up, vector) * .37f;
                bool clear = true;
                for (int step = 1; step <= 7; step++)
                {
                    Vector3 p = root + vector * (.65f * step);
                    if (!TryGround(p, out float height) || Mathf.Abs(height - root.y) > .62f
                        || !TryGround(p + side, out float left) || !TryGround(p - side, out float right)
                        || Mathf.Abs(left - right) > .29f)
                    {
                        clear = false;
                        break;
                    }
                    p.y = height + .84f;
                    float radius = step < 4 ? .44f : .75f;
                    if (Blocked(p, new Vector3(radius, .68f, radius), Quaternion.identity))
                    {
                        clear = false;
                        break;
                    }
                }
                if (clear) return true;
            }
            return false;
        }

        internal static Vector3[] PathTo(RuneForge forge, GroveSite site)
        {
            float side = site.Side;
            Vector3 start = forge.transform.TransformPoint(new Vector3(side * .68f, 0f, -5.10f));
            // The second leg approaches the foot from the forge side and stays outside the ramp.
            Vector3 foot = World(site.Pose, site.Toe);
            Vector3 end = foot - site.Pose.rotation * Vector3.forward * (EntryHalfWidth + .25f);
            float turnX = Mathf.Max(3.65f, Mathf.Abs(forge.transform.InverseTransformPoint(end).x));
            Vector3 turn = forge.transform.TransformPoint(new Vector3(side * turnX, 0f, -4.92f));
            var steps = new Vector3[13];
            for (int i = 0; i < 6; i++) steps[i] = Vector3.Lerp(start, turn, i / 6f);
            for (int i = 6; i < steps.Length; i++) steps[i] = Vector3.Lerp(turn, end, (i - 6) / 6f);
            return steps;
        }

        bool Blocked(Vector3 center, Vector3 half, Quaternion rotation)
            => Physics.OverlapBox(center, half, rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                .Any(c => c.gameObject.scene == scene && !Ground(c)
                    && !ignored.Any(t => t != null && c.transform.IsChildOf(t)));

        internal bool TryGround(Vector3 point, out float height)
        {
            height = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * 8f, Vector3.down, 18f,
                         Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene == scene && Ground(hit.collider) && hit.normal.y >= .67f)
                    height = Mathf.Max(height, hit.point.y);
            }
            return !float.IsNegativeInfinity(height);
        }

        internal Vector3 Snap(Pose pose, Vector3 local, float inset = 0f)
        {
            Vector3 p = World(pose, local);
            if (!TryGround(p, out float y))
                throw new InvalidOperationException("Нет опоры под рунной площадкой в точке " + p + ".");
            p.y = y + inset;
            return Quaternion.Inverse(pose.rotation) * (p - pose.position);
        }

        static bool Ground(Collider collider)
            => collider is TerrainCollider || collider.tag == "Ground" || collider.name == "Ground"
                || collider.name == "TW_WORKYARD_PHYSICS"
                || collider.name.StartsWith("Ground_Chunk_", StringComparison.Ordinal);

        internal static Vector3 World(Pose pose, Vector3 local) => pose.position + pose.rotation * local;
        static Vector3 Flat(Vector3 point) => Vector3.ProjectOnPlane(point, Vector3.up);
    }
}
