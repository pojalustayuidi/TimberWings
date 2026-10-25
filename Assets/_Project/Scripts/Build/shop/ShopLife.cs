// Assets/_Project/Scripts/Npc/ShopLife.cs   (НЕ в папке Editor!)
// «Жизнь» лавки торговца в стиле Cloud Candy. Вешается на корень лавки (рядом с ShopStages), ссылки заполняет генератор
// (Tools > BuilderSim > Здания > Лавка торговца v2).
//
// Что делает:
//   • ПРОДАЖА (TradingStall.SaleMade): из прилавка вылетают монетки и летят к игроку, звенит колокольчик, качается вывеска,
//     весы «взвешивают», из облачной трубы и от прилавка выпрыгивают пушистые облачка, звон монет по нарастающей;
//   • ВИТРИНА РАСТЁТ: чем больше продано бревен, досок и заработано монет, тем полнее полки, поддоны и сундук (4 яруса на каждое).
//     Новый ярус «выпрыгивает» с пружинкой и облачком. Прогресс сохраняется (секция «shop», LoadOrder 65);
//   • ПРИВЕТСТВИЕ: подошёл к лавке (или открыл окно торговли) — звякнул колокольчик, вывеска качнулась, флажки дёрнул ветер;
//   • ЦЕННИК: меловая доска у лавки показывает живые цены (с «Торгом»), а когда готов «Базарный день» — зажигается звезда ×1,5;
//   • МЕЛОЧИ: блики на золоте, парящая золотая звезда на высшем ярусе, ленивые облачка из трубы.
//   • РАДУГА: за лавкой вырастает радуга с облачками на концах, когда открылся новый ярус витрины, прошла крупная продажа
//     или игрок продаёт серией (streakNeed продаж подряд с паузами не больше streakWindow секунд). Через rainbowLife секунд уходит.
//   • ПУХ: облачко-талисман на прилавке: качается, моргает, смотрит на игрока, машет при приветствии, прыгает при продаже.
//   • «+N» над прилавком: золотая цифра выручки всплывает и тает, в серии добавляется «x3».
// «Уменьшить эффекты» (VibeSettings.ReduceEffects): монеток и облачков втрое меньше, бликов нет.
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Shop Life")]
public class ShopLife : MonoBehaviour, ISaveable
{
    [Serializable]
    public class Tiered { public GameObject[] tiers = new GameObject[0]; }

    // ------------------------------------------------------------ ссылки (заполняет генератор)
    [Header("Ссылки (заполняет генератор)")]
    public ShopStages stages;
    [Tooltip("NPC-торговец с TradingStall. Пусто = найдётся сам (в том числе среди выключенных)")]
    public TradingStall stall;
    public Transform counterPoint;     // над прилавком: отсюда летят монеты
    public Transform greetPoint;       // перед прилавком: сюда подходит игрок
    public Transform coinProto;        // монета-образец (выключена)
    public Transform cloudPoint;       // труба-облакодел на крыше
    public Transform signKick;         // вывеска (дочерний пивот: тут «пинок», а ленивое покачивание делает HutLife)
    public Transform bellDome;         // колокольчик на прилавке
    public Transform scalesBeam, panL, panR;
    public float scalesArm = 0.26f, scalesChain = 0.27f, scalesBaseTilt = 4f;
    public Transform priceLogAnchor, pricePlankAnchor;
    public GameObject bazaarBadge;
    public Transform glintPoint;       // где блестит золото
    public Transform goldStar;         // парящая звезда высшего яруса монет
    public Mesh puffMesh;
    public Material puffMat;
    public HutLife hutLife;            // флажки и фонари: порыв ветра при продаже

    [Header("Радуга праздников (группа Stage3/Rainbow, выключена)")]
    public Transform rainbow;
    public Transform rainbowFootL, rainbowFootR;   // концы дуги: тут вылетают облачка
    [Header("Облачко-талисман «Пух»")]
    public Transform mascot;           // корень (поворот к игроку)
    public Transform mascotBody;       // тельце (качание, прыжок, пружинка)
    public Transform mascotEyes;       // общий пивот глаз (моргание)
    public Transform mascotArm;        // рука на шарнире (машет)

    // ------------------------------------------------------------ витрина
    [Header("Витрина: ярусы товара (ярус i виден, если продано не меньше порога i)")]
    public Tiered logTiers = new Tiered();
    public Tiered plankTiers = new Tiered();
    public Tiered coinTiers = new Tiered();
    public int[] logThresholds = { 0, 10, 35, 90 };
    public int[] plankThresholds = { 0, 6, 20, 50 };
    public int[] coinThresholds = { 0, 60, 250, 800 };

