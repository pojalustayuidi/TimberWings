using System;
using UnityEngine;
using static IslandV2.TWSpecialTreeArt;

namespace IslandV2
{
    internal static class TWRuneSoilArt
    {
        internal const float HalfWidth = 3.72f;
        internal const float HalfLength = 4.34f;
        internal const float Corner = .82f;
        internal const float SkirtDepth = .92f;
        internal const float EntryHalfWidth = 1.05f;
        internal const float RampRun = 1.72f;
        const float EntryZ = -1.12f;
        const float BedRadius = 1.36f;

        static readonly Color Slate = SoftMesh.C(0x545568);
        static readonly Color Ash = SoftMesh.C(0x4C4558);
        static readonly Color Podzol = SoftMesh.C(0x383440);
        static readonly Color Stone = SoftMesh.C(0x858192);
        static readonly Color Basalt = SoftMesh.C(0x575563);
        static readonly Color Moss = SoftMesh.C(0x748462);
        static readonly Color Quartz = SoftMesh.C(0xA9A8C2);
        static readonly Color MushroomCap = SoftMesh.C(0x967DA6);
        static readonly Color Mint = SoftMesh.C(0x83DEBD);
        static readonly Color Violet = SoftMesh.C(0xB3A4DF);

        internal static Vector3 EntryLip(float side) => new Vector3(-side * HalfWidth, 0f, EntryZ);
        internal static Vector3 EntryFoot(float side) => EntryLip(side) - Vector3.right * (side * RampRun);
        internal static Vector3 TreeRoot(float side, int index) =>
            new Vector3(side * .42f, .012f, index == 0 ? -TreeSpacing : TreeSpacing);
        internal static Vector3 Beacon(float side) => new Vector3(side * (HalfWidth - .58f), 0f, .36f);

        internal static bool Contains(Vector3 at, float margin = 0f)
        {
            float x = Mathf.Abs(at.x);
            float z = Mathf.Abs(at.z);
            return x <= HalfWidth - margin && z <= HalfLength - margin &&
                x + z <= HalfWidth + HalfLength - Corner - margin * 1.42f;
        }

        internal static Vector3[] Outline() => new[]
        {
            new Vector3(-HalfWidth, 0f, -HalfLength + Corner),
            new Vector3(-HalfWidth + Corner, 0f, -HalfLength),
            new Vector3(HalfWidth - Corner, 0f, -HalfLength),
            new Vector3(HalfWidth, 0f, -HalfLength + Corner),
            new Vector3(HalfWidth, 0f, HalfLength - Corner),
            new Vector3(HalfWidth - Corner, 0f, HalfLength),
            new Vector3(-HalfWidth + Corner, 0f, HalfLength),
            new Vector3(-HalfWidth, 0f, HalfLength - Corner)
        };

        internal static SoftMesh Foundation()
            => Slab(Outline(), Slate);

        internal static SoftMesh Ramp(Vector3 toe, float side)
        {
            Vector3 lip = EntryLip(side);
            Vector3 edge = Vector3.forward * EntryHalfWidth;
            return Slab(new[] { lip - edge, toe - edge, toe + edge, lip + edge }, Stone);
        }

        static SoftMesh Slab(Vector3[] top, Color surface)
        {
            var mesh = new SoftMesh();
            var bottom = new Vector3[top.Length];
            Vector3 middle = Vector3.zero;
            for (int i = 0; i < top.Length; i++)
            {
                bottom[i] = new Vector3(top[i].x, -SkirtDepth, top[i].z);
                middle += top[i] / top.Length;
            }

            mesh.Fan(top, surface, Vector3.up, false);
            mesh.Fan(bottom, Basalt, Vector3.down, false);
            for (int i = 0; i < top.Length; i++)
            {
                int next = (i + 1) % top.Length;
                Vector3 outward = (top[i] + top[next]) * .5f - middle;
                outward.y = 0f;
                mesh.Quad(top[i], top[next], bottom[next], bottom[i],
                    SoftMesh.Shade(Basalt, i % 2 == 0 ? .92f : 1f), outward);
            }
            return mesh;
        }

