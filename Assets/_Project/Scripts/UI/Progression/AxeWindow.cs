using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static UiLayout;

[DefaultExecutionOrder(50)]
internal sealed class AxeWindow : MonoBehaviour
{
    const int PreviewLayer = 5;
    static AxeWindow current;
    PlayerAxeEquipment equipment;
    TradingStall merchant;
    StoryFlow forge;
    AxeId selected;
    bool open;
    int openedFrame;
    GameObject panel, previewStage;
    Transform previewPivot;
    GameObject previewModel;
    RenderTexture previewTexture;
    RawImage previewImage;
    TMP_Text title, details, stats, status, purchaseLabel, repairLabel, wallet;
    Button equipOrBuy, repair;
    readonly Button[] tabs = new Button[AxeCatalog.All.Length];
    readonly TMP_Text[] tabLabels = new TMP_Text[AxeCatalog.All.Length];
    bool dragging;
    float refreshAt;
    Vector2 previousPointer;
    float yaw, pitch;

    internal static void Open(PlayerAxeEquipment axes, TradingStall shop = null, StoryFlow workshop = null)
    {
        if (axes == null || !axes.CanEquip || MobileInput.ModalOpen) return;
        if (current == null) current = new GameObject("Axe equipment window").AddComponent<AxeWindow>();
        current.Show(axes, shop, workshop);
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

    void Show(PlayerAxeEquipment axes, TradingStall shop, StoryFlow workshop)
    {
        if (panel == null) Build();
        equipment = axes;
        merchant = shop;
        forge = workshop;
        open = true;
        openedFrame = Time.frameCount;
        panel.SetActive(true);
        previewStage.SetActive(true);
        Select(shop != null ? AxeId.Iron : workshop != null && axes.Has(AxeId.Steel) && workshop.GunnarBladeAvailable
            ? AxeId.Gunnar : workshop != null ? AxeId.Steel : axes.Equipped.Id);
    }

    void Close()
    {
        if (!open) return;
        open = dragging = false;
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
            (forge != null && !forge.CanUseAxeForge(equipment)))
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
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Select((AxeId)(((int)selected + tabs.Length - 1) % tabs.Length));
        if (Input.GetKeyDown(KeyCode.RightArrow)) Select((AxeId)(((int)selected + 1) % tabs.Length));
        RotatePreview();
        if (Time.unscaledTime >= refreshAt) Refresh();
    }

    void Select(AxeId id)
    {
        selected = id;
        if (previewModel != null)
        {
            previewModel.SetActive(false);
            Destroy(previewModel);
            previewModel = null;
        }
        yaw = 73f;
        pitch = 7f;
        previewPivot.localRotation = Quaternion.Euler(pitch, yaw, 6f);
        var model = AxeCatalog.Get(id).Model;
        if (model != null)
        {
            previewModel = Instantiate(model, previewPivot, false);
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            previewModel.transform.localPosition = -mesh.bounds.center;
            foreach (Transform child in previewModel.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = PreviewLayer;
        }
        Refresh();
    }

    void RotatePreview()
    {
        var pointer = (Vector2)Input.mousePosition;
        if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(previewImage.rectTransform, pointer))
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
        previewPivot.localRotation = Quaternion.Euler(pitch, yaw, 6f);
    }

    void UseSelection()
    {
        if (equipment.Has(selected))
        {
            if (equipment.TryEquip(selected)) SaveIndicator.ShowToast("Экипирован: " + AxeCatalog.Get(selected).Name);
        }
        else if (selected == AxeId.Gunnar)
        {
            if (equipment.TryRestoreGunnar(forge)) SaveIndicator.ShowToast("Топор Гуннара восстановлен");
        }
        else if (equipment.TryBuy(selected, merchant, forge))
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
        var definition = AxeCatalog.Get(selected);
        Set(title, definition.Name);
        Set(details, definition.Description);
        Set(stats, $"Сила ×{Number(definition.Power)}     ·     {Number(definition.Interval)} с между ударами");
        bool owned = equipment.Has(selected);
        bool equipped = equipment.Equipped.Id == selected;
        bool selling = selected == AxeId.Iron && merchant != null || selected == AxeId.Steel && forge != null;
        bool restoring = selected == AxeId.Gunnar && forge != null && forge.GunnarBladeAvailable;
        var inventory = equipment.GetComponent<PlayerInventory>();
        string walletText = inventory != null && (merchant != null || forge != null)
            ? $"{inventory.coinsCount} монет    ·    {inventory.planksCount} досок" : "ЛКМ и перетащи, чтобы повернуть";
        if (forge != null && equipment.ForgeRestored && PlayerSkills.Has(SkillIds.ForgeDiscount))
            walletText += $"    ·    Цена зачарования при открытии: {AxeCatalog.EnchantmentCoinCost(40)} / {AxeCatalog.EnchantmentCoinCost(100)} монет";
        Set(wallet, walletText);
        Set(status, owned ? equipped ? "В руках у Лео" : "В коллекции"
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

        repair.gameObject.SetActive(forge != null && !equipment.ForgeRestored);
        repair.interactable = equipment.CanRestoreForge;
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
            Prepare(win.close);
            var content = win.content;
            string[] names = { "Старый", "Железный", "Стальной", "Гуннара" };
            for (int i = 0; i < tabs.Length; i++)
            {
                var id = (AxeId)i;
                tabs[i] = MakeButton(content, names[i], UiBtn.Gray, 10 + 254 * i, 9, 246, 62, () => Select(id));
                tabLabels[i] = tabs[i].GetComponentInChildren<TMP_Text>();
            }

            var display = new GameObject("3D model", typeof(RectTransform), typeof(RawImage));
            display.layer = 5;
            display.transform.SetParent(content, false);
            previewImage = display.GetComponent<RawImage>();
            TL(previewImage.rectTransform, 10, 86, 474, 414);
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
