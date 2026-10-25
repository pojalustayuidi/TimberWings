#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Terrain/LocationGen.Environment.cs
// Окружение: камни, кусты, цветы, пеньки, грибы, декор площадок и троп. Всё сливается в чанки.
using UnityEngine;

public partial class LocationBuilder
{
    sealed partial class Gen
    {
        readonly Geo cyl4 = Prim.Cyl(4);

        // ключ: (cz*1000+cx)*2 + small. Мелочь (без теней) живёт в отдельных чанках.
        MeshBatch Batch(float x, float z, bool small)
        {
            int n = Mathf.CeilToInt(s.terrainSize / s.chunkSize);
            int cx = Mathf.Clamp(Mathf.FloorToInt((x + half) / s.chunkSize), 0, n - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((z + half) / s.chunkSize), 0, n - 1);
            int key = (cz * 1000 + cx) * 2 + (small ? 1 : 0);
            MeshBatch b;
            if (!batches.TryGetValue(key, out b)) { b = new MeshBatch(); batches[key] = b; }
            return b;
        }

        void Put(int mat, Geo g, Vector3 posU, Quaternion q, Vector3 scaleU)
        {
            Batch(posU.x, posU.z, IsSmall(mat)).Add(mat, g, Matrix4x4.TRS(posU * ws, q, scaleU * ws));
        }

        // ---------------------------------------------------------- запросы

        // нужны LocationCtx (модули)
        bool InGlade(float x, float z, float margin)
        {
            return forest != null && (new Vector2(x, z) - forest.pos).magnitude < forest.rad - margin;
        }

        float DistFlat(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z) - s.centerRadius;
            return Mathf.Min(r, Mathf.Min(FieldAt(sdTrail, x, z), FieldAt(sdPad, x, z)));
        }

        bool Free(float x, float z, float margin)
        {
            if (bridge != null && Mathf.Abs(z - bridge.pos.y) < 3.2f && x < bridge.pos.x + 1f) return false;
            if (Mathf.Abs(x) > half - 2f || Mathf.Abs(z) > half - 2f) return false;
            if (DistFlat(x, z) < margin) return false;
            if (HeightU(x, z) < -0.12f) return false;
            return true;
        }

        float Density(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float t1 = SS(9f, 55f, r);
            float t2 = SS(1.5f, 12f, DistFlat(x, z));
            return Mathf.Lerp(0.08f, 1f, t1) * Mathf.Lerp(0.35f, 1f, t2);
        }

        // материал камня: пятнами по шуму + немного случайности
        int PickRock(float x, float z)
        {
            float n = Perlin(x * 0.06f, z * 0.06f, 600f);
            if (rng.NextDouble() < 0.15) n = (float)rng.NextDouble();
            if (n < 0.26f) return M_ROCK3;
            if (n < 0.50f) return M_ROCK1;
            if (n < 0.74f) return M_ROCK0;
            if (n < 0.90f) return M_ROCK2;
            return M_ROCK4;
        }

        // ---------------------------------------------------------- элементы

        void AddRock(float x, float z, float size, bool sink)
        {
            float y = HeightU(x, z);
            int v = rng.Next(0, 3);
            int mat = PickRock(x, z);
            Quaternion q = Quaternion.Euler(Rf(-8f, 8f), Rf(0f, 360f), Rf(-8f, 8f));
            Vector3 sc = new Vector3(size * Rf(1.1f, 1.5f), size * Rf(0.6f, 0.9f), size * Rf(0.95f, 1.3f));
            Geo g = size > 0.9f ? icoS[v] : ico[v];
            Put(mat, g, new Vector3(x, y + sc.y * (sink ? 0.25f : 0.4f), z), q, sc);
            if (size > 1.2f) bigRocks.Add(new System.Collections.Generic.KeyValuePair<Vector3, float>(new Vector3(x, y + sc.y * 0.3f, z), size * 0.55f));
            if (size > 1.3f && rng.NextDouble() < 0.55)
                Put(M_MOSS, ico[v], new Vector3(x, y + sc.y * 0.58f, z), Quaternion.Euler(0f, Rf(0f, 360f), 0f),
                    new Vector3(sc.x * 0.62f, sc.y * 0.26f, sc.z * 0.62f));
        }

