// Editor-only catalog, state comparison, nine-slice lab and layout preview.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class CandyKitStudio : EditorWindow
{
    sealed class Item
    {
        public CandyUiKitGenerator.KitSpriteInfo info;
        public Sprite sprite;
    }

    static readonly Color Ink = new Color32(49, 39, 95, 255);
    static readonly string[] Tabs = { "Каталог", "Состояния", "Растяжение", "Компоновка", "QA" };
    static readonly string[] Categories = { "Все", "Buttons", "Panels", "Controls", "Icons", "Tabs", "Rarity", "Extra", "Fun" };
    static readonly string[] CategoryLabels = { "Все элементы", "Кнопки", "Панели", "Управление", "Иконки", "Вкладки", "Редкость", "Дополнительно", "Эффекты" };
    static readonly string[] Backgrounds = { "Тёмный", "Светлый", "Лес" };
    static readonly string[] Profiles = { "Золото", "Мята", "Небо", "Коралл", "Настройки", "Закрыть" };
    readonly List<Item> items = new List<Item>();
    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    List<string> issues;
    string query = "", selected = "Panels/panel_window";
    int tab, category, background, profile;
    bool smallButtons, showBorders = true, assignStates = true;
    float borderScale = 0.75f, previewWidth = 520f, previewHeight = 220f;
    Vector2 scroll, detailScroll;

    [MenuItem("Tools/BuilderSim/UI/Candy Kit/Открыть Candy UI Studio", false, 199)]
    public static void ShowWindow()
    {
        var window = GetWindow<CandyKitStudio>();
        window.titleContent = new GUIContent("Candy UI Studio");
        window.minSize = new Vector2(940f, 660f);
        window.Show();
    }

    void OnEnable()
    {
        minSize = new Vector2(940f, 660f);
        EditorApplication.projectChanged += Reload;
        Reload();
    }
    void OnDisable() { EditorApplication.projectChanged -= Reload; }
    void OnSelectionChange() { Repaint(); }

    void Reload()
    {
        items.Clear(); sprites.Clear(); issues = null;
        foreach (var info in CandyUiKitGenerator.Catalog)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(info.AssetPath);
            items.Add(new Item { info = info, sprite = sprite });
            sprites[info.key] = sprite;
        }
        Repaint();
    }

    Sprite Get(string key) { Sprite s; return sprites.TryGetValue(key, out s) ? s : null; }

    void OnGUI()
    {
        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label("CANDY  /  UI STUDIO", new GUIStyle(EditorStyles.boldLabel) { fontSize = 22 });
                GUILayout.Label("Облачный набор TimberWings · PNG / 200 PPU · предпросмотр без Play", EditorStyles.miniLabel);
            }
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Настроить импорт", GUILayout.Width(152), GUILayout.Height(34)))
                { CandyUiKitGenerator.GenerateAll(); Reload(); }
            if (GUILayout.Button("Папка", GUILayout.Width(66), GUILayout.Height(34))) CandyUiKitGenerator.PingFolder();
        }
        EditorGUILayout.Space(8);
        tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(28));
        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Фон предпросмотра", GUILayout.Width(133));
            background = GUILayout.Toolbar(background, Backgrounds, GUILayout.Width(230));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Обновить", EditorStyles.miniButton, GUILayout.Width(78))) Reload();
        }
        EditorGUILayout.Space(6);
        if (tab == 0) CatalogView();
        else if (tab == 1) StatesView();
        else if (tab == 2) SliceView();
        else if (tab == 3) LayoutView();
        else AuditView();
    }

    void CatalogView()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            category = EditorGUILayout.Popup(category, CategoryLabels, GUILayout.Width(135));
            query = GUILayout.TextField(query, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(24))) { query = ""; GUI.FocusControl(null); }
        }
        var visible = items.Where(i => (category == 0 || i.info.Category == Categories[category]) &&
            (string.IsNullOrEmpty(query) || i.info.key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
             CategoryLabels[Array.IndexOf(Categories, i.info.Category)].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label(visible.Count + " / " + items.Count + " элементов", EditorStyles.miniLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                int columns = Mathf.Max(2, Mathf.FloorToInt((position.width - 350f) / 148f));
                for (int row = 0; row * columns < visible.Count; row++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int col = 0; col < columns; col++)
                        {
                            int n = row * columns + col;
                            Rect r = GUILayoutUtility.GetRect(128, 130, GUILayout.ExpandWidth(true));
                            if (n >= visible.Count) continue;
                            var item = visible[n];
                            Back(r);
                            if (selected == item.info.key) Outline(r, new Color(0.68f, 0.61f, 1f), 2f);
                            if (item.sprite != null) DrawFit(new Rect(r.x + 10, r.y + 6, r.width - 20, 83), item.sprite);
                            else GUI.Label(new Rect(r.x + 8, r.y + 20, r.width - 16, 40), "Нет PNG / Sprite", EditorStyles.centeredGreyMiniLabel);
                            string label = item.info.key.Substring(item.info.key.LastIndexOf('/') + 1);
                            GUI.Label(new Rect(r.x + 5, r.y + 93, r.width - 10, 32), label,
                                CardLabelStyle());
                            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { selected = item.info.key; GUI.FocusControl(null); Repaint(); }
                        }
                    }
                }
                if (visible.Count == 0) EditorGUILayout.HelpBox("Совпадений нет. Попробуй panel, coin, mint или название категории.", MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(314))) InspectorView();
        }
    }

    void InspectorView()
    {
        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
        var item = items.Find(i => i.info.key == selected);
        if (item == null) { EditorGUILayout.EndScrollView(); return; }
        EditorGUILayout.SelectableLabel(selected, EditorStyles.boldLabel, GUILayout.Height(38));
        Rect r = GUILayoutUtility.GetRect(280, 190, GUILayout.ExpandWidth(true));
        Back(r); DrawFit(Inset(r, 14), item.sprite);
        GUILayout.Label(item.info.width + " × " + item.info.height + " px  ·  PPU 200", EditorStyles.miniLabel);
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.Vector4Field("9-slice · L / B / R / T", item.info.border);
        EditorGUILayout.Space(6);
        if (GUILayout.Button("Показать спрайт в Project"))
        { Selection.activeObject = item.sprite; if (item.sprite != null) EditorGUIUtility.PingObject(item.sprite); }
        if (GUILayout.Button("Копировать ключ")) GUIUtility.systemCopyBuffer = selected;
        if (GUILayout.Button("Проверить растяжение")) tab = 2;
        assignStates = EditorGUILayout.ToggleLeft("Назначать и состояния Button", assignStates);
        using (new EditorGUI.DisabledScope(item.sprite == null || EditorApplication.isPlayingOrWillChangePlaymode ||
            !Selection.gameObjects.Any(go => go.GetComponent<Image>() != null)))
            if (GUILayout.Button("Назначить выбранным Image")) Assign(item.sprite);
        EditorGUILayout.HelpBox("Назначение меняет только картинки у выделенных Image. Для кнопок может подключить четыре состояния. Доступен Undo.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    string ProfileKey()
    {
        if (profile == 4) return "Buttons/btn_round_gear";
        if (profile == 5) return "Buttons/btn_close";
        string color = new[] { "yellow", "mint", "blue", "coral" }[profile];
        return smallButtons ? "Buttons/Small/btn_small_" + color : "Buttons/btn_" + color;
    }

    void StatesView()
    {
        profile = GUILayout.Toolbar(profile, Profiles);
        using (new EditorGUI.DisabledScope(profile >= 4))
            smallButtons = EditorGUILayout.ToggleLeft("Маленький размер кнопок", smallButtons);
        string[] states = { "normal", "hover", "pressed", "disabled" };
        string[] labels = { "Обычная", "Наведение", "Нажата", "Недоступна" };
        EditorGUILayout.Space(20);
        using (new EditorGUILayout.HorizontalScope())
            for (int i = 0; i < states.Length; i++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(labels[i], EditorStyles.boldLabel);
                    Rect r = GUILayoutUtility.GetRect(180, 150, GUILayout.ExpandWidth(true));
                    Back(r); DrawFit(Inset(r, 14), Get(ProfileKey() + "_" + states[i]));
                    if (GUILayout.Button("Открыть в каталоге")) { selected = ProfileKey() + "_" + states[i]; tab = 0; }
                }
        EditorGUILayout.HelpBox("Сравнение готовых PNG. Размеры файлов одинаковы для всех состояний; тексты рисуются отдельно в игре.", MessageType.Info);
    }

    void SliceView()
    {
        GUILayout.Label(selected, EditorStyles.boldLabel);
        previewWidth = EditorGUILayout.Slider("Ширина", previewWidth, 100, 800);
        previewHeight = EditorGUILayout.Slider("Высота", previewHeight, 40, 340);
        borderScale = EditorGUILayout.Slider("Масштаб рамки", borderScale, 0.3f, 1.6f);
        showBorders = EditorGUILayout.ToggleLeft("Показывать линии 9-slice", showBorders);
        var s = Get(selected);
        var bounds = GUILayoutUtility.GetRect(300, Mathf.Clamp(position.height - 330f, 190f, 390f), GUILayout.ExpandWidth(true));
        Back(bounds);
        if (s == null) return;
        float fit = Mathf.Min(1f, (bounds.width - 24) / previewWidth);
        Rect r = new Rect(bounds.center.x - previewWidth * fit / 2, bounds.center.y - previewHeight * fit / 2,
            previewWidth * fit, previewHeight * fit);
        DrawSliced(r, s, borderScale * fit, showBorders);
        if (s.border == Vector4.zero) EditorGUILayout.HelpBox("У этой иконки нет рамки: используй Image / Simple с Preserve Aspect.", MessageType.Info);
        else EditorGUILayout.HelpBox("Проверка соответствует Canvas Reference PPU 100. В игре Image / Sliced; масштаб рамки = 1 / pixelsPerUnitMultiplier.", MessageType.None);
    }

    void LayoutView()
    {
        EditorGUILayout.HelpBox("Пример компоновки из настоящих спрайтов набора. Предпросмотр не меняет сцену и не подключает игровой HUD.", MessageType.None);
        Rect space = GUILayoutUtility.GetRect(300, Mathf.Max(180, position.height - 230f), GUILayout.ExpandWidth(true));
        float k = Mathf.Min(space.width / 960f, space.height / 540f);
        Rect area = new Rect(space.center.x - 480f * k, space.y, 960f * k, 540f * k);
        Back(area);
        Action<string, float, float, float, float> panel = (key, x, y, w, h) =>
            DrawSliced(new Rect(area.x + x * k, area.y + y * k, w * k, h * k), Get(key), k * 0.75f, false);
        Action<string, float, float, float> icon = (key, x, y, size) =>
            DrawFit(new Rect(area.x + x * k, area.y + y * k, size * k, size * k), Get(key));
        Action<string, float, float, float, float> text = (value, x, y, w, size) =>
            DrawText(new Rect(area.x + x * k, area.y + y * k, w * k, 42 * k), value, Mathf.Max(11, Mathf.RoundToInt(size * k)));
        string[] res = { "coin", "log", "planks" }; string[] values = { "1 217", "3", "0" };
        for (int i = 0; i < 3; i++)
        {
            panel("Panels/panel_chip", 24 + i * 157, 24, 144, 48);
            icon("Icons/icon_" + res[i], 22 + i * 157, 20, 52);
            text(values[i], 73 + i * 157, 28, 90, 23);
        }
        panel("Panels/panel_chip", 724, 24, 212, 76);
        icon("Icons/icon_star", 712, 15, 78); text("LVL 10", 782, 26, 138, 21);
        panel("Controls/xp_frame", 784, 71, 131, 16); panel("Controls/xp_fill", 784, 71, 104, 16);
        panel("Panels/panel_quest", 300, 114, 360, 150);
        panel("Panels/sign_wood", 394, 97, 172, 47); text("ЗАДАНИЕ", 402, 102, 154, 18);
        text("Нарубить дров", 340, 159, 280, 26);
        icon("Icons/icon_log", 340, 211, 32); text("3 / 4", 380, 206, 110, 20);
        panel("Controls/bar_frame", 455, 224, 151, 14); panel("Controls/bar_fill_mint", 455, 224, 113, 14);
        panel("Panels/panel_chip", 376, 320, 208, 58);
        icon("Icons/icon_backpack", 364, 314, 66); text("3 / 21", 434, 329, 126, 28);
        panel("Buttons/btn_yellow_normal", 362, 436, 236, 70);
        text("E   Рубить", 382, 448, 196, 28);
        panel("Panels/panel_chip", 24, 460, 200, 48);
        icon("Icons/icon_check", 33, 465, 36); text("Сохранено", 71, 466, 141, 19);
        panel("Buttons/btn_yellow_normal", 724, 447, 212, 62);
        text("Прокачка", 750, 458, 160, 26);
    }

    void AuditView()
    {
        if (GUILayout.Button("Проверить PNG, размеры, PPU и 9-slice", GUILayout.Height(34))) issues = CandyUiKitGenerator.Audit();
        if (issues == null) { EditorGUILayout.HelpBox("Проверка не меняет файлы и сцену.", MessageType.Info); return; }
        EditorGUILayout.HelpBox(issues.Count == 0 ? "Готово: все " + CandyUiKitGenerator.Catalog.Length + " спрайтов прошли проверку импорта." : "Обнаружено проблем: " + issues.Count,
            issues.Count == 0 ? MessageType.Info : MessageType.Warning);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string issue in issues) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        EditorGUILayout.EndScrollView();
    }

    void Assign(Sprite chosen)
    {
        string stem = selected;
        foreach (var suffix in new[] { "_normal", "_hover", "_pressed", "_disabled" })
            if (stem.EndsWith(suffix, StringComparison.Ordinal)) { stem = stem.Substring(0, stem.Length - suffix.Length); break; }
        bool complete = assignStates && Get(stem + "_normal") != null && Get(stem + "_hover") != null &&
            Get(stem + "_pressed") != null && Get(stem + "_disabled") != null;
        Undo.SetCurrentGroupName("Candy Kit: назначить спрайт");
        foreach (var go in Selection.gameObjects)
        {
            var image = go.GetComponent<Image>(); if (image == null) continue;
            var button = go.GetComponent<Button>();
            bool buttonStates = complete && button != null && button.targetGraphic == image;
            Undo.RecordObject(image, "Candy Kit: назначить спрайт");
            image.sprite = buttonStates ? Get(stem + "_normal") : chosen;
            if (image.type != Image.Type.Filled) image.type = image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image); EditorUtility.SetDirty(image);
            if (buttonStates)
            {
                Undo.RecordObject(button, "Candy Kit: состояния кнопки");
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { highlightedSprite = Get(stem + "_hover"), selectedSprite = Get(stem + "_hover"),
                    pressedSprite = Get(stem + "_pressed"), disabledSprite = Get(stem + "_disabled") };
                PrefabUtility.RecordPrefabInstancePropertyModifications(button); EditorUtility.SetDirty(button);
            }
            if (go.scene.IsValid() && go.scene.isLoaded) EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }

    GUIStyle CardLabelStyle()
    {
        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, wordWrap = true };
        style.normal.textColor = background == 0 ? new Color(0.92f, 0.92f, 1f) : Ink;
        return style;
    }

    void Back(Rect r)
    {
        Color c = background == 0 ? new Color32(43, 48, 72, 255) : background == 1 ? new Color32(233, 236, 249, 255) : new Color32(114, 161, 125, 255);
        EditorGUI.DrawRect(r, c);
    }
    static Rect Inset(Rect r, float n) { return new Rect(r.x + n, r.y + n, Mathf.Max(1, r.width - n * 2), Mathf.Max(1, r.height - n * 2)); }
    static void Outline(Rect r, Color c, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c); EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c); EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
    }
    static void DrawFit(Rect r, Sprite s)
    {
        if (s == null || s.texture == null) return;
        float scale = Mathf.Min(r.width / s.rect.width, r.height / s.rect.height);
        Rect dest = new Rect(r.center.x - s.rect.width * scale / 2, r.center.y - s.rect.height * scale / 2, s.rect.width * scale, s.rect.height * scale);
        GUI.DrawTextureWithTexCoords(dest, s.texture, new Rect(s.rect.x / s.texture.width, s.rect.y / s.texture.height,
            s.rect.width / s.texture.width, s.rect.height / s.texture.height), true);
    }
    static void DrawText(Rect r, string value, int size)
    {
        var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = size };
        style.normal.textColor = Ink; GUI.Label(r, value, style);
    }

    public static void DrawSliced(Rect r, Sprite s, float borderScale, bool guides)
    {
        if (s == null || s.texture == null || r.width <= 0 || r.height <= 0) return;
        var b = s.border; var src = s.rect;
        float k = 100f / Mathf.Max(1f, s.pixelsPerUnit) * borderScale;
        float left = b.x * k, bottom = b.y * k, right = b.z * k, top = b.w * k;
        float sx = Mathf.Min(1f, r.width / Mathf.Max(0.001f, left + right));
        float sy = Mathf.Min(1f, r.height / Mathf.Max(0.001f, bottom + top));
        left *= sx; right *= sx; bottom *= sy; top *= sy;
        float[] x = { src.x, src.x + b.x, src.xMax - b.z, src.xMax };
        float[] y = { src.y, src.y + b.y, src.yMax - b.w, src.yMax };
        float[] dx = { 0, left, r.width - right, r.width };
        float[] dy = { 0, bottom, r.height - top, r.height };
        for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
        {
            float w = dx[col + 1] - dx[col], h = dy[row + 1] - dy[row];
            if (w <= 0 || h <= 0 || x[col + 1] <= x[col] || y[row + 1] <= y[row]) continue;
            GUI.DrawTextureWithTexCoords(new Rect(r.x + dx[col], r.yMax - dy[row + 1], w, h), s.texture,
                new Rect(x[col] / s.texture.width, y[row] / s.texture.height,
                    (x[col + 1] - x[col]) / s.texture.width, (y[row + 1] - y[row]) / s.texture.height), true);
        }
        if (!guides || b == Vector4.zero) return;
        Color color = new Color(0.9f, 0.42f, 0.65f, 0.85f);
        EditorGUI.DrawRect(new Rect(r.x + left, r.y, 1, r.height), color);
        EditorGUI.DrawRect(new Rect(r.xMax - right, r.y, 1, r.height), color);
        EditorGUI.DrawRect(new Rect(r.x, r.y + top, r.width, 1), color);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - bottom, r.width, 1), color);
    }
}
