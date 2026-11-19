using System;
using UnityEngine;

// Editor rollback references and visual compatibility for older campsite bakes.
[DisallowMultipleComponent]
public sealed class CenterArtState : MonoBehaviour
{
    [Serializable]
    public struct HiddenObject
    {
        public GameObject target;
        public bool active;
    }

    [HideInInspector] public int revision;
    [HideInInspector] public string generatedFolder;
    [HideInInspector] public HiddenObject[] hidden = Array.Empty<HiddenObject>();
    [HideInInspector] public Transform campfire;
    [HideInInspector] public string campfireHash;
    [HideInInspector] public Transform marker;
    [HideInInspector] public Vector3 markerPosition;
    [HideInInspector] public int benches;
    [HideInInspector] public int lanterns;

    public const int RaisedGarlandRevision = 3;
    public const float GarlandLift = .20f;
    Mesh liftedWire, liftedBulbs, wornEarth;

    void Awake()
    {
        if (revision >= RaisedGarlandRevision) return;
        DressClearing();
        var wire = transform.Find("Rear_Light_String")?.GetComponent<MeshFilter>();
        var bulbs = transform.Find("Rear_String_Bulbs")?.GetComponent<MeshFilter>();
        var light = bulbs != null ? bulbs.GetComponent<FireSource>() : null;
        if (wire == null || bulbs == null || light == null || light.glowPoints == null || light.glowPoints.Length < 2 ||
            wire.sharedMesh == null || !wire.sharedMesh.isReadable || bulbs.sharedMesh == null || !bulbs.sharedMesh.isReadable)
            return;

        var points = light.glowPoints;
        Vector3 span = Vector3.ProjectOnPlane(points[points.Length - 1] - points[0], Vector3.up);
        if (span.sqrMagnitude < .01f) return;
        span.Normalize();
        float start = float.PositiveInfinity, end = float.NegativeInfinity;
        Vector3[] wireVertices = wire.sharedMesh.vertices;
        foreach (var point in wireVertices)
        {
            float projection = Vector3.Dot(point, span);
            start = Mathf.Min(start, projection);
            end = Mathf.Max(end, projection);
        }
        if (end - start < .1f) return;

        Vector3 Lift(Vector3 point)
        {
            float t = Mathf.InverseLerp(start, end, Vector3.Dot(point, span));
            return point + Vector3.up * (GarlandLift * 4f * t * (1f - t));
        }

        Mesh LiftMesh(MeshFilter part, Vector3[] vertices)
        {
            for (int i = 0; i < vertices.Length; i++) vertices[i] = Lift(vertices[i]);
            var mesh = Instantiate(part.sharedMesh);
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            part.sharedMesh = mesh;
            return mesh;
        }

        // Keep the wire fastened to its posts and lift the glow with the bulbs.
        liftedWire = LiftMesh(wire, wireVertices);
        liftedBulbs = LiftMesh(bulbs, bulbs.sharedMesh.vertices);
        light.glowPoints = Array.ConvertAll(points, Lift);
    }

    void DressClearing()
    {
        var ground = transform.Find("Organic_Earth_Clearing")?.GetComponent<MeshFilter>();
        var source = ground != null ? ground.sharedMesh : null;
        if (source == null || !source.isReadable) return;
        Color[] colors = source.colors;
        if (colors.Length != source.vertexCount) return;
        Vector3[] vertices = source.vertices;
        for (int i = 0; i < colors.Length; i++) colors[i] = WornEarth(colors[i], vertices[i]);
        wornEarth = Instantiate(source);
        wornEarth.colors = colors;
        ground.sharedMesh = wornEarth;
    }

    public static Color WornEarth(Color color, Vector3 point)
    {
        float broad = Mathf.PerlinNoise(point.x * .48f + 12f, point.z * .59f + 27f) - .5f;
        float detail = Mathf.PerlinNoise(point.x * 1.6f + 5f, point.z * 1.8f + 33f) - .5f;
        float brightness = 1f + broad * .24f + detail * .11f;
        float edge = 4f * color.a * (1f - color.a);
        return new Color(color.r * brightness, color.g * brightness, color.b * (brightness - broad * .06f),
            Mathf.Clamp01(color.a + edge * detail * .35f));
    }

    void OnDestroy()
    {
        if (liftedWire != null) Destroy(liftedWire);
        if (liftedBulbs != null) Destroy(liftedBulbs);
        if (wornEarth != null) Destroy(wornEarth);
    }
}
