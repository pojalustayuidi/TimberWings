// Assets/_Project/Scripts/Build/Stages/TentStages.cs   (НЕ в папке Editor!)
// Управляет двумя состояниями палатки и всей её анимацией:
//   состояние 1 — стройплощадка, состояние 2 — готовая палатка.
// SetStage(2) в игре проигрывает «раскладывание»: площадка схлопывается,
// палатка вырастает с пружинкой, вылетает облачко пыли, плавно зажигается свет.
// Дальше постоянно: флаг на ветру, занавесы входа покачиваются, фонари раскачиваются, свет мерцает.
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TentStages : MonoBehaviour
{
    [Header("Состояния")]
    public GameObject stage1;
    public GameObject stage2;
    public Transform buildPoint;
    [Tooltip("С какого состояния начинается игра")]
    public int startStage = 1;
    public UnityEvent<int> onStageChanged = new UnityEvent<int>();

    [Header("Анимация (заполняется билдером)")]
    public Transform[] curtains;
    public Transform[] flagSegs;
    public Transform[] swingers;
    public Light[] lights;
    public ParticleSystem buildPuff;

    [Header("Параметры")]
    public float buildTime = 1.1f;
    public float swingAmp = 3.5f;
    public float swingSpeed = 1.5f;
    public float flagAmp = 14f;

    public int Stage { get; private set; }
    public bool IsBuilding { get { return routine != null; } }

    float[] lightBase;
    float lightFactor = 1f;
    bool init;
    Coroutine routine;
    Vector3 s1Scale = Vector3.one, s2Scale = Vector3.one;

    void Init()
    {
        if (init) return;
        init = true;
        if (stage1 != null) s1Scale = stage1.transform.localScale;
        if (stage2 != null) s2Scale = stage2.transform.localScale;
        int n = lights != null ? lights.Length : 0;
        lightBase = new float[n];
        for (int i = 0; i < n; i++) if (lights[i] != null) lightBase[i] = lights[i].intensity;
    }

    void Start()
    {
        if (Application.isPlaying) SetStage(startStage, false);
    }

    public void SetStage(int stage) { SetStage(stage, Application.isPlaying); }

    // stage >= 2 считается «готово»
    public void SetStage(int stage, bool animate)
    {
        Init();
        if (routine != null) { StopCoroutine(routine); routine = null; }

        bool toBuilt = stage >= 2;
        bool wasBuilt = Stage >= 2;
        Stage = toBuilt ? 2 : 1;

        if (animate && toBuilt && !wasBuilt && isActiveAndEnabled && stage1 != null && stage2 != null)
            routine = StartCoroutine(BuildRoutine());
        else
            Apply(toBuilt);

        if (onStageChanged != null) onStageChanged.Invoke(Stage);
    }

    // Загрузка сохранения: сразу готовое/неготовое состояние, без раскладывания, пыли и onStageChanged
    public void RestoreStage(int stage)
    {
        Init();
        if (routine != null) { StopCoroutine(routine); routine = null; }
        bool built = stage >= 2;
        Stage = built ? 2 : 1;
        Apply(built);
    }

    void Apply(bool built)
    {
        if (stage1 != null) { stage1.SetActive(!built); stage1.transform.localScale = s1Scale; }
        if (stage2 != null) { stage2.SetActive(built); stage2.transform.localScale = s2Scale; }
        lightFactor = 1f;
    }

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    IEnumerator BuildRoutine()
    {
        stage1.SetActive(true);
        stage2.SetActive(false);

        // 1) стройплощадка схлопывается
        float t = 0f, a = 0.3f;
        while (t < a)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / a);
            stage1.transform.localScale = s1Scale * (1f - k * k);
            yield return null;
        }
        stage1.SetActive(false);
        stage1.transform.localScale = s1Scale;

        // 2) палатка "вырастает" с пружинкой, пыль, свет плавно включается
        stage2.SetActive(true);
        lightFactor = 0f;
        stage2.transform.localScale = new Vector3(s2Scale.x * 0.75f, s2Scale.y * 0.02f, s2Scale.z * 0.75f);
        if (buildPuff != null) { buildPuff.Clear(); buildPuff.Play(); }

        t = 0f;
        while (t < buildTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / buildTime);
            float xz = Mathf.Lerp(0.75f, 1f, 1f - (1f - k) * (1f - k));
            stage2.transform.localScale = new Vector3(s2Scale.x * xz, s2Scale.y * Mathf.Max(0.02f, EaseOutBack(k)), s2Scale.z * xz);
            lightFactor = k;
            yield return null;
        }
        Apply(true);
        routine = null;
    }

    void Update()
    {
        if (stage2 == null || !stage2.activeInHierarchy) return;
        float t = Time.time;

        // флаг: волна бежит по сегментам
        if (flagSegs != null)
            for (int i = 0; i < flagSegs.Length; i++)
                if (flagSegs[i] != null)
                    flagSegs[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 3.2f - i * 0.9f) * (flagAmp * 0.6f + i * 3f), Mathf.Sin(t * 2.1f - i * 0.7f) * 4f);

        // занавесы входа — лёгкое покачивание
        if (curtains != null)
            for (int i = 0; i < curtains.Length; i++)
                if (curtains[i] != null)
                    curtains[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 1.3f + i * 2.2f) * 4f + Mathf.Sin(t * 3.1f + i) * 1.2f, 0f);

        // фонари качаются
        if (swingers != null)
            for (int i = 0; i < swingers.Length; i++)
                if (swingers[i] != null)
                {
                    float s = Mathf.Sin(t * swingSpeed + i * 1.7f) * swingAmp;
                    swingers[i].localRotation = Quaternion.Euler(s * 0.6f, 0f, s);
                }

        // мерцание света
        if (lights != null && lightBase != null)
            for (int i = 0; i < lights.Length && i < lightBase.Length; i++)
                if (lights[i] != null)
                {
                    float n = Mathf.PerlinNoise(t * 5f, i * 5.3f);
                    lights[i].intensity = lightBase[i] * (0.85f + 0.3f * n) * lightFactor;
                }
    }
}
