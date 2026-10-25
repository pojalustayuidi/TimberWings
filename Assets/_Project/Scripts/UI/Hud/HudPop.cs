// Положи в Assets/_Project/Scripts/HudPop.cs (НЕ в папку Editor)
// Вешается автоматически билдером HUD: когда текст меняется, цифра "подпрыгивает".
// Pop() можно вызвать и вручную, например когда в счётчик прилетело бревно.
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class HudPop : MonoBehaviour
{
    public float scale = 1.25f;
    public float time = 0.25f;

    TMP_Text label;
    string last;
    float timer = -1f;
    Vector3 baseScale = Vector3.one;
    bool inited;

    void Awake()
    {
        label = GetComponent<TMP_Text>();
        baseScale = transform.localScale;
    }

    void OnDisable()
    {
        // если выключили посреди анимации, не оставляем цифру увеличенной
        if (timer >= 0f) transform.localScale = baseScale;
        timer = -1f;
        inited = false;
    }

    public void Pop()
    {
        if (timer < 0f) baseScale = transform.localScale; // запоминаем нормальный размер, только когда не в анимации
        timer = 0f;
    }

    void LateUpdate()
    {
        // первый кадр: UIManager мог уже записать стартовое значение, это не "изменение"
        if (!inited)
        {
            inited = true;
            last = label.text;
            return;
        }

        if (label.text != last)
        {
            last = label.text;
            Pop();
        }
        if (timer < 0f) return;

        float dur = Mathf.Max(0.01f, time);
        timer += Time.unscaledDeltaTime;
        float k = timer / dur;
        if (k >= 1f)
        {
            transform.localScale = baseScale;
            timer = -1f;
        }
        else
        {
            transform.localScale = baseScale * (1f + (scale - 1f) * Mathf.Sin(k * Mathf.PI));
        }
    }
}