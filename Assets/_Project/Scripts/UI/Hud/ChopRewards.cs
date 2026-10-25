// Assets/_Project/Scripts/UI/Hud/ChopRewards.cs
// Награда за срубленное дерево:
//   - из дерева вылетают N брёвен, подскакивают и по дуге летят в панель дерева в HUD
//   - каждое бревно при приземлении: панель подпрыгивает и вызывается onLogLanded
//     (ТУТ прибавляй бревно в инвентарь: счётчик растёт синхронно с прилётом, UIManager покажет «+N бревна»)
//   - через xpDelay секунд вызывается onXp (ТУТ начисляй XP, UIManager покажет «+N XP» под уровнем)
//   Сами плашки рисует UIManager через HudGain, этот скрипт только летающие брёвна и колбэки.
//   Вид Candy: бревно Candy-иконкой, при прилёте искры CandyFx на панели дерева, при XP мятные искры на панели опыта.
//
// Вызов из кода рубки:
//   ChopRewards.Give(tree.transform.position, 2, 2, () => inventory.AddWood(1), () => player.AddXp(2));
//
// Защита: если HUD не размечен или нет камеры — колбэки вызываются сразу, ничего не теряется.
// Если объект/канвас уничтожили посреди полёта — колбэк всё равно вызывается ровно один раз.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ChopRewards : MonoBehaviour
{
    [Header("Полёт брёвен")]
    public float popTime = 0.22f;       // выскок из дерева
    public float flyMin = 0.55f;        // время полёта в HUD
    public float flyMax = 0.75f;
    public float stagger = 0.11f;       // задержка между брёвнами
    public float iconSize = 46f;        // размер летящего бревна (единицы канваса)
    public float spawnHeight = 1.2f;    // на какой высоте над позицией дерева стартуют брёвна, м

    [Header("XP")]
    public float xpDelay = 0.25f;

    static ChopRewards inst;
    static bool quitting;

    static ChopRewards Instance
    {
        get
        {
            if (inst == null && !quitting)
            {
                inst = FindFirstObjectByType<ChopRewards>();
                if (inst == null)
                {
                    var go = new GameObject("ChopRewards");
                    DontDestroyOnLoad(go);
                    inst = go.AddComponent<ChopRewards>();
                }
            }
            return inst;
        }
    }

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(this); return; }
        inst = this;
    }

    void OnApplicationQuit() { quitting = true; }
    void OnDestroy() { if (inst == this) inst = null; }

    // ---------------------------------------------------------------- API
    public static void Give(Vector3 worldPos, int logs = 2, int xp = 2, Action onLogLanded = null, Action onXp = null)
    {
        var s = Instance;
        if (s == null)
        {
            for (int i = 0; i < logs; i++) Safe(onLogLanded);
            if (xp > 0) Safe(onXp);
            return;
        }
        s.Run(worldPos, logs, xp, onLogLanded, onXp);
    }

    static void Safe(Action a)
    {
        if (a == null) return;
        try { a(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    // ---------------------------------------------------------------- логика
    void Run(Vector3 worldPos, int logs, int xp, Action onLog, Action onXp)
    {
        var target = HudGainAnchor.Find(HudGain.Kind.Wood);
        Canvas canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        if (logs > 0)
        {
            if (target == null || canvas == null || cam == null)
            {
                if (target == null) Debug.LogWarning("[ChopRewards] Панель бревен не размечена (HudGainAnchor, Kind = Wood). Начисляю без анимации.");
                for (int i = 0; i < logs; i++) Safe(onLog);
            }
            else
            {
                canvas = canvas.rootCanvas;
                for (int i = 0; i < logs; i++)
                    StartCoroutine(FlyLog(canvas, cam, worldPos, target, i * stagger, onLog));
            }
        }

        if (xp > 0) StartCoroutine(XpRoutine(xp, onXp));
    }

    IEnumerator XpRoutine(int xp, Action onXp)
    {
        bool fired = false;
        try
        {
            if (xpDelay > 0f) yield return new WaitForSecondsRealtime(xpDelay);
            fired = true;
            Safe(onXp);   // тут игра начисляет XP -> UIManager сам покажет плашку «+N XP»
            var a = HudGainAnchor.Find(HudGain.Kind.Xp);
            if (a != null)
            {
                a.Punch();
                if (!VibeSettings.ReduceEffects) CandyFx.Burst(UiCandy.Kit, a.Rect, UiCandy.Mint, 5, 200f);
            }
        }
        finally
        {
            if (!fired) Safe(onXp);   // нас остановили раньше времени — XP не теряем
        }
    }

    static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    IEnumerator FlyLog(Canvas canvas, Camera cam, Vector3 worldPos, HudGainAnchor target, float delay, Action onLog)
    {
        GameObject go = null;
        bool landed = false;
        try
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (target == null || canvas == null) yield break;

            var crt = (RectTransform)canvas.transform;
            Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            // старт: экранная точка над деревом -> мировая точка канваса
            Vector3 sp = cam.WorldToScreenPoint(worldPos + Vector3.up * spawnHeight);
            if (sp.z < 0f) sp = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Vector3 p0;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(crt, sp, uiCam, out p0);

            go = new GameObject("FlyLog", typeof(RectTransform), typeof(Image));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(crt, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = Vector2.one * iconSize;
            rt.position = p0;
            rt.localScale = Vector3.zero;
            var img = go.GetComponent<Image>();
            img.sprite = target.flyIcon != null ? target.flyIcon : UiKit.CandyIcon(UiIcon.Log);
            img.preserveAspect = true;
            img.raycastTarget = false;

            float k = Mathf.Max(0.0001f, crt.lossyScale.x);
            float spin = UnityEngine.Random.Range(-540f, 540f);

            // 1) выскок из дерева
            Vector3 burst = p0 + (crt.up * UnityEngine.Random.Range(40f, 80f) + crt.right * UnityEngine.Random.Range(-60f, 60f)) * k;
            float t = 0f;
            while (t < popTime)
            {
                if (rt == null || target == null) yield break;
                float dt = Time.unscaledDeltaTime;
                t += dt;
                float a = Mathf.Clamp01(t / popTime);
                rt.position = Vector3.Lerp(p0, burst, UiDraw.EaseOutCubic(a));
                rt.localScale = Vector3.one * Mathf.Max(0f, UiDraw.EaseOutBack(a));
                rt.Rotate(0f, 0f, spin * dt);
                yield return null;
            }

            // 2) полёт по дуге в панель дерева
            float dur = UnityEngine.Random.Range(flyMin, flyMax);
            Vector3 side = crt.right * UnityEngine.Random.Range(-0.18f, 0.18f);
            t = 0f;
            while (t < dur)
            {
                if (rt == null || target == null) yield break;
                float dt = Time.unscaledDeltaTime;
                t += dt;
                float u = Mathf.Clamp01(t / dur);
                Vector3 tp = target.FlyPoint;
                float dist = (tp - burst).magnitude;
                Vector3 ctrl = (burst + tp) * 0.5f + crt.up * dist * 0.22f + side * dist;
                float e = Mathf.Pow(u, 1.7f);
                rt.position = Bez(burst, ctrl, tp, e);
                rt.localScale = Vector3.one * (Mathf.Lerp(1f, 0.65f, e) * (1f + 0.2f * Mathf.Sin(Mathf.PI * u)));
                rt.Rotate(0f, 0f, spin * dt);
                yield return null;
            }

            // 3) приземление: счётчик, плашка, подпрыгивание панели
            landed = true;
            Safe(onLog);   // тут игра добавляет бревно -> UIManager сам покажет «+N бревна»
            if (target != null)
            {
                target.Punch();
                if (!VibeSettings.ReduceEffects) CandyFx.Burst(UiCandy.Kit, target.Rect, UiCandy.Gold, 5, 200f);
            }
        }
        finally
        {
            if (go != null) Destroy(go);
            if (!landed) Safe(onLog);   // прервали (канвас/сцена исчезли) — бревно всё равно засчитываем
        }
    }
}