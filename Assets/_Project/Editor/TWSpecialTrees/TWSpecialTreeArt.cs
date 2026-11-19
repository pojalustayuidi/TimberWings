using UnityEngine;

namespace IslandV2
{
    internal static class TWSpecialTreeArt
    {
        internal const float Height = 4.30f;
        internal const float TrunkRadius = .355f;
        internal const float TreeSpacing = 2.20f;

        static readonly Color DarkRuneBark = SoftMesh.C(0x484751);
        static readonly Color RoseBark = SoftMesh.C(0x67505B);
        static readonly Color GoldBark = SoftMesh.C(0xA56B3F);
        static readonly Color GoldCore = SoftMesh.C(0xE8B368);
        static readonly Color RuneCore = SoftMesh.C(0x98A79F);
        static readonly Color Mint = SoftMesh.C(0x83DFB6);
        static readonly Color Gold = SoftMesh.C(0xFFD288);
        static readonly Color[] RuneLeaves =
        {
            SoftMesh.C(0x676994), SoftMesh.C(0x8B80AC), SoftMesh.C(0x638F89), SoftMesh.C(0x82B6A8)
        };
        static readonly Color[] GoldLeaves =
        {
            SoftMesh.C(0xC38336), SoftMesh.C(0xD5A146), SoftMesh.C(0xEFC064), SoftMesh.C(0xB99C50)
        };

        static Vector3 Trunk(float y)
        {
            float t = Mathf.Clamp01(y / 3.6f);
            return new Vector3(.29f * t * t - .11f * Mathf.Sin(t * Mathf.PI), y, .12f * t);
        }

        internal static SoftMesh Bark(bool gold)
        {
            var mesh = new SoftMesh();
            Color dark = gold ? GoldBark : DarkRuneBark;
            Color light = gold ? SoftMesh.C(0xBE8A50) : RoseBark;
            mesh.Cyl(Trunk(-.04f), Trunk(.42f), .49f, .34f, dark, 10);
            mesh.Cyl(Trunk(.42f), Trunk(1.78f), .34f, .275f, dark, 10);
            mesh.Cyl(Trunk(1.78f), Trunk(2.74f), .275f, .19f, light, 9);
            mesh.Cyl(Trunk(2.74f), Trunk(3.82f), .19f, .065f, light, 9);

            for (int i = 0; i < 6; i++)
            {
                Vector3 direction = Heading(i * 60f + 17f);
                Vector3 hinge = direction * (.49f + .12f * (i % 2)) + Vector3.up * .095f;
                mesh.Cyl(direction * .15f + Vector3.up * .30f, hinge, .18f, .105f, dark, 7);
                mesh.Cyl(hinge, direction * (.78f + .17f * (i % 3)) - Vector3.up * .01f, .105f, .025f, light, 7);

                float height = 1.79f + .13f * (i % 4);
                Vector3 elbow = Trunk(height) + direction * (.54f + .07f * (i % 3)) + Vector3.up * .33f;
                Vector3 tip = BranchTip(i);
                mesh.Cyl(Trunk(height), elbow, .135f, .087f, dark, 7);
                mesh.Cyl(elbow, tip, .095f, .031f, light, 7);
                mesh.Cyl(elbow + Vector3.up * .08f, tip * .86f + Vector3.up * .72f, .065f, .026f, light, 6);
            }
            return mesh;
        }

        static Vector3 BranchTip(int i) =>
            Trunk(2.40f + .22f * (i % 4)) + Heading(i * 60f + 17f) * (1.03f + .20f * (i % 3));

        internal static SoftMesh Leaves(bool gold)
        {
            var mesh = new SoftMesh();
            Color[] tones = gold ? GoldLeaves : RuneLeaves;
            for (int i = 0; i < 6; i++)
            {
                Vector3 tip = BranchTip(i);
                mesh.Puff(tip + Vector3.up * .19f, new Vector3(.68f + i % 3 * .10f, .48f, .66f),
                    tones[i % tones.Length], 31f * i, 8, 4, .28f);
                mesh.Puff(tip * .79f + Vector3.up * .90f, new Vector3(.57f, .43f, .58f),
                    tones[(i + 2) % tones.Length], 25f * i, 7, 4, .24f);
            }
            mesh.Puff(Trunk(3.73f), new Vector3(.80f, .56f, .69f), tones[1], -23f, 8, 5, .26f);
            mesh.Puff(Trunk(3.30f) + new Vector3(-.28f, 0f, -.21f), new Vector3(.72f, .55f, .66f),
                tones[2], 20f, 7, 4, .28f);
            return mesh;
        }