        internal static SoftMesh Courtyard(float side)
        {
            var mesh = new SoftMesh();
            Vector3[] outline = Outline();
            Vector3 entry = EntryLip(side);
            for (int i = 0; i < outline.Length; i++)
            {
                Vector3 a = outline[i];
                Vector3 b = outline[(i + 1) % outline.Length];
                if (Mathf.Abs(a.x - entry.x) < .01f && Mathf.Abs(b.x - entry.x) < .01f)
                {
                    float sign = Mathf.Sign(b.z - a.z);
                    Curb(mesh, a, entry - Vector3.forward * (EntryHalfWidth * sign));
                    Curb(mesh, entry + Vector3.forward * (EntryHalfWidth * sign), b);
                }
                else Curb(mesh, a, b);
            }

            for (int i = 0; i < 2; i++)
            {
                Vector3 bed = TreeRoot(side, i);
                bed.y = .008f;
                Disk(mesh, bed, BedRadius + .07f, Ash, 18);
                Disk(mesh, bed + Vector3.up * .006f, BedRadius - .14f, Podzol, 18);
                Ring(mesh, bed + Vector3.up * .014f, BedRadius - .08f, BedRadius + .08f, Stone, 18);
                for (int stone = 0; stone < 9; stone++)
                {
                    Vector3 outward = Heading(stone * 40f + i * 20f);
                    Vector3 at = bed + outward * (BedRadius - .13f);
                    if (Mathf.Abs(at.z - bed.z) > 1.05f) continue;
                    mesh.Puff(at, new Vector3(.16f, .044f, .115f),
                        stone % 3 == 0 ? Moss : Basalt, stone * 26f, 6, 3, .13f);
                }
                Vector3 growth = bed + new Vector3(side * 1.04f, 0f, i == 0 ? -.38f : .32f);
                Mushroom(mesh, growth, .23f);
                Mushroom(mesh, growth + new Vector3(side * .16f, 0f, .12f), .16f);
                Mushroom(mesh, growth + new Vector3(-side * .09f, 0f, -.16f), .14f);
            }

            // A short fan of pavers leads from the ramp to the medallion between the beds.
            Vector3 step = entry + Vector3.right * (side * .48f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = Vector3.Lerp(step, new Vector3(-side * .78f, 0f, -.08f), i / 3f);
                FlatStone(mesh, at + Vector3.up * .021f, .32f + .04f * (i % 2), .59f, i * 19f,
                    SoftMesh.Shade(Stone, .72f + (i % 3) * .075f));
            }
            Disk(mesh, Centre, .73f, Basalt, 12);
            Ring(mesh, Centre + Vector3.up * .006f, .66f, .70f, Quartz, 12);

            foreach (Vector3 at in GatePosts(side))
            {
                Pedestal(mesh, at, .29f, .45f);
                mesh.Cyl(at + Vector3.up * .45f, at + Vector3.up * .52f, .30f, .25f, Stone, 8);
                mesh.Cyl(at + Vector3.up * .521f, at + Vector3.up * .56f, .172f, .15f, Podzol, 8);
            }

            Vector3 beacon = Beacon(side);
            Pedestal(mesh, beacon, .43f, .76f);
            mesh.Cyl(beacon + Vector3.up * .72f, beacon + Vector3.up * .82f, .39f, .30f, Quartz, 8);
            mesh.Cyl(beacon + Vector3.up * .823f, beacon + Vector3.up * .87f, .20f, .18f, Podzol, 8);
            return mesh;
        }

        static readonly Vector3 Centre = new Vector3(0f, .017f, 0f);

        internal static Vector3[] GatePosts(float side)
        {
            Vector3 inner = EntryLip(side) + Vector3.right * (side * .24f);
            Vector3 flank = Vector3.forward * (EntryHalfWidth + .30f);
            return new[] { inner - flank, inner + flank };
        }

        static void Pedestal(SoftMesh mesh, Vector3 at, float width, float height)
        {
            mesh.Cyl(at, at + Vector3.up * .12f, width, width * .90f, Stone, 7);
            mesh.Cyl(at + Vector3.up * .12f, at + Vector3.up * height,
                width * .68f, width * .76f, Basalt, 7);
        }

        static void Mushroom(SoftMesh mesh, Vector3 at, float height)
        {
            Vector3 cap = at + Vector3.up * height;
            mesh.Cyl(at, cap, height * .15f, height * .12f, Stone, 6);
            mesh.Puff(cap, new Vector3(height * .61f, height * .29f, height * .56f), MushroomCap,
                27f, 7, 4, .20f);
        }

        static void Curb(SoftMesh mesh, Vector3 start, Vector3 end)
        {
            Vector3 axis = end - start;
            if (axis.sqrMagnitude < .02f) return;
            Vector3 midpoint = (start + end) * .5f;
            Vector3 inward = Vector3.Cross(Vector3.up, axis).normalized;
            if (Vector3.Dot(inward, midpoint) > 0f) inward = -inward;
            int count = Mathf.Max(1, Mathf.CeilToInt(axis.magnitude / .77f));
            Quaternion lay = Quaternion.LookRotation(axis, Vector3.up);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = Vector3.Lerp(start, end, (i + .5f) / count);
                at += inward * .145f;
                Color stone = SoftMesh.Shade(Stone, .87f + i % 5 * .034f);
                mesh.Box(at + Vector3.up * .026f, new Vector3(.228f, .16f, axis.magnitude / count - .023f), lay, stone);
                if (i % 4 != 2) continue;
                mesh.Puff(at + inward * .01f + Vector3.up * .104f, new Vector3(.13f, .025f, .11f),
                    Moss, 20f + i * 41f, 6, 3, .10f);
            }
        }

