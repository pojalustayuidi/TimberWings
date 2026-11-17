using UnityEngine;

namespace IslandV2
{
    internal static class TWMysticDecorModels
    {
        internal static readonly Vector3 WhisperStone = new Vector3(0.71f, 0.41f, 1.04f);
        static readonly Color Slate = SoftMesh.C(0x7B8493);
        static readonly Color Moss = SoftMesh.C(0x74885B);

        internal static void SilverFork(SoftMesh wood, SoftMesh leaves, bool detail)
        {
            Color bark = SoftMesh.C(0xABA6B3);
            Color blue = SoftMesh.C(0x688F9E);
            Color pale = SoftMesh.C(0xA1B9C6);
            int sides = detail ? 8 : 5;
            Roots(wood, bark, 0.62f, 2.0f, sides, 231);
            Wood(wood, bark, sides,
                (new Vector3(0f, -0.24f, 0f), 0.74f),
                (new Vector3(-0.20f, 1.05f, -0.02f), 0.52f),
                (new Vector3(-0.29f, 2.72f, 0.09f), 0.46f),
                (new Vector3(-0.75f, 4.68f, 0.33f), 0.24f),
                (new Vector3(-1.42f, 6.85f, 0.48f), 0.047f));
            Wood(wood, bark, sides,
                (new Vector3(-0.29f, 2.58f, 0.02f), 0.38f),
                (new Vector3(0.21f, 3.78f, -0.35f), 0.29f),
                (new Vector3(1.18f, 5.42f, -0.45f), 0.17f),
                (new Vector3(1.09f, 7.58f, -0.50f), 0.046f));
            Wood(wood, bark, sides,
                (new Vector3(-0.78f, 4.77f, 0.22f), 0.15f),
                (new Vector3(-1.74f, 5.30f, -0.46f), 0.11f),
                (new Vector3(-2.46f, 5.88f, -0.94f), 0.037f));
            Wood(wood, bark, sides,
                (new Vector3(1.12f, 5.31f, -0.48f), 0.14f),
                (new Vector3(1.91f, 5.65f, 0.28f), 0.09f),
                (new Vector3(2.38f, 6.14f, 0.89f), 0.026f));
            Tuft(leaves, new Vector3(-2.43f, 5.99f, -0.96f), new Vector3(1.22f, 0.52f, 0.87f), blue, -32f, detail);
            Tuft(leaves, new Vector3(-1.43f, 7.00f, 0.46f), new Vector3(1.17f, 0.86f, 1.11f), blue, 22f, detail);
            Tuft(leaves, new Vector3(2.36f, 6.30f, 0.86f), new Vector3(1.23f, 0.54f, 0.94f), blue, 32f, detail);
            Tuft(leaves, new Vector3(1.08f, 7.72f, -0.47f), new Vector3(0.98f, 0.94f, 1.06f), pale, 12f, detail);
            if (detail)
            {
                Tuft(leaves, new Vector3(-1.55f, 7.56f, -0.03f), new Vector3(0.65f, 0.53f, 0.62f), pale, -17f, true);
                Tuft(leaves, new Vector3(2.77f, 6.50f, 0.61f), new Vector3(0.56f, 0.42f, 0.58f), pale, 41f, true);
            }
        }

