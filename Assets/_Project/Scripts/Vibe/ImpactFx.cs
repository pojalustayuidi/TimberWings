// Assets/_Project/Scripts/Vibe/ImpactFx.cs (НЕ в Editor)
// Сочный «импакт» поверх готовых систем. Ничего вешать в сцену не нужно: VibeBootstrap создаёт компонент сам.
// Слушает GameEvents (ChopHit, TreeChopped, LevelUp, QuestDone) и ничего в старых скриптах не ломает.
//
//  - Удар топора:    вспышка + искры + щепки + пыль у корня + листья сыплются с кроны
//  - Дерево упало:   волна по земле, облако пыли, густой листопад, крупные щепки, микро-«стоп-кадр»
//  - Бег:            пыль из-под ног, сильнее на скорости
//  - Уровень/квест:  золотые кольца по земле и поднимающиеся искры
//  - Костёр:         искры и дымок над любым объектом с CampfireFlicker (горит только когда стадия построена)
//
// Всё рисуется кодом (VibeGfx + собственное кольцо), картинки и префабы не нужны.
// Частицы лежат в пуле: по одной системе на тип эффекта, GameObject на каждый удар не создаются.
// «Уменьшить эффекты» (VibeSettings.ReduceEffects): частиц втрое меньше, без стоп-кадра и без листопада.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImpactFx : MonoBehaviour
{
    [Header("Рубка")]
    public bool chopImpact = true;
    public bool fallingLeaves = true;
    [Tooltip("Микро-пауза времени в момент падения дерева (доли секунды)")]
    public bool hitStop = true;
    public float hitStopSeconds = 0.05f;

    [Header("Visual Polish 3.1 — точность удара")]
    [Tooltip("Короткий, локальный акцент для хороших и идеальных ритм-ударов. Не влияет на урон или награды.")]
    public bool qualityAccent = true;
    [Range(1, 18)] public int normalChips = 8;
    [Range(1, 18)] public int goodAccentChips = 4;
    [Range(1, 18)] public int perfectAccentChips = 9;
    [Tooltip("Минимальный промежуток между визуальными акцентами качества, секунды")]
    [Range(0f, 0.4f)] public float qualityFxCooldown = 0.085f;

    [Header("Бег")]
    public bool footDust = true;
    [Tooltip("Через сколько метров пути выпускать облачко")]
    public float stepDistance = 0.85f;

    [Header("Награды")]
    public bool rewardRings = true;

    [Header("Костёр")]
    public bool campfireEmbers = true;

    [Header("Стройка")]
    public bool buildEffects = true;

    [Header("Visual Polish 3.2 — строительные эффекты")]
    [Tooltip("Вместо взрывных вспышек — древесные щепки и мягкая пыль. Не влияет на строительство.")]
    public bool quietWoodBuild = true;
    [Tooltip("Сила строительной пыли при ударах и посадке деталей (0 отключает пыль).")]
    [Range(0f, 1.5f)] public float buildDust = 0.85f;

    // цвета в стиле проекта (тёплая палитра)
    static readonly Color Warm = new Color(1f, 0.82f, 0.4f, 1f);
    static readonly Color Spark = new Color(1f, 0.62f, 0.18f, 1f);
    static readonly Color Dust = new Color(0.82f, 0.72f, 0.55f, 0.55f);
    static readonly Color Gold = new Color32(0xF5, 0xC5, 0x42, 255);

    ParticleSystem flash, sparks, chips, dust, leaves, ring, risers;
    ParticleSystem bSparks, bSplinters, bFlare, bPop;   // удар молотка по зданию (BuildHit / BuildSlam)
    Light strikeLight;
    float lightKick;
    PlayerInteraction pi;
    Transform player;
    float findAt, scanAt;
    Vector3 lastPos;
    float stepAcc;
    bool hitStopBusy;
    float hitStopPreviousScale = 1f;
    float hitStopAppliedScale;
    Vector3 lastTreeHit;
    int lastTreeHitFrame = -1;
    float lastQualityFxAt = -100f;
    bool reduced;
    float reducedAt;
    static Sprite ringSprite;

    readonly HashSet<int> fires = new HashSet<int>();

    public static ImpactFx Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        flash = MakeFlash();
        sparks = MakeSparks();
        chips = MakeChips();
        dust = MakeDust();
        leaves = MakeLeaves();
        ring = MakeRing();
        risers = MakeRisers();
        bSparks = MakeStrikeSparks();
        bSplinters = MakeStrikeSplinters();
        bFlare = MakeStrikeFlare();
        bPop = MakeStrikePop();
        MakeStrikeLight();
    }

    void OnEnable()
    {
        GameEvents.ChopHit += OnChopHit;
        GameEvents.ChopQuality += OnChopQuality;
        GameEvents.TreeChopped += OnTreeChopped;
        GameEvents.LevelUp += OnLevelUp;
        GameEvents.QuestDone += OnQuestDone;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnDisable()
    {
        GameEvents.ChopHit -= OnChopHit;
        GameEvents.ChopQuality -= OnChopQuality;
        GameEvents.TreeChopped -= OnTreeChopped;
        GameEvents.LevelUp -= OnLevelUp;
        GameEvents.QuestDone -= OnQuestDone;
        if (hitStopBusy)
        {
            // Respect pause/settings systems that may have changed timeScale meanwhile.
            if (Mathf.Approximately(Time.timeScale, hitStopAppliedScale))
                Time.timeScale = hitStopPreviousScale;
            hitStopBusy = false;
        }
    }

    // множитель количества частиц
    float M { get { return reduced ? 0.33f : 1f; } }

    void Update()
    {
        if (Time.unscaledTime >= reducedAt) { reducedAt = Time.unscaledTime + 1f; reduced = VibeSettings.ReduceEffects; }

        if (player == null || pi == null)
        {
            if (Time.unscaledTime >= findAt)
            {
                findAt = Time.unscaledTime + 1f;
                if (player == null) { var pm = PlayerMovement.Instance; if (pm != null) { player = pm.transform; lastPos = player.position; } }
                if (pi == null) pi = FindFirstObjectByType<PlayerInteraction>();
            }
        }

        if (footDust) TickFootDust();
        if (strikeLight != null && lightKick > 0f)
        {
            // тёплая вспышка света от удара гаснет быстро (unscaled: не зависает на микро-паузе)
            lightKick = Mathf.MoveTowards(lightKick, 0f, Time.unscaledDeltaTime * 9f);
            strikeLight.intensity = lightKick * 3.2f;
            if (lightKick <= 0f) strikeLight.enabled = false;
        }
        if (Time.unscaledTime >= scanAt)
        {
            scanAt = Time.unscaledTime + 3f;
            if (campfireEmbers) ScanFires();
            if (buildEffects) ScanSites();
        }
    }

    internal static void PlayLogDebris(Vector3 point, bool split)
    {
        var fx = Instance;
        if (fx == null || !fx.chopImpact) return;
        int count = VibeSettings.ReduceEffects ? 2 : split ? 6 : 4;
        fx.EmitDir(fx.chips, point, count, Vector3.up, 1f, split ? 2.5f : 2f, .66f);
        if (split) fx.Emit(fx.dust, point, VibeSettings.ReduceEffects ? 1 : 3);
    }

    // ------------------------------------------------------------ удар и падение дерева
    void OnChopHit()
    {
        if (!chopImpact || player == null) return;
        if (pi != null && pi.IsProcessingPrototypeLog && pi.TryReadLeoImpact(out Vector3 cut))
        {
            PlayLogDebris(cut, false);
            return;
        }
        Vector3 p = ImpactPoint();
        Vector3 dustPoint = GroundPoint(p);
        if (pi != null && pi.TryReadLeoImpact(out Vector3 bark))
        {
            dustPoint.x = bark.x;
            dustPoint.z = bark.z;
            p = bark;
        }
        lastTreeHit = p;
        lastTreeHitFrame = Time.frameCount;
        // New quality accents are applied by OnChopQuality in this same frame.
        var lib = reduced ? null : FxLibrary.Get();
        if (lib != null && lib.chopHit != null) FxLibrary.Spawn(lib.chopHit, p, 1f, 2.5f);   // готовый префаб (Cartoon FX): без своей вспышки и искр
        else
        {
            Emit(flash, p, 1);
            Emit(sparks, p, Mathf.Max(2, Mathf.RoundToInt(7 * M)));
        }
        // Directional bark chips read much better than a second point burst.
        Vector3 away = player.position - p;
        away.y = Mathf.Max(.25f, Mathf.Abs(away.y) * .25f);
        EmitDir(chips, p, Mathf.Max(2, Mathf.RoundToInt(normalChips * M)), away.normalized,
                1.8f, 4.2f, .65f);
        Emit(dust, dustPoint, Mathf.Max(1, Mathf.RoundToInt(3 * M)));
        if (fallingLeaves && !reduced) DropLeaves(2 + Random.Range(0, 3));
    }

    // The rhythm system raises ChopQuality right after ChopHit. This is purely VFX:
    // a good hit gets a tiny bark burst, a perfect hit adds a golden glint.
    // Failed hits and timed/animation-mode hits keep the existing baseline impact.
    void OnChopQuality(int quality)
    {
        if (!qualityAccent || !chopImpact || player == null || quality <= 0 ||
            lastTreeHitFrame != Time.frameCount ||
            Time.unscaledTime - lastQualityFxAt < qualityFxCooldown) return;

        lastQualityFxAt = Time.unscaledTime;
        bool quiet = VibeSettings.ReduceEffects;
        int amount = quality >= 2 ? perfectAccentChips : goodAccentChips;
        amount = Mathf.Max(1, Mathf.RoundToInt(amount * (quiet ? .33f : 1f)));

        Vector3 away = player.position - lastTreeHit;
        away.y = Mathf.Max(.35f, away.y + .45f);
        EmitDir(chips, lastTreeHit, amount, away.normalized, 2f, quality >= 2 ? 5.2f : 3.9f, .45f);
        if (quality >= 2 && !quiet)
        {
            Emit(flash, lastTreeHit + Vector3.up * .04f, 1,
                 new Color(1f, .91f, .58f, .76f), .57f);
            Emit(sparks, lastTreeHit, 3);
        }
    }

    void OnTreeChopped()
    {
        if (PlayerInteraction.PrototypeFellingEvent || !chopImpact || player == null) return;
        // событие приходит до ChopDown, поэтому TargetItem ещё указывает на срубленное дерево
        Vector3 ground = GroundPoint(ImpactPoint());
        var lib = reduced ? null : FxLibrary.Get();
        if (lib != null && lib.treeFall != null) FxLibrary.Spawn(lib.treeFall, ground + Vector3.up * 0.1f, 1.2f, 4f);
        Emit(ring, ground + Vector3.up * 0.05f, 1, new Color(1f, 0.92f, 0.75f, 0.55f), 3.4f);
        Emit(dust, ground + Vector3.up * 0.2f, Mathf.Max(4, Mathf.RoundToInt(16 * M)));
        Emit(chips, ground + Vector3.up * 0.8f, Mathf.Max(4, Mathf.RoundToInt(14 * M)));
        Emit(sparks, ground + Vector3.up * 1f, Mathf.Max(3, Mathf.RoundToInt(10 * M)));
        if (fallingLeaves && !reduced) DropLeaves(16);
        if (hitStop && !reduced) StartCoroutine(HitStop());
    }

    // точка удара: у дерева, на стороне игрока (камера видит искры, а не заднюю часть ствола)
    Vector3 ImpactPoint()
    {
        Vector3 pp = player.position;
        Transform t = (pi != null && pi.TargetItem != null) ? pi.TargetItem.transform : null;
        if (t == null) return pp + player.forward * 1.2f + Vector3.up * 1.1f;
        Vector3 tp = t.position;
        Vector3 toPlayer = pp - tp; toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.001f) toPlayer.Normalize();
        return tp + toPlayer * 0.4f + Vector3.up * 1.1f;
    }

    static Vector3 GroundPoint(Vector3 p) { return new Vector3(p.x, p.y - 1.05f, p.z); }

    // листья осыпаются с кроны: берём габариты дерева, если есть Renderer
    void DropLeaves(int count)
    {
        Transform t = (pi != null && pi.TargetItem != null) ? pi.TargetItem.transform : null;
        Vector3 center; float radius;
        if (t != null)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(t.position + Vector3.up * 2.5f, Vector3.one);
            bool has = false;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] is ParticleSystemRenderer) continue;
                if (!has) { b = rs[i].bounds; has = true; } else b.Encapsulate(rs[i].bounds);
            }
            center = new Vector3(b.center.x, b.max.y - b.size.y * 0.25f, b.center.z);
            radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.7f, 0.5f, 2.2f);
        }
        else { center = player.position + player.forward * 1.5f + Vector3.up * 3f; radius = 1f; }

        for (int i = 0; i < count; i++)
        {
            Vector3 r = Random.insideUnitSphere * radius;
            r.y *= 0.5f;
            Emit(leaves, center + r, 1);
        }
    }

    IEnumerator HitStop()
    {
        if (hitStopBusy || Time.timeScale < 0.99f || DialogueManager.IsBusy) yield break;
        hitStopBusy = true;
        hitStopPreviousScale = Time.timeScale;
        hitStopAppliedScale = 0.06f;
        Time.timeScale = hitStopAppliedScale;
        yield return new WaitForSecondsRealtime(Mathf.Clamp(hitStopSeconds, 0.01f, 0.12f));
        if (Mathf.Approximately(Time.timeScale, hitStopAppliedScale))
            Time.timeScale = hitStopPreviousScale;
        hitStopBusy = false;
    }

    // ------------------------------------------------------------ бег
    void TickFootDust()
    {
        if (player == null) return;
        Vector3 p = player.position;
        Vector3 d = p - lastPos; d.y = 0f;
        lastPos = p;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = d.magnitude / dt;
        if (speed < 1.2f || speed > 25f) { stepAcc = 0f; return; }   // стоит, или рывок (телепорт)

        stepAcc += d.magnitude;
        if (stepAcc < stepDistance) return;
        stepAcc = 0f;

        Vector3 back = -d.normalized;
        Vector3 at = p + back * 0.25f + Vector3.up * 0.08f;
        Emit(dust, at, Mathf.Max(1, Mathf.RoundToInt((speed > 4.5f ? 3 : 2) * M)));
    }

    // ------------------------------------------------------------ награды
    void OnLevelUp(int level) { Reward(1f); }
    void OnQuestDone(string id) { Reward(0.8f); }

    void Reward(float power)
    {
        if (!rewardRings || player == null) return;
        Vector3 g = player.position + Vector3.up * 0.06f;
        var lib = reduced ? null : FxLibrary.Get();
        if (lib != null)
        {
            GameObject prefab = power >= 1f ? lib.levelUp : lib.questDone;
            if (prefab == null) prefab = lib.levelUp != null ? lib.levelUp : lib.questDone;
            if (prefab != null) FxLibrary.Spawn(prefab, player.position + Vector3.up * 0.2f, 1f, 5f);
        }
        Emit(ring, g, 1, new Color(Gold.r, Gold.g, Gold.b, 0.85f), 3.2f * power);
        Emit(risers, player.position + Vector3.up * 0.2f, Mathf.Max(6, Mathf.RoundToInt(30 * power * M)));
        StartCoroutine(SecondRing(g, power));
    }

    IEnumerator SecondRing(Vector3 g, float power)
    {
        yield return new WaitForSeconds(0.18f);
        Emit(ring, g, 1, new Color(1f, 0.95f, 0.7f, 0.6f), 5.6f * power);
    }

    // ------------------------------------------------------------ стройка
    // каждой площадке стройки добавляем BuildFxHook (он слушает StageBuilt и Finished)
    void ScanSites()
    {
        var all = FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].GetComponent<BuildFxHook>() == null) all[i].gameObject.AddComponent<BuildFxHook>();
    }

    // Стадия готова или построено целиком. Один читаемый акцент, без серии взрывов:
    // золотая волна отвечает за завершение, а обыденная пыль — только за процесс.
    public void StageBurst(Vector3 pos, float power)
    {
        if (!buildEffects) return;
        bool completed = power > 1.3f;
        var lib = reduced ? null : FxLibrary.Get();
        GameObject prefab = lib != null ?
            (completed ? (lib.buildDone != null ? lib.buildDone : lib.stageBuilt) : lib.stageBuilt) : null;
        if (prefab != null)
            FxLibrary.Spawn(prefab, pos + Vector3.up * 0.1f, Mathf.Min(1.15f, power * 0.65f), 3.0f);

        Vector3 ground = pos + Vector3.up * 0.06f;
        // На промежуточной стадии волна меньше. Финал заметный, но камера и дом видны.
        if (!reduced || completed)
            Emit(ring, ground, 1,
                new Color(Gold.r, Gold.g, Gold.b, reduced ? 0.33f : (completed ? 0.70f : 0.47f)),
                completed ? 4.3f : 2.8f);
        EmitBuildDust(pos + Vector3.up * 0.2f, completed ? 10 : 6);
        if (!reduced)
        {
            Emit(risers, pos + Vector3.up * 0.2f, completed ? 14 : 7);
            Emit(chips, pos + Vector3.up * 0.7f, completed ? 6 : 3);
        }
    }

    // Тихая атмосферная пыль без вспышек, колец, звука и движения камеры.
    // Используется между стадиями сборки: старый StageBurst каждые 0.5 с
    // запускал полноценный праздничный эффект, из-за чего здание было плохо видно.
    public void BuildAmbientDust(Vector3 worldPoint)
    {
        if (!buildEffects || reduced) return;
        EmitBuildDust(worldPoint + Vector3.up * 0.12f, 3);
    }

    // Короткий физически понятный контакт детали со своим местом.
    // worldPoint уже является точкой посадки — НЕ прибавляем 1.1 м, как HammerTick.
    public void BuildPieceLand(Vector3 worldPoint)
    {
        if (!buildEffects || reduced) return;
        EmitBuildDust(worldPoint + Vector3.up * 0.05f, 3);
        if (!quietWoodBuild)
            Emit(flash, worldPoint, 1, new Color(1f, 0.88f, 0.62f, 0.28f), 0.32f);
        EmitDir(bSplinters, worldPoint, 3, Vector3.up, 0.4f, 1.8f, 0.85f);
    }

    void EmitBuildDust(Vector3 worldPoint, int baseCount)
    {
        if (buildDust <= 0f) return;
        int count = Mathf.RoundToInt(baseCount * Mathf.Clamp(buildDust, 0f, 1.5f) * M);
        if (count > 0) Emit(dust, worldPoint, count);
    }

    // Запасной удар для стройки без Лео, а также для работающей лесопилки.
    // Здесь pos — точка на земле, поэтому небольшая прибавка высоты намеренная.
    public void HammerTick(Vector3 pos)
    {
        if (!buildEffects) return;
        Vector3 contact = pos + Vector3.up * 0.72f;
        if (!quietWoodBuild)
            Emit(flash, contact, 1, new Color(1f, 0.86f, 0.55f, .42f), .42f);
        EmitDir(bSplinters, contact, reduced ? 1 : 3,
            Vector3.up, .7f, 2.5f, .9f);
        EmitBuildDust(pos + Vector3.up * 0.12f, 3);
    }

    // Удар молотка: основное — направленные щепки. Сильный третий удар
    // получает аккуратный световой акцент, но не взрыв, закрывающий Лео.
    public void BuildHit(Vector3 point, Vector3 normal, Vector3 ground, float power, float progress)
    {
        if (!buildEffects) return;
        normal = SafeNormal(normal);
        Vector3 p = point + normal * 0.05f;
        power = Mathf.Clamp(power, 0.5f, 2.2f);
        bool accent = power > 1.35f || progress > 0.9f;

        EmitDir(bSplinters, p, Mathf.Max(1, Mathf.RoundToInt((accent ? 7 : 5) * M)),
            normal + Vector3.up * 0.42f, 1.3f, accent ? 4.2f : 3.2f, 0.58f);
        EmitBuildDust(ground + Vector3.up * 0.12f + normal * 0.18f, accent ? 4 : 3);
        if (reduced) return;

        if (quietWoodBuild)
        {
            if (accent)
            {
                Emit(bFlare, p, 1, new Color(1f, .90f, .65f, .35f), .30f);
                EmitDir(bSparks, p, 2, normal, 1.8f, 3.8f, .55f);
                Kick(p + normal * .55f, .16f);
            }
        }
        else
        {
            // Оригинальный более яркий режим оставлен в Inspector для сравнения.
            Emit(flash, p, 1, new Color(1f, .87f, .56f, .63f), .43f + .25f * power);
            Emit(bFlare, p, 1, new Color(1f, .95f, .74f, .64f), .28f + .18f * power);
            EmitDir(bSparks, p, Mathf.Max(2, Mathf.RoundToInt(5 * power * M)), normal, 2.6f, 6.2f, .5f);
            Emit(bPop, p, 1, new Color(1f, .82f, .45f, .45f), .55f + .34f * power);
            Kick(p + normal * .8f, .44f + .26f * power);
        }
    }

    // Последний удар этапа: вес молотка чувствуется благодаря более крупным щепкам
    // и короткому тёплому акценту. Финальная волна здания отдельно придёт от StageBurst.
    // Не запускаем второй hit-stop: иначе он иногда совпадал с началом камеры показа.
    public void BuildSlam(Vector3 point, Vector3 normal, Vector3 ground, float power)
    {
        if (!buildEffects) return;
        normal = SafeNormal(normal);
        Vector3 p = point + normal * 0.06f;
        power = Mathf.Clamp(power, 1f, 2.2f);

        EmitDir(bSplinters, p, Mathf.Max(3, Mathf.RoundToInt(10 * M)),
            normal + Vector3.up * .5f, 2f, 5f, .75f);
        EmitBuildDust(ground + Vector3.up * .12f + normal * .2f, 7);
        if (reduced) return;

        Emit(bFlare, p, 1, new Color(1f, .93f, .70f, quietWoodBuild ? .52f : .75f),
            quietWoodBuild ? .56f : .88f);
        EmitDir(bSparks, p, quietWoodBuild ? 4 : 12, normal, 2.2f, 6f, .6f);
        if (!quietWoodBuild)
            Emit(bPop, p, 1, new Color(1f, .85f, .5f, .55f), 1.5f);
        Kick(p + normal * .7f, quietWoodBuild ? .36f : .8f);
    }

    // вспышка света на стене: подвинуть лампу и «зажечь»
    void Kick(Vector3 at, float energy)
    {
        if (strikeLight == null) return;
        strikeLight.transform.position = at;
        lightKick = Mathf.Max(lightKick, energy);
        strikeLight.intensity = lightKick * 3.2f;
        strikeLight.enabled = true;
    }

    static Vector3 SafeNormal(Vector3 n)
    {
        return n.sqrMagnitude < 0.0001f ? Vector3.up : n.normalized;
    }

    // выпуск частиц веером вокруг направления dir: у каждой своя скорость (системы удара без формы, скорость задаём здесь)
    void EmitDir(ParticleSystem ps, Vector3 pos, int count, Vector3 dir, float speedMin, float speedMax, float spread)
    {
        if (ps == null || count <= 0) return;
        var ep = new ParticleSystem.EmitParams();
        ep.position = pos;
        ep.applyShapeToPosition = false;
        for (int i = 0; i < count; i++)
        {
            Vector3 v = (dir + Random.insideUnitSphere * spread).normalized * Random.Range(speedMin, speedMax);
            ep.velocity = v;
            ps.Emit(ep, 1);
        }
    }

    // ------------------------------------------------------------ костёр
    void ScanFires()
    {
        var all = FindObjectsByType<CampfireFlicker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            int id = all[i].GetInstanceID();
            if (fires.Contains(id)) continue;
            fires.Add(id);
            AttachEmbers(all[i].transform);
        }
    }

    // искры и дымок сидят ребёнком стадии костра: пока стадия выключена, их нет
    void AttachEmbers(Transform fire)
    {
        var lib = FxLibrary.Get();
        if (lib != null && lib.campfire != null)
        {
            var cf = Instantiate(lib.campfire, fire);   // готовый огонь (Cartoon FX) живёт ребёнком стадии
            cf.name = "FireFx";
            cf.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            cf.transform.localScale = lib.campfire.transform.localScale * lib.scale;
            return;
        }
        var go = new GameObject("FireFx");
        go.transform.SetParent(fire, false);
        go.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // конусы смотрят вверх

        var emb = Base(go.transform, "Embers", VibeGfx.Soft, true, 60);
        var m = emb.main;
        m.loop = true; m.duration = 4f; m.playOnAwake = true;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        m.startColor = new ParticleSystem.MinMaxGradient(Spark, Warm);
        m.gravityModifier = -0.05f;
        var em = emb.emission; em.rateOverTime = 7f * M;
        var sh = emb.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.25f;
        var nz = emb.noise; nz.enabled = true; nz.strength = 0.7f; nz.frequency = 0.8f; nz.scrollSpeed = 0.6f;
        FadeAndShrink(emb);
        emb.Play();

        var smoke = Base(go.transform, "Smoke", VibeGfx.Soft, false, 40);
        var sm = smoke.main;
        sm.loop = true; sm.duration = 4f; sm.playOnAwake = true;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.6f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        sm.startColor = new Color(0.55f, 0.52f, 0.5f, 0.3f);
        var sem = smoke.emission; sem.rateOverTime = 2.2f * M;
        var ssh = smoke.shape; ssh.enabled = true; ssh.shapeType = ParticleSystemShapeType.Cone; ssh.angle = 10f; ssh.radius = 0.2f;
        var ssz = smoke.sizeOverLifetime; ssz.enabled = true;
        ssz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));
        var scol = smoke.colorOverLifetime; scol.enabled = true;
        scol.color = Fade(0.15f, 0.5f);
        var snz = smoke.noise; snz.enabled = true; snz.strength = 0.4f; snz.frequency = 0.3f;
        smoke.Play();
    }

    // ------------------------------------------------------------ сборка систем
    // общий каркас: система остановлена, без эмиссии по времени, частицы в мировом пространстве; шейдер из VibeGfx или аддитивный
    ParticleSystem Base(Transform parent, string name, Sprite sprite, bool additive, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        Material mat = additive ? AdditiveMat(sprite) : VibeGfx.ParticleMat(sprite);
        if (mat != null) r.sharedMaterial = mat; else r.enabled = false;   // без шейдера частицы просто не рисуются
        return ps;
    }

    // Additive даёт свечение; если шейдер вырезан из билда, берём обычный (прозрачный) из VibeGfx
    static Material AdditiveMat(Sprite s)
    {
        string[] names = { "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive" };
        for (int i = 0; i < names.Length; i++)
        {
            Shader sh = Shader.Find(names[i]);
            if (sh == null) continue;
            var m = new Material(sh);
            if (s != null) m.mainTexture = s.texture;
            return m;
        }
        return VibeGfx.ParticleMat(s);
    }

    // выпуск частиц в точке; шейдеры формы (сфера/конус) сохраняются, если они включены
    void Emit(ParticleSystem ps, Vector3 pos, int count)
    {
        if (ps == null || count <= 0) return;
        var ep = new ParticleSystem.EmitParams();
        ep.position = pos;
        ep.applyShapeToPosition = true;
        ps.Emit(ep, count);
    }

    void Emit(ParticleSystem ps, Vector3 pos, int count, Color color, float size)
    {
        if (ps == null || count <= 0) return;
        var ep = new ParticleSystem.EmitParams();
        ep.position = pos;
        ep.applyShapeToPosition = true;
        ep.startColor = color;
        ep.startSize = size;
        ps.Emit(ep, count);
    }

    static Gradient Fade(float inAt, float outAt)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, outAt), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    static void FadeAndShrink(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0.08f, 0.55f);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f)));
    }

    ParticleSystem MakeFlash()
    {
        var ps = Base(transform, "HitFlash", VibeGfx.Soft, true, 8);
        var m = ps.main;
        m.startLifetime = 0.13f; m.startSpeed = 0f; m.startSize = 1.1f;
        m.startColor = new Color(1f, 0.88f, 0.55f, 0.9f);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0.05f, 0.3f);
        return ps;
    }

    ParticleSystem MakeSparks()
    {
        var ps = Base(transform, "Sparks", VibeGfx.Soft, true, 120);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
        m.startColor = new ParticleSystem.MinMaxGradient(Spark, Warm);
        m.gravityModifier = 1.4f;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
        FadeAndShrink(ps);
        return ps;
    }

    ParticleSystem MakeChips()
    {
        var ps = Base(transform, "BigChips", VibeGfx.Square, false, 120);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.42f, 0.22f, 1f), new Color(0.82f, 0.62f, 0.36f, 1f));
        m.gravityModifier = 1.6f;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0.02f, 0.7f);
        return ps;
    }

    ParticleSystem MakeDust()
    {
        var ps = Base(transform, "Dust", VibeGfx.Soft, false, 150);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        m.startColor = new ParticleSystem.MinMaxGradient(Dust, new Color(0.7f, 0.62f, 0.48f, 0.4f));
        m.gravityModifier = -0.03f;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0.12f, 0.35f);
        return ps;
    }

    ParticleSystem MakeLeaves()
    {
        var ps = Base(transform, "FallLeaves", VibeGfx.Leaf, false, 60);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.34f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.gravityModifier = 0.22f;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.74f, 0.26f, 1f), new Color(0.88f, 0.72f, 0.26f, 1f));
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1.4f; lim.dampen = 0.35f;
        var nz = ps.noise; nz.enabled = true; nz.strength = 1.4f; nz.frequency = 0.5f; nz.scrollSpeed = 0.5f;
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0.05f, 0.75f);
        return ps;
    }

    // плоское кольцо на земле (волна): одна частица растёт и гаснет
    ParticleSystem MakeRing()
    {
        var ps = Base(transform, "GroundRing", RingSprite(), true, 12);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        var m = ps.main;
        m.startLifetime = 0.6f; m.startSpeed = 0f; m.startSize = 3f;
        m.startColor = new Color(1f, 1f, 1f, 0.6f);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.12f, 0f, 3f), new Keyframe(1f, 1f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0.05f, 0.4f);
        return ps;
    }

    // золотые искры, поднимающиеся над героем (уровень, квест)
    ParticleSystem MakeRisers()
    {
        var go = new GameObject("Risers");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = Base(go.transform, "RisersFx", VibeGfx.Soft, true, 80);
        ps.transform.localRotation = Quaternion.identity;
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
        m.startColor = new ParticleSystem.MinMaxGradient(Gold, new Color(1f, 0.95f, 0.7f, 1f));
        m.gravityModifier = -0.1f;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = 0.8f;
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.4f; nz.frequency = 0.8f;
        FadeAndShrink(ps);
        return ps;
    }

    // ------------------------------------------------------------ системы удара молотка по зданию
    // без формы и со стартовой скоростью 0: скорость каждой частицы задаёт EmitDir

    // искры-чёрточки: вытянуты по полёту, быстро гаснут
    ParticleSystem MakeStrikeSparks()
    {
        var ps = Base(transform, "StrikeSparks", VibeGfx.Soft, true, 220);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.5f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        m.startColor = new ParticleSystem.MinMaxGradient(Spark, new Color(1f, 0.93f, 0.62f, 1f));
        m.gravityModifier = 1.7f;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.07f;
        r.lengthScale = 1.8f;
        FadeAndShrink(ps);
        return ps;
    }

    // щепки и крошка дерева: кувыркаются и падают
    ParticleSystem MakeStrikeSplinters()
    {
        var ps = Base(transform, "StrikeSplinters", VibeGfx.Square, false, 160);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.60f, 0.40f, 0.20f, 1f), new Color(0.88f, 0.68f, 0.40f, 1f));
        m.gravityModifier = 2.1f;
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-14f, 14f);
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0.02f, 0.65f);
        return ps;
    }

    // яркая «звезда» в точке удара: растёт и гаснет за долю секунды
    ParticleSystem MakeStrikeFlare()
    {
        var ps = Base(transform, "StrikeFlare", VibeGfx.Soft, true, 12);
        var m = ps.main;
        m.startLifetime = 0.16f; m.startSpeed = 0f; m.startSize = 1f;
        m.startColor = new Color(1f, 0.95f, 0.75f, 1f);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.55f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0.03f, 0.25f);
        return ps;
    }

    // ударное кольцо: тонкий обод быстро расходится от точки удара (смотрит на камеру)
    ParticleSystem MakeStrikePop()
    {
        var ps = Base(transform, "StrikePop", RingSprite(), true, 16);
        var m = ps.main;
        m.startLifetime = 0.3f; m.startSpeed = 0f; m.startSize = 1.6f;
        m.startColor = new Color(1f, 0.85f, 0.5f, 0.8f);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 4f), new Keyframe(1f, 1f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(0.04f, 0.3f);
        return ps;
    }

    // одна лампа на все удары: на мгновение подсвечивает стену тёплым светом
    void MakeStrikeLight()
    {
        var go = new GameObject("StrikeLight");
        go.transform.SetParent(transform, false);
        strikeLight = go.AddComponent<Light>();
        strikeLight.type = LightType.Point;
        strikeLight.color = new Color(1f, 0.78f, 0.42f, 1f);
        strikeLight.range = 4.5f;
        strikeLight.intensity = 0f;
        strikeLight.shadows = LightShadows.None;
        strikeLight.enabled = false;
    }

    // ------------------------------------------------------------ спрайт кольца
    static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var cols = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.12f);   // тонкий обод
                a = a * a * (3f - 2f * a);
                a += Mathf.Clamp01(0.86f - d) * 0.07f;                         // еле заметная заливка внутри
                cols[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        tex.SetPixels(cols);
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { ringSprite = null; }
}
