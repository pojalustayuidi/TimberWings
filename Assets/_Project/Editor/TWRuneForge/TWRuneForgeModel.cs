using UnityEngine;

namespace IslandV2
{
    internal static class TWRuneForgeModel
    {
        internal enum Phase { Ruined, Frame, Restored }

        internal const float HalfWidth = 3.12f;
        internal const float Front = -2.72f;
        internal const float DeckFront = -3.34f;
        internal const float Back = 2.82f;
        internal const float Floor = .24f;
        internal const float RampEnd = -4.68f;
        internal const float RampLow = -.34f;
        internal const float RampHalfWidth = 1.35f;
        internal const float Wall = 2.98f;
        internal const float DoorHalfWidth = 1.24f;
        internal const float SideSillTop = 1.31f;
        internal static readonly Vector3 Fire = new Vector3(-1.48f, 1.03f, 1.22f);
        internal static readonly Vector3 Chimney = new Vector3(-1.65f, 5.06f, 1.95f);
        internal static readonly Vector3 Rune = new Vector3(1.83f, 1.267f, .09f);
        internal static readonly Vector3 Lantern = new Vector3(-2.17f, 2.16f, -3.15f);
        static readonly Vector3 Seal = new Vector3(-.08f, 3.34f, -3.35f);
        internal static readonly Vector3 Anvil = new Vector3(-1.88f, .24f, -.98f);
        internal static readonly Vector3 Workbench = new Vector3(1.83f, .24f, .07f);
        internal static readonly Vector3 FallenRoofTop = new Vector3(-2.98f, 2.36f, -1.39f);
        internal static readonly Vector3 FallenRoofBottom = new Vector3(-1.88f, .52f, -1.39f);

        static readonly Color Timber = SoftMesh.C(0xB88A60);
        static readonly Color EndGrain = SoftMesh.C(0xDDB17F);
        static readonly Color Bark = SoftMesh.C(0x654936);
        static readonly Color AgedWood = SoftMesh.C(0x8C7558);
        static readonly Color RoofSlate = SoftMesh.C(0x657D74);
        static readonly Color DarkSlate = SoftMesh.C(0x435B56);
        static readonly Color Moss = SoftMesh.C(0x869966);
        static readonly Color Metal = SoftMesh.C(0x48505C);
        static readonly Color Pewter = SoftMesh.C(0x79868A);
        static readonly Color Brass = SoftMesh.C(0xC69859);
        static readonly Color Stone = SoftMesh.C(0xA59A8D);
        static readonly Color DarkStone = SoftMesh.C(0x716C69);
        static readonly Color Ash = SoftMesh.C(0x352E2B);
        static readonly Color Verdigris = SoftMesh.C(0x538984);
        static readonly Color Linen = SoftMesh.C(0xE6CAA0);

        internal static SoftMesh Base()
        {
            var mesh = new SoftMesh();
            float depth = Back - DeckFront;
            float middle = (Back + DeckFront) * .5f;
            Box(mesh, new Vector3(0f, -.26f, middle), new Vector3(HalfWidth * 2f - .12f, .9f, depth - .12f), DarkStone);
            int planks = 15;
            for (int i = 0; i < planks; i++)
            {
                float z = DeckFront + (i + .5f) * depth / planks;
                Box(mesh, new Vector3(0f, Floor - .065f, z),
                    new Vector3(HalfWidth * 2f, .13f, depth / planks - .022f),
                    SoftMesh.Shade(Timber, .82f + i % 5 * .072f));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 9; i++)
                {
                    float z = DeckFront + (i + .5f) * depth / 9f;
                    Box(mesh, new Vector3(side * 3.04f, .012f, z),
                        new Vector3(.20f, .27f, depth / 9f - .035f), SoftMesh.Shade(Stone, .84f + .052f * (i % 5)));
                }
                for (int i = 0; i < 3; i++)
                {
                    float x = side * (1.62f + i * .57f);
                    Box(mesh, new Vector3(x, -.045f, DeckFront + .035f), new Vector3(.54f, .28f, .29f),
                        SoftMesh.Shade(Stone, .84f + .058f * i));
                }
                Boulder(mesh, new Vector3(side * 2.93f, .11f, DeckFront + .40f),
                    new Vector3(.23f, .12f, .31f), Moss, side * 13f);
            }