        internal static void DuskWillow(SoftMesh wood, SoftMesh leaves, bool detail)
        {
            Color bark = SoftMesh.C(0x666270);
            Color teal = SoftMesh.C(0x40857D);
            Color bright = SoftMesh.C(0x73A9A5);
            int sides = detail ? 8 : 5;
            Roots(wood, bark, 0.63f, 2.3f, sides, 663);
            Wood(wood, bark, sides,
                (new Vector3(0f, -0.24f, 0f), 0.78f),
                (new Vector3(0.24f, 0.98f, 0.10f), 0.54f),
                (new Vector3(0.40f, 2.64f, 0.04f), 0.41f),
                (new Vector3(0.08f, 4.36f, -0.28f), 0.27f),
                (new Vector3(-0.39f, 5.70f, -0.45f), 0.047f));
            Tuft(leaves, new Vector3(-0.38f, 5.88f, -0.48f), new Vector3(1.89f, 0.96f, 1.66f), bright, 28f, detail);

            float[] directions = { 22f, 117f, 201f, 293f };
            float[] lengths = { 2.5f, 3.47f, 2.94f, 3.1f };
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 aim = Yaw(directions[i]);
                Vector3 side = Vector3.Cross(aim, Vector3.up);
                Vector3 start = new Vector3(0.37f, 2.90f + 0.12f * i, -0.03f);
                Vector3 elbow = start + aim * (lengths[i] * 0.41f) + Vector3.up * 1.54f;
                Vector3 tip = start + aim * lengths[i] + Vector3.up * (0.97f + 0.11f * i);
                Wood(wood, bark, sides, (start, 0.28f), (elbow, 0.18f), (tip, 0.038f));
                Tuft(leaves, elbow + Vector3.up * 0.42f, new Vector3(1.44f, 0.67f, 1.31f), bright, directions[i], detail);
                Tuft(leaves, tip + Vector3.up * 0.22f, new Vector3(1.52f, 0.65f, 1.33f), teal, directions[i], detail);
                for (int strand = 0; strand < (detail ? 5 : 3); strand++)
                {
                    float spread = (strand - (detail ? 2f : 1f)) * (detail ? 0.31f : 0.58f);
                    Vector3 at = tip + Vector3.up * 0.24f + aim * (0.48f + 0.30f * TreeMB.H(87 + i, strand)) + side * spread;
                    Pendant(leaves, at, aim, side, 0.83f + 0.91f * TreeMB.H(95 + i, strand), teal, bright);
                }
            }
        }

        internal static void WhisperTree(SoftMesh wood, SoftMesh leaves, bool detail)
        {
            Color bark = SoftMesh.C(0x776275);
            Color plum = SoftMesh.C(0x81718F);
            Color lilac = SoftMesh.C(0xAB92B3);
            int sides = detail ? 8 : 5;
            Roots(wood, bark, 0.78f, 2.46f, sides, 458);
            Wood(wood, bark, sides,
                (new Vector3(0f, -0.28f, 0f), 0.91f),
                (new Vector3(-0.32f, 1.4f, -0.12f), 0.63f),
                (new Vector3(-0.74f, 2.74f, -0.40f), 0.53f),
                (new Vector3(-0.75f, 4.29f, -0.67f), 0.38f),
                (new Vector3(0.76f, 5.46f, -0.56f), 0.21f),
                (new Vector3(2.42f, 5.58f, 0.16f), 0.056f));
            Wood(wood, bark, sides,
                (new Vector3(-0.72f, 3.26f, -0.50f), 0.32f),
                (new Vector3(-1.68f, 3.99f, 0.48f), 0.21f),
                (new Vector3(-2.52f, 4.79f, 0.88f), 0.04f));
            Wood(wood, bark, sides,
                (new Vector3(-0.42f, 4.50f, -0.63f), 0.23f),
                (new Vector3(-1.23f, 5.87f, -0.94f), 0.12f),
                (new Vector3(-0.74f, 6.85f, -1.14f), 0.044f));
            Tuft(leaves, new Vector3(-2.47f, 5.05f, 0.89f), new Vector3(1.47f, 0.74f, 1.27f), plum, -32f, detail);
            Tuft(leaves, new Vector3(2.30f, 5.73f, 0.07f), new Vector3(1.76f, 0.79f, 1.34f), plum, 25f, detail);
            Tuft(leaves, new Vector3(0.40f, 5.86f, -0.82f), new Vector3(1.46f, 0.71f, 1.24f), plum, 14f, detail);
            Tuft(leaves, new Vector3(-0.72f, 6.99f, -1.14f), new Vector3(1.08f, 0.86f, 1.02f), lilac, -16f, detail);
            if (detail)
                Tuft(leaves, new Vector3(2.82f, 6.14f, -0.36f), new Vector3(0.72f, 0.45f, 0.62f), lilac, 28f, true);

            // Rune cut into a separate stone at the front of the roots.
            wood.Box(WhisperStone, new Vector3(0.58f, 0.89f, 0.32f), Quaternion.identity, SoftMesh.C(0x575465));
            Vector3 face = WhisperStone + Vector3.forward * 0.164f + Vector3.up * 0.035f;
            Color rune = SoftMesh.C(0x91E2D6);
            RuneLine(face + new Vector3(-0.13f, 0.01f, 0), face + new Vector3(0f, 0.17f, 0));
            RuneLine(face + new Vector3(0f, 0.17f, 0), face + new Vector3(0.13f, 0.01f, 0));
            RuneLine(face + Vector3.up * 0.15f, face - Vector3.up * 0.20f);
            RuneLine(face - Vector3.up * 0.08f, face + new Vector3(0.13f, -0.02f, 0));

            void RuneLine(Vector3 from, Vector3 to)
            {
                Vector3 stroke = to - from;
                Vector3 center = (from + to) * 0.5f - Vector3.forward * 0.005f;
                float roll = Mathf.Atan2(-stroke.x, stroke.y) * Mathf.Rad2Deg;
                wood.Box(center, new Vector3(0.026f, stroke.magnitude + 0.01f, 0.022f),
                    Quaternion.Euler(0f, 0f, roll), rune, 1f);
            }
        }

