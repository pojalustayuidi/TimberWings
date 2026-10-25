// Assets/_Project/Scripts/Location/IslandSource.cs (НЕ в Editor)
// Единая точка «данные острова» для рантайма. Берёт FloatingIsland (старая локация) или IslandV2Data (остров v2), смотря что есть в сцене.
// Имена членов совпадают с FloatingIsland (Outline, GroundY, groundY, depth, seed, цвета, cloudShader, rockMaterial, Radius, GetOutline, GetEdgeLoop),
// поэтому потребителям достаточно сменить тип поля и способ поиска: IslandSource.Find() вместо FindFirstObjectByType<FloatingIsland>().
// Если в сцене есть оба, приоритет у FloatingIsland (старую локацию надо отключать: IslandCheck об этом скажет).
using System.Collections.Generic;
using UnityEngine;

public class IslandSource
{
    readonly FloatingIsland fi;
    readonly IslandV2Data v2;

    IslandSource(FloatingIsland f, IslandV2Data d) { fi = f; v2 = d; }

    public static float OuterMargin { get { return FloatingIsland.OuterMargin; } }

    public static IslandSource Find()
    {
        var f = FloatingIsland.Instance != null ? FloatingIsland.Instance : Object.FindFirstObjectByType<FloatingIsland>();
        if (f != null) return new IslandSource(f, null);
        var d = IslandV2Data.Instance != null ? IslandV2Data.Instance : Object.FindFirstObjectByType<IslandV2Data>();
        return d != null ? new IslandSource(null, d) : null;
    }

    // остров на конкретном объекте (для IslandEdgeGuard: он живёт на том же объекте)
    public static IslandSource For(GameObject go)
    {
        if (go == null) return null;
        var f = go.GetComponent<FloatingIsland>();
        if (f != null) return new IslandSource(f, null);
        var d = go.GetComponent<IslandV2Data>();
        return d != null ? new IslandSource(null, d) : null;
    }

    public bool IsV2 { get { return fi == null; } }
    public GameObject gameObject { get { return fi != null ? fi.gameObject : v2.gameObject; } }
    public Transform transform { get { return fi != null ? fi.transform : v2.transform; } }

    public List<Vector2> Outline { get { return GetOutline(); } }
    public List<Vector2> GetOutline() { return fi != null ? fi.GetOutline() : v2.GetOutline(); }
    public List<Vector2> GetEdgeLoop() { return fi != null ? fi.GetEdgeLoop() : v2.GetEdgeLoop(); }

    public float groundY { get { return fi != null ? fi.groundY : v2.groundY; } }
    public float GroundY { get { return groundY; } }
    public float depth { get { return fi != null ? fi.depth : v2.depth; } }
    public int seed { get { return fi != null ? fi.seed : v2.seed; } }
    public float Radius { get { return fi != null ? fi.Radius : v2.Radius; } }

    public Color grassTop { get { return fi != null ? fi.grassTop : v2.grassTop; } }
    public Color grassDark { get { return fi != null ? fi.grassDark : v2.grassDark; } }
    public Color soil { get { return fi != null ? fi.soil : v2.soil; } }
    public Color rockWarm { get { return fi != null ? fi.rockWarm : v2.rockWarm; } }
    public Color rockMid { get { return fi != null ? fi.rockMid : v2.rockMid; } }
    public Color rockDark { get { return fi != null ? fi.rockDark : v2.rockDark; } }

    public Shader cloudShader { get { return fi != null ? fi.cloudShader : v2.cloudShader; } }
    public Material rockMaterial { get { return fi != null ? fi.rockMaterial : v2.rockMaterial; } }
}
