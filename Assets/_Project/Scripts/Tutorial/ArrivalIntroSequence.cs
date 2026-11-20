// Assets/_Project/Scripts/Tutorial/ArrivalIntroSequence.cs
// Сразу после ВИДЕО новой игры — короткая игровая сцена прибытия.
// Точка появления: необязательный Scene GameObject с именем "IntroArrivalSpawn".
// Без точки не перемещаем Лео: безопасный fallback к текущему старту.
// Подключается к событию SaveSystem.NewGameStarted. Главное меню, сохранения и сюжет не заменяем.
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(500)]
public sealed class ArrivalIntroSequence : MonoBehaviour
{
    public const string SpawnMarkerName = "IntroArrivalSpawn";
    const string VoiceResource = "Intro/Leo_Intro_Arrival";
    const float VoiceAt = 0.85f;
    const float FallbackVoiceLength = 3.52f;
    const float AfterVoicePause = 0.50f;
    const float ReturnDuration = 1.35f;

    static ArrivalIntroSequence instance;
    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; Active = false; }

    // Вызывается напрямую из MenuFlow после SaveSystem.NewGame(), пока ещё не был Start.
    // НЕ вызывается при Continue и обычном запуске сцены из редактора.
    public static void BeginForNewGame()
    {
        if (SceneManager.GetActiveScene().buildIndex != MenuFlow.GameScene || instance != null) return;
        Active = true; // Сигнал до Update IntroFlow/HudIntro.
        var go = new GameObject("_LeoArrivalAfterIntroVideo");
        instance = go.AddComponent<ArrivalIntroSequence>();
        instance.PrepareBeforeFirstFrame();
    }

    PlayerMovement leo;
    Camera gameCamera;
    AudioSource voiceSource;
    AudioClip voiceClip;
    CanvasGroup subtitleGroup;
    TextMeshProUGUI subtitle;
    Transform anchor;
    bool wasControlLocked;
    bool lockedControl;
    bool shotActive;
    Rigidbody body;
    float sceneTime;
    float returnAt;
    float completeAt;
    float sentenceEnds;
    Vector3 scenicBack, scenicRight, scenicFocus;
    readonly RaycastHit[] sightHits = new RaycastHit[16];

    // Вызов до Start остальных систем (см. MenuFlow.OnSceneLoaded).
    void PrepareBeforeFirstFrame()
    {
        leo = PlayerMovement.Instance;
        if (leo == null) return;
        wasControlLocked = leo.ControlLocked;
        leo.ControlLocked = true;
        lockedControl = true;
        if (leo.AutoWalking) leo.CancelWalk();
        body = leo.GetComponent<Rigidbody>();
        StopBodyMotion();

        // Старая стартовая реплика «Дядя? ...Никого. Ладно, осмотрюсь»
        // уже озвучена заново новым WAV. Не даём OnStart запускаться параллельно.
        var oldLine = leo.GetComponent<DialogueSequence>();
        if (oldLine != null && oldLine.trigger == DialogueSequence.Trigger.OnStart && oldLine.lines != null)
        {
            foreach (var line in oldLine.lines)
            {
                if (line == null || string.IsNullOrEmpty(line.text)) continue;
                if (line.text.IndexOf("осмотрюсь", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.text.IndexOf("никого", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    oldLine.SetTrigger(DialogueSequence.Trigger.Manual);
                    Debug.Log("ArrivalIntroSequence: прежняя стартовая реплика Лео отключена для новой игры.");
                    break;
                }
            }
        }
    }

    void StopBodyMotion()
    {
        if (body == null) return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    void Start() { StartCoroutine(PlayArrival()); }

    void FixedUpdate()
    {
        if (!lockedControl || body == null) return;
        // Сохраняем гравитацию, но не допускаем случайной горизонтальной скорости во вступлении.
        var v = body.linearVelocity;
        body.linearVelocity = new Vector3(0f, v.y, 0f);
        body.angularVelocity = Vector3.zero;
    }

    IEnumerator PlayArrival()
    {
        // MenuFlow ещё выводит игру из чёрного. Не показываем Лео до первого кадра камеры.
        float deadline = Time.unscaledTime + 8f;
        while ((PlayerMovement.Instance == null || Camera.main == null) && Time.unscaledTime < deadline)
            yield return null;
        leo = PlayerMovement.Instance;
        gameCamera = Camera.main;
        if (leo == null || gameCamera == null) { Finish(); yield break; }

        // Если игрок создался поздно, дублируем безопасную подготовку.
        if (!lockedControl) PrepareBeforeFirstFrame();
        if (!lockedControl)
        {
            wasControlLocked = leo.ControlLocked;
            leo.ControlLocked = true;
            lockedControl = true;
            body = leo.GetComponent<Rigidbody>();
        }
        StopBodyMotion();

        PositionAtAnchor();
        CameraFollow.SnapToTarget();
        ResolveScenicDirection();
        BuildSubtitle();
        var arrival = ChapterOneText.Find("twv3_s00_01");
        voiceClip = arrival != null ? ChapterOneText.Audio(arrival) : Resources.Load<AudioClip>(VoiceResource);
        if (voiceClip == null)
            Debug.LogWarning("ArrivalIntroSequence: не найден Assets/Resources/Intro/Leo_Intro_Arrival.wav. Субтитры останутся.");
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.volume = Mathf.Clamp01(GameAudio.VoiceLevel * .95f);
        voiceSource.clip = voiceClip;
        float length = voiceClip != null ? Mathf.Max(1f, voiceClip.length) : FallbackVoiceLength;
        sentenceEnds = VoiceAt + length;
        returnAt = sentenceEnds + AfterVoicePause;
        completeAt = returnAt + ReturnDuration;

        // Подавить все кнопки, пока MenuFlow закрывает загрузочную шторку.
        while (MenuFlow.Busy && Time.unscaledTime < deadline) yield return null;
        if (!isActiveAndEnabled || leo == null) { Finish(); yield break; }
        sceneTime = Time.unscaledTime;
        shotActive = true;
        bool voiceStarted = false;
        while (Time.unscaledTime - sceneTime < completeAt)
        {
            float t = Time.unscaledTime - sceneTime;
            // Однократный запуск без короткого временного окна: не теряем реплику при просадке FPS.
            if (!voiceStarted && t >= VoiceAt)
            {
                voiceStarted = true;
                if (voiceClip != null && GameAudio.UseRecordedVoices) voiceSource.Play();
            }
            if (subtitleGroup != null)
            {
                float intro = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(VoiceAt - 0.1f, VoiceAt + 0.3f, t));
                float outro = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sentenceEnds - 0.2f, sentenceEnds + 0.35f, t));
                subtitleGroup.alpha = DialogueManager.SubtitlesEnabled ? intro * outro : 0f;
            }
            yield return null;
        }
        shotActive = false;
        Finish();
    }

    void PositionAtAnchor()
    {
        var go = GameObject.Find(SpawnMarkerName);
        if (go == null) return;
        anchor = go.transform;
        // Маркер ставят на безопасную поверхность у входа в лагерь, без случайных телепортов по карте.
        // Разворачиваем только игровой root вокруг Y. Модель Лео имеет собственный
        // локальный поворот/риг: прямое присваивание model.rotation ломало стартовый шаг.
        Quaternion yaw = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);
        leo.transform.SetPositionAndRotation(anchor.position, yaw);
        if (body != null)
        {
            body.position = anchor.position;
            body.rotation = yaw;
            StopBodyMotion();
        }
        Physics.SyncTransforms();
    }

    void ResolveScenicDirection()
    {
        Vector3 start = leo.transform.position;
        var camp = GameObject.Find("CAMPFIRE_Point");
        Vector3 d = camp != null ? camp.transform.position - start :
            (leo.model != null ? leo.model.forward : leo.transform.forward);
        d.y = 0f;
        if (d.sqrMagnitude < 0.05f) d = Vector3.forward;
        scenicBack = d.normalized;
        scenicRight = Vector3.Cross(Vector3.up, scenicBack).normalized;
        scenicFocus = start + Vector3.up * 1.7f + scenicBack * 1.25f;
    }

    void LateUpdate()
    {
        if (!shotActive || gameCamera == null || leo == null) return;
        float t = Time.unscaledTime - sceneTime;
        // Gameplay-камера уже посчитана в своём LateUpdate, здесь мы только делаем мягкий авторский ракурс.
        Vector3 gameplayPosition = gameCamera.transform.position;
        Quaternion gameplayRotation = gameCamera.transform.rotation;
        float gameplayFov = gameCamera.fieldOfView;
        float gameplayOrtho = gameCamera.orthographicSize;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - returnAt) / ReturnDuration));
        float shotWeight = 1f - k;

        Vector3 drift = scenicRight * Mathf.Sin(t * 0.5f) * 0.34f + scenicBack * (t * 0.075f);
        Vector3 plannedPos = scenicFocus - scenicBack * 6.4f + Vector3.up * 2.5f + scenicRight * 1.2f + drift;
        plannedPos.y = Mathf.Max(plannedPos.y, leo.transform.position.y + 1.75f);
        Vector3 direction = plannedPos - scenicFocus;
        float dist = direction.magnitude;
        // Отсечь стену/скалу между персонажем и стартовой камерой, игнорируя ВСЕ коллайдеры Лео.
        if (dist > 0.1f)
        {
            int count = Physics.SphereCastNonAlloc(scenicFocus, 0.28f, direction / dist,
                sightHits, dist, ~0, QueryTriggerInteraction.Ignore);
            float closest = dist;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(leo.transform)) continue;
                if (hit.distance > 0.05f && hit.distance < closest) closest = hit.distance;
            }
            if (closest < dist) plannedPos = scenicFocus + direction.normalized * Mathf.Max(0.6f, closest - 0.32f);
        }
        Quaternion plannedRot = Quaternion.LookRotation((scenicFocus + scenicBack * 0.7f - plannedPos).normalized, Vector3.up);
        Vector3 finalPos = Vector3.Lerp(gameplayPosition, plannedPos, shotWeight);
        Quaternion finalRot = Quaternion.Slerp(gameplayRotation, plannedRot, shotWeight);
        gameCamera.transform.SetPositionAndRotation(finalPos, finalRot);
        if (gameCamera.orthographic)
            gameCamera.orthographicSize = Mathf.Lerp(gameplayOrtho, 8.5f, shotWeight);
        else
            gameCamera.fieldOfView = Mathf.Lerp(gameplayFov, 46f, shotWeight);
    }

    void BuildSubtitle()
    {
        var canvasGO = new GameObject("ArrivalSubtitle", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.MainMenu - 10;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var labelGO = new GameObject("LeoSubtitle", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(canvasGO.transform, false);
        var rt = (RectTransform)labelGO.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .075f);
        rt.pivot = new Vector2(.5f,.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1250f, 95f);
        subtitle = labelGO.GetComponent<TextMeshProUGUI>();
        var cue = ChapterOneText.Find("twv3_s00_01");
        if (cue != null) ChapterOneVoice.Mark(cue.id);
        subtitle.text = cue != null
            ? ChapterOneText.SpeakerName(cue.speaker) + ": " + ChapterOneText.Subtitle(cue.speaker, cue.ru)
            : "Лео: Эй!.. Здесь вообще кто-нибудь есть?";
        subtitle.fontSize = 32f;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = new Color(1f,.96f,.87f,1f);
        subtitle.outlineWidth = 0.24f;
        subtitle.outlineColor = new Color32(24,29,42,255);
        subtitle.enableWordWrapping = true;
        subtitle.raycastTarget = false;
        subtitleGroup = labelGO.GetComponent<CanvasGroup>();
        subtitleGroup.alpha = 0f;
        subtitleGroup.interactable = false;
        subtitleGroup.blocksRaycasts = false;
    }

    void Finish()
    {
        shotActive = false;
        if (voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
        if (lockedControl && leo != null) leo.ControlLocked = wasControlLocked;
        lockedControl = false;
        Active = false;
        if (instance == this) instance = null;
        if (this != null) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (lockedControl && leo != null) leo.ControlLocked = wasControlLocked;
        lockedControl = false;
        if (instance == this) { instance = null; Active = false; }
    }
}