        internal static SoftMesh MossBoulder()
        {
            var mesh = new SoftMesh();
            Stone(mesh, Vector3.zero, new Vector3(0.73f, 0.89f, 0.61f), -11f, Slate, 31);
            mesh.Puff(new Vector3(-0.10f, 0.74f, -0.04f), new Vector3(0.51f, 0.18f, 0.46f), Moss, -20f, 7, 3);
            mesh.Puff(new Vector3(0.18f, 0.65f, 0.32f), new Vector3(0.32f, 0.17f, 0.27f), SoftMesh.C(0x8D9D67), 21f, 7, 3);
            return mesh;
        }

        internal static SoftMesh SlateShelf()
        {
            var mesh = new SoftMesh();
            Stone(mesh, new Vector3(-0.20f, 0f, 0.03f), new Vector3(0.88f, 0.51f, 0.49f), 13f, Slate, 82);
            Stone(mesh, new Vector3(0.53f, 0f, -0.16f), new Vector3(0.53f, 0.32f, 0.37f), -24f, SoftMesh.C(0x626B80), 83);
            Stone(mesh, new Vector3(-0.29f, 0.19f, 0f), new Vector3(0.48f, 0.36f, 0.32f), 11f, SoftMesh.C(0x9A9FB0), 84);
            return mesh;
        }

        internal static SoftMesh LooseStones()
        {
            var mesh = new SoftMesh();
            Stone(mesh, new Vector3(-0.36f, 0f, 0.06f), new Vector3(0.27f, 0.29f, 0.22f), -19f, Slate, 13);
            Stone(mesh, new Vector3(0.19f, 0f, -0.18f), new Vector3(0.19f, 0.19f, 0.18f), 32f, SoftMesh.C(0x9194A4), 14);
            Stone(mesh, new Vector3(0.39f, 0f, 0.20f), new Vector3(0.15f, 0.11f, 0.11f), 51f, Slate, 15);
            Stone(mesh, new Vector3(-0.04f, 0f, 0.31f), new Vector3(0.10f, 0.095f, 0.10f), -8f, SoftMesh.C(0x626B80), 16);
            return mesh;
        }

        internal static SoftMesh Fern()
        {
            Color stem = SoftMesh.C(0x657F54);
            Color green = SoftMesh.C(0x79A46C);
            Color tipColor = SoftMesh.C(0xAEC682);
            var mesh = new SoftMesh();
            for (int i = 0; i < 6; i++)
            {
                Vector3 direction = Yaw(i * 60f + 13f * TreeMB.H(49, i));
                Vector3 side = Vector3.Cross(direction, Vector3.up);
                float length = 0.45f + 0.42f * TreeMB.H(72, i);
                float height = 0.48f + 0.17f * TreeMB.H(73, i);
                for (int segment = 0; segment < 4; segment++)
                {
                    float t = segment * 0.25f;
                    Vector3 start = At(t);
                    mesh.Cyl(start, At(t + 0.25f), 0.013f - 0.008f * t, 0.011f - 0.008f * t, stem, stem, false, false, 4);
                    float width = (0.23f - 0.13f * t) * (segment == 0 ? 0.42f : 1f);
                    Blade(mesh, start, start + direction * 0.15f + side * width, Vector3.up, 0.042f, green, tipColor);
                    Blade(mesh, start, start + direction * 0.14f - side * width, Vector3.up, 0.042f, green, tipColor);
                }
                Blade(mesh, At(0.82f), At(1.07f), Vector3.up, 0.031f, green, tipColor);

                Vector3 At(float t) => direction * (length * t) + Vector3.up * (height * Mathf.Sin(t * 2.09f));
            }
            return mesh;
        }

