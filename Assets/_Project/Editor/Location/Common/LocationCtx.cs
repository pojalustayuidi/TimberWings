#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Common/LocationCtx.cs
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
    public Func<float, float, float> heightU;
    public Func<float, float, float> distFlat;          // расстояние до ровных зон (центр, тропы, площадки)
    public Func<float, float, float, bool> inGlade;
    public Func<float, float, float> edgeIn;           // расстояние до края острова внутрь, u
    public Func<float, float, float> headMask;         // 0..1: след горы у истока (для деревьев и разметки)
    public float riverSrcZ;                            // z истока, u
    // заполняет LocationHeadMountain: профиль передней стены горы по оси реки (s=z, y), снизу вверх, и высота родника
    public List<Vector2> headFace;
    public float headSpringY, headSpringX;
    // заполняет LocationSpring (исток-каскад): LocationRiver тогда не строит старые дугу валунов и тонкий водопадик
    public bool springBuilt;
    public float springTopY;
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