    [Header("Поведение")]
    public float greetRadius = 4.5f;
    public float greetCooldown = 30f;
    [Tooltip("От скольких монет за раз продажа считается «крупной» (больше монет и облачков)")]
    public int bigSale = 60;
    public bool ambientPuffs = true;
    [Tooltip("Сколько секунд радуга висит после праздника (новый ярус даёт на 2 секунды больше)")]
    public float rainbowLife = 7f;
    [Tooltip("Сколько продаж подряд считаются серией и зовут радугу")]
    public int streakNeed = 3;
    [Tooltip("Максимальная пауза между продажами серии, секунд")]
    public float streakWindow = 25f;
    public Color floatColor = new Color(1f, 0.86f, 0.30f, 1f);
    public float priceFontSize = 2.4f;
    public Color chalk = new Color(0.98f, 0.96f, 0.88f, 1f);
    public Color chalkBazaar = new Color(1f, 0.82f, 0.30f, 1f);

    // ------------------------------------------------------------ прогресс (сохраняется)
    int logsSold, planksSold, coinsEarned, sales;
    public int LogsSold { get { return logsSold; } }
    public int PlanksSold { get { return planksSold; } }
    public int CoinsEarned { get { return coinsEarned; } }
    public int Sales { get { return sales; } }

    // ------------------------------------------------------------ служебное
    readonly int[] shown = new int[3];

    // радуга
    Vector3 rbBase = Vector3.one;
    bool rbCaptured, rbUp;
    float rbK, rbHold, rbOver;
    int streak;
    float lastSaleT = -999f;
    static AudioClip rainbowClip;

    // «+N» над прилавком
    class FloatTxt { public TextMeshPro t; public float age = -1f, life, scale = 1f; public Vector3 vel; public Color col; }
    readonly List<FloatTxt> floatTxts = new List<FloatTxt>();

    // Пух
    Quaternion mascotBaseRot = Quaternion.identity, armBaseRot = Quaternion.identity;
    Vector3 mascotBodyPos, mascotEyesScale = Vector3.one;
    bool mascotCaptured;
    float hopT = -1f, hopH = 0.2f, waveT = -1f, nextBlink = 2f, blinkT = -1f;
    int hopsLeft;
    bool inside, started;
    float nextGreet;

    // колокольчик, вывеска, весы, ветер
    float bellT = -1f, kickT = -1f, scalesWobble, gust;
    Vector3 bellBasePos; Quaternion bellBaseRot; bool bellCaptured;
    float baseSwing = -1f;

    // монетки
    class Fly { public Transform t; public Vector3 start; public float delay, dur, spin, arc, t0; public bool active, started; }
    readonly List<Fly> flies = new List<Fly>();
    Transform runtimeRoot;
    int sfxLeft;

    // облачка
    class Puff { public Transform t; public float age = -1f, life, size, spin; public Vector3 vel; }
    readonly List<Puff> puffs = new List<Puff>();
    float nextAmbient;

    // «выпрыгивание» новых ярусов
    class Pop { public Transform t; public float t0; }
    readonly List<Pop> pops = new List<Pop>();

    // блики
    ParticleSystem glints;
    float nextGlint;

    // ценник
    TextMeshPro txtLog, txtPlank;
    float priceAt;
    string lastLog, lastPlank;
    bool lastBazaar;

