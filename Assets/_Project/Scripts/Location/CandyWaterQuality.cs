// Assets/_Project/Scripts/Location/CandyWaterQuality.cs (НЕ в Editor)
// Тир качества воды: один глобальный переключатель для всех материалов Project/CandyWater.
//   Low  = пена по вершинной маске, без каустики и бликов (_CANDY_WATER_LOW)
//   Mid  = как LocationToonWater (без ключевых слов)
//   High = пена по глубине сцены (_CANDY_WATER_HIGH), нужна галка Depth Texture в URP Asset
// Тир выбирается сам перед загрузкой первой сцены: мобильные = Low, остальные = High (если в URP Asset включена Depth Texture), иначе Mid.
// Переопределить из кода: CandyWaterQuality.Apply(CandyWaterTier.Low). Попробовать в редакторе: меню Tools/Candy/Water quality.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum CandyWaterTier { Auto, Low, Mid, High }

public static class CandyWaterQuality
{
    public static event Action Changed;
    public static CandyWaterTier Current { get; private set; } = CandyWaterTier.Mid;

    // бюджет частиц воды на весь остров (жёсткий потолок ещё и в CandyWaterFx.HardCap = 40)
    public static int ParticleBudget
    {
        get
        {
            switch (Current)
            {
                case CandyWaterTier.Low: return 16;
                case CandyWaterTier.High: return 40;
                default: return 28;
            }
        }
    }

    static readonly string KLow = "_CANDY_WATER_LOW";
    static readonly string KHigh = "_CANDY_WATER_HIGH";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() { Apply(CandyWaterTier.Auto); }

    static bool DepthTextureOn()
    {
        var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        return rp != null && rp.supportsCameraDepthTexture;
    }

    public static CandyWaterTier Detect()
    {
        if (Application.isMobilePlatform) return CandyWaterTier.Low;
        return DepthTextureOn() ? CandyWaterTier.High : CandyWaterTier.Mid;
    }

    public static void Apply(CandyWaterTier tier)
    {
        if (tier == CandyWaterTier.Auto) tier = Detect();
        if (tier == CandyWaterTier.High && !DepthTextureOn()) tier = CandyWaterTier.Mid;   // без Depth Texture пена по глубине не работает

        Shader.DisableKeyword(KLow);
        Shader.DisableKeyword(KHigh);
        if (tier == CandyWaterTier.Low) Shader.EnableKeyword(KLow);
        else if (tier == CandyWaterTier.High) Shader.EnableKeyword(KHigh);

        Current = tier;
        if (Changed != null) Changed();
    }
}
