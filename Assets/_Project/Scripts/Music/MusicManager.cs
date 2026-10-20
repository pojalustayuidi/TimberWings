// Assets/_Project/Scripts/Music/MusicManager.cs (НЕ в Editor)
// Фоновая музыка: плейлист с плавным переходом между треками.
//   - Создаётся сам, ничего вешать в сцену не нужно. Если в сцене уже есть MusicManager с треками в инспекторе, берутся они.
//   - Треков в инспекторе нет: играет всё из Resources/Music.
//   - Переживает перезагрузку сцены («Начать заново»), второй экземпляр из новой сцены сам удаляется (раньше музыка накладывалась).
//   - Громкость = volume (инспектор) * GameAudio.MusicLevel (настройки). Меняется на лету, переход треков работает и на паузе.
//   - Во время реплик музыка чуть тише, чтобы было слышно голос.
//   - Время суток: на закате и ночью играют спокойные «вечерние» треки, утром и днём обычные. Смена плавная (moodFadeTime).
//     Вечерние треки: поле Evening Tracks, либо папка Resources/Music/Evening, либо треки с evening / night / dusk / вечер / ночь / сумер в названии.
//     Если вечерних или обычных треков нет вовсе, играет единый плейлист, как раньше.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Треки (пусто = всё из Resources/Music)")]
    public AudioClip[] tracks;

    [Header("Настройки")]
    [Range(0.5f, 5f)] public float fadeTime = 2f;
    [Range(0f, 1f)] public float volume = 0.6f;
    public bool shuffle = true;

    [Header("Вечер и ночь")]
    [Tooltip("Спокойные треки для заката и ночи. Пусто: Resources/Music/Evening и треки с evening / night / dusk / вечер / ночь / сумер в названии")]
    public AudioClip[] eveningTracks;
    [Range(1f, 8f)] public float moodFadeTime = 4f;     // плавный переход между дневной и вечерней музыкой, секунд
    [Range(0f, 1f)] public float duckDuringDialogue = 0.55f;

    [Header("Главное меню")]
    [Tooltip("Трек меню (зацикливается). Пусто: Resources/MenuMusic/MainMenu; нет и его: в меню тишина, игровые треки там не играют")]
    public AudioClip menuTrack;
    bool menuMode;                          // сейчас открыта сцена главного меню

    readonly AudioSource[] src = new AudioSource[2];
    readonly float[] w = new float[2];      // доля громкости каждого источника во время перехода
    int cur = -1;                           // какой источник играет сейчас
    int pos = -1;                           // позиция в order
    List<int> order;
    bool fading, warned;
    float duck = 1f;

    bool[] isEvening;                  // для каждого трека: вечерний ли
    bool hasEvening, hasDay;           // есть ли треки обоих настроений (иначе плейлист один)
    bool eveningMood;                  // какой плейлист играет сейчас
    int curTrack = -1;
    float moodAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var existing = FindFirstObjectByType<MusicManager>();
        if (existing != null) return;                       // сам проснётся через Awake
        var go = new GameObject("Music");
        go.AddComponent<MusicManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // дубликат из перезагруженной сцены; настройки из инспектора (треки, громкость) переходят к живому менеджеру
            Instance.Adopt(this);
            if (GetComponents<Component>().Length <= 2) Destroy(gameObject); else Destroy(this);
            return;
        }
        Instance = this;
        if (transform.parent != null) transform.SetParent(null);   // DontDestroyOnLoad работает только для корневых объектов
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < 2; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            src[i] = s;
        }
        if (tracks == null || tracks.Length == 0) tracks = Resources.LoadAll<AudioClip>("Music");
        ClassifyTracks();
    }

    void OnEnable() { GameEvents.TimeOfDayChanged += OnPhase; SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { GameEvents.TimeOfDayChanged -= OnPhase; SceneManager.sceneLoaded -= OnSceneLoaded; }

    // Менеджер создаётся уже в меню (без настроек из игровой сцены). Когда игровая сцена принесла свой экземпляр с треками, берём его настройки.
    void Adopt(MusicManager o)
    {
        if (o == null || o == this) return;
        if (o.tracks != null && o.tracks.Length > 0) tracks = o.tracks;
        if (o.eveningTracks != null && o.eveningTracks.Length > 0) eveningTracks = o.eveningTracks;
        if (o.menuTrack != null) menuTrack = o.menuTrack;
        fadeTime = o.fadeTime; volume = o.volume; shuffle = o.shuffle;
        moodFadeTime = o.moodFadeTime; duckDuringDialogue = o.duckDuringDialogue;
        ClassifyTracks();
    }

    // Заставка / ролик поверх меню: музыка меню плавно уходит (в игру её не возвращаем, дальше сработает LeaveMenuMode)
    public static void SilenceForCutscene(float fade = 0.8f)
    {
        var m = Instance;
        if (m == null) return;
        m.StopAllCoroutines(); m.fading = false;
        m.StartCoroutine(m.CrossfadeTo(null, false, fade));
    }

    // Источники молчат по плану (cur < 0), но после прерванного перехода один мог остаться играть: гасим
    void HardStopIfSilent()
    {
        if (cur >= 0) return;
        for (int i = 0; i < 2; i++) { if (src[i] != null) src[i].Stop(); w[i] = 0f; }
    }

    static bool IsMenuScene(Scene s) { return s.buildIndex == MenuFlow.MainMenuScene; }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        if (Instance != this || mode != LoadSceneMode.Single) return;
        if (IsMenuScene(s)) EnterMenuMode();
        else if (menuMode) LeaveMenuMode();
    }

    void EnterMenuMode()
    {
        menuMode = true;
        order = null; pos = -1; curTrack = -1;          // Update не листает игровые треки
        var clip = menuTrack != null ? menuTrack : Resources.Load<AudioClip>("MenuMusic/MainMenu");
        StopAllCoroutines(); fading = false;
        HardStopIfSilent();
        StartCoroutine(CrossfadeTo(clip, true, 1.2f));   // нет клипа: игровая музыка просто затихает
    }

    // из меню в игру: игровой трек плавно проявляется поверх уходящего трека меню
    void LeaveMenuMode()
    {
        menuMode = false;
        StopAllCoroutines(); fading = false;
        HardStopIfSilent();
        BeginPlayback();
    }
    void OnPhase(DayPhase p) { moodAt = 0f; }   // сменилась фаза: проверить настроение сразу, не ждать опроса

    // раскладываем треки на дневные и вечерние (вечерние, которых нет в общем списке, добавляем)
    void ClassifyTracks()
    {
        if (tracks == null) tracks = new AudioClip[0];
        var ev = (eveningTracks != null && eveningTracks.Length > 0) ? eveningTracks : Resources.LoadAll<AudioClip>("Music/Evening");
        var list = new List<AudioClip>(tracks);
        foreach (var c in ev) if (c != null && !list.Contains(c)) list.Add(c);
        tracks = list.ToArray();

        isEvening = new bool[tracks.Length];
        hasEvening = hasDay = false;
        for (int i = 0; i < tracks.Length; i++)
        {
            var c = tracks[i];
            if (c == null) continue;
            bool e = System.Array.IndexOf(ev, c) >= 0 || NameLooksEvening(c.name);
            isEvening[i] = e;
            if (e) hasEvening = true; else hasDay = true;
        }
    }

    static bool NameLooksEvening(string n)
    {
        n = (n ?? "").ToLowerInvariant();
        return n.Contains("evening") || n.Contains("night") || n.Contains("dusk") || n.Contains("вечер") || n.Contains("ноч") || n.Contains("сумер");
    }

    static bool EveningNow()
    {
        DayPhase p = DayNightCycle.Phase;
        return p == DayPhase.Dusk || p == DayPhase.Night;
    }

    List<int> BuildOrder(bool evening)
    {
        var o = new List<int>();
        bool split = hasEvening && hasDay;
        for (int i = 0; i < tracks.Length; i++)
        {
            if (tracks[i] == null) continue;
            if (!split || isEvening[i] == evening) o.Add(i);
        }
        if (shuffle) Shuffle(o);
        return o;
    }

    // сменилась фаза суток: если играет трек не того настроения, плавно переходим на подходящий
    void CheckMood()
    {
        if (!(hasEvening && hasDay)) return;
        if (DayNightCycle.Warping || CampSleep.Sleeping) return;   // во время сна время мелькает: решим после
        bool want = EveningNow();
        if (want == eveningMood) return;

        eveningMood = want;
        order = BuildOrder(want);
        if (order.Count == 0) return;
        pos = 0;
        if (curTrack >= 0 && curTrack < isEvening.Length && isEvening[curTrack] == want)
        {
            pos = Mathf.Max(0, order.IndexOf(curTrack));   // текущий трек уже подходит
            return;
        }
        StartCoroutine(Crossfade(order[0], moodFadeTime));
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (Instance != this) return;
        if (IsMenuScene(SceneManager.GetActiveScene())) { EnterMenuMode(); return; }
        BeginPlayback();
    }

    void BeginPlayback()
    {
        if (tracks == null || tracks.Length == 0)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[MusicManager] Нет треков: перетащи mp3 в поле Tracks или положи их в Assets/_Project/Resources/Music.");
            }
            return;
        }
        eveningMood = EveningNow();
        order = BuildOrder(eveningMood);
        if (order.Count == 0) return;
        pos = 0;
        StartCoroutine(Crossfade(order[0]));
    }

    void Update()
    {
        if (Instance != this) return;     // дубликат дожидается удаления, источников у него нет
        float target = DialogueManager.IsBusy ? duckDuringDialogue : 1f;
        duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime * 1.5f);
        float level = volume * GameAudio.MusicLevel * duck;
        for (int i = 0; i < 2; i++) src[i].volume = w[i] * level;

        if (cur < 0 || fading || order == null || order.Count == 0) return;

        if (Time.unscaledTime >= moodAt)
        {
            moodAt = Time.unscaledTime + 0.5f;
            CheckMood();
            if (fading) return;
        }

        var s = src[cur];
        var clip = s.clip;
        if (clip == null) return;

        if (!s.isPlaying)
        {
            // источник остановили извне (смена аудиоустройства, возврат в браузерную вкладку): продолжаем, а не молчим
            s.Play();
            return;
        }

        float lead = Mathf.Min(fadeTime, clip.length * 0.5f);
        if (s.time >= clip.length - lead) PlayNext();
    }

    public void PlayNext()
    {
        if (fading || order == null || order.Count == 0) return;
        pos = (pos + 1) % order.Count;
        StartCoroutine(Crossfade(order[pos]));
    }

    IEnumerator Crossfade(int trackIndex, float fadeOverride = -1f)
    {
        fading = true;
        curTrack = trackIndex;
        yield return CrossfadeTo(tracks[trackIndex], false, fadeOverride);
    }

    // clip == null: только затихаем и останавливаемся (тишина)
    IEnumerator CrossfadeTo(AudioClip clip, bool loop, float fadeOverride)
    {
        fading = true;
        int from = cur;
        int to = from == 0 ? 1 : 0;

        var s = src[to];
        s.Stop();
        s.clip = clip;
        s.loop = loop;
        w[to] = 0f;
        s.volume = 0f;
        if (clip != null) s.Play();
        cur = clip != null ? to : -1;

        float dur = from < 0 ? 1.5f : (fadeOverride > 0f ? fadeOverride : fadeTime);
        float wFrom = from < 0 ? 0f : w[from];
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)     // unscaled: на паузе (timeScale 0) переход не зависает
        {
            float k = t / dur;
            if (from >= 0) w[from] = wFrom * (1f - k);
            if (clip != null) w[to] = k;
            yield return null;
        }

        if (from >= 0) { src[from].Stop(); w[from] = 0f; }
        w[to] = clip != null ? 1f : 0f;
        fading = false;
    }

    static void Shuffle(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            int tmp = list[i]; list[i] = list[r]; list[r] = tmp;
        }
    }
}
