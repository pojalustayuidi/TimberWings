// Assets/_Project/Scripts/Camera/ForestCameraZone.cs (НЕ в Editor)
// Мягко меняет камеру, пока игрок в лесу: чуть выше (круче сверху), чтобы между стволами был виден игрок и тропа.
// CameraFollow не трогаем: этот скрипт считается раньше него (-101) и сдвигает его публичные поля.
// Меняется только дистанция, наклон, FOV и высота взгляда. Yaw остаётся прежним, поэтому управление не ломается.
// LocationBuilder создаёт этот объект сам (ForestCameraZone в центре леса). Центр = положение объекта.
//
// Что нового:
//  - Раньше по умолчанию в лесу камера опускалась (pitch -2°) и приближалась (x0.92): это ровно то, что заставляло кроны
//    перекрывать игрока. Теперь лес чуть ПОДНИМАЕТ камеру и держит дистанцию.
//  - «Густота»: каждые 0.2 с считаем, сколько деревьев стоит в коридоре между игроком и камерой. Чем гуще, тем круче камера
//    (до +coverPitch°) и чуть дальше. В редком лесу и на полянах камера остаётся почти обычной. Меняется плавно (coverSmooth).
//  - На существующем объекте в сцене лежат СТАРЫЕ числа из инспектора: нажми ПКМ по заголовку компонента →
//    «Рекомендуемые значения» (или Reset).
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-101)]
public class ForestCameraZone : MonoBehaviour
{
    [Header("Зона (метры)")]
    public float radius = 8.8f;          // внутри радиуса эффект полный
    public float fade = 4.4f;            // за радиусом эффект плавно уходит на этой ширине

    [Header("Что меняем в лесу")]
    public float distanceMul = 1.0f;     // <1 = ближе к игроку, >1 = дальше (для Orthographic меняется orthoSize)
    public float pitchAdd = 2f;          // + круче сверху (меньше перекрытия), - ниже и «кинематографичнее»
    public float fovAdd = 0f;            // шире кадр (Perspective)
    public float lookUp = 0.25f;         // взгляд чуть выше, метры

    [Header("Густота: камера подстраивается под деревья между ней и игроком")]
    public bool denseAssist = true;
    [Tooltip("Добавка к наклону при полном перекрытии, градусы")]
    public float coverPitch = 6f;
    [Tooltip("Добавка к дистанции при полном перекрытии (0.06 = +6%)")]
    public float coverDistance = 0.06f;
    [Tooltip("Как далеко в сторону камеры ищем деревья, метры")]
    public float coverRange = 9f;
    [Tooltip("Полуширина коридора от игрока к камере, метры")]
    public float coverSide = 2.4f;
    [Tooltip("Сколько «весов» деревьев в коридоре = полное перекрытие (молодое 0.6, зрелое 1, старое 1.4)")]
    public float coverFull = 3.5f;
    public float coverSmooth = 1.2f;     // секунд на переход

    [Header("Лесной грейд (создаётся Tools > BuilderSim > Camera Vibe)")]
    public Volume forestVolume;          // пусто = найдёт объект «Vibe Forest» сам

    [Header("Плавность")]
    public float blendTime = 1.4f;       // секунд на полный переход

    CameraFollow cam;
    Camera unityCam;
    float w;                             // 0..1, текущая сила эффекта
    float cover, coverWant, coverNext;   // 0..1, перекрытие деревьями (сглаженное, цель, время следующего замера)
    bool volumeLooked;
    float aDist, aPitch, aFov, aLook;    // что мы сейчас прибавили к CameraFollow

    [ContextMenu("Рекомендуемые значения")]
    public void ResetToRecommended()
    {
        distanceMul = 1.0f;
        pitchAdd = 2f;
        fovAdd = 0f;
        lookUp = 0.25f;
        denseAssist = true;
        coverPitch = 6f;
        coverDistance = 0.06f;
        coverRange = 9f;
        coverSide = 2.4f;
        coverFull = 3.5f;
        coverSmooth = 1.2f;
        blendTime = 1.4f;
    }

