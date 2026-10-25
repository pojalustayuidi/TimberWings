// Assets/_Project/Scripts/UI/Pets/PetCollectionWindow.Presets.cs (НЕ в Editor)
// ЧАСТЬ класса PetCollectionWindow (partial): наборы (пресеты) питомцев.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCollectionWindow.cs.
// Остальные части: PetCollectionWindow.Actions.cs, PetCollectionWindow.Fill.cs, PetCollectionWindow.DeltaBook.cs, PetCollectionWindow.Build.cs, PetCollectionWindow.Interact.cs, PetCollectionWindow.HudHelpers.cs.
// Методы этой части: FindOwned, PresetBurst, LoadPresets, PresetFilled, OnPresetMain, SavePreset, ClearPreset, ApplyPreset, OnUnequipAllClick, OnUndoClick, PresetActive, StatNorm, TopTwo, MiniPic, OnPresetCaption, RefreshPresets, AnimatePresets, BuildPresets.

using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class PetCollectionWindow
{
    // ============================================================ наборы (пресеты)
    // 3 набора питомцев: список uid по слотам. Нажал на пустой набор: сохранился текущий состав. Нажал на заполненный: питомцы встали в слоты.
    // Раньше это были три мелкие карточки 112x112 внизу левой колонки (портреты по 23 px, текст 10-16 px: «еле видно»).
    // Теперь отдельная колонка «Наборы»: три карточки 168x216, портреты 58 px по схеме слотов 2x2 (рамка цвета редкости), два главных бонуса, три кнопки
    // (подпись, перезаписать текущим составом, очистить). Галочка: этот набор сейчас надет, его рамка пульсирует.
    // Наборы хранит PetManager (секция «pets» общего сейва): они привязаны к сохранению, переживают слияние и «Отпустить».
    const int PresetCount = PetManager.PresetCount;
    const string OldPresetKey = "pet_presets_v1";      // старое хранилище в PlayerPrefs: один раз удаляется
    readonly int[] presetRes = new int[PetManager.MaxSlots];
    const float PCardX = 10f, PCardW = PrW - 20f, PCardH = 216f, PCardGap = 8f, PCardY0 = 46f;
    const float PMini = 58f, PMiniGap = 4f, PMiniY = 34f;
    static readonly string[] PresetCaptions = { "Основной", "Рубка", "Торговля", "Бригада", "Дорога", "Пила" };
    static readonly string[] AutoNames = { "Рубка", "Бег", "Рюкзак", "Добыча", "Торговля", "Пила", "Бригада" };   // по PetStat
    readonly RectTransform[] pWrap = new RectTransform[PresetCount];      // обёртка: на ней въезд при открытии (PetHover шевелит сам фон)
    readonly CanvasGroup[] pCg = new CanvasGroup[PresetCount];
    readonly Image[] pBg = new Image[PresetCount];
    readonly PetHover[] pHover = new PetHover[PresetCount];
    readonly Image[][] pFrame = new Image[PresetCount][];                 // рамка цвета редкости
    readonly Image[][] pBack = new Image[PresetCount][];                  // плашка под портретом
    readonly Image[][] pPic = new Image[PresetCount][];
    readonly Image[][] pChipIco = new Image[PresetCount][];
    readonly TMP_Text[][] pChipTxt = new TMP_Text[PresetCount][];
    readonly Image[] pCheck = new Image[PresetCount], pPlus = new Image[PresetCount];
    readonly TMP_Text[] pLabel = new TMP_Text[PresetCount], pEmpty = new TMP_Text[PresetCount];
    readonly Button[] pCap = new Button[PresetCount], pSave = new Button[PresetCount], pClear = new Button[PresetCount];
    readonly bool[] pActive = new bool[PresetCount];
    readonly float[] pIn = new float[PresetCount], pPop = new float[PresetCount];
    readonly int[] topI = new int[2];
    readonly float[] topV = new float[2];
    bool presetPortraitPending;                  // у мини-портрета ещё нет 3D-снимка: обновим позже
    Image hoverFrame;
    PoolCard hoverPc;
    float hoverK;

    // питомец по uid среди всех (а не только прошедших фильтр коллекции: иначе набор «теряет» отфильтрованных)
    static PetInstance FindOwned(int uid)
    {
        var pm = PetManager.Instance;
        if (pm == null || uid < 0) return null;
        for (int i = 0; i < pm.Owned.Count; i++) if (pm.Owned[i].uid == uid) return pm.Owned[i];
        return null;
    }

    void PresetBurst(int i)
    {
        if (fx == null) return;
        float cy = PrY + PCardY0 + i * (PCardH + PCardGap) + PMiniY + PMini + PMiniGap * 0.5f;
        fx.Burst(InnerToFx(PrX + PCardX + PCardW * 0.5f, cy), UiKit.BrassHi, 14, 240f, 20f, 260f, 0.7f);
    }

    void LoadPresets()
    {
        if (PlayerPrefs.HasKey(OldPresetKey)) { PlayerPrefs.DeleteKey(OldPresetKey); PlayerPrefs.Save(); }
    }

    bool PresetFilled(int i)
    {
        var pm = PetManager.Instance;
        return pm != null && pm.PresetFilled(i);
    }

    void OnPresetMain(int i)
    {
        if (PresetFilled(i)) ApplyPreset(i); else SavePreset(i);
    }

    void SavePreset(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (!pm.SavePreset(i)) { Toast("Сначала поставь питомцев в слоты, потом сохрани набор"); return; }
        SfxManager.Play("coin", 0.6f, 1.2f);
        pPop[i] = 0.0001f;                                      // портреты «подпрыгивают» волной
        PresetBurst(i);
        Toast("Набор " + (i + 1) + " сохранён");
        RefreshPresets();
    }

    void ClearPreset(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        pm.ClearPreset(i);
        SfxManager.Play("ui", 0.3f, 0.8f);
        Toast("Набор " + (i + 1) + " очищен");
        RefreshPresets();
    }

    void ApplyPreset(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        int subst;
        int missing = pm.ApplyPreset(i, out subst);
        if (missing < 0) return;
        SfxManager.Play("coin", 0.7f, 1.1f);
        pPop[i] = 0.0001f;
        PresetBurst(i);
        if (fx != null) fx.FloatText(InnerToFx(TeamX + TeamW * 0.5f, TeamY + SlotY + SlotH), "Набор " + (i + 1), UiKit.BrassHi, 40f);
        string msg = "Набор " + (i + 1) + " надет";
        if (subst > 0) msg += ", другие копии: " + subst;
        if (missing > 0) msg += ", не хватило питомцев или слотов: " + missing;
        Toast(msg + ". Ctrl+Z: отменить");
    }

    void OnUnequipAllClick()
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (pm.UnequipAll()) { SfxManager.Play("ui", 0.4f, 0.9f); Toast("Все питомцы сняты. Ctrl+Z: отменить"); }
        else Toast("Слоты и так пусты");
    }

    void OnUndoClick()
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (pm.UndoLoadout()) { SfxManager.Play("ui", 0.4f, 1.0f); Toast("Состав возвращён"); }
        else Toast("Отменять нечего");
    }

    bool PresetActive(int i)
    {
        var pm = PetManager.Instance;
        return pm != null && pm.PresetActive(i);
    }

    static float StatNorm(PetBonusSet b, PetStat st)
    {
        return st == PetStat.Capacity ? b.capacityLogs / (float)GameBalance.PetCapacityBase : b.Get(st);
    }

    // две самые сильные характеристики набора (topI = номера PetStat или -1, topV = нормированные значения)
    void TopTwo(PetBonusSet b)
    {
        topI[0] = topI[1] = -1;
        topV[0] = topV[1] = 0f;
        for (int s = 0; s < 7; s++)
        {
            float x = StatNorm(b, (PetStat)s);
            if (x <= 1e-4f) continue;
            if (x > topV[0]) { topV[1] = topV[0]; topI[1] = topI[0]; topV[0] = x; topI[0] = s; }
            else if (x > topV[1]) { topV[1] = x; topI[1] = s; }
        }
    }

    static Sprite MiniPic(PetData d, out bool pending)
    {
        Sprite s = PetPortrait.Get(d);
        pending = s == null && PetPortrait.Enabled && d.prefab != null;
        if (s != null) return s;
        return d.icon != null ? d.icon : UiKit.Icon(UiIcon.Paw);
    }

    void OnPresetCaption(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (!pm.PresetFilled(i)) { Toast("Сначала сохрани состав в набор"); return; }
        pm.CyclePresetCaption(i, PresetCaptions.Length);
        SfxManager.Play("ui", 0.3f, 1.2f);
        RefreshPresets();
    }

    void RefreshPresets()
    {
        if (pBg[0] == null) return;
        var pm = PetManager.Instance;
        presetPortraitPending = false;
        bool canSave = pm != null && pm.EquippedCount > 0;
        for (int i = 0; i < PresetCount; i++)
        {
            bool filled = PresetFilled(i);
            bool active = filled && PresetActive(i);
            pActive[i] = active;
            PetBonusSet pb = filled && pm != null ? pm.PresetBonus(i) : PetBonusSet.None;
            if (pm != null) pm.ResolvePreset(i, presetRes);

            for (int k = 0; k < PetManager.MaxSlots; k++)
            {
                bool wants = pm != null && pm.PresetWants(i, k);
                PetInstance inst = wants && presetRes[k] >= 0 ? FindOwned(presetRes[k]) : null;
                Image fr = pFrame[i][k], bk = pBack[i][k], pic = pPic[i][k];
                if (!wants)
                {
                    fr.color = new Color(1f, 1f, 1f, 0.14f);
                    bk.color = new Color(0f, 0f, 0f, 0.35f);
                    pic.enabled = false;
                }
                else if (inst == null)                                    // питомцев этого вида больше нет
                {
                    fr.color = new Color(DotColor.r, DotColor.g, DotColor.b, 0.85f);
                    bk.color = new Color(0.25f, 0.08f, 0.08f, 0.85f);
                    pic.enabled = false;
                }
                else
                {
                    PetRarity r = inst.data.rarity;
                    fr.color = PetUi.RarityAccent(r);
                    bk.color = Color.Lerp(PetUi.RarityFill(r), Color.black, 0.2f);
                    bool pend;
                    pic.sprite = MiniPic(inst.data, out pend);
                    pic.enabled = true;
                    presetPortraitPending |= pend;
                }
            }

            TopTwo(pb);
            for (int c = 0; c < 2; c++)
            {
                bool on = filled && topI[c] >= 0;
                pChipIco[i][c].gameObject.SetActive(on);
                pChipTxt[i][c].gameObject.SetActive(on);
                if (!on) continue;
                PetStat st = (PetStat)topI[c];
                pChipIco[i][c].sprite = UiKit.Icon(PetUi.StatIcon(st));
                Set(pChipTxt[i][c], StatValueText(st, pb));
            }

            int cap = pm != null ? pm.PresetCaption(i) : -1;
            string name = cap >= 0 && cap < PresetCaptions.Length ? PresetCaptions[cap]
                        : filled && topI[0] >= 0 ? AutoNames[topI[0]]
                        : "Набор " + (i + 1);
            pLabel[i].color = active ? UiKit.BrassHi : Color.white;
            Set(pLabel[i], name);

            pEmpty[i].gameObject.SetActive(!filled);
            pEmpty[i].color = canSave ? UiKit.BrassHi : Dim;
            Set(pEmpty[i], canSave ? "Сохранить состав" : "Пока пусто");
            pPlus[i].gameObject.SetActive(!filled);
            pCheck[i].gameObject.SetActive(active);
            SetBtn(pCap[i], null, filled);
            SetBtn(pSave[i], null, canSave);
            SetBtn(pClear[i], null, filled);
            pHover[i].tip = filled
                ? "<b>Набор " + (i + 1) + " «" + name + "»</b>: клик надевает всех сразу  ·  кнопки внизу: сменить подпись, перезаписать текущим составом, очистить"
                : "Набор " + (i + 1) + " пуст: клик сохранит текущий состав слотов";
        }
    }

    // анимации наборов: въезд при открытии, пульс рамки надетого набора, волна на портретах, мигающий плюс у пустого
    void AnimatePresets(float dt)
    {
        if (pBg[0] == null) return;
        float tm = Time.unscaledTime;
        var pm = PetManager.Instance;
        bool canSave = pm != null && pm.EquippedCount > 0;
        for (int i = 0; i < PresetCount; i++)
        {
            if (pIn[i] < 1f) pIn[i] = Mathf.Min(1f, pIn[i] + dt);
            float k = Mathf.Clamp01(pIn[i] / 0.35f);
            pWrap[i].localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, 1f, UiDraw.EaseOutBack(k));
            pWrap[i].anchoredPosition = new Vector2(PCardX + PCardW * 0.5f,
                -(PCardY0 + i * (PCardH + PCardGap) + PCardH * 0.5f) - (1f - UiDraw.EaseOutCubic(k)) * 24f);
            pCg[i].alpha = Mathf.Clamp01(k * 2.5f);

            // надетый набор: рамка тёплым светом пульсирует
            Color tgt = pActive[i] ? Color.Lerp(Color.white, UiKit.BrassHi, 0.45f + 0.2f * Mathf.Sin(tm * 3.2f)) : Color.white;
            pBg[i].color = Color.Lerp(pBg[i].color, tgt, Mathf.Clamp01(dt * 12f));
            if (pCheck[i].gameObject.activeSelf) pCheck[i].rectTransform.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(tm * 4f));

            // пустой набор: плюс мигает, если есть что сохранить
            if (pPlus[i].gameObject.activeSelf)
            {
                float pl = 0.5f + 0.5f * Mathf.Sin(tm * 3f + i);
                pPlus[i].rectTransform.localScale = Vector3.one * (canSave ? 1f + 0.14f * pl : 1f);
                pPlus[i].color = new Color(1f, 1f, 1f, canSave ? 0.5f + 0.5f * pl : 0.22f);
            }

            // сохранили или надели: портреты подпрыгивают по очереди
            if (pPop[i] > 0f)
            {
                pPop[i] += dt;
                if (pPop[i] > 1.2f) pPop[i] = 0f;
                for (int m = 0; m < PetManager.MaxSlots; m++)
                {
                    float t = (pPop[i] - m * 0.07f) / 0.4f;
                    float sc = 1f + (t > 0f && t < 1f ? 0.35f * Mathf.Sin(t * Mathf.PI) : 0f);
                    pFrame[i][m].rectTransform.localScale = Vector3.one * sc;
                }
            }
        }
    }

    void BuildPresets(Transform p)
    {
        LoadPresets();

        // колонка в той же рамке, что и «Команда»
        var col = UiKit.MakeImage(p, "PresetsCol", UiKit.Slot, true, 1f);
        TL(col.rectTransform, PrX, PrY, PrW, PrH);
        col.raycastTarget = false;
        var tr = col.transform;

        var hico = UiKit.MakeImage(tr, "TitleIco", PetIcons.Get(PetIco.Save), false);
        hico.preserveAspect = true;
        hico.raycastTarget = false;
        TLC(hico.rectTransform, 28f, 24f, 28f, 28f);
        var ttl = UiKit.MakeLabel(tr, "Наборы", 28f, Color.white, TextAlignmentOptions.Left, true);
        TL(ttl.rectTransform, 50f, 8f, PrW - 60f, 34f);
        PetUi.AutoSize(ttl, 16f, 28f);

        Vector2 mid = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < PresetCount; i++)
        {
            int n = i;

            // обёртка по центру карточки: на ней масштаб и прозрачность въезда
            var wrap = NewRT("PresetWrap" + (i + 1), tr);
            Anch(wrap, new Vector2(0f, 1f), mid, new Vector2(PCardX + PCardW * 0.5f, -(PCardY0 + i * (PCardH + PCardGap) + PCardH * 0.5f)), new Vector2(PCardW, PCardH));
            pCg[i] = wrap.gameObject.AddComponent<CanvasGroup>();
            pWrap[i] = wrap;
            pIn[i] = 1f;

            var bg = UiKit.MakeImage(wrap, "Preset" + (i + 1), UiKit.Slot, true, 0.9f);
            Stretch(bg.rectTransform);
            AddBtn(bg, () => OnPresetMain(n));
            var hv = bg.gameObject.AddComponent<PetHover>();
            hv.grow = 0.03f;
            pBg[i] = bg;
            pHover[i] = hv;

            // имя набора и галочка «надет сейчас»
            pLabel[i] = UiKit.MakeLabel(bg.transform, "", 22f, Color.white, TextAlignmentOptions.Left, true);
            TL(pLabel[i].rectTransform, 10f, 5f, PCardW - 44f, 26f);
            PetUi.AutoSize(pLabel[i], 13f, 22f);
            pLabel[i].raycastTarget = false;
            pCheck[i] = UiKit.MakeIcon(bg.transform, UiIcon.Check, 22f, "Active");
            pCheck[i].raycastTarget = false;
            pCheck[i].color = UiKit.Good;
            Anch(pCheck[i].rectTransform, new Vector2(1f, 1f), mid, new Vector2(-20f, -18f), new Vector2(24f, 24f));
            pCheck[i].gameObject.SetActive(false);

            // 4 портрета по схеме слотов 2x2: рамка цвета редкости, плашка, сам питомец
            pFrame[i] = new Image[PetManager.MaxSlots];
            pBack[i] = new Image[PetManager.MaxSlots];
            pPic[i] = new Image[PetManager.MaxSlots];
            float mx0 = (PCardW - (2f * PMini + PMiniGap)) * 0.5f;
            for (int k = 0; k < PetManager.MaxSlots; k++)
            {
                float mx = mx0 + (k % 2) * (PMini + PMiniGap), my = PMiniY + (k / 2) * (PMini + PMiniGap);
                var fr = UiKit.MakeImage(bg.transform, "Mini" + k, null, false);
                TLC(fr.rectTransform, mx + PMini * 0.5f, my + PMini * 0.5f, PMini, PMini);   // от центра: подпрыгивает без смещения
                fr.raycastTarget = false;
                var bk = UiKit.MakeImage(fr.transform, "Back", null, false);
                Stretch(bk.rectTransform);
                bk.rectTransform.offsetMin = new Vector2(2f, 2f);
                bk.rectTransform.offsetMax = new Vector2(-2f, -2f);
                bk.raycastTarget = false;
                var pic = UiKit.MakeImage(fr.transform, "Pic", null, false);
                pic.preserveAspect = true;
                pic.raycastTarget = false;
                Stretch(pic.rectTransform);
                pic.rectTransform.offsetMin = new Vector2(3f, 3f);
                pic.rectTransform.offsetMax = new Vector2(-3f, -3f);
                pFrame[i][k] = fr;
                pBack[i][k] = bk;
                pPic[i][k] = pic;
            }

            // плюс по центру блока портретов: виден, только пока набор пуст
            pPlus[i] = UiKit.MakeImage(bg.transform, "Plus", PetIcons.Get(PetIco.Plus), false);
            pPlus[i].preserveAspect = true;
            pPlus[i].raycastTarget = false;
            TLC(pPlus[i].rectTransform, PCardW * 0.5f, PMiniY + PMini + PMiniGap * 0.5f, 56f, 56f);

            // два главных бонуса набора: значок характеристики и значение
            pChipIco[i] = new Image[2];
            pChipTxt[i] = new TMP_Text[2];
            for (int c = 0; c < 2; c++)
            {
                float cx = 8f + c * (PCardW * 0.5f - 4f);
                var ci = UiKit.MakeImage(bg.transform, "ChipIco" + c, null, false);
                ci.preserveAspect = true;
                ci.raycastTarget = false;
                TL(ci.rectTransform, cx, 159f, 22f, 22f);
                var ct = UiKit.MakeLabel(bg.transform, "", 20f, UiKit.Good, TextAlignmentOptions.Left, true);
                TL(ct.rectTransform, cx + 24f, 157f, PCardW * 0.5f - 34f, 26f);
                PetUi.AutoSize(ct, 11f, 20f);
                ct.raycastTarget = false;
                pChipIco[i][c] = ci;
                pChipTxt[i][c] = ct;
            }

            pEmpty[i] = UiKit.MakeLabel(bg.transform, "Сохранить состав", 17f, Dim, TextAlignmentOptions.Center, true);
            TL(pEmpty[i].rectTransform, 4f, 157f, PCardW - 8f, 26f);
            PetUi.AutoSize(pEmpty[i], 11f, 17f);
            pEmpty[i].raycastTarget = false;

            // кнопки: подпись, перезаписать, очистить
            const float BW = 49f, BH = 28f, BY = 184f;
            pCap[i] = UiKit.MakeButton(bg.transform, "", UiBtn.Amber, new Vector2(BW, BH), 14f, 0.6f);
            TL(pCap[i].GetComponent<RectTransform>(), 6f, BY, BW, BH);
            pCap[i].onClick.AddListener(() => OnPresetCaption(n));
            Ico(pCap[i].transform, PetIco.Swap, 18f);
            pCap[i].gameObject.AddComponent<PetHover>().tip = "Сменить подпись набора " + (i + 1) + " (по кругу, первая: по главному бонусу)";

            pSave[i] = UiKit.MakeButton(bg.transform, "", UiBtn.Green, new Vector2(BW, BH), 14f, 0.6f);
            TL(pSave[i].GetComponent<RectTransform>(), 6f + BW + 4f, BY, BW, BH);
            pSave[i].onClick.AddListener(() => SavePreset(n));
            Ico(pSave[i].transform, PetIco.Save, 19f);
            var hs = pSave[i].gameObject.AddComponent<PetHover>(); hs.grow = 0.08f; hs.tip = "Перезаписать набор " + (i + 1) + " текущим составом слотов";

            pClear[i] = UiKit.MakeButton(bg.transform, "", UiBtn.Gray, new Vector2(BW, BH), 14f, 0.6f);
            TL(pClear[i].GetComponent<RectTransform>(), 6f + 2f * (BW + 4f), BY, BW, BH);
            pClear[i].onClick.AddListener(() => ClearPreset(n));
            var ci2 = UiKit.MakeIcon(pClear[i].transform, UiIcon.Cross, 16f, "C");
            ci2.raycastTarget = false;
            var hc = pClear[i].gameObject.AddComponent<PetHover>(); hc.grow = 0.08f; hc.tip = "Очистить набор " + (i + 1);
        }
        RefreshPresets();
    }
}
