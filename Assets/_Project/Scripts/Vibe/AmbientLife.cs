// Assets/_Project/Scripts/Vibe/AmbientLife.cs (НЕ в Editor)
// Остров дышит (GDD 7.4, 11.4): тёплые светлячки-пылинки в воздухе, редкие листья с крон, тихий ветер и птицы.
// Частицы следуют за игроком (их немного, лимит GDD: до 200 на экране). Ветер и птицы синтезируются кодом.
// «Уменьшить эффекты» (VibeSettings.ReduceEffects) режет частицы втрое.
// Время суток (DayNightCycle.Night01, работает и без объекта в сцене): днём поют птицы, ночью их нет, зато звучат сверчки
// (свои клипы: Resources/Sfx/crickets/, иначе синтез) и светлячков в воздухе заметно больше. Всё меняется плавно, без щелчков.
using UnityEngine;

public class AmbientLife : MonoBehaviour
{
    [Header("Частицы")]
    public bool motes = true;
    public bool leaves = true;
    public float moteRate = 7f;
    public float leafRate = 1.6f;

    [Header("Звук")]
    public bool wind = false;   // ветер выключен: постоянный шум мешал. Включить: true (громкость: windLevel и ползунок «Природа»)
    public bool birds = true;
    [Range(0f, 1f)] public float windLevel = 0.35f;
    [Range(0f, 1f)] public float birdLevel = 0.5f;

    [Header("Время суток")]
    [Tooltip("Во сколько раз больше светлячков в глубокую ночь")]
    public float moteNightMul = 2.2f;
    public bool crickets = true;
    [Range(0f, 1f)] public float cricketLevel = 0.4f;

    ParticleSystem moteFx, leafFx;
    AudioSource windSrc, birdSrc, cricketSrc;
    AudioClip[] chirps;
    Transform player;
    float findAt, birdAt, rateAt;
    bool reduced;
    float cricketVol;   // текущая громкость сверчков (плавно догоняет цель)

    void Start()
    {
        if (motes) moteFx = MakeMotes();
        if (leaves) leafFx = MakeLeaves();
        ApplyRates();

        if (wind)
        {
            windSrc = NewSource();
            var wf = Resources.LoadAll<AudioClip>("Sfx/wind");          // свой луп ветра: Resources/Sfx/wind/
            windSrc.clip = wf.Length > 0 ? wf[0] : BuildWind();
            windSrc.loop = true;
            windSrc.volume = 0f;
            windSrc.Play();
        }
        if (birds)
        {
            birdSrc = NewSource();
            chirps = Resources.LoadAll<AudioClip>("Sfx/birds");         // свои птицы: Resources/Sfx/birds/ (каждый файл = один крик)
            if (chirps.Length == 0) chirps = new[] { BuildChirp(1), BuildChirp(2), BuildChirp(3), BuildChirp(4) };
            birdAt = Time.time + Random.Range(3f, 7f);
        }
        if (crickets)
        {
            cricketSrc = NewSource();
            var cf = Resources.LoadAll<AudioClip>("Sfx/crickets");      // свой луп сверчков: Resources/Sfx/crickets/
            cricketSrc.clip = cf.Length > 0 ? cf[0] : BuildCrickets();
            cricketSrc.loop = true;
            cricketSrc.volume = 0f;
        }
    }

