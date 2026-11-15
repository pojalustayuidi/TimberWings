// Assets/_Project/Scripts/Workers/WorkerPackFx.cs (НЕ в Editor)
// Эффекты открытия пака по редкости. Обычный класс без MonoBehaviour: его создаёт WorkerPackTable и зовёт Tease / Reveal / Tick / Reset.
// Всё рисуется кодом (спрайты PetUi плюс три своих: кольцо, столб света, блик), никаких новых ассетов.
//
// Что происходит у каждой редкости (карта выбрана, но ещё рубашкой вверх):
//   Обычный      ничего лишнего: короткая вспышка и конфетти, как раньше.
//   Необычный    короткая пауза, рубашка наливается голубым, после переворота один блик пробегает по карте.
//   Редкий       «зарядка» 0,8 с: рубашка светится лавандой, карта дрожит, внутрь летят искры, один удар «сердца».
//                После переворота: кольцо-волна, баннер «РЕДКИЙ!», золотая пыль, блик раз в 4 с.
//   Легендарный  «зарядка» 1,5 с: фон темнеет, рубашка розовеет, спираль искр, два удара «сердца» с тряской и кольцами.
//                После переворота: три кольца, взрыв осколков, столб света, вторые лучи навстречу первым, орбитальные искры, блик раз в 2,6 с.
//   Мифический   «зарядка» 2,4 с: фон темнеет до космоса, рубашка переливается радугой, четыре всё более сильных удара «сердца»,
//                спираль искр. Потом ослепительная вспышка, четыре кольца, ливень осколков, радужный баннер, радужные лучи и аура,
//                голографический отлив на карте, орбитальные звёзды без конца.
// «Уменьшить эффекты» (VibeSettings.ReduceEffects): без зарядки и тряски, частиц втрое меньше, фон не темнеет.
// Тёмный фон возвращается к светлому под белой вспышкой переворота, поэтому текст (индиго) на нём всегда читается.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

public class WorkerPackFx
{
    // ============================================================ связь со столом
    public Image dim;                                  // светлая подложка стола: стол отдаёт её сюда, эффект темнит её на зарядке
    public System.Action<float, float> onShake;        // (длительность, амплитуда) тряски окна

    // сколько секунд длится «зарядка» перед переворотом (0 = без неё)
    public static float TeaseDuration(WorkerRarity r)
    {
        if (VibeSettings.ReduceEffects) return 0f;
        switch (r)
        {
            case WorkerRarity.Uncommon: return 0.3f;
            case WorkerRarity.Rare: return 0.8f;
            case WorkerRarity.Legendary: return 1.5f;
            case WorkerRarity.Mythic: return 2.4f;
            default: return 0f;
        }
    }

    // ============================================================ состояние
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    static readonly Color Light = new Color(0.96f, 0.97f, 1f, 0.97f);
    static readonly Color Dark = new Color(0.08f, 0.07f, 0.19f, 0.985f);
    const float SweepDur = 0.75f;
    const int PoolSize = 150;

    class P
    {
        public Image img;
        public RectTransform rt;
        public bool live, rainbow;
        public int mode;                         // 0: свободный полёт, 1: по кругу вокруг карты
        public Vector2 pos, vel;
        public float drag, grav, life, max, s0, s1, spin, rot, delay, hue;
        public float ang, angVel, rad, radVel;
        public Color c0, c1;
    }

    readonly P[] ps = new P[PoolSize];
    readonly RectTransform back;
    RectTransform front, auraRt, rays2Rt, pillarRt, bannerRt, foilRt, sweepRt;
    Image aura, rays2, pillar, sweep, tint;
    TMP_Text banner;
    Sprite ringSp, beamSp, sweepSp;

    bool revealed, teaseOn, teaseSoundDone, bannerOn;
    WorkerRarity rar;
    float time, revealT, dimLevel, dimTarget, kick;
    float auraA, auraS = 1f, spiralAcc, ambAcc, orbAcc;
    float sweepT = SweepDur, nextSweep, sweepPeriod, foilW;
    int beat;
    Color auraC = Color.white;
    Vector2 center;

