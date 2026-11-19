using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static UiLayout;

[DefaultExecutionOrder(50)]
internal sealed partial class AxeWindow : MonoBehaviour
{
    const int PreviewLayer = 5;
    static AxeWindow current;
    PlayerAxeEquipment equipment;
    TradingStall merchant;
    StoryFlow forge;
    RuneForgeInspection table;
    AxeId selected;
    bool open;
    int openedFrame;
    GameObject panel, axeView, previewStage;
    Transform previewPivot;
    GameObject previewModel;
    MeshRenderer previewRenderer;
    Camera previewCamera;
    readonly AxeRuneFinish previewFinish = new AxeRuneFinish();
    AxeId previewAxe;
    bool previewSelected;
    RenderTexture previewTexture;
    RawImage previewImage;
    TMP_Text title, details, stats, status, purchaseLabel, repairLabel, wallet;
    Button equipOrBuy, repair, forgeRunes, sawMode;
    TMP_Text sawModeLabel;
    readonly Button[] tabs = new Button[AxeCatalog.All.Length];
    readonly TMP_Text[] tabLabels = new TMP_Text[AxeCatalog.All.Length];
    bool dragging;
    float refreshAt;
    Vector2 previousPointer;
    float yaw, pitch;

    internal static void Open(PlayerAxeEquipment axes, TradingStall shop = null, StoryFlow workshop = null,
        RuneForgeInspection table = null)
    {
        if (axes == null || !axes.CanEquip || MobileInput.ModalOpen) return;
        if (current == null) current = new GameObject("Axe equipment window").AddComponent<AxeWindow>();
        current.Show(axes, shop, workshop, table);
    }

    void Awake()
    {
        UiModal.Register(this, () => open, Close);
        SaveSystem.Loaded += Close;
        SaveSystem.NewGameStarted += Close;
    }

