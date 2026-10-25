// Assets/_Project/Scripts/UI/Kit/UiCandyKnob.cs   (НЕ в Editor)
// Ручка переключателя UiCandy.Toggle: плавно «пружинит» к нужной X-позиции внутри дорожки. Больше ничего не делает.
// Время unscaled: работает на паузе (окна игры часто открываются при Time.timeScale = 0).
using UnityEngine;

[DisallowMultipleComponent]
public class UiCandyKnob : MonoBehaviour
{
    public float stiffness = 380f;
    public float damping = 15f;

    RectTransform rt;
    CandySpring x;
    float target;
    bool inited;

    void Init()
    {
        if (inited) return;
        inited = true;
        rt = (RectTransform)transform;
        x = new CandySpring(rt.anchoredPosition.x);
        target = x.x;
    }

    void OnEnable() { Init(); x = new CandySpring(target); Apply(); }

    // instant: без анимации (первый показ или начальное значение)
    public void SetTarget(float posX, bool instant)
    {
        Init();
        target = posX;
        if (instant || !isActiveAndEnabled) { x = new CandySpring(posX); Apply(); }
    }

    void Update()
    {
        if (x.Settled(target)) return;
        x.Step(target, stiffness, damping, Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        Apply();
    }

    void Apply()
    {
        var p = rt.anchoredPosition;
        p.x = x.x;
        rt.anchoredPosition = p;
    }

    void OnDisable()
    {
        if (!inited) return;
        x = new CandySpring(target);
        Apply();
    }
}
