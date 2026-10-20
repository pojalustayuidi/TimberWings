// Assets/_Project/Scripts/Forest/ForestAtmosphere.cs (НЕ в Editor)
// Атмосфера леса: пятна солнца на земле, лучи света и светлячки. Создаётся инструментом Tools → Forest → Декор леса,
// вешается на объект ForestDecor/Fx. Сам ничего не рисует: только плавно меняет яркость (_Intensity у материала ForestDecor/Glow)
// в зависимости от времени суток: днём видны пятна и лучи, к вечеру они тают, а светлячки, наоборот, разгораются.
// «Ночь» считается тем же способом, что в FireLightPool: по высоте солнца (RenderSettings.sun).
using UnityEngine;

[DisallowMultipleComponent]
public class ForestAtmosphere : MonoBehaviour
{
    [System.Serializable]
    public class Group
    {
        public string name;
        public Renderer[] renderers = new Renderer[0];
        [Tooltip("Яркость днём (множитель _Intensity)")] [Range(0f, 2f)] public float day = 1f;
        [Tooltip("Яркость ночью")] [Range(0f, 2f)] public float night = 1f;
    }

    public Group[] groups = new Group[0];
    [Tooltip("Если солнца в сцене нет: какой считать ночь")] public float noSunNight = 0.35f;
    [Tooltip("Солнце выше этого sin(высоты) = полный день")] public float fullDaySunElevation = 0.45f;
    [Tooltip("Скорость смены дня и ночи (1 = примерно 4 секунды на полный переход)")] public float response = 1f;

    [Header("Тест")]
    public bool forceNight;
    [Range(0f, 1f)] public float forceNightLevel = 1f;

    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    MaterialPropertyBlock mpb;
    float night = -1f;
    float nextCheck;

    void OnEnable()
    {
        night = -1f;
        Tick(true);
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.2f;
        Tick(false);
    }

    float TargetNight()
    {
        if (forceNight) return forceNightLevel;
        Light sun = RenderSettings.sun;
        if (sun == null) return noSunNight;
        float elev = -sun.transform.forward.y;
        float day = Mathf.Clamp01(Mathf.InverseLerp(0.02f, Mathf.Max(0.1f, fullDaySunElevation), elev)) * Mathf.Clamp01(sun.intensity / 0.6f);
        return 1f - day;
    }

    void Tick(bool snap)
    {
        float t = TargetNight();
        float prev = night;
        night = (snap || night < 0f) ? t : Mathf.MoveTowards(night, t, 0.2f * 0.25f * Mathf.Max(0.05f, response));
        if (!snap && Mathf.Abs(night - prev) < 0.002f) return;
        Apply();
    }

    void Apply()
    {
        if (groups == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        for (int g = 0; g < groups.Length; g++)
        {
            var gr = groups[g];
            if (gr == null || gr.renderers == null) continue;
            float k = Mathf.Lerp(gr.day, gr.night, night);
            for (int i = 0; i < gr.renderers.Length; i++)
            {
                var r = gr.renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(IntensityId, k);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    [ContextMenu("Тест: ночь")] void TestNight() { forceNight = true; forceNightLevel = 1f; }
    [ContextMenu("Тест: день")] void TestDay() { forceNight = true; forceNightLevel = 0f; }
    [ContextMenu("Тест: по солнцу")] void TestAuto() { forceNight = false; }
}