    // Задний слой (аура, вторые лучи, столб света) создаётся сразу, чтобы лечь под карты. Передний слой: BuildFront
    public WorkerPackFx(RectTransform parent)
    {
        back = NewRT("FxBack", parent);
        Stretch(back);

        pillar = UiKit.MakeImage(back, "Pillar", Beam, false);
        pillarRt = pillar.rectTransform;
        Anch(pillarRt, Mid, Mid, Vector2.zero, new Vector2(240f, 1500f));
        pillar.raycastTarget = false;
        pillar.gameObject.SetActive(false);

        rays2 = UiKit.MakeImage(back, "Rays2", PetUi.RaysSprite, false);
        rays2Rt = rays2.rectTransform;
        Anch(rays2Rt, Mid, Mid, Vector2.zero, new Vector2(900f, 900f));
        rays2.raycastTarget = false;
        rays2.gameObject.SetActive(false);

        aura = UiKit.MakeImage(back, "Aura", PetUi.GlowSprite, false);
        auraRt = aura.rectTransform;
        Anch(auraRt, Mid, Mid, Vector2.zero, new Vector2(560f, 560f));
        aura.color = new Color(1f, 1f, 1f, 0f);
        aura.raycastTarget = false;
    }

    // Передний слой: частицы и баннер редкости; блик на карте (создаётся тут, садится на карту в Reveal)
    public void BuildFront(RectTransform parent)
    {
        front = NewRT("FxFront", parent);
        Stretch(front);
        for (int i = 0; i < PoolSize; i++)
        {
            var im = UiKit.MakeImage(front, "P", PetUi.SparkSprite, false);
            im.raycastTarget = false;
            Anch(im.rectTransform, Mid, Mid, Vector2.zero, new Vector2(16f, 16f));
            im.gameObject.SetActive(false);
            ps[i] = new P { img = im, rt = im.rectTransform };
        }

        banner = UiKit.MakeLabel(front, "", 70f, Color.white, TextAlignmentOptions.Center, true);
        bannerRt = banner.rectTransform;
        Anch(bannerRt, Mid, Mid, new Vector2(0f, 352f), new Vector2(1200f, 96f));
        banner.enableWordWrapping = false;
        banner.raycastTarget = false;
        banner.gameObject.SetActive(false);

        // блик и голографический отлив: живут внутри маски, садятся на лицо выбранной карты
        foilRt = NewRT("Foil", back);
        foilRt.gameObject.AddComponent<RectMask2D>();
        tint = UiKit.MakeImage(foilRt, "Tint", null, false);
        Stretch(tint.rectTransform);
        tint.color = new Color(1f, 1f, 1f, 0f);
        tint.raycastTarget = false;
        sweep = UiKit.MakeImage(foilRt, "Sweep", SweepSprite, false);
        sweepRt = sweep.rectTransform;
        Anch(sweepRt, Mid, Mid, Vector2.zero, new Vector2(200f, 500f));
        sweep.color = new Color(1f, 1f, 1f, 0f);
        sweep.raycastTarget = false;
        foilRt.gameObject.SetActive(false);
    }

    // Всё в исходное: новая раздача или закрытие стола
    public void Reset()
    {
        revealed = teaseOn = teaseSoundDone = bannerOn = false;
        beat = 0;
        kick = dimLevel = dimTarget = revealT = auraA = 0f;
        spiralAcc = ambAcc = orbAcc = 0f;
        sweepT = SweepDur;
        if (front == null) return;
        for (int i = 0; i < PoolSize; i++) { ps[i].live = false; ps[i].img.gameObject.SetActive(false); }
        aura.color = new Color(1f, 1f, 1f, 0f);
        rays2.gameObject.SetActive(false);
        pillar.gameObject.SetActive(false);
        banner.gameObject.SetActive(false);
        foilRt.gameObject.SetActive(false);
        foilRt.SetParent(back, false);
        if (dim != null) dim.color = Light;
    }

