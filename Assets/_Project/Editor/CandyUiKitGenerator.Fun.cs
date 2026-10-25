// Assets/_Project/Editor/CandyUiKitGenerator.Fun.cs   (ДОЛЖЕН лежать в папке Editor, рядом с CandyUiKitGenerator.cs)
//
// «Fun-набор» Candy Kit: декоративные спрайты для эффектов и украшений. В готовых PNG v2 их нет, поэтому рисуются кодом
// (движок: CandyUiKitGenerator.Painter.cs), но уже в палитре v2: тёмно-фиолетовый контур, коралловый и кремовый.
// Результат ложится в CandyKit/Fun/ и подхватывается CandyFxKit (меню Candy Kit > Fun > 1. Собрать CandyFxKit).
//
//   Fun/fx_sparkle         искорка-звезда (белая, красится через Image.Color)
//   Fun/fx_sparkle_small   маленькая искорка
//   Fun/fx_glow            мягкое свечение (подложка под иконки, вспышки)
//   Fun/fx_ring            кольцо-волна (расходится от нажатия)
//   Fun/fx_heart           сердечко
//   Fun/fx_confetti_rect | fx_confetti_circle | fx_confetti_tri   конфетти (белые, красятся в рантайме)
//   Fun/ribbon_banner      лента-баннер («Уровень 4!», «Отличная работа!»), текст кладётся поверх через TMP
//   Fun/cloud_puff         пухлое облачко для украшения окон и переходов
//
// Запуск:  Tools > BuilderSim > UI > Candy Kit > Fun > 0. Пересоздать Fun-спрайты   (перезапишет все 10 файлов)
// Автоматически: «Настроить импорт спрайтов набора» и «1. Собрать CandyFxKit» создают только недостающие файлы.
// Хотите другой цвет или форму: правьте Ink и вызовы p.Fill(...) ниже и запустите пункт 0.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Sdf = System.Func<float, float, float>;
using Shade = System.Func<float, float, UnityEngine.Color>;

public static partial class CandyUiKitGenerator
{
    // цвет контура как у PNG v2 (у кнопок и панелей набора контур #3A2D63)
    const string Ink = "#3A2D63";

    sealed class Spec
    {
        public string path; public int w, h; public Action<Painter> draw;
    }
    static readonly List<Spec> specs = new List<Spec>();

    static void Add(string path, int w, int h, Action<Painter> draw)
    {
        specs.Add(new Spec { path = path, w = w, h = h, draw = draw });
    }

    // ======================================================================================
    //  МЕНЮ
    // ======================================================================================
    [MenuItem("Tools/BuilderSim/UI/Candy Kit/Fun/0. Пересоздать Fun-спрайты", false, 299)]
    static void RegenerateFunMenu()
    {
        int n = EnsureFunSprites(true);
        Debug.Log("[CandyKit] Fun-спрайтов записано: " + n + " (" + OutDir + "/Fun)");
        var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(OutDir + "/Fun");
        if (folder != null) { Selection.activeObject = folder; EditorGUIUtility.PingObject(folder); }
    }

