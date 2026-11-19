// Assets/_Project/Scripts/Vibe/SfxManager.cs (НЕ в Editor)
// Пул эффектов игры. В первую очередь использует записанные клипы из Resources/Sfx.
// Слушает события игры сам: рубка (удары в такт, падение дерева), бревно и доска в рюкзак, монеты, шаги, стройка,
// уровень, выполненное задание. Бормотание реплик (GDD 12.3, «голоса-бормотания»): DialogueManager зовёт SfxManager.Blip.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Мелкий синтезатор: клип из функции времени, колокольчики, огибающие.
public static class VibeSynth
{
    public const int Rate = 44100;
    static readonly System.Random rng = new System.Random(20261011);

    public static float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

    public static float Sin(float freq, float t) { return Mathf.Sin(2f * Mathf.PI * freq * t); }

    // быстрый старт и плавный спад
    public static float Env(float t, float attack, float decay)
    {
        if (t < 0f) return 0f;
        return Mathf.Clamp01(t / Mathf.Max(0.0001f, attack)) * Mathf.Exp(-t / Mathf.Max(0.0001f, decay));
    }

    // колокольчик: основной тон и два обертона
    public static float Bell(float freq, float t, float decay)
    {
        if (t < 0f) return 0f;
        float s = Sin(freq, t) + 0.35f * Sin(freq * 2.01f, t) + 0.15f * Sin(freq * 3.02f, t);
        return s * Env(t, 0.004f, decay);
    }

    public static float Tri(float freq, float t)
    {
        float p = freq * t - Mathf.Floor(freq * t);
        return 4f * Mathf.Abs(p - 0.5f) - 1f;
    }

    // seconds секунд звука, f(t) возвращает отсчёт; результат нормализуется до peak
    public static AudioClip Make(string name, float seconds, Func<float, float> f, float peak = 0.9f)
    {
        int n = Mathf.Max(64, (int)(seconds * Rate));
        var d = new float[n];
        float max = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float v = f(i / (float)Rate);
            d[i] = v;
            float a = Mathf.Abs(v);
            if (a > max) max = a;
        }
        float g = peak / max;
        int fade = Mathf.Min(300, n / 4);
        for (int i = 0; i < n; i++)
        {
            float k = i >= n - fade ? (n - 1 - i) / (float)fade : 1f;   // без щелчка в конце
            d[i] *= g * k;
        }
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}