        internal static SoftMesh Trail(Func<Vector3, float, Vector3> snap, Vector3[] route)
        {
            var mesh = new SoftMesh();
            for (int i = 0; i < route.Length; i++)
            {
                Vector3 on = snap(route[i], .026f);
                mesh.Puff(on, new Vector3(.29f + .024f * (i % 3), .041f, .23f + .036f * (i % 2)),
                    SoftMesh.Shade(Stone, .89f + (i % 3) * .059f), i * 31f, 7, 4, .10f);
            }
            return mesh;
        }

        internal static SoftMesh Veins(float side)
        {
            var mesh = new SoftMesh();
            Vector3 hub = Centre + Vector3.up * .019f;
            Diamond(mesh, hub, .41f, Mint);
            Stroke(mesh, hub + Vector3.back * .51f, hub + Vector3.forward * .51f, .015f, Mint);
            for (int index = 0; index < 2; index++)
            {
                Color glow = index == 0 ? Mint : Violet;
                Vector3 bed = TreeRoot(side, index);
                bed.y = .032f;
                for (int i = 0; i < 6; i++)
                {
                    float azimuth = i * 60f + index * 19f;
                    Vector3 axis = Heading(azimuth);
                    Vector3 skew = Heading(azimuth + (i % 2 == 0 ? 18f : -18f));
                    Stroke(mesh, bed + axis * .59f, bed + skew * .89f, .014f, glow);
                    Stroke(mesh, bed + skew * .89f, bed + axis * 1.08f, .012f, glow);
                    if (i % 2 == 0)
                    {
                        Vector3 glyph = bed + axis * 1.25f;
                        Diamond(mesh, glyph, .061f, glow);
                    }
                }
            }
            foreach (Vector3 post in GatePosts(side))
                Crystal(mesh, post + Vector3.up * .57f, .15f, .36f, Mint);
            Crystal(mesh, Beacon(side) + Vector3.up * .91f, .22f, .58f, Violet);
            return mesh;
        }

        static void Crystal(SoftMesh mesh, Vector3 at, float width, float height, Color tint)
        {
            Vector3 waist = at + Vector3.up * (height * .36f);
            mesh.Cyl(at, waist, .034f, width, SoftMesh.Shade(tint, .79f), 6);
            mesh.Cyl(waist, at + Vector3.up * height, width, .006f, tint, 6);
        }

        static void Diamond(SoftMesh mesh, Vector3 at, float half, Color tint)
        {
            Vector3 north = at + Vector3.forward * half;
            Vector3 south = at + Vector3.back * half;
            Vector3 east = at + Vector3.right * (half * .65f);
            Vector3 west = at + Vector3.left * (half * .65f);
            float width = Mathf.Min(.018f, half * .17f);
            Stroke(mesh, north, east, width, tint);
            Stroke(mesh, east, south, width, tint);
            Stroke(mesh, south, west, width, tint);
            Stroke(mesh, west, north, width, tint);
        }

        static void Stroke(SoftMesh mesh, Vector3 a, Vector3 b, float width, Color color)
        {
            Vector3 cross = Vector3.Cross(Vector3.up, b - a).normalized * width;
            mesh.Quad(a - cross, a + cross, b + cross, b - cross, color, Vector3.up);
        }

        static void Disk(SoftMesh mesh, Vector3 center, float radius, Color tint, int sides)
        {
            var outline = new Vector3[sides];
            for (int i = 0; i < sides; i++) outline[i] = center + Heading(i * 360f / sides) * radius;
            mesh.Fan(outline, tint, Vector3.up, false);
        }

        static void Ring(SoftMesh mesh, Vector3 center, float inner, float outer, Color tint, int sides)
        {
            float arc = 360f / sides;
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Heading(i * arc + 1.6f);
                Vector3 b = Heading((i + 1) * arc - 1.6f);
                mesh.Quad(center + a * inner, center + b * inner, center + b * outer, center + a * outer,
                    SoftMesh.Shade(tint, .85f + i % 4 * .055f), Vector3.up);
            }
        }

        static void FlatStone(SoftMesh mesh, Vector3 at, float halfX, float halfZ, float angle, Color tint)
        {
            var outline = new Vector3[7];
            for (int i = 0; i < outline.Length; i++)
            {
                Vector3 direction = Heading(i * 360f / outline.Length + angle);
                outline[i] = at + new Vector3(direction.x * halfX, 0f, direction.z * halfZ);
            }
            mesh.Fan(outline, tint, Vector3.up, false);
        }
    }
}