    // force = false: пишет только отсутствующие файлы; true: перезаписывает все. Возвращает, сколько файлов записано.
    public static int EnsureFunSprites(bool force)
    {
        specs.Clear();
        DefineFunSprites();

        var todo = new List<Spec>();
        foreach (var s in specs)
            if (force || !File.Exists(Path.GetFullPath(OutDir + "/" + s.path + ".png"))) todo.Add(s);
        if (todo.Count == 0) return 0;

        int done = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < todo.Count; i++)
            {
                var s = todo[i];
                if (EditorUtility.DisplayCancelableProgressBar("Candy Kit: Fun", s.path, (float)i / todo.Count)) break;
                var p = new Painter(s.w, s.h);
                s.draw(p);
                p.Bleed();
                WritePng(OutDir + "/" + s.path + ".png", p);
                done++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }
        AssetDatabase.Refresh();
        return done;
    }

    static void WritePng(string assetPath, Painter p)
    {
        string full = Path.GetFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        var tex = new Texture2D(p.W, p.H, TextureFormat.RGBA32, false);
        tex.SetPixels(p.px);
        tex.Apply(false, false);
        File.WriteAllBytes(full, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        // настройки Sprite ставит CandyKitImportPostprocessor по таблице Borders (ключи Fun/* там заданы)
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    // ======================================================================================
    //  СПРАЙТЫ
    // ======================================================================================
    // четырёхлучевая звезда: R длина луча, r «талия» между лучами
    static Sdf Star4(float cx, float cy, float R, float r)
    {
        var v = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            float rad = (i % 2 == 0) ? R : r;
            v[i] = new Vector2(cx + Mathf.Cos(a) * rad, cy + Mathf.Sin(a) * rad);
        }
        return Poly(v);
    }

    static void DefineFunSprites()
    {
        // ---------------------------------------------------------------- искорки
        Add("Fun/fx_sparkle", 64, 64, p =>
        {
            var s = Star4(32f, 32f, 27f, 6f);
            p.Fill(Grow(s, 3f), Solid("#FFE08A", 0.55f));
            p.Fill(s, Solid("#FFFFFF"));
            p.Fill(Circle(32f, 32f, 5f), Solid("#FFFFFF"));
        });
        Add("Fun/fx_sparkle_small", 32, 32, p =>
        {
            var s = Star4(16f, 16f, 13f, 3.2f);
            p.Fill(Grow(s, 1.6f), Solid("#FFE08A", 0.55f));
            p.Fill(s, Solid("#FFFFFF"));
        });

        // ---------------------------------------------------------------- свечение и кольцо
        Add("Fun/fx_glow", 128, 128, p =>
        {
            Shade glow = (x, y) =>
            {
                float d = Mathf.Clamp01(Mathf.Sqrt((x - 64f) * (x - 64f) + (y - 64f) * (y - 64f)) / 64f);
                float a = (1f - d) * (1f - d) * 0.95f;
                return new Color(1f, 1f, 1f, a);
            };
            p.Fill(Circle(64f, 64f, 64f), glow);
        });
        Add("Fun/fx_ring", 128, 128, p =>
        {
            p.Fill(Ring(Circle(64f, 64f, 60f), 7f), Solid("#FFFFFF", 0.95f));
        });

        // ---------------------------------------------------------------- сердечко (коралл v2)
        Add("Fun/fx_heart", 64, 64, p =>
        {
            var heart = Union(Circle(22f, 42f, 13f), Circle(42f, 42f, 13f),
                              Poly(V(10.6f, 35.5f), V(53.4f, 35.5f), V(32f, 10f)));
            p.Fill(Grow(heart, 2.4f), Solid(Ink));
            p.Fill(heart, VGrad(10f, 56f, "#E5576B", "#FF8C93"));
            p.Fill(Ellipse(17f, 46f, 5f, 3.2f), Solid("#FFFFFF", 0.8f));
        });

        // ---------------------------------------------------------------- конфетти (белые, красятся в игре)
        Add("Fun/fx_confetti_rect", 24, 14, p =>
        {
            p.Fill(RR(1f, 1f, 22f, 12f, 3f), Solid("#FFFFFF"));
            p.Fill(RR(1f, 1f, 22f, 5f, 2.5f), Solid("#000000", 0.12f));
        });
        Add("Fun/fx_confetti_circle", 16, 16, p =>
        {
            p.Fill(Circle(8f, 8f, 7f), Solid("#FFFFFF"));
            p.Fill(Circle(8f, 6f, 5f), Solid("#000000", 0.1f));
        });
        Add("Fun/fx_confetti_tri", 20, 20, p =>
        {
            p.Fill(Poly(V(10f, 18f), V(1.5f, 3f), V(18.5f, 3f)), Solid("#FFFFFF"));
        });

        // ---------------------------------------------------------------- лента-баннер (текст поверх делается TMP)
        Add("Fun/ribbon_banner", 220, 64, p =>
        {
            var tailL = Poly(V(2f, 10f), V(44f, 10f), V(44f, 44f), V(2f, 44f), V(14f, 27f));
            var tailR = Poly(V(218f, 10f), V(176f, 10f), V(176f, 44f), V(218f, 44f), V(206f, 27f));
            p.Fill(Grow(tailL, 2.2f), Solid(Ink));
            p.Fill(tailL, VGrad(10f, 44f, "#C44466", "#E5576B"));
            p.Fill(Grow(tailR, 2.2f), Solid(Ink));
            p.Fill(tailR, VGrad(10f, 44f, "#C44466", "#E5576B"));
            var plate = RR(20f, 16f, 180f, 40f, 7f);
            p.Fill(Grow(plate, 2.4f), Solid(Ink));
            p.Fill(plate, VGrad(16f, 56f, "#FD6A73", "#FF9AA0"));
            p.Fill(RR(30f, 44f, 160f, 5f, 2.5f), Solid("#FFFFFF", 0.5f));
        });

        // ---------------------------------------------------------------- облачко (в тон голубой кромке панелей)
        Add("Fun/cloud_puff", 160, 96, p =>
        {
            var cl = Union(Circle(42f, 40f, 26f), Circle(82f, 52f, 34f), Circle(120f, 42f, 26f), RR(32f, 14f, 100f, 30f, 15f));
            p.Fill(Grow(cl, 2.4f), Solid(Ink));
            p.Fill(cl, VGrad(14f, 86f, "#BFE0FB", "#FFFFFF"));
            p.Fill(Ellipse(60f, 66f, 14f, 5f), Solid("#FFFFFF", 0.85f));
        });
    }
}
