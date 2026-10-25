// Assets/_Project/Scripts/UI/Progression/AchievementsWindow.HudHelpers.cs (НЕ в Editor)
// ЧАСТЬ класса AchievementsWindow (partial): кнопка кубка в HUD и мелкие помощники.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: AchievementsWindow.cs.
// Остальные части: AchievementsWindow.Anim.cs, AchievementsWindow.TabsData.cs, AchievementsWindow.Build.cs.
// Методы этой части: BuildHud, UpdateHud, Set, AddBtn.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;
using static AchUi;

public partial class AchievementsWindow
{
    // ============================================================ кнопка в HUD
    void BuildHud()
    {
        var cgo = new GameObject("AchievementsHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.HudOverlay;
        ConfigScaler(cgo.GetComponent<CanvasScaler>());
        hudGroup = cgo.GetComponent<CanvasGroup>();
        hudGroup.alpha = 0f;
        hudGroup.blocksRaycasts = false;

        var btn = UiKit.MakeButton(cgo.transform, "", UiBtn.Yellow, new Vector2(HudBtn, HudBtn), 20f, 1f);
        btn.name = "AchievementsButton";
        hudRt = btn.GetComponent<RectTransform>();
        // справа налево: «Прокачка», питомцы, достижения
        float x = -(UpgradeWindow.HudRight + UpgradeWindow.HudBtnW + UpgradeWindow.HudGap + HudBtn + UpgradeWindow.HudGap);
        Anch(hudRt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(x, UpgradeWindow.HudBottom), new Vector2(HudBtn, HudBtn));
        btn.onClick.AddListener(Toggle);

        hudIcon = UiKit.MakeIcon(btn.transform, UiIcon.Trophy, 58f, "Cup");
        hudIcon.rectTransform.anchoredPosition = new Vector2(0f, 2f);

        if (!Application.isMobilePlatform)
        {
            var key = UiKit.MakeKeycap(btn.transform, "K", 38f);
            Anch(key.rectTransform, new Vector2(0f, 1f), Ctr, new Vector2(8f, -8f), new Vector2(38f, 38f));
        }

        // красная точка с числом: награды ждут
        var dot = UiKit.MakeImage(btn.transform, "Dot", UiKit.Medallion, false);
        dot.color = DotColor;
        hudDot = dot.rectTransform;
        Anch(hudDot, new Vector2(1f, 1f), Ctr, new Vector2(-8f, -8f), new Vector2(34f, 34f));
        dotLabel = UiKit.MakeLabel(dot.transform, "!", 22f, Color.white, TextAlignmentOptions.Center, true);
        Stretch(dotLabel.rectTransform);
        hudDot.gameObject.SetActive(false);
    }

    void UpdateHud(float dt)
    {
        if (hudGroup == null) return;
        if (!hudRegistered && HudSafeAreaRegistry.Instance != null)
        {
            HudSafeAreaRegistry.Instance.Register(hudRt);
            hudRegistered = true;
        }

        bool visible = Available && !MobileInput.ModalOpen && !DialogueManager.IsBusy && !DialogueSequence.AnyPlaying;
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, visible ? 1f : 0f, dt / 0.2f);
        bool on = visible && hudGroup.alpha > 0.5f;
        hudGroup.blocksRaycasts = on;
        hudGroup.interactable = on;

        var m = AchievementManager.Instance;
        int n = visible && m != null ? m.ClaimableCount : 0;
        bool hint = n > 0;
        if (hudDot.gameObject.activeSelf != hint) hudDot.gameObject.SetActive(hint);
        if (hint)
        {
            Set(dotLabel, n > 9 ? "9+" : n.ToString());
            dotT += dt;
            hudDot.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(dotT * 5f));
            // кубок встряхивается раз в пару секунд: «забери меня»
            float kick = Mathf.Clamp01(Mathf.Sin(dotT * 1.6f) * 3f - 1.5f);
            hudIcon.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(dotT * 18f) * 10f * kick);
        }
        else if (hudIcon != null) hudIcon.rectTransform.localEulerAngles = Vector3.zero;
    }

    // ============================================================ мелкие помощники
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void AddBtn(Image img, UnityEngine.Events.UnityAction a)
    {
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(a);
    }
}
