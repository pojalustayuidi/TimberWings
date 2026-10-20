// Умное позиционирование: safeArea + clamp + масштаб + краевой указатель
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class SmartWorldTag : MonoBehaviour
{
    public Vector3 worldOffset = new Vector3(0, 2.5f, 0);
    public float minScale = 0.7f, maxScale = 1.3f, refDist = 15f;
    public bool useSafeArea = true, useEdgeArrow = true;
    public Sprite buildingIcon;
    WorldTag worldTag; RectTransform rt, root, edge; TMP_Text edgeTxt; Camera cam; HudSafeAreaRegistry safe;
    int lastDist = -1;
    float findTagAt;
    void Awake()
    {
        worldTag = GetComponent<WorldTag>(); if (worldTag != null) worldOffset = worldTag.offset;
        var c = GameObject.Find("WorldTags_Canvas");
        root = c != null ? (RectTransform)c.transform : (RectTransform)FindFirstObjectByType<Canvas>().transform;
        cam = Camera.main;
        if (HudSafeAreaRegistry.Instance == null) { var go = new GameObject("HudSafeAreaRegistry"); DontDestroyOnLoad(go); go.AddComponent<HudSafeAreaRegistry>(); }
        safe = HudSafeAreaRegistry.Instance;
    }
    void LateUpdate()
    {
        if (cam == null) cam = Camera.main; if (cam == null || root == null) return;
        if (rt == null && worldTag != null)
        {
            if (Time.unscaledTime < findTagAt) return;       // раньше полный обход Canvas с поиском по имени шёл каждый кадр
            findTagAt = Time.unscaledTime + 0.5f;
            foreach (RectTransform ch in root.GetComponentsInChildren<RectTransform>(true))
                if (ch.name.StartsWith("Tag_") && ch.name.Contains(name)) { rt = ch; break; }
            if (rt == null) return;
        }
        if (rt == null) return;
        Vector3 wp = transform.position + worldOffset;
        Vector3 sp3 = cam.WorldToScreenPoint(wp);
        bool behind = sp3.z < 0.2f;
        float dist = Vector3.Distance(cam.transform.position, wp);
        float s = Mathf.Clamp(1f - (dist - refDist) * 0.02f, minScale, maxScale);
        float punchMag = rt.localScale.magnitude / 1.732f;
        rt.localScale = Vector3.one * s * Mathf.Clamp(punchMag / s, 0.9f, 1.4f);
        Vector2 tagSize = rt.rect.size * s * Mathf.Max(0.001f, root.lossyScale.x);
        if (tagSize.x < 10) tagSize = new Vector2(200, 70) * s;
        Vector2 sp = new Vector2(sp3.x, sp3.y);
        bool off = behind || sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height;
        if (off && useEdgeArrow) { ShowEdge(sp, dist); return; }
        HideEdge();
        if (useSafeArea && safe != null && safe.GetSafeRect(sp, tagSize, out Rect sr)) sp = safe.PushOutOfSafe(sp, sr, tagSize);
        sp.x = Mathf.Clamp(sp.x, tagSize.x * 0.5f + 10f, Screen.width - tagSize.x * 0.5f - 10f);
        sp.y = Mathf.Clamp(sp.y, tagSize.y * 0.5f + 10f, Screen.height - tagSize.y * 0.5f - 10f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out Vector2 lp);
        rt.anchoredPosition = lp;
    }
    void ShowEdge(Vector2 sp, float dist)
    {
        if (worldTag != null) worldTag.visible = false;
        EnsureEdge(); edge.gameObject.SetActive(true);
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 d = sp - center; if (d.sqrMagnitude < 1f) d = Vector2.up; d.Normalize();
        edge.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
        float tx = (Screen.width * 0.5f - 70f) / Mathf.Max(0.01f, Mathf.Abs(d.x));
        float ty = (Screen.height * 0.5f - 70f) / Mathf.Max(0.01f, Mathf.Abs(d.y));
        Vector2 epos = center + d * Mathf.Min(tx, ty);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, epos, null, out Vector2 lp);
        edge.anchoredPosition = lp;
        int dm = Mathf.RoundToInt(dist);
        if (dm != lastDist) { lastDist = dm; if (edgeTxt != null) edgeTxt.text = dm + "м"; }
    }
    void HideEdge() { if (edge != null) edge.gameObject.SetActive(false); if (worldTag != null) worldTag.visible = true; lastDist = -1; }
    void EnsureEdge()
    {
        if (edge != null) return;
        var go = new GameObject("Edge_" + name, typeof(RectTransform), typeof(Image));
        go.layer = 5; go.transform.SetParent(root, false);
        edge = (RectTransform)go.transform; edge.sizeDelta = new Vector2(78, 78);
        var img = go.GetComponent<Image>(); img.sprite = UiKit.Medallion; img.type = Image.Type.Simple; img.color = Color.white; img.raycastTarget = false;
        var ring = new GameObject("Ring", typeof(RectTransform), typeof(Image)); ring.layer = 5; ring.transform.SetParent(edge, false);
        var rr = (RectTransform)ring.transform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
        var ri = ring.GetComponent<Image>(); ri.sprite = UiDraw.PillRing; ri.type = Image.Type.Sliced; ri.color = new Color32(0x3A, 0x4A, 0x70, 255); ri.raycastTarget = false; ri.enabled = false;
        var ic = new GameObject("Icon", typeof(RectTransform), typeof(Image)); ic.layer = 5; ic.transform.SetParent(edge, false);
        var ir = (RectTransform)ic.transform; ir.sizeDelta = new Vector2(36, 36); ir.anchoredPosition = new Vector2(0, 10);
        var ii = ic.GetComponent<Image>(); ii.sprite = buildingIcon != null ? buildingIcon : UiKit.Icon(UiIcon.Log); ii.preserveAspect = true; ii.raycastTarget = false;
        var ar = new GameObject("Arrow", typeof(RectTransform), typeof(Image)); ar.layer = 5; ar.transform.SetParent(edge, false);
        var arr = (RectTransform)ar.transform; arr.sizeDelta = new Vector2(22, 22); arr.anchoredPosition = new Vector2(0, -18);
        var ai = ar.GetComponent<Image>(); ai.sprite = UiDraw.Star; ai.color = new Color32(0x6B, 0xCB, 0x5A, 255); ai.raycastTarget = false;
        var tg = new GameObject("Dist", typeof(RectTransform), typeof(TextMeshProUGUI)); tg.layer = 5; tg.transform.SetParent(edge, false);
        var tr = (RectTransform)tg.transform; tr.anchoredPosition = new Vector2(0, -34); tr.sizeDelta = new Vector2(80, 20);
        edgeTxt = tg.GetComponent<TextMeshProUGUI>();
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null) edgeTxt.font = UIManager.Instance.objectiveText.font;
        edgeTxt.fontSize = 20; edgeTxt.alignment = TextAlignmentOptions.Center; edgeTxt.color = Color.white; edgeTxt.fontStyle = FontStyles.Bold;
        edgeTxt.outlineWidth = 0.22f; edgeTxt.outlineColor = new Color32(0x14, 0x1C, 0x30, 255); edgeTxt.raycastTarget = false;
    }
    void OnDestroy() { if (edge != null) Destroy(edge.gameObject); }
}