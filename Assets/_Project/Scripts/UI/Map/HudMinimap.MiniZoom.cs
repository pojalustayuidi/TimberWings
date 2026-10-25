// Assets/_Project/Scripts/UI/Map/HudMinimap.MiniZoom.cs (НЕ в Editor)
// Зум мини-карты: колесо мыши над кругом, клавиши [ и ], кнопки + и - у левой кромки круга (для телефона). Плавный, запоминается.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HudMinimap
{
    [Header("Зум мини-карты")]
    public bool miniZoomEnabled = true;
    public float minMiniZoom = 0.7f, maxMiniZoom = 2.5f;
    public bool miniZoomButtons = true;

    float miniZoom = 1f, miniZoomTarget = 1f;
    Button miniZoomIn, miniZoomOut;
    const string ZoomPref = "minimap_zoom";

    // сколько метров мира видно в круге мини-карты сейчас
    float ViewWorld { get { return miniViewWorld / Mathf.Max(0.1f, miniZoom); } }

    void InitMiniZoom()
    {
        if (!miniZoomEnabled) return;
        miniZoomTarget = miniZoom = Mathf.Clamp(PlayerPrefs.GetFloat(ZoomPref, 1f), minMiniZoom, maxMiniZoom);
        if (miniZoomButtons) BuildMiniZoomButtons();
    }

    public void MiniZoomIn() { SetMiniZoom(miniZoomTarget * 1.3f); }
    public void MiniZoomOut() { SetMiniZoom(miniZoomTarget / 1.3f); }

    void SetMiniZoom(float z)
    {
        miniZoomTarget = Mathf.Clamp(z, minMiniZoom, maxMiniZoom);
        PlayerPrefs.SetFloat(ZoomPref, miniZoomTarget);
    }

    void UpdateMiniZoom(float dt)
    {
        if (!miniZoomEnabled) return;
        if (!open)
        {
            if (Input.GetKeyDown(KeyCode.RightBracket)) MiniZoomIn();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) MiniZoomOut();
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && miniMask != null)
            {
                var cv = miniMask.GetComponentInParent<Canvas>();
                Camera cam = cv != null && cv.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cv.rootCanvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(miniMask, Input.mousePosition, cam)) SetMiniZoom(miniZoomTarget * Mathf.Pow(1.2f, wheel));
            }
        }
        miniZoom = Mathf.Lerp(miniZoom, miniZoomTarget, 1f - Mathf.Exp(-12f * dt));
        if (Mathf.Abs(miniZoom - miniZoomTarget) < 0.001f) miniZoom = miniZoomTarget;
        if (miniZoomIn != null) miniZoomIn.interactable = miniZoomTarget < maxMiniZoom - 0.01f;
        if (miniZoomOut != null) miniZoomOut.interactable = miniZoomTarget > minMiniZoom + 0.01f;
        FollowMiniZoomButtons();
    }

    // Кнопки стоят у левой кромки круга и каждый кадр едут за ним в мировых координатах:
    // не зависят от якорей, safe area и масштаба Canvas (раньше при растянутых якорях sizeDelta добавлялся к размеру родителя и кнопки превращались в большой квадрат)
    void FollowMiniZoomButtons()
    {
        if (miniMask == null) return;
        float r = miniMask.rect.width * 0.5f;
        Vector2 c = miniMask.rect.center;
        if (miniZoomIn != null) miniZoomIn.transform.position = miniMask.TransformPoint(c + new Vector2(-r * 0.97f, 22f));
        if (miniZoomOut != null) miniZoomOut.transform.position = miniMask.TransformPoint(c + new Vector2(-r * 0.97f, -22f));
        bool show = !open;                                                       // пока открыта большая карта, мини-кнопки не нужны
        if (miniZoomIn != null && miniZoomIn.gameObject.activeSelf != show) miniZoomIn.gameObject.SetActive(show);
        if (miniZoomOut != null && miniZoomOut.gameObject.activeSelf != show) miniZoomOut.gameObject.SetActive(show);
    }

    void BuildMiniZoomButtons()
    {
        if (miniMask == null || miniMask.parent == null) return;
        var parent = (RectTransform)miniMask.parent;
        miniZoomIn = MakeMiniZoomBtn("+", Vector2.zero, parent);
        miniZoomOut = MakeMiniZoomBtn("-", Vector2.zero, parent);
        if (miniZoomIn != null) miniZoomIn.onClick.AddListener(MiniZoomIn);
        if (miniZoomOut != null) miniZoomOut.onClick.AddListener(MiniZoomOut);
        FollowMiniZoomButtons();
    }

    // Общий вид кнопок +/- (SkinZoomButton) лежит в HudMinimap.Candy.cs: Candy-желе или прежний вид

    Button MakeMiniZoomBtn(string label, Vector2 pos, RectTransform parent)
    {
        var go = new GameObject("MiniZoom" + (label == "+" ? "In" : "Out"), typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = miniMask.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);       // точечные якоря: размер строго 36x36
        rt.sizeDelta = new Vector2(36f, 36f);
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;
        var im = go.GetComponent<Image>();
        var b = go.GetComponent<Button>();
        SkinZoomButton(im, b, 0.6f);
        var tgo = new GameObject("Label", typeof(RectTransform));
        tgo.layer = go.layer;
        var tr = (RectTransform)tgo.transform;
        tr.SetParent(rt, false);
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var tx = tgo.AddComponent<TextMeshProUGUI>();
        SetLabelFont(tx);
        tx.text = label; tx.fontSize = 28f; tx.fontStyle = FontStyles.Bold;
        tx.alignment = TextAlignmentOptions.Center; tx.color = ZoomLabelColor; tx.raycastTarget = false;
        return b;
    }
}