    // ============================================================ зарядка (карта выбрана, лежит рубашкой вверх в центре)
    // k: 0..1 по ходу зарядки. Возвращает дрожь, добавку масштаба и цвет, которым наливается рубашка.
    public void Tease(WorkerRarity r, float k, float dt, Vector2 c, out Vector2 shake, out float scaleAdd, out Color tintOut)
    {
        rar = r;
        center = c;
        teaseOn = true;
        int lv = (int)r;
        Color acc = r == WorkerRarity.Mythic ? Hue(0f, 1f, 0.6f) : WorkerUi.RarityAccent(r);
        tintOut = acc;
        float e = k * k;

        if (!teaseSoundDone)
        {
            teaseSoundDone = true;
            if (lv >= 2 && SfxManager.Instance != null) SfxManager.Play("riser", 0.75f, Mathf.Clamp(1.6f / TeaseDuration(r), 0.67f, 1.3f));
        }

        float maxDim = r == WorkerRarity.Mythic ? 1f : r == WorkerRarity.Legendary ? 0.55f : 0f;
        dimTarget = maxDim * Mathf.Clamp01(k * 2.5f);

        auraC = acc;
        auraA = (0.12f + 0.5f * e) * Mathf.Min(1f, lv / 2f);
        auraS = 1.1f + 1.5f * e;

        // искры по спирали внутрь карты
        if (lv >= 2)
        {
            spiralAcc += (lv - 1) * 14f * e * dt * Density();
            while (spiralAcc >= 1f) { spiralAcc -= 1f; SpawnSpiral(acc, lv); }
        }

        // удары «сердца»
        float[] beats = lv >= 4 ? Beats4 : lv == 3 ? Beats3 : Beats2;
        if (lv >= 2 && beat < beats.Length && k >= beats[beat])
        {
            beat++;
            kick = 1f;
            var p = Spawn(Ring, center, 120f, 560f, 0.7f, new Color(acc.r, acc.g, acc.b, 0.7f));
            p.rainbow = lv >= 4;
            if (SfxManager.Instance != null) SfxManager.Play("thock", 0.9f, 0.45f + 0.08f * beat);
            if (onShake != null && lv >= 3) onShake(0.25f, 2.5f + 2.2f * beat);
        }

        float power = lv <= 1 ? 0.3f : (1.5f + 7f * e * (lv - 1) / 3f);
        shake = Random.insideUnitCircle * power;
        scaleAdd = kick * 0.07f + e * 0.04f;
    }

    static readonly float[] Beats2 = { 0.7f };
    static readonly float[] Beats3 = { 0.45f, 0.8f };
    static readonly float[] Beats4 = { 0.25f, 0.5f, 0.72f, 0.9f };

    void SpawnSpiral(Color acc, int lv)
    {
        var p = Spawn(PetUi.SparkSprite, center, 24f, 6f, 0.7f, Color.Lerp(acc, Color.white, 0.4f));
        p.mode = 1;
        p.ang = Random.value * Mathf.PI * 2f;
        p.rad = Random.Range(220f, 330f);
        p.radVel = -p.rad / 0.7f;
        p.angVel = 5f;
        p.rainbow = lv >= 4;
        p.hue = Random.value;
    }

