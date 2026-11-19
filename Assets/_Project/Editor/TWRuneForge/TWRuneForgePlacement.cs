using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IslandV2.TWRuneForgeModel;

namespace IslandV2
{
    internal static class TWRuneForgePlacement
    {
        const float RoofHalfWidth = 3.60f;
        static readonly float[] Distances = { 11.3f, 13.2f, 15.2f, 17.0f };
        static readonly int[] Bearings = { 0, -35, 35, -70, 70, -105, 105, -140, 140, 180 };

        internal static Pose Choose(Scene scene, Transform yard, RuneForge existing, Transform marker)
        {
            var props = yard.GetComponentsInChildren<WorkyardProp>(true);
            Physics.SyncTransforms();
            if (marker != null)
            {
                Vector3 direction = Vector3.ProjectOnPlane(marker.position - yard.position, Vector3.up);
                Quaternion rotation = direction.sqrMagnitude > 1f
                    ? Quaternion.LookRotation(direction)
                    : Quaternion.Euler(0f, marker.eulerAngles.y, 0f);
                if (TrySite(scene, marker.position, rotation, existing, props, out var selected)) return selected;
                throw new InvalidOperationException("В выбранной точке мало места, неровный подход или есть препятствия. Нужна площадка примерно 8 × 10 м.");
            }

            foreach (float radius in Distances)
            foreach (int bearing in Bearings)
            {
                Vector3 direction = Quaternion.Euler(0f, bearing, 0f) * Vector3.right;
                Vector3 center = yard.position + direction * radius;
                if (TrySite(scene, center, Quaternion.LookRotation(direction), existing, props, out var pose)) return pose;
            }
            throw new InvalidOperationException("У рабочего двора нет свободной площадки. Выбери ровную точку и запусти второй пункт меню.");
        }

        static bool TrySite(Scene scene, Vector3 position, Quaternion rotation, RuneForge existing, WorkyardProp[] props,
            out Pose pose)
        {
            pose = default;
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;

            foreach (float x in new[] { -RoofHalfWidth, 0f, RoofHalfWidth })
            foreach (float z in new[] { DeckFront, 0f, Back + .4f })
            {
                Vector3 sample = position + rotation * new Vector3(x, 0f, z);
                if (!TryGround(scene, sample, out float height)) return false;
                min = Mathf.Min(min, height);
                max = Mathf.Max(max, height);
            }
            if (max - min > .62f) return false;
            position.y = max - .04f;

            foreach (float x in new[] { -1.12f, 0f, 1.12f })
            {
                Vector3 toe = position + rotation * new Vector3(x, 0f, RampEnd);
                if (!TryGround(scene, toe, out float ground)) return false;
                if (position.y + RampLow - ground > .13f || ground - position.y > Floor - .07f) return false;
                Vector3 approach = position + rotation * new Vector3(x, 0f, RampEnd - .80f);
                if (!TryGround(scene, approach, out float approachY) || Mathf.Abs(approachY - ground) > .38f) return false;
            }

            // Old and new workyard stages may be inactive in Edit Mode. Keep their props clear too.
            Quaternion inverse = Quaternion.Inverse(rotation);
            foreach (var prop in props)
            {
                Vector3 local = inverse * (prop.transform.position - position);
                if (Mathf.Abs(local.x) < RoofHalfWidth + .55f && local.z > -4.3f && local.z < Back + .8f) return false;
            }

            Vector3 buildingCenter = position + rotation * new Vector3(0f, 2.23f, -.27f);
            if (Blocked(scene, buildingCenter, new Vector3(RoofHalfWidth, 2.19f, 3.57f), rotation, existing)) return false;
            Vector3 approachCenter = position + rotation * new Vector3(0f, .82f, -4.44f);
            if (Blocked(scene, approachCenter, new Vector3(1.46f, .94f, 1.18f), rotation, existing)) return false;
            pose = new Pose(position, rotation);
            return true;
        }

        static bool Blocked(Scene scene, Vector3 center, Vector3 half, Quaternion rotation, RuneForge existing)
            => Physics.OverlapBox(center, half, rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                .Any(collider => collider.gameObject.scene == scene && !IsGround(collider)
                    && (existing == null || !collider.transform.IsChildOf(existing.transform)));

        static bool TryGround(Scene scene, Vector3 point, out float height)
        {
            height = float.NegativeInfinity;
            var hits = Physics.RaycastAll(point + Vector3.up * 8f, Vector3.down, 18f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
                if (hit.collider.gameObject.scene == scene && IsGround(hit.collider) && hit.normal.y >= .72f)
                    height = Mathf.Max(height, hit.point.y);
            return !float.IsNegativeInfinity(height);
        }

        static bool IsGround(Collider collider)
            => collider is TerrainCollider || collider.name == "TW_WORKYARD_PHYSICS"
                || collider.name.StartsWith(IslandBlockout.ChunkPrefix, StringComparison.Ordinal);
    }
}
