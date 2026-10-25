#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Core/LocationCtx.cs
// Данные, которые LocationBuilder передаёт модулям.
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LocationCtx
{
    public LocationBuilder.Settings s;
    public Transform root;
    public float ws, half, centerRadius, terrainSize, maxHeight;
    public float waterLevel, riverWidth, riverBank, riverDepth;
    public int seed;
    public Func<float, float> riverX;
    public Func<float, float> riverHalfTop;            // полуширина до бровки (максимум из двух берегов) на z, u
    public Func<float, float, float> heightU;
    public Func<float, float, float> distFlat;          // расстояние до ровных зон (центр, тропы, площадки)
    public Func<float, float, float, bool> inGlade;
    public Func<float, float, float> edgeIn;           // расстояние до края острова внутрь, u
    public float riverSrcZ;                            // z истока, u
    public bool lakeEnabled;                           // Р4: чаша озера построена рельефом
    public Vector2 lakeCenter;                         // центр чаши, u
    public float lakeWaterR, lakeWaterY;               // водный радиус и уровень воды, u: земля внутри ниже уровня
    public bool springBuilt;                            // родник строит другой модуль: река свой каскад не рисует (сейчас всегда false)
    public List<Vector2> headFace;                      // профиль передней стены горы у истока (s, y) снизу вверх, null = горы нет
    public float headSpringX;                           // x ручья на горе, u
    public bool hasBridge;
    public Vector2 bridgePos;          // (x, z) площадки моста в u
    public float bridgeSide = -1f;     // в какую сторону по X от площадки лежит река
    public readonly List<Vector2> trailDirs = new List<Vector2>();   // направления троп от центра
    public readonly List<Color> trailColors = new List<Color>();
    public bool hasForest;
    public Vector2 forestPos;
    public float forestRad;
}
#endif