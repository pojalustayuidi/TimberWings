// Assets/_Project/Scripts/Pets/PetStage.cs (НЕ в Editor)
// Мини-студия для показа питомца в интерфейсе: далеко под миром ставится копия префаба, камера и свет,
// камера рисует в RenderTexture. Используют PetPortrait (снимки для карточек) и PetLiveView (вращающийся питомец).
// Мир и основная камера студию не видят: она стоит в 3 км под картой, а свет у неё точечный (с ограниченной дальностью).
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Одна студия: корень, камера, питомец. Держит PetPortrait или PetLiveView
public class PetStageRig
{
    public GameObject root;
    public Camera cam;
    public Transform pet;

    public void SetActive(bool on)
    {
        if (root != null) root.SetActive(on);
    }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}

public static class PetStage
{
    static int slotCounter;
    static readonly Vector3 Origin = new Vector3(0f, -3000f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { slotCounter = 0; }

    public static RenderTexture MakeRT(int size)
    {
        var rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.antiAliasing = 4;
        rt.useMipMap = false;
        rt.Create();
        return rt;
    }

    // Собирает студию с моделью питомца нужной стадии роста (по умолчанию взрослой: 2). Камера выключена: включает вызывающий.
    // null, если у питомца нет префаба.
    public static PetStageRig Build(PetData d, RenderTexture rt, int stage = 2)
    {
        GameObject prefab = d != null ? d.PrefabForStage(Mathf.Clamp(stage, 0, 2)) : null;
        if (prefab == null) return null;

        // каждой студии свой участок, чтобы несколько питомцев не пересекались
        Vector3 center = Origin + new Vector3(30f * (slotCounter % 50), 0f, 0f);
        slotCounter++;

        var rig = new PetStageRig();
        rig.root = new GameObject("PetStage");
        rig.root.transform.position = center;

        GameObject pet = Object.Instantiate(prefab, center, Quaternion.identity);
        pet.name = "StagePet";
        pet.transform.SetParent(rig.root.transform, true);
        rig.pet = pet.transform;

        // В студии питомец не бегает: выключаем PetFollower и убираем его тень (анимация Idle продолжает играть)
        PetFollower pf = pet.GetComponent<PetFollower>();
        if (pf != null)
        {
            Transform sh = pet.transform.Find("Shadow");
            if (sh != null) Object.DestroyImmediate(sh.gameObject);
            pf.enabled = false;
        }

        // размеры модели
        Bounds b = new Bounds(center + Vector3.up * 0.5f, Vector3.one);
        bool first = true;
        Renderer[] rs = pet.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] is ParticleSystemRenderer) continue;
            if (first) { b = rs[i].bounds; first = false; }
            else b.Encapsulate(rs[i].bounds);
        }

        // камера: спереди-справа-сверху, расстояние подобрано так, чтобы питомец целиком влез
        GameObject cg = new GameObject("PetCam");
        cg.transform.SetParent(rig.root.transform, true);
        Camera cam = cg.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = ~0;
        cam.fieldOfView = 24f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 80f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.depth = -50f;
        cam.targetTexture = rt;
        cam.enabled = false;

        UniversalAdditionalCameraData ad = cam.GetUniversalAdditionalCameraData();
        ad.renderPostProcessing = false;
        ad.renderShadows = false;

        Vector3 c = b.center;
        float radius = Mathf.Max(0.2f, b.extents.magnitude);
        float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.85f;
        Vector3 dir = Quaternion.Euler(-14f, 28f, 0f) * Vector3.forward;
        cam.transform.position = c + dir * dist;
        cam.transform.LookAt(c);
        rig.cam = cam;

        // подсветка: точечный свет (его дальность не достаёт до мира)
        GameObject lg = new GameObject("PetLight");
        lg.transform.SetParent(rig.root.transform, true);
        lg.transform.position = cam.transform.position + Vector3.up * dist * 0.4f - cam.transform.right * dist * 0.3f;
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = dist * 4f;
        l.intensity = Mathf.Max(1.5f, dist * dist * 0.9f);
        l.color = new Color(1f, 0.97f, 0.92f, 1f);
        l.shadows = LightShadows.None;

        return rig;
    }
}
