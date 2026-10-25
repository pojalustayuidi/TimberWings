// Assets/_Project/Scripts/Location/CandyGroundParams.cs
// Параметры «конфетной» земли для шейдеров Project/CandyGround (Terrain) и Project/IslandRock (кромка и поле острова).
// Вешается на объект Terrain (его добавляет сборщик локации). Что делает:
//   1) кладёт цвета лайма/мяты и параметры шума и ворсинок в ГЛОБАЛЬНЫЕ свойства шейдеров: одни значения на Terrain и на остров, шва нет;
//   2) привязывает к материалу террейна alphamap-текстуры (_Ctrl0.._Ctrl2) и прямоугольник террейна (_TerrainRect).
// Работает и в редакторе (ExecuteAlways): сцена выглядит так же, как в игре.
using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
public class CandyGroundParams : MonoBehaviour
{
    [Header("Трава")]
    public Color lime = new Color(0.60f, 0.85f, 0.22f);
    public Color mint = new Color(0.42f, 0.82f, 0.55f);
    [Tooltip("Масштаб пятен лайма/мяты, 1/м. Меньше = пятна крупнее")]
    [Range(0.01f, 0.3f)] public float patchScale = 0.06f;
    [Tooltip("Сколько мяты в пятнах: 0 = везде лайм")]
    [Range(0f, 1f)] public float mintAmount = 1f;
    [Tooltip("Смещение всей травы к мяте")]
    [Range(0f, 1f)] public float mintBias = 0f;
    [Tooltip("Полуширина границы между пятнами: меньше = чётче")]
    [Range(0.02f, 0.4f)] public float patchSoftness = 0.14f;

    [Header("Ворсинки")]
    [Tooltip("Плотность, 1/м")]
    [Range(1f, 14f)] public float tuftDensity = 5.5f;
    [Range(0f, 0.6f)] public float tuftStrength = 0.22f;
    [Tooltip("Дистанция, на которой ворсинки начинают таять, м")]
    public float tuftFadeStart = 18f;
    [Tooltip("Дистанция, на которой ворсинок нет совсем, м")]
    public float tuftFadeEnd = 45f;

    static readonly int LimeId = Shader.PropertyToID("_CGLime");
    static readonly int MintId = Shader.PropertyToID("_CGMint");
    static readonly int NoiseId = Shader.PropertyToID("_CGNoise");
    static readonly int TuftId = Shader.PropertyToID("_CGTuft");
    static readonly int RectId = Shader.PropertyToID("_TerrainRect");
    static readonly int[] CtrlIds =
    {
        Shader.PropertyToID("_Ctrl0"), Shader.PropertyToID("_Ctrl1"), Shader.PropertyToID("_Ctrl2")
    };

    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }

    void OnDisable()
    {
        // нули = шейдеры возьмут запасные значения
        Shader.SetGlobalVector(LimeId, Vector4.zero);
        Shader.SetGlobalVector(MintId, Vector4.zero);
        Shader.SetGlobalVector(NoiseId, Vector4.zero);
        Shader.SetGlobalVector(TuftId, Vector4.zero);
    }

    // цвета отдаём в цветовом пространстве проекта (в Linear шейдер ждёт уже линейные значения)
    static Vector4 Col(Color c)
    {
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.linear;
        return new Vector4(c.r, c.g, c.b, 1f);   // a = 1: «задан»
    }

    public void Apply()
    {
        Shader.SetGlobalVector(LimeId, Col(lime));
        Shader.SetGlobalVector(MintId, Col(mint));
        Shader.SetGlobalVector(NoiseId, new Vector4(patchScale, mintAmount, mintBias, patchSoftness));
        Shader.SetGlobalVector(TuftId, new Vector4(tuftDensity, tuftStrength, tuftFadeStart, Mathf.Max(tuftFadeEnd, tuftFadeStart + 1f)));
        BindTerrain();
    }

    void BindTerrain()
    {
        var t = GetComponent<Terrain>();
        if (t == null || t.terrainData == null || t.materialTemplate == null) return;
        var m = t.materialTemplate;
        if (m.shader == null || m.shader.name != "Project/CandyGround") return;

        var td = t.terrainData;
        var tex = td.alphamapTextures;
        for (int i = 0; i < CtrlIds.Length && i < tex.Length; i++) m.SetTexture(CtrlIds[i], tex[i]);

        Vector3 p = t.transform.position, sz = td.size;
        m.SetVector(RectId, new Vector4(p.x, p.z, 1f / Mathf.Max(sz.x, 0.01f), 1f / Mathf.Max(sz.z, 0.01f)));
    }
}
