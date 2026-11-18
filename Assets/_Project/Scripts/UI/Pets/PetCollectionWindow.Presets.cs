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
    const float PCardX = 30f, PCardW = 276f, PCardH = 354f, PCardGap = 16f, PCardY0 = 88f;
    const float PMini = 64f, PMiniGap = 8f, PMiniY = 48f;
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
        Vector2 point = panel.InverseTransformPoint(pWrap[i].position);
        fx.Burst(point, TWPetCollectionSkin.Selection, 14, 240f, 20f, 260f, 0.7f);
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
        if (fx != null) fx.FloatText(InnerToFx(TeamX + TeamW * 0.5f, TeamY + SlotY + SlotH), "Набор " + (i + 1), TWPetCollectionSkin.Accent, 40f);
        string msg = "Набор " + (i + 1) + " надет";
        if (subst > 0) msg += ", другие копии: " + subst;
        if (missing > 0) msg += ", не хватило питомцев или слотов: " + missing;
        Toast(msg + ". Ctrl+Z: отменить");
        SetPopup(Popup.None);
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
        if (d.icon != null) { pending = false; return d.icon; }
        Sprite s = PetPortrait.Get(d);
        pending = s == null && PetPortrait.Enabled && d.prefab != null;
        if (s != null) return s;
        return d.icon != null ? d.icon : TWPetCollectionSkin.Icon(UiIcon.Paw);
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
        if (pBg[0] == null || popup != Popup.Presets) return;
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
                    fr.color = new Color(0.88f, 0.86f, 0.96f);
                    bk.color = new Color(0.96f, 0.95f, 1f);
                    pic.enabled = false;
                }
                else if (inst == null)                                    // питомцев этого вида больше нет
                {
                    fr.color = new Color(DotColor.r, DotColor.g, DotColor.b, 0.85f);
                    bk.color = new Color(1f, 0.87f, 0.90f);
                    pic.enabled = false;
                }
                else
                {
                    PetRarity r = inst.data.rarity;
                    fr.color = PetUi.RarityAccent(r);
                    bk.color = Color.Lerp(Color.white, PetUi.RarityAccent(r), 0.16f);
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
                pChipIco[i][c].sprite = TWPetCollectionSkin.Icon(PetUi.StatIcon(st));
                Set(pChipTxt[i][c], StatValueText(st, pb));
            }

            int cap = pm != null ? pm.PresetCaption(i) : -1;
            string name = cap >= 0 && cap < PresetCaptions.Length ? PresetCaptions[cap]
                        : filled && topI[0] >= 0 ? AutoNames[topI[0]]
                        : "Набор " + (i + 1);
            pLabel[i].color = TWPetCollectionSkin.Ink;
            Set(pLabel[i], name);

            pEmpty[i].gameObject.SetActive(!filled);
            pEmpty[i].color = canSave ? TWPetCollectionSkin.Accent : Dim;
            Set(pEmpty[i], canSave ? "Готово к сохранению" : "Сначала собери команду");
            pPlus[i].gameObject.SetActive(!filled);
            pCheck[i].gameObject.SetActive(active);
            SetBtn(pCap[i], null, filled);
            SetBtn(pSave[i], null, canSave);
            SetBtn(pClear[i], null, filled);
            Set(presetApplyText[i], filled ? (active ? "Эта команда выбрана" : "Выбрать команду") : "Сохранить команду");
            SetBtn(presetApply[i], presetApplyText[i], filled || canSave);
            pHover[i].tip = filled
                ? "<b>Набор " + (i + 1) + " «" + name + "»</b>: клик надевает всех сразу  ·  кнопки внизу: сменить подпись, перезаписать текущим составом, очистить"
                : "Набор " + (i + 1) + " пуст: кнопка «Сохранить команду» запомнит текущий состав";
        }
    }

    // анимации наборов: въезд при открытии, пульс рамки надетого набора, волна на портретах, мигающий плюс у пустого
    void AnimatePresets(float dt)
    {
        if (pBg[0] == null || popup != Popup.Presets) return;
        float tm = Time.unscaledTime;
        var pm = PetManager.Instance;
        bool canSave = pm != null && pm.EquippedCount > 0;
        for (int i = 0; i < PresetCount; i++)
        {
            if (pIn[i] < 1f) pIn[i] = Mathf.Min(1f, pIn[i] + dt);
            float k = Mathf.Clamp01(pIn[i] / 0.35f);
            pWrap[i].localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, 1f, UiDraw.EaseOutBack(k));
            pWrap[i].anchoredPosition = new Vector2(PCardX + i * (PCardW + PCardGap) + PCardW * 0.5f,
                -(PCardY0 + PCardH * 0.5f) - (1f - UiDraw.EaseOutCubic(k)) * 12f);
            pCg[i].alpha = Mathf.Clamp01(k * 2.5f);

            // надетый набор: рамка тёплым светом пульсирует
            Color tgt = pActive[i] ? Color.Lerp(Color.white, TWPetCollectionSkin.Accent, 0.45f + 0.2f * Mathf.Sin(tm * 3.2f)) : Color.white;
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

    void BuildPresets(Transform parent)
    {
        LoadPresets();
        var card = PopupCard("PresetsPopup", new Vector2(920f, 550f), Vector2.zero, out presetsPopup);
        var title = TWPetCollectionSkin.Label(card, "Сохранённые команды", 30f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left);
        TL(title.rectTransform, 30f, 22f, 700f, 42f);
        TMP_Text done;
        MakeBtn(card, "Готово", UiBtn.Gray, 760f, 22f, 130f, 44f, 20f, () => SetPopup(Popup.None), out done);
        for (int i = 0; i < PresetCount; i++)
        {
            int index = i;
            var wrap = NewRT("Preset" + i, card);
            Anch(wrap, new Vector2(0f, 1f), Vector2.one * 0.5f,
                new Vector2(PCardX + i * (PCardW + PCardGap) + PCardW * 0.5f, -(PCardY0 + PCardH * 0.5f)), new Vector2(PCardW, PCardH));
            pWrap[i] = wrap;
            pCg[i] = wrap.gameObject.AddComponent<CanvasGroup>();
            pIn[i] = 1f;
            var bg = TWPetCollectionSkin.Surface(wrap, "Card");
            Stretch(bg.rectTransform);
            pBg[i] = bg;
            pHover[i] = bg.gameObject.AddComponent<PetHover>();
            pLabel[i] = TWPetCollectionSkin.Label(bg.transform, "", 24f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left);
            TL(pLabel[i].rectTransform, 14f, 10f, PCardW - 56f, 30f);
            pCheck[i] = PetIcon(bg.transform, UiIcon.Check, 22f, "Active");
            TLC(pCheck[i].rectTransform, PCardW - 26f, 26f, 22f, 22f);
            pFrame[i] = new Image[PetManager.MaxSlots]; pBack[i] = new Image[PetManager.MaxSlots]; pPic[i] = new Image[PetManager.MaxSlots];
            float x0 = (PCardW - PMini * 2f - PMiniGap) * 0.5f;
            for (int j = 0; j < PetManager.MaxSlots; j++)
            {
                var frame = TWPetCollectionSkin.Surface(bg.transform, "Pet" + j);
                TLC(frame.rectTransform, x0 + j % 2 * (PMini + PMiniGap) + PMini * 0.5f,
                    PMiniY + j / 2 * (PMini + PMiniGap) + PMini * 0.5f, PMini, PMini);
                var back = UiKit.MakeImage(frame.transform, "Back", null, false);
                Stretch(back.rectTransform); back.rectTransform.offsetMin = Vector2.one * 4f; back.rectTransform.offsetMax = Vector2.one * -4f;
                var pic = UiKit.MakeImage(frame.transform, "Portrait", null, false);
                Stretch(pic.rectTransform); pic.preserveAspect = true;
                pic.rectTransform.offsetMin = Vector2.one * 3f; pic.rectTransform.offsetMax = Vector2.one * -3f;
                pFrame[i][j] = frame; pBack[i][j] = back; pPic[i][j] = pic;
            }
            pPlus[i] = PetIcon(bg.transform, UiIcon.Paw, 44f, "Empty");
            TLC(pPlus[i].rectTransform, PCardW * 0.5f, PMiniY + PMini + PMiniGap * 0.5f, 44f, 44f);
            pChipIco[i] = new Image[2]; pChipTxt[i] = new TMP_Text[2];
            for (int j = 0; j < 2; j++)
            {
                float x = 24f + j * 120f;
                pChipIco[i][j] = PetIcon(bg.transform, UiIcon.Star, 23f, "Bonus");
                TL(pChipIco[i][j].rectTransform, x, 208f, 23f, 23f);
                pChipTxt[i][j] = TWPetCollectionSkin.Label(bg.transform, "", 20f, TWPetCollectionSkin.Good, TextAlignmentOptions.Left);
                TL(pChipTxt[i][j].rectTransform, x + 30f, 206f, 78f, 28f);
            }
            pEmpty[i] = TWPetCollectionSkin.Label(bg.transform, "", 19f, Dim, TextAlignmentOptions.Center, false);
            TL(pEmpty[i].rectTransform, 12f, 204f, PCardW - 24f, 34f);
            presetApply[i] = MakeBtn(bg.transform, "", UiBtn.Gray, 12f, 252f, PCardW - 24f, 46f, 19f, () => OnPresetMain(index), out presetApplyText[i]);
            TMP_Text label;
            pCap[i] = MakeBtn(bg.transform, "Имя", UiBtn.Gray, 12f, 310f, 60f, 32f, 16f, () => OnPresetCaption(index), out label);
            pSave[i] = MakeBtn(bg.transform, "Обновить", UiBtn.Gray, 82f, 310f, 124f, 32f, 16f, () => SavePreset(index), out label);
            pClear[i] = MakeBtn(bg.transform, "×", UiBtn.Gray, 216f, 310f, 48f, 32f, 21f, () => ClearPreset(index), out label);
            Tip(pSave[i], "Заменить сохранённую команду текущим составом");
            Tip(pClear[i], "Очистить набор. Питомцы останутся в коллекции");
        }
        unequipAllBtn = MakeBtn(card, "Снять всех", UiBtn.Gray, 30f, 478f, 210f, 44f, 20f, OnUnequipAllClick, out unequipAllLabel);
        undoBtn = MakeBtn(card, "Вернуть состав", UiBtn.Gray, 256f, 478f, 250f, 44f, 20f, OnUndoClick, out undoLabel);
        var note = TWPetCollectionSkin.Label(card, "Ctrl+Z — отменить смену команды", 17f, Dim, TextAlignmentOptions.Right, false);
        TL(note.rectTransform, 520f, 484f, 366f, 32f);
    }
}