    void OnDestroy()
    {
        UiModal.Unregister(this);
        SaveSystem.Loaded -= Close;
        SaveSystem.NewGameStarted -= Close;
        if (current == this) current = null;
        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
        }
    }

    void Show(PlayerAxeEquipment axes, TradingStall shop, StoryFlow workshop, RuneForgeInspection station)
    {
        if (panel == null) Build();
        equipment = axes;
        merchant = shop;
        forge = workshop;
        table = station;
        open = true;
        openedFrame = Time.frameCount;
        panel.SetActive(true);
        panel.GetComponent<Image>().color = table != null ? new Color(.16f, .12f, .27f, .34f)
            : new Color(.17f, .18f, .36f, .48f);
        if (runeView != null) runeView.SetActive(false);
        axeView.SetActive(true);
        previewStage.SetActive(true);
        AxeId opening = axes.Equipped.Id;
        if (shop != null) opening = AxeId.Iron;
        else if (table != null || forge != null)
            opening = forge != null && forge.GunnarBladeAvailable && !axes.Has(AxeId.Gunnar)
                ? AxeId.Gunnar : AxeId.Steel;
        if (table != null && (axes.PendingRuneOffer != null || opening != AxeId.Gunnar &&
            (axes.Has(AxeId.Iron) || axes.Has(AxeId.Steel) || axes.Has(AxeId.Gunnar))))
        {
            selected = axes.Equipped.Id;
            ShowRunes();
            return;
        }
        Select(opening);
    }

    void Close()
    {
        if (!open) return;
        open = dragging = false;
        ResetForgePresentation();
        panel.SetActive(false);
        previewStage.SetActive(false);
        MobileInput.SuppressHeldActionUntilRelease();
    }

    void Update()
    {
        if (!open) return;
        if (equipment == null || !equipment.CanEquip ||
            (merchant != null && (!merchant.Unlocked || !merchant.isActiveAndEnabled ||
                merchant.DistanceToPlayer() > merchant.Radius)) ||
            (table != null ? !table.CanUse(equipment) : forge != null && !forge.CanUseAxeForge(equipment)))
        {
            Close();
            return;
        }
        if (Time.frameCount != openedFrame &&
            (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(MobileInput.ActionKey)))
        {
            Close();
            return;
        }
        if (axeView.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Select((AxeId)(((int)selected + tabs.Length - 1) % tabs.Length));
            if (Input.GetKeyDown(KeyCode.RightArrow)) Select((AxeId)(((int)selected + 1) % tabs.Length));
            RotatePreview(previewImage);
        }
        else if (runeView != null && runeView.activeSelf) TickRuneView();
        if (Time.unscaledTime >= refreshAt) Refresh();
    }

    void Select(AxeId id)
    {
        selected = id;
        Refresh();
    }

    void DisplayAxe(AxeId id, AxeRuneId rune)
    {
        if (!previewSelected || previewAxe != id)
        {
            previewSelected = true;
            previewAxe = id;
            if (previewModel != null)
            {
                previewModel.SetActive(false);
                Destroy(previewModel);
            }
            previewModel = null;
            previewRenderer = null;
            yaw = 81f;
            pitch = 6f;
            previewPivot.localRotation = Quaternion.Euler(pitch, yaw, -10f);
            var model = AxeCatalog.Get(id).Model;
            if (model != null)
            {
                previewModel = Instantiate(model, previewPivot, false);
                previewRenderer = previewModel.GetComponent<MeshRenderer>();
                var filter = model.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) previewModel.transform.localPosition = -filter.sharedMesh.bounds.center;
                foreach (Transform child in previewModel.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = PreviewLayer;
            }
        }
        previewFinish.Set(previewRenderer, id, rune);
    }

    void RotatePreview(RawImage target)
    {
        var pointer = (Vector2)Input.mousePosition;
        if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(target.rectTransform, pointer))
        {
            dragging = true;
            previousPointer = pointer;
        }
        if (!Input.GetMouseButton(0)) dragging = false;
        if (!dragging) return;
        Vector2 delta = pointer - previousPointer;
        previousPointer = pointer;
        yaw -= delta.x * .55f;
        pitch = Mathf.Clamp(pitch + delta.y * .42f, -62f, 62f);
        previewPivot.localRotation = Quaternion.Euler(pitch, yaw, -10f);
    }

    void UseSelection()
    {
        if (equipment.Has(selected))
        {
            if (equipment.TryEquip(selected)) SaveIndicator.ShowToast("Экипирован: " + AxeCatalog.Get(selected).Name);
        }
        else if (selected == AxeId.Gunnar)
        {
            if (equipment.TryRestoreGunnar(forge, table)) SaveIndicator.ShowToast("Топор Гуннара восстановлен");
        }
        else if (equipment.TryBuy(selected, merchant, forge, table))
        {
            SaveIndicator.ShowToast("Экипирован: " + AxeCatalog.Get(selected).Name);
        }
        Refresh();
    }

    void RestoreForge()
    {
        if (equipment.TryRestoreForge(forge)) SaveIndicator.ShowToast("Кузница восстановлена");
        Refresh();
    }

    void Refresh()
    {
        refreshAt = Time.unscaledTime + .15f;
        if (runeView != null && runeView.activeSelf)
        {
            RefreshRunes();
            return;
        }
        var definition = AxeCatalog.Get(selected);
        AxeRuneId rune = equipment.RuneFor(selected);
        DisplayAxe(selected, rune);
        previewCamera.backgroundColor = new Color32(220, 207, 180, 255);
        Set(title, definition.Name);
        Set(details, rune == AxeRuneId.None ? definition.Description : AxeRunes.Get(rune).Effect);
        float power = definition.Power + (rune == AxeRuneId.SharpEdge ? AxeRunes.EdgePower : 0f);
        Set(stats, $"Сила ×{Number(power)}     ·     {Number(PlayerAxeEquipment.SwingInterval(definition, rune))} с между ударами");
        bool owned = equipment.Has(selected);
        bool equipped = equipment.Equipped.Id == selected;
        bool selling = selected == AxeId.Iron && merchant != null || selected == AxeId.Steel && (table != null || forge != null);
        bool restoring = selected == AxeId.Gunnar && forge != null && forge.GunnarBladeAvailable;
        var inventory = equipment.GetComponent<PlayerInventory>();
        string walletText = inventory != null && (merchant != null || forge != null || table != null)
            ? $"{inventory.coinsCount} монет    ·    {inventory.planksCount} досок" : "ЛКМ и перетащи, чтобы повернуть";
        if (table != null && inventory != null && inventory.RuneEssenceDiscovered)
            walletText += $"    ·    Эссенция {inventory.RuneEssence}";
        Set(wallet, walletText);
        Set(status, owned ? rune != AxeRuneId.None ? AxeRunes.Get(rune).Name : equipped ? "В руках у Лео" : "В коллекции"
            : restoring ? "Лезвие получено от Оскара" : definition.Source);

        equipOrBuy.gameObject.SetActive(owned || selling || restoring);
        if (owned)
        {
            Set(purchaseLabel, equipped ? "Экипирован" : "Экипировать");
            equipOrBuy.interactable = !equipped && equipment.CanEquip;
        }
        else if (restoring)
        {
            Set(purchaseLabel, "Восстановить топор");
            equipOrBuy.interactable = equipment.ForgeRestored && equipment.CanEquip;
        }
        else if (selling)
        {
            Set(purchaseLabel, $"Купить · {definition.Price} монет");
            equipOrBuy.interactable = equipment.CanBuy(selected);
        }

        repair.gameObject.SetActive(table == null && forge != null && !equipment.ForgeRestored);
        repair.interactable = equipment.CanRestoreForge;
        forgeRunes.gameObject.SetActive(table != null);
        sawMode.gameObject.SetActive(equipped && rune == AxeRuneId.RunicSaw);
        Set(sawModeLabel, equipment.BoardsMode ? ForgeText("Режим · доски", "Mode · boards") : ForgeText("Режим · брёвна", "Mode · logs"));
        for (int i = 0; i < tabs.Length; i++)
        {
            var id = (AxeId)i;
            tabLabels[i].color = equipment.Has(id) ? UiCandy.Ink : UiCandy.Muted;
            tabs[i].interactable = id != selected;
        }
    }

    static string Number(float value) =>
        value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    static void Set(TMP_Text label, string value)
    {
        if (label.text != value) label.text = value;
    }

    void Build()
    {
        UiInputGuard.EnsureEventSystem();
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            var canvas = new GameObject("Axe canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.layer = 5;
            canvas.transform.SetParent(transform, false);
            var surface = canvas.GetComponent<Canvas>();
            surface.renderMode = RenderMode.ScreenSpaceOverlay;
            surface.sortingOrder = UiLayers.Window;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var veil = UiKit.MakeImage(canvas.transform, "Overlay", null, false);
            veil.color = new Color(.17f, .18f, .36f, .48f);
            veil.raycastTarget = true;
            Stretch(veil.rectTransform);
            panel = veil.gameObject;

            var win = UiCandy.Window(veil.transform, new Vector2(1090, 646), "ТОПОРЫ ЛЕО", Close);
            axeView = win.root.gameObject;
            Prepare(win.close);
            var content = win.content;
            string[] names = { "Старый", "Железный", "Стальной", "Гуннара" };
            for (int i = 0; i < tabs.Length; i++)
            {
                var id = (AxeId)i;
                tabs[i] = MakeButton(content, names[i], UiBtn.Gray, 10 + 254 * i, 9, 246, 62, () => Select(id));
                tabLabels[i] = tabs[i].GetComponentInChildren<TMP_Text>();
            }

            previewImage = MakePreviewImage(content, 10, 86, 474, 414);
            BuildPreview();

            title = Lbl(content, "", 38, UiCandy.Ink, TextAlignmentOptions.Left, true, 514, 92, 500, 56);
            details = UiKit.MakeLabel(content, "", 25, UiCandy.Ink, TextAlignmentOptions.TopLeft, false);
            TL(details.rectTransform, 515, 165, 485, 82);
            details.enableWordWrapping = true;
            stats = Lbl(content, "", 26, UiCandy.Ink, TextAlignmentOptions.Left, true, 514, 260, 492, 43);
            status = Lbl(content, "", 24, UiCandy.Muted, TextAlignmentOptions.Left, false, 514, 309, 492, 38);
            equipOrBuy = MakeButton(content, "", UiBtn.Green, 514, 367, 484, 58, UseSelection);
            purchaseLabel = equipOrBuy.GetComponentInChildren<TMP_Text>();
            repair = MakeButton(content, $"Кузница · {PlayerAxeEquipment.ForgePlanks} досок и {PlayerAxeEquipment.ForgeCoins} монет",
                UiBtn.Amber, 514, 440, 484, 60, RestoreForge);
            repairLabel = repair.GetComponentInChildren<TMP_Text>();
            AutoSize(repairLabel, 18, 25);
            forgeRunes = MakeButton(content, ForgeText("Зачарование", "Enchant"), UiBtn.Amber, 514, 440, 232, 60, ShowRunes);
            sawMode = MakeButton(content, "", UiBtn.Gray, 756, 440, 242, 60, () =>
            {
                equipment.TrySwitchBoardsMode();
                Refresh();
            });
            sawModeLabel = sawMode.GetComponentInChildren<TMP_Text>();
            AutoSize(sawModeLabel, 17, 25);
            wallet = Lbl(content, "", 22, UiCandy.Muted, TextAlignmentOptions.Left, false, 12, 505, 970, 32);
        }
    }

    Button MakeButton(Transform parent, string label, UiBtn style, float x, float y, float w, float h,
        UnityEngine.Events.UnityAction action)
    {
        var button = UiKit.MakeButton(parent, label, style, new Vector2(w, h), 25, 1f);
        TL((RectTransform)button.transform, x, y, w, h);
        button.onClick.AddListener(action);
        Prepare(button);
        return button;
    }

    void Prepare(Button button)
    {
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        UiClickFallback.Register(button, () => open, true);
    }

    static RawImage MakePreviewImage(Transform parent, float x, float y, float width, float height)
    {
        var display = new GameObject("3D axe", typeof(RectTransform), typeof(RawImage)) { layer = PreviewLayer };
        display.transform.SetParent(parent, false);
        var image = display.GetComponent<RawImage>();
        image.raycastTarget = false;
        TL(image.rectTransform, x, y, width, height);
        return image;
    }

    void BuildPreview()
    {
        // A render target refreshes only while the window is open. Keep the display clear of the world cameras.
        previewStage = new GameObject("Axe preview stage");
        previewStage.transform.SetParent(transform, false);
        previewStage.transform.position = new Vector3(0, -1000, 0);

        previewTexture = new RenderTexture(560, 490, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            { name = "Axe preview", antiAliasing = 2 };
        previewTexture.Create();
        previewImage.texture = previewTexture;
        var eye = new GameObject("Preview camera").AddComponent<Camera>();
        previewCamera = eye;
        eye.transform.SetParent(previewStage.transform, false);
        eye.transform.localPosition = new Vector3(0, 0, -2.1f);
        eye.orthographic = true;
        eye.orthographicSize = .405f;
        eye.nearClipPlane = .05f;
        eye.farClipPlane = 5f;
        eye.cullingMask = 1 << PreviewLayer;
        eye.clearFlags = CameraClearFlags.SolidColor;
        eye.backgroundColor = new Color32(220, 207, 180, 255);
        eye.useOcclusionCulling = false;
        eye.allowHDR = false;
        eye.targetTexture = previewTexture;
        var cameraData = eye.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;

        previewPivot = new GameObject("Turntable").transform;
        previewPivot.SetParent(previewStage.transform, false);
        PreviewLight(new Vector3(-.65f, .85f, -1.22f), new Color(1f, .88f, .72f), 3.8f);
        PreviewLight(new Vector3(.8f, .42f, -.26f), new Color(.67f, .84f, 1f), 2.3f);
    }

    void PreviewLight(Vector3 localPosition, Color color, float intensity)
    {
        var light = new GameObject("Preview light").AddComponent<Light>();
        light.transform.SetParent(previewStage.transform, false);
        light.transform.localPosition = localPosition;
        light.type = LightType.Point;
        light.range = 3f;
        light.intensity = intensity;
        light.color = color;
        light.cullingMask = 1 << PreviewLayer;
    }
}
