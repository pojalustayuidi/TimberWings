// Assets/_Project/Scripts/Resource/TreeGrowthVisual.cs (НЕ в Editor)
// Визуал «падение → пень → росток → молодое → зрелое» для срубленного дерева. Вешать вручную не нужно:
// создаётся из ResourceItem.ChopDown() и живёт рядом с деревом (как соседний объект, НЕ ребёнок).
//
// Как это устроено и почему совместимо со старым кодом:
//   - Настоящее дерево по-прежнему просто SetActive(false) и SetActive(true) (TreeRespawn, ResourceItem.Active, подсветка, поиск цели: всё как раньше).
//   - Пока дерево спрятано, вместо него рисуется «двойник»: те же меши и материалы, тот же трансформ, но масштаб растёт. Коллайдеров у него нет.
//   - В последнем кадре отроста TreeRespawn включает настоящее дерево и выключает двойника в ОДНОМ Update. Масштаб двойника к этому
//     моменту ровно равен масштабу дерева (кривая заканчивается в 1 с нулевой скоростью), качание ветра идёт в той же фазе: мерцания нет.
//   - Двойник создаётся один раз на дерево, дальше только включается и выключается: аллокаций на каждую рубку нет.
//   - Пока двойник выключен (то есть почти всё время), он ничего не стоит: у него нет Update.
using System.Collections.Generic;
using UnityEngine;

public class TreeGrowthVisual : MonoBehaviour
{
    enum Phase { Idle, Fall, Grow }

    ResourceItem item;
    Phase phase;
    float progress;                       // доля роста 0..1 (приходит из TreeRespawn)
    float stumpBlend = 1f;                // 1 = росток стоит на срезе пня, 0 = на земле (пень ушёл в землю)

    // поза зрелого дерева (локальная, в родителе дерева)
    Vector3 restPos, restScale;
    Quaternion restRot;

    // падение
    float fallT, growT;
    Quaternion fallStartRot;
    Vector3 fallAxis;

    WindAgent wind;
    readonly List<Renderer> rends = new List<Renderer>(4);

    // ------------------------------------------------------------ создание
    public static TreeGrowthVisual GetOrCreate(ResourceItem item)
    {
        var g = item.Growth;
        if (g != null) return g;

        var go = new GameObject(item.name + "_Grow");
        go.SetActive(false);
        go.layer = item.gameObject.layer;
        go.transform.SetParent(item.transform.parent, false);

        g = go.AddComponent<TreeGrowthVisual>();
        g.item = item;
        g.BuildCopy();
        item.Growth = g;
        return g;
    }

    // LOD1 и LOD2 (деревья-виды с LODGroup) не копируем: у двойника нет LODGroup, иначе он рисовал бы все три уровня сразу
    static bool Skip(string n) { return n == "Outline" || n == "Hull" || n == "HighlightRing" || n == "ResHint_Tag" || n == "LOD1" || n == "LOD2"; }

    // копия видимых мешей дерева: трансформ относительно корня дерева, те же mesh и sharedMaterials (листва уже подкрашена)
    void BuildCopy()
    {
        Transform tree = item.transform;
        var src = tree.GetComponentsInChildren<MeshRenderer>(false);
        for (int i = 0; i < src.Length; i++)
        {
            var r = src[i];
            if (r == null || Skip(r.gameObject.name) || !r.enabled) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var c = new GameObject(r.gameObject.name);
            c.layer = r.gameObject.layer;
            var ct = c.transform;
            ct.SetParent(transform, false);

            Matrix4x4 rel = tree.worldToLocalMatrix * r.transform.localToWorldMatrix;
            ct.localPosition = rel.GetColumn(3);
            ct.localRotation = rel.rotation;
            ct.localScale = rel.lossyScale;

            c.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var cr = c.AddComponent<MeshRenderer>();
            cr.sharedMaterials = r.sharedMaterials;
            cr.shadowCastingMode = r.shadowCastingMode;
            cr.receiveShadows = r.receiveShadows;
            rends.Add(cr);
        }
    }

