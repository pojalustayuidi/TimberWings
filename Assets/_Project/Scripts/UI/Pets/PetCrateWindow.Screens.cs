// Assets/_Project/Scripts/UI/Pets/PetCrateWindow.Screens.cs (НЕ в Editor)
// ЧАСТЬ класса PetCrateWindow (partial): частицы и экраны клети: подготовка, рулетка, выпадение.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCrateWindow.cs.
// Остальные части: PetCrateWindow.Update.cs, PetCrateWindow.Build.cs.
// Методы этой части: Emit, Burst, UpdateSparks, ClearSparks, BuildStrip, UpdateStripLook, UpdateLive, SetPointerColor, AnimateRoll, AnimatePrepare, ShowResult, RayAlpha, OnReveal, AnimateResult, Animate, PlayRevealSfx, RefreshResultButtons.

using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class PetCrateWindow
{
    // ============================================================ частицы
    void Emit(float x, float y, float vx, float vy, float g, float life, float s0, float s1, Color c, float vrot)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (s.alive) continue;
            s.alive = true;
            s.x = x; s.y = y; s.vx = vx; s.vy = vy; s.g = g;
            s.life = s.max = life;
            s.s0 = s0; s.s1 = s1; s.col = c;
            s.rot = Random.Range(0f, 90f); s.vrot = vrot;
            s.img.gameObject.SetActive(true);
            s.rt.anchoredPosition = new Vector2(x, -y);
            return;
        }
    }

    // взрыв искр из точки (координаты внутри окна от левого верхнего угла, вниз положительно)
    void Burst(float cx, float cy, int n, Color accent, float vMin, float vMax)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float v = Random.Range(vMin, vMax);
            Color c = (i % 3 == 0) ? Color.white : (i % 3 == 1 ? accent : Color.Lerp(accent, UiKit.BrassHi, 0.5f));
            Emit(cx, cy, Mathf.Cos(a) * v, Mathf.Sin(a) * v - 120f, 700f, Random.Range(0.6f, 1.2f),
                 Random.Range(14f, 30f), 2f, c, Random.Range(-240f, 240f));
        }
    }

    void UpdateSparks(float dt)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (!s.alive) continue;
            s.life -= dt;
            if (s.life <= 0f)
            {
                s.alive = false;
                s.img.gameObject.SetActive(false);
                continue;
            }
            s.x += s.vx * dt;
            s.y += s.vy * dt;
            s.vy += s.g * dt;
            s.vx *= 1f - Mathf.Min(1f, 0.8f * dt);
            s.rot += s.vrot * dt;

            float t = 1f - s.life / s.max;
            float size = Mathf.Lerp(s.s0, s.s1, t);
            float a = Mathf.Clamp01(t * 12f) * Mathf.Pow(1f - t, 1.4f);

            s.rt.anchoredPosition = new Vector2(s.x, -s.y);
            s.rt.sizeDelta = new Vector2(size, size);
            s.rt.localEulerAngles = new Vector3(0f, 0f, s.rot);
            Color c = s.col; c.a = a * s.col.a;
            s.img.color = c;
        }
    }

    void ClearSparks()
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] == null) continue;
            sparks[i].alive = false;
            if (sparks[i].img != null) sparks[i].img.gameObject.SetActive(false);
        }
    }

    // ============================================================ экран 2: рулетка
    void BuildStrip(PetData winner, PetDatabase db)
    {
        // в ленте только те редкости, что реально выпадают из этой клети
        float[] odds = GameBalance.PetCrateOdds[(int)crate];
        var pool = new List<PetData>();
        if (db != null && db.pets != null)
            for (int i = 0; i < db.pets.Count; i++)
            {
                var pd = db.pets[i];
                if (pd == null) continue;
                int ri = (int)pd.rarity;
                if (ri < odds.Length && odds[ri] > 0f) pool.Add(pd);
            }
        if (pool.Count == 0) pool.Add(winner);

        PetData prev = null;
        for (int i = 0; i < CardCount; i++)
        {
            PetData d;
            if (i == WinIndex) d = winner;
            else
            {
                d = pool[Random.Range(0, pool.Count)];
                for (int t = 0; t < 4 && pool.Count > 1 && d == prev; t++) d = pool[Random.Range(0, pool.Count)];
            }
            prev = d;
            stripData[i] = d;
            stripCards[i].Set(d);
            stripCards[i].rt.localScale = new Vector3(0.84f, 0.84f, 1f);
        }

        stripX0 = MaskCenter - (StartIndex * Pitch + CardW * 0.5f);
        stripX1 = MaskCenter - (WinIndex * Pitch + CardW * 0.5f);       // здесь выпавшая карточка ровно под указателем
    }

    // карточки у центра крупнее, дальние мельче
    void UpdateStripLook(float x)
    {
        int c = Mathf.FloorToInt((MaskCenter - x) / Pitch);
        for (int i = c - 4; i <= c + 4; i++)
        {
            if (i < 0 || i >= CardCount) continue;
            float dist = Mathf.Abs(x + i * Pitch + CardW * 0.5f - MaskCenter);
            float s = Mathf.Lerp(1.05f, 0.84f, Mathf.Clamp01(dist / 380f));
            stripCards[i].rt.localScale = new Vector3(s, s, 1f);
        }
    }

    // имя и редкость карточки под указателем; ping: карточка редкая, указатель вспыхивает и сыплет искры
    void UpdateLive(int idx, bool ping)
    {
        idx = Mathf.Clamp(idx, 0, CardCount - 1);
        PetData d = stripData[idx];
        if (d == null) return;
        Color acc = RarityAccent(d.rarity);
        liveR = d.rarity;
        Set(liveName, PetUi.NameOf(d));
        liveName.color = acc;
        Set(liveRarity, PetText.Rarity(d.rarity).ToUpper());
        liveRarity.color = acc;
        popT = 1f;

        if (ping && d.rarity >= PetRarity.Rare && Time.unscaledTime - lastPingTime > 0.12f)
        {
            lastPingTime = Time.unscaledTime;
            pingT = 1f;
            Burst(InW * 0.5f, PointerCy, d.rarity == PetRarity.Legendary ? 9 : 5, acc, 120f, 320f);
        }
    }

    void SetPointerColor(Color c)
    {
        for (int i = 0; i < pointerBars.Count; i++) pointerBars[i].color = c;
    }

    void AnimateRoll(float dt)
    {
        rollT += dt;
        rollCg.alpha = Mathf.Clamp01(rollT / IntroTime);

        float u = Mathf.Clamp01(rollT / RollTime);
        float e = 1f - Mathf.Pow(1f - u, 3.5f);                          // ease-out: быстро в начале, плавно в конце
        float x = u >= 1f ? stripX1 : Mathf.Lerp(stripX0, stripX1, e);
        stripRt.anchoredPosition = new Vector2(x, 0f);
        UpdateStripLook(x);

        int idx = Mathf.FloorToInt((MaskCenter - x) / Pitch);            // карточка под указателем
        if (idx != lastTickIdx)
        {
            lastTickIdx = idx;
            UpdateLive(idx, true);
            if (u < 1f && Time.unscaledTime - lastTickTime > 0.035f)
            {
                lastTickTime = Time.unscaledTime;
                SfxManager.Play("ui", 0.3f, Mathf.Lerp(1.4f, 0.85f, e));
            }
        }

        // указатель подсвечивается цветом редкости карточки под ним, при редкой вспыхивает и «пульсирует»
        pointerK = Mathf.MoveTowards(pointerK, liveR >= PetRarity.Uncommon ? 1f : 0f, dt * 10f);
        pingT = Mathf.MoveTowards(pingT, 0f, dt * 3f);
        popT = Mathf.MoveTowards(popT, 0f, dt * 6f);
        Color pc = Color.Lerp(UiKit.Brass, RarityAccent(liveR), pointerK);
        SetPointerColor(Color.Lerp(pc, Color.white, pingT * 0.6f));
        float ps = 1f + 0.05f * pingT;
        pointerRt.localScale = new Vector3(ps, ps, 1f);
        float lp = 1f + 0.12f * popT;
        liveName.rectTransform.localScale = new Vector3(lp, lp, 1f);
        liveRarity.rectTransform.localScale = new Vector3(lp, lp, 1f);

        if (rollT >= RollTime + HoldTime) ShowResult();
    }

    // ============================================================ экран 1: подготовка (анимация)
    void AnimatePrepare(float dt)
    {
        float t = Time.unscaledTime;
        Color ca = PetUi.CrateAccent(crate);

        // ящик слегка покачивается, ореол дышит
        float sc = 1f + 0.035f * Mathf.Sin(t * 2.4f);
        crateImg.rectTransform.localScale = new Vector3(sc, sc, 1f);
        crateImg.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 1.3f) * 2.5f);
        Color h = ca; h.a = 0.30f + 0.10f * Mathf.Sin(t * 2f);
        crateHalo.color = h;
        Color rc = ca; rc.a = 0.16f + 0.04f * Mathf.Sin(t * 1.5f);
        crateRays.color = rc;
        crateRays.rectTransform.localEulerAngles = new Vector3(0f, 0f, -t * 8f);

        // строки шансов выезжают по очереди, полосы набирают длину
        prepT += dt;
        for (int i = 0; i < 4; i++)
        {
            if (!oddsRow[i].activeSelf) continue;
            float k = Mathf.Clamp01((prepT - i * 0.08f) / 0.28f);
            float e = UiDraw.EaseOutCubic(k);
            oddsCg[i].alpha = k;
            oddsRt[i].anchoredPosition = new Vector2(470f + (1f - e) * 50f, -(76f + i * 62f));
            float k2 = UiDraw.EaseOutCubic(Mathf.Clamp01((prepT - 0.15f - i * 0.08f) / 0.6f));
            oddsBar[i].rectTransform.anchorMax = new Vector2(oddsTarget[i] * k2, 1f);
        }

        // искры поднимаются вокруг ящика
        ambientT -= dt;
        if (ambientT <= 0f)
        {
            ambientT = 0.16f;
            Emit(CrateCx + Random.Range(-100f, 100f), CrateCy + Random.Range(-10f, 100f),
                 Random.Range(-14f, 14f), -Random.Range(30f, 70f), 0f, Random.Range(1.1f, 1.7f),
                 Random.Range(9f, 18f), 0f, Random.value < 0.4f ? Color.white : ca, Random.Range(-60f, 60f));
        }
    }

    // ============================================================ экран 3: выпадение
    void ShowResult()
    {
        if (result == null || result.data == null) { SetStep(Step.Prepare); Refresh(); return; }
        var pm = PetManager.Instance;
        PetData d = result.data;
        resultData = d;
        Color accent = RarityAccent(d.rarity);

        SetStep(Step.Result);
        resultCard.LiveStage = PetManager.Instance != null ? PetManager.Instance.GrowthStage : 2;
        resultCard.Set(d);

        Set(nameText, PetUi.NameOf(d));
        nameText.color = Color.white;
        Set(rarityText, PetText.Rarity(d.rarity));
        rarityText.color = accent;
        gemL.color = accent; gemR.color = accent;

        // строка стата с иконкой по центру
        Set(statText, PetUi.StatLine(d));
        statBadgeIcon.sprite = UiKit.Icon(PetUi.StatIcon(d.stat));
        float tw = statText.GetPreferredValues(statText.text).x;
        float total = 46f + 12f + tw;
        float sx = (InW - total) * 0.5f;
        TLC(statBadge.rectTransform, sx + 23f, 401f, 46f, 46f);
        TL(statText.rectTransform, sx + 58f, 382f, tw + 24f, 38f);

        int count = pm != null ? pm.CountOf(d) : 1;
        isNew = count <= 1;
        Set(dupText, isNew
            ? "<color=#" + Hex(UiKit.Good) + ">Новый вид в коллекции!</color>"
            : "Дубликат: теперь у тебя x" + count
                + (count >= GameBalance.PetMergeCount ? "  <size=70%><color=#" + Hex(Dim) + ">(можно слить в окне питомцев, клавиша P)</color></size>" : ""));

        resultT = 0f;
        flashT = 0f;
        flashed = false;
        backGo.SetActive(true);
        resultCard.rt.localScale = Vector3.one;
        resultInfo.alpha = 0f;
        resultInfo.blocksRaycasts = false;
        newTag.gameObject.SetActive(false);

        Color c = accent; c.a = 0f;
        burst.color = c; glowIdle.color = c; rays.color = c;
        cardFlash.color = new Color(1f, 1f, 1f, 0f);
        screenFlash.color = c;
        burst.rectTransform.localScale = Vector3.one * 0.4f;
        cardFlash.rectTransform.SetAsLastSibling();

        RefreshResultButtons();
    }

    // сила эффектов по редкости: 0 обычный .. 3 легендарный
    static float RayAlpha(PetRarity r)
    {
        switch (r)
        {
            case PetRarity.Common: return 0f;
            case PetRarity.Uncommon: return 0.16f;
            case PetRarity.Rare: return 0.34f;
            default: return 0.55f;
        }
    }

    void OnReveal(PetRarity rar, Color accent)
    {
        int n = rar == PetRarity.Legendary ? 46 : rar == PetRarity.Rare ? 30 : rar == PetRarity.Uncommon ? 20 : 14;
        Burst(CardCx, CardCy, n, accent, 220f, rar >= PetRarity.Rare ? 720f : 520f);
        if (rar == PetRarity.Legendary) { shakeAmp = 16f; shakeT = 0.45f; }
        else if (rar == PetRarity.Rare) { shakeAmp = 9f; shakeT = 0.45f; }
    }

    void AnimateResult(float dt)
    {
        resultT += dt;
        float f = Mathf.Clamp01(resultT / FlipTime);

        float sx;
        if (f < 0.5f) sx = Mathf.Lerp(1f, 0f, f * 2f);
        else sx = Mathf.Lerp(0f, 1f, UiDraw.EaseOutCubic((f - 0.5f) * 2f));
        float sy = 1f + 0.06f * Mathf.Sin(f * Mathf.PI);
        resultCard.rt.localScale = new Vector3(Mathf.Max(sx, 0.02f), sy, 1f);
        backGo.SetActive(f < 0.5f);

        PetRarity rar = resultData != null ? resultData.rarity : PetRarity.Common;
        Color accent = RarityAccent(rar);

        if (!flashed && f >= 0.5f)
        {
            flashed = true;
            flashT = 0f;
            PlayRevealSfx();
            OnReveal(rar, accent);
        }

        if (flashed)
        {
            flashT += dt;
            float fu = Mathf.Clamp01(flashT / FlashTime);

            Color b = accent; b.a = (1f - fu) * 0.9f;
            burst.color = b;
            burst.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.7f, UiDraw.EaseOutCubic(fu));

            Color cf = Color.Lerp(accent, Color.white, 0.55f); cf.a = (1f - fu) * 0.75f;
            cardFlash.color = cf;

            float baseA = rar >= PetRarity.Rare ? 0.45f : 0.22f;
            Color g = accent;
            g.a = baseA * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 2.5f)) * Mathf.Clamp01(flashT / 0.3f);
            glowIdle.color = g;

            // вращающиеся лучи за карточкой
            Color rc = accent; rc.a = RayAlpha(rar) * Mathf.Clamp01(flashT / 0.5f);
            rays.color = rc;
            rays.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * (10f + 6f * (int)rar));
            float rs = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 1.7f);
            rays.rectTransform.localScale = new Vector3(rs, rs, 1f);

            // цветная вспышка на весь экран для редких
            float fa = rar == PetRarity.Legendary ? 0.30f : rar == PetRarity.Rare ? 0.20f : rar == PetRarity.Uncommon ? 0.10f : 0f;
            Color sf = accent; sf.a = fa * (1f - fu);
            screenFlash.color = sf;

            // тег «НОВЫЙ!» выскакивает с отскоком
            if (isNew)
            {
                float tt = Mathf.Clamp01((flashT - 0.25f) / 0.35f);
                newTag.gameObject.SetActive(tt > 0f);
                float ts = Mathf.Max(0.01f, UiDraw.EaseOutBack(tt));
                newTag.rectTransform.localScale = new Vector3(ts, ts, 1f);
            }

            // у Необычных и выше вокруг карточки не гаснут искры
            if (rar >= PetRarity.Uncommon)
            {
                ambientT -= dt;
                if (ambientT <= 0f)
                {
                    ambientT = rar == PetRarity.Legendary ? 0.05f : rar == PetRarity.Rare ? 0.09f : 0.18f;
                    Emit(CardCx + Random.Range(-170f, 170f), CardCy + Random.Range(-120f, 140f),
                         Random.Range(-16f, 16f), -Random.Range(40f, 90f), 0f, Random.Range(1f, 1.6f),
                         Random.Range(10f, 20f), 0f, Random.value < 0.45f ? Color.white : accent, Random.Range(-90f, 90f));
                }
            }

            // камни редкости и медальон стата выскакивают с отскоком
            float pk = UiDraw.EaseOutBack(Mathf.Clamp01((flashT - 0.12f) / 0.4f));
            gemL.rectTransform.localScale = Vector3.one * pk;
            gemR.rectTransform.localScale = Vector3.one * pk;
            statBadge.rectTransform.localScale = Vector3.one * UiDraw.EaseOutBack(Mathf.Clamp01((flashT - 0.25f) / 0.4f));

            resultInfo.alpha = Mathf.Clamp01(flashT / 0.35f);
            resultInfo.blocksRaycasts = resultInfo.alpha > 0.6f;
        }
    }

    void Animate(float dt)
    {
        UpdateSparks(dt);
        if (step == Step.Prepare) AnimatePrepare(dt);
        else if (step == Step.Roll) AnimateRoll(dt);
        else if (step == Step.Result) AnimateResult(dt);
    }

    void PlayRevealSfx()
    {
        PetRarity r = resultData != null ? resultData.rarity : PetRarity.Common;
        switch (r)
        {
            case PetRarity.Common: SfxManager.Play("coin", 0.8f, 1f); break;
            case PetRarity.Uncommon: SfxManager.Play("coin", 0.9f, 1.2f); break;
            case PetRarity.Rare:
                SfxManager.Play("level", 0.9f, 1f);
                SfxManager.Play("coin", 0.6f, 1.4f);
                break;
            default:
                SfxManager.Play("quest", 1f, 1f);
                SfxManager.Play("level", 0.7f, 1.25f);
                break;
        }
    }

    void RefreshResultButtons()
    {
        var pm = PetManager.Instance;
        if (pm == null || result == null) return;

        bool isEq = pm.IsEquipped(result.uid);
        bool free = pm.EquippedCount < pm.SlotCount;
        SetBtn(equipBtn, equipLabel, !isEq && free, UiKit.Navy);
        Set(equipLabel, isEq ? "Взят" : "Взять с собой");

        if (isEq) Set(slotNote, "<color=#" + Hex(UiKit.Good) + ">Питомец теперь с тобой</color>");
        else if (!free) Set(slotNote, "<color=#" + Hex(UiKit.Warn) + ">Все слоты заняты</color>");
        else Set(slotNote, "");
    }
}