            Vector3 foot = new Vector3(0f, RampLow, RampEnd);
            Vector3 landing = new Vector3(0f, Floor, DeckFront);
            float rampLength = Vector3.Distance(foot, landing);
            Quaternion pitch = Quaternion.LookRotation(landing - foot, Vector3.up);
            for (int i = 0; i < 7; i++)
            {
                Vector3 top = Vector3.Lerp(foot, landing, (i + .5f) / 7f);
                mesh.Box(top - pitch * Vector3.up * .08f, new Vector3(RampHalfWidth * 2f, .16f, rampLength / 7f - .012f),
                    pitch, i % 2 == 0 ? Timber : EndGrain);
            }
            return mesh;
        }

        internal static SoftMesh RepairStand()
        {
            var mesh = new SoftMesh();
            Beam(mesh, new Vector3(1.66f, -.50f, .19f), new Vector3(1.66f, 1.30f, .19f), .18f, Bark);
            Box(mesh, new Vector3(1.66f, .97f, .12f), new Vector3(.61f, .38f, .16f), EndGrain);
            Box(mesh, new Vector3(1.66f, .97f, .027f), new Vector3(.44f, .25f, .036f), Linen);
            for (int i = 0; i < 3; i++)
                Box(mesh, new Vector3(1.63f, 1.045f - i * .067f, .004f),
                    new Vector3(.27f - i * .04f, .014f, .012f), AgedWood);
            return mesh;
        }

        internal static SoftMesh Architecture(Phase phase)
        {
            bool ready = phase == Phase.Restored;
            bool ruined = phase == Phase.Ruined;
            var mesh = new SoftMesh();
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * Wall;
                for (int column = 0; column < 3; column++)
                {
                    float z = Mathf.Lerp(Front, Back - .11f, column / 2f);
                    float top = 2.95f;
                    if (ruined)
                        top = side < 0 && column < 2 ? 2.36f : 1.47f + column * .16f;
                    Beam(mesh, new Vector3(x, Floor, z), new Vector3(x, top, z), .25f, ruined ? AgedWood : Bark);
                    Boulder(mesh, new Vector3(x, .32f, z), new Vector3(.25f, .20f, .235f), DarkStone, column * 20f);
                }
                if (ready) SideApron(mesh, x);
                else for (int i = 0; i < 5; i++)
                {
                    float bottomY = ruined ? 1.02f : 1.27f;
                    float z = -.07f + (i - 2) * 1.015f;
                    Box(mesh, new Vector3(x, (Floor + bottomY) * .5f, z),
                        new Vector3(.21f, bottomY - Floor + (ruined ? (i % 3) * .12f : 0f), 1.03f),
                        SoftMesh.Shade(ruined ? AgedWood : Timber, .78f + i % 3 * .13f));
                }
                if (!ruined)
                {
                    if (!ready)
                        Beam(mesh, new Vector3(x, 1.30f, Front), new Vector3(x, 1.30f, Back), .17f, EndGrain);
                    Beam(mesh, new Vector3(x, 2.76f, Front), new Vector3(x, 2.76f, Back), .24f, Bark);
                    foreach (float z in new[] { Front, .03f, Back - .11f })
                    {
                        if (z > Front) Beam(mesh, new Vector3(x, 2.11f, z), new Vector3(x, 2.69f, z - .63f), .15f, Timber);
                        if (z < Back - .2f) Beam(mesh, new Vector3(x, 2.11f, z), new Vector3(x, 2.69f, z + .63f), .15f, Timber);
                    }
                }

                float wing = side * (Wall + DoorHalfWidth) * .5f;
                if (ready)
                    for (int i = 0; i < 4; i++)
                        Box(mesh, new Vector3(wing + (i - 1.5f) * .435f, Floor + .217f, Front + .035f),
                            new Vector3(.414f, .434f, .23f), SoftMesh.Shade(Stone, .85f + i % 3 * .088f));
                for (int i = 0; i < 8; i++)
                {
                    float px = wing + (i - 3.5f) * .22f;
                    float height = ready ? 1.96f : .74f + (i % 3) * .18f;
                    float foot = ready ? Floor + .43f : Floor;
                    Box(mesh, new Vector3(px, foot + height * .5f, Front + .035f),
                        new Vector3(.215f, height, .15f), SoftMesh.Shade(ruined ? AgedWood : Timber, .76f + (i % 4) * .1f));
                }
                if (!ruined)
                {
                    float jamb = side * (DoorHalfWidth + .1f);
                    Beam(mesh, new Vector3(jamb, Floor, Front), new Vector3(jamb, 2.78f, Front), .20f, Bark);
                    Beam(mesh, new Vector3(jamb, 2.16f, Front), new Vector3(side * .67f, 2.76f, Front), .16f, Timber);
                }
            }

