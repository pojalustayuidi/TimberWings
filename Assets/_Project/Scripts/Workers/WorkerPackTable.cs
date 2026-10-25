// Assets/_Project/Scripts/Workers/WorkerPackTable.cs (НЕ в Editor)
// «Стол пака»: открытие пака рабочих в режиме «выбери одну из трёх карт» (как выбор карты в FIFA).
// Обычный класс без MonoBehaviour: его создаёт WorkersWindow внутри своей панели и вызывает Tick(dt) каждый кадр.
//
// Ход (все числа времени ниже в секундах):
//   Раздача (1.15)   пак трясётся, из него веером вылетают три карты рубашкой вверх;
//   Выбор            карты слегка парят, под курсором поднимаются и светятся цветом пака. Клик, цифры 1 2 3 или стрелки + Enter.
//                    Монеты НЕ списаны: Esc возвращает в окно без потерь;
//   Переворот (2.3)  выбранная карта выезжает в центр, остальные гаснут, карта переворачивается со вспышкой цвета редкости
//                    (лучи у Необычного и выше, тряска окна у Редкого и выше), затем уезжает влево, справа появляется описание,
//                    а две невыбранные карты внизу переворачиваются «могло выпасть» (они настоящие, см. WorkerPackSystem.Draft);
//   Итог             «Ещё» (Enter) открывает следующий такой же пак, «Готово» (E, Esc) закрывает.
// E, Esc, Enter или клик пропускают анимацию (пропуск не меняет результат: он выбран до анимации).
// CANDY (задача 13): светлое облако вместо тёмного стола, подарочная коробка вместо сундука, пастельные рубашки карт, текст индиго, конфетти CandyFx.
// Таблица создаётся внутри окна рабочих под скином Candy; во время игры иконки берутся через UiKit.CandyIcon.
// Честность: все три карты брошены по шансам пака, гарантия действует на каждую, поэтому выбор — это ритуал и азарт,
// а не способ повысить шансы. Об этом сказано строкой на экране выбора.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

public class WorkerPackTable
{
    public enum Stage { Idle, Deal, Choose, Flip, Reveal }

    // колбэки окна
    public Action onClosed;      // стол закрыт (окно обновляет вкладки и монеты)
    public Action onPaid;        // монеты списаны, рабочий выдан (окно помечает себя грязным)
    public Action onShake;       // тряхнуть окно (Редкий и выше)

    public bool Active { get { return stage != Stage.Idle; } }

    // ============================================================ размеры (координаты от центра панели)
    const float CW = 280f, CH = 380f;
    const float DealDur = 1.15f, FlipEnd = 2.3f;
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    static readonly Vector2 PackPos = new Vector2(0f, 215f);
    static readonly Vector2 ResultPos = new Vector2(-380f, 60f);
    static readonly Vector2[] SmallPos = { new Vector2(-450f, -285f), new Vector2(-310f, -285f) };
    static readonly Vector2[] Homes = { new Vector2(-340f, 10f), new Vector2(0f, 10f), new Vector2(340f, 10f) };
    static readonly float[] HomeRot = { 7f, 0f, -7f };

    static readonly Color Soft = WorkerUi.Soft;
    static readonly Color Dim = WorkerUi.Dim;

    class Slot
    {
        public RectTransform rt, body;
        public CanvasGroup cg;
        public Image halo;
        public GameObject back;
        public Image backFrame, backFill, backTop, backShine, backMedal;
        public TMP_Text backQ, backName;
        public WorkerCardView front;
        public bool faceUp;
        public float hover;
        public Vector2 home;
        public float homeRot;
    }

    class Spark { public Image img; public RectTransform rt; public Vector2 p, v; public float life, max; public bool live; }

    // ============================================================ состояние
    Stage stage = Stage.Idle;
    float t;
    WorkerPackType type;
    PackDraft draft;
    PackResult res;
    int pick = -1, focus = 1;
    bool burstDone, flashDone;
    readonly bool[] dealt = new bool[3];
    Vector2 lastMouse;
    string notice = "";

    // ============================================================ элементы
    readonly RectTransform root;
    readonly Slot[] slots = new Slot[3];
    Image bgRays, rays, glow, flash, packArt, chestBodyImg, chestLidImg;
    RectTransform raysRt, glowRt, packRt;
    TMP_Text titleT, subT, priceT, honestT, hintT, smallLabel;
    CanvasGroup infoCg;
    Image rTag, rGem, rRoleIcon;
    TMP_Text rTagText, rName, rRarity, rRole, rLevel, rLine, rPerks, rBio;
    readonly Image[] rStars = new Image[5];
    Button againBtn, doneBtn;
    readonly Spark[] sparks = new Spark[48];