    AudioSource NewSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    void Update()
    {
        if (player == null && Time.unscaledTime >= findAt)
        {
            findAt = Time.unscaledTime + 1f;
            var pm = PlayerMovement.Instance;
            if (pm != null) player = pm.transform;
        }

        if (player != null)
        {
            Vector3 p = player.position;
            if (moteFx != null) moteFx.transform.position = p + Vector3.up * 1.2f;
            if (leafFx != null) leafFx.transform.position = p;
        }

        if (Time.unscaledTime >= rateAt)
        {
            rateAt = Time.unscaledTime + 1f;
            ApplyRates();   // раз в секунду: «меньше эффектов» и ночное число светлячков
        }

        float amb = GameAudio.AmbientLevel;     // уже с учётом «Звук ВЫКЛ» и общей громкости
        bool talking = DialogueManager.IsBusy;     // во время реплик природа тише
        float k = talking ? 0.5f : 1f;

        if (windSrc != null)
        {
            // порывы: громкость плавно гуляет от почти тишины до полной, а не гудит ровно (Perlin по реальному времени)
            float gust = Mathf.Lerp(0.08f, 1f, Mathf.PerlinNoise(Time.unscaledTime * 0.06f, 3.1f));
            float wt = amb * windLevel * 0.55f * gust * k;
            // тише или ВЫКЛ: сразу, без медленного затухания (раньше «выключенный» ветер шумел ещё секунду-две)
            windSrc.volume = wt < windSrc.volume ? wt : Mathf.MoveTowards(windSrc.volume, wt, Time.unscaledDeltaTime * 0.3f);
        }

        // птицы поют днём: к закату реже и тише, ночью молчат; сверчки наоборот
        float night = DayNightCycle.Night01;
        float dayK = Mathf.Clamp01(1f - night * 1.6f);          // 1 день ... 0 уже в начале ночи
        float nightK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.9f, night));

        if (birdSrc != null && birdSrc.isPlaying)
            birdSrc.volume = amb * birdLevel * k * dayK;      // ползунок действует и на уже поющую птицу

        if (birdSrc != null && chirps != null && Time.time >= birdAt)
        {
            if (dayK < 0.2f) birdAt = Time.time + 3f;           // ночью птиц нет: проверим позже
            else
            {
                birdAt = Time.time + Random.Range(5f, 12f) / Mathf.Max(0.4f, dayK);   // вечером реже
                birdSrc.Stop();
                birdSrc.clip = chirps[Random.Range(0, chirps.Length)];
                birdSrc.panStereo = Random.Range(-0.7f, 0.7f);
                birdSrc.pitch = Random.Range(0.9f, 1.15f);
                birdSrc.volume = amb * birdLevel * k * dayK;
                birdSrc.Play();
            }
        }

        if (cricketSrc != null)
        {
            float target = amb * cricketLevel * nightK * (talking ? 0.5f : 1f);
            // тише: сразу (ползунок, «Звук ВЫКЛ»); громче: плавно, чтобы сверчки «вступали» на закате
            cricketVol = target < cricketVol ? target : Mathf.MoveTowards(cricketVol, target, Time.unscaledDeltaTime * 0.15f);
            cricketSrc.volume = cricketVol;
            if (cricketVol > 0.002f) { if (!cricketSrc.isPlaying) cricketSrc.Play(); }
            else if (cricketSrc.isPlaying) cricketSrc.Pause();
        }
    }

    void ApplyRates()
    {
        reduced = VibeSettings.ReduceEffects;
        float m = reduced ? 0.3f : 1f;
        float night = DayNightCycle.Night01;
        if (moteFx != null)
        {
            var e = moteFx.emission;
            e.rateOverTime = moteRate * m * Mathf.Lerp(1f, Mathf.Max(1f, moteNightMul), night);   // ночью светлячков больше
            var mm = moteFx.main;
            mm.startSizeMultiplier = Mathf.Lerp(1f, 1.3f, night);                                   // и они крупнее
        }
        if (leafFx != null) { var e = leafFx.emission; e.rateOverTime = leafRate * m; }
    }

    // ------------------------------------------------------------ частицы
    ParticleSystem NewSystem(string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // иначе нельзя менять duration
        var r = go.GetComponent<ParticleSystemRenderer>();
        Material pm = VibeGfx.ParticleMat(sprite);
        if (pm != null) r.sharedMaterial = pm; else r.enabled = false;   // без шейдера частицы просто не рисуются
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static Gradient FadeInOut(float inAt, float outAt)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, outAt), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // светлячки: тёплые мягкие точки плывут в воздухе и мерцают при появлении
    ParticleSystem MakeMotes()
    {
        var ps = NewSystem("Motes", VibeGfx.Soft);
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.prewarm = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 220;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f, 0.9f), new Color(1f, 0.82f, 0.4f, 0.7f));

        var em = ps.emission;
        em.rateOverTime = moteRate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(36f, 4f, 36f);

        var nz = ps.noise;
        nz.enabled = true;
        nz.strength = 0.5f;
        nz.frequency = 0.25f;
        nz.scrollSpeed = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeInOut(0.15f, 0.8f);

        ps.Play();
        return ps;
    }

    // листья: падают сверху медленно и качаются, их видно на фоне земли
    ParticleSystem MakeLeaves()
    {
        var ps = NewSystem("Leaves", VibeGfx.Leaf);
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.prewarm = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.06f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.72f, 0.25f, 1f), new Color(0.88f, 0.7f, 0.25f, 1f));

        var em = ps.emission;
        em.rateOverTime = leafRate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.position = new Vector3(0f, 9f, 0f);
        sh.scale = new Vector3(30f, 1f, 30f);

        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = 0.9f;
        lim.dampen = 0.4f;

        var nz = ps.noise;
        nz.enabled = true;
        nz.strength = 1.2f;
        nz.frequency = 0.18f;
        nz.scrollSpeed = 0.15f;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeInOut(0.1f, 0.85f);

        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------ звук
    // ветер: фильтрованный шум, громкость плавает; края склеены, петля без щелчка
    static AudioClip BuildWind()
    {
        const float len = 6f;
        const float fade = 0.5f;
        int n = (int)(len * VibeSynth.Rate);
        int f = (int)(fade * VibeSynth.Rate);
        var raw = new float[n + f];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            lp += 0.02f * (VibeSynth.Noise() - lp);
            lp2 += 0.2f * (lp - lp2);
            raw[i] = lp2;
        }
        var d = new float[n];
        float max = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float v = raw[i];
            if (i < f) { float k = i / (float)f; v = raw[i] * k + raw[n + i] * (1f - k); }
            float t = i / (float)VibeSynth.Rate;
            float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * t / len) + 0.15f * Mathf.Sin(4f * Mathf.PI * t / len + 1.3f);   // целые циклы: стык ровный
            d[i] = v * swell;
            float a = Mathf.Abs(d[i]);
            if (a > max) max = a;
        }
        float g = 0.85f / max;
        for (int i = 0; i < n; i++) d[i] *= g;
        var clip = AudioClip.Create("wind", n, 1, VibeSynth.Rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    // сверчки: два «стрекотальщика» с разной высотой и периодом (0.5 и 0.4 с), клип 2 с = целое число периодов обоих, петля без щелчка
    static AudioClip BuildCrickets()
    {
        return VibeSynth.Make("crickets", 2f, t =>
        {
            float s = 0f;
            s += CricketVoice(t, 0.5f, 0.00f, 4300f, 3);
            s += CricketVoice(t, 0.4f, 0.17f, 3900f, 4) * 0.8f;
            return s;
        }, 0.5f);
    }

    // одна «трель» из нескольких коротких импульсов каждые period секунд
    static float CricketVoice(float t, float period, float offset, float freq, int pulses)
    {
        float p = Mathf.Repeat(t - offset, period);
        const float gap = 0.032f, len = 0.022f;
        float s = 0f;
        for (int k = 0; k < pulses; k++)
        {
            float tt = p - k * gap;
            if (tt < 0f || tt > len) continue;
            s += Mathf.Sin(2f * Mathf.PI * freq * tt) * Mathf.Sin(Mathf.PI * tt / len);
        }
        return s;
    }

    // птичья трель: 2-5 слогов со скольжением тона и лёгким вибрато
    static AudioClip BuildChirp(int variant)
    {
        var r = new System.Random(variant * 7919);
        int syl = 2 + r.Next(0, 4);
        float f0 = 2400f + r.Next(0, 1200);
        float sweep = (r.Next(0, 2) == 0 ? 1f : -1f) * (500f + r.Next(0, 900));
        float gap = 0.085f + (float)r.NextDouble() * 0.04f;
        const float sylLen = 0.07f;
        float total = syl * gap + 0.12f;

        return VibeSynth.Make("chirp" + variant, total, t =>
        {
            float s = 0f;
            for (int k = 0; k < syl; k++)
            {
                float tt = t - k * gap;
                if (tt < 0f || tt > sylLen) continue;
                float kk = tt / sylLen;
                float ph = 2f * Mathf.PI * (f0 * tt + 0.5f * sweep * tt * tt / sylLen + 14f * Mathf.Sin(2f * Mathf.PI * 30f * tt) / (2f * Mathf.PI * 30f) * 6f);
                s += Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * kk);
            }
            return s;
        }, 0.6f);
    }
}
