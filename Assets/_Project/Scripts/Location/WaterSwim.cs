using UnityEngine;

// Assets/_Project/Scripts/Location/WaterSwim.cs (НЕ в Editor)
// Плавание и утопление в реке. Висит на игроке, добавляется сам (PlayerMovement.Start). Без объёма воды (WaterVolume) ничего не делает.
//
// Как это работает (глубину отдаёт WaterVolume.TrySample: submersion = на сколько метров поверхность выше ног):
//   мелко   (< wadeDepth)   : идём как обычно
//   брод    (wadeDepth..swimDepth): идём медленнее (wadeSpeedMul)
//   глубоко (>= swimDepth)  : герой плывёт. Гравитация выключена, плавучесть держит его у поверхности,
//                             скорость x swimSpeedMul, течение сносит вниз по реке (flowPush).
//                             Дольше drownSeconds в глубокой воде = утонул: экран «Вы утонули!» и возврат на берег.
//   Короткий нырок не страшен: таймер медленно остывает, когда герой выбрался на мелководье.
//
// Зачем так: невидимых стенок над рекой больше нет, поэтому реку нельзя «просто пройти» (плыть через неё дольше, чем можно продержаться),
// а мост остаётся единственным нормальным переходом. Ширина реки 8-10 u (4-5 м), swimSpeedMul 0.4 даёт около 1.7 м/с: 2.5-3 с на переправу при лимите 2 с.
//
// Возврат: если на объекте острова есть IslandEdgeGuard, используется его экран возвращения (с надписью про утопление),
// иначе (тестовый остров без FloatingIsland) игрок возвращается на последнее место на суше рядом с рекой.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]   // после PlayerMovement: течение добавляется поверх его скорости
public class WaterSwim : MonoBehaviour
{
    // читает PlayerMovement.CurrentSpeed: 1 = на суше, меньше = брод или плавание
    public static float SpeedMul = 1f;
    public static bool Swimming { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { SpeedMul = 1f; Swimming = false; }

    [Header("Глубина, м (ноги ниже поверхности)")]
    [Tooltip("Глубже этого герой плывёт")]
    public float swimDepth = 1.0f;
    [Tooltip("Глубже этого герой бредёт по мелководью")]
    public float wadeDepth = 0.3f;

    [Header("Скорость")]
    public float wadeSpeedMul = 0.8f;
    [Tooltip("Скорость плавания относительно обычной")]
    public float swimSpeedMul = 0.4f;
    [Tooltip("Течение сносит пловца, м/с (должно быть меньше 1: иначе разгон PlayerMovement его гасит)")]
    public float flowPush = 0.9f;

    [Header("Утопление")]
    public bool canDrown = true;
    [Tooltip("Сколько секунд можно продержаться в глубокой воде")]
    public float drownSeconds = 2.0f;
    [Tooltip("Насколько ноги ниже поверхности, пока плывём (голова над водой), м")]
    public float holdDepth = 1.1f;

    Rigidbody rb;
    PlayerMovement player;
    bool swimming, drowning, warned, prevGravity;
    float swimTime, drownAt, nextDrySample;
    Vector3 lastDry;
    bool hasDry;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        player = GetComponent<PlayerMovement>();
    }

    void OnDisable() { Leave(); }

    void OnDestroy() { Leave(); }

    // вышли из режима плавания: вернуть гравитацию и скорость
    void Leave()
    {
        if (swimming && rb != null) rb.useGravity = prevGravity;
        swimming = false;
        Swimming = false;
        SpeedMul = 1f;
    }

    void FixedUpdate()
    {
        if (rb == null || rb.isKinematic) return;
        float dt = Time.fixedDeltaTime;

        // идёт возвращение после утопления: ждём конец экрана, ничего не трогаем
        if (drowning)
        {
            var g = IslandEdgeGuard.Instance;
            if (Time.time - drownAt > 0.3f && (g == null || !g.IsFalling)) drowning = false;
            return;
        }
        // падение с острова и другие блокировки управления: не вмешиваемся
        if (player != null && player.ControlLocked) { Leave(); return; }

        Vector3 pos = rb.position;
        WaterVolume.Hit h;
        bool inWater = WaterVolume.TrySample(pos, out h);
        float sub = inWater ? h.submersion : 0f;

        // последнее сухое место: сюда вернёмся, если IslandEdgeGuard нет
        if ((!inWater || sub < 0.05f) && Time.time >= nextDrySample)
        {
            nextDrySample = Time.time + 0.3f;
            if (rb.linearVelocity.y > -2f) { lastDry = pos; hasDry = true; }
        }

        // гистерезис: зашли глубже swimDepth, а выходим только когда стало заметно мельче
        bool deep = inWater && sub >= (swimming ? swimDepth - 0.2f : swimDepth);
        bool wade = inWater && !deep && sub >= wadeDepth;

        if (!deep)
        {
            if (swimming) Leave();
            SpeedMul = wade ? wadeSpeedMul : 1f;
            swimTime = Mathf.Max(0f, swimTime - dt * 1.5f);   // выбрался: воздух возвращается
            if (swimTime < 0.2f) warned = false;
            return;
        }

        // ---- плывём
        if (!swimming)
        {
            swimming = true;
            Swimming = true;
            prevGravity = rb.useGravity;
            rb.useGravity = false;
        }
        SpeedMul = swimSpeedMul;
        swimTime += dt;

        Vector3 v = rb.linearVelocity;
        float targetY = h.surfaceY - holdDepth;                       // ноги на этой высоте, голова над водой
        float wantVy = Mathf.Clamp((targetY - pos.y) * 4f, -1.5f, 2.5f);
        v.y = Mathf.MoveTowards(v.y, wantVy, 25f * dt);
        v.x += h.flow.x * flowPush;                                    // течение (PlayerMovement на следующем шаге снимет прошлую добавку)
        v.z += h.flow.z * flowPush;
        rb.linearVelocity = v;

        if (canDrown)
        {
            if (!warned && swimTime >= drownSeconds * 0.45f)
            {
                warned = true;
                SaveIndicator.ShowToast("Вы тонете! Скорее к берегу");
            }
            if (swimTime >= drownSeconds) Drown();
        }
    }

    void Drown()
    {
        drowning = true;
        drownAt = Time.time;
        swimTime = 0f;
        warned = false;
        Leave();   // гравитация обратно: пока идёт экран, герой тонет

        var g = IslandEdgeGuard.Instance;
        if (g != null && g.ForceRespawn("УТОПЛЕНИЕ", "Вы утонули!", "Река оказалась глубже, чем казалась", "Мост цел: по нему можно перейти сухим"))
            return;

        // запасной путь: на последнее сухое место
        Vector3 to = hasDry ? lastDry : transform.position + Vector3.up * 3f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = to + Vector3.up * 0.1f;
        transform.position = to + Vector3.up * 0.1f;
        Physics.SyncTransforms();
        CameraFollow.SnapToTarget();
        SaveIndicator.ShowToast("Вы утонули! Возвращаемся на берег");
        drowning = false;
    }
}
