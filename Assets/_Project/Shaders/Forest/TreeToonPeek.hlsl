// Assets/_Project/Shaders/Forest/TreeToonPeek.hlsl
// «Окно в листве»: вокруг игрока на экране деревья, которые ближе к камере, чем игрок, мягко растворяются (дизеринг).
// Данные каждый кадр кладёт CameraFollow (Shader.SetGlobalVector), материалы и рендереры не трогаются.
//
// _PeekData   x,y = центр окна на экране (viewport 0..1), z = радиус (доля высоты экрана), w = мягкость края (доля радиуса)
// _PeekParams x = глубина игрока минус запас (метры вдоль взгляда), y = аспект экрана, z = сила 0..1 (0 = окна нет), w = мин. непрозрачность
#ifndef TREE_TOON_PEEK_INCLUDED
#define TREE_TOON_PEEK_INCLUDED

float4 _PeekData;
float4 _PeekParams;

// 1 = дерево видно полностью, меньше = в окне
float PeekOpacity(float3 positionWS)
{
    if (_PeekParams.z <= 0.001) return 1.0;

    float4 clip = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
    float2 uv = clip.xy / clip.w * 0.5 + 0.5;
    if (_ProjectionParams.x < 0) uv.y = 1.0 - uv.y;            // камера рисует вверх ногами (рендер в текстуру)

    float viewDepth = -mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).z;

    float2 d = (uv - _PeekData.xy) * float2(_PeekParams.y, 1.0);   // аспект: окно круглое, а не овальное
    float dist = length(d);
    float r = max(_PeekData.z, 1e-4);
    float hole = 1.0 - smoothstep(r * (1.0 - _PeekData.w), r, dist);      // 1 в центре окна, 0 снаружи
    float inFront = smoothstep(0.0, 0.6, _PeekParams.x - viewDepth);      // только то, что ближе игрока

    float k = hole * inFront * _PeekParams.z;
    return lerp(1.0, _PeekParams.w, k);
}

// вызывать в начале фрагментного шейдера основного прохода (НЕ в ShadowCaster: тень от дерева остаётся)
void PeekClip(float4 positionCS, float3 positionWS)
{
    float a = PeekOpacity(positionWS);
    if (a >= 0.999) return;
    // интерливный градиентный шум (Jimenez): мягкий дизеринг без массивов и текстур
    float t = frac(52.9829189 * frac(dot(positionCS.xy, float2(0.06711056, 0.00583715))));
    clip(a - t);
}

#endif
