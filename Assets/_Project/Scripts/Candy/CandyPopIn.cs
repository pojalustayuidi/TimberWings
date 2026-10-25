// Assets/_Project/Scripts/UI/Candy/CandyPopIn.cs (НЕ в Editor)
// Окно, плашка или тост «выскакивает» при включении: растёт с лёгким перелётом и проявляется.
// Режим staggerChildren: вместо самого объекта по очереди выскакивают его прямые дети (строки настроек, слоты инвентаря).
// Replay() запускает заново. Время unscaled: работает на паузе.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CandyPopIn : MonoBehaviour
{
    public float delay = 0f;
    [Range(0f, 1f)] public float fromScale = 0.7f;
    public float stiffness = 240f;
    public float damping = 13f;
    public bool fade = true;
    public float fadeTime = 0.15f;

    [Header("Дети по очереди")]
    public bool staggerChildren = false;
    public float staggerStep = 0.06f;

    CandySpring self;
    Vector3 selfBase = Vector3.one;
    CanvasGroup group;
    float t;
    bool playing;

    readonly List<RectTransform> kids = new List<RectTransform>();
    readonly List<Vector3> kidBase = new List<Vector3>();
    CandySpring[] kidSpring;

    void OnEnable() { Replay(); }

    public void Replay()
    {
        t = -delay;
        bool wasPlaying = playing;
        playing = true;

        if (staggerChildren)
        {
            kids.Clear();
            kidBase.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i) as RectTransform;
                if (c == null || !c.gameObject.activeSelf) continue;
                kids.Add(c);
                kidBase.Add(c.localScale);
                c.localScale = Vector3.zero;
            }
            kidSpring = new CandySpring[kids.Count];
            for (int i = 0; i < kidSpring.Length; i++) kidSpring[i] = new CandySpring(0f);
        }
        else
        {
            if (!wasPlaying) selfBase = transform.localScale;   // при повторном запуске посреди анимации база уже запомнена
            self = new CandySpring(fromScale);
            transform.localScale = selfBase * fromScale;
            if (fade)
            {
                if (group == null) group = GetComponent<CanvasGroup>();
                if (group == null) group = gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
            }
        }
    }

    void Update()
    {
        if (!playing) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        t += dt;
        if (t < 0f) return;

        if (staggerChildren)
        {
            bool allDone = true;
            for (int i = 0; i < kids.Count; i++)
            {
                if (kids[i] == null) continue;
                float ct = t - i * staggerStep;
                if (ct < 0f) { allDone = false; continue; }
                kidSpring[i].Step(1f, stiffness, damping, dt);
                kids[i].localScale = kidBase[i] * kidSpring[i].x;
                if (!kidSpring[i].Settled(1f)) allDone = false;
            }
            if (allDone)
            {
                for (int i = 0; i < kids.Count; i++) if (kids[i] != null) kids[i].localScale = kidBase[i];
                playing = false;
            }
            return;
        }

        self.Step(1f, stiffness, damping, dt);
        transform.localScale = selfBase * self.x;
        if (fade && group != null) group.alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeTime));

        if (t > fadeTime && self.Settled(1f))
        {
            transform.localScale = selfBase;
            if (fade && group != null) group.alpha = 1f;
            playing = false;
        }
    }

    void OnDisable()
    {
        // закрыли окно посреди анимации: вернуть нормальный размер, чтобы в следующий раз не остаться «сжатым»
        if (playing)
        {
            if (staggerChildren) { for (int i = 0; i < kids.Count; i++) if (kids[i] != null) kids[i].localScale = kidBase[i]; }
            else { transform.localScale = selfBase; if (group != null) group.alpha = 1f; }
            playing = false;
        }
    }
}