    public WorkerPackTable(RectTransform parent)
    {
        root = NewRT("PackTable", parent);
        Stretch(root);

        var dim = UiKit.MakeImage(root, "Dim", null, false);
        dim.color = new Color(0.96f, 0.97f, 1f, 0.97f);   // светлое облако
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        bgRays = UiKit.MakeImage(root, "BgRays", PetUi.RaysSprite, false);
        Anch(bgRays.rectTransform, Mid, Mid, new Vector2(0f, 10f), new Vector2(1500f, 1500f));
        bgRays.raycastTarget = false;

        rays = UiKit.MakeImage(root, "Rays", PetUi.RaysSprite, false);
        raysRt = rays.rectTransform;
        Anch(raysRt, Mid, Mid, ResultPos, new Vector2(860f, 860f));
        rays.raycastTarget = false;
        rays.gameObject.SetActive(false);

        glow = UiKit.MakeImage(root, "Glow", PetUi.GlowSprite, false);
        glowRt = glow.rectTransform;
        Anch(glowRt, Mid, Mid, ResultPos, new Vector2(720f, 720f));
        glow.raycastTarget = false;

        // Подарочная коробка вместо сундука: мягкая тень, корпус, крышка, белая лента, бант-камень. Цвета корпуса и крышки подбираются под пак в Begin
        packArt = UiKit.MakeImage(root, "PackArt_Glow", PetUi.GlowSprite, false);
        packRt = packArt.rectTransform;
        Anch(packRt, Mid, Mid, PackPos, new Vector2(250f, 250f));
        packArt.color = new Color(0.45f, 0.74f, 1f, 0.3f);
        packArt.raycastTarget = false;
        var chest = NewRT("PackChest", root);
        Anch(chest, Mid, Mid, PackPos + new Vector2(0f, -4f), new Vector2(148f, 112f));
        var chestShadow = UiKit.MakeImage(chest, "Shadow", UiKit.Slot, true, 0.8f);
        Anch(chestShadow.rectTransform, Mid, Mid, new Vector2(0f, -50f), new Vector2(150f, 34f));
        chestShadow.color = new Color(UiCandy.Ink.r, UiCandy.Ink.g, UiCandy.Ink.b, 0.16f);
        chestBodyImg = UiKit.MakeImage(chest, "Box", UiKit.Panel, true, 0.5f);
        Anch(chestBodyImg.rectTransform, Mid, Mid, new Vector2(0f, -12f), new Vector2(128f, 80f));
        chestBodyImg.color = UiCandy.Peach;
        chestLidImg = UiKit.MakeImage(chest, "Lid", UiKit.Panel, true, 0.4f);
        Anch(chestLidImg.rectTransform, Mid, Mid, new Vector2(0f, 26f), new Vector2(144f, 40f));
        chestLidImg.color = UiCandy.PeachDeep;
        var chestBand = UiKit.MakeImage(chest, "Ribbon", UiDraw.Pill, true, 0.3f);
        Anch(chestBand.rectTransform, Mid, Mid, new Vector2(0f, 0f), new Vector2(18f, 104f));
        chestBand.color = new Color(1f, 1f, 1f, 0.92f);
        var chestLock = UiKit.MakeImage(chest, "GemBow", UiKit.CandyIcon(UiIcon.Gem), false);
        Anch(chestLock.rectTransform, Mid, Mid, new Vector2(0f, 34f), new Vector2(40f, 40f));
        chestLock.raycastTarget = false;

        for (int i = 0; i < 3; i++) slots[i] = MakeSlot(i);

        BuildInfo();

        var fx = NewRT("Fx", root);
        Stretch(fx);
        for (int i = 0; i < sparks.Length; i++)
        {
            var im = UiKit.MakeImage(fx, "Spark", PetUi.SparkSprite, false);
            im.raycastTarget = false;
            Anch(im.rectTransform, Mid, Mid, Vector2.zero, new Vector2(16f, 16f));
            im.gameObject.SetActive(false);
            sparks[i] = new Spark { img = im, rt = im.rectTransform };
        }

        flash = UiKit.MakeImage(root, "Flash", null, false);
        Stretch(flash.rectTransform);
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;

        titleT = UiKit.MakeLabel(root, "", 46f, UiCandy.Ink, TextAlignmentOptions.Center, true);
        Anch(titleT.rectTransform, Mid, Mid, new Vector2(0f, 355f), new Vector2(1000f, 58f));
        subT = UiKit.MakeLabel(root, "", 25f, Soft, TextAlignmentOptions.Center, false);
        Anch(subT.rectTransform, Mid, Mid, new Vector2(0f, 308f), new Vector2(1100f, 36f));
        priceT = UiKit.MakeLabel(root, "", 28f, UiCandy.Ink, TextAlignmentOptions.Center, true);
        Anch(priceT.rectTransform, Mid, Mid, new Vector2(0f, -262f), new Vector2(900f, 40f));
        honestT = UiKit.MakeLabel(root, "", 21f, Soft, TextAlignmentOptions.Center, false);
        Anch(honestT.rectTransform, Mid, Mid, new Vector2(0f, -304f), new Vector2(1250f, 32f));
        hintT = UiKit.MakeLabel(root, "", 21f, Soft, TextAlignmentOptions.Center, false);
        Anch(hintT.rectTransform, Mid, Mid, new Vector2(0f, -385f), new Vector2(1250f, 30f));
        smallLabel = UiKit.MakeLabel(root, "Остальные карты: могло выпасть", 21f, Soft, TextAlignmentOptions.Center, false);
        Anch(smallLabel.rectTransform, Mid, Mid, new Vector2(-380f, -152f), new Vector2(420f, 28f));
        foreach (var l in new[] { titleT, subT, priceT, honestT, hintT, smallLabel }) l.raycastTarget = false;

        root.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ карта: рубашка + лицо
    Slot MakeSlot(int i)
    {
        var s = new Slot();
        s.home = Homes[i];
        s.homeRot = HomeRot[i];

        s.rt = NewRT("Card" + i, root);
        Anch(s.rt, Mid, Mid, s.home, new Vector2(CW, CH));
        s.cg = s.rt.gameObject.AddComponent<CanvasGroup>();
        s.cg.blocksRaycasts = false;

        s.halo = UiKit.MakeImage(s.rt, "Halo", PetUi.GlowSprite, false);
        Anch(s.halo.rectTransform, Mid, Mid, Vector2.zero, new Vector2(CW * 1.9f, CH * 1.55f));
        s.halo.color = new Color(1f, 1f, 1f, 0f);
        s.halo.raycastTarget = false;

        s.body = NewRT("Body", s.rt);
        Stretch(s.body);

        // рубашка
        var back = NewRT("Back", s.body);
        Stretch(back);
        s.back = back.gameObject;
        s.backFrame = UiKit.MakeImage(back, "Frame", UiKit.Slot, true, 1f);
        Stretch(s.backFrame.rectTransform);
        s.backFill = UiKit.MakeImage(back, "Fill", null, false);
        Inset(s.backFill.rectTransform, 7f);
        s.backTop = UiKit.MakeImage(back, "Top", null, false);
        var tb = s.backTop.rectTransform;
        tb.anchorMin = new Vector2(0f, 1f); tb.anchorMax = new Vector2(1f, 1f); tb.pivot = new Vector2(0.5f, 1f);
        tb.offsetMin = new Vector2(7f, -7f - 12f); tb.offsetMax = new Vector2(-7f, -7f);
        s.backShine = UiKit.MakeImage(back, "Shine", PetUi.GlowSprite, false);
        Anch(s.backShine.rectTransform, Mid, Mid, new Vector2(0f, 30f), new Vector2(CW * 1.1f, CW * 1.1f));
        s.backMedal = UiKit.MakeImage(back, "Medal", UiKit.Medallion, false);
        Anch(s.backMedal.rectTransform, Mid, Mid, new Vector2(0f, 24f), new Vector2(150f, 150f));
        s.backQ = UiKit.MakeLabel(s.backMedal.transform, "?", 110f, UiCandy.Ink, TextAlignmentOptions.Center, true);
        s.backQ.raycastTarget = false;
        // Внутренний двойной контур и центральная вертикаль создают ощущение гравированной рубашки.
        var innerLine = UiKit.MakeImage(back, "InnerLine", null, false);
        Anch(innerLine.rectTransform, Mid, Mid, Vector2.zero, new Vector2(CW - 28f, CH - 28f));
        innerLine.color = new Color(1f, 1f, 1f, 0.55f);
        innerLine.raycastTarget = false;
        var sigil = UiKit.MakeImage(back, "CentralSigil", UiKit.CandyIcon(UiIcon.Gem), false);
        Anch(sigil.rectTransform, Mid, Mid, new Vector2(0f, 24f), new Vector2(62f, 62f));
        sigil.color = new Color(1f, 1f, 1f, 0.95f);
        sigil.raycastTarget = false;
        for (int k = 0; k < 4; k++)
        {
            var st = UiKit.MakeIcon(back, UiIcon.Star, 26f, "Corner" + k);
            float sx = (k % 2 == 0 ? -1f : 1f) * (CW * 0.5f - 34f), sy = (k < 2 ? 1f : -1f) * (CH * 0.5f - 52f);
            Anch(st.rectTransform, Mid, Mid, new Vector2(sx, sy), new Vector2(26f, 26f));
            st.color = new Color(1f, 1f, 1f, 0.75f);
            st.raycastTarget = false;
        }
        s.backName = UiKit.MakeLabel(back, "", 24f, UiCandy.Ink, TextAlignmentOptions.Center, true);
        Anch(s.backName.rectTransform, Mid, Mid, new Vector2(0f, -135f), new Vector2(CW - 30f, 34f));
        s.backName.raycastTarget = false;
        foreach (var g in back.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        // лицо
        s.front = WorkerCardView.Create(s.body, CW, CH, "Front");
        Anch(s.front.rt, Mid, Mid, Vector2.zero, new Vector2(CW, CH));
        s.front.frame.raycastTarget = false;
        s.front.rt.gameObject.SetActive(false);
        return s;
    }

    static void Inset(RectTransform r, float v)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(v, v); r.offsetMax = new Vector2(-v, -v);
    }

    // ------------------------------------------------------------ описание рабочего (справа) и кнопки
    void BuildInfo()
    {
        var info = NewRT("Info", root);
        Anch(info, Mid, Mid, new Vector2(300f, 120f), new Vector2(640f, 440f));
        infoCg = info.gameObject.AddComponent<CanvasGroup>();
        var b = info.transform;

        rTag = UiKit.MakeImage(b, "Tag", null, false);
        TL(rTag.rectTransform, 0f, 0f, 470f, 44f);
        rTag.raycastTarget = false;
        rTagText = UiKit.MakeLabel(rTag.transform, "", 26f, Color.white, TextAlignmentOptions.Center, true);
        AutoSize(rTagText, 14f, 26f);

        rName = Lbl(b, "", 40f, UiCandy.Ink, TextAlignmentOptions.Left, true, 0f, 56f, 640f, 54f);
        rGem = Ic(b, UiIcon.Gem, 20f, 138f, 34f);
        rRarity = Lbl(b, "", 30f, UiCandy.Ink, TextAlignmentOptions.Left, true, 46f, 118f, 230f, 40f);
        for (int i = 0; i < 5; i++) rStars[i] = Ic(b, UiIcon.Star, 300f + i * 32f, 138f, 28f);
        rRoleIcon = Ic(b, UiIcon.Coin, 20f, 184f, 32f);
        rRole = Lbl(b, "", 26f, UiCandy.Ink, TextAlignmentOptions.Left, true, 46f, 166f, 590f, 36f);
        rLevel = Lbl(b, "", 24f, Soft, TextAlignmentOptions.Left, false, 0f, 208f, 640f, 32f);
        rLine = Wrap(b, 25f, UiCandy.Ink, true, 0f, 246f, 640f, 84f);
        rPerks = Wrap(b, 21f, Soft, false, 0f, 336f, 640f, 56f);
        rBio = Wrap(b, 20f, Soft, false, 0f, 394f, 640f, 46f);
        rBio.fontStyle = FontStyles.Italic;
        foreach (var g in info.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        againBtn = UiKit.MakeButton(root, "Ещё", UiBtn.Amber, new Vector2(300f, 56f), 26f, 1f);
        Anch(againBtn.GetComponent<RectTransform>(), Mid, Mid, new Vector2(300f, -335f), new Vector2(300f, 56f));
        againBtn.onClick.AddListener(Again);
        againBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        doneBtn = UiKit.MakeButton(root, "Готово", UiBtn.Gray, new Vector2(200f, 56f), 26f, 1f);
        Anch(doneBtn.GetComponent<RectTransform>(), Mid, Mid, new Vector2(560f, -335f), new Vector2(200f, 56f));
        doneBtn.onClick.AddListener(Close);
        doneBtn.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    static Image Ic(Transform p, UiIcon icon, float cx, float cy, float size)
    {
        var im = UiKit.MakeIcon(p, icon, size, "Icon");
        TLC(im.rectTransform, cx, cy, size, size);
        im.raycastTarget = false;
        return im;
    }

    static TMP_Text Wrap(Transform p, float size, Color c, bool bold, float x, float y, float w, float h)
    {
        var tx = UiKit.MakeLabel(p, "", size, c, TextAlignmentOptions.TopLeft, bold);
        TL(tx.rectTransform, x, y, w, h);
        tx.enableAutoSizing = true;
        tx.fontSizeMin = Mathf.Max(10f, size * 0.55f);
        tx.fontSizeMax = size;
        tx.enableWordWrapping = true;
        return tx;
    }

    // ============================================================ вход и выход
    // Раздать карты. Ничего не списывается. Не Ok: причину показывает окно.
    public PackOpenResult Begin(WorkerPackType t0, WorkerManager m)
    {
        if (m == null) return PackOpenResult.NoPlayer;
        PackDraft d;
        var why = m.TryDraftPack(t0, out d);
        if (why != PackOpenResult.Ok) return why;

        draft = d;
        type = t0;
        pick = -1;
        focus = 1;
        t = 0f;
        notice = "";
        burstDone = flashDone = false;
        for (int i = 0; i < 3; i++) dealt[i] = false;
        lastMouse = Input.mousePosition;

        Color pc = WorkerUi.PackColor(type);
        chestBodyImg.color = Color.Lerp(Color.white, pc, 0.55f);
        chestLidImg.color = Color.Lerp(Color.white, pc, 0.9f);
        packArt.color = new Color(pc.r, pc.g, pc.b, 0.3f);
        root.gameObject.SetActive(true);
        stage = Stage.Deal;

        for (int i = 0; i < 3; i++)
        {
            var s = slots[i];
            ShowFace(s, false);
            s.hover = 0f;
            s.front.Set(d.defs[i], null, false);
            s.backFill.color = Color.Lerp(Color.white, pc, 0.45f);
            s.backTop.color = pc;
            s.backShine.color = new Color(1f, 1f, 1f, 0.5f);
            s.backMedal.color = Color.Lerp(Color.white, pc, 0.35f);
            s.backFrame.color = Color.Lerp(Color.white, pc, 0.55f);
            s.backName.text = WorkerText.Pack(type);
            s.halo.color = new Color(pc.r, pc.g, pc.b, 0f);
            Place(s, PackPos, 0f, 0.25f, 1f, 0f);
        }

        packArt.gameObject.SetActive(true);
        packArt.color = Color.Lerp(Color.white, pc, 0.35f);
        packRt.anchoredPosition = PackPos;
        packRt.localScale = Vector3.one;
        bgRays.color = new Color(pc.r, pc.g, pc.b, 0.10f);
        glow.color = new Color(pc.r, pc.g, pc.b, 0.12f);
        glowRt.anchoredPosition = new Vector2(0f, 10f);
        glowRt.localScale = Vector3.one * 1.8f;
        rays.gameObject.SetActive(false);
        flash.color = new Color(1f, 1f, 1f, 0f);
        infoCg.alpha = 0f;
        infoCg.interactable = false; infoCg.blocksRaycasts = false;
        SetButtons(false);
        smallLabel.gameObject.SetActive(false);

        int price = m.PackPrice(type);
        titleT.text = "Раздаём карты…";
        subT.text = WorkerText.Pack(type);
        priceT.text = "";
        honestT.text = "";
        hintT.text = "Enter или клик: пропустить";
        for (int i = 0; i < sparks.Length; i++) { sparks[i].live = false; sparks[i].img.gameObject.SetActive(false); }
        Tick1();
        return PackOpenResult.Ok;
    }

    static void Tick1() { if (SfxManager.Instance != null) SfxManager.Play("ui"); }

    // Скрыть без колбэков (окно закрылось или сменило вкладку)
    public void Hide()
    {
        stage = Stage.Idle;
        draft = null;
        if (root != null) root.gameObject.SetActive(false);
    }

    void Close()
    {
        Hide();
        if (onClosed != null) onClosed();
    }

    void Again()
    {
        if (stage != Stage.Reveal) return;
        var m = WorkerManager.Instance;
        if (m == null) { Close(); return; }
        var tp = type;
        if (Begin(tp, m) != PackOpenResult.Ok) Close();
    }

    // ============================================================ ввод от окна
    // Esc и E: шаг назад
    public void Back()
    {
        switch (stage)
        {
            case Stage.Flip: Skip(); break;
            case Stage.Idle: break;
            default: Close(); break;     // Раздача, Выбор: монеты не потрачены; Итог: закрыть
        }
    }

    public void HandleKeys()
    {
        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        switch (stage)
        {
            case Stage.Deal:
                if (enter || Input.GetKeyDown(KeyCode.Space)) SkipDeal();
                break;
            case Stage.Choose:
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) focus = (focus + 2) % 3;
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) focus = (focus + 1) % 3;
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Choose(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Choose(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Choose(2);
                else if (enter || Input.GetKeyDown(KeyCode.Space)) Choose(focus);
                break;
            case Stage.Flip:
                if (enter || Input.GetKeyDown(KeyCode.Space)) Skip();
                break;
            case Stage.Reveal:
                if (enter) Again();
                break;
        }
    }

    // ============================================================ ход времени
    public void Tick(float dt)
    {
        if (stage == Stage.Idle) return;
        t += dt;
        bgRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 4f);

        switch (stage)
        {
            case Stage.Deal:
                PoseDeal(t);
                if (t >= DealDur) EnterChoose();
                break;
            case Stage.Choose:
                PoseChoose(dt);
                HandleMouse();
                break;
            case Stage.Flip:
                PoseFlip(t);
                if (t >= FlipEnd) EnterReveal();
                break;
            case Stage.Reveal:
                if (rays.gameObject.activeSelf) raysRt.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 18f);
                var c = slots[pick];
                if (c.halo != null)
                {
                    Color acc = WorkerUi.RarityAccent(res.rarity);
                    float pulse = 0.40f + 0.10f * Mathf.Sin(Time.unscaledTime * 2.4f);
                    c.halo.color = new Color(acc.r, acc.g, acc.b, pulse);
                }
                break;
        }
        TickSparks(dt);
    }

    // ------------------------------------------------------------ раздача
    void PoseDeal(float tt)
    {
        Color pc = WorkerUi.PackColor(type);

        // пак: трясётся 0..0.4, затем растворяется
        if (tt < 0.4f)
        {
            float k = tt / 0.4f;
            packRt.anchoredPosition = PackPos + new Vector2(Mathf.Sin(tt * 70f) * (3f + 9f * k), Mathf.Sin(tt * 53f) * 3f * k);
            packRt.localScale = Vector3.one * (1f + 0.18f * k);
        }
        else
        {
            float k = Mathf.Clamp01((tt - 0.4f) / 0.45f);
            packRt.anchoredPosition = PackPos;
            packRt.localScale = Vector3.one * (1.18f + 0.5f * k);
            var c = packArt.color; c.a = 1f - k; packArt.color = c;
            if (k >= 1f) packArt.gameObject.SetActive(false);
        }

        for (int i = 0; i < 3; i++)
        {
            var s = slots[i];
            float start = 0.35f + i * 0.17f;
            float k = Mathf.Clamp01((tt - start) / 0.55f);
            if (k > 0f && !dealt[i])
            {
                dealt[i] = true;
                for (int n = 0; n < 8; n++)
                {
                    float a = UnityEngine.Random.value * Mathf.PI * 2f;
                    SpawnSpark(PackPos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(90f, 260f), Color.Lerp(pc, Color.white, 0.4f), UnityEngine.Random.Range(0.4f, 0.8f), UnityEngine.Random.Range(10f, 20f));
                }
                if (SfxManager.Instance != null) SfxManager.Play("ui", 0.6f, 1.1f + 0.1f * i);
            }
            float e = UiDraw.EaseOutBack(k);
            Vector2 pos = Vector2.LerpUnclamped(PackPos, s.home, e);
            float rot = Mathf.LerpUnclamped(0f, s.homeRot, e);
            float sc = Mathf.LerpUnclamped(0.25f, 1f, e);
            Place(s, pos, rot, sc, 1f, Mathf.Clamp01(k * 3f));
        }
    }

    void SkipDeal()
    {
        t = DealDur;
        PoseDeal(DealDur);
        EnterChoose();
    }

    void EnterChoose()
    {
        stage = Stage.Choose;
        t = 0f;
        packArt.gameObject.SetActive(false);

        var m = WorkerManager.Instance;
        int price = m != null ? m.PackPrice(type) : 0;
        titleT.text = "Выбери карту";
        subT.text = WorkerText.Pack(type);
        priceT.text = price <= 0 ? "Подарочный пак: бесплатно" : "Цена: " + price + " монет. Спишутся, когда выберешь карту";
        priceT.color = price <= 0 ? UiCandy.Done : UiCandy.Ink;
        honestT.text = "Все три карты настоящие и брошены по шансам этого пака. Остальные две покажем после выбора.";
        hintT.text = "← →  или  1 2 3: выбрать   ·   Enter или клик: взять карту   ·   Esc: назад (монеты не потрачены)";
    }

    // ------------------------------------------------------------ выбор
    void PoseChoose(float dt)
    {
        Color pc = WorkerUi.PackColor(type);
        Vector2 lp;
        for (int i = 0; i < 3; i++)
        {
            var s = slots[i];
            float target = i == focus ? 1f : 0f;
            s.hover = Mathf.MoveTowards(s.hover, target, dt * 6f);
            float h = UiDraw.EaseOutCubic(s.hover);

            float bob = Mathf.Sin(Time.unscaledTime * 1.6f + i * 1.9f) * 6f * (1f - h);
            float tilt = 0f;
            if (i == focus && RectTransformUtility.ScreenPointToLocalPointInRectangle(s.rt, Input.mousePosition, null, out lp) && HoveredIndex() == i)
                tilt = Mathf.Clamp(-lp.x / (CW * 0.5f), -1f, 1f) * 5f;

            Vector2 pos = s.home + new Vector2(0f, bob + 24f * h);
            float rot = Mathf.Lerp(s.homeRot, tilt, h);
            Place(s, pos, rot, 1f + 0.10f * h, 1f, Mathf.Lerp(0.80f, 1f, h));
            s.halo.color = new Color(pc.r, pc.g, pc.b, 0.55f * h * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f)));
            s.backShine.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 1.3f + i) * 12f, 30f + Mathf.Cos(Time.unscaledTime * 1.1f + i) * 8f);
            s.backShine.color = new Color(1f, 1f, 1f, 0.12f + 0.12f * h);
        }
        glow.color = new Color(pc.r, pc.g, pc.b, 0.10f);
    }

    int HoveredIndex()
    {
        Vector2 mp = Input.mousePosition;
        for (int i = 0; i < 3; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(slots[i].rt, mp, null)) return i;
        return -1;
    }

    void HandleMouse()
    {
        Vector2 mp = Input.mousePosition;
        int h = HoveredIndex();
        if ((mp - lastMouse).sqrMagnitude > 1f)
        {
            lastMouse = mp;
            if (h >= 0) focus = h;
        }
        if (Input.GetMouseButtonDown(0) && h >= 0) Choose(h);
    }

    // Игрок выбрал карту: здесь списываются монеты и применяется результат
    void Choose(int index)
    {
        if (stage != Stage.Choose || draft == null) return;
        var m = WorkerManager.Instance;
        if (m == null) return;

        PackResult r;
        var why = m.TryPickCard(draft, index, out r);
        if (why != PackOpenResult.Ok)
        {
            priceT.text = Why(why, m.PackPrice(type));
            priceT.color = UiCandy.GoldText;
            if (SfxManager.Instance != null) SfxManager.Play("ui", 0.5f, 0.6f);
            return;
        }

        res = r;
        pick = index;
        stage = Stage.Flip;
        t = 0f;
        burstDone = flashDone = false;
        Tick1();

        titleT.text = "";
        subT.text = "";
        priceT.text = "";
        honestT.text = "";
        hintT.text = "Enter, E или Esc: пропустить";

        // лицо выбранной карты показывает рабочего уже после выдачи (уровень, опыт)
        slots[pick].front.Set(r.def, r.worker, false);
        for (int i = 0; i < 3; i++)
        {
            if (i == pick) continue;
            slots[i].front.Set(draft.defs[i], null, false);
        }
        FillInfo(m);
        if (onPaid != null) onPaid();
    }

    static string Why(PackOpenResult why, int price)
    {
        switch (why)
        {
            case PackOpenResult.Locked: return "Пак ещё закрыт: нужен уровень выше";
            case PackOpenResult.NoCoins: return "Не хватает монет: нужно " + price;
            case PackOpenResult.NoDatabase: return "Не назначена база рабочих";
            default: return "Нет игрока";
        }
    }

    // ------------------------------------------------------------ переворот
    int Other(int j)
    {
        int n = 0;
        for (int i = 0; i < 3; i++)
        {
            if (i == pick) continue;
            if (n == j) return i;
            n++;
        }
        return 0;
    }

    void PoseFlip(float tt)
    {
        Color acc = WorkerUi.RarityAccent(res.rarity);
        var c = slots[pick];
        Vector2 center = new Vector2(0f, 40f);

        Vector2 pos; float sc, rot;
        if (tt < 0.45f)
        {
            float k = UiDraw.EaseOutCubic(tt / 0.45f);
            pos = Vector2.Lerp(c.home, center, k); sc = Mathf.Lerp(1f, 1.3f, k); rot = Mathf.Lerp(c.homeRot, 0f, k);
        }
        else if (tt < 1.4f) { pos = center; sc = 1.3f; rot = 0f; }
        else
        {
            float k = UiDraw.EaseOutCubic(Mathf.Clamp01((tt - 1.4f) / 0.7f));
            pos = Vector2.Lerp(center, ResultPos, k); sc = Mathf.Lerp(1.3f, 1f, k); rot = 0f;
        }

        bool up = tt >= 0.8f;
        float flip;
        if (tt < 0.55f) flip = 1f;
        else if (tt < 0.8f) { float k = (tt - 0.55f) / 0.25f; flip = 1f - k * k; }
        else flip = UiDraw.EaseOutBack(Mathf.Clamp01((tt - 0.8f) / 0.45f));
        flip = Mathf.Max(0.001f, flip);
        if (up != c.faceUp) ShowFace(c, up);
        Place(c, pos, rot, sc, flip, 1f);
        c.halo.color = new Color(acc.r, acc.g, acc.b, up ? 0.45f : 0f);

        // момент переворота: вспышка, искры, тряска
        if (up && !burstDone) Burst(true);
        if (flashDone || up)
        {
            float fk = Mathf.Clamp01((tt - 0.8f) / 0.5f);
            float fa = res.rarity >= WorkerRarity.Legendary ? 0.9f : res.rarity >= WorkerRarity.Rare ? 0.7f : 0.5f;
            flash.color = new Color(acc.r, acc.g, acc.b, fa * (1f - fk));
        }

        // свечение и лучи следуют за картой
        glowRt.anchoredPosition = pos;
        glowRt.localScale = Vector3.one * (up ? 1f : 0.8f);
        glow.color = new Color(acc.r, acc.g, acc.b, up ? 0.35f : 0.12f);
        bool wantRays = up && res.rarity >= WorkerRarity.Uncommon;
        if (rays.gameObject.activeSelf != wantRays) rays.gameObject.SetActive(wantRays);
        if (wantRays)
        {
            raysRt.anchoredPosition = pos;
            raysRt.localRotation = Quaternion.Euler(0f, 0f, -tt * 18f);
            rays.color = new Color(acc.r, acc.g, acc.b, 0.55f * Mathf.Clamp01((tt - 0.8f) / 0.5f));
        }

        // невыбранные: гаснут и расходятся, потом внизу маленькими переворачиваются «могло выпасть»
        for (int j = 0; j < 2; j++)
        {
            var s = slots[Other(j)];
            float popAt = 1.55f + 0.14f * j;
            if (tt < 0.45f)
            {
                float k = tt / 0.45f;
                float dir = s.home.x < 0f ? -1f : 1f;
                ShowFace(s, false);
                Place(s, s.home + new Vector2(dir * 70f * k, 0f), s.homeRot, 1f - 0.1f * k, 1f, 1f - k);
                s.halo.color = new Color(1f, 1f, 1f, 0f);
            }
            else if (tt < popAt)
            {
                Place(s, SmallPos[j], 0f, 0.2f, 1f, 0f);
            }
            else
            {
                float pk = Mathf.Clamp01((tt - popAt) / 0.3f);
                float flipStart = popAt + 0.15f + 0.1f * j;
                float f = Mathf.Clamp01((tt - flipStart) / 0.4f);
                bool faceUp = f >= 0.5f;
                float fx = f < 0.5f ? 1f - f * 2f : (f - 0.5f) * 2f;
                if (faceUp != s.faceUp) ShowFace(s, faceUp);
                float scale = Mathf.LerpUnclamped(0.2f, 0.5f, UiDraw.EaseOutBack(pk));
                Place(s, SmallPos[j], 0f, scale, Mathf.Max(0.001f, fx), Mathf.Clamp01(pk * 2f) * (faceUp ? 0.92f : 1f));
                Color oa = WorkerUi.RarityAccent(draft.defs[Other(j)].rarity);
                s.halo.color = new Color(oa.r, oa.g, oa.b, faceUp ? 0.25f * f : 0f);
            }
        }
        smallLabel.gameObject.SetActive(tt >= 1.7f);
        if (smallLabel.gameObject.activeSelf)
        {
            var c2 = smallLabel.color; c2.a = Mathf.Clamp01((tt - 1.7f) / 0.4f); smallLabel.color = c2;
        }

        float ik = Mathf.Clamp01((tt - 1.9f) / 0.4f);
        infoCg.alpha = ik;
    }

    void Skip()
    {
        if (stage != Stage.Flip) return;
        t = FlipEnd;
        PoseFlip(FlipEnd);
        EnterReveal();
    }

    void EnterReveal()
    {
        PoseFlip(FlipEnd);
        stage = Stage.Reveal;
        flash.color = new Color(1f, 1f, 1f, 0f);
        infoCg.alpha = 1f;
        infoCg.interactable = true; infoCg.blocksRaycasts = true;
        smallLabel.gameObject.SetActive(true);
        var c = smallLabel.color; c.a = 1f; smallLabel.color = c;
        hintT.text = "E или Esc: закрыть   ·   Enter: открыть ещё один";
        SetButtons(true);
    }

    void SetButtons(bool on)
    {
        againBtn.gameObject.SetActive(on);
        doneBtn.gameObject.SetActive(on);
        if (!on) return;
        var m = WorkerManager.Instance;
        PackOpenResult why = PackOpenResult.Ok;
        bool can = m != null && m.CanOpenPack(type, out why);
        int price = m != null ? m.PackPrice(type) : 0;
        againBtn.interactable = can;
        var tx = againBtn.GetComponentInChildren<TMP_Text>();
        if (tx != null) tx.text = can ? "Ещё: " + (price == 0 ? "бесплатно" : price + " монет") : (why == PackOpenResult.NoCoins ? "Не хватает монет" : "Ещё нельзя");
    }

    // взрыв искр в момент показа лица; тряска окна у Редкого и выше
    void Burst(bool withShake)
    {
        burstDone = true;
        flashDone = true;
        Color acc = WorkerUi.RarityAccent(res.rarity);
        Vector2 at = new Vector2(0f, 40f);
        int n = 12 + 7 * (int)res.rarity;
        for (int i = 0; i < n; i++)
        {
            float a = UnityEngine.Random.value * Mathf.PI * 2f, sp = UnityEngine.Random.Range(140f, 420f);
            SpawnSpark(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, Color.Lerp(acc, Color.white, UnityEngine.Random.value * 0.5f), UnityEngine.Random.Range(0.6f, 1.2f), UnityEngine.Random.Range(12f, 28f));
        }
        CandyFx.Confetti(UiCandy.Kit, slots[pick].rt, 14 + 8 * (int)res.rarity);   // конфетти из карты (нет CandyFxKit: без него)
        if (withShake && res.rarity >= WorkerRarity.Rare && onShake != null) onShake();
        if (SfxManager.Instance != null) SfxManager.Play("ui", 0.8f, 0.7f + 0.15f * (int)res.rarity);
    }

    // ============================================================ описание рабочего
    void FillInfo(WorkerManager m)
    {
        var d = res.def;
        var st = res.worker;
        Color acc = WorkerUi.RarityAccent(res.rarity);

        if (res.isNew) { rTag.color = WorkerUi.Bad; rTagText.text = "НОВЫЙ!"; }
        else { rTag.color = WorkerUi.DupTag; rTagText.text = "ДУБЛИКАТ"; }
        if (res.wasFree) rTagText.text += "  ·  ПОДАРОК";
        if (res.GuaranteeTriggered) rTagText.text += "  ·  ГАРАНТИЯ";

        rName.text = d.FullName + ", " + d.age;
        rGem.color = acc;
        rRarity.text = WorkerText.Rarity(res.rarity);
        rRarity.color = WorkerUi.RarityText(res.rarity);
        for (int i = 0; i < 5; i++) rStars[i].gameObject.SetActive(i <= (int)res.rarity);
        rRoleIcon.sprite = UiKit.CandyIcon(WorkerUi.RoleIcon(d.role));
        rRole.text = WorkerText.Role(d.role) + " · " + WorkerText.Output(d.role);
        rLevel.text = "Уровень " + st.level + (st.IsMax ? " (максимум)" : "");

        var sb = new System.Text.StringBuilder();
        if (res.isNew)
        {
            int slot = m.Sim.SlotOf(st);
            if (slot >= 0) sb.Append("Встал в слот ").Append(slot + 1).Append(" и уже работает.");
            else if (m.Sim.CountInSlots(d.role) >= WorkerBalance.MaxPerRole) sb.Append("В запасе: в лагере уже ").Append(WorkerBalance.MaxPerRole).Append(" рабочих этой роли.");
            else sb.Append("В запасе: все слоты заняты.");
        }
        else if (res.coinsGained > 0) sb.Append("Уже на максимальном уровне: вместо опыта +").Append(res.coinsGained).Append(" монет.");
        else
        {
            sb.Append("Уже в лагере: +").Append(res.xpGained).Append(" XP.");
            if (res.levelsGained > 0) sb.Append(" Новый уровень: ").Append(st.level).Append("!");
        }
        if (res.GuaranteeTriggered) sb.Append("\nСработала гарантия: ").Append(GuaranteeName(res.guarantee)).Append(".");
        rLine.text = sb.ToString();

        var pk = new System.Text.StringBuilder();
        for (int i = 0; i < d.perks.Count; i++) { if (i > 0) pk.Append("  ·  "); pk.Append(WorkerText.Perk(d.perks[i])); }
        if (d.HasSignature) pk.Append("  ·  Фирменный");
        rPerks.text = pk.Length > 0 ? "Перки: " + pk : "Перков нет";
        rBio.text = string.IsNullOrEmpty(d.bio) ? "" : d.bio;
    }

    static string GuaranteeName(PackGuarantee g)
    {
        switch (g)
        {
            case PackGuarantee.Mythic: return "Мифический";
            case PackGuarantee.Legendary: return "Легендарный или выше";
            default: return "Редкий или выше";
        }
    }

    // ============================================================ мелочи
    static void Place(Slot s, Vector2 pos, float rot, float scale, float flipX, float alpha)
    {
        s.rt.anchoredPosition = pos;
        s.rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        s.rt.localScale = new Vector3(scale, scale, 1f);
        s.body.localScale = new Vector3(flipX, 1f, 1f);
        s.cg.alpha = alpha;
    }

    static void ShowFace(Slot s, bool up)
    {
        s.faceUp = up;
        s.back.SetActive(!up);
        s.front.rt.gameObject.SetActive(up);
    }

    void SpawnSpark(Vector2 p, Vector2 v, Color c, float life, float size)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (s.live) continue;
            s.live = true; s.p = p; s.v = v; s.life = s.max = life;
            s.img.color = c;
            s.img.gameObject.SetActive(true);
            s.rt.anchoredPosition = p;
            s.rt.sizeDelta = new Vector2(size, size);
            return;
        }
    }

    void TickSparks(float dt)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (!s.live) continue;
            s.life -= dt;
            if (s.life <= 0f) { s.live = false; s.img.gameObject.SetActive(false); continue; }
            s.p += s.v * dt;
            s.v *= 1f - Mathf.Min(1f, 1.6f * dt);
            s.rt.anchoredPosition = s.p;
            var c = s.img.color; c.a = Mathf.Clamp01(s.life / s.max);
            s.img.color = c;
        }
    }
}