        internal static SoftMesh Glow(bool gold)
        {
            var mesh = new SoftMesh();
            Color glow = gold ? Gold : Mint;
            for (int i = 0; i < 4; i++)
            {
                Vector3 side = Heading(i * 90f + 14f);
                Vector3 sideShift = Quaternion.Euler(0f, 14f, 0f) * side;
                Vector3 a = Trunk(.29f) + side * .40f;
                Vector3 b = Trunk(.84f + (i % 2) * .11f) + sideShift * .349f;
                Vector3 c = Trunk(1.30f + (i % 3) * .14f) + side * .333f;
                Vector3 d = Trunk(1.84f + i * .08f) + sideShift * .293f;
                mesh.Cyl(a, b, .020f, .015f, glow, 6);
                mesh.Cyl(b, c, .016f, .018f, glow, 6);
                mesh.Cyl(c, d, .018f, .004f, glow, 6);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 center = BranchTip(i) + new Vector3(.11f, -.085f, -.19f);
                mesh.Puff(center, new Vector3(.083f, .10f, .085f), glow, 13f * i, 6, 4);
            }

            Vector3 origin = Trunk(1.13f) + Vector3.back * .355f;
            if (!gold)
            {
                Vector3 top = origin + Vector3.up * .24f;
                Vector3 left = origin - Vector3.right * .126f;
                Vector3 right = origin + Vector3.right * .126f;
                Vector3 bottom = origin - Vector3.up * .20f;
                mesh.Cyl(left, top, .018f, .019f, glow, 6);
                mesh.Cyl(top, right, .019f, .018f, glow, 6);
                mesh.Cyl(right, bottom, .018f, .017f, glow, 6);
                mesh.Cyl(bottom, left, .017f, .018f, glow, 6);
                mesh.Cyl(origin + Vector3.up * .05f, bottom - Vector3.up * .13f, .016f, .018f, glow, 6);
            }
            else mesh.Star(origin, Vector3.right, Vector3.up, .165f, .1f, 5, glow);
            return mesh;
        }

        internal static SoftMesh Stump(bool gold)
        {
            var mesh = new SoftMesh();
            Color bark = gold ? GoldBark : DarkRuneBark;
            Color core = gold ? GoldCore : RuneCore;
            mesh.Cyl(new Vector3(0f, -.04f, 0f), new Vector3(.015f, .37f, 0f), .48f, .355f, bark, core, true, true, 10);
            for (int i = 0; i < 5; i++)
            {
                Vector3 side = Heading(i * 72f + 11f);
                mesh.Cyl(side * .16f + Vector3.up * .16f, side * (.69f + i % 2 * .11f), .155f, .021f, bark, 7);
            }
            for (int i = 0; i < 3; i++)
            {
                float radius = .254f - i * .07f;
                Vector3 center = new Vector3(.015f, .372f + i * .004f, 0f);
                mesh.Cyl(center, center + Vector3.up * .002f, radius, radius,
                    SoftMesh.Shade(core, i % 2 == 0 ? .67f : 1.16f), 10);
            }
            return mesh;
        }

        internal static SoftMesh Section(bool gold)
        {
            var mesh = new SoftMesh();
            Color bark = gold ? GoldBark : DarkRuneBark;
            Color wood = gold ? GoldCore : RuneCore;
            mesh.Cyl(Vector3.back * .498f, Vector3.forward * .498f, .92f, .82f, bark, wood, true, true, 12);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * .498f;
                for (int i = 0; i < 3; i++)
                {
                    float radius = .69f - i * .16f;
                    mesh.Cyl(new Vector3(0f, 0f, z + side * i * .0003f), new Vector3(0f, 0f, z + side * (i + 1) * .0003f),
                        radius, radius, SoftMesh.Shade(wood, i % 2 == 0 ? .62f : 1.22f), 12);
                }
            }
            return mesh;
        }

        internal static SoftMesh SectionGlow(bool gold)
        {
            var mesh = new SoftMesh();
            Color light = gold ? Gold : Mint;
            foreach (float x in new[] { -.48f, .37f })
            {
                mesh.Cyl(new Vector3(x, .813f, -.43f), new Vector3(x * .83f, .841f, -.11f), .045f, .031f, light, 6);
                mesh.Cyl(new Vector3(x * .83f, .841f, -.11f), new Vector3(x * 1.22f, .671f, .38f), .035f, .017f, light, 6);
            }
            return mesh;
        }

        internal static Vector3 Heading(float degrees) => Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
    }
}