    // ============================================================ переворот: карта показала лицо
    public void Reveal(WorkerRarity r, Vector2 c, RectTransform cardBody, float w, float h)
    {
        rar = r;
        center = c;
        revealed = true;
        teaseOn = false;
        revealT = 0f;
        dimTarget = 0f;
        int lv = (int)r;
        bool reduce = VibeSettings.ReduceEffects;
        float rd = Density();
        Color acc = r == WorkerRarity.Mythic ? Hue(0f, 1f, 0.6f) : WorkerUi.RarityAccent(r);

        // кольца-волны
        for (int i = 0; i < lv; i++)
        {
            var p = Spawn(Ring, c, 60f, 520f + 90f * i, 0.9f, new Color(acc.r, acc.g, acc.b, 0.8f));
            p.rainbow = lv >= 4;
            p.hue = i * 0.25f;
            if (i > 0) Delay(p, 0.12f * i);
        }

        // мелкие блёстки у всех, осколки у Легендарного и Мифического
        int twinkles = Mathf.RoundToInt((6 + 6 * lv) * rd);
        for (int i = 0; i < twinkles; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            var p = Spawn(PetUi.SparkSprite, c, Random.Range(14f, 26f), 4f, Random.Range(0.8f, 1.4f), Color.Lerp(acc, Color.white, Random.value * 0.6f));
            p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(180f, 520f);
            p.drag = 1.6f;
            p.grav = 120f;
            p.spin = Random.Range(-200f, 200f);
            p.rainbow = lv >= 4 && Random.value < 0.6f;
            p.hue = Random.value;
        }
        if (lv >= 3)
        {
            int shards = Mathf.RoundToInt((lv == 3 ? 24 : 42) * rd);
            for (int i = 0; i < shards; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var p = Spawn(PetUi.SparkSprite, c, Random.Range(26f, 44f), 6f, Random.Range(1.0f, 1.8f), lv == 3 ? new Color(1f, 0.85f, 0.4f, 1f) : Color.white);
                p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(320f, 760f);
                p.drag = 1.9f;
                p.grav = 220f;
                p.spin = Random.Range(-420f, 420f);
                p.rainbow = lv >= 4;
                p.hue = Random.value;
            }
        }

        // столб света
        if (lv >= 3) pillar.gameObject.SetActive(true);
        if (lv >= 3) rays2.gameObject.SetActive(true);

        // баннер редкости
        if (lv >= 2)
        {
            bannerOn = true;
            banner.text = WorkerText.Rarity(r).ToUpperInvariant() + "!";
            banner.color = new Color(1f, 1f, 1f, 0f);
            banner.gameObject.SetActive(true);
        }

        // блик и отлив на карте
        if (lv >= 1 && cardBody != null)
        {
            foilW = w;
            foilRt.SetParent(cardBody, false);
            Stretch(foilRt);
            foilRt.offsetMin = new Vector2(5f, 5f);
            foilRt.offsetMax = new Vector2(-5f, -5f);
            foilRt.SetAsLastSibling();
            foilRt.gameObject.SetActive(true);
            sweepRt.sizeDelta = new Vector2(w * 0.7f, h * 1.5f);
            sweepT = -0.35f;
            sweepPeriod = lv == 1 ? 9999f : lv == 2 ? 4f : lv == 3 ? 2.6f : 1.8f;
            nextSweep = sweepPeriod;
            tint.color = new Color(1f, 1f, 1f, 0f);
        }

        // звук и тряска окна
        if (SfxManager.Instance != null)
        {
            if (lv == 2) SfxManager.Play("level", 0.9f, 1f);
            else if (lv == 3) { SfxManager.Play("quest", 1f, 1f); SfxManager.Play("boom", 0.8f, 1f); }
            else if (lv == 4) { SfxManager.Play("boom", 1f, 0.8f); SfxManager.Play("silver", 0.9f, 1f); SfxManager.Play("quest", 0.8f, 1.2f); }
        }
        if (!reduce && lv >= 2 && onShake != null)
            onShake(lv == 2 ? 0.45f : lv == 3 ? 0.7f : 1.1f, lv == 2 ? 9f : lv == 3 ? 14f : 20f);
    }

    // ============================================================ каждый кадр
    public void Tick(float dt, Vector2 c)
    {
        time += dt;
        center = c;
        kick = Mathf.Max(0f, kick - dt * 4f);

        // фон: темнеет на зарядке, светлеет под вспышкой переворота
        if (dim != null)
        {
            dimLevel = Mathf.MoveTowards(dimLevel, dimTarget, dt * (revealed ? 2.5f : 3f));
            dim.color = Color.Lerp(Light, Dark, dimLevel);
        }

        if (revealed) TickRevealed(dt);

        // аура, лучи и столб следуют за картой
        auraRt.anchoredPosition = center;
        auraRt.localScale = Vector3.one * auraS;
        aura.color = new Color(auraC.r, auraC.g, auraC.b, auraA);
        if (rays2.gameObject.activeSelf) rays2Rt.anchoredPosition = center;
        if (pillar.gameObject.activeSelf) pillarRt.anchoredPosition = center;

        TickParticles(dt);
    }

    void TickRevealed(float dt)
    {
        revealT += dt;
        int lv = (int)rar;
        bool myth = lv >= 4;
        float rd = Density();
        float fadeIn = Mathf.Clamp01(revealT / 0.4f);
        Color acc = myth ? Hue(0f, 1f, 0.6f) : WorkerUi.RarityAccent(rar);
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * (myth ? 3.2f : 2.2f));

        // аура
        auraC = acc;
        if (lv >= 2) { auraA = (0.20f + 0.04f * lv + 0.10f * pulse) * fadeIn; auraS = 1.7f + 0.25f * pulse + (lv >= 3 ? 0.5f : 0f); }
        else { auraA = 0.12f * fadeIn; auraS = 1.4f; }

