#if UNITY_EDITOR
// TimberWings: journal main menu installer, v1.9 FINAL POLISH. Additive, idempotent and reversible.
//
// * Updates an EXISTING scene in place: it never rebuilds what is already there and never touches
//   scripts / save logic (MenuFlow, SaveSystem, SaveSummary, SettingsMenu, GameAudio stay as they are).
// * Manual settings are kept: every object whose layout the installer manages carries a tiny
//   TWJournalLayoutStamp. If the rect still equals the stamp (or the known v1.9.x defaults) it is re-laid out;
//   if you moved / resized it by hand it is left alone and listed in the report.
//   "Сбросить раскладку" ignores the stamps and applies the new layout to every managed object
//   (references, texts, colours, sounds and your own extra objects are still kept).
// * A timestamped backup of MainMenu.unity is created before every run.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TWJournalMenuInstaller
{
    const string MenuScene = "Assets/_Project/Scenes/MainMenu.unity";
    const string AssetRoot = "Assets/_Project/UI/JournalMenu/";
    const string Art = AssetRoot + "Art/";
    const string PrefabPath = AssetRoot + "Prefabs/TWJournalMainMenu.prefab";
    const string RootName = "[TW] Journal Main Menu";
    const int StampRevision = 194;
    static TMP_FontAsset font;

    // ---------------------------------------------------------------- layout numbers (book-local units, book = 1600x900)
    // NOTE: plain "public const float" lines are parsed by the external layout checker; keep this format.
    public static class Spec
    {
        // Left page: one VerticalLayoutGroup column, centred in the page between the page edge and the spine.
        public const float ColumnX = -312f;
        public const float ColumnY = 30f;
        public const float ColumnW = 490f;
        public const float ColumnH = 420f;
        public const float ButtonVisibleW = 464f;   // visible width of every button (identical left/right edges)
        public const float ButtonSpacing = 22f;
        public const float IconX = -168f;           // identical icon column for all buttons
        public const float LabelX = 10f;            // identical text axis for all buttons
        // Right page safe area (guard rect for the animated illustrations).
        public const float SafeCX = 329f;
        public const float SafeCY = 34f;
        public const float SafeW = 498f;
        public const float SafeH = 584f;
        // Version badge: bottom-right corner of the SCREEN (canvas units), not of the book page.
        public const float BadgeW = 184f;
        public const float BadgeH = 63f;
        public const float BadgeMargin = 22f;
        public const float BadgeTextX = -9f;
        public const float BadgeTextY = -6f;
    }

    sealed class StickerSpec
    {
        public readonly string name, sprite, hint;
        public readonly float x, y, w, tilt, hoverAngle, lift;
        public StickerSpec(string name, string sprite, float x, float y, float w, float tilt, float hoverAngle, float lift, string hint)
        { this.name = name; this.sprite = sprite; this.x = x; this.y = y; this.w = w; this.tilt = tilt; this.hoverAngle = hoverAngle; this.lift = lift; this.hint = hint; }
    }

    // name, sprite, x, y, width (height follows the sprite aspect, no distortion), static tilt, hover angle, hover lift, story hint.
    // Composition: hero cedar top-left, vertical rhythm of small "pasted" items on the right,
    // Gunnar + note in the middle row, broken bridge at the bottom. All validated against the real page shape.
    static readonly StickerSpec[] Stickers =
    {
        new StickerSpec("Cedar_Island_StorySketch", "09_ApprovedFinal/cedar_mystic_approved", 262f, 188f, 318f, -1f, 1.0f, 5f, "Мистический кедр. Кто оставил на нём этот знак?"),
        new StickerSpec("Portal_Parchment", "09_ApprovedFinal/portal_approved", 484f, 222f, 112f, 2f, -1.0f, 5f, "Портал? Возможно, путь ко второй главе..."),
        new StickerSpec("Rune_Polaroid", "09_ApprovedFinal/rune_photo_approved", 492f, 88f, 124f, 3f, 0.8f, 5f, "Эта руна точно связана с ночным лесом."),
        new StickerSpec("Gunnar_Portrait", "09_ApprovedFinal/gunnar_note_approved", 170f, 8f, 140f, -2f, -1.3f, 5f, "Гуннар... Что он знает об этом острове?"),
        new StickerSpec("Mystery_Note", "09_ApprovedFinal/mystery_note_approved", 318f, 18f, 98f, 3f, 0.8f, 5f, "Здесь что-то не так..."),
        new StickerSpec("Map_Clue", "09_ApprovedFinal/map_clue_approved", 490f, -58f, 128f, -2f, -0.5f, 5f, "На карте кто-то отметил неизвестное место."),
        new StickerSpec("Broken_Bridge", "09_ApprovedFinal/broken_bridge_approved", 262f, -170f, 306f, 0f, 0.5f, 5f, "Сломанный мост ещё можно восстановить."),
        new StickerSpec("Rune_Shard", "09_ApprovedFinal/rune_shard_approved", 496f, -178f, 50f, 6f, 1.2f, 5f, "Одна из старых рун. Почему она светится?"),
    };
    static readonly float[] StickerParallax = { 1.0f, .6f, .8f, 1.2f, .9f, .7f, 1.1f, .5f };

    // Known v1.9.3 defaults (name -> x, y, w, h). Lets the first update recognise "untouched" objects without stamps.
    static readonly Dictionary<string, Vector4> LegacySticker = new Dictionary<string, Vector4>
    {
        { "Cedar_Island_StorySketch", new Vector4(264f, 167f, 413f, 225f) },
        { "Portal_Parchment", new Vector4(536f, 241f, 157f, 112f) },
        { "Rune_Shard", new Vector4(93f, -8f, 79f, 109f) },
        { "Gunnar_Portrait", new Vector4(225f, -94f, 182f, 170f) },
        { "Mystery_Note", new Vector4(386f, -83f, 120f, 103f) },
        { "Rune_Polaroid", new Vector4(536f, -22f, 160f, 166f) },
        { "Map_Clue", new Vector4(536f, -166f, 167f, 100f) },
        { "Broken_Bridge", new Vector4(267f, -238f, 350f, 123f) },
    };

    static readonly string[] LoadingSketchSprites =
    {
        "09_ApprovedFinal/cedar_mystic_approved", "09_ApprovedFinal/broken_bridge_approved",
        "09_ApprovedFinal/rune_photo_approved", "09_ApprovedFinal/map_clue_approved",
        "09_ApprovedFinal/gunnar_note_approved", "09_ApprovedFinal/portal_approved",
    };
    static readonly string[] LoadingSketchCaptions =
    {
        "Мистический кедр. Он будто дышит.",
        "Мост через ущелье. Починю при случае.",
        "Эта руна светилась ночью.",
        "Крестик на карте ставил не я.",
        "Гуннар знает больше, чем говорит.",
        "Портал... или дверь во вторую главу?",
    };

    // ================================================================ menu commands

    [MenuItem("Tools/BuilderSim/UI/Главное меню/Дневник: финальная доработка (обновить сцену)", false, 950)]
    public static void Install() { Run(false); }

    [MenuItem("Tools/BuilderSim/UI/Главное меню/Дневник: финальная доработка (сбросить раскладку)", false, 951)]
    public static void InstallResetLayout()
    {
        if (!EditorUtility.DisplayDialog("TimberWings",
                "Новая раскладка будет применена ко ВСЕМ управляемым объектам (кнопки, иллюстрации, бейдж версии), " +
                "включая те, что ты сдвигал вручную.\n\nСсылки, тексты, цвета, звуки и твои собственные объекты сохранятся. " +
                "Перед этим создаётся резервная копия сцены.", "Сбросить раскладку", "Отмена")) return;
        Run(true);
    }

    [MenuItem("Tools/BuilderSim/UI/Главное меню/Дневник: вернуть старое меню", false, 952)]
    public static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(MenuScene)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        var root = GameObject.Find(RootName);
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        foreach (var legacy in scene.GetRootGameObjects())
        {
            if (legacy.name == RootName) continue;
            if (legacy.GetComponentInChildren<MainMenuStub>(true) == null) continue;
            legacy.SetActive(true);
            foreach (var c in legacy.GetComponentsInChildren<MainMenuStub>(true)) c.enabled = true;
            foreach (var c in legacy.GetComponentsInChildren<MainMenuBackground>(true)) c.enabled = true;
            foreach (var c in legacy.GetComponentsInChildren<MainMenuLife>(true)) c.enabled = true;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("JournalMenu: previous UI enabled again; the generated art and prefab have been left in Assets for safety.");
    }

    [MenuItem("Tools/BuilderSim/UI/Главное меню/Дневник: проверить сцену", false, 953)]
    public static void AuditScene()
    {
        if (SceneManager.GetActiveScene().path != MenuScene)
        { Debug.LogWarning("JournalMenu audit: сначала открой " + MenuScene); return; }
        var root = GameObject.Find(RootName);
        if (root == null) { Debug.LogError("JournalMenu audit: корневой объект дневника не найден."); return; }
        var menu = root.GetComponent<TWJournalMenuController>();
        if (menu == null) { Debug.LogError("JournalMenu audit: нет TWJournalMenuController."); return; }

        var problems = new List<string>();
        var notes = new List<string>();
        if (menu.continueButton == null) problems.Add("не назначена кнопка Продолжить");
        if (menu.newGameButton == null) problems.Add("не назначена кнопка Новая игра");
        if (menu.settingsButton == null) problems.Add("не назначена кнопка Настройки");
        if (menu.exitButton == null) problems.Add("не назначена кнопка Выйти");
        if (menu.continueSubtitle == null) problems.Add("нет строки 'Уровень · День' внутри Продолжить");
        if (menu.confirmation == null) problems.Add("нет окна подтверждения Новой игры");
        if (menu.bookRoot == null) problems.Add("нет книги (bookRoot)");
        if (menu.layoutRoot == null) problems.Add("нет layoutRoot");
        if (menu.hoverClip == null || menu.clickClip == null || menu.loreClip == null) problems.Add("не назначены UI-звуки hover/click/lore");
        if (menu.pageClip == null || menu.readyClip == null) problems.Add("не назначены звуки загрузки (page/ready)");
        if (menu.loadingSketches == null || menu.loadingSketches.Length == 0) problems.Add("пуст список зарисовок экрана загрузки");
        if (menu.loadingLeaf == null) problems.Add("не назначен листик для экрана загрузки");

        // Canvas conflicts
        if (menu.canvasRect != null)
        {
            var cv = menu.canvasRect.GetComponent<Canvas>();
            if (cv == null) problems.Add("у Canvas_Journal нет компонента Canvas");
            else if (cv.sortingOrder >= UiLayers.Settings)
                problems.Add("Canvas_Journal.sortingOrder=" + cv.sortingOrder + " >= UiLayers.Settings (" + UiLayers.Settings + "): окно настроек окажется ПОД меню");
            else notes.Add("sorting меню " + cv.sortingOrder + " < Settings " + UiLayers.Settings + " < MenuFlow fade/загрузка");
        }

        // Version badge: code-driven TMP, bottom-right of the screen
        if (menu.versionLabel == null) problems.Add("нет TMP-метки версии");
        else
        {
            if (!menu.versionLabel.gameObject.activeInHierarchy) problems.Add("метка версии выключена");
            if (!menu.versionLabel.enabled) problems.Add("компонент TMP метки версии отключён");
            if (menu.canvasRect != null && !menu.versionLabel.transform.IsChildOf(menu.canvasRect)) problems.Add("метка версии вне Canvas");
            if (menu.bookRoot != null && menu.versionLabel.transform.IsChildOf(menu.bookRoot)) problems.Add("метка версии всё ещё внутри книги (должна быть в углу экрана)");
        }
        if (menu.versionBadge == null) problems.Add("не назначен versionBadge");
        else
        {
            var r = menu.versionBadge;
            if (r.anchorMin != Vector2.right || r.anchorMax != Vector2.right) problems.Add("бейдж версии не привязан к правому нижнему углу Canvas");
            if (menu.bookRoot != null && r.IsChildOf(menu.bookRoot)) problems.Add("бейдж версии внутри книги");
        }
        if (font == null) font = FindCyrillicFont();
        if (font == null) problems.Add("не найден TMP-шрифт с кириллицей");

        // Left column
        if (menu.leftPageRoot == null) problems.Add("не назначен leftPageRoot");
        else if (menu.leftPageRoot.GetComponent<VerticalLayoutGroup>() == null)
            notes.Add("у колонки кнопок нет VerticalLayoutGroup (ручная раскладка?)");
        else notes.Add("кнопки: VerticalLayoutGroup, равные интервалы");

        // Right page: every illustration inside the safe area (resting pose)
        var safe = menu.bookRoot != null ? menu.bookRoot.Find("RightPage_SafeArea") as RectTransform : null;
        if (safe == null) problems.Add("нет RightPage_SafeArea");
        else if (menu.rightPageRoot != null)
        {
            var corners = new Vector3[4];
            Rect lim = safe.rect;
            lim.xMin -= 1f; lim.yMin -= 1f; lim.xMax += 1f; lim.yMax += 1f;
            int count = 0;
            foreach (var egg in menu.rightPageRoot.GetComponentsInChildren<TWJournalMenuEasterEgg>(true))
            {
                count++;
                var rt = (RectTransform)egg.transform;
                rt.GetWorldCorners(corners);
                bool inside = true;
                for (int i = 0; i < 4; i++) { var p = safe.InverseTransformPoint(corners[i]); if (!lim.Contains(new Vector2(p.x, p.y))) inside = false; }
                if (!inside) problems.Add("'" + egg.name + "' выходит за безопасную область правой страницы");
                if (egg.pageBounds == null) problems.Add("'" + egg.name + "': не назначен pageBounds (защита от выхода при hover)");
                if (egg.menu == null) problems.Add("'" + egg.name + "': не назначен menu");
            }
            notes.Add("иллюстраций на странице: " + count);
        }

        foreach (var legacy in SceneManager.GetActiveScene().GetRootGameObjects())
            if (legacy.name != RootName && legacy.GetComponentInChildren<MainMenuStub>(true) != null && legacy.activeSelf)
                problems.Add("старое меню всё ещё активно (конфликт Canvas)");

        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy).ToArray();
        foreach (var a in canvases)
            foreach (var b in canvases)
                if (a.GetInstanceID() < b.GetInstanceID() && a.sortingOrder == b.sortingOrder && a.sortingLayerID == b.sortingLayerID)
                    notes.Add("одинаковый sortingOrder " + a.sortingOrder + ": '" + a.name + "' и '" + b.name + "'");

        if (problems.Count > 0)
            Debug.LogError("JournalMenu audit: найдены проблемы:\n - " + string.Join("\n - ", problems) + (notes.Count > 0 ? "\nЗаметки:\n - " + string.Join("\n - ", notes) : ""));
        else
            Debug.Log("JournalMenu audit: OK.\n - " + string.Join("\n - ", notes) + "\nПроверь Play Mode по чек-листу.");
    }

    // ================================================================ main flow

    static void Run(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("JournalMenu: останови Play Mode перед установкой."); return; }
        if (!File.Exists(MenuScene))
        { EditorUtility.DisplayDialog("TimberWings", "Сцена MainMenu не найдена. Сначала создай её штатным MainMenuSceneBuilder.", "OK"); return; }
        string[] required =
        {
            "02_Book/book_open_blank.png", "08_Polish/version_badge_v19_blank.png", "09_ApprovedFinal/cedar_mystic_approved.png",
            "08_Polish/Sounds/journal_page.wav", "08_Polish/Sounds/journal_ready.wav",
        };
        foreach (var r in required)
            if (!File.Exists(Art + r))
            { EditorUtility.DisplayDialog("TimberWings", "Не найден файл " + Art + r + "\nСкопируй из архива все папки Assets/_Project/… поверх проекта.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        AssetDatabase.Refresh();
        ImportSprites();
        font = FindCyrillicFont();
        if (font == null) Debug.LogWarning("JournalMenu: не найден TMP_FontAsset с кириллицей. Назначь шрифт с русскими буквами текстовым компонентам.");

        BackupScene();
        var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        GameObject root = null;
        foreach (var o in scene.GetRootGameObjects()) if (o.name == RootName) { root = o; break; }
        bool fresh = root == null;
        if (fresh) root = new GameObject(RootName);
        var menu = root.GetComponent<TWJournalMenuController>();
        if (menu == null) menu = root.AddComponent<TWJournalMenuController>();

        var report = new Report { force = force };
        DisableOldUI();
        Ensure(root, menu, report);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = root;

        var sb = new StringBuilder();
        sb.AppendLine(fresh ? "Дневник установлен в MainMenu." : "Сцена обновлена без пересборки.");
        sb.AppendLine("Применена новая раскладка: " + report.applied.Count + " объектов.");
        if (report.kept.Count > 0)
        {
            sb.AppendLine("Ручные настройки СОХРАНЕНЫ (" + report.kept.Count + "):");
            foreach (var k in report.kept.Take(10)) sb.AppendLine("  • " + k);
            if (report.kept.Count > 10) sb.AppendLine("  … и ещё " + (report.kept.Count - 10));
            sb.AppendLine("Чтобы применить новую раскладку и к ним: «Дневник: финальная доработка (сбросить раскладку)».");
        }
        foreach (var n in report.notes) sb.AppendLine("— " + n);
        sb.AppendLine("\nДальше: «Дневник: проверить сцену», затем Play Mode по чек-листу.");
        Debug.Log("TimberWings Journal v1.9 final polish:\n" + sb);
        EditorUtility.DisplayDialog("TimberWings", sb.ToString(), "OK");
    }

    sealed class Report
    {
        public bool force;
        public readonly List<string> applied = new List<string>();
        public readonly List<string> kept = new List<string>();
        public readonly List<string> notes = new List<string>();
    }

    static void Ensure(GameObject root, TWJournalMenuController menu, Report rep)
    {
        WireAssets(menu);
        EnsureCanvasSkeleton(root, menu);
        EnsureLeftPage(menu, rep);
        EnsureRightPage(menu, rep);
        EnsureDecor(menu);
        EnsureHintAndConfirmation(menu);
        EnsureVersionBadge(menu, rep);

        // Final ordering inside the canvas: background, layout, badge, confirmation (on top).
        menu.canvasRect.Find("Background_Blurred_Fallback").SetAsFirstSibling();
        menu.layoutRoot.SetSiblingIndex(1);
        if (menu.versionBadge != null) menu.versionBadge.SetAsLastSibling();
        if (menu.confirmation != null) menu.confirmation.transform.SetAsLastSibling();
        EditorUtility.SetDirty(menu);
    }

    // ================================================================ assets & references

    static void WireAssets(TWJournalMenuController menu)
    {
        if (string.IsNullOrEmpty(menu.menuVersion)) menu.menuVersion = "v1.9";   // user-facing version; kept if you changed it
        if (menu.hoverClip == null) menu.hoverClip = Clip("journal_hover");
        if (menu.clickClip == null) menu.clickClip = Clip("journal_click");
        if (menu.loreClip == null) menu.loreClip = Clip("journal_lore");
        if (menu.pageClip == null) menu.pageClip = Clip("journal_page");
        if (menu.readyClip == null) menu.readyClip = Clip("journal_ready");
        if (menu.loadingLeaf == null) menu.loadingLeaf = S("06_Decor/leaf_green");
        if (menu.loadingSketches == null || menu.loadingSketches.Length == 0)
            menu.loadingSketches = LoadingSketchSprites.Select(p => S(p)).ToArray();
        if (menu.loadingSketchCaptions == null || menu.loadingSketchCaptions.Length == 0)
            menu.loadingSketchCaptions = (string[])LoadingSketchCaptions.Clone();
        // legacy fallback-screen sprites
        if (menu.loadingBackground == null) menu.loadingBackground = S("07_Backgrounds/forest_camp_blurred_fallback");
        if (menu.loadingBook == null) menu.loadingBook = S("02_Book/book_open_blank");
        if (menu.loadingLogo == null) menu.loadingLogo = S("01_Logo/timberwings_logo_approved");
        if (menu.loadingCedar == null) menu.loadingCedar = S("09_ApprovedFinal/cedar_mystic_approved");
        if (menu.loadingBridge == null) menu.loadingBridge = S("09_ApprovedFinal/broken_bridge_approved");
        if (menu.loadingPortal == null) menu.loadingPortal = S("09_ApprovedFinal/portal_approved");
    }

    // ================================================================ canvas skeleton

    static void EnsureCanvasSkeleton(GameObject root, TWJournalMenuController menu)
    {
        Transform cgT = root.transform.Find("Canvas_Journal");
        bool created = cgT == null;
        GameObject cg = created ? UI("Canvas_Journal", root.transform) : cgT.gameObject;
        var canvas = cg.GetComponent<Canvas>(); if (canvas == null) canvas = cg.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Canvas conflict fix: Settings (1000) must draw above the menu. MenuFlow fade (5000) / loading (32000) stay above both.
        canvas.sortingOrder = UiLayers.Settings - 1;
        if (cg.GetComponent<GraphicRaycaster>() == null) cg.AddComponent<GraphicRaycaster>();
        var scaler = cg.GetComponent<CanvasScaler>(); if (scaler == null) scaler = cg.AddComponent<CanvasScaler>();
        if (created)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            Fill((RectTransform)cg.transform);
        }
        menu.canvasRect = (RectTransform)cg.transform;

        // Background
        Transform bgT = menu.canvasRect.Find("Background_Blurred_Fallback");
        if (bgT == null)
        {
            var backdrop = AddImage(menu.canvasRect, "Background_Blurred_Fallback", S("07_Backgrounds/forest_camp_blurred_fallback"), 0, 0, 1920, 1080);
            bgT = backdrop.transform;
            Fill((RectTransform)bgT);
            var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;   // cover, never stretch
            fitter.aspectRatio = 16f / 9f;
        }
        menu.backgroundRect = (RectTransform)bgT;

        // Layout root + book
        Transform layT = menu.canvasRect.Find("Layout_1920x1080");
        if (layT == null)
        {
            var lay = UI("Layout_1920x1080", menu.canvasRect);
            Place((RectTransform)lay.transform, 0, 0, 1920, 1080);
            lay.AddComponent<CanvasGroup>();
            layT = lay.transform;
        }
        menu.layoutRoot = (RectTransform)layT;
        menu.layoutGroup = layT.GetComponent<CanvasGroup>() ?? layT.gameObject.AddComponent<CanvasGroup>();

        Transform bookT = menu.layoutRoot.Find("Book_Open_Blank");
        if (bookT == null)
        {
            var book = AddImage(menu.layoutRoot, "Book_Open_Blank", S("02_Book/book_open_blank"), 0, -25, 1600, 900);
            book.transform.localScale = Vector3.one * .86f;
            bookT = book.transform;
        }
        menu.bookRoot = (RectTransform)bookT;

        // Logo (approved, unchanged) is created only if missing.
        if (menu.layoutRoot.Find("Logo_APPROVED_UNCHANGED") == null)
        {
            var logo = AddImage(menu.layoutRoot, "Logo_APPROVED_UNCHANGED", S("01_Logo/timberwings_logo_approved"), 0, 386, 558, 281);
            Animate(logo, 2.3f, 0f, .007f, .12f, .5f);
        }
    }

    // ================================================================ left page

    static void EnsureLeftPage(TWJournalMenuController menu, Report rep)
    {
        // Find / create the column and the four buttons.
        RectTransform col = menu.leftPageRoot;
        if (col == null && menu.continueButton != null) col = menu.continueButton.transform.parent as RectTransform;
        if (col == null) col = menu.bookRoot.Find("LeftPage_Buttons") as RectTransform;
        bool colCreated = col == null;
        if (colCreated)
        {
            col = (RectTransform)UI("LeftPage_Buttons", menu.bookRoot).transform;
            Place(col, Spec.ColumnX, Spec.ColumnY, Spec.ColumnW, Spec.ColumnH);
            col.SetSiblingIndex(0);
        }
        menu.leftPageRoot = col;

        bool createdButtons = false;
        if (menu.continueButton == null) { menu.continueButton = MakeButton(col, "Continue", "btn_continue", "icon_play", "Продолжить", true); createdButtons = true; }
        if (menu.newGameButton == null) { menu.newGameButton = MakeButton(col, "NewGame", "btn_secondary", "icon_leaf", "Новая игра", false); createdButtons = true; }
        if (menu.settingsButton == null) { menu.settingsButton = MakeButton(col, "Settings", "btn_secondary", "icon_settings", "Настройки", false); createdButtons = true; }
        if (menu.exitButton == null) { menu.exitButton = MakeButton(col, "Exit", "btn_secondary", "icon_exit", "Выйти", false); createdButtons = true; }
        if (menu.continueSubtitle == null)
            menu.continueSubtitle = Text(menu.continueButton.transform, "Save_LevelAndDay_InsideContinue", "Уровень 8 · День 8", 0, 0, 270, 24, 18, SubtleInk, true);
        if (createdButtons) ConfigureNav(menu);

        var buttons = new[] { menu.continueButton, menu.newGameButton, menu.settingsButton, menu.exitButton };
        if (buttons.Any(b => b.transform.parent != col))
        { rep.notes.Add("Кнопки лежат в разных контейнерах: автоматическая раскладка кнопок пропущена."); return; }

        bool apply = Managed(col, new Vector2(Spec.ColumnX, Spec.ColumnY), new Vector2(Spec.ColumnW, Spec.ColumnH), 0f,
            rep, colCreated, new Vector2(0f, 0f), new Vector2(1600f, 900f), "Левая страница: колонка кнопок");
        var vlg = col.GetComponent<VerticalLayoutGroup>();
        if (apply)
        {
            bool vlgNew = vlg == null;
            if (vlgNew) vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
            if (vlgNew || rep.force) ConfigureColumn(vlg);
            for (int i = 0; i < buttons.Length; i++) LayoutButton(buttons[i], i == 0, menu, rep);
        }
        else if (vlg == null)
            rep.notes.Add("У колонки кнопок нет VerticalLayoutGroup (ручная раскладка сохранена).");
        // Order inside the column = keyboard order.
        for (int i = 0; i < buttons.Length; i++) buttons[i].transform.SetSiblingIndex(i);
        LayoutRebuilder.ForceRebuildLayoutImmediate(col);
    }

    static void ConfigureColumn(VerticalLayoutGroup vlg)
    {
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.spacing = Spec.ButtonSpacing;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childScaleWidth = false;     // hover / press scale must not disturb the layout
        vlg.childScaleHeight = false;
    }

    // Visible width is identical for all buttons, so left and right edges line up exactly.
    // Rect width compensates for the transparent margin that each sprite carries.
    static void LayoutButton(Button b, bool isContinue, TWJournalMenuController menu, Report rep)
    {
        // Measured on the art: continue 954x183 (visible 950 px), secondary 918x159 (visible 904 px).
        float visibleRatio = isContinue ? 950f / 954f : 904f / 918f;
        float aspect = isContinue ? 183f / 954f : 159f / 918f;
        float w = Mathf.Round(Spec.ButtonVisibleW / visibleRatio * 10f) / 10f;
        float h = Mathf.Round(w * aspect * 10f) / 10f;
        var le = b.GetComponent<LayoutElement>();
        if (le == null || rep.force)
        {
            if (le == null) le = b.gameObject.AddComponent<LayoutElement>();
            le.minWidth = w; le.preferredWidth = w; le.minHeight = h; le.preferredHeight = h;
            le.flexibleWidth = 0f; le.flexibleHeight = 0f;
        }
        var rt = (RectTransform)b.transform;
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;

        var halo = b.transform.Find("HoverHalo") as RectTransform;
        if (halo != null) { halo.anchoredPosition = Vector2.zero; halo.sizeDelta = new Vector2(w * 1.05f, h * 1.42f); }
        var hover = b.GetComponent<TWJournalButtonHover>();
        if (hover != null) { if (hover.hoverHalo == null && halo != null) hover.hoverHalo = halo.GetComponent<Image>(); }

        string key = b.name;
        // Inner parts: identical icon column and text axis on every button.
        var icon = b.transform.Find("Icon") as RectTransform;
        float iconSize = isContinue ? 46f : 42f;
        if (icon != null)
        {
            Managed(icon, new Vector2(Spec.IconX, 0f), new Vector2(iconSize, iconSize), 0f, rep, false,
                isContinue ? new Vector2(-181.125f, 0f) : new Vector2(-174.225f, 0f),
                isContinue ? new Vector2(61f, 61f) : new Vector2(49f, 49f), key + ": иконка");
            if (hover != null && hover.icon == null) hover.icon = icon;
        }
        var label = b.transform.Find("Label") as RectTransform;
        if (label != null)
        {
            Vector2 pos = new Vector2(Spec.LabelX, isContinue ? 11f : 0f);
            Vector2 size = new Vector2(270f, isContinue ? 40f : 50f);
            bool ok = Managed(label, pos, size, 0f, rep, false,
                isContinue ? new Vector2(37f, 22f) : new Vector2(32f, 0f),
                isContinue ? new Vector2(390f, 53f) : new Vector2(380f, 62.64f), key + ": надпись");
            var t = label.GetComponent<TMP_Text>();
            if (ok && t != null) { t.fontSize = isContinue ? 34f : 32f; t.fontSizeMax = t.fontSize; t.fontSizeMin = t.fontSize * .78f; t.color = Ink; }
        }
        if (isContinue && menu.continueSubtitle != null)
        {
            var sub = (RectTransform)menu.continueSubtitle.transform;
            if (sub.parent == b.transform)
            {
                // The save line stays a small SECOND LINE inside the button (no plaques, stars or XP bars).
                bool ok = Managed(sub, new Vector2(Spec.LabelX, -19f), new Vector2(270f, 24f), 0f, rep, false,
                    new Vector2(38f, -30f), new Vector2(395f, 28f), "Продолжить: строка уровня и дня");
                if (ok) { var t = menu.continueSubtitle; t.fontSize = 18f; t.fontSizeMax = 18f; t.fontSizeMin = 14f; t.color = SubtleInk; }
            }
        }
    }

    // ================================================================ right page

    static void EnsureRightPage(TWJournalMenuController menu, Report rep)
    {
        RectTransform right = menu.rightPageRoot;
        if (right == null) right = menu.bookRoot.Find("RightPage_EasterEggs") as RectTransform;
        if (right == null)
        {
            right = (RectTransform)UI("RightPage_EasterEggs", menu.bookRoot).transform;
            Place(right, 0, 0, 1600, 900);
        }
        menu.rightPageRoot = right;

        // Invisible safe-area rect (visible in the Scene view as a gizmo; used by the hover guard and the audit).
        Transform safeT = menu.bookRoot.Find("RightPage_SafeArea");
        bool safeCreated = safeT == null;
        if (safeCreated) safeT = UI("RightPage_SafeArea", menu.bookRoot).transform;
        var safe = (RectTransform)safeT;
        Managed(safe, new Vector2(Spec.SafeCX, Spec.SafeCY), new Vector2(Spec.SafeW, Spec.SafeH), 0f, rep, safeCreated, null, null, "Правая страница: безопасная область");
        safe.SetSiblingIndex(right.GetSiblingIndex());

        for (int i = 0; i < Stickers.Length; i++)
        {
            var s = Stickers[i];
            Sprite sprite = S(s.sprite);
            if (sprite == null) continue;
            Transform t = right.Find(s.name);
            bool created = t == null;
            Image img;
            if (created)
            {
                img = AddImage(right, s.name, sprite, s.x, s.y, s.w, s.w * sprite.rect.height / sprite.rect.width, true);
                t = img.transform;
            }
            else
            {
                img = t.GetComponent<Image>();
                if (img == null) img = t.gameObject.AddComponent<Image>();
                if (img.sprite == null) img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = true;
            }
            var rt = (RectTransform)t;
            Vector4 lg;
            Vector2? lPos = null, lSize = null;
            if (LegacySticker.TryGetValue(s.name, out lg)) { lPos = new Vector2(lg.x, lg.y); lSize = new Vector2(lg.z, lg.w); }
            float h = s.w * sprite.rect.height / sprite.rect.width;
            bool applied = Managed(rt, new Vector2(s.x, s.y), new Vector2(s.w, h), s.tilt, rep, created, lPos, lSize, "Иллюстрация " + s.name);

            ApplyPaperDepth(img);
            var egg = t.GetComponent<TWJournalMenuEasterEgg>();
            bool eggCreated = egg == null;
            if (eggCreated) egg = t.gameObject.AddComponent<TWJournalMenuEasterEgg>();
            egg.menu = menu;
            egg.pageBounds = safe;
            if (eggCreated || created) egg.message = s.hint;
            if (eggCreated || applied)
            {
                egg.hoverAngle = s.hoverAngle;
                egg.hoverLiftPixels = s.lift;
                egg.idleFloatPixels = 1.1f;
                egg.parallaxPixels = 2.5f;
                egg.parallaxDepth = StickerParallax[i % StickerParallax.Length];
                egg.boundsPadding = 2f;
            }
        }

        foreach (Transform child in right)
            if (!Stickers.Any(s => s.name == child.name))
                rep.notes.Add("На правой странице есть посторонний объект «" + child.name + "» (не тронут). Проверь, не выходит ли он за страницу.");
    }

    // ================================================================ decor, hint, confirmation

    static void EnsureDecor(TWJournalMenuController menu)
    {
        var page = menu.bookRoot;
        if (page.Find("Bookmark_Wings") == null)
        {
            var bookmark = AddImage(page, "Bookmark_Wings", S("06_Decor/bookmark_wings"), 84, -425, 106, 129);
            Animate(bookmark, 1.5f, 1.1f, .004f, .16f, 1.4f);
        }
        if (page.Find("Vine_PageLeft") == null)
        {
            var vine = AddImage(page, "Vine_PageLeft", S("06_Decor/vine_leaves"), -638, 309, 112, 125);
            Animate(vine, 1.1f, .7f, 0f, .13f, 2.1f);
        }
        int sparkles = page.GetComponentsInChildren<Transform>(true).Count(t => t.name == "IdleSparkle");
        if (sparkles == 0) { Sparkle(page, -690f, 320f, 32f, .2f); Sparkle(page, 610f, -314f, 28f, 2.6f); }
    }

    static void EnsureHintAndConfirmation(TWJournalMenuController menu)
    {
        if (menu.hintGroup == null || menu.hintLabel == null)
        {
            var lore = menu.layoutRoot.Find("LoreHint");
            if (lore == null)
            {
                var go = UI("LoreHint", menu.layoutRoot);
                Place((RectTransform)go.transform, 0, -487, 640, 54);
                lore = go.transform;
            }
            menu.hintGroup = lore.GetComponent<CanvasGroup>() ?? lore.gameObject.AddComponent<CanvasGroup>();
            var txt = lore.GetComponentInChildren<TMP_Text>(true);
            if (txt == null)
            {
                txt = Text(lore, "HintText", "", 0, 0, 640, 50, 25, new Color(1f, .93f, .78f));
                txt.outlineWidth = .18f;
                txt.outlineColor = Ink;
            }
            menu.hintLabel = txt;
        }
        if (menu.confirmation == null) BuildConfirmation(menu.canvasRect, menu);
        else if (menu.confirmation.transform.parent != menu.canvasRect)
        {
            // Full-screen dimmer: must cover the whole SCREEN, not only the scaled 1920x1080 layout area.
            menu.confirmation.transform.SetParent(menu.canvasRect, false);
            Fill((RectTransform)menu.confirmation.transform);
        }
    }

    static void BuildConfirmation(Transform parent, TWJournalMenuController menu)
    {
        var panel = UI("NewGameConfirmation", parent);
        Fill((RectTransform)panel.transform);
        var dim = panel.AddComponent<Image>();
        dim.color = new Color(.05f, .02f, .08f, .7f);
        dim.raycastTarget = true;
        menu.confirmation = panel;
        var popup = AddImage(panel.transform, "ParchmentConfirmationPanel", S("06_Decor/dialog_panel"), 0, 0, 740, 390, true);
        menu.confirmationTitle = Text(popup.transform, "Title", "Новая игра", 0, 115, 620, 65, 45, Ink);
        menu.confirmationText = Text(popup.transform, "Message", "Начать заново?", 0, 32, 610, 96, 32, SubtleInk);
        menu.confirmNo = MakeButtonRaw(popup.transform, "Cancel", S("03_Buttons/btn_secondary_default"), S("03_Buttons/btn_secondary_hover"),
            S("03_Buttons/btn_secondary_pressed"), null, -168, -102, 296, 71, "Отмена", 30);
        menu.confirmYes = MakeButtonRaw(popup.transform, "Confirm", S("03_Buttons/btn_continue_default"), S("03_Buttons/btn_continue_hover"),
            S("03_Buttons/btn_continue_pressed"), null, 168, -102, 296, 71, "Начать", 30);
        var n = new Navigation { mode = Navigation.Mode.Explicit };
        n.selectOnRight = menu.confirmYes; n.selectOnLeft = menu.confirmYes;
        menu.confirmNo.navigation = n;
        n.selectOnRight = menu.confirmNo; n.selectOnLeft = menu.confirmNo;
        menu.confirmYes.navigation = n;
        panel.SetActive(false);
    }

    // ================================================================ version badge (screen corner, code-driven text)

    static void EnsureVersionBadge(TWJournalMenuController menu, Report rep)
    {
        Transform bt = menu.canvasRect.Find("Version_Badge");
        bool created = bt == null;
        Image badgeImg;
        if (created)
        {
            badgeImg = AddImage(menu.canvasRect, "Version_Badge", S("08_Polish/version_badge_v19_blank"), 0, 0, Spec.BadgeW, Spec.BadgeH);
            bt = badgeImg.transform;
        }
        else badgeImg = bt.GetComponent<Image>();
        var rt = (RectTransform)bt;
        rt.anchorMin = rt.anchorMax = Vector2.right;      // bottom-right corner of the whole screen
        rt.pivot = Vector2.right;
        var stamp = rt.GetComponent<TWJournalLayoutStamp>();
        Vector2 pos = new Vector2(-Spec.BadgeMargin, Spec.BadgeMargin), size = new Vector2(Spec.BadgeW, Spec.BadgeH);
        bool apply = created || rep.force || stamp == null ||
            (Near(rt.anchoredPosition, stamp.position) && Near(rt.sizeDelta, stamp.size));
        if (apply)
        {
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            if (stamp == null) stamp = rt.gameObject.AddComponent<TWJournalLayoutStamp>();
            stamp.position = pos; stamp.size = size; stamp.rotationZ = 0f; stamp.revision = StampRevision;
            rep.applied.Add("Бейдж версии");
        }
        else rep.kept.Add("Бейдж версии: позиция / размер");
        if (badgeImg != null)
        {
            badgeImg.raycastTarget = false;
            badgeImg.preserveAspect = true;
            if (created || badgeImg.sprite == null || badgeImg.sprite.name.Contains("plaque")) badgeImg.sprite = S("08_Polish/version_badge_v19_blank");
        }
        menu.versionBadge = rt;

        // The TMP label: re-use the existing one (it used to sit, disabled, on the old baked plaque).
        TMP_Text label = menu.versionLabel;
        bool restyle = false;
        if (label == null)
        {
            label = Text(rt, "Version_Runtime", "v1.9", 0, 0, 110, 34, 23, new Color(1f, .94f, .76f), true);
            restyle = true;
        }
        else if (label.transform.parent != rt)
        {
            label.transform.SetParent(rt, false);
            restyle = true;
        }
        if (restyle || rep.force)
        {
            Place(label.rectTransform, Spec.BadgeTextX, Spec.BadgeTextY, 110f, 34f);
            label.fontSize = 23f; label.fontSizeMax = 23f; label.fontSizeMin = 18f;
            label.enableAutoSizing = true;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, .93f, .74f, 1f);
            label.outlineWidth = .16f;
            label.outlineColor = new Color(.16f, .05f, .22f, 1f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (font != null) label.font = font;
        }
        label.raycastTarget = false;
        label.enabled = true;
        label.gameObject.SetActive(true);
        label.text = string.IsNullOrEmpty(menu.menuVersion) ? "v" + Application.version : menu.menuVersion;
        menu.versionLabel = label;
        if (rt.GetComponent<CanvasGroup>() == null) rt.gameObject.AddComponent<CanvasGroup>();

        // Obsolete versions of the badge (baked "v1.9" in the PNG, footer leaf, floating plaque): remove after the label has moved.
        RemoveIfExists(menu.bookRoot, "Version_Footer_Leaf", label.transform, rep);
        RemoveIfExists(menu.rightPageRoot, "Version_Approved_Badge", label.transform, rep);
        RemoveIfExists(menu.bookRoot, "Version_Approved_Badge", label.transform, rep);
        RemoveIfExists(menu.layoutRoot, "Version_v19_Plaque", label.transform, rep);
    }

    static void RemoveIfExists(Transform parent, string name, Transform protect, Report rep)
    {
        if (parent == null) return;
        var t = parent.Find(name);
        if (t == null) return;
        if (protect != null && protect.IsChildOf(t)) return;   // never delete the version label itself
        UnityEngine.Object.DestroyImmediate(t.gameObject);
        rep.notes.Add("Удалён устаревший объект «" + name + "».");
    }

    // ================================================================ managed layout helper (manual settings are kept)

    static bool Near(Vector2 a, Vector2 b) { return Mathf.Abs(a.x - b.x) <= 1.5f && Mathf.Abs(a.y - b.y) <= 1.5f; }

    // Applies pos/size/rotation if the object is new, forced, still equal to its stamp, or still equal to the known v1.9.x default.
    // Otherwise it was changed by hand: leave it and report.
    static bool Managed(RectTransform rt, Vector2 pos, Vector2 size, float rotZ, Report rep, bool created,
        Vector2? legacyPos, Vector2? legacySize, string label)
    {
        var stamp = rt.GetComponent<TWJournalLayoutStamp>();
        bool apply;
        if (created || rep.force) apply = true;
        else if (Near(rt.anchoredPosition, pos) && Near(rt.sizeDelta, size) &&
                 Mathf.Abs(Mathf.DeltaAngle(rt.localEulerAngles.z, rotZ)) < .6f) apply = true;   // already at the target
        else if (stamp != null)
            apply = Near(rt.anchoredPosition, stamp.position) && Near(rt.sizeDelta, stamp.size) &&
                    Mathf.Abs(Mathf.DeltaAngle(rt.localEulerAngles.z, stamp.rotationZ)) < .6f;
        else if (legacyPos.HasValue)
            apply = Near(rt.anchoredPosition, legacyPos.Value) && (!legacySize.HasValue || Near(rt.sizeDelta, legacySize.Value));
        else apply = false;

        if (!apply) { rep.kept.Add(label); return false; }
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        if (stamp == null) stamp = rt.gameObject.AddComponent<TWJournalLayoutStamp>();
        stamp.position = pos; stamp.size = size; stamp.rotationZ = rotZ; stamp.revision = StampRevision;
        EditorUtility.SetDirty(rt);
        rep.applied.Add(label);
        return true;
    }

    // ================================================================ backup

    static void BackupScene()
    {
        Directory.CreateDirectory(AssetRoot + "Backups");
        // The very first pre-journal snapshot is never overwritten by this installer.
        string first = AssetRoot + "Backups/MainMenu_before_Journal.unity";
        if (!File.Exists(first)) { File.Copy(MenuScene, first, false); AssetDatabase.ImportAsset(first); }
        string target = AssetRoot + "Backups/MainMenu_before_FinalPolish_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        File.Copy(MenuScene, target, false);
        AssetDatabase.ImportAsset(target);
        Debug.Log("JournalMenu: резервная копия сцены: " + target);
        var old = Directory.GetFiles(AssetRoot + "Backups", "MainMenu_before_FinalPolish_*.unity").OrderByDescending(f => f).Skip(6).ToArray();
        foreach (var f in old) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
    }

    // ================================================================ legacy UI

    static void DisableOldUI()
    {
        // Disabled MonoBehaviours still run Awake while their GameObject is active, so deactivate the whole legacy root
        // (non-destructive). This prevents MainMenuBackground.Awake from creating a second art Canvas.
        var scene = SceneManager.GetActiveScene();
        foreach (var legacy in scene.GetRootGameObjects())
        {
            if (legacy.name == RootName) continue;
            if (legacy.GetComponentInChildren<MainMenuStub>(true) == null) continue;
            foreach (var c in legacy.GetComponentsInChildren<MainMenuStub>(true)) c.enabled = false;
            foreach (var c in legacy.GetComponentsInChildren<MainMenuBackground>(true)) c.enabled = false;
            foreach (var c in legacy.GetComponentsInChildren<MainMenuLife>(true)) c.enabled = false;
            legacy.SetActive(false);
        }
    }

    // ================================================================ import helpers

    static void ImportSprites()
    {
        foreach (var f in Directory.GetFiles(Art, "*.png", SearchOption.AllDirectories))
        {
            string path = f.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool isBackground = path.Contains("07_Backgrounds");
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
                !importer.mipmapEnabled && importer.maxTextureSize >= 2048 && importer.textureCompression == TextureImporterCompression.Uncompressed)
                continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = !isBackground;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }
        AssetDatabase.Refresh();
    }

    static Sprite S(string path)
    {
        var result = AssetDatabase.LoadAssetAtPath<Sprite>(Art + path + ".png");
        if (result == null) Debug.LogError("JournalMenu: missing sprite " + Art + path + ".png");
        return result;
    }

    static AudioClip Clip(string path)
    {
        var result = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "08_Polish/Sounds/" + path + ".wav");
        if (result == null) Debug.LogError("JournalMenu: missing audio " + path);
        return result;
    }

    static TMP_FontAsset FindCyrillicFont()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.HasCharacter('Ж') && asset.HasCharacter('я')) return asset;
        }
        return null;
    }

    // ================================================================ UI factory helpers

    static GameObject UI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    static RectTransform Fill(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = Vector2.one * .5f;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    static Image AddImage(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, bool raycast = false)
    {
        var go = UI(name, parent);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple; // NOT nine-sliced: curled parchment ends would stretch badly.
        img.preserveAspect = true;
        img.raycastTarget = raycast;
        Place((RectTransform)go.transform, x, y, w, h);
        return img;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string content, float x, float y, float w, float h,
        float size, Color color, bool bold = true, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = UI(name, parent);
        Place((RectTransform)go.transform, x, y, w, h);
        var label = go.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = content;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.color = color;
        label.alignment = align;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * .75f;
        label.fontSizeMax = size;
        label.raycastTarget = false;
        return label;
    }

    static readonly Color Ink = new Color32(70, 32, 62, 255);
    static readonly Color SubtleInk = new Color32(91, 53, 52, 255);

    static TWJournalAmbientMotion Animate(Image image, float vertical, float rotation, float scale, float cycles, float phase)
    {
        var a = image.gameObject.AddComponent<TWJournalAmbientMotion>();
        a.verticalPixels = vertical;
        a.rotationDegrees = rotation;
        a.scaleAmplitude = scale;
        a.cyclesPerSecond = cycles;
        a.phase = phase;
        return a;
    }

    static void Sparkle(Transform parent, float x, float y, float size, float phase)
    {
        var img = AddImage(parent, "IdleSparkle", S("08_Polish/sparkle"), x, y, size, size);
        img.color = new Color(1f, .97f, .87f, 1f);
        img.gameObject.AddComponent<CanvasGroup>();
        var motion = Animate(img, 2.2f, 4f, .045f, .23f, phase);
        motion.opacityBase = .38f;
        motion.opacityAmplitude = .28f;
    }

    static void ApplyPaperDepth(Image img)
    {
        // UI Shadow casts a warm offset silhouette under each sticker; depth follows the PNG silhouette.
        var shadow = img.GetComponent<Shadow>();
        if (shadow == null) shadow = img.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.19f, 0.075f, 0.14f, 0.22f);
        shadow.effectDistance = new Vector2(4f, -6f);
        shadow.useGraphicAlpha = true;
    }

    static void ConfigureNav(TWJournalMenuController menu)
    {
        Button[] b = { menu.continueButton, menu.newGameButton, menu.settingsButton, menu.exitButton };
        for (int i = 0; i < b.Length; i++)
        {
            var navigation = new Navigation { mode = Navigation.Mode.Explicit };
            navigation.selectOnUp = b[(i + b.Length - 1) % b.Length];
            navigation.selectOnDown = b[(i + 1) % b.Length];
            b[i].navigation = navigation;
        }
    }

    // Main-menu button created from scratch (only used when the scene has none). Size and position come from the LayoutGroup.
    static Button MakeButton(Transform parent, string name, string spriteBase, string iconName, string label, bool isContinue)
    {
        var btn = MakeButtonRaw(parent, name, S("03_Buttons/" + spriteBase + "_default"), S("03_Buttons/" + spriteBase + "_hover"),
            S("03_Buttons/" + spriteBase + "_pressed"), S("04_Icons/" + iconName), 0, 0, 460, isContinue ? 88 : 80, label, isContinue ? 34 : 32);
        return btn;
    }

    static Button MakeButtonRaw(Transform parent, string name, Sprite normal, Sprite hover, Sprite pressed, Sprite icon,
        float x, float y, float w, float h, string label, float fontSize)
    {
        var img = AddImage(parent, name, normal, x, y, w, h, true);
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.SpriteSwap;
        var state = new SpriteState
        {
            highlightedSprite = hover != null ? hover : normal,
            pressedSprite = pressed != null ? pressed : normal,
            selectedSprite = hover != null ? hover : normal,
            disabledSprite = normal
        };
        button.spriteState = state;
        var colors = button.colors;
        colors.disabledColor = new Color(1f, 1f, 1f, .58f);
        button.colors = colors;
        var feedback = img.gameObject.AddComponent<TWJournalButtonHover>();
        var halo = AddImage(img.transform, "HoverHalo", S("08_Polish/button_hover_halo"), 0f, 0f, w * 1.05f, h * 1.42f);
        halo.color = new Color(1f, 1f, 1f, 0f);
        feedback.hoverHalo = halo;
        if (icon != null)
        {
            var ic = AddImage(img.transform, "Icon", icon, Spec.IconX, 0, 42, 42);
            feedback.icon = (RectTransform)ic.transform;
        }
        Text(img.transform, "Label", label, Spec.LabelX, 0, 270, 50, fontSize, Ink, true);
        return button;
    }
}
#endif