    // ------------------------------------------------------------ управление (зовёт ResourceItem / TreeRespawn)
    // падение: дерево заваливается от камеры, последняя треть времени: проседает в пень
    public void BeginFall()
    {
        item.GetRestPose(out restPos, out restRot, out restScale);
        Transform tree = item.transform;
        fallStartRot = tree.localRotation;     // продолжаем с текущей позы (с учётом ветра): без скачка
        progress = 0f;
        fallT = 0f;

        Vector3 dir = Vector3.forward;
        var cam = Camera.main;
        if (cam != null)
        {
            dir = tree.position - cam.transform.position;
            dir.y = 0f;
        }
        if (dir.sqrMagnitude < 0.01f)
        {
            float a = TreeSizing.Value01(tree.position) * Mathf.PI * 2f;
            dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
        dir.Normalize();
        Transform par = transform.parent;
        if (par != null) dir = par.InverseTransformDirection(dir);
        fallAxis = Vector3.Cross(Vector3.up, dir).normalized;   // ось в пространстве родителя

        transform.localPosition = restPos;
        transform.localRotation = fallStartRot;
        transform.localScale = restScale;
        UnregisterWind();
        phase = Phase.Fall;
        gameObject.SetActive(true);
    }

    // TreeRespawn каждый кадр сообщает долю роста
    public void SetProgress(float p) { progress = Mathf.Clamp01(p); }

    // TreeRespawn: есть ли под ростком пень (1) и как он уходит в землю (1 → 0). Росток опускается вместе с ним
    public void SetStumpBlend(float b) { stumpBlend = Mathf.Clamp01(b); }

    // дерево выросло или загрузили сохранение: двойник больше не нужен
    public void Finish()
    {
        phase = Phase.Idle;
        stumpBlend = 1f;
        UnregisterWind();
        if (this != null && gameObject.activeSelf) gameObject.SetActive(false);
    }

    void OnDestroy() { UnregisterWind(); }

    // ------------------------------------------------------------ кадры
    void Update()
    {
        if (phase == Phase.Fall) TickFall();
        else if (phase == Phase.Grow) ApplyGrow(progress);
    }

    void TickFall()
    {
        fallT += Time.deltaTime;
        float u = Mathf.Clamp01(fallT / Mathf.Max(0.1f, GameBalance.GrowFallSeconds));

        // 0..0.7: заваливается (ускоряется как маятник), 0.7..1: проседает и исчезает
        float tilt = Mathf.Clamp01(u / 0.7f);
        float angle = 88f * tilt * tilt;
        transform.localRotation = Quaternion.AngleAxis(angle, fallAxis) * fallStartRot;

        float sink = Mathf.Clamp01((u - 0.7f) / 0.3f);
        sink = sink * sink * (3f - 2f * sink);
        transform.localScale = restScale * Mathf.Max(0.001f, 1f - sink);

        if (u >= 1f) EnterGrow();
    }

    void EnterGrow()
    {
        phase = Phase.Grow;
        growT = 0f;
        transform.localRotation = restRot;
        RegisterWind();
        ApplyGrow(progress);
    }

    // три стадии: росток, молодое, зрелое. Внутри каждой smoothstep, в узлах скорость нулевая: рост идёт «ступенями», а в конце ровно 1
    // Было: 0.10 → 0.14 (на 18%) → 0.50: первую пятую часть времени дерево почти не менялось и выглядело «зависшим».
    // Теперь росток сразу крупнее (GrowSproutScale 0.24), быстро набирает «саженец» (0.42 к 15% времени), потом молодое (0.70 к 55%) и зрелое.
    static readonly float[] keyP = { 0f, 0.15f, 0.55f, 1f };
    static float KeyK(int i) { return i == 0 ? GameBalance.GrowSproutScale : (i == 1 ? 0.42f : (i == 2 ? 0.70f : 1f)); }

    public static float GrowCurve(float p)
    {
        p = Mathf.Clamp01(p);
        for (int i = 0; i < 3; i++)
        {
            if (p > keyP[i + 1]) continue;
            float u = (p - keyP[i]) / (keyP[i + 1] - keyP[i]);
            u = u * u * (3f - 2f * u);
            return Mathf.Lerp(KeyK(i), KeyK(i + 1), u);
        }
        return 1f;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.9f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;   // 0 → ~1.12 → 1, в конце ровно 1 (дерево включится без скачка)
    }

    void ApplyGrow(float p)
    {
        float k = GrowCurve(p);
        // первые GrowPopSeconds росток «выскакивает» (из нуля с небольшим перелётом и пружинкой), чтобы появиться заметно, а не рывком
        growT += Time.deltaTime;
        float pop = EaseOutBack(Mathf.Clamp01(growT / Mathf.Max(0.05f, GameBalance.GrowPopSeconds)));
        transform.localScale = restScale * (k * pop);
        // пока под ростком пень, он стоит на срезе; пень ушёл в землю (stumpBlend 0) — росток стоит в земле. Подъём уходит в ноль вместе с ростом
        transform.localPosition = restPos + Vector3.up * (GameBalance.GrowStumpLift * restScale.y * (1f - k) * stumpBlend);
    }

    // ------------------------------------------------------------ ветер
    void RegisterWind()
    {
        if (wind != null) return;
        var sway = item.GetComponent<ToonSway>();
        if (sway == null || sway.Agent == null) return;
        var a = sway.Agent;
        wind = new WindAgent
        {
            t = transform,
            r0 = restRot,
            phase = a.phase,
            angle = a.angle,
            speed = a.speed,
            rend = rends.Count > 0 ? rends[0] : null
        };
        WindManager.Add(wind);
    }

    void UnregisterWind()
    {
        if (wind == null) return;
        WindManager.Remove(wind);
        wind = null;
    }
}
