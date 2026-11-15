// Assets/_Project/Scripts/Vibe/AmbientLife.cs (НЕ в Editor)
// Остров дышит: листья рождаются под кронами реальных деревьев; ночные светлячки остаются по правилам 3.3.2.
// Visual Polish 3.3.3: один существующий ParticleSystem Leaves, без новых эффектов/эмиттеров в сцене.
// «Уменьшить эффекты» (VibeSettings.ReduceEffects) дополнительно сокращает число частиц.
// Время суток (DayNightCycle.Night01, работает и без объекта в сцене): днём поют птицы, ночью их нет, зато звучат сверчки
// (свои клипы: Resources/Sfx/crickets/, иначе синтез). Светлячки вне кедровника и днём отсутствуют.
using UnityEngine;

public class AmbientLife : MonoBehaviour
{
    [Header("Частицы")]
    public bool motes = true;
    public bool leaves = true;
    [Tooltip("Наследуемая интенсивность. Visual Polish 3.3.2 ограничивает её настройкой maxMysticMotesPerSecond.")]
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

    [Header("Visual Polish 3.3 — листья и светлячки")]
    [Tooltip("Редкие мягкие порывы ветра в существующей системе листьев")]
    public bool softLeafGusts = true;
    [Range(0f, 0.5f)] public float leafGustStrength = 0.28f;
    [Tooltip("Учитывать настройку «Уменьшить эффекты» при ночном свечении")]
    public bool scaleNightMotesWithQuality = true;

    [Header("Visual Polish 3.3.2 — локальные ночные светлячки")]
    [Tooltip("Светлячки существуют только в зоне ForestBiome.Cedar (мистический кедровник)")]
    public bool mysticOnlyMotes = true;
    [Range(0.3f, 0.95f)] public float mysticMotesNightStart = 0.58f;
    [Tooltip("Жёсткий верхний предел эмиссии, даже если в сцене сохранился старый moteRate = 7")]
    [Range(0f, 1.5f)] public float maxMysticMotesPerSecond = 0.7f;

    [Header("Visual Polish 3.3.3 — листья под кронами")]
    [Tooltip("Листья рождаются возле листвы настоящих деревьев, а не в прямоугольнике вокруг игрока.")]
    public bool leavesUnderCanopies = true;
    [Tooltip("Максимальное расстояние от игрока до дерева, с которого могут падать листья")]
    [Range(4f, 25f)] public float leafCanopySearchRadius = 12f;
    [Tooltip("Не создавать листья непосредственно над лесными дорожками и полянами")]
    [Range(0f, 2f)] public float leafPathClearance = 0.65f;