        void AddBush(float x, float z, bool dark)
        {
            float y = HeightU(x, z);
            float r = Rf(0.5f, 0.95f);
            int mat = dark ? M_FBUSH : (rng.Next(0, 2) == 0 ? M_BUSH0 : M_BUSH1);
            for (int k = 0; k < 3; k++)
            {
                float a = Rf(0f, Mathf.PI * 2f), o = k == 0 ? 0f : r * 0.45f;
                float rr = r * (k == 0 ? 1f : Rf(0.7f, 0.9f));
                Vector3 p = new Vector3(x + Mathf.Cos(a) * o, y + rr * 0.45f, z + Mathf.Sin(a) * o);
                Put(mat, ico[k], p, Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(rr * 1.1f, rr * 0.85f, rr * 1.1f));
            }
            // ягоды
            if (!dark && rng.NextDouble() < 0.3)
            {
                for (int k = 0; k < 4; k++)
                {
                    float a = Rf(0f, Mathf.PI * 2f);
                    Put(M_FR, ico[0], new Vector3(x + Mathf.Cos(a) * r * 0.5f, y + r * 0.7f, z + Mathf.Sin(a) * r * 0.5f),
                        Quaternion.identity, new Vector3(0.1f, 0.1f, 0.1f));
                }
            }
        }

        // запасная трава (когда Polish выключен)
        void AddTuft(float x, float z)
        {
            float y = HeightU(x, z);
            int blades = rng.Next(3, 6);
            for (int k = 0; k < blades; k++)
            {
                float a = Rf(0f, Mathf.PI * 2f), o = Rf(0f, 0.14f);
                float h = Rf(0.35f, 0.7f), d = Rf(0.1f, 0.18f);
                Quaternion q = Quaternion.Euler(Rf(-14f, 14f), Rf(0f, 360f), Rf(-14f, 14f));
                Vector3 b = new Vector3(x + Mathf.Cos(a) * o, y - 0.02f, z + Mathf.Sin(a) * o);
                Put(M_GRASS, cone4, b + q * new Vector3(0f, h * 0.5f, 0f), q, new Vector3(d, h, d));
            }
        }

        void AddFlowers(float x, float z)
        {
            int head = rng.Next(0, 3);
            int count = rng.Next(3, 8);
            for (int k = 0; k < count; k++)
            {
                float fx = x + Rf(-0.6f, 0.6f), fz = z + Rf(-0.6f, 0.6f);
                if (!Free(fx, fz, 1f)) continue;
                float y = HeightU(fx, fz);
                float h = Rf(0.3f, 0.5f);
                Put(M_STEM, cyl4, new Vector3(fx, y + h * 0.5f, fz), Quaternion.identity, new Vector3(0.05f, h, 0.05f));
                Put(M_FW + head, ico[0], new Vector3(fx, y + h, fz), Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(0.16f, 0.1f, 0.16f));
            }
        }

        void AddStump(float x, float z)
        {
            float y = HeightU(x, z);
            float d = Rf(0.6f, 1.0f), h = Rf(0.3f, 0.6f);
            Put(M_WOOD, cyl6, new Vector3(x, y + h * 0.5f - 0.03f, z), Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(d, h, d));
            Put(M_WOODL, cyl6, new Vector3(x, y + h - 0.02f, z), Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(d * 0.86f, 0.05f, d * 0.86f));
        }

        void AddLog(float x, float z)
        {
            float y = HeightU(x, z);
            float d = Rf(0.4f, 0.65f), len = Rf(1.6f, 2.9f);
            Quaternion q = Quaternion.Euler(0f, Rf(0f, 360f), 90f);
            Put(M_WOOD, cyl6, new Vector3(x, y + d * 0.42f, z), q, new Vector3(d, len, d));
        }

        void AddMushrooms(float x, float z)
        {
            int cnt = rng.Next(2, 5);
            for (int k = 0; k < cnt; k++)
            {
                float mx = x + Rf(-0.5f, 0.5f), mz = z + Rf(-0.5f, 0.5f);
                if (!Free(mx, mz, 1f)) continue;
                float y = HeightU(mx, mz);
                float h = Rf(0.14f, 0.28f), cap = Rf(0.22f, 0.4f);
                Put(M_MSTEM, cyl6, new Vector3(mx, y + h * 0.5f, mz), Quaternion.identity, new Vector3(0.09f, h, 0.09f));
                Put(M_CAP, cone5, new Vector3(mx, y + h + cap * 0.1f, mz), Quaternion.identity, new Vector3(cap, cap * 0.5f, cap));
            }
        }

