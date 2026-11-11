// Assets/_Project/Scripts/Camera/CameraVibe.cs (НЕ в Editor)
// Кинематографичные мелочи поверх CameraFollow. Вешается на камеру рядом с CameraFollow (меню Tools > BuilderSim > Camera Vibe делает это само).
//  - Разговор: камера мягко наезжает и смещается к говорящему, пока идёт диалог.
//  - Притяжение к персонажам: рядом с NPC кадр чуть сдвигается к нему, и они не торчат на краю экрана.
//  - Рубка и награды: короткий «удар» зума и лёгкая тряска при срубленном дереве и новом уровне.
// Работает через поля CameraFollow: focusBias и zoomBias. Базовый yaw не трогает, управление не ломается.
//  - Показ стадии здания (BuildReveal): камера наезжает на здание (zoomBias GameBalance.RevealZoom) и обходит его по yawBias (SetReveal / ClearReveal).
using UnityEngine;

[DefaultExecutionOrder(-102)]
[RequireComponent(typeof(CameraFollow))]
public class CameraVibe : MonoBehaviour
{
    [Header("Разговор")]
    public bool dialogueFocus = true;
    public float dialogueZoom = 0.88f;           // < 1 = ближе
    [Range(0f, 0.8f)] public float dialogueFocusShare = 0.4f;   // какая доля пути от игрока к говорящему
    public float dialogueMaxDist = 12f;          // дальше говорящего не ищем

    [Header("Притяжение к персонажам")]
    public bool npcPull = true;
    public float npcPullRadius = 4.5f;
    [Range(0f, 0.6f)] public float npcPullShare = 0.22f;

    [Header("Рубка и награды")]
    public bool chopFeel = true;
    public float chopShake = 0.07f;
    public float chopPunch = 0.03f;              // доля зума, на которую «щёлкает» камера
    public float levelUpShake = 0.1f;            // тряска уровня: теперь множится на levelUpShakeShare
    public float levelUpPunch = 0.07f;           // размах праздничного акцента: на эту долю дистанция «вдыхает» ближе и возвращается

    [Header("Camera Polish: акцент уровня")]
    [Tooltip("Один короткий мягкий наезд-вдох при новом уровне вместо удара с тряской. Плашка и звук идут отдельно")]
    public bool levelUpAccent = true;
    [Tooltip("Длительность акцента, с")]
    [Range(.2f, 1.2f)] public float levelUpAccentTime = .55f;
    [Tooltip("Доля старой тряски levelUpShake, добавляемая к акценту (0 = только наезд)")]
    [Range(0f, 1f)] public float levelUpShakeShare = 0f;
    [Tooltip("Во время рубки, диалога и показа стройки акцент ждёт конца не дольше этого времени, потом пропускается, с")]
    [Range(0f, 3f)] public float levelUpMaxWait = 1.5f;

    [Header("Плавность")]
    public float focusSmooth = 0.45f;
    public float zoomSmooth = 0.5f;
    public float punchRelax = 0.2f;              // за сколько секунд «удар» затухает

    CameraFollow cf;
    Speaker[] speakers = new Speaker[0];
    float refreshT;
    float dialogueShown, dialogueShownVelocity;
    Transform dialoguePartner;
    public static float DialogueShown { get { return inst != null ? inst.dialogueShown : 0f; } }
    public static Transform DialoguePartner { get { return inst != null ? inst.dialoguePartner : null; } }

    Vector3 bias, biasVel;
    float zoomVal = 1f, zoomVel;
    float punch, punchVel;

    // идёт ли «загляни туда» или праздничный акцент: другие необязательные движения камеры в это время молчат
    public static bool PeekActive { get { return inst != null && inst.peekT > 0f; } }
    public static bool AccentActive { get { return inst != null && inst.accentT >= 0f; } }

    float accentT = -1f;          // -1 = не идёт, 0..1 = прогресс
    float accentWait;             // сколько ещё ждать подходящего момента, с
    PlayerInteraction interaction;

    // «Загляни туда»: камера на пару секунд сдвигается к точке и возвращается. Зовёт QuestGuide, когда цель далеко за кадром
    static CameraVibe inst;
    Vector3 peekPoint;
    float peekT;

    public static void Peek(Vector3 world, float seconds = 2.2f)
    {
        if (inst == null || inst.cf == null || DialogueManager.IsBusy || revealOn) return;
        inst.peekPoint = world;
        inst.peekT = Mathf.Max(0.5f, seconds);
    }