    ParticleSystem moteFx, leafFx;
    float canopyLeafRate, canopyLeafCarry;
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
            if (leafFx != null) leafFx.transform.position = p; // Только резервный штатный box, если leavesUnderCanopies отключён
        }

        if (Time.unscaledTime >= rateAt)
        {
            rateAt = Time.unscaledTime + 0.35f;
            ApplyRates();   // раз в 0.35 с: сразу убираем светлячков при выходе из кедровника
        }

        EmitCanopyLeaves(); // Та же система Leaves, но позиция каждой частицы берётся из кроны.

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
            var emission = moteFx.emission;
            ForestZone zone = player != null ? ForestZone.At(player.position) : null;
            bool inMysticForest = !mysticOnlyMotes || (zone != null && zone.biome == ForestBiome.Cedar);
            float start = Mathf.Clamp(mysticMotesNightStart, 0.3f, 0.95f);
            float nightFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, Mathf.Min(1f, start + 0.32f), night));
            bool visible = player != null && inMysticForest && nightFade > 0.01f && maxMysticMotesPerSecond > 0f;

            if (!visible)
            {
                emission.rateOverTime = 0f;
                // World-space частицы иначе остаются висеть на дороге после выхода из зоны.
                if (moteFx.isPlaying || moteFx.particleCount > 0)
                    moteFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            else
            {
                float legacyRate = Mathf.Max(0f, moteRate) * Mathf.Lerp(1f, Mathf.Max(1f, moteNightMul), night) * 0.1f;
                emission.rateOverTime = Mathf.Min(legacyRate, maxMysticMotesPerSecond) * nightFade * m;
                if (!moteFx.isPlaying) moteFx.Play();
            }
        }
        if (leafFx != null)
        {
            float gust = softLeafGusts ? Mathf.PerlinNoise(Time.time * 0.065f, 7.71f) : 0.5f;
            float sway = (gust * 2f - 1f) * leafGustStrength;
            var e = leafFx.emission;
            // Режим 3.3.3: встроенный Box не спавнит листья над пустыми полями.
            // Позиции задаёт EmitParams существующей ParticleSystem в EmitCanopyLeaves().
            canopyLeafRate = Mathf.Max(0f, leafRate * m * (1f + sway));
            e.rateOverTime = leavesUnderCanopies ? 0f : canopyLeafRate;
            if (!leavesUnderCanopies) canopyLeafCarry = 0f;
            if (softLeafGusts)
            {
                // Уже существующие листья плавно дрейфуют; никаких дополнительных частиц.
                var v = leafFx.velocityOverLifetime;
                v.enabled = true;
                v.space = ParticleSystemSimulationSpace.World;
                // Unity требует одинаковый режим всех трех кривых Velocity over Lifetime.
                // Используем Constant для X/Y/Z; плавность порывов дает sway, шум дает Noise.
                // Ранее X/Z были TwoConstants, а Y оставалась Constant, из-за чего Unity ругалась.
                v.x = new ParticleSystem.MinMaxCurve(0.11f + sway * 0.375f);
                v.y = new ParticleSystem.MinMaxCurve(0f);
                v.z = new ParticleSystem.MinMaxCurve(0.02f + sway * 0.15f);
            }
        }
    }

    // Никакой отдельной системы листьев: вручную задаём точку рождения в УЖЕ существующем Leaves.
    // Если подходящей листвы рядом нет, частиц нет (а не fallback в пустой воздух).
    void EmitCanopyLeaves()
    {
        if (!leavesUnderCanopies || leafFx == null || player == null || canopyLeafRate <= 0f)
        {
            canopyLeafCarry = 0f;
            return;
        }
        canopyLeafCarry = Mathf.Min(2f, canopyLeafCarry + canopyLeafRate * Time.deltaTime);
        if (canopyLeafCarry < 1f) return;
        if (leafFx.particleCount >= 38) { canopyLeafCarry = 0f; return; }

        Vector3 spawn;
        if (!LeafCanopyHabitat333.TryPick(player.position, leafCanopySearchRadius, leafPathClearance, out spawn))
        {
            canopyLeafCarry = 0f;
            return;
        }
        int count = Mathf.Min(Mathf.FloorToInt(canopyLeafCarry), 2);
        canopyLeafCarry -= count;
        if (!leafFx.isPlaying) leafFx.Play();
        for (int i = 0; i < count; i++)
        {
            var p = new ParticleSystem.EmitParams();
            p.position = spawn + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(-0.07f, 0.07f), Random.Range(-0.18f, 0.18f));
            p.velocity = new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.22f, -0.06f), Random.Range(-0.12f, 0.12f));
            leafFx.Emit(p, 1);
        }
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
        main.prewarm = false;    // не создавать сразу большое облако при входе в сцену
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 10;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.050f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.65f, 0.35f), new Color(0.98f, 0.87f, 0.48f, 0.22f));

        var em = ps.emission;
        em.rateOverTime = 0f;    // включается после проверки зоны и времени суток

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(5.5f, 1.5f, 5.5f);

        var nz = ps.noise;
        nz.enabled = true;
        nz.strength = 0.5f;
        nz.frequency = 0.25f;
        nz.scrollSpeed = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeInOut(0.15f, 0.8f);

        // Play() вызывается только в ApplyRates, когда игрок в кедровнике ночью.
        return ps;
    }

    // листья: падают сверху медленно и качаются, их видно на фоне земли
    ParticleSystem MakeLeaves()
    {
        var ps = NewSystem("Leaves", VibeGfx.Leaf);
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.prewarm = false; // Нельзя заранее заполнить открытое небо листьями до поиска деревьев
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
        em.rateOverTime = leavesUnderCanopies ? 0f : leafRate;

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
        nz.strength = 0.42f; // Мягкое покачивание, без хаотического «листопада из воздуха»
        nz.frequency = 0.18f;
        nz.scrollSpeed = 0.10f;

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

// Visual Polish 3.3.3. Выбор мест рождения для СУЩЕСТВУЮЩИХ AmbientLife.Leaves.
// Деревья из штатных ForestTrees и TreeSpeciesInfo; дорожки из ForestTrails.
// Это только кеш геометрии, не отдельная система частиц/новые объекты Unity.
internal static class LeafCanopyHabitat333
{
    struct Crown
    {
        public Transform tree;
        public Renderer foliage;
        public Vector3 trunk;
        public float height;
        public float radius;
    }

    struct TrailTriangle
    {
        public Vector2 a, b, c;
        public float minX, maxX, minZ, maxZ;
    }

    static readonly System.Collections.Generic.List<Crown> crowns = new System.Collections.Generic.List<Crown>();
    static readonly System.Collections.Generic.List<TrailTriangle> trails = new System.Collections.Generic.List<TrailTriangle>();
    static readonly System.Collections.Generic.List<int> nearby = new System.Collections.Generic.List<int>();
    static readonly System.Collections.Generic.HashSet<int> seen = new System.Collections.Generic.HashSet<int>();
    static bool cached;
    static float refreshAt;
    static int selected = -1;
    static float selectUntil;
    static Transform sawmill, findSpot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        cached = false;
        crowns.Clear(); trails.Clear(); nearby.Clear(); seen.Clear();
        refreshAt = 0f; selected = -1; selectUntil = 0f;
        sawmill = null; findSpot = null;
    }

    static void ScanScene()
    {
        if (cached && Time.unscaledTime < refreshAt) return;
        cached = true;
        crowns.Clear(); trails.Clear(); seen.Clear();
        selected = -1;

        GameObject forest = GameObject.Find("ForestTrees");
        if (forest != null)
            foreach (Transform group in forest.transform)
                foreach (Transform tree in group)
                    AddTree(tree);

        // Сюжетные и ресурсные деревья могут быть вне ForestTrees.
        var species = UnityEngine.Object.FindObjectsByType<TreeSpeciesInfo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < species.Length; i++)
            if (species[i] != null) AddTree(species[i].transform);

        // Ленты дорожек не обязаны быть видимыми: MeshFilter остаётся на ground_pathOpen_*.
        GameObject paths = GameObject.Find("ForestTrails");
        if (paths != null)
        {
            var meshes = paths.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                MeshFilter mf = meshes[i];
                if (mf == null || !mf.name.StartsWith("ground_pathOpen_", System.StringComparison.OrdinalIgnoreCase)) continue;
                Mesh mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Vector3[] v = mesh.vertices;
                int[] tri = mesh.triangles;
                for (int j = 0; j + 2 < tri.Length; j += 3)
                {
                    Vector3 wa = mf.transform.TransformPoint(v[tri[j]]);
                    Vector3 wb = mf.transform.TransformPoint(v[tri[j + 1]]);
                    Vector3 wc = mf.transform.TransformPoint(v[tri[j + 2]]);
                    Vector2 a = new Vector2(wa.x, wa.z), b = new Vector2(wb.x, wb.z), c = new Vector2(wc.x, wc.z);
                    if (Mathf.Abs(Cross(b - a, c - a)) < 0.0001f) continue;
                    trails.Add(new TrailTriangle {
                        a = a, b = b, c = c,
                        minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)),
                        minZ = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), maxZ = Mathf.Max(a.y, Mathf.Max(b.y, c.y))
                    });
                }
            }
        }
        GameObject sm = GameObject.Find("ForestSawmill"), fp = GameObject.Find("ForestFindSpot");
        sawmill = sm != null ? sm.transform : null;
        findSpot = fp != null ? fp.transform : null;
        refreshAt = Time.unscaledTime + (crowns.Count == 0 ? 3f : 18f);
    }

    static bool HasFoliage(Material[] materials, string rendererName)
    {
        if (rendererName.IndexOf("leaf", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            rendererName.IndexOf("crown", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            rendererName.IndexOf("foliage", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        for (int i = 0; i < materials.Length; i++)
        {
            Material m = materials[i];
            if (m == null) continue;
            string n = m.name.ToLowerInvariant();
            if (n.Contains("leaf") || n.Contains("foliage") || n.Contains("needle") ||
                n.Contains("crown") || n.Contains("листв")) return true;
        }
        return false;
    }

    static void AddTree(Transform t)
    {
        if (t == null || !t.gameObject.activeInHierarchy || !seen.Add(t.GetInstanceID())) return;
        var rs = t.GetComponentsInChildren<MeshRenderer>(true);
        Renderer leaf = null, fallback = null;
        float bestLeaf = -1f, bestFallback = -1f;
        for (int i = 0; i < rs.Length; i++)
        {
            MeshRenderer r = rs[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("stump") || n.Contains("outline") || n.Contains("highlight") ||
                n.Contains("ground") || n.Contains("trunksolid")) continue;
            Bounds b = r.bounds;
            float h = b.max.y - t.position.y;
            if (h < 1.5f || h > 40f || b.extents.x > 18f || b.extents.z > 18f) continue;
            if (HasFoliage(r.sharedMaterials, r.name))
            {
                if (h > bestLeaf) { leaf = r; bestLeaf = h; }
            }
            else if (h > bestFallback) { fallback = r; bestFallback = h; }
        }
        // Материал может быть объединённым мешем (кора+листва), но голые стволы не подходят.
        if (leaf == null)
        {
            var info = t.GetComponent<TreeSpeciesInfo>();
            if (info != null && !string.IsNullOrEmpty(info.species))
            {
                string kind = info.species.ToLowerInvariant();
                if (kind.Contains("dead") || kind.Contains("stump") || kind.Contains("bare")) return;
            }
            if (fallback == null || fallback.bounds.extents.x < 0.65f || fallback.bounds.extents.z < 0.65f) return;
        }
        Renderer canopy = leaf != null ? leaf : fallback;
        if (canopy == null) return;
        Bounds bounds = canopy.bounds;
        float height = bounds.max.y - t.position.y;
        if (height < 1.5f) return;
        float radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.78f, 0.7f, 5.5f);
        crowns.Add(new Crown { tree = t, foliage = canopy, trunk = t.position, height = height, radius = radius });
    }

    static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
    static float SegmentDistSqr(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float f = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.00001f, ab.sqrMagnitude));
        return (p - (a + ab * f)).sqrMagnitude;
    }

    static bool NearMarker(Vector2 point, Transform marker, float minRadius)
    {
        if (marker == null) return false;
        float radius = Mathf.Max(minRadius, Mathf.Max(marker.lossyScale.x, marker.lossyScale.z) * 0.5f);
        Vector2 d = point - new Vector2(marker.position.x, marker.position.z);
        return d.sqrMagnitude < radius * radius;
    }

    static bool NearPath(Vector3 pos, float margin)
    {
        Vector2 p = new Vector2(pos.x, pos.z);
        if (NearMarker(p, sawmill, 3f) || NearMarker(p, findSpot, 2.5f)) return true;
        for (int i = 0; i < trails.Count; i++)
        {
            TrailTriangle t = trails[i];
            if (p.x < t.minX - margin || p.x > t.maxX + margin || p.y < t.minZ - margin || p.y > t.maxZ + margin) continue;
            float a = Cross(t.b - t.a, p - t.a);
            float b = Cross(t.c - t.b, p - t.b);
            float c = Cross(t.a - t.c, p - t.c);
            if ((a >= 0f && b >= 0f && c >= 0f) || (a <= 0f && b <= 0f && c <= 0f)) return true;
            float m2 = margin * margin;
            if (SegmentDistSqr(p, t.a, t.b) < m2 || SegmentDistSqr(p, t.b, t.c) < m2 || SegmentDistSqr(p, t.c, t.a) < m2) return true;
        }
        return false;
    }

    static bool Valid(Crown c, Vector3 player, float searchRadius)
    {
        if (c.tree == null || c.foliage == null || !c.tree.gameObject.activeInHierarchy ||
            !c.foliage.gameObject.activeInHierarchy || !c.foliage.enabled) return false;
        Vector3 b = c.tree.position;
        float dx = b.x - player.x, dz = b.z - player.z;
        return dx * dx + dz * dz <= searchRadius * searchRadius && Mathf.Abs(b.y - player.y) < 8f;
    }

    internal static bool TryPick(Vector3 player, float searchRadius, float pathClearance, out Vector3 spawn)
    {
        spawn = Vector3.zero;
        ScanScene();
        nearby.Clear();
        for (int i = 0; i < crowns.Count; i++)
            if (Valid(crowns[i], player, searchRadius)) nearby.Add(i);
        if (nearby.Count == 0) { selected = -1; return false; }

        // Несколько секунд листочки падают с одной кроны: локальный островок, не сплошной дождь.
        if (selected < 0 || selected >= crowns.Count || !Valid(crowns[selected], player, searchRadius) ||
            Time.unscaledTime > selectUntil)
        {
            selected = nearby[Random.Range(0, nearby.Count)];
            selectUntil = Time.unscaledTime + Random.Range(4f, 8f);
        }

        for (int attempt = 0; attempt < 18; attempt++)
        {
            Crown c = crowns[selected];
            float a = Random.Range(0f, Mathf.PI * 2f);
            float d = Mathf.Sqrt(Random.value) * c.radius;
            Vector3 pos = c.tree.position + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            if (NearPath(pos, Mathf.Max(0f, pathClearance)))
            {
                if (attempt == 8) selected = nearby[Random.Range(0, nearby.Count)];
                continue;
            }
            // Высота зависит от РЕАЛЬНОЙ модели: нижняя/средняя часть кроны, не фиксированные +9 м от игрока.
            pos.y = c.tree.position.y + c.height * Random.Range(0.55f, 0.82f);
            spawn = pos;
            return true;
        }
        return false;
    }
}
