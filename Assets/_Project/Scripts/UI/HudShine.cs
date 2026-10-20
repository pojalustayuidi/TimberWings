// Assets/_Project/Scripts/UI/HudShine.cs
// Раз в несколько секунд по панели пробегает световой блик, а иконка слегка «дышит».
// Вешается и настраивается автоматически (Tools > BuilderSim > Build HUD).
using UnityEngine;

public class HudShine : MonoBehaviour
{
    public RectTransform shine;          // полоска блика (ребёнок маскированной панели)
    public float period = 5.5f;          // раз в сколько секунд
    public float delay;                  // сдвиг, чтобы панели блестели не одновременно
    [Range(0.05f, 0.6f)] public float sweepFraction = 0.25f;   // доля периода, когда блик едет

    public Transform breathe;            // что «дышит» (обычно иконка)
    public float breatheAmount = 0.05f;
    public float breatheSpeed = 1.6f;

    Vector3 breatheBase = Vector3.one;

    void Start()
    {
        if (breathe != null) breatheBase = breathe.localScale;
        if (shine != null) shine.gameObject.SetActive(false);
    }

    void Update()
    {
        float now = Time.unscaledTime;

        if (shine != null)
        {
            float t = Mathf.Repeat(now + delay, Mathf.Max(0.5f, period)) / Mathf.Max(0.5f, period);
            if (t < sweepFraction)
            {
                float k = t / sweepFraction;
                k = k * k * (3f - 2f * k);
                var parent = shine.parent as RectTransform;
                float w = parent != null ? parent.rect.width : 300f;
                float sw = Mathf.Max(1f, shine.rect.width);
                if (!shine.gameObject.activeSelf) shine.gameObject.SetActive(true);
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-sw, w + sw, k), 0f);
            }
            else if (shine.gameObject.activeSelf)
            {
                shine.gameObject.SetActive(false);
            }
        }

        if (breathe != null)
            breathe.localScale = breatheBase * (1f + Mathf.Sin(now * breatheSpeed + delay) * breatheAmount);
    }
}