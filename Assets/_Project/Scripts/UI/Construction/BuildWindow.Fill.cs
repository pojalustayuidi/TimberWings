// Assets/_Project/Scripts/UI/Construction/BuildWindow.Fill.cs (НЕ в Editor)
// BuildWindow, часть «обновление» (Candy). Здесь: Refresh (все тексты, цвета узлов и кнопок), FillRow, полосы, анимация.
// Основной файл: BuildWindow.cs. Сборка UI: BuildWindow.Build.cs.
//
//   Refresh()        раз в 0.1 с и по событиям: шапка, узлы стадий, этап, ресурсы, состояние, кнопки
//   MissingText()    чего не хватает
//   BackpackLine()   «В рюкзаке: …»
//   FillRow()        одна строка ресурса (чип «сдано +несёшь/нужно» и цель полосы)
//   UpdateBar()      полоса стройки в окне, каждый кадр
//   UpdateStrip()    то же для свёрнутой полоски
//   AnimateStrip()   полоска выезжает снизу
//   Animate()        «подпрыгивание» рабочей области, пульс узла-цели, плавные полосы
//
// Цвета: белый и бледный текст старого стиля заменён индиго (Ink, Muted), «хорошо» (Done), золотой для числа награды (GoldText).
// Появление окна (масштаб и проявление) делает CandyPopIn, поэтому масштаб панели здесь не трогаем: «подпрыгивает» только Body.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BuildWindow
{
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void SetBtn(Btn b, string label, bool enabled)
    {
        Set(b.txt, label);
        b.txt.color = enabled ? b.fg : UiCandy.Muted;
        b.btn.interactable = enabled;
    }

    static string Wood(int n) { return UiDraw.Plural(n, "бревно", "бревна", "бревен"); }
    static string Plank(int n) { return UiDraw.Plural(n, "доску", "доски", "досок"); }
    static string Coin(int n) { return UiDraw.Plural(n, "монету", "монеты", "монет"); }

    static string FormatTime(float sec)
    {
        if (sec >= 10f) return Mathf.RoundToInt(sec) + " с";
        return sec.ToString("0.0") + " с";
    }

    // значок здания: из данных или с компонента метки; запасной Candy-значок берётся при включённом скине (вызывать внутри using UseSkin)
    static Sprite IconOf(BuildingController b)
    {
        if (b.data != null && b.data.icon != null) return b.data.icon;
        var v = b.GetComponent<BuildSiteTagView>();
        if (v != null && v.buildingIcon != null) return v.buildingIcon;
        return UiKit.Icon(UiIcon.Log);
    }

    void Refresh()
    {
        refreshAt = Time.unscaledTime + 0.1f;
        if (!built || bc == null) return;
        if (inv == null) inv = PlayerInventory.Instance;

        bool done = bc.IsFinished;
        bool running = bc.StageRunning;
        int cur = bc.StageCurrent;
        int tot = bc.StageTotal;
        bool staged = tot > 1;

        // ----- шапка: у каждого здания своё название и значок (в Candy название обычными буквами, не КАПСОМ)
        string title = bc.BuildingTitle;
        if (title != lastTitle)
        {
            lastTitle = title;
            titleText.text = title;
            Sprite icon;
            using (UiKit.UseSkin(UiSkin.Candy)) icon = IconOf(bc);
            headerIcon.sprite = icon;
            bigIcon.sprite = icon;
            // значок встаёт слева от названия, какое бы длинное оно ни было (название по центру окна)
            float w = Mathf.Min(titleText.GetPreferredValues(title).x, 330f);
            headerIconRt.anchoredPosition = new Vector2(WinW * 0.5f - w * 0.5f - 34f, -27f);
        }

        // ----- левая карточка: стадии, название и описание этапа
        if (tot != lastTotal) { RebuildNodes(staged ? tot : 0); lastTotal = tot; }
        nodesRoot.gameObject.SetActive(staged);
        stageCaption.gameObject.SetActive(staged);
        targetNode = null;
        for (int i = 0; i < nodeRing.Length; i++)
        {
            bool reached = i < cur;
            bool target = i == cur && !done;
            // выучено мятное, текущая лимонная (пульсирует), впереди лавандовая
            nodeRing[i].color = reached ? UiCandy.Done : (target ? UiCandy.PeachDeep : UiCandy.Lavender);
            nodeFill[i].color = reached ? UiCandy.Mint : (target ? UiCandy.Lemon : UiCandy.TintOf(UiTone.Lavender));
            nodeCheck[i].enabled = reached;
            nodeNum[i].enabled = !reached;
            nodeNum[i].color = target ? UiCandy.Ink : UiCandy.Muted;
            nodeRing[i].rectTransform.localScale = Vector3.one;
            if (target) targetNode = nodeRing[i].rectTransform;
            if (i > 0) links[i - 1].color = reached ? UiCandy.Done : UiCandy.Lavender;
        }
        if (staged)
            Set(stageCaption, done ? "<color=" + HexDone + ">Построено полностью</color>" : "Стадия " + cur + " из " + tot);

        string name = bc.StepName;
        if (string.IsNullOrEmpty(name) || name == "Этап") name = staged ? "Стадия " + (cur + 1) : "Строительство";
        Set(stageName, done ? "Готово!" : name);

        string desc = bc.StepDescription;
        if (string.IsNullOrEmpty(desc)) desc = "Сдай материалы и нажми «Играть», чтобы построить.";
        Set(stageDesc, done ? "Здание построено и готово к использованию." : desc);

        Set(timeText, FormatTime(bc.BuildTime));
        int xp = bc.StepXp;
        chipXp.gameObject.SetActive(xp > 0);
        Set(xpText, "<color=" + HexGold + ">+" + xp + " XP</color>");

        // ----- правая карточка: что нужно сдать
        int wood = inv != null ? inv.woodCount : 0;
        int planks = inv != null ? inv.planksCount : 0;
        int coins = inv != null ? inv.coinsCount : 0;
        int shownRows = 0;
        shownRows += FillRow(rowWood, shownRows, bc.Deposited, wood, bc.CostTotal, done);
        shownRows += FillRow(rowPlank, shownRows, bc.DepositedPlanks, planks, bc.PlankTotal, done);
        int haveCoins = bc.DepositedCoins >= bc.CoinTotal ? bc.CoinTotal : bc.DepositedCoins;
        shownRows += FillRow(rowCoin, shownRows, haveCoins, coins, bc.CoinTotal, done);
        needTitle.gameObject.SetActive(!done);
        Set(backpackText, done ? "" : BackpackLine(wood, planks, coins));

        // ----- состояние и кнопки
        bool canDeposit = bc.CanDeposit(inv);
        canPlayNow = bc.CanPlay;

        // состояние красит и саму карточку: мята (хорошо), лимон (надо принести или идёт стройка), лаванда (нейтрально)
        string status;
        UiTone tone;
        if (done) { status = "<color=" + HexDone + ">Построено!</color>"; tone = UiTone.Mint; }
        else if (running) { status = "<color=" + HexGold + ">Строим…</color>"; tone = UiTone.Lemon; }
        else if (bc.AllDeposited) { status = "<color=" + HexDone + ">Всё сдано. Жми «Играть»!</color>"; tone = UiTone.Mint; }
        else if (canDeposit) { status = "<color=" + HexDone + ">Нажми «Сдать ресурсы»</color>"; tone = UiTone.Mint; }
        else { status = "<color=" + HexGold + ">" + MissingText() + "</color>"; tone = UiTone.Lemon; }
        Set(statusText, status);
        statusCard.color = UiCandy.TintOf(tone);

        barFrame.gameObject.SetActive(running && !done);

        SetBtn(deposit, "Сдать ресурсы", canDeposit);
        SetBtn(play, done ? "Готово" : (running ? "Строим…" : "Играть"), canPlayNow);
        // готовая к нажатию «Играть» раз в несколько секунд качается сама (CandyButtonJuice.attention), ручное «дыхание» убрано: они мешали друг другу
        if (play.juice != null) play.juice.attention = canPlayNow;
    }

    // чего не хватает: сначала брёвна, потом доски, потом монеты
    string MissingText()
    {
        int w = bc.CostTotal - bc.Deposited;
        if (w > 0) return "Принеси ещё " + w + " " + Wood(w);
        int p = bc.PlankTotal - bc.DepositedPlanks;
        if (p > 0) return "Принеси ещё " + p + " " + Plank(p);
        int c = bc.CoinTotal - bc.DepositedCoins;
        if (c > 0) return "Нужно ещё " + c + " " + Coin(c);
        return "";
    }

    string BackpackLine(int wood, int planks, int coins)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (bc.CostTotal > 0) parts.Add("бревна <color=" + HexDone + ">" + wood + "</color>");
        if (bc.PlankTotal > 0) parts.Add("доски <color=" + HexDone + ">" + planks + "</color>");
        if (bc.CoinTotal > 0) parts.Add("монеты <color=" + HexDone + ">" + coins + "</color>");
        return parts.Count == 0 ? "" : "В рюкзаке: " + string.Join("  ·  ", parts.ToArray());
    }

    // заполнить строку ресурса. have = сдано, carry = несёшь с собой, total = нужно. Возвращает 1, если строка показана
    int FillRow(Row r, int index, int have, int carry, int total, bool done)
    {
        bool on = total > 0 && !done;
        if (r.root.gameObject.activeSelf != on) r.root.gameObject.SetActive(on);
        if (!on) return 0;

        r.root.anchoredPosition = new Vector2(24f, -(RowTop + index * RowStep));

        int add = Mathf.Clamp(carry, 0, Mathf.Max(0, total - have));
        r.target = Mathf.Clamp01(have / (float)total);
        r.targetPreview = Mathf.Clamp01((have + add) / (float)total);

        bool full = have >= total;
        r.check.enabled = full;
        r.chip.color = UiCandy.TintOf(full ? UiTone.Mint : UiTone.Neutral);   // всё сдано: чип мятный
        string col = full ? HexDone : HexInk;
        string plus = (!full && add > 0) ? " <size=75%><color=" + HexGold + ">+" + add + "</color></size>" : "";
        Set(r.count, "<color=" + col + ">" + have + "</color>" + plus + "<color=" + HexMuted + ">/" + total + "</color>");
        return 1;
    }

    // полоса стройки идёт каждый кадр: она плавная
    void UpdateBar()
    {
        if (bc == null || !bc.StageRunning || bc.IsFinished) return;
        float p = bc.Progress;
        barFill.fillAmount = p;
        barFill.gameObject.SetActive(p > 0.02f);
        float left = Mathf.Max(0f, (1f - p) * bc.BuildTime);
        Set(barText, bc.IsBuilding ? "ещё " + FormatTime(left) : "Встань на площадку");
    }

    // полоска идёт каждый кадр, как и полоса в окне
    void UpdateStrip()
    {
        if (bc == null) return;
        float p = bc.Progress;
        stripFill.fillAmount = p;
        stripFill.gameObject.SetActive(p > 0.02f);
        float left = Mathf.Max(0f, (1f - p) * bc.BuildTime);
        Set(stripTime, bc.IsBuilding ? "ещё " + FormatTime(left) : "Встань на площадку");

        // «Хижина · стадия 2/3»: номер той стадии, которая строится сейчас
        int tot = bc.StageTotal;
        string t = bc.BuildingTitle;
        if (tot > 1) t += " · " + Mathf.Min(bc.StageCurrent + 1, tot) + "/" + tot;
        if (t != stripTitleShown) { stripTitleShown = t; stripTitle.text = t; }
    }

    // полоска мягко выезжает снизу
    void AnimateStrip()
    {
        float e = UiDraw.EaseOutCubic(stripK);
        stripGroup.alpha = stripK;
        stripGroup.blocksRaycasts = stripK > 0.5f;
        stripRt.anchoredPosition = new Vector2(0f, StripY + Mathf.Lerp(-30f, 0f, e));
    }

    // лёгкая анимация: «подпрыгивание» при событиях, дыхание узла следующей стадии, плавные полосы.
    // Открытие окна здесь не анимируется: его делает CandyPopIn на панели. Кнопки пружинят сами (CandyButtonJuice)
    void Animate()
    {
        float dt = Time.unscaledDeltaTime;
        pulse += dt * 2.4f;
        punch = Mathf.Min(1f, punch + dt / 0.3f);

        body.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(punch * Mathf.PI) * (1f - punch));

        if (targetNode != null)
            targetNode.localScale = Vector3.one * (1f + 0.14f * (0.5f + 0.5f * Mathf.Sin(pulse * 1.6f)));

        float k = 1f - Mathf.Exp(-10f * dt);
        AnimRow(rowWood, k); AnimRow(rowPlank, k); AnimRow(rowCoin, k);
    }

    static void AnimRow(Row r, float k)
    {
        if (r == null || !r.root.gameObject.activeSelf) return;
        r.shown = Mathf.Lerp(r.shown, r.target, k);
        r.shownPreview = Mathf.Lerp(r.shownPreview, r.targetPreview, k);
        r.fill.fillAmount = r.shown;
        r.preview.fillAmount = r.shownPreview;
    }
}