            BackWall(mesh, ruined, ready);
            Roof(mesh, phase);
            if (ready) Porch(mesh);
            else Barrier(mesh, ruined);
            return mesh;
        }

        static void SideApron(SoftMesh mesh, float x)
        {
            float span = Back - Front - .14f;
            float start = Front + .07f;
            for (int i = 0; i < 11; i++)
            {
                float z = start + (i + .5f) * span / 11f;
                Box(mesh, new Vector3(x, Floor + .23f, z), new Vector3(.245f, .46f, span / 11f - .025f),
                    SoftMesh.Shade(i % 4 == 0 ? DarkStone : Stone, .92f + .05f * (i % 4)));
            }
            for (int row = 0; row < 3; row++)
            for (int segment = 0; segment < 2; segment++)
            {
                float z = start + (.25f + .5f * segment) * span;
                Box(mesh, new Vector3(x, .755f + .16f * row, z), new Vector3(.19f, .151f, span * .5f - .039f),
                    SoftMesh.Shade(Timber, .86f + .10f * ((row + segment) % 3)));
            }
            Box(mesh, new Vector3(x, SideSillTop - .065f, (Back + Front) * .5f),
                new Vector3(.33f, .13f, span), EndGrain);
        }

        static void BackWall(SoftMesh mesh, bool ruined, bool ready)
        {
            for (int i = 0; i < 13; i++)
            {
                float height = ready ? 2.63f : (ruined ? 1.41f : 2.05f) - i % 3 * .20f;
                Box(mesh, new Vector3(-2.86f + i * .476f, Floor + height * .5f, Back - .10f),
                    new Vector3(.46f, height, .20f), SoftMesh.Shade(ruined ? AgedWood : Timber, .73f + i % 4 * .105f));
            }
        }

        static void Roof(SoftMesh mesh, Phase phase)
        {
            if (phase == Phase.Ruined)
            {
                CrumpledRoof(mesh);
                return;
            }

            foreach (int side in new[] { -1, 1 })
            {
                Vector3 eave = Eave(side);
                Vector3 ridge = Ridge;
                Quaternion pitch = RoofRotation(side);
                for (int truss = 0; truss < 4; truss++)
                {
                    Vector3 z = Vector3.forward * (-2.92f + truss * 1.91f);
                    Beam(mesh, eave + z, ridge + z, .17f, Bark);
                }
                float span = Vector3.Distance(eave, ridge);
                for (int row = 0; row < 7; row++)
                {
                    for (int tile = 0; tile < 12; tile++)
                    {
                        if (phase == Phase.Frame && tile < (side < 0 ? 6 : 9)) continue;
                        float z = -3.08f + .55f * tile + (row % 2 == 1 ? .11f : -.07f);
                        Vector3 center = Vector3.Lerp(eave, ridge, (row + .45f) / 7f) + new Vector3(0f, .095f, z);
                        Color slate = (tile >= 7 && (tile + row * 5) % 7 == 0) ? Moss : RoofSlate;
                        Color tint = SoftMesh.Shade(slate, .85f + ((tile * 17 + row * 7 + side) % 5 + 1) * .056f);
                        mesh.Box(center, new Vector3(span / 7f + .10f, .11f, .61f), pitch, tint);
                    }
                }
                if (phase == Phase.Restored)
                {
                    mesh.Box((eave + ridge) * .5f + new Vector3(0f, -.035f, 0f),
                        new Vector3(span + .02f, .14f, 6.44f), pitch, DarkSlate);
                    Beam(mesh, eave + Vector3.back * 3.33f, eave + Vector3.forward * 3.31f, .19f, Bark);
                    foreach (float edge in new[] { -3.29f, 3.27f })
                    {
                        Vector3 offset = Vector3.forward * edge;
                        Beam(mesh, eave + offset, ridge + offset, .18f, Bark);
                        Beam(mesh, eave + offset + Vector3.up * .087f, ridge + offset + Vector3.up * .087f, .064f, EndGrain);
                    }
                }
            }
            if (phase == Phase.Restored)
            {
                Beam(mesh, Ridge + Vector3.back * 3.39f, Ridge + Vector3.forward * 3.34f, .20f, DarkSlate);
                for (int i = 0; i < 10; i++)
                {
                    Vector3 at = Ridge + new Vector3(0f, .102f, -3.03f + i * .671f);
                    mesh.Cyl(at - Vector3.forward * .31f, at + Vector3.forward * .31f,
                        .145f, .145f, i == 0 || i == 9 ? Verdigris : RoofSlate, 8);
                }
            }
        }

        static void CrumpledRoof(SoftMesh mesh)
        {
            Beam(mesh, FallenRoofTop + Vector3.back * 1.33f, FallenRoofTop + Vector3.forward * 1.43f, .20f, Bark);
            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = Vector3.forward * ((i - 1) * 1.03f);
                Beam(mesh, FallenRoofTop + offset, FallenRoofBottom + offset, .17f, AgedWood);
                Beam(mesh, FallenRoofBottom + offset, FallenRoofBottom + offset - Vector3.up * .31f, .14f, Bark);
            }
            float span = Vector3.Distance(FallenRoofTop, FallenRoofBottom);
            for (int i = 0; i < 5; i++)
            for (int j = 0; j < 4; j++)
            {
                if ((i == 4 && j < 2) || (i == 0 && j == 3)) continue;
                Vector3 center = Vector3.Lerp(FallenRoofTop, FallenRoofBottom, (i + .5f) / 5f);
                center += new Vector3(.043f, .055f, (j - 1.5f) * .65f);
                mesh.Box(center, new Vector3(span / 5f + .055f, .105f, .71f), FallenRoofRotation,
                    SoftMesh.Shade((i + j) % 4 == 0 ? Moss : DarkSlate, .86f + j * .074f));
            }
        }

        internal static Quaternion FallenRoofRotation
        {
            get
            {
                Vector3 span = FallenRoofBottom - FallenRoofTop;
                return Quaternion.Euler(0f, 0f, Mathf.Atan(span.y / span.x) * Mathf.Rad2Deg);
            }
        }

        internal static Vector3 Eave(int side) => new Vector3(side * 3.48f, 2.86f, 0f);
        internal static Vector3 Ridge => new Vector3(-.18f, 4.42f, 0f);
        internal static Quaternion RoofRotation(int side)
        {
            Vector3 span = Ridge - Eave(side);
            float angle = Mathf.Atan(span.y / span.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle);
        }

        static void Porch(SoftMesh mesh)
        {
            Beam(mesh, new Vector3(-3.18f, 2.66f, -3.70f), new Vector3(3.18f, 2.66f, -3.70f), .18f, Bark);
            Beam(mesh, new Vector3(-3.16f, 2.70f, -3.793f), new Vector3(3.16f, 2.70f, -3.793f), .065f, EndGrain);
            for (int i = 0; i < 12; i++)
            {
                Vector3 tile = new Vector3(-2.97f + i * .54f, 2.78f + (i % 2) * .02f, -3.25f);
                mesh.Box(tile, new Vector3(.59f, .1f, 1.23f), Quaternion.Euler(-13f, 0f, 0f),
                    SoftMesh.Shade(RoofSlate, .82f + i % 4 * .11f));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 2.83f;
                Beam(mesh, new Vector3(x, 2.34f, Front - .08f), new Vector3(x, 2.66f, -3.70f), .15f, Timber);
                Box(mesh, new Vector3(x, 2.62f, -3.803f), new Vector3(.14f, .135f, .08f), Verdigris);
            }

            Gable(mesh);
            Vector3 flag = new Vector3(1.93f, 2.21f, -3.25f);
            Beam(mesh, flag + new Vector3(-.31f, .29f, 0f), flag + new Vector3(.31f, .29f, 0f), .047f, Brass);
            var cloth = new[]
            {
                flag + new Vector3(-.238f, .255f, -.025f), flag + new Vector3(.238f, .255f, -.025f),
                flag + new Vector3(.225f, -.29f, -.06f), flag + new Vector3(0f, -.398f, -.035f),
                flag + new Vector3(-.227f, -.29f, -.05f)
            };
            mesh.Fan(cloth, DarkSlate, Vector3.back, true);
            Beam(mesh, flag + new Vector3(-.136f, .053f, -.051f), flag + new Vector3(.14f, .053f, -.051f), .049f, Linen);
            Beam(mesh, flag + new Vector3(.018f, .04f, -.051f), flag + new Vector3(.018f, -.10f, -.074f), .076f, Linen);
            Beam(mesh, flag + new Vector3(-.092f, -.115f, -.071f), flag + new Vector3(.108f, -.115f, -.071f), .044f, Linen);

            mesh.Cyl(Lantern + Vector3.up * .20f, Lantern + Vector3.up * .55f, .018f, .018f, Metal, 6);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(mesh, Lantern + Vector3.up * (side * .18f), new Vector3(.31f, .075f, .29f), Brass);
                Box(mesh, Lantern + Vector3.right * (side * .13f) + Vector3.back * .095f,
                    new Vector3(.027f, .34f, .03f), Metal);
            }
        }

        static void Gable(SoftMesh mesh)
        {
            Vector3 crossbar = new Vector3(0f, 2.92f, -3.12f);
            Vector3 peak = Ridge + new Vector3(0f, -.17f, -3.12f);
            Vector3 left = crossbar + Vector3.left * 3.21f;
            Vector3 right = crossbar + Vector3.right * 3.21f;
            mesh.Tri(left, peak, right, DarkSlate, Vector3.back);
            Beam(mesh, left + Vector3.back * .041f, right + Vector3.back * .041f, .171f, Bark);
            Beam(mesh, crossbar, peak, .146f, Bark);
            Beam(mesh, left + Vector3.right * 1.11f, peak + new Vector3(.19f, -.40f, 0f), .126f, Timber);
            Beam(mesh, right + Vector3.left * 1.11f, peak + new Vector3(-.04f, -.40f, 0f), .126f, Timber);
            mesh.Cyl(Seal + Vector3.forward * .09f, Seal, .53f, .52f, Bark, Brass, true, true, 10);
            mesh.Cyl(Seal + Vector3.back * .002f, Seal + Vector3.back * .026f, .455f, .43f, Verdigris, DarkSlate, true, true, 10);
        }

        static void Barrier(SoftMesh mesh, bool ruined)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Beam(mesh, new Vector3(side * 1.18f, .29f, Front + .02f),
                    new Vector3(side * 1.14f, 1.66f, Front - .07f), .16f, Bark);
                mesh.Box(new Vector3(0f, .77f + i * .51f, Front - .08f), new Vector3(2.64f, .37f, .19f),
                    Quaternion.Euler(0f, 0f, side * (ruined ? 9f : 1.25f)), ruined ? AgedWood : Timber);
            }
            if (!ruined)
            {
                Beam(mesh, new Vector3(-2.93f, .4f, Front - .28f),
                    new Vector3(-1.47f, 2.55f, Front - .28f), .17f, EndGrain);
                Beam(mesh, new Vector3(2.93f, .4f, Front - .28f),
                    new Vector3(1.47f, 2.55f, Front - .28f), .17f, EndGrain);
            }
        }

        internal static SoftMesh Equipment(Phase phase)
        {
            var mesh = new SoftMesh();
            bool ready = phase == Phase.Restored;
            Hearth(mesh, phase);
            AnvilModel(mesh, ready);
            TableModel(mesh, ready);
            for (int i = 0; i < (ready ? 3 : 7); i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = -2.04f + i * .36f;
                Vector3 center = new Vector3(side * (2.17f + .12f * (i % 3)), .32f + .036f * (i % 3), z);
                mesh.Box(center, new Vector3(.74f, .13f, .25f), Quaternion.Euler(0f, i * 27f, i % 3 * 4f),
                    ready ? AgedWood : Timber);
            }
            return mesh;
        }

        static void Hearth(SoftMesh mesh, Phase phase)
        {
            bool ready = phase == Phase.Restored;
            Vector3 hearth = new Vector3(Fire.x, .59f, 1.77f);
            Box(mesh, hearth, new Vector3(1.87f, .70f, 1.69f), DarkStone);
            if (ready)
            {
                for (int i = 0; i < 4; i++)
                    Box(mesh, new Vector3(Fire.x + (i - 1.5f) * .458f, .54f, .91f),
                        new Vector3(.431f, .52f, .085f), SoftMesh.Shade(Stone, .80f + i % 3 * .095f));
                Box(mesh, new Vector3(Fire.x, .885f, .86f), new Vector3(1.88f, .10f, .18f), Metal);
            }
            Box(mesh, new Vector3(Fire.x, .94f, 1.69f), new Vector3(1.95f, .16f, 1.86f), Stone);
            Box(mesh, new Vector3(Fire.x, 1.27f, 2.16f), new Vector3(1.61f, .77f, .74f), Ash);
            for (int i = 0; i < 9; i++)
            {
                float angle = Mathf.PI * i / 8f;
                Vector3 stone = new Vector3(Fire.x + Mathf.Cos(angle) * .66f, 1.19f + Mathf.Sin(angle) * .62f, 1.19f);
                mesh.Puff(stone, new Vector3(.23f, .20f, .26f), SoftMesh.Shade(Stone, .85f + i % 4 * .09f),
                    Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg), 8, 4);
            }
            for (int i = 0; i < (ready ? 11 : phase == Phase.Frame ? 5 : 3); i++)
            {
                float width = Mathf.Max(.82f, 1.76f - i * .16f);
                Vector3 center = new Vector3(Mathf.Lerp(Fire.x, Chimney.x, Mathf.Clamp01(i / 5f)),
                    1.83f + i * .306f, Mathf.Lerp(1.70f, Chimney.z, Mathf.Clamp01(i / 5f)));
                mesh.Box(center, new Vector3(width, .324f, width * .81f),
                    Quaternion.Euler(0f, i % 2 == 0 ? 4f : -3f, 0f),
                    SoftMesh.Shade(i % 4 == 0 ? DarkStone : Stone, .80f + i % 4 * .09f));
            }
            if (ready)
            {
                Box(mesh, Chimney - Vector3.up * .12f, new Vector3(1.03f, .21f, .96f), DarkStone);
                Box(mesh, Chimney - Vector3.up * .005f, new Vector3(.69f, .035f, .64f), Ash);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(mesh, Chimney + new Vector3(side * .37f, .11f, 0f), new Vector3(.177f, .28f, .72f), Stone);
                    Box(mesh, Chimney + new Vector3(0f, .252f, side * .33f), new Vector3(.98f, .116f, .17f), Verdigris);
                }
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 coal = Fire + new Vector3((i % 3 - 1) * .23f, .002f, (i / 3) * .23f);
                Boulder(mesh, coal, new Vector3(.16f, .10f, .14f), Ash, 17f * i);
            }
        }

        static void AnvilModel(SoftMesh mesh, bool ready)
        {
            mesh.Cyl(Anvil, Anvil + Vector3.up * .58f, .44f, .38f, Bark, EndGrain, true, true, 10);
            mesh.Cyl(Anvil + Vector3.up * .35f, Anvil + Vector3.up * .43f, .42f, .41f, Metal, 10);
            Vector3 foot = Anvil + Vector3.up * .63f;
            Box(mesh, foot, new Vector3(.76f, .16f, .55f), Metal);
            Box(mesh, foot + Vector3.up * .17f, new Vector3(.42f, .23f, .30f), Metal);
            Box(mesh, foot + Vector3.up * .32f, new Vector3(.89f, .18f, .48f), ready ? Pewter : DarkSlate);
            mesh.Cyl(foot + new Vector3(.39f, .31f, 0f), foot + new Vector3(.79f, .31f, -.026f),
                .214f, .035f, ready ? Pewter : Metal, 8);
        }

        static void TableModel(SoftMesh mesh, bool ready)
        {
            float tilt = ready ? 0f : 7f;
            Vector3 board = Workbench + Vector3.up * .88f;
            foreach (float x in new[] { -.63f, .63f })
            foreach (float z in new[] { -.44f, .44f })
                Beam(mesh, Workbench + new Vector3(x * 1.1f, 0f, z), Workbench + new Vector3(x, .87f, z), .137f, Bark);

            mesh.Box(board, new Vector3(1.70f, .16f, 1.23f),
                Quaternion.Euler(0f, 0f, tilt), ready ? Timber : AgedWood);
            if (!ready) return;
            Box(mesh, board + new Vector3(0f, -.44f, .21f), new Vector3(1.31f, .11f, .72f), AgedWood);
            mesh.Cyl(Rune - Vector3.up * .063f, Rune - Vector3.up * .005f, .47f, .44f, DarkSlate, Stone, true, true, 8);
            for (int i = 0; i < 4; i++)
            {
                float angle = Mathf.PI * i * .5f;
                Vector3 p = Rune + new Vector3(Mathf.Cos(angle) * .34f, .006f, Mathf.Sin(angle) * .34f);
                mesh.Puff(p, new Vector3(.036f, .024f, .036f), Brass, 0f, 6, 3);
            }
            mesh.Cyl(board + new Vector3(.60f, .10f, .35f), board + new Vector3(.60f, .32f, .35f),
                .093f, .076f, Verdigris, 8);
            mesh.Cyl(board + new Vector3(-.54f, .10f, -.40f), board + new Vector3(-.24f, .10f, -.49f),
                .039f, .05f, Linen, 8);
        }

        internal static SoftMesh WarmGlow()
        {
            var mesh = new SoftMesh();
            Box(mesh, Lantern, new Vector3(.24f, .28f, .22f), SoftMesh.C(0xFBC67D));
            for (int i = 0; i < 3; i++)
            {
                Vector3 root = Fire + new Vector3((i - 1) * .19f, .012f, i % 2 * .08f);
                mesh.Teardrop(root, .42f - .075f * (i % 2), .12f, new Vector3(.045f, 0f, .022f),
                    SoftMesh.C(0xE95E38), SoftMesh.C(0xFFD081), 7, 5);
            }
            return mesh;
        }

        internal static SoftMesh RuneGlow()
        {
            var mesh = new SoftMesh();
            Color jade = SoftMesh.C(0x76D0AA);
            Vector3 lower = Rune + new Vector3(0f, .014f, -.23f);
            Vector3 left = Rune + new Vector3(-.20f, .014f, .04f);
            Vector3 right = Rune + new Vector3(.20f, .014f, .04f);
            Vector3 upper = Rune + new Vector3(0f, .014f, .23f);
            Beam(mesh, lower, left, .027f, jade);
            Beam(mesh, lower, right, .027f, jade);
            Beam(mesh, left, upper, .027f, jade);
            Beam(mesh, right, upper, .027f, jade);
            Beam(mesh, Rune + Vector3.back * .30f, Rune + Vector3.forward * .30f, .024f, jade);

            Vector3 seal = Seal + Vector3.back * .035f;
            Vector3 crown = seal + Vector3.up * .28f;
            Vector3 basePoint = seal - Vector3.up * .265f;
            Vector3 west = seal + Vector3.left * .171f;
            Vector3 east = seal + Vector3.right * .171f;
            Beam(mesh, crown, west, .034f, jade);
            Beam(mesh, crown, east, .034f, jade);
            Beam(mesh, basePoint, west, .034f, jade);
            Beam(mesh, basePoint, east, .034f, jade);
            Beam(mesh, crown, basePoint, .033f, jade);
            return mesh;
        }

        static void Boulder(SoftMesh mesh, Vector3 at, Vector3 half, Color color, float yaw)
            => mesh.Puff(at, half, color, yaw, seg: 7, rings: 4, gradient: .18f);

        static void Beam(SoftMesh mesh, Vector3 a, Vector3 b, float width, Color color)
        {
            Vector3 delta = b - a;
            Vector3 up = Mathf.Abs(delta.normalized.y) > .95f ? Vector3.forward : Vector3.up;
            mesh.Box((a + b) * .5f, new Vector3(width, width, delta.magnitude),
                Quaternion.LookRotation(delta, up), color);
        }

        static void Box(SoftMesh mesh, Vector3 center, Vector3 size, Color color)
            => mesh.Box(center, size, Quaternion.identity, color);
    }
}