        // вторые лучи навстречу первым и столб света
        if (lv >= 3)
        {
            rays2Rt.localRotation = Quaternion.Euler(0f, 0f, time * (myth ? -34f : 24f));
            rays2.color = new Color(acc.r, acc.g, acc.b, (myth ? 0.34f : 0.26f) * fadeIn);
            float pa = Mathf.Lerp(0.8f, 0.22f, Mathf.Clamp01(revealT / 1.2f)) * Mathf.Clamp01(revealT / 0.15f);
            pillar.color = new Color(acc.r, acc.g, acc.b, pa * (0.85f + 0.15f * pulse));
            pillarRt.localScale = new Vector3(1f + 0.12f * pulse, 1f, 1f);
        }

        // фоновые частицы: пылинки вверх и орбитальные звёзды
        float moteRate = lv == 2 ? 4f : lv == 3 ? 6f : lv >= 4 ? 10f : 0f;
        float orbitRate = lv == 3 ? 8f : lv >= 4 ? 14f : 0f;
        ambAcc += moteRate * rd * dt;
        while (ambAcc >= 1f)
        {
            ambAcc -= 1f;
            var p = Spawn(PetUi.SparkSprite, center + new Vector2(Random.Range(-150f, 150f), Random.Range(-190f, 120f)), Random.Range(10f, 18f), 2f, 1.5f, Color.Lerp(acc, Color.white, 0.5f));
            p.vel = new Vector2(Random.Range(-12f, 12f), Random.Range(30f, 70f));
            p.rainbow = myth;
            p.hue = Random.value;
        }
        orbAcc += orbitRate * rd * dt;
        while (orbAcc >= 1f)
        {
            orbAcc -= 1f;
            var p = Spawn(PetUi.SparkSprite, center, 22f, 8f, 2.4f, Color.Lerp(acc, Color.white, 0.4f));
            p.mode = 1;
            p.ang = Random.value * Mathf.PI * 2f;
            p.rad = Random.Range(190f, 290f);
            p.angVel = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.7f, 1.4f);
            p.spin = Random.Range(-150f, 150f);
            p.rainbow = myth;
            p.hue = Random.value;
        }

        // баннер: «впечатывается» крупным и садится на место
        if (bannerOn)
        {
            float k = Mathf.Clamp01(revealT / 0.45f);
            float sc = Mathf.LerpUnclamped(2.8f, 1f, UiDraw.EaseOutBack(k));
            if (k >= 1f) sc *= 1f + 0.025f * Mathf.Sin(time * 3f);
            bannerRt.localScale = Vector3.one * sc;
            banner.characterSpacing = Mathf.Lerp(50f, 0f, UiDraw.EaseOutCubic(k));
            float a = Mathf.Clamp01(k * 3f);
            if (myth)
            {
                banner.enableVertexGradient = true;
                banner.colorGradient = new VertexGradient(Hue(0f, a, 0.95f), Hue(0.25f, a, 0.95f), Hue(0.5f, a, 0.95f), Hue(0.75f, a, 0.95f));
                banner.color = Color.white;
            }
            else
            {
                banner.enableVertexGradient = false;
                var tc = WorkerUi.RarityText(rar);
                banner.color = new Color(tc.r, tc.g, tc.b, a);
            }
        }

        // блик по карте и радужный отлив у мифика
        if (foilRt.gameObject.activeSelf)
        {
            Color sc2 = myth ? Color.Lerp(Color.white, Hue(0f, 1f, 0.5f), 0.5f) : Color.white;
            if (sweepT < SweepDur)
            {
                sweepT += dt;
                if (sweepT >= 0f)
                {
                    float k = Mathf.Clamp01(sweepT / SweepDur);
                    sweepRt.anchoredPosition = new Vector2(Mathf.Lerp(-foilW * 0.9f, foilW * 0.9f, UiDraw.EaseOutCubic(k)), 0f);
                    sweep.color = new Color(sc2.r, sc2.g, sc2.b, 0.6f * Mathf.Sin(k * Mathf.PI));
                }
            }
            else
            {
                sweep.color = new Color(1f, 1f, 1f, 0f);
                nextSweep -= dt;
                if (nextSweep <= 0f) { sweepT = 0f; nextSweep = sweepPeriod; }
            }
            if (myth)
            {
                var hc = Hue(0f, 1f, 0.5f);
                tint.color = new Color(hc.r, hc.g, hc.b, (0.13f + 0.06f * pulse) * fadeIn);
            }
        }
    }

    void TickParticles(float dt)
    {
        for (int i = 0; i < PoolSize; i++)
        {
            var p = ps[i];
            if (!p.live) continue;
            if (p.delay > 0f)
            {
                p.delay -= dt;
                if (p.delay > 0f) continue;
                p.img.gameObject.SetActive(true);
            }
            p.life -= dt;
            if (p.life <= 0f) { p.live = false; p.img.gameObject.SetActive(false); continue; }
            float k = 1f - p.life / p.max;
            if (p.mode == 0)
            {
                p.vel *= 1f - Mathf.Min(1f, p.drag * dt);
                p.vel.y -= p.grav * dt;
                p.pos += p.vel * dt;
            }
            else
            {
                p.ang += p.angVel * dt;
                p.rad = Mathf.Max(0f, p.rad + p.radVel * dt);
                p.pos = center + new Vector2(Mathf.Cos(p.ang), Mathf.Sin(p.ang)) * p.rad;
            }
            p.rt.anchoredPosition = p.pos;
            float size = Mathf.Lerp(p.s0, p.s1, k);
            p.rt.sizeDelta = new Vector2(size, size);
            p.rot += p.spin * dt;
            p.rt.localRotation = Quaternion.Euler(0f, 0f, p.rot);

            Color c = p.rainbow ? Hue(p.hue, 1f, 0.55f) : Color.Lerp(p.c0, p.c1, k);
            float fade = (k < 0.12f ? k / 0.12f : 1f) * Mathf.Clamp01(p.life / p.max * 1.6f);
            c.a = (p.rainbow ? p.c0.a : Mathf.Lerp(p.c0.a, p.c1.a, k)) * fade;
            p.img.color = c;
        }
    }

    // ============================================================ частицы
    P Spawn(Sprite sp, Vector2 pos, float s0, float s1, float life, Color c)
    {
        P p = null;
        for (int i = 0; i < PoolSize; i++) { if (!ps[i].live) { p = ps[i]; break; } }
        if (p == null) p = ps[0];                 // пул полон: перезаписываем самую старую в списке, лучше чем потерять важное кольцо
        p.live = true;
        p.mode = 0;
        p.rainbow = false;
        p.pos = pos;
        p.vel = Vector2.zero;
        p.drag = p.grav = p.spin = p.rot = p.delay = p.hue = 0f;
        p.ang = p.angVel = p.rad = p.radVel = 0f;
        p.life = p.max = life;
        p.s0 = s0;
        p.s1 = s1;
        p.c0 = c;
        p.c1 = c;
        p.img.sprite = sp;
        p.img.color = c;
        p.rt.anchoredPosition = pos;
        p.rt.sizeDelta = new Vector2(s0, s0);
        p.rt.localRotation = Quaternion.identity;
        p.img.gameObject.SetActive(true);
        return p;
    }

    static void Delay(P p, float d)
    {
        p.delay = d;
        p.img.gameObject.SetActive(false);
    }

    float Density() { return VibeSettings.ReduceEffects ? 0.34f : 1f; }

    // радужный цвет: off сдвигает оттенок
    Color Hue(float off, float a, float sat = 0.7f)
    {
        var c = Color.HSVToRGB(Mathf.Repeat(time * 0.35f + off, 1f), sat, 1f);
        c.a = a;
        return c;
    }

    // ============================================================ спрайты, нарисованные кодом
    Sprite Ring { get { if (ringSp == null) ringSp = MakeTex("Fx_Ring", 128, 128, (u, v) => { float r = Mathf.Sqrt(u * u + v * v); float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.14f); return a * a; }); return ringSp; } }

    Sprite Beam { get { if (beamSp == null) beamSp = MakeTex("Fx_Beam", 32, 128, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 2.5f) * Mathf.Clamp01(1f - v * v)); return beamSp; } }

    Sprite SweepSprite { get { if (sweepSp == null) sweepSp = MakeTex("Fx_Sweep", 64, 64, (u, v) => { float a = Mathf.Clamp01(1f - Mathf.Abs(u * 0.9f + v * 0.5f) / 0.35f); return a * a; }); return sweepSp; } }

    static Sprite MakeTex(string name, int w, int h, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float a = Mathf.Clamp01(alpha(u, v));
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        return s;
    }
}
