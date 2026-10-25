// Assets/_Project/Scripts/UI/Pets/PetCollectionWindow.Build.cs (НЕ в Editor)
// ЧАСТЬ класса PetCollectionWindow (partial): сборка окна из кода (все RectTransform, кнопки, панели).
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCollectionWindow.cs.
// Остальные части: PetCollectionWindow.Actions.cs, PetCollectionWindow.Fill.cs, PetCollectionWindow.DeltaBook.cs, PetCollectionWindow.Presets.cs, PetCollectionWindow.Interact.cs, PetCollectionWindow.HudHelpers.cs.
// Методы этой части: Build, BuildHeader, BuildTop.

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
    // ============================================================ сборка окна
    void Build()
    {
        if (built) return;

        var cgo = new GameObject("PetCollectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.Window;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // затемнение: только гасит клики по миру
        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;   // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        // окно: деревянная планка сверху. Пустая кнопка на самом окне не даёт клику «всплыть» до затемнения и закрыть окно
        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, WinH), true, BS, "Window");
        panel = win.rectTransform;
        Anch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, WinH));
        AddBtn(win, () => { });
        BuildHeader();

        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f * BS, 26f * BS);
        content.offsetMax = new Vector2(-26f * BS, -36f * BS);

        var inner = UiKit.MakePanel(content, new Vector2(InW, InH), false, 0.6f, "Inner");
        Stretch(inner.rectTransform);

        BuildTop(inner.transform);
        BuildGrid(inner.transform);
        BuildDetail(inner.transform);

        hintText = UiKit.MakeLabel(ov.transform, "", 22f, Dim, TextAlignmentOptions.Center, false);
        Anch(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 12f)), new Vector2(WinW - 40f, 34f));
        PetUi.AutoSize(hintText, 14f, 22f);                          // длинная подсказка уменьшается, а не вылезает за экран

        fx = AchSparks.Create(panel, new Vector2(0.5f, 0.5f));
        hearts = PetHeartFx.Create(panel, new Vector2(0.5f, 0.5f));      // сердечки над питомцем (поверх всего окна)

        ov.gameObject.SetActive(false);
        built = true;
    }

    void BuildHeader()
    {
        // сколько питомцев всего: слева на деревянной планке
        var paw = UiKit.MakeIcon(panel, UiIcon.Paw, 36f, "PawIcon");
        TLC(paw.rectTransform, 64, 27, 36, 36);
        countText = UiKit.MakeLabel(panel, "0", 30f, WoodText, TextAlignmentOptions.Left, true);
        TL(countText.rectTransform, 90, 8, 200, 38);

        titleText = UiKit.MakeLabel(panel, "ПИТОМЦЫ", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(titleText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(520, 40));

        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.onClick.AddListener(DoClose);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
    }

    // левая колонка «Команда»: слоты 2x2, бонусы, пресеты
    void BuildTop(Transform p)
    {
        var team = UiKit.MakeImage(p, "Team", UiKit.Slot, true, 1f);
        TL(team.rectTransform, TeamX, TeamY, TeamW, TeamH);
        team.raycastTarget = false;
        var tr = team.transform;

        var paw = UiKit.MakeIcon(tr, UiIcon.Paw, 30f, "TeamIcon");
        TLC(paw.rectTransform, 28f, 24f, 30f, 30f);
        slotsLabel = UiKit.MakeLabel(tr, "Команда", 28f, Color.white, TextAlignmentOptions.Left, true);
        TL(slotsLabel.rectTransform, 50f, 8f, 180f, 34f);
        PetUi.AutoSize(slotsLabel, 16f, 28f);

        // «Снять всех»: один клик убирает команду (U с клавиатуры, Ctrl+Z вернёт)
        unequipAllBtn = MakeBtn(tr, "Снять всех", UiBtn.Gray, TeamW - 10f - 124f, 6f, 124f, 34f, 18f, OnUnequipAllClick, out unequipAllLabel);
        BtnIco(unequipAllBtn, PetIcons.Get(PetIco.Minus), 22f, 8f);
        unequipAllBtn.gameObject.AddComponent<PetHover>().tip = "Снять всех питомцев (U). Ctrl+Z вернёт состав назад";

        for (int i = 0; i < slots.Length; i++)
        {
            var s = new SlotView();
            float x = 10f + (i % 2) * (SlotW + SlotGapX);
            float y = SlotY + (i / 2) * (SlotH + SlotGapY);

            s.bg = UiKit.MakeImage(tr, "Slot" + i, UiKit.Slot, true, 1f);
            TL(s.bg.rectTransform, x, y, SlotW, SlotH);
            int capI = i;
            AddBtn(s.bg, () => OnSlotClick(capI));
            s.hover = s.bg.gameObject.AddComponent<PetHover>();
            s.hover.grow = 0.04f;
            s.bg.gameObject.AddComponent<PetRightClick>().cb = () => OnSlotUnequip(capI);

            s.card = PetCardView.Create(s.bg.transform, SlotW, SlotH, "SlotCard", false);
            Anch(s.card.rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotW, SlotH));

            s.icon = UiKit.MakeIcon(s.bg.transform, UiIcon.Paw, 56f, "Empty");
            s.icon.rectTransform.anchoredPosition = new Vector2(0f, 14f);

            s.label = UiKit.MakeLabel(s.bg.transform, "", 22f, Dim, TextAlignmentOptions.Center, true);
            Anch(s.label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(SlotW - 12f, 30f));
            PetUi.AutoSize(s.label, 14f, 22f);

            // крестик «снять»: быстрее, чем выбирать питомца и жать «Снять»
            var xb = UiKit.MakeButton(s.bg.transform, "", UiBtn.Gray, new Vector2(30f, 28f), 14f, 0.6f);
            Anch(xb.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-19f, -6f), new Vector2(30f, 28f));
            xb.onClick.AddListener(() => OnSlotUnequip(capI));
            var xi = UiKit.MakeIcon(xb.transform, UiIcon.Cross, 16f, "X");
            xi.raycastTarget = false;
            var xh = xb.gameObject.AddComponent<PetHover>();
            xh.grow = 0.1f;
            xh.tip = "Снять питомца из этого слота (он останется в коллекции)";
            s.xBtn = xb.gameObject;
            s.xBtn.SetActive(false);

            // номер слота = горячая клавиша (на телефоне не показываем)
            if (!Application.isMobilePlatform)
            {
                var kc = UiKit.MakeKeycap(s.bg.transform, (i + 1).ToString(), 30f);
                Anch(kc.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(18f, -18f), new Vector2(30f, 30f));
                kc.raycastTarget = false;
            }
            slots[i] = s;
        }

        // бонусы: до семи строк в одну колонку (крупнее прежних: место освободили наборы, переехавшие в свою колонку)
        var bp = UiKit.MakeImage(tr, "Bonus", UiKit.Slot, true, 0.6f);
        TL(bp.rectTransform, 10f, BonusY, TeamW - 20f, BonusH);
        bp.raycastTarget = false;
        bonusPanel = bp;

        var title = UiKit.MakeLabel(bp.transform, "Бонусы питомцев", 26f, UiKit.BrassHi, TextAlignmentOptions.Left, true);
        TL(title.rectTransform, 16f, 6f, 320f, 34f);

        for (int i = 0; i < 7; i++)
        {
            float y = BonusRowY + i * BonusPitch;
            bonusIcon[i] = UiKit.MakeIcon(bp.transform, UiIcon.Axe, 26f, "BIcon");
            TLC(bonusIcon[i].rectTransform, 30f, y + 14f, 26f, 26f);
            bonusText[i] = UiKit.MakeLabel(bp.transform, "", 24f, Color.white, TextAlignmentOptions.Left, false);
            TL(bonusText[i].rectTransform, 54f, y, TeamW - 20f - 66f, 28f);
            PetUi.AutoSize(bonusText[i], 13f, 24f);

            // тонкая полоска: сколько от потолка бонуса уже набрано
            var tk = UiKit.MakeImage(bp.transform, "BarTrack", null, false);
            TL(tk.rectTransform, 54f, y + 27f, TeamW - 20f - 82f, 4f);
            tk.color = new Color(0f, 0f, 0f, 0.4f);
            tk.raycastTarget = false;
            var bf = UiKit.MakeImage(tk.transform, "BarFill", null, false);
            bf.raycastTarget = false;
            bf.color = UiKit.Good;
            Stretch(bf.rectTransform);
            bonusBarTrack[i] = tk;
            bonusBarFill[i] = bf;
        }

        bonusEmpty = UiKit.MakeLabel(bp.transform, "Поставь питомца в слот, и он начнёт помогать", 24f, Dim, TextAlignmentOptions.Center, false);
        TL(bonusEmpty.rectTransform, 16f, 56f, TeamW - 52f, 150f);
        bonusEmpty.enableWordWrapping = true;

        BuildPresets(p);
    }
}
