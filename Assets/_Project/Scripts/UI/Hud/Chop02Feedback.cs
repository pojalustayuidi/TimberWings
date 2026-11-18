using UnityEngine;

// Lightweight runtime-only experimental overlay. No scene/prefab changes, no
// dependencies on the final HUD art, and no persistent PlayerPrefs keys.
internal sealed class Chop02Feedback : MonoBehaviour
{
    static Chop02Feedback instance;
    Transform anchor;
    string modeName, hint, toast, pulse;
    bool visible, showMeter, greenZone, powerWindow;
    float cutProgress, marker, zoneHalf, toastUntil, pulseUntil;
    GUIStyle titleStyle, hintStyle, toastStyle, pulseStyle;
    Texture2D pixel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() { instance = null; }

    static Chop02Feedback Get()
    {
        if (instance != null) return instance;
        var go = new GameObject("CHOP-02 experimental feedback");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Chop02Feedback>();
        return instance;
    }

    internal static void DrawNear(Transform at, string name, float progress, bool meter,
        float value, float half, bool isGreen, bool power, string message)
    {
        if (at == null) return;
        var f = Get();
        f.anchor = at;
        f.modeName = name;
        f.visible = true;
        f.cutProgress = Mathf.Clamp01(progress);
        f.showMeter = meter;
        f.marker = Mathf.Clamp01(value);
        f.zoneHalf = Mathf.Clamp01(half);
        f.greenZone = isGreen;
        f.powerWindow = power;
        f.hint = message;
    }

    internal static void Hide()
    {
        if (instance != null) instance.visible = false;
    }

    internal static void ModeChanged(string name)
    {
        var f = Get();
        f.toast = "CHOP-02  /  " + name;
        f.toastUntil = Time.unscaledTime + 2.0f;
        f.visible = false;
    }

    internal static void Pulse(string message)
    {
        var f = Get();
        f.pulse = message;
        f.pulseUntil = Time.unscaledTime + .70f;
    }

    void EnsureSkin()
    {
        if (titleStyle != null) return;
        pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "CHOP-02 HUD pixel", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave
        };
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(.99f, .91f, .73f) }
        };
        hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 10,
            normal = { textColor = new Color(.92f, .88f, .76f) }
        };
        toastStyle = new GUIStyle(titleStyle) { fontSize = 13 };
        pulseStyle = new GUIStyle(titleStyle) { fontSize = 12 };
    }

    void RectColor(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, pixel);
        GUI.color = old;
    }

    void OnGUI()
    {
        if ((!visible || anchor == null) && Time.unscaledTime >= toastUntil &&
            Time.unscaledTime >= pulseUntil) return;
        EnsureSkin();
        int oldDepth = GUI.depth;
        GUI.depth = -3;
        if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toast))
        {
            float width = Mathf.Min(290f, Screen.width - 24f);
            Rect toastRect = new Rect((Screen.width - width) * .5f, 22f, width, 30f);
            RectColor(toastRect, new Color(.21f, .20f, .15f, .88f));
            GUI.Label(toastRect, toast, toastStyle);
        }
        if (visible && anchor != null && Camera.main != null)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(anchor.position + Vector3.up * .22f);
            if (screen.z > .08f)
            {
                const float width = 192f;
                float x = Mathf.Clamp(screen.x - width * .5f, 8f, Mathf.Max(8f, Screen.width - width - 8f));
                float y = Mathf.Clamp(Screen.height - screen.y - 90f, 56f, Mathf.Max(56f, Screen.height - 84f));
                Rect bg = new Rect(x, y, width, showMeter ? 72f : 49f);
                RectColor(bg, new Color(.20f, .18f, .14f, .84f));
                RectColor(new Rect(x + 9f, y + 29f, width - 18f, 5f), new Color(.43f, .37f, .26f));
                RectColor(new Rect(x + 9f, y + 29f, (width - 18f) * cutProgress, 5f),
                    new Color(.82f, .64f, .33f));
                GUI.Label(new Rect(x + 5f, y + 4f, width - 10f, 21f), modeName ?? "CHOP-02", titleStyle);
                if (showMeter)
                {
                    float barX = x + 17f, barY = y + 40f, barW = width - 34f;
                    RectColor(new Rect(barX, barY, barW, 7f), new Color(.53f, .48f, .35f));
                    if (greenZone)
                        RectColor(new Rect(barX + barW * (.5f - zoneHalf), barY,
                            barW * zoneHalf * 2f, 7f), new Color(.52f, .83f, .42f));
                    else if (powerWindow)
                        RectColor(new Rect(barX, barY, barW, 7f), new Color(.42f, .76f, .45f));
                    float px = barX + marker * barW;
                    RectColor(new Rect(px - 2f, barY - 3f, 4f, 13f), new Color(1f, .89f, .59f));
                    GUI.Label(new Rect(x + 5f, y + 51f, width - 10f, 18f), hint ?? "", hintStyle);
                }
                else
                    GUI.Label(new Rect(x + 5f, y + 34f, width - 10f, 14f), hint ?? "", hintStyle);
            }
        }
        if (Time.unscaledTime < pulseUntil && !string.IsNullOrEmpty(pulse))
        {
            float pulseY = Mathf.Max(64f, Screen.height * .26f);
            Rect b = new Rect(Screen.width * .5f - 106f, pulseY, 212f, 28f);
            RectColor(b, new Color(.18f, .30f, .18f, .70f));
            GUI.Label(b, pulse, pulseStyle);
        }
        GUI.depth = oldDepth;
    }

    void OnDestroy()
    {
        if (pixel != null) Destroy(pixel);
        if (instance == this) instance = null;
    }
}