        internal static SoftMesh SilverUnderbrush()
        {
            var mesh = new SoftMesh();
            Color green = SoftMesh.C(0x496F71);
            Color silver = SoftMesh.C(0x87AEA8);
            for (int i = 0; i < 10; i++)
            {
                Vector3 aim = Yaw(i * 137.5f);
                float length = 0.29f + 0.35f * TreeMB.H(155, i);
                float rise = 0.31f + 0.43f * TreeMB.H(156, i);
                Blade(mesh, aim * 0.048f, aim * length + Vector3.up * rise, Vector3.up, 0.105f, green, silver);
                Blade(mesh, aim * 0.11f + Vector3.up * 0.12f, aim * (length + 0.22f) + Vector3.up * (rise * 0.55f), Vector3.up, 0.085f, green, silver);
            }
            return mesh;
        }

        internal static SoftMesh Heather()
        {
            var mesh = new SoftMesh();
            Color stem = SoftMesh.C(0x697C65);
            Color bloom = SoftMesh.C(0xB68BB6);
            Color bud = SoftMesh.C(0xD4A8C4);
            for (int i = 0; i < 7; i++)
            {
                Vector3 d = Yaw(i * 137.5f);
                Vector3 basePoint = d * (0.12f + 0.052f * i);
                Vector3 end = basePoint + d * 0.11f + Vector3.up * (0.38f + 0.31f * TreeMB.H(151, i));
                mesh.Cyl(basePoint, end, 0.019f, 0.009f, stem, stem, false, false, 4);
                Blade(mesh, basePoint + Vector3.up * 0.10f, basePoint + d * 0.20f + Vector3.up * 0.23f, Vector3.up, 0.032f, stem, Moss);
                Blade(mesh, basePoint + Vector3.up * 0.15f, basePoint - d * 0.15f + Vector3.up * 0.27f, Vector3.up, 0.033f, stem, Moss);
                mesh.Puff(end - Vector3.up * 0.12f, new Vector3(0.077f, 0.14f, 0.078f), bloom, i * 30f, 5, 3);
                mesh.Puff(end + Vector3.up * 0.03f, new Vector3(0.043f, 0.076f, 0.048f), bud, i * 18f, 5, 3);
            }
            return mesh;
        }

        internal static SoftMesh MossPatch()
        {
            var mesh = new SoftMesh();
            mesh.Puff(new Vector3(0f, 0.025f, 0f), new Vector3(0.69f, 0.11f, 0.48f), Moss, -22f, 8, 3);
            Color light = SoftMesh.C(0x95AC74);
            for (int i = 0; i < 6; i++)
            {
                Vector3 point = Yaw(i * 137.5f) * (0.11f + 0.22f * TreeMB.H(674, i));
                point.y = 0.07f;
                mesh.Puff(point, new Vector3(0.20f, 0.08f, 0.15f), i % 2 == 0 ? light : Moss, i * 37f, 6, 3);
            }
            return mesh;
        }