public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    const int PoolSize = 14;

    AudioSource[] pool;
    AudioSource blipSrc;
    readonly bool[] timberVoice = new bool[PoolSize];
    readonly float[] timberGain = new float[PoolSize];
    int next;

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    readonly System.Random soundRandom = new System.Random();

    PlayerMovement pm;
    PlayerInventory inv;
    float findAt;
    bool invInit;
    int lastWood, lastPlanks, lastCoins;
    Vector3 lastPos;
    bool hasPos;
    float stepDist;
    CapsuleCollider stepBody;
    Renderer campEarth;
    readonly RaycastHit[] stepHits = new RaycastHit[12];

    readonly HashSet<BuildingController> hooked = new HashSet<BuildingController>();
    float hookAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    // ------------------------------------------------------------ публичное
    public static void Play(string key, float volume = 1f, float pitch = 1f)
    {
        if (Instance != null) Instance.PlayInternal(key, volume, pitch);
    }

    internal static void Timber(string key, float volume = 1f, float pitch = 1f, Vector3? point = null,
        string fallback = null)
    {
        if (Instance != null && Instance.isActiveAndEnabled)
            Instance.PlayInternal(key, volume, pitch, point, timber: true, fallback: fallback);
    }

    // бормотание реплики: зовётся на каждую новую букву, сам решает, на какой звучать
    public static void Blip(string speaker, string text, int index)
    {
        if (Instance != null) Instance.BlipInternal(speaker, text, index);
    }

    // короткий «голос» для ползунка «Голоса» в настройках (играет, даже если бормотание выключено)
    public static void PreviewVoice()
    {
        if (Instance != null) Instance.PreviewVoiceInternal();
    }

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        Instance = this;
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var voice = new GameObject("SFX voice " + (i + 1));
            voice.transform.SetParent(transform, false);
            pool[i] = NewSource(voice);
        }
        blipSrc = NewSource(gameObject);
    }

    static AudioSource NewSource(GameObject owner)
    {
        var s = owner.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.dopplerLevel = 0f;
        s.minDistance = 1.6f;
        s.maxDistance = 24f;
        s.rolloffMode = AudioRolloffMode.Linear;
        return s;
    }

    void OnEnable()
    {
        GameEvents.TreeChopped += OnTreeChopped;
        GameEvents.ChopHit += OnChopHit;
        GameEvents.LevelUp += OnLevelUp;
        GameEvents.QuestDone += OnQuestDone;
        SaveSystem.Loaded += ResetInventoryAudio;
        SaveSystem.NewGameStarted += ResetInventoryAudio;
    }

    void OnDisable()
    {
        GameEvents.TreeChopped -= OnTreeChopped;
        GameEvents.ChopHit -= OnChopHit;
        GameEvents.LevelUp -= OnLevelUp;
        GameEvents.QuestDone -= OnQuestDone;
        SaveSystem.Loaded -= ResetInventoryAudio;
        SaveSystem.NewGameStarted -= ResetInventoryAudio;
        ResetInventoryAudio();
        foreach (var voice in pool) voice.Stop();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var b in hooked)
        {
            if (b == null) continue;
            b.StageBuilt -= OnStageBuilt;
            b.Finished -= OnBuildFinished;
        }
        hooked.Clear();
    }

    // удар топора приходит из PlayerInteraction (по таймеру или по событию из анимации), поэтому звук всегда в такт кадру удара
    void OnChopHit()
    {
        var actor = PlayerInteraction.ActivePlayer;
        if (actor == null || !actor.Chop03ImpactThisFrame && !OrbitCamera.FirstPersonActive)
        {
            PlayInternal("thock", .95f, UnityEngine.Random.Range(.90f, 1.10f));
            return;
        }
        Vector3? point = actor.TryReadLeoImpact(out var contact) ? contact : actor.CurrentTarget != null
            ? actor.CurrentTarget.position + Vector3.up * .65f : (Vector3?)null;
        bool ground = actor.IsProcessingPrototypeLog;
        var quality = PlayerInteraction.CurrentChop03Result;
        string key = quality switch
        {
            ChopTimingResult.Perfect => "axePerfect",
            ChopTimingResult.Early or ChopTimingResult.Late => "axeWeak",
            _ => ground ? "axeGround" : "axeWood"
        };
        float pitch = ground ? .94f : 1f;
        if (actor.EquippedAxe.Id == AxeId.Iron || actor.EquippedAxe.Id == AxeId.Gunnar) pitch *= .94f;
        Timber(key, ground ? .83f : .91f, pitch, point, "thock");
        if (VibeSettings.ReduceEffects) return;
        if (quality == ChopTimingResult.Good) Timber("timingGood", .25f);
        if (quality == ChopTimingResult.Perfect) Timber("timingPerfect", .29f);
        if (quality == ChopTimingResult.Early || quality == ChopTimingResult.Late) Timber("timingError", .23f);
    }
    void OnTreeChopped()
    {
        if (PlayerInteraction.PrototypeFellingEvent)
        {
            var actor = PlayerInteraction.ActivePlayer;
            Vector3? point = actor != null && actor.TryReadLeoImpact(out var bark) ? bark : (Vector3?)null;
            Timber("woodCrack", .78f, .91f, point);
        }
        else
            PlayInternal("fall", 1f, UnityEngine.Random.Range(.94f, 1.06f));
    }
    void OnLevelUp(int level) { PlayInternal("level", 1f, 1f); }
    void OnQuestDone(string id) { PlayInternal("quest", 1f, 1f); }
    void OnStageBuilt() { PlayInternal("build", 1f, 1f); }
    void OnBuildFinished() { PlayInternal("buildDone", 1f, 1f); }

    // ------------------------------------------------------------ каждый кадр
    void Update()
    {
        float dt = Time.deltaTime;
        UpdateTimberVoices();

        if (pm == null || inv == null)
        {
            if (Time.unscaledTime >= findAt)
            {
                findAt = Time.unscaledTime + 1f;
                if (pm == null)
                {
                    pm = PlayerMovement.Instance;
                    stepBody = pm != null ? pm.GetComponent<CapsuleCollider>() : null;
                    var center = pm != null ? FindFirstObjectByType<CenterArtState>() : null;
                    var earth = center != null ? center.transform.Find("Organic_Earth_Clearing") : null;
                    campEarth = earth != null ? earth.GetComponent<Renderer>() : null;
                    stepDist = 0f;
                    hasPos = false;
                }
                if (pm != null)
                {
                    if (inv == null)
                    {
                        inv = pm.GetComponent<PlayerInventory>();
                        ResetInventoryAudio();
                    }
                }
            }
        }

        TickSteps(dt);
        TickInventory();
        TickBuildHooks();
    }

    void TickSteps(float dt)
    {
        if (pm == null || dt < 0.0001f) return;
        Vector3 p = pm.transform.position;
        if (!hasPos) { lastPos = p; hasPos = true; return; }
        Vector3 d = p - lastPos;
        d.y = 0f;
        lastPos = p;
        float m = d.magnitude;
        if (m / dt > 14f || WaterSwim.Swimming || CampSleep.Sleeping ||
            (IslandEdgeGuard.Instance != null && IslandEdgeGuard.Instance.IsFalling))
        {
            stepDist = 0f;
            return;
        }
        stepDist += m;
        if (stepDist >= 1.25f)
        {
            stepDist -= 1.25f;
            PlayFootstep(p);
        }
    }

    void PlayFootstep(Vector3 position)
    {
        position.y = (stepBody != null ? stepBody.bounds.min.y : position.y) + .20f;
        Collider surface = null;
        float distance = float.PositiveInfinity;
        int count = Physics.RaycastNonAlloc(position, Vector3.down, stepHits, .52f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = stepHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(pm.transform) || hit.distance >= distance) continue;
            surface = hit.collider;
            distance = hit.distance;
        }
        if (surface == null) return;

        bool sand = surface.name.IndexOf("sand", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!sand && campEarth != null && campEarth.enabled && campEarth.gameObject.activeInHierarchy && ChopPrototypeLog.IsGround(surface))
        {
            Bounds bounds = campEarth.bounds;
            Vector3 delta = position - bounds.center;
            float x = delta.x / Mathf.Max(.1f, bounds.extents.x);
            float z = delta.z / Mathf.Max(.1f, bounds.extents.z);
            sand = x * x + z * z < .93f;
        }
        PlayInternal(sand ? "stepSand" : "step", .5f, UnityEngine.Random.Range(.91f, 1.10f));
    }

    void TickInventory()
    {
        if (inv == null) return;
        if (!invInit || SaveSystem.IsLoading)
        {
            invInit = true;
            lastWood = inv.woodCount; lastPlanks = inv.planksCount; lastCoins = inv.coinsCount;
            return;
        }

        if (inv.woodCount > lastWood) Timber("logPickup", .70f, fallback: "log");
        if (inv.planksCount > lastPlanks)
        {
            var workshop = PlankWorkshopWindow.Instance;
            if (workshop != null && workshop.IsOpen && !SettingsMenu.IsOpen && Time.timeScale > 0f &&
                Application.isFocused && !DialogueManager.IsBusy && !DialogueSequence.AnyPlaying)
                PlayInternal("boardPickup", .69f, 1f, fallback: "log");
            else
                Timber("boardPickup", .69f, fallback: "log");
        }

        int dc = inv.coinsCount - lastCoins;
        if (dc > 0)
        {
            int n = dc >= 20 ? 3 : (dc >= 6 ? 2 : 1);          // крупная выручка звенит дольше
            StartCoroutine(Cascade(n));
        }

        lastWood = inv.woodCount; lastPlanks = inv.planksCount; lastCoins = inv.coinsCount;
    }

    void ResetInventoryAudio() => invInit = false;

    IEnumerator Cascade(int n)
    {
        for (int i = 0; i < n; i++)
        {
            PlayInternal("coin", 0.65f, 1f + i * 0.14f);
            yield return new WaitForSecondsRealtime(0.07f);   // хит-стоп (timeScale 0.06) растягивал звон монет в 16 раз
        }
    }

    // подписка на стройки: они могут появиться позже старта
    void TickBuildHooks()
    {
        if (Time.unscaledTime < hookAt) return;
        hookAt = Time.unscaledTime + 2f;
        foreach (var b in FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null || !hooked.Add(b)) continue;
            b.StageBuilt += OnStageBuilt;
            b.Finished += OnBuildFinished;
        }
    }

    // ------------------------------------------------------------ воспроизведение
    void UpdateTimberVoices()
    {
        bool blocked = HudGameplayLayout.Blocked;
        float level = GameAudio.SfxLevel;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!timberVoice[i]) continue;
            if (!pool[i].isPlaying || blocked)
            {
                pool[i].Stop();
                timberVoice[i] = false;
                continue;
            }
            pool[i].volume = Mathf.Clamp01(timberGain[i] * level);
        }
    }

    void PlayInternal(string key, float volume, float pitch, Vector3? point = null, bool timber = false,
        string fallback = null)
    {
        float level = GameAudio.SfxLevel;
        if (level <= .001f || timber && HudGameplayLayout.Blocked) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(key, out var last) && now - last < (timber ? .058f : .03f)) return;
        AudioClip clip = Get(key);
        if (clip == null && fallback != null) clip = Get(fallback);
        if (clip == null) return;
        lastPlayed[key] = now;

        int index = next;
        AudioSource s = pool[index];
        next = (next + 1) % pool.Length;
        s.Stop();
        s.clip = clip;
        s.spatialBlend = point.HasValue ? .86f : 0f;
        if (point.HasValue) s.transform.position = point.Value;
        else s.transform.localPosition = Vector3.zero;
        if (timber)
        {
            pitch *= Mathf.Lerp(.956f, 1.043f, (float)soundRandom.NextDouble());
            volume *= Mathf.Lerp(.92f, 1.03f, (float)soundRandom.NextDouble());
        }
        timberVoice[index] = timber;
        timberGain[index] = volume;
        s.pitch = pitch;
        s.volume = Mathf.Clamp01(volume * level);
        s.Play();
    }

    void BlipInternal(string speaker, string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length) return;
        char c = text[index];
        if (!char.IsLetter(c) || index % 3 != 0) return;
        float vv = GameAudio.VoiceLevel;                   // бормотание = голос персонажа, регулируется ползунком «Голоса»
        if (!GameAudio.UseBabble || vv <= 0.001f) return;

        AudioClip clip = Get("blip:" + speaker);
        if (clip == null) return;
        blipSrc.Stop();
        blipSrc.clip = clip;
        blipSrc.pitch = 0.85f + ((c * 7) % 9) * 0.04f;
        blipSrc.volume = Mathf.Clamp01(0.32f * vv);
        blipSrc.Play();
    }

    void PreviewVoiceInternal()
    {
        blipSrc.Stop();
        float vv = GameAudio.VoiceLevel;
        if (vv <= 0.001f) return;
        var library = DialogueAudioLibrary.Current;
        AudioClip clip = GameAudio.UseRecordedVoices && library != null ? library.Find("leo_5c45e50c") : null;
        if (clip == null && GameAudio.UseBabble)
            clip = DialogueBabble.PreviewClip("Лео");
        if (clip == null) return;
        blipSrc.Stop();
        blipSrc.clip = clip;
        blipSrc.pitch = 1f;
        blipSrc.volume = Mathf.Clamp01(0.5f * vv);
        blipSrc.Play();
    }

    // ------------------------------------------------------------ синтез клипов
    // Настоящие звуки: Assets/_Project/Resources/Sfx/<ключ>/*.wav|ogg|mp3. Несколько файлов в папке = варианты, играется случайный.
    // Папки нет или она пустая: звучит синтез из кода, как раньше.
    readonly Dictionary<string, AudioClip[]> files = new Dictionary<string, AudioClip[]>();
    readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();

    AudioClip Get(string key)
    {
        if (key.IndexOf(':') < 0)
        {
            AudioClip[] arr;
            if (!files.TryGetValue(key, out arr)) { arr = Resources.LoadAll<AudioClip>("Sfx/" + key); files[key] = arr; }
            if (arr.Length > 0)
            {
                // варианты не повторяются два раза подряд (иначе рубка и шаги звучат как заевшая пластинка)
                int pick;
                if (arr.Length > 1 && lastVariant.TryGetValue(key, out int previous))
                {
                    pick = soundRandom.Next(arr.Length - 1);
                    if (pick >= previous) pick++;
                }
                else pick = soundRandom.Next(arr.Length);
                lastVariant[key] = pick;
                return arr[pick];
            }
        }

        AudioClip c;
        if (clips.TryGetValue(key, out c)) return c;
        c = Build(key);
        clips[key] = c;
        return c;
    }

    static AudioClip Build(string key)
    {
        if (key.StartsWith("blip:")) return BuildBlip(key.Substring(5));

        switch (key)
        {
            case "thock":   // удар топора: низкий «тук», щелчок и короткий шорох
            {
                float lp = 0f;
                return VibeSynth.Make("thock", 0.22f, t =>
                {
                    float tone = Mathf.Sin(2f * Mathf.PI * (95f * t + 2f * (1f - Mathf.Exp(-30f * t)))) * VibeSynth.Env(t, 0.002f, 0.07f);
                    lp += 0.35f * (VibeSynth.Noise() - lp);
                    float body = lp * VibeSynth.Env(t, 0.001f, 0.025f) * 0.9f;
                    float click = VibeSynth.Noise() * VibeSynth.Env(t, 0.0005f, 0.003f) * 0.5f;
                    return tone + body + click;
                });
            }
            case "woodCrack":
            {
                float smooth = 0f;
                return VibeSynth.Make("woodCrack", .28f, t =>
                {
                    float noise = VibeSynth.Noise();
                    smooth += .19f * (noise - smooth);
                    float clicks = VibeSynth.Env(t, .0008f, .018f) +
                        .68f * VibeSynth.Env(t - .047f, .0012f, .023f) +
                        .37f * VibeSynth.Env(t - .102f, .001f, .024f);
                    float note = VibeSynth.Sin(143f - t * 150f, t) * VibeSynth.Env(t, .002f, .046f);
                    return (noise * .29f + smooth * 1.35f) * clicks + note * .20f;
                });
            }
            case "trunkLand":
            {
                float smooth = 0f;
                return VibeSynth.Make("trunkLand", .40f, t =>
                {
                    smooth += .07f * (VibeSynth.Noise() - smooth);
                    return (VibeSynth.Sin(57f, t) * .58f + VibeSynth.Sin(89f, t) * .21f + smooth) *
                        VibeSynth.Env(t, .004f, .083f);
                }, .72f);
            }
            case "fall":    // дерево падает: шорох кроны и глухой удар о землю
            {
                float lp = 0f;
                return VibeSynth.Make("fall", 0.95f, t =>
                {
                    lp += 0.15f * (VibeSynth.Noise() - lp);
                    float rustle = lp * Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t / 0.35f) * 0.7f;
                    float tt = t - 0.28f;
                    float thud = tt > 0f ? VibeSynth.Sin(58f, tt) * VibeSynth.Env(tt, 0.003f, 0.13f) : 0f;
                    float burst = tt > 0f ? VibeSynth.Noise() * VibeSynth.Env(tt, 0.001f, 0.02f) * 0.4f : 0f;
                    return rustle + thud + burst;
                });
            }
            case "log":     // бревно в рюкзак: короткий восходящий «блюп»
                return VibeSynth.Make("log", 0.13f, t =>
                {
                    float ph = 2f * Mathf.PI * (520f * t + 1083f * t * t);
                    return (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 2f)) * VibeSynth.Env(t, 0.003f, 0.05f);
                }, 0.7f);
            case "coin":    // монета: две высокие ноты
                return VibeSynth.Make("coin", 0.5f, t =>
                {
                    float a = VibeSynth.Bell(1318f, t, 0.12f);
                    float b = VibeSynth.Bell(1760f, t - 0.07f, 0.16f);
                    return a * 0.8f + b;
                }, 0.7f);
            case "level":   // новый уровень: арпеджио вверх
                return VibeSynth.Make("level", 1.1f, t =>
                {
                    float[] f = { 523f, 659f, 784f, 1047f };
                    float s = 0f;
                    for (int i = 0; i < f.Length; i++) s += VibeSynth.Bell(f[i], t - i * 0.11f, 0.3f);
                    return s;
                }, 0.85f);
            case "quest":   // задание выполнено: тёплый аккорд, затем светлое разрешение
                return VibeSynth.Make("quest", 1.6f, t =>
                {
                    float s = 0f;
                    s += VibeSynth.Bell(392f, t, 0.5f) * 0.7f;
                    s += VibeSynth.Bell(494f, t, 0.5f) * 0.6f;
                    s += VibeSynth.Bell(587f, t, 0.5f) * 0.6f;
                    s += VibeSynth.Bell(523f, t - 0.26f, 0.7f);
                    s += VibeSynth.Bell(659f, t - 0.26f, 0.7f) * 0.9f;
                    s += VibeSynth.Bell(784f, t - 0.26f, 0.8f) * 0.9f;
                    s += VibeSynth.Bell(1047f, t - 0.26f, 0.9f) * 0.5f;
                    return s;
                }, 0.85f);
            case "build":   // стук молотка
            {
                float lp = 0f;
                return VibeSynth.Make("build", 0.28f, t =>
                {
                    lp += 0.3f * (VibeSynth.Noise() - lp);
                    return VibeSynth.Sin(165f, t) * VibeSynth.Env(t, 0.001f, 0.05f)
                         + lp * VibeSynth.Env(t, 0.001f, 0.03f) * 0.8f
                         + (t > 0.13f ? VibeSynth.Sin(190f, t - 0.13f) * VibeSynth.Env(t - 0.13f, 0.001f, 0.05f) * 0.8f : 0f);
                }, 0.85f);
            }
            case "buildDone":   // постройка готова: быстрая восходящая пентатоника
                return VibeSynth.Make("buildDone", 1.0f, t =>
                {
                    float[] f = { 523f, 587f, 659f, 784f, 880f };
                    float s = 0f;
                    for (int i = 0; i < f.Length; i++) s += VibeSynth.Bell(f[i], t - i * 0.07f, 0.4f);
                    return s;
                }, 0.8f);
            case "step":    // шаг по траве: мягкий шорох и глухой толчок
            {
                float lp = 0f;
                return VibeSynth.Make("step", 0.1f, t =>
                {
                    lp += 0.2f * (VibeSynth.Noise() - lp);
                    return lp * VibeSynth.Env(t, 0.003f, 0.025f) + VibeSynth.Sin(70f, t) * VibeSynth.Env(t, 0.002f, 0.03f) * 0.5f;
                }, 0.5f);
            }
            case "stepSand":
            {
                float lp = 0f;
                return VibeSynth.Make("stepSand", .15f, t =>
                {
                    lp += .34f * (VibeSynth.Noise() - lp);
                    float grain = .65f + .35f * VibeSynth.Sin(37f, t);
                    float scuff = lp * grain * VibeSynth.Env(t, .006f, .042f);
                    float heel = .15f * VibeSynth.Sin(92f, t) * VibeSynth.Env(t, .002f, .021f);
                    return scuff + heel;
                }, .48f);
            }
            case "grow":    // дерево выросло: мягкий восходящий «пуф» и шорох листвы
            {
                float lp = 0f;
                return VibeSynth.Make("grow", 0.28f, t =>
                {
                    float ph = 2f * Mathf.PI * (300f * t + 420f * t * t);
                    float tone = Mathf.Sin(ph) * VibeSynth.Env(t, 0.01f, 0.09f) * 0.6f;
                    lp += 0.2f * (VibeSynth.Noise() - lp);
                    float rustle = lp * VibeSynth.Env(t, 0.02f, 0.1f) * 0.8f;
                    return tone + rustle;
                }, 0.4f);
            }
            case "sleep":   // ложимся спать: мягкий нисходящий колыбельный перелив
                return VibeSynth.Make("sleep", 1.4f, t =>
                {
                    float[] f = { 659f, 523f, 440f, 330f };
                    float s = 0f;
                    for (int i = 0; i < f.Length; i++) s += VibeSynth.Bell(f[i], t - i * 0.22f, 0.55f) * (1f - i * 0.15f);
                    return s;
                }, 0.55f);
            case "silver":  // серебряное дерево: холодный высокий «звон» с мерцанием
                return VibeSynth.Make("silver", 1.2f, t =>
                {
                    float shimmer = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                    return (VibeSynth.Bell(1568f, t, 0.45f) + 0.8f * VibeSynth.Bell(2093f, t - 0.09f, 0.5f) + 0.5f * VibeSynth.Bell(2637f, t - 0.18f, 0.55f)) * shimmer;
                }, 0.6f);
            case "hint":    // всплыла подсказка: две мягкие ноты
                return VibeSynth.Make("hint", 0.4f, t => VibeSynth.Bell(660f, t, 0.12f) + VibeSynth.Bell(880f, t - 0.09f, 0.16f), 0.6f);
            case "riser":   // зарядка редкой карты: тон ползёт вверх, шум крепнет, в конце короткий вдох перед ударом
            {
                float lp = 0f;
                return VibeSynth.Make("riser", 1.6f, t =>
                {
                    float k = t / 1.6f;
                    float ph = 2f * Mathf.PI * (160f * t + 900f * t * t * t / (3f * 2.56f));
                    lp += 0.25f * (VibeSynth.Noise() - lp);
                    float tail = t > 1.5f ? (1.6f - t) * 10f : 1f;
                    return (Mathf.Sin(ph) * 0.6f + Mathf.Sin(ph * 1.5f) * 0.25f + lp * 0.5f) * k * k * tail;
                }, 0.55f);
            }
            case "boom":    // удар при открытии легендарной и мифической карты: низкий «бум» с падающим тоном и шорохом
            {
                float lp = 0f;
                return VibeSynth.Make("boom", 1.4f, t =>
                {
                    lp += 0.12f * (VibeSynth.Noise() - lp);
                    float ph = 2f * Mathf.PI * (45f * t + 65f * (1f - Mathf.Exp(-5f * t)) / 5f);
                    return Mathf.Sin(ph) * VibeSynth.Env(t, 0.004f, 0.45f) + lp * VibeSynth.Env(t, 0.001f, 0.12f) * 0.9f;
                }, 0.9f);
            }
            case "ui":
                return VibeSynth.Make("ui", 0.07f, t => VibeSynth.Sin(880f, t) * VibeSynth.Env(t, 0.002f, 0.02f), 0.5f);
        }
        return null;
    }

    // у каждого говорящего свой «голос»: Оскар низкий, Мира звонкая, Лео посередине
    static AudioClip BuildBlip(string who)
    {
        string w = (who ?? "").ToLowerInvariant();
        float f;
        if (w.Contains("оскар")) f = 130f;
        else if (w.Contains("мира")) f = 310f;
        else if (w.Contains("лео")) f = 210f;
        else
        {
            int h = 0;
            for (int i = 0; i < w.Length; i++) h = (h * 31 + w[i]) & 0x7fffffff;
            f = 160f + (h % 8) * 28f;
        }
        return VibeSynth.Make("blip", 0.05f, t => VibeSynth.Tri(f, t) * VibeSynth.Env(t, 0.003f, 0.016f), 0.6f);
    }
}
