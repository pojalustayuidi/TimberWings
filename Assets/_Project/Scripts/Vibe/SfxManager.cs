// Assets/_Project/Scripts/Vibe/SfxManager.cs (НЕ в Editor)
// Звук игры без единого аудиофайла: все эффекты синтезируются кодом при первом использовании.
// Слушает события игры сам: рубка (удары в такт, падение дерева), бревно и доска в рюкзак, монеты, шаги, стройка,
// уровень, выполненное задание. Бормотание реплик (GDD 12.3, «голоса-бормотания»): DialogueManager зовёт SfxManager.Blip.
// Когда появятся настоящие звуки из Freesound, замени клип в Get(): ключи остаются теми же.
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
    int next;
    float volCache = 0.8f, volAt;

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    PlayerMovement pm;
    PlayerInteraction pi;
    PlayerInventory inv;
    float findAt;
    bool invInit;
    int lastWood, lastPlanks, lastCoins;
    Vector3 lastPos;
    bool hasPos;
    float stepDist;

    readonly HashSet<BuildingController> hooked = new HashSet<BuildingController>();
    float hookAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    // ------------------------------------------------------------ публичное
    public static void Play(string key, float volume = 1f, float pitch = 1f)
    {
        if (Instance != null) Instance.PlayInternal(key, volume, pitch);
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
        for (int i = 0; i < PoolSize; i++) pool[i] = NewSource();
        blipSrc = NewSource();
    }

    AudioSource NewSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    void OnEnable()
    {
        GameEvents.TreeChopped += OnTreeChopped;
        GameEvents.ChopHit += OnChopHit;
        GameEvents.LevelUp += OnLevelUp;
        GameEvents.QuestDone += OnQuestDone;
    }

    void OnDisable()
    {
        GameEvents.TreeChopped -= OnTreeChopped;
        GameEvents.ChopHit -= OnChopHit;
        GameEvents.LevelUp -= OnLevelUp;
        GameEvents.QuestDone -= OnQuestDone;
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
    void OnChopHit() { PlayInternal("thock", 0.95f, UnityEngine.Random.Range(0.92f, 1.08f)); }
    void OnTreeChopped() { PlayInternal("fall", 1f, UnityEngine.Random.Range(0.94f, 1.06f)); }
    void OnLevelUp(int level) { PlayInternal("level", 1f, 1f); }
    void OnQuestDone(string id) { PlayInternal("quest", 1f, 1f); }
    void OnStageBuilt() { PlayInternal("build", 1f, 1f); }
    void OnBuildFinished() { PlayInternal("buildDone", 1f, 1f); }

    // ------------------------------------------------------------ каждый кадр
    void Update()
    {
        float dt = Time.deltaTime;
        volCache = GameAudio.SfxLevel;   // раньше обновлялось раз в 0.5 с: ползунок «Звуки» реагировал с задержкой

        if (pm == null || pi == null || inv == null)
        {
            if (Time.unscaledTime >= findAt)
            {
                findAt = Time.unscaledTime + 1f;
                if (pm == null) pm = PlayerMovement.Instance;
                if (pm != null)
                {
                    if (pi == null) pi = pm.GetComponent<PlayerInteraction>();
                    if (inv == null) inv = pm.GetComponent<PlayerInventory>();
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
        if (m / dt > 14f) return;                   // телепорт, а не шаг
        stepDist += m;
        if (stepDist >= 1.25f)
        {
            stepDist -= 1.25f;
            PlayInternal("step", 0.5f, UnityEngine.Random.Range(0.85f, 1.15f));
        }
    }

    void TickInventory()
    {
        if (inv == null) return;
        if (!invInit)
        {
            invInit = true;
            lastWood = inv.woodCount; lastPlanks = inv.planksCount; lastCoins = inv.coinsCount;
            return;
        }

        if (inv.woodCount > lastWood) PlayInternal("log", 0.7f, 1f + Mathf.Min(6, inv.woodCount) * 0.025f);
        if (inv.planksCount > lastPlanks) PlayInternal("log", 0.75f, 0.78f);

        int dc = inv.coinsCount - lastCoins;
        if (dc > 0)
        {
            int n = dc >= 20 ? 3 : (dc >= 6 ? 2 : 1);          // крупная выручка звенит дольше
            StartCoroutine(Cascade(n));
        }

        lastWood = inv.woodCount; lastPlanks = inv.planksCount; lastCoins = inv.coinsCount;
    }

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
    void PlayInternal(string key, float volume, float pitch)
    {
        volCache = GameAudio.SfxLevel;
        if (volCache <= 0.001f) return;                    // звуки выключены: даже не создаём клип
        AudioClip c = Get(key);
        if (c == null) return;

        float now = Time.unscaledTime, last;
        if (lastPlayed.TryGetValue(key, out last) && now - last < 0.03f) return;
        lastPlayed[key] = now;

        AudioSource s = pool[next];
        next = (next + 1) % pool.Length;
        s.Stop();
        s.clip = c;
        s.pitch = pitch;
        s.volume = Mathf.Clamp01(volume * volCache);
        s.Play();
    }

    void BlipInternal(string speaker, string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length) return;
        char c = text[index];
        if (!char.IsLetter(c) || index % 3 != 0) return;
        float vv = GameAudio.VoiceLevel;                   // бормотание = голос персонажа, регулируется ползунком «Голоса»
        if (!GameAudio.Babble || vv <= 0.001f) return;

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
        float vv = GameAudio.VoiceLevel;
        if (vv <= 0.001f) return;
        AudioClip clip = Get("blip:Лео");
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
                int pick = UnityEngine.Random.Range(0, arr.Length), prev;
                if (arr.Length > 1 && lastVariant.TryGetValue(key, out prev) && pick == prev) pick = (pick + 1) % arr.Length;
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
