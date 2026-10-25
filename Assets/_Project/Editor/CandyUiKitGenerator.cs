// Assets/_Project/Editor/CandyUiKitGenerator.cs   (ДОЛЖЕН лежать в папке Editor)
//
// Candy Kit v2 + Fun. ОСНОВНЫЕ спрайты (кнопки, панели, иконки, камни...) это готовые PNG из v2, они лежат в
//   Assets/_Project/Art/UI/CandyKit/   (те же имена и размеры, что были у старого процедурного набора).
// Этот файл только настраивает импорт: Sprite, PPU = 200, без mip-карт, Bilinear, 9-slice рамки по таблице ниже.
//
// FUN-спрайты (искры, конфетти, сердечко, свечение, кольцо, лента, облачко) в v2 не нарисованы, поэтому они по-прежнему
// создаются кодом, но уже в цветах v2. Их рисует CandyUiKitGenerator.Fun.cs (движок рисования: .Painter.cs),
// результат: CandyKit/Fun/*.png. Для них здесь тоже заданы настройки импорта.
//
// Меню:  Tools > BuilderSim > UI > Candy Kit > Настроить импорт спрайтов набора      (+ создаёт недостающие Fun-спрайты)
//        Tools > BuilderSim > UI > Candy Kit > Показать папку в Project
//        Tools > BuilderSim > UI > Candy Kit > Fun > 0. Пересоздать Fun-спрайты      (см. CandyUiKitGenerator.Fun.cs)
// Новые и обновлённые PNG в папке CandyKit настраиваются автоматически при импорте.
//
// Имя класса и константы OutDir / Q сохранены: CandyHudBuilder, CandyFunSetup и остальной код работают как раньше.
// Старая «Polish»-часть генератора (CandyUiKitGenerator.Polish.cs) УДАЛЕНА: её панели, камни и курсор заменены PNG из v2.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class CandyUiKitGenerator
{
    public const int Q = 2;
    public const string OutDir = "Assets/_Project/Art/UI/CandyKit";

    // относительный путь без расширения -> 9-slice рамка (left, bottom, right, top) в пикселях текстуры
    public static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>();
    static void T(string p, float l, float b, float r, float t) { Borders[p] = new Vector4(l, b, r, t); }

    static CandyUiKitGenerator()
    {
        T("Buttons/btn_yellow_normal", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_yellow_normal", 44f, 44f, 44f, 44f);
        T("Buttons/btn_yellow_hover", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_yellow_hover", 44f, 44f, 44f, 44f);
        T("Buttons/btn_yellow_pressed", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_yellow_pressed", 44f, 44f, 44f, 44f);
        T("Buttons/btn_yellow_disabled", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_yellow_disabled", 44f, 44f, 44f, 44f);
        T("Buttons/btn_mint_normal", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_mint_normal", 44f, 44f, 44f, 44f);
        T("Buttons/btn_mint_hover", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_mint_hover", 44f, 44f, 44f, 44f);
        T("Buttons/btn_mint_pressed", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_mint_pressed", 44f, 44f, 44f, 44f);
        T("Buttons/btn_mint_disabled", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_mint_disabled", 44f, 44f, 44f, 44f);
        T("Buttons/btn_blue_normal", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_blue_normal", 44f, 44f, 44f, 44f);
        T("Buttons/btn_blue_hover", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_blue_hover", 44f, 44f, 44f, 44f);
        T("Buttons/btn_blue_pressed", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_blue_pressed", 44f, 44f, 44f, 44f);
        T("Buttons/btn_blue_disabled", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_blue_disabled", 44f, 44f, 44f, 44f);
        T("Buttons/btn_coral_normal", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_coral_normal", 44f, 44f, 44f, 44f);
        T("Buttons/btn_coral_hover", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_coral_hover", 44f, 44f, 44f, 44f);
        T("Buttons/btn_coral_pressed", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_coral_pressed", 44f, 44f, 44f, 44f);
        T("Buttons/btn_coral_disabled", 60f, 60f, 60f, 60f);
        T("Buttons/Small/btn_small_coral_disabled", 44f, 44f, 44f, 44f);
        T("Buttons/btn_round_gear_normal", 0f, 0f, 0f, 0f);
        T("Buttons/btn_close_normal", 0f, 0f, 0f, 0f);
        T("Buttons/btn_round_gear_hover", 0f, 0f, 0f, 0f);
        T("Buttons/btn_close_hover", 0f, 0f, 0f, 0f);
        T("Buttons/btn_round_gear_pressed", 0f, 0f, 0f, 0f);
        T("Buttons/btn_close_pressed", 0f, 0f, 0f, 0f);
        T("Buttons/btn_round_gear_disabled", 0f, 0f, 0f, 0f);
        T("Buttons/btn_close_disabled", 0f, 0f, 0f, 0f);
        T("Panels/panel_window", 132f, 132f, 132f, 132f);
        T("Panels/panel_quest", 100f, 100f, 100f, 100f);
        T("Panels/panel_chip", 60f, 60f, 60f, 60f);
        T("Panels/panel_toast", 72f, 72f, 72f, 72f);
        T("Panels/sign_wood", 80f, 32f, 80f, 60f);
        T("Panels/bubble_speech", 100f, 100f, 100f, 100f);
        T("Panels/note_paper", 0f, 0f, 0f, 0f);
        T("Panels/row_setting", 44f, 44f, 44f, 44f);
        T("Controls/toggle_track_on", 42f, 42f, 42f, 42f);
        T("Controls/toggle_track_off", 42f, 42f, 42f, 42f);
        T("Controls/toggle_knob", 0f, 0f, 0f, 0f);
        T("Controls/slider_track", 22f, 22f, 22f, 22f);
        T("Controls/slider_fill", 22f, 22f, 22f, 22f);
        T("Controls/slider_handle", 0f, 0f, 0f, 0f);
        T("Controls/bar_frame", 26f, 26f, 26f, 26f);
        T("Controls/bar_fill_mint", 26f, 26f, 26f, 26f);
        T("Controls/bar_fill_yellow", 26f, 26f, 26f, 26f);
        T("Controls/bar_fill_blue", 26f, 26f, 26f, 26f);
        T("Controls/bar_fill_coral", 26f, 26f, 26f, 26f);
        T("Controls/xp_frame", 36f, 36f, 36f, 36f);
        T("Controls/xp_fill", 36f, 36f, 36f, 36f);
        T("Controls/lvl_plate", 38f, 38f, 38f, 38f);
        T("Tabs/tab_active", 48f, 20f, 48f, 48f);
        T("Tabs/tab_inactive", 48f, 20f, 48f, 48f);
        T("Tabs/dot_red", 0f, 0f, 0f, 0f);
        T("Tabs/badge_burst", 0f, 0f, 0f, 0f);
        T("Icons/icon_coin", 0f, 0f, 0f, 0f);
        T("Icons/icon_log", 0f, 0f, 0f, 0f);
        T("Icons/icon_planks", 0f, 0f, 0f, 0f);
        T("Icons/icon_axe", 0f, 0f, 0f, 0f);
        T("Icons/icon_boot", 0f, 0f, 0f, 0f);
        T("Icons/icon_backpack", 0f, 0f, 0f, 0f);
        T("Icons/icon_saw", 0f, 0f, 0f, 0f);
        T("Icons/icon_star", 0f, 0f, 0f, 0f);
        T("Icons/icon_trophy", 0f, 0f, 0f, 0f);
        T("Icons/icon_paw", 0f, 0f, 0f, 0f);
        T("Icons/icon_gift", 0f, 0f, 0f, 0f);
        T("Icons/icon_lock", 0f, 0f, 0f, 0f);
        T("Rarity/gem_common", 0f, 0f, 0f, 0f);
        T("Rarity/gem_uncommon", 0f, 0f, 0f, 0f);
        T("Rarity/gem_rare", 0f, 0f, 0f, 0f);
        T("Rarity/gem_legendary", 0f, 0f, 0f, 0f);
        T("Rarity/gem_mythic", 0f, 0f, 0f, 0f);
        T("Extra/spinner", 0f, 0f, 0f, 0f);
        T("Extra/cursor_hand", 0f, 0f, 0f, 0f);
        T("Extra/cedar_branch", 0f, 0f, 0f, 0f);
        T("Extra/wind", 0f, 0f, 0f, 0f);
        T("Icons/icon_check", 0f, 0f, 0f, 0f);
        T("Extra/icon_chevron", 0f, 0f, 0f, 0f);

        // Fun-спрайты (рисуются кодом, без 9-slice)
        T("Fun/fx_sparkle", 0f, 0f, 0f, 0f);
        T("Fun/fx_sparkle_small", 0f, 0f, 0f, 0f);
        T("Fun/fx_glow", 0f, 0f, 0f, 0f);
        T("Fun/fx_ring", 0f, 0f, 0f, 0f);
        T("Fun/fx_heart", 0f, 0f, 0f, 0f);
        T("Fun/fx_confetti_rect", 0f, 0f, 0f, 0f);
        T("Fun/fx_confetti_circle", 0f, 0f, 0f, 0f);
        T("Fun/fx_confetti_tri", 0f, 0f, 0f, 0f);
        T("Fun/ribbon_banner", 0f, 0f, 0f, 0f);
        T("Fun/cloud_puff", 0f, 0f, 0f, 0f);
    }

    [MenuItem("Tools/BuilderSim/UI/Candy Kit/Настроить импорт спрайтов набора", false, 200)]
    public static void GenerateAll()
    {
        EnsureFunSprites(false);   // создаёт только то, чего ещё нет в CandyKit/Fun

        int n = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var kv in Borders)
            {
                string path = OutDir + "/" + kv.Key + ".png";
                if (AssetImporter.GetAtPath(path) == null) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                n++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        Debug.Log("[CandyKit] Настройки импорта применены к " + n + " спрайтам в " + OutDir);
    }

    [MenuItem("Tools/BuilderSim/UI/Candy Kit/Показать папку в Project", false, 201)]
    public static void PingFolder()
    {
        var folder = AssetDatabase.LoadAssetAtPath<Object>(OutDir);
        if (folder == null) { Debug.LogWarning("[CandyKit] Папки ещё нет: скопируйте в неё PNG набора."); return; }
        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }
}

public class CandyKitImportPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string prefix = CandyUiKitGenerator.OutDir + "/";
        if (!assetPath.StartsWith(prefix) || !assetPath.EndsWith(".png")) return;
        string key = assetPath.Substring(prefix.Length, assetPath.Length - prefix.Length - 4);
        Vector4 border;
        if (!CandyUiKitGenerator.Borders.TryGetValue(key, out border)) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f * CandyUiKitGenerator.Q;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.isReadable = false;
        ti.spriteBorder = border;
    }
}