    static AudioClip bellClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { bellClip = null; rainbowClip = null; }

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        if (stages == null) stages = GetComponent<ShopStages>();
        SaveSystem.Register(this);   // если файл уже прочитан, секция «shop» применится прямо здесь (Load)
        if (hutLife != null) baseSwing = hutLife.swingAmp;
    }

    void OnEnable() { TradingStall.SaleMade += OnSale; }
    void OnDisable() { TradingStall.SaleMade -= OnSale; }

    void OnDestroy()
    {
        SaveSystem.Unregister(this);
        if (runtimeRoot != null) Destroy(runtimeRoot.gameObject);
    }

    void Start()
    {
        started = true;
        if (stall == null) stall = FindFirstObjectByType<TradingStall>(FindObjectsInactive.Include);
        EnsureTexts();
        ApplyTiers(false);
        nextAmbient = Time.time + 2f;
        CaptureRainbow();
        CaptureMascot();
        if (rainbow != null) rainbow.gameObject.SetActive(false);
    }

    bool Built { get { return stages == null || stages.Stage >= 3; } }

    // ------------------------------------------------------------ сохранение (ISaveable, секция «shop»)
    public string SaveKey { get { return "shop"; } }
    public int LoadOrder { get { return 65; } }

    [Serializable] class SaveData { public int version = 1; public int logs, planks, coins, sales; }

    public string Save()
    {
        return JsonUtility.ToJson(new SaveData { logs = logsSold, planks = planksSold, coins = coinsEarned, sales = sales });
    }

    // без событий, эффектов и RequestSave (правило ISaveable)
    public void Load(string json, int formatVersion)
    {
        var d = new SaveData();
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, d); }
            catch (Exception) { d = new SaveData(); }
        }
        logsSold = Mathf.Max(0, d.logs);
        planksSold = Mathf.Max(0, d.planks);
        coinsEarned = Mathf.Max(0, d.coins);
        sales = Mathf.Max(0, d.sales);
        ApplyTiers(false);
    }

    public void ResetToDefault()
    {
        logsSold = planksSold = coinsEarned = sales = 0;
        ApplyTiers(false);
    }

    // ------------------------------------------------------------ витрина
    // для предпросмотра в редакторе (кнопка в генераторе): показать ярусы до t включительно
    public void PreviewTiers(int t)
    {
        SetTiers(logTiers, t);
        SetTiers(plankTiers, t);
        SetTiers(coinTiers, t);
    }

    static void SetTiers(Tiered tt, int t)
    {
        if (tt == null || tt.tiers == null) return;
        for (int i = 0; i < tt.tiers.Length; i++)
            if (tt.tiers[i] != null) tt.tiers[i].SetActive(i <= t);
    }

    static int TierOf(int value, int[] th, int count)
    {
        int tier = 0;
        if (th == null) return 0;
        for (int i = 0; i < count && i < th.Length; i++)
            if (value >= th[i]) tier = i;
        return tier;
    }

    // true, если хотя бы одна витрина получила новый ярус (нужно для радуги)
    bool ApplyTiers(bool animate)
    {
        bool a = ApplyOne(0, logTiers, logThresholds, logsSold, animate);
        bool b = ApplyOne(1, plankTiers, plankThresholds, planksSold, animate);
        bool c = ApplyOne(2, coinTiers, coinThresholds, coinsEarned, animate);
        return a || b || c;
    }

    bool ApplyOne(int slot, Tiered tt, int[] th, int value, bool animate)
    {
        bool anyNew = false;
        if (tt == null || tt.tiers == null) return false;
        int n = tt.tiers.Length;
        int tier = TierOf(value, th, n);
        for (int i = 0; i < n; i++)
        {
            var g = tt.tiers[i];
            if (g == null) continue;
            bool on = i <= tier;
            bool turnedOn = on && !g.activeSelf;
            if (g.activeSelf != on) g.SetActive(on);
            if (turnedOn && animate) anyNew = true;
            if (turnedOn && animate && started && Built && !VibeSettings.ReduceEffects) StartPop(g.transform);
        }
        shown[slot] = tier;
        return anyNew;
    }

    void StartPop(Transform t)
    {
        t.localScale = Vector3.one * 0.01f;
        pops.Add(new Pop { t = t, t0 = Time.time });
        SpawnPuff(t.position + Vector3.up * 0.25f, 0.5f);
        SpawnPuff(t.position + Vector3.up * 0.4f, 0.4f);
        SfxManager.Play("build", 0.6f, 1.25f);
    }

    static float EaseOutBack(float k)
    {
        const float c1 = 2.2f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    void TickPops()
    {
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            var p = pops[i];
            if (p.t == null) { pops.RemoveAt(i); continue; }
            float k = (Time.time - p.t0) / 0.5f;
            if (k >= 1f) { p.t.localScale = Vector3.one; pops.RemoveAt(i); continue; }
            p.t.localScale = Vector3.one * Mathf.Max(0.01f, EaseOutBack(Mathf.Clamp01(k)));
        }
    }

    // ------------------------------------------------------------ продажа
    void OnSale(int coins, int logs, int planks)
    {
        if (coins <= 0) return;
        logsSold += Mathf.Max(0, logs);
        planksSold += Mathf.Max(0, planks);
        coinsEarned += coins;
        sales++;
        SaveSystem.RequestSave("shop");
        if (!isActiveAndEnabled || !Built) return;

        bool big = coins >= bigSale;
        bool reduce = VibeSettings.ReduceEffects;

        // серия: продажи с паузой не больше streakWindow секунд
        streak = (Time.time - lastSaleT <= streakWindow) ? streak + 1 : 1;
        lastSaleT = Time.time;

        int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(coins) * 1.4f), 3, 16);
        if (reduce) n = Mathf.Max(2, n / 3);
        FlyCoins(n);

        RingBell(1f);
        kickT = Time.time;
        scalesWobble = 1f;
        gust = 1f;

        int puffsN = big ? 5 : 2;
        if (reduce) puffsN = 1;
        for (int i = 0; i < puffsN; i++) SpawnPuff(PuffPoint() + new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), 0f, UnityEngine.Random.Range(-0.2f, 0.2f)), big ? 0.75f : 0.55f);

        SpawnSaleText(coins);
        HopMascot(big ? 2 : 1, big ? 0.3f : 0.2f);

        bool newTier = ApplyTiers(true);
        if (newTier) ShowRainbow(rainbowLife + 2f);
        else if (big) ShowRainbow(rainbowLife);
        else if (streak >= streakNeed) ShowRainbow(rainbowLife * 0.7f);
    }

    Vector3 PuffPoint()
    {
        if (cloudPoint != null) return cloudPoint.position;
        if (counterPoint != null) return counterPoint.position + Vector3.up * 0.4f;
        return transform.position + Vector3.up * 3f;
    }

    // ------------------------------------------------------------ монетки: из прилавка к игроку
    void FlyCoins(int n)
    {
        if (coinProto == null || counterPoint == null) return;
        if (PlayerMovement.Instance == null) return;
        if (runtimeRoot == null) runtimeRoot = new GameObject("ShopLifeRuntime").transform;
        sfxLeft = 6;

        for (int i = 0; i < n; i++)
        {
            Fly f = null;
            for (int k = 0; k < flies.Count; k++) if (!flies[k].active) { f = flies[k]; break; }
            if (f == null)
            {
                if (flies.Count >= 24) break;
                var go = Instantiate(coinProto.gameObject, runtimeRoot);
                go.name = "FlyCoin";
                go.transform.localScale = coinProto.lossyScale;
                f = new Fly { t = go.transform };
                flies.Add(f);
            }
            f.active = true;
            f.started = false;
            f.t0 = Time.time;
            f.delay = i * 0.055f;
            f.dur = UnityEngine.Random.Range(0.62f, 0.85f);
            f.arc = UnityEngine.Random.Range(0.7f, 1.4f);
            f.spin = UnityEngine.Random.Range(360f, 900f);
            f.start = counterPoint.position + new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f), 0.05f, UnityEngine.Random.Range(-0.1f, 0.1f));
            f.t.gameObject.SetActive(false);
        }
    }

    void TickCoins(float dt)
    {
        if (flies.Count == 0) return;
        var pm = PlayerMovement.Instance;
        Vector3 target = pm != null ? pm.transform.position + Vector3.up * 1.1f : Vector3.zero;
        for (int i = 0; i < flies.Count; i++)
        {
            var f = flies[i];
            if (!f.active) continue;
            if (f.t == null) { f.active = false; continue; }
            float el = Time.time - f.t0 - f.delay;
            if (el < 0f) continue;
            if (!f.started) { f.started = true; f.t.position = f.start; f.t.gameObject.SetActive(true); }
            float k = el / f.dur;
            if (k >= 1f || pm == null)
            {
                f.active = false;
                f.t.gameObject.SetActive(false);
                if (sfxLeft > 0)
                {
                    sfxLeft--;
                    SfxManager.Play("coin", 0.5f, 0.9f + (6 - sfxLeft) * 0.07f);
                }
                continue;
            }
            float e = k * k * (3f - 2f * k);
            Vector3 p = Vector3.Lerp(f.start, target, e);
            p.y += Mathf.Sin(e * Mathf.PI) * f.arc;
            f.t.position = p;
            f.t.Rotate(f.spin * dt, f.spin * 0.6f * dt, 0f, Space.Self);
        }
    }

    // ------------------------------------------------------------ облачка
    void SpawnPuff(Vector3 pos, float size)
    {
        if (puffMesh == null || puffMat == null) return;
        if (runtimeRoot == null) runtimeRoot = new GameObject("ShopLifeRuntime").transform;
        Puff p = null;
        for (int i = 0; i < puffs.Count; i++) if (puffs[i].age < 0f) { p = puffs[i]; break; }
        if (p == null)
        {
            if (puffs.Count >= 16) return;
            var go = new GameObject("ShopPuff");
            go.transform.SetParent(runtimeRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = puffMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = puffMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            p = new Puff { t = go.transform };
            puffs.Add(p);
        }
        p.age = 0f;
        p.life = UnityEngine.Random.Range(1.7f, 2.4f);
        p.size = size * UnityEngine.Random.Range(0.8f, 1.15f);
        p.vel = new Vector3(UnityEngine.Random.Range(-0.12f, 0.12f), UnityEngine.Random.Range(0.55f, 0.9f), UnityEngine.Random.Range(-0.12f, 0.12f));
        p.spin = UnityEngine.Random.Range(-50f, 50f);
        p.t.position = pos;
        p.t.rotation = UnityEngine.Random.rotation;
        p.t.localScale = Vector3.one * 0.01f;
        p.t.gameObject.SetActive(true);
    }

    void TickPuffs(float dt)
    {
        if (ambientPuffs && Built && !VibeSettings.ReduceEffects && Time.time >= nextAmbient)
        {
            nextAmbient = Time.time + UnityEngine.Random.Range(3.2f, 5.5f);
            SpawnPuff(PuffPoint(), 0.42f);
        }
        for (int i = 0; i < puffs.Count; i++)
        {
            var p = puffs[i];
            if (p.age < 0f || p.t == null) continue;
            p.age += dt;
            float k = p.age / p.life;
            if (k >= 1f) { p.age = -1f; p.t.gameObject.SetActive(false); continue; }
            p.t.position += (p.vel + new Vector3(0.28f, 0f, 0.08f) * (0.4f + k)) * dt;
            float grow = Mathf.SmoothStep(0.2f, 1f, Mathf.Clamp01(k * 2.4f));
            float fade = 1f - Mathf.SmoothStep(0.7f, 1f, k);
            p.t.localScale = Vector3.one * (p.size * grow * fade);
            p.t.Rotate(0f, p.spin * dt, 0f, Space.World);
        }
    }

    // ------------------------------------------------------------ колокольчик
    static AudioClip BellClip()
    {
        if (bellClip == null)
            bellClip = VibeSynth.Make("shop_bell", 1.1f, t =>
                VibeSynth.Bell(2093f, t, 0.35f) * 0.7f
                + VibeSynth.Bell(3136f, t, 0.22f) * 0.45f
                + VibeSynth.Bell(4186f, t - 0.015f, 0.12f) * 0.25f, 0.6f);
        return bellClip;
    }

    void RingBell(float power)
    {
        bellT = Time.time;
        if (bellDome != null && !bellCaptured)
        {
            bellBasePos = bellDome.localPosition;
            bellBaseRot = bellDome.localRotation;
            bellCaptured = true;
        }
        var clip = BellClip();
        if (clip != null)
        {
            Vector3 at = bellDome != null ? bellDome.position : transform.position + Vector3.up;
            AudioSource.PlayClipAtPoint(clip, at, Mathf.Clamp01(VibeSettings.SfxVolume) * Mathf.Clamp01(power));
        }
    }

    void TickBell()
    {
        if (bellDome == null || !bellCaptured) return;
        float el = Time.time - bellT;
        if (bellT < 0f || el > 0.9f)
        {
            bellDome.localPosition = bellBasePos;
            bellDome.localRotation = bellBaseRot;
            return;
        }
        float d = 1f - el / 0.9f;
        float hop = Mathf.Max(0f, Mathf.Sin(el * 26f)) * 0.03f * d;
        bellDome.localPosition = bellBasePos + new Vector3(0f, hop, 0f);
        bellDome.localRotation = bellBaseRot * Quaternion.Euler(Mathf.Sin(el * 38f) * 14f * d, 0f, Mathf.Cos(el * 31f) * 12f * d);
    }

    // ------------------------------------------------------------ вывеска, весы, ветер
    void TickSign()
    {
        if (signKick == null) return;
        float el = Time.time - kickT;
        if (kickT < 0f || el > 1.6f) { signKick.localRotation = Quaternion.identity; return; }
        float d = 1f - el / 1.6f;
        signKick.localRotation = Quaternion.Euler(Mathf.Sin(el * 7f) * 7f * d, 0f, Mathf.Sin(el * 9f + 0.6f) * 16f * d * d);
    }

    void TickScales(float dt)
    {
        if (scalesBeam == null) return;
        scalesWobble = Mathf.MoveTowards(scalesWobble, 0f, dt * 0.5f);
        float a = scalesBaseTilt + Mathf.Sin(Time.time * 9f) * 14f * scalesWobble * scalesWobble + Mathf.Sin(Time.time * 0.8f) * 0.7f;
        var q = Quaternion.Euler(0f, 0f, a);
        scalesBeam.localRotation = q;
        Vector3 c = scalesBeam.localPosition;
        if (panL != null) panL.localPosition = c + q * new Vector3(-scalesArm, 0f, 0f) + Vector3.down * scalesChain;
        if (panR != null) panR.localPosition = c + q * new Vector3(scalesArm, 0f, 0f) + Vector3.down * scalesChain;
    }

    void TickGust(float dt)
    {
        if (hutLife == null || baseSwing < 0f) return;
        gust = Mathf.MoveTowards(gust, 0f, dt * 0.55f);
        hutLife.swingAmp = baseSwing * (1f + 0.9f * gust);
    }

    // ------------------------------------------------------------ приветствие
    void TickGreeting()
    {
        var pm = PlayerMovement.Instance;
        if (pm == null || greetPoint == null) { inside = false; return; }
        Vector3 d = pm.transform.position - greetPoint.position;
        d.y = 0f;
        bool near = d.sqrMagnitude < greetRadius * greetRadius;
        if (near && !inside && Time.time >= nextGreet && (stall == null || stall.Unlocked))
        {
            nextGreet = Time.time + greetCooldown;
            RingBell(0.7f);
            kickT = Time.time;
            gust = Mathf.Max(gust, 0.6f);
            if (!VibeSettings.ReduceEffects) SpawnPuff(PuffPoint(), 0.5f);
            waveT = Time.time;
            HopMascot(1, 0.12f);
        }
        inside = near;
    }

    // открыл окно торговли: «дзинь» (окно открывается по E, мы следим за переходом)
    bool wasOpen;
    void TickWindow()
    {
        bool open = stall != null && SellWindow.IsOpenFor(stall);
        if (open && !wasOpen) { RingBell(0.55f); kickT = Time.time; scalesWobble = Mathf.Max(scalesWobble, 0.5f); HopMascot(1, 0.1f); }
        wasOpen = open;
    }

    // ------------------------------------------------------------ ценник на меловой доске
    TextMeshPro MakeText(Transform anchor, string name)
    {
        if (anchor == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(anchor, false);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // доска смотрит на +Z, TMP читается с -Z
        var t = go.AddComponent<TextMeshPro>();
        t.fontSize = priceFontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.color = chalk;
        t.rectTransform.sizeDelta = new Vector2(0.6f, 0.3f);
        t.text = "";
        return t;
    }

    void EnsureTexts()
    {
        if (txtLog == null) txtLog = MakeText(priceLogAnchor, "PriceLog");
        if (txtPlank == null) txtPlank = MakeText(pricePlankAnchor, "PricePlank");
    }

    static string Fmt(float v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }

    void TickPrices()
    {
        if (Time.unscaledTime < priceAt) return;
        priceAt = Time.unscaledTime + 0.5f;
        EnsureTexts();

        bool bazaar = HostEffects.BazaarReady;
        float mul = bazaar ? GameBalance.SkillBazaarMul : 1f;
        string a = Fmt(Pricing.UnitPrice(SellItem.Log) * mul);
        string b = Fmt(Pricing.UnitPrice(SellItem.Plank) * mul);

        if (txtLog != null && (a != lastLog || bazaar != lastBazaar)) { txtLog.text = a; txtLog.color = bazaar ? chalkBazaar : chalk; }
        if (txtPlank != null && (b != lastPlank || bazaar != lastBazaar)) { txtPlank.text = b; txtPlank.color = bazaar ? chalkBazaar : chalk; }
        lastLog = a; lastPlank = b; lastBazaar = bazaar;

        if (bazaarBadge != null && bazaarBadge.activeSelf != bazaar) bazaarBadge.SetActive(bazaar);
    }

    void TickBadge()
    {
        if (bazaarBadge == null || !bazaarBadge.activeSelf) return;
        float t = Time.time;
        bazaarBadge.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.2f) * 12f);
        bazaarBadge.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 4f) * 0.08f);
    }

    // ------------------------------------------------------------ блики и звезда
    ParticleSystem MakeGlints()
    {
        var go = new GameObject("ShopGlints");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 20;
        main.startLifetime = 0.7f; main.startSpeed = 0f; main.startSize = 0.16f;
        main.startColor = new Color(1f, 0.95f, 0.65f, 1f);
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        Material mat = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (mat != null) r.sharedMaterial = mat; else r.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.1f)));
        return ps;
    }

    void TickGlints()
    {
        if (glintPoint == null || VibeSettings.ReduceEffects || !Built) return;
        if (Time.time < nextGlint) return;
        nextGlint = Time.time + UnityEngine.Random.Range(1.6f, 3.4f);
        if (glints == null) glints = MakeGlints();
        var ep = new ParticleSystem.EmitParams();
        ep.position = glintPoint.position + new Vector3(UnityEngine.Random.Range(-0.18f, 0.18f), UnityEngine.Random.Range(0f, 0.1f), UnityEngine.Random.Range(-0.08f, 0.08f));
        glints.Emit(ep, 1);
    }

    void TickStar()
    {
        if (goldStar == null || !goldStar.gameObject.activeInHierarchy) return;
        goldStar.localRotation = Quaternion.Euler(0f, Time.time * 70f, 0f);
    }

    // ------------------------------------------------------------ главный цикл
    void Update()
    {
        if (!started || !Built) return;
        float dt = Time.deltaTime;

        TickGreeting();
        TickWindow();
        TickBell();
        TickSign();
        TickScales(dt);
        TickGust(dt);
        TickCoins(dt);
        TickPuffs(dt);
        TickPops();
        TickPrices();
        TickBadge();
        TickGlints();
        TickStar();
        TickRainbow(dt);
        TickMascot(dt);
        TickFloats(dt);
    }

    // ------------------------------------------------------------ радуга
    static AudioClip RainbowClip()
    {
        if (rainbowClip == null)
            rainbowClip = VibeSynth.Make("shop_rainbow", 1.6f, t =>
                VibeSynth.Bell(1046.5f, t, 0.5f) * 0.6f
                + VibeSynth.Bell(1318.5f, t - 0.12f, 0.5f) * 0.55f
                + VibeSynth.Bell(1568f, t - 0.24f, 0.5f) * 0.5f
                + VibeSynth.Bell(2093f, t - 0.36f, 0.7f) * 0.5f, 0.55f);
        return rainbowClip;
    }

    void CaptureRainbow()
    {
        if (rbCaptured || rainbow == null) return;
        rbBase = rainbow.localScale;
        rbCaptured = true;
    }

    // показать радугу (или продлить, если уже висит); hold = сколько секунд держать
    void ShowRainbow(float hold)
    {
        if (rainbow == null || !Built) return;
        CaptureRainbow();
        if (VibeSettings.ReduceEffects) hold *= 0.6f;
        rbHold = Mathf.Max(rbHold, hold);
        rbUp = true;
        if (rainbow.gameObject.activeSelf) return;

        rbK = 0f;
        rbOver = 1f;
        rainbow.localScale = rbBase * 0.01f;
        rainbow.gameObject.SetActive(true);
        var clip = RainbowClip();
        if (clip != null) AudioSource.PlayClipAtPoint(clip, rainbow.position, Mathf.Clamp01(VibeSettings.SfxVolume) * 0.8f);
        if (!VibeSettings.ReduceEffects) FootPuffs(0.85f);
    }

    void FootPuffs(float size)
    {
        if (rainbowFootL != null) SpawnPuff(rainbowFootL.position, size);
        if (rainbowFootR != null) SpawnPuff(rainbowFootR.position, size);
    }

    void TickRainbow(float dt)
    {
        if (rainbow == null || !rainbow.gameObject.activeSelf) return;
        if (rbHold > 0f) { rbHold -= dt; rbUp = true; } else rbUp = false;

        rbK = Mathf.MoveTowards(rbK, rbUp ? 1f : 0f, dt * (rbUp ? 0.9f : 1.3f));
        if (!rbUp && rbK <= 0f)
        {
            if (!VibeSettings.ReduceEffects) FootPuffs(0.6f);
            rainbow.gameObject.SetActive(false);
            return;
        }
        // одна кривая и на подъём, и на спуск (без скачков при смене направления); «перелёт» пружинки подмешивается плавно
        // и только пока растёт. Никакого «дыхания»: дрожали края дуги и облачка на концах
        rbOver = Mathf.MoveTowards(rbOver, rbUp ? 1f : 0f, dt * 3f);
        float s = rbK * rbK * (3f - 2f * rbK) + Mathf.Sin(rbK * Mathf.PI) * 0.09f * rbOver;
        rainbow.localScale = new Vector3(
            rbBase.x * Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(s)),
            rbBase.y * Mathf.Max(0.01f, s),
            rbBase.z);
    }

    // ------------------------------------------------------------ «+N» над прилавком
    void SpawnSaleText(int coins)
    {
        string txt = "+" + coins;
        if (streak >= 2) txt += "  x" + streak;
        SpawnFloat(CounterTop() + new Vector3(0f, 0.35f, 0f), txt, floatColor, coins >= bigSale ? 1.35f : 1f);
    }

    Vector3 CounterTop()
    {
        if (counterPoint != null) return counterPoint.position;
        return transform.position + Vector3.up * 1.5f;
    }

    void SpawnFloat(Vector3 pos, string text, Color col, float scale)
    {
        if (runtimeRoot == null) runtimeRoot = new GameObject("ShopLifeRuntime").transform;
        FloatTxt f = null;
        for (int i = 0; i < floatTxts.Count; i++) if (floatTxts[i].age < 0f) { f = floatTxts[i]; break; }
        if (f == null)
        {
            if (floatTxts.Count >= 6) return;
            var go = new GameObject("ShopFloatText");
            go.transform.SetParent(runtimeRoot, false);
            var t = go.AddComponent<TextMeshPro>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            t.fontSize = 3.2f;
            t.rectTransform.sizeDelta = new Vector2(3f, 1f);
            t.outlineWidth = 0.22f;
            t.outlineColor = new Color32(0x6B, 0x3A, 0x10, 255);
            f = new FloatTxt { t = t };
            floatTxts.Add(f);
        }
        f.age = 0f;
        f.life = 1.5f;
        f.col = col;
        f.vel = new Vector3(0f, 0.7f, 0f);
        f.t.text = text;
        f.t.color = col;
        f.t.transform.position = pos;
        f.scale = scale;
        f.t.transform.localScale = Vector3.one * 0.01f;
        f.t.gameObject.SetActive(true);
    }

    void TickFloats(float dt)
    {
        if (floatTxts.Count == 0) return;
        var cam = Camera.main;
        for (int i = 0; i < floatTxts.Count; i++)
        {
            var f = floatTxts[i];
            if (f.age < 0f || f.t == null) continue;
            f.age += dt;
            float k = f.age / f.life;
            if (k >= 1f) { f.age = -1f; f.t.gameObject.SetActive(false); continue; }
            var tr = f.t.transform;
            tr.position += new Vector3(0f, f.vel.y * (1f - k * 0.6f) * dt, 0f);
            float pop = k < 0.18f ? EaseOutBack(k / 0.18f) : 1f;
            tr.localScale = Vector3.one * (0.8f * f.scale * Mathf.Max(0.05f, pop));
            if (cam != null) tr.rotation = cam.transform.rotation;
            var c = f.col; c.a = 1f - Mathf.SmoothStep(0.55f, 1f, k);
            f.t.color = c;
        }
    }

    // ------------------------------------------------------------ Пух
    void CaptureMascot()
    {
        if (mascotCaptured) return;
        if (mascot != null) mascotBaseRot = mascot.localRotation;
        if (mascotBody != null) mascotBodyPos = mascotBody.localPosition;
        if (mascotEyes != null) mascotEyesScale = mascotEyes.localScale;
        if (mascotArm != null) armBaseRot = mascotArm.localRotation;
        mascotCaptured = true;
    }

    // прыжок с пружинкой: n раз подряд, h = высота в метрах
    void HopMascot(int n, float h)
    {
        if (mascotBody == null) return;
        hopsLeft = Mathf.Max(0, n - 1);
        hopH = h;
        hopT = Time.time;
    }

    void TickMascot(float dt)
    {
        if (!mascotCaptured) return;
        float t = Time.time;

        // качание на месте и дыхание
        Vector3 pos = mascotBodyPos + new Vector3(0f, Mathf.Sin(t * 2.1f) * 0.01f, 0f);
        float breathe = Mathf.Sin(t * 2.1f + 0.8f) * 0.02f;
        Vector3 scl = new Vector3(1f - breathe * 0.6f, 1f + breathe, 1f - breathe * 0.6f);

        // прыжок: сначала вытягивается вверх, на спуске сплющивается
        if (hopT >= 0f)
        {
            float k = (t - hopT) / 0.6f;
            if (k >= 1f)
            {
                if (hopsLeft > 0) { hopsLeft--; hopT = t; k = 0f; }
                else hopT = -1f;
            }
            if (hopT >= 0f)
            {
                float y = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * hopH * (1f - 0.2f * k);
                float sq = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI * 2f) * 0.14f * (1f - k);
                pos.y += y;
                scl = new Vector3(1f - sq * 0.6f, 1f + sq, 1f - sq * 0.6f);
            }
        }
        if (mascotBody != null)
        {
            mascotBody.localPosition = pos;
            mascotBody.localScale = scl;
        }

        // смотрит на игрока (не больше ±75° от «лицом вперёд»)
        if (mascot != null)
        {
            var pm = PlayerMovement.Instance;
            Quaternion want = mascotBaseRot;
            if (pm != null && mascot.parent != null)
            {
                Vector3 to = pm.transform.position - mascot.position;
                to.y = 0f;
                if (to.sqrMagnitude < greetRadius * greetRadius * 1.7f && to.sqrMagnitude > 0.01f)
                {
                    Vector3 loc = mascot.parent.InverseTransformDirection(to);
                    float yaw = Mathf.Clamp(Mathf.Atan2(loc.x, loc.z) * Mathf.Rad2Deg, -75f, 75f);
                    want = Quaternion.Euler(0f, yaw, 0f) * mascotBaseRot;
                }
            }
            mascot.localRotation = Quaternion.Slerp(mascot.localRotation, want, 1f - Mathf.Exp(-6f * dt));
        }

        // моргание
        if (mascotEyes != null)
        {
            if (blinkT < 0f && t >= nextBlink) { blinkT = t; nextBlink = t + UnityEngine.Random.Range(2.5f, 5f); }
            float open = 1f;
            if (blinkT >= 0f)
            {
                float k = (t - blinkT) / 0.16f;
                if (k >= 1f) blinkT = -1f; else open = Mathf.Abs(k * 2f - 1f) * 0.9f + 0.1f;
            }
            mascotEyes.localScale = new Vector3(mascotEyesScale.x, mascotEyesScale.y * open, mascotEyesScale.z);
        }

        // машет рукой при приветствии
        if (mascotArm != null)
        {
            float el = t - waveT;
            if (waveT < 0f || el > 1.6f) mascotArm.localRotation = armBaseRot;
            else
            {
                float d = 1f - el / 1.6f;
                float ang = 55f + Mathf.Sin(el * 14f) * 28f * d;
                mascotArm.localRotation = armBaseRot * Quaternion.Euler(0f, 0f, ang * Mathf.Min(1f, el * 5f) * Mathf.Min(1f, d * 4f));
            }
        }
    }

    // ---- тест в редакторе (правый клик по заголовку компонента, в режиме Play)
    [ContextMenu("Тест: продажа на 30 монет")]
    void TestSale() { OnSale(30, 10, 3); }

    [ContextMenu("Тест: крупная продажа")]
    void TestBigSale() { OnSale(240, 40, 20); }

    [ContextMenu("Тест: радуга")]
    void TestRainbow() { ShowRainbow(rainbowLife); }

    [ContextMenu("Тест: Пух прыгает и машет")]
    void TestMascot() { HopMascot(2, 0.25f); waveT = Time.time; }

    [ContextMenu("Тест: колокольчик")]
    void TestBell() { RingBell(1f); kickT = Time.time; }
}
