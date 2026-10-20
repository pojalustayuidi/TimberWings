using System.Collections;
using UnityEngine;

// Вешать вручную не нужно: PlayerInteraction добавит сам.
// Ритм-рубка: здесь же хранится, сколько дерева уже «надрублено» (rhythmProgress 0..1), и сила тряски зависит от качества удара.
public class TreeHits : MonoBehaviour
{
    public int done;                 // сколько ударов уже нанесено
    public float rhythmProgress;     // ритм-рубка: доля дерева, которая уже надрублена (0..1). Дерево не «залечивается», пока игрок отошёл
    Quaternion baseRot;
    Coroutine shake;
    static Material chipMat;

    void Awake() => baseRot = transform.localRotation;

    // дерево наклонили после Awake (ResourceItem.ApplyJitter): иначе после тряски оно вернулось бы прямым
    public void Rebase() { baseRot = transform.localRotation; }

    // дерево выросло заново (SetActive(true) после респауна): рубить снова с нуля
    void OnEnable() { rhythmProgress = 0f; }

    // дерево после рубки прячется (SetActive(false)) прямо посреди тряски: корутина умирает, и без этого оно возвращалось бы с наклоном
    void OnDisable()
    {
        shake = null;
        transform.localRotation = baseRot;
    }

    // strength: 1 = обычный удар, 0.5 = слабый, 1.5 = идеальный (сильнее качается и больше щепок)
    public void Shake(float strength = 1f)
    {
        if (shake != null) StopCoroutine(shake);
        shake = StartCoroutine(ShakeRoutine(strength));
        Chips(strength);
    }

    IEnumerator ShakeRoutine(float strength)
    {
        const float duration = 0.25f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = 1f - t / duration;
            float angle = Mathf.Sin(t * 60f) * 5f * strength * k;
            transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
        transform.localRotation = baseRot;
    }

    // Щепки: маленький взрыв частиц, создаётся из кода, префабы не нужны
    void Chips(float strength)
    {
        var go = new GameObject("Chips");
        go.transform.position = transform.position + Vector3.up * 1.2f;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // иначе нельзя менять duration
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = 0.6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new Color(0.65f, 0.45f, 0.25f);
        main.gravityModifier = 1.5f;

        var em = ps.emission;
        em.rateOverTime = 0f;
        int lo = Mathf.Max(3, Mathf.RoundToInt(8 * strength)), hi = Mathf.Max(lo + 1, Mathf.RoundToInt(12 * strength));
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)lo, (short)hi) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.2f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        if (chipMat == null) chipMat = VibeGfx.ParticleMat(VibeGfx.Square);   // Sprites/Default: цвет частицы = тон щепки
        if (chipMat == null) { Destroy(go); return; }                         // нет шейдера: лучше без щепок, чем розовые квадраты
        r.sharedMaterial = chipMat;

        ps.Play();
        Destroy(go, 1.5f);
    }
}