#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/GrandpaTree.cs (Editor)
// A5 v4 «Cloud Candy»: достопримечательность «Дедушка» = ДРЕВНИЙ ХРАНИТЕЛЬ в стиле референса (сладкие облака, пастель).
// Меню: Tools/BuilderSim/Лес/Красота леса/1. Дедушка: собрать и поставить (230). Безопасно жать повторно.
//
// Что нового в v4 (было: тёмно-зелёное дерево с рунами):
//   • РУН БОЛЬШЕ НЕТ: ни глифов, ни рамы вокруг дупла, ни жил на стволе. Тайна теперь делается светом и кристаллами;
//   • крона = пастельные облака: лаванда, розовый, мята, сливочный. Большие пухлые шапки, сателлиты-«облачка» по краю и светлые «взбитые» макушки;
//   • возраст читается по силуэту: кряжистый скрученный ствол, 6 контрфорсов и 3 корня-горба, капы, дупло-арка, обломанная толстая ветка,
//     жемчужные сухие сучья над кроной (торчат из облаков), мёртвая ветвь, лишайник-«борода» мятного цвета, мох, трутовики;
//   • тайна: дупло подсвечено изнутри, вокруг него и между корнями растут светящиеся кристаллы, над землёй медленно кружат парящие кристаллы (Motes),
//     из дупла вылетают духи-огоньки (GrandpaVibe), на ветвях висят фонарики и гирлянда;
//   • качели остались и качаются как раньше.
// Префаб Assets/_Project/Prefabs/Landmarks/Landmark_Grandpa.prefab: LODGroup (2 уровня), сплошной ствол, LandmarkTree, GrandpaVibe.
// Тега Resource и ResourceItem нет: срубить нельзя. По E можно поздороваться.
// Поляну вокруг (камни-стражи, лавки, фонари, дорожка, грибное кольцо) строит GrandpaGrove.cs (пункт 7).
// Материалы новые числа получают сами (BuildPrefab вызывает ForestMats с force = true).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    // общие помощники геометрии (используют и дерево, и поляна)
    public static class GroveGeo
    {
        // параллелепипед: c центр, h половины сторон, q поворот. Порядок вершин TreeMB.Tri подбирает сам.
        public static void Box(TreeMB b, int sub, Vector3 c, Vector3 h, Quaternion q)
        {
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var s = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                p[i] = c + q * Vector3.Scale(s, h);
            }
            int[,] f = { { 0, 1, 3, 2 }, { 4, 5, 7, 6 }, { 0, 1, 5, 4 }, { 2, 3, 7, 6 }, { 0, 2, 6, 4 }, { 1, 3, 7, 5 } };
            for (int k = 0; k < 6; k++)
            {
                b.Tri(sub, p[f[k, 0]], p[f[k, 1]], p[f[k, 2]], c);
                b.Tri(sub, p[f[k, 0]], p[f[k, 2]], p[f[k, 3]], c);
            }
        }

        public static void Box(TreeMB b, int sub, Vector3 c, Vector3 h, float yawDeg) { Box(b, sub, c, h, Quaternion.Euler(0f, yawDeg, 0f)); }
    }

    public static class GrandpaTree
    {
        const string Dir = "Assets/_Project/Prefabs/Landmarks";
        const string PrefabPath = Dir + "/Landmark_Grandpa.prefab";
        const string SlipPath = ForestMats.MatDir + "/TreeTrunk_Slip.asset";

        public const float Height = 31f;
        public const float TrunkR = 2.0f;
        const float SplitY = 9.0f;      // высота, где ствол расходится на ветви
        const float SwingAz = 55f;      // азимут ветки с качелями (0 = вперёд к игроку, + вправо)

        // ============================================================ ствол
        static float TOfY(float y) { return Mathf.Clamp01((y + 0.5f) / (SplitY + 0.5f)); }
        static Vector3 TrunkPos(float t)
        {
            return new Vector3(Mathf.Sin(t * 6.3f) * 0.55f * t, Mathf.Lerp(-0.5f, SplitY, t), Mathf.Cos(t * 5.0f + 0.6f) * 0.45f * t);
        }
        // «шишковатый» ствол: к основной кривой добавлена волна, поэтому ствол выглядит кряжистым и старым
        static float TrunkRad(float t)
        {
            float baseR = Mathf.Lerp(1.55f, 1.05f, t) + 1.6f * Mathf.Exp(-t * 9f);
            return baseR * (1f + 0.07f * t * Mathf.Sin(t * 21f + 1.3f));
        }
        static Vector3 AzDir(float azDeg) { float a = azDeg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)); }

        // точка на поверхности ствола на высоте y, азимут az (0 = вперёд), push 1 = ровно по поверхности
        static Vector3 Surf(float y, float az, float push)
        {
            float t = TOfY(y);
            Vector3 c = TrunkPos(t);
            return new Vector3(c.x, y, c.z) + AzDir(az) * (TrunkRad(t) * push);
        }

        public static Vector3 SwingAnchor()
        {
            return TrunkPos(TOfY(5.4f)) + AzDir(SwingAz) * 4.6f + Vector3.up * 0.42f;
        }

        // разворот Дедушки лицом (дупло, дорожка) в сторону PAD_Forest, а если метки нет, то к костру
        public static float FacingYaw(Vector3 pos)
        {
            var pf = GameObject.Find("PAD_Forest");
            Vector3 look = pf != null ? pf.transform.position : Vector3.zero;
            Vector3 d = look - pos; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return 23f;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // ============================================================ ветка
        static Vector3[] Limb(Vector3 start, Vector3 dir, float len, float rise, int n, float wig, int seed)
        {
            var p = new Vector3[n];
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                p[i] = start + dir * (len * t) + Vector3.up * (rise * Mathf.Pow(t, 1.6f)) + side * (Mathf.Sin(t * 5f + seed) * wig * t);
            }
            return p;
        }

        // три главные ветви: одна и та же геометрия нужна и для дерева, и для лишайника на ней
        static readonly float[] MainAz = { 25f, 145f, 265f };
        static Vector3[] MainLimb(int k)
        {
            Vector3 top = TrunkPos(1f);
            Vector3 dir = AzDir(MainAz[k] + (TreeMB.H(130, k) - 0.5f) * 20f);
            return Limb(top + Vector3.up * -0.9f - dir * 0.2f, dir, 7.5f, 7.5f, 6, 0.7f, k * 3);
        }

        // ============================================================ крона
        struct Clump { public Vector3 c, r; public int layer; }

        static void Layer(List<Clump> l, int layer, int n, float rad, float radJ, float y, float yJ, Vector3 r, float off)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + off + (TreeMB.H(40 + layer, i) - 0.5f) * 0.5f;
                float rr = rad + (TreeMB.H(50 + layer, i) - 0.5f) * 2f * radJ;
                rr *= 1f + 0.22f * Mathf.Cos(a - 0.8f);   // ветер: с одной стороны крона шире, силуэт не «зонтик по линейке»
                float yy = y + (TreeMB.H(60 + layer, i) - 0.5f) * 2f * yJ;
                float k = 0.90f + 0.22f * TreeMB.H(70 + layer, i);
                l.Add(new Clump { c = new Vector3(Mathf.Sin(a) * rr, yy, Mathf.Cos(a) * rr), r = r * k * 1.14f, layer = layer });
            }
        }

        static List<Clump> Clumps()
        {
            var l = new List<Clump>();
            Layer(l, 0, 9, 7.8f, 1.2f, 11.8f, 0.9f, new Vector3(3.4f, 2.2f, 3.4f), 0.2f);   // «юбка»
            Layer(l, 1, 7, 5.2f, 1.4f, 15.8f, 1.0f, new Vector3(3.7f, 2.7f, 3.7f), 0.7f);
            Layer(l, 2, 5, 3.0f, 1.2f, 20.4f, 0.8f, new Vector3(3.2f, 2.4f, 3.2f), 0.3f);
            l.Add(new Clump { c = new Vector3(0.4f, 24.6f, 0.3f), r = new Vector3(3.0f, 2.5f, 3.0f), layer = 3 });
            l.Add(new Clump { c = new Vector3(-0.6f, 26.6f, -0.2f), r = new Vector3(2.1f, 1.9f, 2.1f), layer = 3 });
            return l;
        }

        // подвесные фонарики: под клякстами «юбки»
        public static List<Vector3> LanternSpots()
        {
            var cl = Clumps(); var res = new List<Vector3>();
            foreach (int k in new[] { 1, 4, 7 })
            {
                var c = cl[k];
                Vector3 inward = new Vector3(-c.c.x, 0f, -c.c.z).normalized;
                res.Add(new Vector3(c.c.x, c.c.y - c.r.y * 0.4f - 2.5f, c.c.z) + inward * 0.6f);
            }
            return res;
        }

        // лампочки гирлянды: под «юбкой»
        public static List<Vector3> BulbSpots()
        {
            var cl = Clumps(); var res = new List<Vector3>();
            for (int k = 0; k < 9; k++)
            {
                var c = cl[k];
                res.Add(c.c + new Vector3(Mathf.Cos(k * 2.4f) * 1.2f, -c.r.y * 0.55f - 0.85f - 0.5f * TreeMB.H(120, k), Mathf.Sin(k * 2.4f) * 1.2f));
            }
            return res;
        }

        // ============================================================ кристаллы (вместо рун)
        // Светящиеся кристаллы: вокруг дупла, между корнями и парящие вокруг ствола. Свет и пульс им даёт GrandpaVibe + FireSource.
        // точка на арке дупла (th 0..PI). Арка та же, что у «губы» дупла в BuildBody
        static Vector3 HollowArch(float th, float push)
        {
            float xx = 0.92f * Mathf.Cos(th);
            float yy = 0.35f + 2.15f * Mathf.Sin(th);
            float tt = TOfY(yy); Vector3 cc = TrunkPos(tt); float rr = TrunkRad(tt);
            float zz = cc.z + Mathf.Sqrt(Mathf.Max(0.1f, rr * rr - xx * xx)) + push;
            return new Vector3(0.1f + xx, yy, zz);
        }

        // углы корневых кристальных групп (в промежутках между корнями-горбами и мимо дорожки перед дуплом)
        static readonly float[] BaseCrystalAz = { 120f, 152f, 178f, 236f, 262f };

        static Vector3 BaseCrystalPos(int i)
        {
            float a = BaseCrystalAz[i] * Mathf.Deg2Rad;
            float r = 3.3f + 0.5f * TreeMB.H(31, i);
            return new Vector3(Mathf.Sin(a) * r, -0.05f, Mathf.Cos(a) * r);
        }

        // точки свечения кристаллов (локально, корень префаба): сюда FireSource ставит мягкие огоньки
        public static List<Vector3> CrystalSpots()
        {
            var res = new List<Vector3>();
            foreach (float th in new[] { 0.35f, 1.2f, 1.95f, 2.8f }) res.Add(HollowArch(th, 0.35f) + Vector3.up * 0.2f);
            for (int i = 0; i < BaseCrystalAz.Length; i++) res.Add(BaseCrystalPos(i) + Vector3.up * 0.55f);
            return res;
        }

        // один кристалл: пятигранная призма-шпиль. base - основание, dir - направление, len - длина, w - радиус
        public static void Crystal(TreeMB b, int sub, Vector3 baseP, Vector3 dir, float len, float w, float yawDeg)
        {
            dir.Normalize();
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 side2 = Vector3.Cross(dir, side).normalized;
            const int n = 5;
            Vector3 mid = baseP + dir * (len * 0.64f);
            Vector3 tip = baseP + dir * len;
            Vector3 inside = baseP + dir * (len * 0.45f);
            var ring = new Vector3[n];
            var low = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float a = (k / (float)n * 360f + yawDeg) * Mathf.Deg2Rad;
                Vector3 o = (side * Mathf.Cos(a) + side2 * Mathf.Sin(a));
                ring[k] = mid + o * w;
                low[k] = baseP + o * (w * 0.85f);
            }
            for (int k = 0; k < n; k++)
            {
                int k1 = (k + 1) % n;
                b.Tri(sub, low[k], ring[k], ring[k1], inside);
                b.Tri(sub, low[k], ring[k1], low[k1], inside);
                b.Tri(sub, ring[k], tip, ring[k1], inside);
            }
        }

        // группа кристаллов: центральный высокий и несколько наклонённых наружу
        public static void CrystalCluster(TreeMB b, int sub, Vector3 p, Vector3 outward, int count, float scale, int seed)
        {
            outward.y = 0f;
            outward = outward.sqrMagnitude < 0.01f ? Vector3.forward : outward.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            for (int i = 0; i < count; i++)
            {
                float k = i == 0 ? 1f : 0.45f + 0.4f * TreeMB.H(seed, i);
                float spread = i == 0 ? 0.12f : 0.55f + 0.35f * TreeMB.H(seed + 1, i);
                float sgn = (i % 2 == 0 ? 1f : -1f);
                Vector3 d = (Vector3.up * 1.0f + outward * spread * 0.8f + side * (sgn * spread * 0.7f * TreeMB.H(seed + 2, i))).normalized;
                Vector3 off = outward * (0.10f * i) + side * (sgn * 0.12f * i);
                Crystal(b, sub, p + off, d, 0.95f * scale * k, 0.13f * scale * (0.8f + 0.5f * k), TreeMB.H(seed + 3, i) * 72f);
            }
        }

        // парящий кристалл: двойная пирамида (центр c, размер s)
        static void Gem(TreeMB b, int sub, Vector3 c, float s, float yawDeg)
        {
            const int n = 5;
            Vector3 top = c + Vector3.up * (s * 1.55f), bot = c - Vector3.up * (s * 1.25f);
            var ring = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float a = (k / (float)n * 360f + yawDeg) * Mathf.Deg2Rad;
                ring[k] = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s;
            }
            for (int k = 0; k < n; k++)
            {
                int k1 = (k + 1) % n;
                b.Tri(sub, top, ring[k], ring[k1], c);
                b.Tri(sub, bot, ring[k1], ring[k], c);
            }
        }

        // ============================================================ меш: тело (0 кора, 1 кора-запас, 2 тёмное дупло)
        static Mesh BuildBody(int lod, string name)
        {
            var b = new TreeMB();
            bool hi = lod == 0;

            // ствол
            b.TubeF(0, TrunkPos, TrunkRad, hi ? 14 : 6, hi ? 14 : 8, true);
            Vector3 top = TrunkPos(1f);

            // винтовые складки коры: 6 валиков, закручены влево, лицевая часть свободна для рун
            if (hi)
            {
                float[] ra = { 118f, 150f, 182f, 214f, 246f, 276f };
                for (int i = 0; i < ra.Length; i++)
                {
                    var rp = new Vector3[9]; var rrad = new float[9];
                    for (int j = 0; j < 9; j++)
                    {
                        float t = j / 8f * 0.97f;
                        float y = Mathf.Lerp(-0.5f, SplitY, t);
                        rp[j] = Surf(y, ra[i] - 35f * t + (TreeMB.H(14, i) - 0.5f) * 8f, 1.0f);
                        rrad[j] = Mathf.Lerp(0.22f, 0.07f, t) * (0.8f + 0.4f * TreeMB.H(15, i + j));
                    }
                    b.Tube(0, rp, rrad, 5, false);
                }
            }

            // корни-контрфорсы (перед дуплом корней нет)
            int roots = hi ? 9 : 6;
            for (int i = 0; i < roots; i++)
            {
                float a = i / (float)roots * Mathf.PI * 2f + TreeMB.H(3, i) * 0.45f;
                float deg = Mathf.Repeat(a * Mathf.Rad2Deg + 180f, 360f) - 180f;
                if (Mathf.Abs(deg) < 26f) continue;
                Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 sd = new Vector3(d.z, 0f, -d.x);
                float len = 4.6f + TreeMB.H(7, i) * 2.0f;
                float wig = (TreeMB.H(9, i) - 0.5f) * 1.6f;
                var pts = new[]
                {
                    d * 1.2f + Vector3.up * 2.1f,
                    d * (len * 0.30f) + sd * (wig * 0.30f) + Vector3.up * 0.95f,
                    d * (len * 0.62f) + sd * (wig * 0.75f) + Vector3.up * 0.28f,
                    d * len + sd * wig + Vector3.up * -0.32f
                };
                b.Tube(0, pts, new[] { 1.05f, 0.72f, 0.44f, 0.12f }, hi ? 7 : 5, false);
                if (hi && i % 2 == 0)   // отросток-палец
                {
                    var f = new[] { pts[2], pts[2] + sd * 0.9f + d * 0.9f + Vector3.up * -0.1f, pts[2] + sd * 1.5f + d * 1.5f + Vector3.up * -0.3f };
                    b.Tube(0, f, new[] { 0.26f, 0.16f, 0.06f }, 5, false);
                }
            }

            // корни-ГОРБЫ: уходят из земли, выгибаются дугой выше колена и снова уходят в землю (как у очень старых деревьев)
            if (hi)
            {
                float[] ha = { 78f, 205f, 300f };
                for (int i = 0; i < ha.Length; i++)
                {
                    Vector3 d = AzDir(ha[i]);
                    Vector3 sd = new Vector3(d.z, 0f, -d.x);
                    float d0 = 3.0f + 0.3f * i;
                    var ap = new[]
                    {
                        d * d0 + Vector3.up * -0.25f,
                        d * (d0 + 0.55f) + sd * 0.15f + Vector3.up * 0.75f,
                        d * (d0 + 1.25f) + sd * 0.30f + Vector3.up * 1.30f,
                        d * (d0 + 2.00f) + sd * 0.35f + Vector3.up * 0.95f,
                        d * (d0 + 2.60f) + sd * 0.25f + Vector3.up * 0.10f,
                        d * (d0 + 3.00f) + sd * 0.20f + Vector3.up * -0.30f
                    };
                    b.Tube(0, ap, new[] { 0.38f, 0.34f, 0.30f, 0.30f, 0.33f, 0.36f }, 6, false);
                }
            }

            // наросты-капы на стволе (в стороне от глифов)
            float[] capAz = { 100f, 160f, 225f, 268f, 130f, 250f };
            float[] capY = { 2.6f, 3.9f, 3.0f, 4.6f, 6.2f, 6.6f };
            int caps = hi ? 6 : 2;
            for (int i = 0; i < caps; i++)
                b.Blob(0, Surf(capY[i], capAz[i], 0.92f), new Vector3(0.62f, 0.56f, 0.56f) * (0.8f + 0.4f * TreeMB.H(85, i)), 90 + i, hi ? 1 : 0, 0.35f, -1);

            // дупло: тёмный «провал» + губа из коры, привязаны к поверхности ствола
            {
                float hy = 1.5f; float ht = TOfY(hy);
                Vector3 hp = TrunkPos(ht);
                b.Blob(2, new Vector3(hp.x + 0.1f, hy, hp.z + TrunkRad(ht) * 0.88f), new Vector3(0.78f, 1.15f, 0.55f), 77, hi ? 1 : 0, 0.12f, -1);
                if (hi)
                {
                    var ap = new Vector3[9]; var ar = new float[9];
                    for (int i = 0; i < 9; i++)
                    {
                        float th = i / 8f * Mathf.PI;
                        float xx = 0.92f * Mathf.Cos(th);
                        float yy = 0.35f + 2.15f * Mathf.Sin(th);
                        float tt = TOfY(yy); Vector3 cc = TrunkPos(tt); float rr = TrunkRad(tt);
                        float zz = cc.z + Mathf.Sqrt(Mathf.Max(0.1f, rr * rr - xx * xx)) - 0.02f;
                        ap[i] = new Vector3(0.1f + xx, yy, zz);
                        ar[i] = 0.22f + 0.05f * Mathf.Sin(th);
                    }
                    b.Tube(0, ap, ar, 6, false);
                }
            }

            // три главные ветви + вторичные
            for (int k = 0; k < 3; k++)
            {
                var p = MainLimb(k);
                b.Tube(0, p, new[] { 0.95f, 0.82f, 0.66f, 0.50f, 0.36f, 0.22f }, hi ? 8 : 5, false);
                if (hi || k == 0)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 d2 = AzDir(MainAz[k] + s * 40f);
                        var q = Limb(p[4], d2, 3.2f, 2.5f, 4, 0.3f, k + s);
                        b.Tube(0, q, new[] { 0.34f, 0.26f, 0.18f, 0.10f }, hi ? 6 : 4, false);
                    }
            }
            // вожак
            b.Tube(0, new[] { top + Vector3.up * -0.8f, top + new Vector3(0.3f, 3.5f, 0.2f), top + new Vector3(-0.2f, 8.0f, -0.1f), top + new Vector3(0.2f, 12.5f, 0.1f) },
                   new[] { 0.9f, 0.7f, 0.45f, 0.22f }, hi ? 7 : 5, false);

            // ветка с качелями: ровная, чуть загибается вверх
            {
                Vector3 s0 = TrunkPos(TOfY(5.4f)); Vector3 dd = AzDir(SwingAz);
                var sp = new[] { s0 - dd * 0.4f, s0 + dd * 2.4f + Vector3.up * 0.15f, s0 + dd * 4.6f + Vector3.up * 0.42f, s0 + dd * 6.4f + Vector3.up * 1.1f };
                b.Tube(0, sp, new[] { 0.66f, 0.52f, 0.36f, 0.20f }, hi ? 8 : 5, false);
            }

            // крона теперь отдельным мешем (BuildCrown): пастельные облака с четырьмя цветами
            return b.Build(name);
        }

        // ============================================================ меш: крона «Cloud Candy» (0 лаванда, 1 розовый, 2 мята, 3 сливочный)
        // Цвет облака зависит от угла и яруса: рядом лежат пятна одного цвета (как в референсе), а не конфетти. Светлые «взбитые» макушки сверху.
        static int PuffSub(Clump c, int i)
        {
            float ang = Mathf.Atan2(c.c.x, c.c.z) / (Mathf.PI * 2f) + 0.5f;          // 0..1 вокруг ствола
            int k = Mathf.FloorToInt(ang * 4f + c.layer * 0.9f + 0.35f * TreeMB.H(401, i));
            int sub = ((k % 4) + 4) % 4;
            if (c.layer == 3) sub = (i % 2 == 0) ? 3 : 1;                            // макушка: сливочная и розовая
            else if (TreeMB.H(402, i) > 0.82f) sub = 3;                              // редкие сливочные акценты
            return sub;
        }

        static Mesh BuildCrown(int lod, string name)
        {
            var b = new TreeMB();
            bool hi = lod == 0;
            var cl = Clumps();
            for (int i = 0; i < cl.Count; i++)
            {
                var c = cl[i];
                int sub = PuffSub(c, i);
                int seed = 200 + i * 7;
                b.Blob(sub, c.c, c.r, seed, 1, 0.16f, -1);                           // основной шар: мягкий, малый шум = «облако», а не булыжник
                if (!hi) continue;

                // боковой выступ того же цвета и два облачка-сателлита по краю (силуэт «цветной капусты»)
                float a0 = TreeMB.H(150, i) * Mathf.PI * 2f;
                b.Blob(sub, c.c + new Vector3(Mathf.Sin(a0) * c.r.x * 0.62f, -c.r.y * 0.22f, Mathf.Cos(a0) * c.r.z * 0.62f), c.r * 0.66f, seed + 1, 1, 0.16f, -1);
                for (int j = 0; j < 2; j++)
                {
                    float a = a0 + 2.1f + j * 2.5f + TreeMB.H(160 + j, i) * 0.8f;
                    float u = 0.15f + 0.55f * TreeMB.H(170 + j, i);
                    int ssub = TreeMB.H(180 + j, i) > 0.55f ? (sub + 1) % 4 : sub;     // соседний цвет у части сателлитов: переливы
                    Vector3 dir = new Vector3(Mathf.Sin(a), u, Mathf.Cos(a)).normalized;
                    b.Blob(ssub, c.c + Vector3.Scale(dir, c.r) * 0.95f, c.r * (0.36f + 0.10f * TreeMB.H(190 + j, i)), seed + 2 + j, 0, 0.18f, -1);
                }
                if (sub != 3)   // «взбитая» светлая макушка
                    b.Blob(3, c.c + Vector3.up * (c.r.y * 0.60f), new Vector3(c.r.x * 0.58f, c.r.y * 0.34f, c.r.z * 0.58f), seed + 5, 1, 0.14f, -1);
            }

            // кончик ветки с качелями: маленькое облачко над ней
            {
                Vector3 s0 = TrunkPos(TOfY(5.4f)); Vector3 dd = AzDir(SwingAz);
                b.Blob(1, s0 + dd * 6.4f + Vector3.up * 1.6f, new Vector3(1.5f, 1.05f, 1.5f), 140, hi ? 1 : 0, 0.16f, -1);
            }
            return b.Build(name);
        }

        // ============================================================ меш: парящие кристаллы (0 кристалл). Их медленно крутит GrandpaVibe
        public const int MoteCount = 9;
        static Mesh BuildMotes(string name)
        {
            var b = new TreeMB();
            for (int i = 0; i < MoteCount; i++)
            {
                float az = 55f + 40f + i * (320f / MoteCount) + (TreeMB.H(450, i) - 0.5f) * 16f;   // обходим ветку с качелями (azimut 55)
                float rr = 5.4f + 2.6f * TreeMB.H(451, i);
                float y = 3.2f + 6.0f * TreeMB.H(452, i);
                Vector3 c = AzDir(az) * rr + Vector3.up * y;
                Gem(b, 0, c, 0.17f + 0.15f * TreeMB.H(453, i), TreeMB.H(454, i) * 72f);
            }
            return b.Build(name);
        }

        // ============================================================ меш: детали (0 мох и лианы, 1 жемчужное цветение, 2 розовые грибы, 3 тёплое)
        static Mesh BuildDetail(string name)
        {
            var b = new TreeMB();
            var cl = Clumps();

            // мох на корнях (сверху толстых лап) и вокруг основания
            for (int i = 0; i < 10; i++)
            {
                float a = TreeMB.H(160, i) * Mathf.PI * 2f;
                float deg = Mathf.Repeat(a * Mathf.Rad2Deg + 180f, 360f) - 180f;
                if (Mathf.Abs(deg) < 25f) a += 0.9f;
                float r = 2.4f + TreeMB.H(161, i) * 2.2f;
                b.Blob(0, new Vector3(Mathf.Sin(a) * r, 0.42f - r * 0.07f, Mathf.Cos(a) * r), new Vector3(0.80f, 0.24f, 0.66f), 170 + i, 1, 0.35f, -1);
            }
            // мох на северной (задней) стороне ствола
            for (int i = 0; i < 8; i++)
                b.Blob(0, Surf(1.6f + i * 0.95f, 120f + TreeMB.H(180, i) * 130f, 0.98f), new Vector3(0.44f, 0.90f, 0.34f), 181 + i, 1, 0.35f, -1);

            // грибы-трутовики на стволе (тёплые полки), подальше от глифов
            float[] sy = { 3.6f, 4.7f, 5.5f, 2.3f, 4.0f };
            float[] sa = { -92f, -104f, 118f, 152f, 205f };
            for (int i = 0; i < 5; i++)
                b.Blob(3, Surf(sy[i], sa[i], 1.02f), new Vector3(0.5f, 0.11f, 0.42f), 190 + i, 1, 0.3f, -1);

            // красные грибки у корней слева от дупла
            for (int i = 0; i < 8; i++)
            {
                float a = (-52f - i * 9f + TreeMB.H(200, i) * 14f) * Mathf.Deg2Rad;
                float r = 2.7f + TreeMB.H(201, i) * 1.3f;
                Vector3 p = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                float h = 0.18f + 0.26f * TreeMB.H(202, i);
                b.Tube(3, new[] { p + Vector3.down * 0.05f, p + new Vector3(0.02f, h, 0.01f) }, new[] { 0.06f, 0.045f }, 5, false);
                b.Blob(2, p + new Vector3(0.02f, h + 0.05f, 0.01f), new Vector3(0.24f, 0.14f, 0.24f) * (0.8f + 0.6f * TreeMB.H(203, i)), 210 + i, 0, 0.25f, -1);
            }

            // лианы с «юбки»: тонкие плети с листиками на конце
            for (int i = 0; i < 9; i++)
            {
                var c = cl[i];
                float a = TreeMB.H(220, i) * Mathf.PI * 2f;
                Vector3 s = c.c + new Vector3(Mathf.Sin(a) * c.r.x * 0.7f, -c.r.y * 0.45f, Mathf.Cos(a) * c.r.z * 0.7f);
                float len = 2.4f + 2.0f * TreeMB.H(221, i);
                var vp = new[] { s, s + new Vector3(0.12f, -len * 0.5f, 0.05f), s + new Vector3(-0.05f, -len, 0.1f) };
                b.Tube(0, vp, new[] { 0.05f, 0.04f, 0.025f }, 4, false);
                b.Blob(0, vp[2] + Vector3.down * 0.1f, new Vector3(0.20f, 0.30f, 0.20f), 230 + i, 0, 0.3f, -1);
                b.Blob(0, vp[1], new Vector3(0.16f, 0.22f, 0.16f), 240 + i, 0, 0.3f, -1);
            }

            // розовое цветение: теперь только акцент (по 2-3 пятна на клякст)
            int bi = 0;
            for (int i = 0; i < cl.Count; i++)
            {
                var c = cl[i];
                int n = c.layer == 3 ? 2 : (i % 2 == 0 ? 3 : 2);
                for (int j = 0; j < n; j++)
                {
                    float a = TreeMB.H(250 + i, j) * Mathf.PI * 2f;
                    float u = 0.10f + 0.85f * TreeMB.H(270 + i, j);
                    float sq = Mathf.Sqrt(1f - u * u);
                    Vector3 dir = new Vector3(sq * Mathf.Cos(a), u, sq * Mathf.Sin(a));
                    float k = 0.8f + 0.5f * TreeMB.H(290 + i, j);
                    b.Blob(1, c.c + Vector3.Scale(dir, c.r) * 1.0f, new Vector3(0.62f, 0.42f, 0.62f) * k, 300 + bi++, 0, 0.3f, -1);
                }
            }

            // подвесные фонарики: лиана + тёплый шар
            foreach (var sp in LanternSpots())
            {
                b.Tube(0, new[] { sp + Vector3.up * 2.9f, sp + Vector3.up * 0.3f }, new[] { 0.035f, 0.03f }, 4, false);
                b.Blob(3, sp, new Vector3(0.22f, 0.30f, 0.22f), 330 + (int)(sp.x * 7f), 0, 0.10f, -1);
            }
            // лампочки гирлянды
            var bulbs = BulbSpots();
            for (int i = 0; i < bulbs.Count; i++)
                b.Blob(3, bulbs[i], new Vector3(0.11f, 0.14f, 0.11f), 340 + i, 0, 0.1f, -1);

            return b.Build(name);
        }

        // ============================================================ меш: «тайна» (0 жемчужное сухое дерево, 1 светящиеся кристаллы, 2 лишайник, 3 камни)
        // lod 1: только сухие сучья и обломанная ветка (силуэт читается издалека)
        static Mesh BuildMystic(int lod, string name)
        {
            var b = new TreeMB();
            bool hi = lod == 0;

            // ---- сухие сучья над кроной: силуэт старика
            Vector3[] ss = { new Vector3(0.9f, 22.5f, 0.5f), new Vector3(-2.2f, 19.5f, 1.8f), new Vector3(2.8f, 18.5f, -1.9f) };
            Vector3[] sd = { new Vector3(0.25f, 1f, 0.15f), new Vector3(-0.55f, 1f, 0.30f), new Vector3(0.60f, 1f, -0.25f) };
            float[] sl = { 8.0f, 6.0f, 5.5f };
            for (int i = 0; i < 3; i++)
            {
                Vector3 d = sd[i].normalized;
                Vector3 side = Vector3.Cross(d, Vector3.up).normalized;
                var p = new Vector3[4];
                for (int j = 0; j < 4; j++)
                {
                    float t = j / 3f;
                    p[j] = ss[i] + d * (sl[i] * t) + side * (Mathf.Sin(t * 4f + i * 2f) * 0.45f * t);
                }
                b.Tube(0, p, new[] { 0.32f, 0.24f, 0.14f, 0.04f }, hi ? 6 : 4, false);
                if (hi)   // сучок-вилка
                {
                    Vector3 fd = (d + side * (i % 2 == 0 ? 0.9f : -0.9f)).normalized;
                    b.Tube(0, new[] { p[2], p[2] + fd * 1.1f + Vector3.up * 0.3f, p[2] + fd * 2.0f + Vector3.up * 0.9f }, new[] { 0.11f, 0.07f, 0.02f }, 4, false);
                }
            }

            // ---- низкая мёртвая ветвь под «юбкой» (жемчужная, видна против пастельной кроны)
            {
                Vector3 st = TrunkPos(TOfY(7.2f));
                Vector3 dd = AzDir(215f);
                var lp = Limb(st, dd, 6.0f, 2.0f, 5, 0.5f, 9);
                b.Tube(0, lp, new[] { 0.52f, 0.42f, 0.30f, 0.18f, 0.06f }, hi ? 6 : 4, false);
                if (hi)
                {
                    Vector3 d2 = AzDir(215f + 38f);
                    b.Tube(0, new[] { lp[2], lp[2] + d2 * 1.4f + Vector3.up * 0.5f, lp[2] + d2 * 2.5f + Vector3.up * 1.3f }, new[] { 0.16f, 0.10f, 0.03f }, 4, false);
                }
            }

            // ---- обломанная толстая ветка: рваный торец и щепка
            {
                Vector3 st = Surf(6.0f, 150f, 0.9f);
                Vector3 dd = AzDir(150f);
                var bp = new[] { st, st + dd * 1.0f + Vector3.up * 0.15f, st + dd * 1.55f + Vector3.up * 0.35f };
                b.Tube(0, bp, new[] { 0.44f, 0.38f, 0.33f }, hi ? 7 : 5, true);
                if (hi)
                    b.Tube(0, new[] { bp[2], bp[2] + Vector3.up * 0.55f + dd * 0.08f }, new[] { 0.09f, 0.01f }, 4, false);
            }

            if (!hi) return b.Build(name);

            // ---- кристаллы: обрамляют дупло и растут между корнями (свет даёт GrandpaVibe + FireSource)
            foreach (float th in new[] { 0.30f, 0.95f, 1.60f, 2.25f, 2.85f })
            {
                Vector3 p0 = HollowArch(th, 0.02f);
                Vector3 outw = new Vector3(Mathf.Cos(th) * 0.8f, 0f, 0.6f);
                CrystalCluster(b, 1, p0, outw, th > 1f && th < 2f ? 2 : 3, 0.62f, 700 + (int)(th * 10f));
            }
            for (int i = 0; i < BaseCrystalAz.Length; i++)
            {
                Vector3 p0 = BaseCrystalPos(i);
                CrystalCluster(b, 1, p0, p0, 4, 0.95f + 0.35f * TreeMB.H(720, i), 740 + i * 3);
            }
            // два кристалла-подвески на ветви с качелями (звенят на ветру, как колокольчики)
            {
                Vector3 s0 = TrunkPos(TOfY(5.4f)); Vector3 dd = AzDir(SwingAz + 18f);
                for (int i = 0; i < 2; i++)
                {
                    Vector3 hp = s0 + dd * (3.4f + i * 1.4f) + Vector3.up * (0.25f + 0.2f * i);
                    b.Tube(0, new[] { hp, hp + Vector3.down * 0.9f }, new[] { 0.02f, 0.02f }, 3, false);
                    Gem(b, 1, hp + Vector3.down * 1.05f, 0.12f + 0.03f * i, 20f * i);
                }
            }

            // ---- лишайник-«борода» на главных ветвях и мох поверх
            for (int k = 0; k < 3; k++)
            {
                var p = MainLimb(k);
                for (int j = 2; j < p.Length; j++)
                {
                    for (int s2 = 0; s2 < 2; s2++)
                    {
                        float len = 1.4f + 1.8f * TreeMB.H(500 + k, j * 2 + s2);
                        float ox = (s2 == 0 ? -0.18f : 0.18f);
                        Vector3 st = p[j] + new Vector3(ox, -0.10f, 0.05f * (s2 == 0 ? 1f : -1f));
                        var vp = new[] { st, st + new Vector3(0.08f, -len * 0.5f, 0.04f), st + new Vector3(-0.04f, -len, 0.07f) };
                        b.Tube(2, vp, new[] { 0.06f, 0.045f, 0.02f }, 4, false);
                        b.Blob(2, vp[2] + Vector3.down * 0.08f, new Vector3(0.15f, 0.28f, 0.15f), 520 + k * 31 + j * 3 + s2, 0, 0.3f, -1);
                    }
                    b.Blob(2, p[j] + Vector3.up * 0.18f, new Vector3(0.42f, 0.16f, 0.36f), 560 + k * 7 + j, 0, 0.35f, -1);
                }
            }

            // ---- два каменных кэрна у входа в дупло
            Vector3[] cairn = { new Vector3(0.95f, 0f, 3.55f), new Vector3(2.60f, 0f, 2.30f) };   // справа от дорожки: слева стоит рунный камень поляны
            for (int c = 0; c < cairn.Length; c++)
            {
                Vector3 p = cairn[c];
                b.Blob(3, p + new Vector3(0f, 0.12f, 0f), new Vector3(0.40f, 0.17f, 0.36f), 600 + c * 5, 0, 0.35f, -1);
                b.Blob(3, p + new Vector3(0.03f, 0.33f, 0.02f), new Vector3(0.29f, 0.14f, 0.26f), 601 + c * 5, 0, 0.35f, -1);
                b.Blob(3, p + new Vector3(-0.02f, 0.50f, 0.01f), new Vector3(0.18f, 0.12f, 0.17f), 602 + c * 5, 0, 0.35f, -1);
            }
            return b.Build(name);
        }

        // ============================================================ меш: качели (пивот в точке подвеса, висит вниз)
        static Mesh BuildSwing(string name)
        {
            var b = new TreeMB();
            const float L = 2.9f;
            for (int s = -1; s <= 1; s += 2)
                b.Tube(0, new[] { new Vector3(s * 0.36f, 0f, 0f), new Vector3(s * 0.36f, -L * 0.5f, 0f), new Vector3(s * 0.36f, -L, 0f) }, new[] { 0.028f, 0.028f, 0.028f }, 4, false);
            GroveGeo.Box(b, 1, new Vector3(0f, -L - 0.04f, 0f), new Vector3(0.52f, 0.035f, 0.17f), 0f);
            GroveGeo.Box(b, 1, new Vector3(0f, -L - 0.075f, 0f), new Vector3(0.50f, 0.012f, 0.15f), 0f);
            // узелки на верёвках
            for (int s = -1; s <= 1; s += 2)
                b.Blob(0, new Vector3(s * 0.36f, -0.05f, 0f), new Vector3(0.06f, 0.06f, 0.06f), 400 + s + 5, 0, 0.2f, -1);
            return b.Build(name);
        }

        // ============================================================ меню
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/1. Дедушка: собрать и поставить", false, 230)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Дедушка: останови Play Mode."); return; }
            var pad = GameObject.Find("PAD_Grandpa");
            if (pad == null) { Debug.LogError("Дедушка: в сцене нет метки PAD_Grandpa. Применить «Зоны A3-A4» и «2. Собрать блокаут» (Остров v2)."); return; }

            var prefab = BuildPrefab();
            if (prefab == null) return;
            Debug.Log(Place(prefab, pad.transform.position));
        }

        // ============================================================ префаб
        static GameObject BuildPrefab()
        {
            EditorAssets.EnsureFolder(Dir);
            // force = true: цвета v3 перезаписываются в уже существующих материалах
            var body = ForestMats.Grandpa(true);
            var crown = ForestMats.GrandpaCrown(true);
            var det = ForestMats.GrandpaDetail(true);
            var mys = ForestMats.GrandpaMystic(true);
            var moteMat = new[] { mys[1] };
            var swm = new[] { ForestMats.GroveWoodDark(false), ForestMats.GroveWood(false) };
            foreach (var arr in new[] { body, crown, det, mys, swm })
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i] == null) { Debug.LogError("Дедушка: не создался материал #" + i + " (проверь шейдер TreeToon: Tools/BuilderSim/Лес/Tree Toon)."); return null; }

            var mB0 = ForestMats.SaveMesh(BuildBody(0, "Grandpa_Body_LOD0"), Dir + "/Grandpa_Body_LOD0.asset");
            var mB1 = ForestMats.SaveMesh(BuildBody(1, "Grandpa_Body_LOD1"), Dir + "/Grandpa_Body_LOD1.asset");
            var mC0 = ForestMats.SaveMesh(BuildCrown(0, "Grandpa_Crown_LOD0"), Dir + "/Grandpa_Crown_LOD0.asset");
            var mC1 = ForestMats.SaveMesh(BuildCrown(1, "Grandpa_Crown_LOD1"), Dir + "/Grandpa_Crown_LOD1.asset");
            var mMo = ForestMats.SaveMesh(BuildMotes("Grandpa_Motes"), Dir + "/Grandpa_Motes.asset");
            var mD0 = ForestMats.SaveMesh(BuildDetail("Grandpa_Detail"), Dir + "/Grandpa_Detail.asset");
            var mM0 = ForestMats.SaveMesh(BuildMystic(0, "Grandpa_Mystic_LOD0"), Dir + "/Grandpa_Mystic_LOD0.asset");
            var mM1 = ForestMats.SaveMesh(BuildMystic(1, "Grandpa_Mystic_LOD1"), Dir + "/Grandpa_Mystic_LOD1.asset");
            var mSw = ForestMats.SaveMesh(BuildSwing("Grandpa_Swing"), Dir + "/Grandpa_Swing.asset");

            var root = new GameObject("Landmark_Grandpa");
            try
            {
                var rBody0 = AddMesh(root.transform, "Body_LOD0", mB0, body, true);
                var rCrown0 = AddMesh(root.transform, "Crown_LOD0", mC0, crown, true);
                var rCrown1 = AddMesh(root.transform, "Crown_LOD1", mC1, crown, true);
                var rDet0 = AddMesh(root.transform, "Detail_LOD0", mD0, det, true);
                var rMys0 = AddMesh(root.transform, "Mystic_LOD0", mM0, mys, true);
                var rBody1 = AddMesh(root.transform, "Body_LOD1", mB1, body, true);
                var rMys1 = AddMesh(root.transform, "Mystic_LOD1", mM1, mys, true);

                var lg = root.AddComponent<LODGroup>();
                lg.fadeMode = LODFadeMode.None;
                lg.SetLODs(new[] { new LOD(0.08f, new Renderer[] { rBody0, rCrown0, rDet0, rMys0 }), new LOD(0.008f, new Renderer[] { rBody1, rCrown1, rMys1 }) });
                lg.RecalculateBounds();

                // качели
                var swing = new GameObject("Swing");
                swing.transform.SetParent(root.transform, false);
                swing.transform.localPosition = SwingAnchor() + Vector3.down * 0.12f;
                swing.transform.localRotation = Quaternion.Euler(0f, SwingAz, 0f);
                var mr = swing.AddComponent<MeshRenderer>();
                swing.AddComponent<MeshFilter>().sharedMesh = mSw;
                mr.sharedMaterials = swm;
                mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;

                // парящие кристаллы: отдельный объект, GrandpaVibe медленно крутит его вокруг ствола и качает вверх-вниз
                var motes = new GameObject("Motes");
                motes.transform.SetParent(root.transform, false);
                motes.AddComponent<MeshFilter>().sharedMesh = mMo;
                var mrMotes = motes.AddComponent<MeshRenderer>();
                mrMotes.sharedMaterials = moteMat;
                mrMotes.shadowCastingMode = ShadowCastingMode.Off; mrMotes.receiveShadows = false;

                // свет: свечка и бирюзовое сияние в дупле, 3 фонарика, гирлянда, мягкое свечение кристаллов
                AddFire(root.transform, "Light_Hollow", new Vector3(0.1f, 0.95f, 1.95f), FlickerPreset.Lantern, new Color(1f, 0.72f, 0.38f), 2.6f, 8f, false, 2.2f, null);
                AddFire(root.transform, "Light_HollowSpirit", new Vector3(0.1f, 2.2f, 2.35f), FlickerPreset.Garland, new Color(0.45f, 1f, 0.85f), 1.5f, 7f, false, 1.9f, null);
                AddFire(root.transform, "Light_Crystals", Vector3.zero, FlickerPreset.Garland, new Color(0.62f, 0.90f, 1f), 1f, 4f, true, 1.15f, CrystalSpots().ToArray());
                int li = 0;
                foreach (var sp in LanternSpots())
                    AddFire(root.transform, "Light_Lantern" + (li++), sp, FlickerPreset.Lantern, new Color(1f, 0.78f, 0.45f), 2.2f, 7f, false, 1.5f, null);
                AddFire(root.transform, "Light_Garland", Vector3.zero, FlickerPreset.Garland, new Color(1f, 0.86f, 0.58f), 1f, 5f, true, 0.55f, BulbSpots().ToArray());

                // сплошной ствол
                var col = root.AddComponent<CapsuleCollider>();
                col.direction = 1; col.radius = 2.1f; col.height = 9f; col.center = new Vector3(0.1f, 4.2f, 0.1f);
#if UNITY_6000_0_OR_NEWER
                var slip = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(SlipPath);
#else
                var slip = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(SlipPath);
#endif
                if (slip != null) col.sharedMaterial = slip;

                var lm = root.AddComponent<LandmarkTree>();
                lm.title = "Дедушка";
                lm.caption = "ДРЕВНИЙ ХРАНИТЕЛЬ";
                lm.radius = 7.5f;
                lm.promptHeight = 2.4f;

                var vibe = root.AddComponent<GrandpaVibe>();
                vibe.swing = swing.transform;
                vibe.crownHeight = 17f;
                vibe.glow = rMys0;                 // статичные кристаллы (сабмеш 1 «Mystic»)
                vibe.glowMaterialIndex = 1;
                vibe.motes = motes.transform;      // парящие кристаллы
                vibe.motesGlow = mrMotes;
                vibe.crownRadius = 10f;
                vibe.hollowLocal = new Vector3(0.1f, 1.8f, 2.3f);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return saved;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static MeshRenderer AddMesh(Transform parent, string name, Mesh mesh, Material[] mats, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            int subs = Mathf.Max(1, mesh.subMeshCount);
            var arr = new Material[subs];
            for (int i = 0; i < subs; i++) arr[i] = mats[Mathf.Min(i, mats.Length - 1)];
            mr.sharedMaterials = arr;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return mr;
        }

        static void AddFire(Transform parent, string name, Vector3 local, FlickerPreset preset, Color color, float intensity, float range, bool simOnly, float glowSize, Vector3[] glowPoints)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var fs = go.AddComponent<FireSource>();
            fs.preset = preset;
            fs.color = color;
            fs.intensity = intensity;
            fs.range = range;
            fs.simOnly = simOnly;
            fs.glow = true;
            fs.glowSize = glowSize;
            if (glowPoints != null) fs.glowPoints = glowPoints;
        }

        // ============================================================ сцена
        static string Place(GameObject prefab, Vector3 padPos)
        {
            var manual = GameObject.Find("IslandV2/_Manual");
            Transform parent = manual != null ? manual.transform : null;

            var old = parent != null ? parent.Find("Grandpa") : GameObject.Find("Grandpa") != null ? GameObject.Find("Grandpa").transform : null;
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(inst, "Дедушка");
            inst.name = "Grandpa";
            inst.transform.position = padPos;
            inst.transform.rotation = Quaternion.Euler(0f, FacingYaw(padPos), 0f);

            string blk = "коробки BLK_Grandpa в сцене нет";
            var box = GameObject.Find("BLK_Grandpa");
            if (box != null)
            {
                var mr = box.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
                var bc = box.GetComponent<BoxCollider>();
                if (bc != null) bc.size = new Vector3(TrunkR * 1.8f, bc.size.y, TrunkR * 1.8f);
                EditorUtility.SetDirty(box);
                blk = "серая коробка BLK_Grandpa спрятана, коллайдер сужен до ствола";
            }
            EditorSceneManager.MarkSceneDirty(inst.scene);
            Selection.activeGameObject = inst;
            return "Дедушка v4 Cloud Candy: древний хранитель " + Height.ToString("0") + " м в (" + padPos.x.ToString("0.#") + ", " + padPos.z.ToString("0.#") + "), разворот " + FacingYaw(padPos).ToString("0") +
                   "° (дупло смотрит на PAD_Forest), " + (parent != null ? "в IslandV2/_Manual" : "в корне сцены") + "; " + blk + ".\n" +
                   "Префаб: " + PrefabPath + ". Рубить нельзя. Дальше: «7. Поляна Дедушки» (камни-стражи, лавки, фонари, грибное кольцо).";
        }
    }
}
#endif
