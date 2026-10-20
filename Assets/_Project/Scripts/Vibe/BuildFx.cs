// Assets/_Project/Scripts/Vibe/BuildFx.cs (НЕ в Editor)
// Эффекты стройки. ImpactFx сам вешает этот компонент на каждый BuildingController в сцене, руками ничего добавлять не нужно.
//  - пока идёт стройка: «стук молотка», искры, щепки и пыль на площадке, камера чуть вздрагивает;
//  - стадия готова:   облако пыли, золотая волна по земле, поднимающиеся искры, «отскок» здания;
//  - стройка завершена: то же, но крупнее, двойная волна и сильнее тряска;
//  - новая стадия НЕ включается разом, а собирается по деталям: каждая деталь прилетает сверху, вырастает с отскоком и встаёт на место
//    (снизу вверх, при посадке стук, искры и пыль), в конце общая вспышка и отскок здания. Кто включил стадию (HutStages, ShopStages),
//    не важно: мы смотрим, какой объект стадии включился в кадре сдачи, и успеваем спрятать детали до отрисовки (LateUpdate).
//    Пропуск показа сразу достраивает здание. «Уменьшить эффекты»: стадия включается мгновенно, как раньше;
//  - показ стадии (GameEvents.BuildRevealStarted): пока камера держит здание в кадре, на площадке поднимается пыль,
//    а у последней стадии идут ещё несколько вспышек с «отскоком» (GameBalance.RevealDust*, RevealFinalBurst*).
//    «Уменьшить эффекты»: дополнительных пыли и вспышек нет, остаётся только основная вспышка.
// Звук играет SfxManager (он уже слушает StageBuilt и Finished).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildFxHook : MonoBehaviour
{
    BuildingController bc;
    float tickAt;
    Coroutine pop, reveal;
    Vector3 popBase;        // масштаб здания до отскока (чтобы перекрытие отскоков не копило ошибку)
    Transform popTarget;

    // сборка по деталям
    class Piece
    {
        public Transform t;
        public Vector3 pos, scale, drop;
        public Quaternion rot, tilt;
        public float delay, key;
        public bool landed;
    }
    readonly List<GameObject> cand = new List<GameObject>();      // объекты стадий, за включением которых следим
    readonly List<GameObject> fresh = new List<GameObject>();     // включились в этом кадре
    bool[] prevActive = new bool[0];
    float candAt;
    readonly List<Piece> pieces = new List<Piece>();
    readonly HashSet<Transform> tmpSet = new HashSet<Transform>();
    bool asmOn;
    float asmStart, lastLandFx;
    bool pendBurst, endBurst;                                      // вспышка просится в этом кадре / отложена до конца сборки
    float pendPower, pendShake;

    void Awake() { bc = GetComponent<BuildingController>(); }

    void OnEnable()
    {
        if (bc == null) bc = GetComponent<BuildingController>();
        if (bc == null) return;
        bc.StageBuilt += OnStage;
        bc.Finished += OnFinished;
        GameEvents.BuildRevealStarted += OnRevealStarted;
    }

    void OnDisable()
    {
        GameEvents.BuildRevealStarted -= OnRevealStarted;
        FinishAssemble(false);
        StopPop();
        reveal = null;
        if (bc == null) return;
        bc.StageBuilt -= OnStage;
        bc.Finished -= OnFinished;
    }

    // точка эффектов: стадии здания (если есть), иначе сама площадка
    Transform Anchor
    {
        get { return (bc != null && bc.stages != null) ? bc.stages.transform : transform; }
    }

    void Update()
    {
        if (bc == null || !bc.IsBuilding || ImpactFx.Instance == null) return;
        if (Time.time < tickAt) return;
        tickAt = Time.time + Random.Range(0.26f, 0.38f);
        Vector3 p = Anchor.position + new Vector3(Random.Range(-0.9f, 0.9f), 0f, Random.Range(-0.9f, 0.9f));
        ImpactFx.Instance.HammerTick(p);
        if (!VibeSettings.ReduceEffects) CameraFollow.Shake(0.015f, 0.08f);
    }

    void OnStage()
    {
        if (bc != null && bc.IsFinished) return;     // последний этап отыграет OnFinished
        QueueBurst(1f, 0.07f);
    }

    void OnFinished() { QueueBurst(1.7f, 0.13f); }

    // вспышка не сразу: если стадия сейчас начнёт собираться по деталям, вспышка встанет в конец сборки (решает LateUpdate этого же кадра)
    void QueueBurst(float power, float shake)
    {
        pendBurst = true;
        pendPower = power;
        pendShake = shake;
    }

    // ---------- сборка стадии по деталям ----------
    void LateUpdate()
    {
        if (bc == null) return;

        // список объектов стадий обновляем раз в пару секунд (BuildStageLink и вид появляются не сразу) и только вне показа
        if (!bc.IsRevealing && Time.time >= candAt)
        {
            RefreshCandidates();
            candAt = Time.time + 2f;
        }

        // что включилось с прошлого кадра (во время показа: значит, это новая стадия)
        fresh.Clear();
        for (int i = 0; i < cand.Count; i++)
        {
            bool a = cand[i] != null && cand[i].activeSelf;
            if (a && !prevActive[i] && bc.IsRevealing) fresh.Add(cand[i]);
            prevActive[i] = a;
        }
        if (fresh.Count > 0 && !VibeSettings.ReduceEffects) BeginAssemble();

        if (asmOn) TickAssemble();

        if (pendBurst)
        {
            pendBurst = false;
            if (asmOn) { endBurst = true; }                 // вспышка и отскок здания в самом конце сборки
            else Burst(pendPower, pendShake);
        }
    }

    void RefreshCandidates()
    {
        cand.Clear();
        if (bc.stages != null)
            for (int n = 1; n <= 3; n++)
                if (bc.stages.HasStage(n)) cand.Add(bc.stages.GetStageObject(n));
        var link = GetComponent<BuildStageLink>();
        if (link != null && link.shop != null)
        {
            if (link.shop.stage2 != null) cand.Add(link.shop.stage2);
            if (link.shop.stage3 != null) cand.Add(link.shop.stage3);
        }
        prevActive = new bool[cand.Count];
        for (int i = 0; i < cand.Count; i++) prevActive[i] = cand[i] != null && cand[i].activeSelf;
    }

    void BeginAssemble()
    {
        FinishAssemble(false);
        pieces.Clear();
        tmpSet.Clear();

        // детали: все меши включившихся объектов (кроме слитых в статический батч: те двигать нельзя)
        for (int i = 0; i < fresh.Count; i++)
        {
            var rs = fresh[i].GetComponentsInChildren<Renderer>(false);
            for (int j = 0; j < rs.Length; j++)
            {
                var r = rs[j];
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;   // частицы, следы и линии не трогаем
                if (r.isPartOfStaticBatch) continue;
                tmpSet.Add(r.transform);
            }
        }

        bool fin = bc.IsFinished;
        float h = fin ? GameBalance.RevealAssembleDropFinal : GameBalance.RevealAssembleDrop;
        float tiltMax = GameBalance.RevealAssembleTilt;
        foreach (var t in tmpSet)
        {
            // вложенную деталь не двигаем отдельно: она едет вместе с родителем
            bool nested = false;
            for (var p = t.parent; p != null; p = p.parent)
                if (tmpSet.Contains(p)) { nested = true; break; }
            if (nested) continue;

            var r = t.GetComponent<Renderer>();
            var pc = new Piece();
            pc.t = t;
            pc.pos = t.localPosition;
            pc.rot = t.localRotation;
            pc.scale = t.localScale;
            pc.tilt = pc.rot * Quaternion.Euler(Random.Range(-tiltMax, tiltMax), Random.Range(-tiltMax * 1.5f, tiltMax * 1.5f), Random.Range(-tiltMax, tiltMax));
            Vector3 up = Vector3.up * (h * Random.Range(0.8f, 1.2f));
            pc.drop = t.parent != null ? t.parent.InverseTransformVector(up) : up;
            pc.key = (r != null ? r.bounds.min.y : t.position.y) + Random.value * 0.15f;   // снизу вверх, с небольшой случайностью
            pieces.Add(pc);
        }
        tmpSet.Clear();
        if (pieces.Count == 0) return;

        pieces.Sort((a, b) => a.key.CompareTo(b.key));
        float span = (fin ? GameBalance.RevealTimeFinal : GameBalance.RevealTime) * GameBalance.RevealAssembleShare;
        float room = Mathf.Max(0f, span - GameBalance.RevealAssemblePieceTime);
        int n = pieces.Count;
        for (int i = 0; i < n; i++)
        {
            pieces[i].delay = n > 1 ? room * i / (n - 1) : 0f;
            pieces[i].t.localScale = pieces[i].scale * 0.001f;   // спрятана до своей очереди (не ровно ноль: нулевой масштаб ругается физика)
        }
        asmStart = Time.time + GameBalance.RevealAssembleDelay;
        asmOn = true;
    }

    void TickAssemble()
    {
        // показ пропущен или кончился (в том числе стройку выключили): достраиваем всё сразу
        if (!bc.IsRevealing || bc.RevealSkipped) { FinishAssemble(true); return; }

        float el = Time.time - asmStart;
        float dur = Mathf.Max(0.05f, GameBalance.RevealAssemblePieceTime);
        bool all = true;
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p.landed) continue;
            if (p.t == null) { p.landed = true; continue; }

            float k = (el - p.delay) / dur;
            if (k <= 0f) { all = false; continue; }       // ещё не её очередь: остаётся спрятанной
            if (k >= 1f)
            {
                Restore(p);
                p.landed = true;
                OnLand(p);
                continue;
            }
            all = false;
            float m = UiDraw.EaseOutCubic(k);
            p.t.localScale = p.scale * EaseOutBack(k);                 // вырастает с небольшим перелётом
            p.t.localPosition = p.pos + p.drop * (1f - m);             // падает сверху
            p.t.localRotation = Quaternion.Slerp(p.tilt, p.rot, m);    // выравнивается
        }
        if (all) FinishAssemble(true);
    }

    static void Restore(Piece p)
    {
        p.t.localPosition = p.pos;
        p.t.localRotation = p.rot;
        p.t.localScale = p.scale;
    }

    // деталь встала на место: стук, искры и пыль (не чаще RevealLandFxInterval, чтобы не сыпалось на каждую из сотни деталей)
    void OnLand(Piece p)
    {
        if (Time.time - lastLandFx < GameBalance.RevealLandFxInterval) return;
        lastLandFx = Time.time;
        if (ImpactFx.Instance != null) ImpactFx.Instance.HammerTick(p.t.position);
        SfxManager.Play("thock", 0.35f, Random.Range(0.9f, 1.2f));
        CameraFollow.Shake(0.012f, 0.06f);
    }

    // конец сборки: все детали на своих местах (всегда, даже если прервали). fire: сыграть отложенную общую вспышку
    void FinishAssemble(bool fire)
    {
        if (!asmOn) return;
        asmOn = false;
        for (int i = 0; i < pieces.Count; i++)
            if (pieces[i].t != null) Restore(pieces[i]);
        pieces.Clear();
        bool burst = endBurst;
        endBurst = false;
        if (fire && burst) Burst(pendPower, pendShake);
    }

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    void Burst(float power, float shake)
    {
        if (ImpactFx.Instance != null) ImpactFx.Instance.StageBurst(Anchor.position, power);
        if (!VibeSettings.ReduceEffects)
        {
            CameraFollow.Shake(shake, 0.25f);
            if (bc != null && bc.stages != null)
            {
                StopPop();
                pop = StartCoroutine(Pop(bc.stages.transform, power));
            }
        }
    }

    // ---------- показ стадии ----------
    void OnRevealStarted(BuildingController b)
    {
        if (b == null || b != bc || VibeSettings.ReduceEffects) return;
        if (reveal != null) StopCoroutine(reveal);
        reveal = StartCoroutine(RevealFx());
    }

    // пока идёт показ: облачка пыли вокруг здания; у последней стадии ещё несколько вспышек с отскоком
    IEnumerator RevealFx()
    {
        bool fin = bc != null && bc.IsFinished;
        float dustAt = Time.time + GameBalance.RevealDustInterval;
        float burstAt = Time.time + GameBalance.RevealFinalBurstInterval;
        int burstsLeft = fin ? GameBalance.RevealFinalBursts : 0;
        int burstNo = 0;

        while (bc != null && bc.IsRevealing)
        {
            if (VibeSettings.ReduceEffects) break;   // настройку могли включить посреди показа

            if (asmOn) { burstAt = Time.time + GameBalance.RevealFinalBurstInterval; dustAt = Time.time + GameBalance.RevealDustInterval; }   // пыль даёт сама сборка
            else if (burstsLeft > 0 && Time.time >= burstAt)
            {
                burstsLeft--;
                burstNo++;
                burstAt = Time.time + GameBalance.RevealFinalBurstInterval;
                Burst(Mathf.Max(0.8f, 1.6f - 0.2f * burstNo), 0.06f);
            }
            else if (Time.time >= dustAt && ImpactFx.Instance != null)
            {
                dustAt = Time.time + GameBalance.RevealDustInterval;
                Vector3 p = Anchor.position + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                ImpactFx.Instance.StageBurst(p, 0.4f);   // маленькое облачко: пыль, искры, щепки
            }
            yield return null;
        }
        reveal = null;
    }

    void StopPop()
    {
        if (pop != null) StopCoroutine(pop);
        pop = null;
        if (popTarget != null) popTarget.localScale = popBase;   // прерванный отскок возвращает масштаб
        popTarget = null;
    }

    // «отскок» здания: чуть вырастает и возвращается (в исходный масштаб всегда, даже если прервали)
    IEnumerator Pop(Transform t, float power)
    {
        Vector3 baseScale = t.localScale;
        popBase = baseScale;
        popTarget = t;
        float amp = 0.07f * power;
        const float dur = 0.4f;
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            if (t == null) yield break;
            float k = e / dur;
            float s = 1f + amp * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
            t.localScale = baseScale * s;
            yield return null;
        }
        if (t != null) t.localScale = baseScale;
        popTarget = null;
        pop = null;
    }

    void OnDestroy() { }
}
