// Assets/_Project/Scripts/UI/Hud/HudBarSpark.cs (НЕ в Editor)
// Искра на конце полоски опыта: едет вместе с заливкой, мерцает, прячется на 0% и на 100%.
// Поля назначает BuilderSimHud: fill, spark, sparkImage, track.
using UnityEngine;
using UnityEngine.UI;

public class HudBarSpark : MonoBehaviour
{
    public Image fill;
    public RectTransform spark;
    public Image sparkImage;
    public RectTransform track;
    public float inset = 3f;   // отступ заливки от края дорожки (как в билдере)

    float baseAlpha = 0.9f;

    void Awake()
    {
        if (sparkImage != null) baseAlpha = sparkImage.color.a;
    }

    void LateUpdate()
    {
        if (fill == null || spark == null || track == null) return;

        float f = fill.fillAmount;
        float w = track.rect.width - inset * 2f;
        spark.anchoredPosition = new Vector2(inset + w * f, 0f);

        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f / 0.04f));
        float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - 0.97f) / 0.03f));
        float vis = fadeIn * fadeOut;

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
        spark.localScale = Vector3.one * (0.9f + 0.2f * pulse);

        if (sparkImage != null)
        {
            var c = sparkImage.color;
            c.a = baseAlpha * vis * pulse;
            sparkImage.color = c;
        }
    }
}