    void Reset() { ResetToRecommended(); }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = FindFirstObjectByType<CameraFollow>();
            if (cam == null) return;
            unityCam = cam.GetComponent<Camera>();
        }

        Transform t = cam.target;
        if (t == null) return;   // CameraFollow сам найдёт игрока, подождём

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Vector3 d = t.position - transform.position;
        float dist = new Vector2(d.x, d.z).magnitude;
        float want = 1f - Mathf.InverseLerp(radius, radius + Mathf.Max(0.1f, fade), dist);
        w = Mathf.MoveTowards(w, want, dt / Mathf.Max(0.05f, blendTime));
        float e = w * w * (3f - 2f * w);

        // густота леса между игроком и камерой (дёшево: раз в 0.2 с, по списку включённых деревьев)
        if (denseAssist && e > 0.01f)
        {
            if (Time.time >= coverNext)
            {
                coverNext = Time.time + 0.2f;
                coverWant = MeasureCover(t.position);
            }
        }
        else coverWant = 0f;
        cover = Mathf.MoveTowards(cover, coverWant, dt / Mathf.Max(0.05f, coverSmooth));
        float c = cover * cover * (3f - 2f * cover);

        Apply(e, c);

        if (forestVolume == null && !volumeLooked)
        {
            volumeLooked = true;
            var go = GameObject.Find("Vibe Forest");
            if (go != null) forestVolume = go.GetComponent<Volume>();
        }
        if (forestVolume != null) forestVolume.weight = e;
    }

    // 0..1: насколько деревья стоят «на линии» камера → игрок
    float MeasureCover(Vector3 p)
    {
        // горизонтальное направление ОТ игрока К камере (камера смотрит вдоль yaw, значит стоит позади)
        Vector3 toCam = -(Quaternion.Euler(0f, cam.yaw + cam.yawBias, 0f) * Vector3.forward);
        toCam.y = 0f;
        toCam.Normalize();
        Vector3 right = new Vector3(toCam.z, 0f, -toCam.x);

        var list = ResourceItem.Active;
        float sum = 0f;
        float range = Mathf.Max(1f, coverRange), side = Mathf.Max(0.5f, coverSide);
        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            if (it == null) continue;
            Vector3 v = it.transform.position - p;
            v.y = 0f;
            float along = Vector3.Dot(v, toCam);
            if (along < 0.6f || along > range) continue;       // позади игрока или слишком далеко
            float lat = Mathf.Abs(Vector3.Dot(v, right));
            if (lat > side) continue;

            float sizeW = it.Size == TreeSize.Young ? 0.6f : (it.Size == TreeSize.Old ? 1.4f : 1f);
            float k = (1f - 0.5f * along / range) * (1f - 0.5f * lat / side);   // ближе к линии и к игроку = важнее
            sum += sizeW * k;
        }
        return Mathf.Clamp01(sum / Mathf.Max(0.5f, coverFull));
    }

    // убираем прошлую добавку и ставим новую: базовые значения камеры остаются теми, что в инспекторе CameraFollow
    void Apply(float e, float c)
    {
        bool ortho = unityCam != null && unityCam.orthographic;

        float baseDist = cam.distance - aDist;
        float baseOrtho = cam.orthoSize - aDist;      // для Orthographic aDist хранит добавку к orthoSize
        float basePitch = cam.pitch - aPitch;
        float baseFov = cam.fieldOfView - aFov;
        float baseLook = cam.lookOffset.y - aLook;

        float mul = (distanceMul - 1f) * e + coverDistance * c * e;   // густота работает только внутри леса (e)

        float nDist = 0f;
        if (ortho) { cam.orthoSize = baseOrtho; nDist = baseOrtho * mul; cam.orthoSize = baseOrtho + nDist; }
        else { nDist = baseDist * mul; cam.distance = baseDist + nDist; }

        aPitch = pitchAdd * e + coverPitch * c * e;
        cam.pitch = basePitch + aPitch;

        aFov = ortho ? 0f : fovAdd * e;
        cam.fieldOfView = baseFov + aFov;

        aLook = lookUp * e;
        Vector3 lo = cam.lookOffset;
        lo.y = baseLook + aLook;
        cam.lookOffset = lo;

        aDist = nDist;
    }

    void OnDisable()
    {
        if (cam == null) return;
        Apply(0f, 0f);
        w = 0f;
        cover = coverWant = 0f;
        if (forestVolume != null) forestVolume.weight = 0f;
    }
}