        void AddFern(float x, float z)
        {
            float y = HeightU(x, z);
            int fr = rng.Next(5, 8);
            float baseYaw = Rf(0f, 360f);
            for (int k = 0; k < fr; k++)
            {
                float h = Rf(0.5f, 0.85f);
                Quaternion q = Quaternion.Euler(0f, baseYaw + k * (360f / fr) + Rf(-10f, 10f), 0f) * Quaternion.Euler(Rf(45f, 65f), 0f, 0f);
                Vector3 b = new Vector3(x, y, z);
                Put(M_FERN, cone4, b + q * new Vector3(0f, h * 0.5f, 0f), q, new Vector3(0.14f, h, 0.07f));
            }
        }

        void AddPebbles(float x, float z)
        {
            int n = rng.Next(3, 7);
            for (int k = 0; k < n; k++)
            {
                float px = x + Rf(-0.4f, 0.4f), pz = z + Rf(-0.4f, 0.4f);
                float y = HeightU(px, pz);
                float sz = Rf(0.1f, 0.24f);
                Put(M_PEBBLE, ico[k % 3], new Vector3(px, y + sz * 0.15f, pz), Quaternion.Euler(0f, Rf(0f, 360f), 0f),
                    new Vector3(sz * 1.3f, sz * 0.7f, sz));
            }
        }