        static Vector3 Yaw(float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        static void Tuft(SoftMesh mesh, Vector3 center, Vector3 size, Color color, float yaw, bool detail)
        {
            var rotation = Quaternion.Euler(-9f, yaw, 7f);
            mesh.Puff(center, size, color, rotation, detail ? 9 : 6, detail ? 5 : 3, 0.39f);
        }

        static void Roots(SoftMesh mesh, Color bark, float width, float reach, int sides, int seed)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 direction = Yaw(i * 72f + 24f * TreeMB.H(seed, i));
                Vector3 side = Vector3.Cross(direction, Vector3.up);
                float length = reach * (0.67f + 0.34f * TreeMB.H(seed + 1, i));
                float sweep = (TreeMB.H(seed + 2, i) - 0.5f) * 0.89f;
                Wood(mesh, bark, sides,
                    (direction * (width * 0.31f) + Vector3.up * 0.73f, width * 0.43f),
                    (direction * (length * 0.47f) + Vector3.up * 0.27f, width * 0.29f),
                    (direction * (length * 0.77f) + side * (sweep * 0.38f) + Vector3.up * 0.09f, width * 0.18f),
                    (direction * length + side * sweep - Vector3.up * 0.10f, 0.029f));
            }
        }

        static void Wood(SoftMesh mesh, Color bark, int sides,
            params (Vector3 point, float radius)[] path)
        {
            var ring = new Vector3[path.Length, sides];
            Vector3 startAxis = (path[1].point - path[0].point).normalized;
            Vector3 reference = Mathf.Abs(startAxis.y) > 0.8f ? Vector3.forward : Vector3.up;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 axis = (path[Mathf.Min(path.Length - 1, i + 1)].point - path[Mathf.Max(0, i - 1)].point).normalized;
                Vector3 right = Vector3.Cross(axis, reference).normalized;
                Vector3 side = Vector3.Cross(axis, right);
                for (int j = 0; j < sides; j++)
                {
                    float angle = j * 2f * Mathf.PI / sides;
                    float radius = path[i].radius * (j % 2 == 0 ? 1.03f : 0.90f);
                    ring[i, j] = path[i].point + radius * (Mathf.Cos(angle) * right + Mathf.Sin(angle) * side);
                }
            }

            for (int j = 0; j < sides; j++)
            {
                int next = (j + 1) % sides;
                Color tone = SoftMesh.Shade(bark, 0.76f + 0.30f * TreeMB.H(66, j));
                for (int i = 1; i < path.Length; i++)
                {
                    Vector3 a = ring[i - 1, j], b = ring[i - 1, next], c = ring[i, next], d = ring[i, j];
                    Vector3 outward = (a + b + c + d) * 0.25f - (path[i - 1].point + path[i].point) * 0.5f;
                    mesh.Quad(a, b, c, d, tone, outward);
                }
                int last = path.Length - 1;
                mesh.Tri(path[last].point, ring[last, j], ring[last, next], tone, path[last].point - path[last - 1].point);
            }
        }

        static void Pendant(SoftMesh mesh, Vector3 at, Vector3 outward, Vector3 side, float length, Color dark, Color light)
        {
            Vector3 end = at + outward * 0.33f - Vector3.up * length;
            Vector3 fold = Vector3.Lerp(at, end, 0.36f) + outward * 0.15f;
            Vector3 left = fold - side * 0.155f, right = fold + side * 0.155f;
            Vector3 ridge = fold + outward * 0.10f;
            mesh.Quad(left, at, ridge, end, dark, outward);
            mesh.Quad(right, end, ridge, at, light, outward);
            mesh.Quad(left, end, ridge, at, dark, -outward);
            mesh.Quad(right, at, ridge, end, dark, -outward);
        }

        static void Blade(SoftMesh mesh, Vector3 start, Vector3 tip, Vector3 up, float width, Color shade, Color light)
        {
            Vector3 side = Vector3.Cross(tip - start, up).normalized * width;
            Vector3 middle = Vector3.Lerp(start, tip, 0.43f);
            Vector3 fold = middle + up * (width * 0.37f);
            mesh.Quad(middle - side, start, fold, tip, shade, up);
            mesh.Quad(middle + side, tip, fold, start, light, up);
            mesh.Quad(middle - side, tip, fold, start, shade, -up);
            mesh.Quad(middle + side, start, fold, tip, shade, -up);
        }

        static void Stone(SoftMesh mesh, Vector3 origin, Vector3 size, float yaw, Color color, int seed)
        {
            const int sides = 7;
            var bottom = new Vector3[sides];
            var waist = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                Vector3 d = Yaw(yaw + i * (360f / sides) + 11f * TreeMB.H(seed, i));
                d = Vector3.Scale(d, size) * (0.79f + 0.25f * TreeMB.H(seed + 1, i));
                bottom[i] = origin + d * 0.80f - Vector3.up * 0.07f;
                waist[i] = origin + d + Vector3.up * (size.y * (0.32f + 0.20f * TreeMB.H(seed + 2, i)));
                top[i] = origin + d * (0.49f + 0.20f * TreeMB.H(seed + 3, i))
                    + Vector3.up * (size.y * (0.79f + 0.17f * TreeMB.H(seed + 4, i)));
            }
            Vector3 crest = origin + new Vector3(-0.09f * size.x, size.y, 0.12f * size.z);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Vector3 outward = (waist[i] + waist[next]) * 0.5f - origin;
                outward.y = 0f;
                float shade = 0.78f + 0.28f * TreeMB.H(seed + 5, i);
                mesh.Quad(bottom[i], bottom[next], waist[next], waist[i], SoftMesh.Shade(color, shade * 0.76f), outward);
                mesh.Quad(waist[i], waist[next], top[next], top[i], SoftMesh.Shade(color, shade), outward);
                mesh.Tri(top[i], top[next], crest, SoftMesh.Shade(color, shade * 1.16f), Vector3.up);
            }
        }
    }
}