    // Показ стадии здания: BuildReveal каждый кадр зовёт SetReveal (amount 0..1 уже сглажен: наезд в начале и возврат в конце),
    // а когда показ кончился или выключен, ClearReveal. Без вызова SetReveal в кадре камера сама плавно возвращается к обычному виду.
    static bool revealOn;
    static Vector3 revealPoint;
    static float revealAmount, revealYaw, revealRadius;
    float revealShown, revealShownVel;      // сглаженная доля показа (только для плавного ухода после ClearReveal)

    // point: куда смотрит камера; amount: 0 = обычный вид, 1 = полный наезд; yawDegrees: облёт в градусах (уже умножен на amount)
    // radius: размер здания в метрах (0 = неизвестно). Нужен свободной камере (OrbitCamera), чтобы уместить здание в кадр
    public static void SetReveal(Vector3 point, float amount, float yawDegrees, float radius = 0f)
    {
        revealOn = true;
        revealRadius = radius;
        revealPoint = point;
        revealAmount = Mathf.Clamp01(amount);
        revealYaw = yawDegrees;
    }

    public static void ClearReveal() { revealOn = false; }

    // для OrbitCamera: сглаженная доля показа 0..1 (держится и пока камера возвращается после показа), точка и размер здания
    public static float RevealShown { get { return inst != null ? inst.revealShown : 0f; } }
    public static bool RevealActive { get { return revealOn; } }
    public static Vector3 RevealPoint { get { return revealPoint; } }
    public static float RevealRadius { get { return revealRadius; } }

    void Awake() { cf = GetComponent<CameraFollow>(); inst = this; }

    void OnDestroy() { if (inst == this) inst = null; revealOn = false; revealRadius = 0f; }

    void OnEnable()
    {
        GameEvents.TreeChopped += OnChop;
        GameEvents.LevelUp += OnLevelUp;
    }

    void OnDisable()
    {
        dialogueShown = 0f; dialogueShownVelocity = 0f; dialoguePartner = null;
        GameEvents.TreeChopped -= OnChop;
        GameEvents.LevelUp -= OnLevelUp;
        if (cf != null) { cf.focusBias = Vector3.zero; cf.zoomBias = 1f; cf.yawBias = 0f; }
    }

    void OnChop()
    {
        if (!chopFeel) return;
        CameraFollow.Shake(chopShake, 0.14f);                                      // OrbitCamera уже ослабляет тряску в режиме эффектов и с прицелом
        if (!VibeSettings.ReduceEffects) punch = Mathf.Max(punch, chopPunch);      // зум-«щелчок» вдоль взгляда не уводит цель из-под прицела
    }

    void OnLevelUp(int level)
    {
        Transform target = cf != null ? cf.target : null;
        if (target == null && PlayerMovement.Instance != null) target = PlayerMovement.Instance.transform;
        if (target != null && LeoCelebration.ForTarget(target) != null) return;
        if (!chopFeel || VibeSettings.ReduceEffects) return;   // «Уменьшить эффекты»: остаётся только плашка и звук
        if (!levelUpAccent)
        {
            CameraFollow.Shake(levelUpShake, 0.3f);
            punch = Mathf.Max(punch, levelUpPunch);
            return;
        }
        accentWait = levelUpMaxWait;   // акцент проиграется, когда рубка, диалог и показ стройки закончатся
    }

    // можно ли сейчас показывать акцент: при рубке, диалоге и показе стройки хватает плашки и звука
    bool AccentAllowed()
    {
        if (DialoguePreferences.ConversationOwnsCamera || DialogueManager.IsBusy || BuildingController.AnyRevealing || revealOn || revealShown > 0.01f) return false;
        if (interaction == null && cf != null && cf.target != null) interaction = cf.target.GetComponent<PlayerInteraction>();
        return interaction == null || !interaction.IsChopping;
    }

    Speaker Nearest(Vector3 pos, float maxDist, Transform self)
    {
        Speaker best = null;
        float bd = maxDist;
        for (int i = 0; i < speakers.Length; i++)
        {
            var s = speakers[i];
            if (s == null || !s.isActiveAndEnabled) continue;
            if (s.transform == self || s.transform.IsChildOf(self) || self.IsChildOf(s.transform)) continue;   // сам игрок
            Vector3 d = s.transform.position - pos; d.y = 0f;
            float m = d.magnitude;
            if (m < bd) { bd = m; best = s; }
        }
        return best;
    }

