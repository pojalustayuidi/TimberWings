// Assets/_Project/Scripts/UI/Pets/PetCrateWindow.Update.cs (НЕ в Editor)
// ЧАСТЬ класса PetCrateWindow (partial): покадровое обновление окна.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCrateWindow.cs.
// Остальные части: PetCrateWindow.Screens.cs, PetCrateWindow.Build.cs.
// Методы этой части: Set, SetBtn, Refresh, SetPriceLayout, RefreshPrepare.

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
    // ============================================================ обновление
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void SetBtn(Button b, TMP_Text label, bool on, Color onColor)
    {
        if (b == null) return;
        b.interactable = on;
        if (label != null) label.color = on ? onColor : OffText;
    }

    void Refresh()
    {
        refreshAt = Time.unscaledTime + 0.1f;
        if (!built) return;
        if (inv == null) inv = PlayerInventory.Instance;

        Set(coinsText, (inv != null ? inv.coinsCount : 0).ToString());
        Set(titleText, PetText.Crate(crate).ToUpper());

        if (step == Step.Prepare) RefreshPrepare();
        else if (step == Step.Result) RefreshResultButtons();
    }

    void SetPriceLayout(bool free)
    {
        int want = free ? 1 : 0;
        if (priceLayout == want) return;
        priceLayout = want;
        priceCoin.gameObject.SetActive(!free);
        if (free)
        {
            TL(priceText.rectTransform, 0f, 378f, 420f, 50f);
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.color = UiKit.Good;
        }
        else
        {
            TL(priceText.rectTransform, 182f, 378f, 220f, 50f);
            priceText.alignment = TextAlignmentOptions.Left;
            priceText.color = UiKit.BrassHi;
        }
    }

    void RefreshPrepare()
    {
        var pm = PetManager.Instance;
        int price = pm != null ? pm.Price(crate) : GameBalance.PetCratePrice[(int)crate];
        SetPriceLayout(price <= 0);
        Set(priceText, price <= 0 ? "БЕСПЛАТНО" : price.ToString());

        // шансы: показываем только редкости с шансом больше нуля, рядом сколько видов в этой редкости
        float[] odds = GameBalance.PetCrateOdds[(int)crate];
        int row = 0;
        for (int r = 0; r < 4; r++)
        {
            float v = r < odds.Length ? odds[r] : 0f;
            if (v <= 0f) continue;
            Color acc = RarityAccent((PetRarity)r);
            oddsRow[row].SetActive(true);
            oddsGem[row].color = acc;
            int kinds = 0;
            if (pm != null && pm.database != null)
            {
                pm.database.CollectByRarity((PetRarity)r, tmpPets);
                kinds = tmpPets.Count;
            }
            Set(oddsName[row], PetText.Rarity((PetRarity)r)
                + "  <size=60%><color=#" + Hex(Dim) + ">" + kinds + " " + UiDraw.Plural(kinds, "вид", "вида", "видов") + "</color></size>");
            oddsName[row].color = acc;
            Set(oddsPct[row], v.ToString("0.#", CultureInfo.InvariantCulture) + "%");
            oddsBar[row].color = RarityFill((PetRarity)r);
            oddsTarget[row] = Mathf.Clamp(v / 100f, 0.07f, 1f);
            row++;
        }
        for (; row < 4; row++) oddsRow[row].SetActive(false);

        // гарантия: полоса заполняется с каждой клетью без Редкого
        if (pm != null)
        {
            int until = pm.OpensUntilGuarantee(crate);
            bool next = until <= 1;
            Set(guaranteeText, next
                ? "<color=#" + Hex(UiKit.Warn) + ">Следующая клеть даст Редкого!</color>"
                : "До Редкого: <b>" + until + "</b>  <size=70%><color=#" + Hex(Dim) + ">(гарантия каждые " + GameBalance.PetGuaranteeEvery + ")</color></size>");
            pityFill.fillAmount = Mathf.Clamp01((GameBalance.PetGuaranteeEvery - until) / (float)Mathf.Max(1, GameBalance.PetGuaranteeEvery - 1));
        }
        else
        {
            Set(guaranteeText, "");
            pityFill.fillAmount = 0f;
        }

        // можно ли открыть, и если нет, то почему
        bool ok = false;
        string reason = "";
        if (pm == null)
            reason = "Питомцы недоступны: на сцене нет PetManager";
        else
        {
            PetOpenResult why;
            ok = pm.CanOpen(crate, out why);
            if (!ok)
            {
                switch (why)
                {
                    case PetOpenResult.NoCoins: reason = "Не хватает монет"; break;
                    case PetOpenResult.Locked: reason = "С " + GameBalance.PetCrateLevel[(int)crate] + " уровня"; break;
                    case PetOpenResult.NoDatabase: reason = "Питомцы недоступны: не подключена база питомцев"; break;
                    default: reason = "Питомцы недоступны"; break;
                }
            }
        }
        Set(reasonText, reason);
        SetBtn(openBtn, openLabel, ok, UiKit.Navy);
    }
}
