// Assets/_Project/Scripts/Build/Hut/HutLife.cs   (НЕ в папке Editor!)
// «Жизнь» хижины и пилорамы: мягкие облачка дыма из трубы (белые «шарики» как на референсе Cloud Candy),
// флаги и вымпелы на ветру (цепочка шарниров, волна бежит вниз), покачивающиеся фонари, крюк крана и вывески.
// Вешается на объект стадии (Stage1/2/3): пока стадия выключена, ничего не считается. Заполняет генератор (меню Tools > BuilderSim > Хижина лесоруба v2).
// «Уменьшить эффекты» (VibeSettings.ReduceEffects): дыма вдвое меньше, флаги качаются слабее.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Hut Life")]
public class HutLife : MonoBehaviour
{
    [System.Serializable]
    public class ClothChain
    {
        [Tooltip("Шарниры сверху вниз: каждый следующий ребёнок предыдущего")]
        public Transform[] segs;
        public float amp = 10f;
        public float phase;
    }

    [Header("Флаги и вымпелы")]
    public ClothChain[] cloths = new ClothChain[0];

    [Header("Качающиеся предметы (фонари, крюк, вывески)")]
    public Transform[] swingers = new Transform[0];
    public float swingAmp = 3.5f;
    public float swingSpeed = 1.4f;

    [Header("Дым из трубы")]
    [Tooltip("Где рождаются облачка. Пусто = дыма нет")]
    public Transform smokePoint;
    public Mesh puffMesh;
    public Material smokeMat;
    [Min(0.1f)] public float smokeInterval = 0.6f;
    public float smokeLife = 3.6f;
    public float smokeRise = 0.85f;
    public Vector3 smokeWind = new Vector3(0.35f, 0f, 0.1f);
    public float smokeMaxSize = 1.0f;
    [Range(2, 14)] public int smokeMax = 8;

    // ---- служебное
    class Puff { public Transform t; public float age = -1f; public Vector3 vel; public float spin; public float size; }
    readonly List<Puff> puffs = new List<Puff>();
    Transform smokeRoot;
    float nextPuff;
    Quaternion[] swingBase;
    float seed;

    void Awake()
    {
        seed = Random.value * 10f;
        CaptureSwing();
    }

    void CaptureSwing()
    {
        if (swingers == null) { swingBase = new Quaternion[0]; return; }
        swingBase = new Quaternion[swingers.Length];
        for (int i = 0; i < swingers.Length; i++) swingBase[i] = swingers[i] != null ? swingers[i].localRotation : Quaternion.identity;
    }

    void OnDisable()
    {
        // стадию выключили: возвращаем качающееся в покой, живые облачка гасим (чтобы не висели в воздухе)
        if (swingers != null && swingBase != null)
            for (int i = 0; i < swingers.Length && i < swingBase.Length; i++)
                if (swingers[i] != null) swingers[i].localRotation = swingBase[i];
        for (int i = 0; i < puffs.Count; i++)
            if (puffs[i].t != null) { puffs[i].age = -1f; puffs[i].t.gameObject.SetActive(false); }
    }

    void OnDestroy()
    {
        if (smokeRoot != null) Destroy(smokeRoot.gameObject);
    }

    void Update()
    {
        float t = Time.time + seed;
        bool reduce = VibeSettings.ReduceEffects;

        // ---- флаги: волна бежит вниз по сегментам
        float flagMul = reduce ? 0.5f : 1f;
        if (cloths != null)
            for (int c = 0; c < cloths.Length; c++)
            {
                var ch = cloths[c];
                if (ch == null || ch.segs == null) continue;
                for (int i = 0; i < ch.segs.Length; i++)
                {
                    var s = ch.segs[i];
                    if (s == null) continue;
                    float k = 0.55f + i * 0.28f;
                    float yaw = Mathf.Sin(t * 2.7f - i * 0.95f + ch.phase) * ch.amp * k * flagMul;
                    float roll = Mathf.Sin(t * 1.8f - i * 0.7f + ch.phase * 1.3f) * ch.amp * 0.3f * flagMul;
                    s.localRotation = Quaternion.Euler(0f, yaw, roll);
                }
            }

        // ---- фонари и крюк: плавное покачивание вокруг своей позы
        if (swingers != null && swingBase != null)
            for (int i = 0; i < swingers.Length && i < swingBase.Length; i++)
            {
                if (swingers[i] == null) continue;
                float a = Mathf.Sin(t * swingSpeed + i * 1.7f) * swingAmp;
                float b = Mathf.Sin(t * swingSpeed * 0.73f + i * 0.9f) * swingAmp * 0.6f;
                swingers[i].localRotation = swingBase[i] * Quaternion.Euler(b, 0f, a);
            }

        // ---- дым
        TickSmoke(reduce);
    }

    void TickSmoke(bool reduce)
    {
        if (smokePoint == null || puffMesh == null || smokeMat == null) return;

        if (Time.time >= nextPuff)
        {
            nextPuff = Time.time + smokeInterval * (reduce ? 2f : 1f) * Random.Range(0.8f, 1.3f);
            Spawn();
        }

        float dt = Time.deltaTime;
        for (int i = 0; i < puffs.Count; i++)
        {
            var p = puffs[i];
            if (p.age < 0f || p.t == null) continue;
            p.age += dt;
            float k = p.age / smokeLife;
            if (k >= 1f) { p.age = -1f; p.t.gameObject.SetActive(false); continue; }
            // ветер усиливается с высотой: дым красиво «отгибается»
            p.t.position += (p.vel + smokeWind * (0.4f + k * 1.2f)) * dt;
            // растёт быстро в начале, в конце тает (уменьшается): мягкий «ватный» вид без прозрачности
            float grow = Mathf.SmoothStep(0.25f, 1f, Mathf.Clamp01(k * 2.2f));
            float fade = 1f - Mathf.SmoothStep(0.7f, 1f, k);
            p.t.localScale = Vector3.one * (p.size * grow * fade);
            p.t.Rotate(0f, p.spin * dt, 0f, Space.World);
        }
    }

    void Spawn()
    {
        Puff p = null;
        for (int i = 0; i < puffs.Count; i++)
            if (puffs[i].age < 0f) { p = puffs[i]; break; }
        if (p == null)
        {
            if (puffs.Count >= smokeMax) return;
            if (smokeRoot == null)
            {
                // корень дыма не внутри стадии: BuildFx двигает и масштабирует стадии, дым не должен прыгать вместе с ними
                smokeRoot = new GameObject("HutSmoke").transform;
            }
            var go = new GameObject("Puff");
            go.transform.SetParent(smokeRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = puffMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = smokeMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            p = new Puff { t = go.transform };
            puffs.Add(p);
        }
        p.age = 0f;
        p.size = smokeMaxSize * Random.Range(0.75f, 1.15f);
        p.vel = new Vector3(Random.Range(-0.08f, 0.08f), smokeRise * Random.Range(0.85f, 1.15f), Random.Range(-0.08f, 0.08f));
        p.spin = Random.Range(-40f, 40f);
        p.t.position = smokePoint.position + new Vector3(Random.Range(-0.08f, 0.08f), 0f, Random.Range(-0.08f, 0.08f));
        p.t.rotation = Random.rotation;
        p.t.localScale = Vector3.one * 0.01f;
        p.t.gameObject.SetActive(true);
    }
}