    void LateUpdate()
    {
        if (cf == null) return;
        Transform t = cf.target;
        if (t == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt < 0.0001f) return;

        refreshT -= dt;
        if (refreshT <= 0f)
        {
            refreshT = 2f;
            speakers = FindObjectsByType<Speaker>(FindObjectsSortMode.None);
        }

        Vector3 pos = t.position;
        Vector3 wantBias = Vector3.zero;
        float wantZoom = 1f;

        // A whole sequence owns one partner, including the pauses between its lines.
        bool busy = dialogueFocus && DialoguePreferences.ConversationOwnsCamera;
        if (busy) dialoguePartner = DialogueSequence.Focus;
        dialogueShown = Mathf.SmoothDamp(dialogueShown, busy ? 1f : 0f, ref dialogueShownVelocity, .4f, Mathf.Infinity, dt);
        if (!busy && dialogueShown < .001f) dialoguePartner = null;
        if (busy)
        {
            if (dialoguePartner != null)
            {
                Vector3 d = dialoguePartner.position - pos; d.y = 0f;
                wantBias = Vector3.ClampMagnitude(d, dialogueMaxDist) * dialogueFocusShare;
                wantZoom = VibeSettings.ReduceEffects ? 1f : dialogueZoom;
            }
        }
        else
        {
            if (npcPull)
            {
                Speaker s = Nearest(pos, npcPullRadius, t);
                if (s != null)
                {
                    Vector3 d = s.transform.position - pos; d.y = 0f;
                    float k = 1f - Mathf.Clamp01(d.magnitude / Mathf.Max(0.1f, npcPullRadius));
                    k = k * k * (3f - 2f * k);
                    wantBias = d * (npcPullShare * k);
                }
            }
        }
        // взгляд к цели: плавность даёт SmoothDamp ниже, вход и выход автоматические
        if (peekT > 0f)
        {
            peekT -= dt;
            if (busy || revealOn) peekT = 0f;             // реплика и показ здания важнее
            else
            {
                Vector3 pd = peekPoint - pos; pd.y = 0f;
                wantBias = Vector3.ClampMagnitude(pd * 0.55f, 13f);
                wantZoom = Mathf.Max(wantZoom, 1.1f);     // чуть шире, чтобы цель попала в кадр
            }
        }

        // праздничный акцент уровня: ждёт подходящего момента, играется один раз, мягко туда и обратно
        if (accentT < 0f && accentWait > 0f)
        {
            accentWait -= dt;
            if (VibeSettings.ReduceEffects) accentWait = 0f;
            else if (AccentAllowed())
            {
                accentWait = 0f;
                accentT = 0f;
                if (levelUpShakeShare > 0f) CameraFollow.Shake(levelUpShake * levelUpShakeShare, 0.2f);
            }
        }
        float accent = 0f;
        if (accentT >= 0f)
        {
            if (revealOn || DialoguePreferences.ConversationOwnsCamera || VibeSettings.ReduceEffects) accentT = -1f;   // началось что-то важнее: акцент не доигрываем
            else
            {
                accentT += dt / Mathf.Max(0.2f, levelUpAccentTime);
                if (accentT >= 1f) accentT = -1f;
                else accent = levelUpPunch * 0.5f * (1f - Mathf.Cos(accentT * 2f * Mathf.PI));
            }
        }

        bias = Vector3.SmoothDamp(bias, wantBias, ref biasVel, Mathf.Max(0.05f, focusSmooth), Mathf.Infinity, dt);
        zoomVal = Mathf.SmoothDamp(zoomVal, wantZoom, ref zoomVel, Mathf.Max(0.05f, zoomSmooth), Mathf.Infinity, dt);
        punch = Mathf.SmoothDamp(punch, 0f, ref punchVel, Mathf.Max(0.03f, punchRelax), Mathf.Infinity, dt);

        float zoomNow = zoomVal * (1f - punch - accent);

        // показ стадии: поверх обычного вида. Пока BuildReveal зовёт SetReveal, берём его долю как есть (она уже плавная),
        // после ClearReveal доля мягко уходит в ноль сама, чтобы камера не дёрнулась
        if (revealOn) { revealShown = revealAmount; revealShownVel = 0f; }
        else if (revealShown > 0.0005f) revealShown = Mathf.SmoothDamp(revealShown, 0f, ref revealShownVel, 0.3f, Mathf.Infinity, dt);
        else revealShown = 0f;

        Vector3 biasOut = bias;     // bias сглаживается между кадрами, показ кладём поверх в отдельную переменную
        if (revealShown > 0f)
        {
            // фокус: к центру здания (CameraFollow сам прибавляет lookOffset, поэтому его вычитаем)
            Vector3 toPoint = revealPoint - pos - cf.lookOffset;
            biasOut = Vector3.Lerp(bias, toPoint, revealShown);
            zoomNow = Mathf.Lerp(zoomNow, GameBalance.RevealZoom, revealShown);
            cf.yawBias = revealOn ? revealYaw : cf.yawBias * (1f - Mathf.Clamp01(dt / 0.3f));
        }
        else cf.yawBias = 0f;

        cf.focusBias = biasOut;
        cf.zoomBias = zoomNow;
    }
}
