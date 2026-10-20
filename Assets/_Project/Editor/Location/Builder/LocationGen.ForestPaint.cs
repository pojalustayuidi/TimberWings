#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Builder/LocationGen.ForestPaint.cs
// Тропы леса на Terrain: тот же способ, что у троп острова (поля расстояний -> слои Trail / TrailDark / Pad),
// поэтому лесные тропы выглядят как остальные: цвет, тёмная кромка, колеи, рваный край.
// Меши и маркеры делает ForestTrails.cs; здесь только перекраска alphamap существующего Terrain.
//
// Порядок: Gen по сохранённым настройкам Location Builder раскладывает площадки и тропы острова (детерминированно по seed),
// к ним добавляются тропы и поляны леса из ForestTrailPlan (метры мира -> внутренние единицы u), считаются поля, красится splat.
// Высота рельефа не меняется (локация и её объекты остаются на месте): при «Выразительном» рельефе она читается из Terrain только для раскраски.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class LocationBuilder
{
    // настройки так, как их сохранило окно Location Builder
    static Settings LoadSavedSettings()
    {
        var st = new Settings();
        string json = EditorPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, st); }
            catch (Exception) { st = new Settings(); }
        }
        st.Validate();
        return st;
    }

    // plan = null: убрать лесные тропы с Terrain (остаются только тропы острова).
    // true = Terrain перекрашен; false = не вышло, msg объясняет почему (тогда ForestTrails рисует тропы лентами поверх земли)
    internal static bool PaintForestTrails(ForestTrailPlan plan, out string msg)
    {
        msg = "";
        try
        {
            var rootGo = GameObject.Find(RootName);
            if (rootGo == null) { msg = "объект Location не найден: соберите локацию в Location Builder"; return false; }
            var terrain = rootGo.GetComponentInChildren<Terrain>(true);
            if (terrain == null || terrain.terrainData == null) { msg = "под Location нет Terrain"; return false; }

            var st = LoadSavedSettings();
            var td = terrain.terrainData;
            float S = st.terrainSize * st.worldScale;
            if (Mathf.Abs(td.size.x - S) > 0.5f)
            { msg = "размер Terrain (" + td.size.x.ToString("0.#") + " м) не совпадает с настройками Location Builder (" + S.ToString("0.#") + " м): пересоберите локацию"; return false; }
            if (td.alphamapResolution != st.alphaRes || td.alphamapLayers != Gen.LayerCountFor(st))
            { msg = "разрешение или слои alphamap Terrain не совпадают с настройками: пересоберите локацию"; return false; }

            var g = new Gen(st);
            Vector3 rp = rootGo.transform.position;
            string r = g.RepaintSplat(terrain, plan, new Vector2(rp.x, rp.z));
            if (r != null) { msg = r; return false; }
            msg = plan == null ? "лесные тропы убраны с Terrain" : "тропы и поляны леса прокрашены в Terrain";
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            msg = "ошибка перекраски Terrain, см. Console";
            return false;
        }
    }

    sealed partial class Gen
    {
        readonly List<Vector3> forestDirt = new List<Vector3>();    // x, z, радиус (u): утоптанная земля на поляне
        readonly List<Vector3> forestGrass = new List<Vector3>();   // x, z, радиус (u): светлая трава вокруг

        // null = всё хорошо, иначе текст ошибки
        internal string RepaintSplat(Terrain terrain, ForestTrailPlan plan, Vector2 org)
        {
            var td = terrain.terrainData;
            SolveLayout();
            if (forest == null) return "в раскладке Location Builder нет круглой площадки леса";
            BuildTrails();
            // «Выразительный» рельеф красит по уклону и гребням: высоты берём из готового Terrain (сам рельеф не пересчитываем)
            if (Expressive)
            {
                string he = LoadHeightsFromTerrain(terrain);
                if (he != null) return he;
            }
            int added = plan != null ? AddForestPlan(plan, org) : 0;
            ComputeFields();
            td.SetAlphamaps(0, 0, BuildSplat());
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            return null;
        }

        // метры мира -> u: u = (мир - origin локации) / worldScale
        int AddForestPlan(ForestTrailPlan plan, Vector2 org)
        {
            int n = 0;
            for (int k = 0; k < plan.paths.Count; k++)
            {
                var fp = plan.paths[k];
                if (fp.p.Count < 2) continue;
                var t = new Trail { name = "Trail_Forest_" + fp.name, target = forest };
                for (int i = 0; i < fp.p.Count; i++)
                {
                    Vector2 u = (fp.p[i] - org) / ws;
                    t.p.Add(u);
                    t.half.Add(Mathf.Max(0.3f, fp.half[i] / ws));
                }
                t.len = fp.len / ws;
                trails.Add(t);
                n++;
                if (fp.glade)
                {
                    Vector2 c = (fp.end - org) / ws;
                    float r = fp.endRadius / ws;
                    forestGrass.Add(new Vector3(c.x, c.y, r * 1.2f));
                    forestDirt.Add(new Vector3(c.x, c.y, r * (fp.main ? 0.75f : 0.55f)));
                }
            }
            return n;
        }

        // круг в поле расстояний: d = расстояние до центра - радиус (как у площадок)
        void StampDisc(float[] field, Vector3 disc, bool resetCenter)
        {
            float reach = disc.z + 3f;
            int i0 = Mathf.Max(0, Mathf.FloorToInt((disc.x - reach + half) / cell));
            int i1 = Mathf.Min(N - 1, Mathf.CeilToInt((disc.x + reach + half) / cell));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((disc.y - reach + half) / cell));
            int j1 = Mathf.Min(N - 1, Mathf.CeilToInt((disc.y + reach + half) / cell));
            for (int j = j0; j <= j1; j++)
            {
                float z = -half + j * cell;
                for (int i = i0; i <= i1; i++)
                {
                    float x = -half + i * cell;
                    float d = new Vector2(x - disc.x, z - disc.y).magnitude - disc.z;
                    int idx = j * N + i;
                    if (d < field[idx]) { field[idx] = d; if (resetCenter) tCenter[idx] = 999f; }   // 999: колей на пятне нет
                }
            }
        }

        void StampForestDiscs()
        {
            for (int i = 0; i < forestGrass.Count; i++) StampDisc(sdPad, forestGrass[i], false);
            for (int i = 0; i < forestDirt.Count; i++) StampDisc(sdTrail, forestDirt[i], true);
        }
    }
}
#endif