        void AddLanternPost(float x, float z)
        {
            float y = HeightU(x, z);
            Put(M_WOOD, cyl6, new Vector3(x, y + 0.6f, z), Quaternion.identity, new Vector3(0.12f, 1.2f, 0.12f));
            Put(M_WOOD, box, new Vector3(x + 0.15f, y + 1.18f, z), Quaternion.identity, new Vector3(0.34f, 0.07f, 0.07f));
            Put(M_LANTERN, box, new Vector3(x + 0.3f, y + 1.0f, z), Quaternion.identity, new Vector3(0.2f, 0.26f, 0.2f));
            Put(M_WOODL, cone4, new Vector3(x + 0.3f, y + 1.2f, z), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.3f, 0.14f, 0.3f));
        }

        void AddSign(Vector2 pos, Vector2 dir)
        {
            float y = HeightU(pos.x, pos.y);
            Put(M_WOOD, box, new Vector3(pos.x, y + 0.575f, pos.y), Quaternion.identity, new Vector3(0.14f, 1.15f, 0.14f));
            float yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 d3 = new Vector3(dir.x, 0f, dir.y);
            Vector3 c = new Vector3(pos.x, y + 0.95f, pos.y) + d3 * 0.3f;
            Put(M_WOODL, box, c, q, new Vector3(0.8f, 0.2f, 0.07f));
            Quaternion q2 = Quaternion.FromToRotation(Vector3.up, d3);
            Put(M_WOODL, cone4, c + d3 * 0.48f, q2, new Vector3(0.24f, 0.22f, 0.24f));
        }

        Vector2 RandInDisc(Vector2 c, float r)
        {
            float a = Rf(0f, Mathf.PI * 2f);
            float rr = Mathf.Sqrt(Rf(0f, 1f)) * r;
            return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
        }

        // ---------------------------------------------------------- сборка окружения

        void BuildEnvironment()
        {
            float dens = s.envDensity;
            float area = (s.terrainSize / 140f) * (s.terrainSize / 140f);
            // трава Polish заменяет «клочки»-конусы (они тяжелее и без ветра)
            bool polishGrass = s.polishEnabled && s.polishGrass > 0.02f;

            // --- камни
            int rockAttempts = Mathf.RoundToInt(1400 * area);
            for (int k = 0; k < rockAttempts; k++)
            {
                float x = Rf(-half + 2f, half - 2f), z = Rf(-half + 2f, half - 2f), u = Rf(0f, 1f);
                if (!Free(x, z, 1.4f)) continue;
                if (u > Density(x, z) * 0.3f * dens) continue;
                AddRock(x, z, rng.NextDouble() < 0.06 ? Rf(1.3f, 2.0f) : Rf(0.3f, 0.95f), true);
            }
            // --- крупные валуны ближе к краю
            for (int k = 0, placed = 0; k < 800 && placed < Mathf.RoundToInt(45 * area * dens * s.boundaryRocks); k++)
            {
                float x = Rf(-half + 2f, half - 2f), z = Rf(-half + 2f, half - 2f);
                float edge = half - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (edge > 16f) continue;
                if (!Free(x, z, 3f)) continue;
                if (InGlade(x, z, -2f)) continue;
                AddRock(x, z, Rf(2.0f, 4.5f), true);
                placed++;
            }

            // --- кусты, трава, цветы, пеньки, брёвна вне глэйда
            int bushAtt = Mathf.RoundToInt(1500 * area);
            for (int k = 0; k < bushAtt; k++)
            {
                float x = Rf(-half + 2f, half - 2f), z = Rf(-half + 2f, half - 2f), u = Rf(0f, 1f);
                if (!Free(x, z, 1.6f) || InGlade(x, z, -1f)) continue;
                if (u > Density(x, z) * 0.42f * dens) continue;
                AddBush(x, z, false);
            }
            if (!polishGrass)
            {
                int tuftAtt = Mathf.RoundToInt(4200 * area);
                for (int k = 0; k < tuftAtt; k++)
                {
                    float x = Rf(-half + 2f, half - 2f), z = Rf(-half + 2f, half - 2f), u = Rf(0f, 1f);
                    if (!Free(x, z, 1.1f) || InGlade(x, z, -1f)) continue;
                    if (u > Density(x, z) * 0.55f * dens) continue;
                    AddTuft(x, z);
                }
            }
            int flowerAtt = Mathf.RoundToInt(500 * area);
            for (int k = 0; k < flowerAtt; k++)
            {
                float x = Rf(-half + 2f, half - 2f), z = Rf(-half + 2f, half - 2f), u = Rf(0f, 1f);
                if (!Free(x, z, 1.4f) || InGlade(x, z, -1f)) continue;
                if (u > Density(x, z) * 0.35f * dens) continue;
                AddFlowers(x, z);
            }
            for (int k = 0, placed = 0; k < 600 && placed < Mathf.RoundToInt(12 * area * dens); k++)
            {
                float x = Rf(-half + 2f, half - 2f), z = Rf(-half + 2f, half - 2f);
                if (!Free(x, z, 2f) || InGlade(x, z, -1f) || Density(x, z) < 0.4f) continue;
                if (placed % 2 == 0) AddStump(x, z); else AddLog(x, z);
                placed++;
            }
            for (int k = 0, placed = 0; k < 400 && placed < Mathf.RoundToInt(10 * area * dens); k++)
            {
                float x = Rf(-half + 2f, half - 2f), z = Rf(-half + 2f, half - 2f);
                if (!Free(x, z, 2f) || InGlade(x, z, -1f) || Density(x, z) < 0.5f) continue;
                AddMushrooms(x, z);
                placed++;
            }

            // --- подлесок глэйда (деревья ставит LocationForest)
            if (forest != null)
            {
                float R = forest.rad - 2f;
                // режим «по композиции»: случайного мусора в глэйде заметно меньше, поляна и тропа чистые
                float gd = s.forestClutter;
                int gl = 0;
                for (int k = 0; k < 4000 && gl < Mathf.RoundToInt(18 * dens * gd); k++)
                {
                    Vector2 p = RandInDisc(forest.pos, R);
                    if ((p - forest.pos).magnitude < 6f) continue;
                    if (!Free(p.x, p.y, 2f)) continue;
                    AddStump(p.x, p.y); gl++;
                }
                gl = 0;
                for (int k = 0; k < 4000 && gl < Mathf.RoundToInt(10 * dens * gd); k++)
                {
                    Vector2 p = RandInDisc(forest.pos, R);
                    if ((p - forest.pos).magnitude < 6f) continue;
                    if (!Free(p.x, p.y, 2.5f)) continue;
                    AddLog(p.x, p.y); gl++;
                }
                gl = 0;
                for (int k = 0; k < 4000 && gl < Mathf.RoundToInt(16 * dens * gd); k++)
                {
                    Vector2 p = RandInDisc(forest.pos, R);
                    if ((p - forest.pos).magnitude < 6f) continue;
                    if (!Free(p.x, p.y, 1.6f)) continue;
                    AddRock(p.x, p.y, Rf(0.4f, 1.4f), true); gl++;
                }
                gl = 0;
                for (int k = 0; k < 4000 && gl < Mathf.RoundToInt(30 * dens * gd); k++)
                {
                    Vector2 p = RandInDisc(forest.pos, R);
                    if ((p - forest.pos).magnitude < 6f) continue;
                    if (!Free(p.x, p.y, 2f)) continue;
                    AddBush(p.x, p.y, true); gl++;
                }
                gl = 0;
                for (int k = 0; k < 4000 && gl < Mathf.RoundToInt(70 * dens * gd); k++)
                {
                    Vector2 p = RandInDisc(forest.pos, R);
                    if ((p - forest.pos).magnitude < 6f) continue;
                    if (!Free(p.x, p.y, 1.5f)) continue;
                    AddFern(p.x, p.y); gl++;
                }
                gl = 0;
                for (int k = 0; k < 4000 && gl < Mathf.RoundToInt(25 * dens * gd); k++)
                {
                    Vector2 p = RandInDisc(forest.pos, R);
                    if ((p - forest.pos).magnitude < 6f) continue;
                    if (!Free(p.x, p.y, 1.5f)) continue;
                    AddMushrooms(p.x, p.y); gl++;
                }
                if (!polishGrass)
                {
                    gl = 0;
                    for (int k = 0; k < 6000 && gl < Mathf.RoundToInt(260 * dens * gd); k++)
                    {
                        Vector2 p = RandInDisc(forest.pos, R);
                    if ((p - forest.pos).magnitude < 6f) continue;
                        if (!Free(p.x, p.y, 1.2f)) continue;
                        AddTuft(p.x, p.y); gl++;
                    }
                }
            }

            // --- декор площадок (за их пределами, чтобы не мешать зданиям)
            foreach (var p in pads)
            {
                if (p.round) continue;
                float off = 0.9f;
                DecorSide(p, -p.hw - off, p.hd + off, true, p.hw * 2f + 2f * off);
                DecorSide(p, -p.hw - off, -p.hd - off, true, p.hw * 2f + 2f * off);
                DecorSide(p, -p.hw - off, -p.hd - off, false, p.hd * 2f + 2f * off);
                DecorSide(p, p.hw + off, -p.hd - off, false, p.hd * 2f + 2f * off);

                // каменные плитки на подходе к площадке
                for (int k = 0; k < 3; k++)
                {
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        if (rng.NextDouble() < 0.25) continue;
                        float lat = sd * (s.trailWidthAtPad * 0.5f - 0.15f + Rf(-0.1f, 0.2f));
                        Vector2 q = p.entry + p.fwd * (0.7f + k * 1.25f + Rf(-0.2f, 0.2f)) + p.right * lat;
                        float sw = Rf(0.55f, 0.85f);
                        float y = HeightU(q.x, q.y);
                        Put(M_SLAB, box, new Vector3(q.x, y + 0.04f, q.y), Quaternion.Euler(0f, Rf(0f, 360f), 0f),
                            new Vector3(sw, 0.09f, sw * Rf(0.8f, 1.1f)));
                    }
                }

                // фонарь у входа на площадку
                if (s.padLanterns)
                {
                    float side = (p.def.name.Length % 2 == 0) ? 1f : -1f;
                    Vector2 lp = p.pos + p.right * (side * (p.hw + 0.9f)) + p.fwd * (p.hd + 0.6f);
                    if (FieldAt(sdTrail, lp.x, lp.y) > 0.8f && Free(lp.x, lp.y, 0.2f)) AddLanternPost(lp.x, lp.y);
                }
            }

            // --- каменная окантовка центра (запасная, если модуль «Центр» выключен)
            if (!s.buildCenter)
            {
                float cr = s.centerRadius + 0.35f;
                float stepAng = 1.25f / cr;
                for (float a = 0f; a < Mathf.PI * 2f; a += stepAng)
                {
                    float x = Mathf.Cos(a) * cr, z = Mathf.Sin(a) * cr;
                    if (FieldAt(sdTrail, x, z) < 1.0f) continue;
                    AddRock(x, z, Rf(0.4f, 0.65f), true);
                }
            }

            // --- камни, галька, цветы и кусты вдоль троп; указатели на развилках
            foreach (var tr in trails)
            {
                for (int i = 6; i < tr.p.Count - 2; i += 2)
                {
                    Vector2 tan = (tr.p[i + 1] - tr.p[i - 1]).normalized;
                    Vector2 nrm = new Vector2(-tan.y, tan.x);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double r0 = rng.NextDouble();
                        if (r0 < 0.36)
                        {
                            Vector2 q = tr.p[i] + nrm * side * (tr.half[i] + Rf(0.35f, 0.7f));
                            if (FieldAt(sdTrail, q.x, q.y) > 0.15f && FieldAt(sdPad, q.x, q.y) > 0.6f && HeightU(q.x, q.y) > -0.1f)
                                AddRock(q.x, q.y, Rf(0.2f, 0.38f), true);
                        }
                        else if (r0 < 0.54)
                        {
                            Vector2 q = tr.p[i] + nrm * side * (tr.half[i] + Rf(0.3f, 0.9f));
                            if (FieldAt(sdTrail, q.x, q.y) > 0.1f && FieldAt(sdPad, q.x, q.y) > 0.6f && HeightU(q.x, q.y) > -0.1f)
                                AddPebbles(q.x, q.y);
                        }
                        else if (r0 < 0.59)
                        {
                            Vector2 q = tr.p[i] + nrm * side * (tr.half[i] + Rf(1.2f, 2.2f));
                            if (Free(q.x, q.y, 0.9f)) AddBush(q.x, q.y, false);
                        }
                        else if (r0 < 0.64)
                        {
                            Vector2 q = tr.p[i] + nrm * side * (tr.half[i] + Rf(1.0f, 1.8f));
                            if (Free(q.x, q.y, 0.9f)) AddFlowers(q.x, q.y);
                        }
                    }
                }
            }
            foreach (var p in pads)
            {
                if (!p.branch) continue;
                Vector2 branchDir = p.trail != null && p.trail.p.Count > 8 ? (p.trail.p[7] - p.trail.p[0]).normalized : -p.fwd;
                Vector2 nrm = new Vector2(-p.forkTan.y, p.forkTan.x);
                float side = Mathf.Sign(Vector2.Dot(nrm, branchDir));
                if (Mathf.Abs(side) < 0.5f) side = 1f;
                AddSign(p.fork + nrm * side * (s.trailWidth * 0.5f + 1.3f), branchDir);
            }
        }

        // идёт вдоль стороны площадки в локальных координатах (lx, lz) от стартовой точки
        void DecorSide(Pad p, float lx0, float lz0, bool alongX, float length)
        {
            for (float t = 0f; t <= length; t += 1.35f)
            {
                if (rng.NextDouble() > 0.7) continue;
                float lx = alongX ? lx0 + t : lx0;
                float lz = alongX ? lz0 : lz0 + t;
                Vector2 q = p.pos + p.right * (lx + Rf(-0.3f, 0.3f)) + p.fwd * (lz + Rf(-0.3f, 0.3f));
                if (FieldAt(sdTrail, q.x, q.y) < 1.0f || FieldAt(sdPad, q.x, q.y) < 0.4f) continue;
                if (!Free(q.x, q.y, 0.3f) && DistFlat(q.x, q.y) < 0f) continue;
                if (HeightU(q.x, q.y) < -0.1f) continue;
                double r0 = rng.NextDouble();
                if (r0 < 0.3) AddBush(q.x, q.y, false);
                else if (r0 < 0.4) AddFlowers(q.x, q.y);
                else AddRock(q.x, q.y, Rf(0.3f, 0.7f), true);
            }
        }

        // ---------------------------------------------------------- flush

        void FlushChunks()
        {
            var env = new GameObject("Environment").transform;
            env.SetParent(root, false);
            foreach (var kv in batches)
            {
                bool small = (kv.Key & 1) == 1;
                int c = kv.Key >> 1;
                int cx = c % 1000, cz = c / 1000;
                string nm = "Chunk_" + cx + "_" + cz + (small ? "_S" : "");
                var go = new GameObject(nm);
                go.transform.SetParent(env, false);
                if (!FinishMesh(go, kv.Value, nm, !small)) UnityEngine.Object.DestroyImmediate(go);
            }

            // коллайдеры крупных камней
            var cols = new GameObject("Colliders").transform;
            cols.SetParent(env, false);
            foreach (var kv in bigRocks)
            {
                var c = new GameObject("RockCollider");
                c.transform.SetParent(cols, false);
                c.transform.localPosition = kv.Key * ws;
                c.AddComponent<SphereCollider>().radius = kv.Value * ws;
                c.isStatic = true;
            }
        }
    }
}
#endif