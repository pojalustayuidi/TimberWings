using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// World-space scenery owned by CloudSea. Draw requests go only to the gameplay camera.
internal sealed class HorizonScenery : IDisposable
{
    const float FlightSeconds = 172f;
    const float Turn = 2f * Mathf.PI;

    static readonly int HazeId = Shader.PropertyToID("_Haze");
    static readonly int AtmosphereId = Shader.PropertyToID("_Atmosphere");
    static readonly int SunId = Shader.PropertyToID("_SunDirection");
    static readonly int LightTintId = Shader.PropertyToID("_LightTint");

    readonly Material material;
    readonly Mesh ship;
    readonly Mesh propeller;
    readonly Island[] islands;
    readonly Vector3 flightCenter;
    readonly Vector2 flightRadius;
    readonly float ground;
    float flightTime;

    struct Island
    {
        internal Mesh mesh;
        internal Vector3 position;
        internal Vector3 scale;
        internal float heading;
    }

    internal static HorizonScenery Create(IslandSource source, Vector3 center, float radius, Vector2 ring, float southEdge)
    {
        var shader = Resources.Load<Shader>("TWHorizonScenery");
        if (shader == null)
        {
            Debug.LogWarning("[CloudSea] Не найден шейдер дальних островов и дирижабля.");
            return null;
        }
        return new HorizonScenery(shader, source, center, radius, ring, southEdge);
    }

    HorizonScenery(Shader shader, IslandSource source, Vector3 center, float radius, Vector2 ring, float southEdge)
    {
        material = new Material(shader) { name = "Horizon scenery (runtime)", hideFlags = HideFlags.DontSave };
        ship = HorizonMeshes.Airship();
        propeller = HorizonMeshes.Propeller();
        ground = center.y;

        // East of the bridge is reserved for the playable finale. The southern
        // approach leaves an open view from the pond and from the bridge landing.
        float size = Mathf.Clamp(radius / 85f, 0.65f, 1.45f);
        float near = Mathf.Max(radius + 20f, ring.x);
        float far = Mathf.Max(near + 12f, Mathf.Min(ring.y, near * 1.24f));
        islands = new[]
        {
            MakeIsland(HorizonMeshes.Island(source, HorizonMeshes.Landform.Shelf), center, near * 1.04f,
                -58f, -7.2f * size, 22f, 11.4f * size),
            MakeIsland(HorizonMeshes.Island(source, HorizonMeshes.Landform.Spire), center, far,
                35f, -10.2f * size, -27f, 8.9f * size),
            MakeIsland(HorizonMeshes.Island(source, HorizonMeshes.Landform.Fragments), center, near * 1.12f,
                -139f, -11.2f * size, 43f, 7.7f * size)
        };

        flightRadius = new Vector2(Mathf.Max(31f, radius * 0.82f), Mathf.Max(16f, radius * 0.38f));
        flightCenter = new Vector3(center.x + radius * 0.56f, ground, southEdge - flightRadius.y - 13f);
    }

    static Island MakeIsland(Mesh mesh, Vector3 center, float distance, float azimuth, float height,
        float heading, float size)
    {
        float radians = azimuth * Mathf.Deg2Rad;
        return new Island
        {
            mesh = mesh,
            position = center + new Vector3(Mathf.Cos(radians) * distance, height, Mathf.Sin(radians) * distance),
            scale = Vector3.one * size,
            heading = heading
        };
    }

    internal void Draw(Camera camera, float cloudLevel, float deltaTime)
    {
        flightTime = Mathf.Repeat(flightTime + deltaTime, FlightSeconds);
        float night = DayNightCycle.Night01;
        float twilight = DayNightCycle.Twilight01;
        var light = RenderSettings.sun;
        Vector3 toLight = light != null ? -light.transform.forward : new Vector3(0.4f, 0.8f, -0.3f).normalized;

        material.SetColor(HazeId, RenderSettings.fogColor);
        float far = camera.farClipPlane;
        material.SetVector(AtmosphereId, new Vector4(night, twilight, far, cloudLevel));
        material.SetVector(SunId, toLight);
        material.SetColor(LightTintId, light != null ? light.color : Color.white);

        float clock = Time.time;
        for (int i = 0; i < islands.Length; i++)
        {
            Island island = islands[i];
            float bob = Mathf.Sin(clock * 0.14f + i * 1.6f) * (0.28f + 0.14f * i);
            var pose = Matrix4x4.TRS(island.position + Vector3.up * bob,
                Quaternion.Euler(0f, island.heading, 0f), island.scale);
            DrawMesh(island.mesh, pose, camera);
        }

        SampleFlight(flightTime, cloudLevel, out Vector3 position, out Quaternion rotation);
        Matrix4x4 hull = Matrix4x4.TRS(position, rotation, Vector3.one);
        DrawMesh(ship, hull, camera);

        float spin = Mathf.Repeat(flightTime * 630f, 360f);
        Matrix4x4 rotor = Matrix4x4.TRS(HorizonMeshes.PropellerPivot,
            Quaternion.AngleAxis(spin, Vector3.forward), Vector3.one);
        DrawMesh(propeller, hull * rotor, camera);
    }

    void DrawMesh(Mesh mesh, Matrix4x4 pose, Camera camera)
    {
        Graphics.DrawMesh(mesh, pose, material, 0, camera, 0, null,
            ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
    }

    void SampleFlight(float time, float cloudLevel, out Vector3 position, out Quaternion orientation)
    {
        float angle = 0.60f + time * (Turn / FlightSeconds);
        float sin = Mathf.Sin(angle);
        float cos = Mathf.Cos(angle);
        float lift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.58f, 0.4f, sin));
        float height = Mathf.Lerp(cloudLevel - 6f, ground + 12.2f, lift);
        position = flightCenter + new Vector3(flightRadius.x * cos, height - ground + 0.65f * Mathf.Sin(angle * 2f),
            flightRadius.y * sin);
        var tangent = new Vector3(-flightRadius.x * sin, 0f, flightRadius.y * cos);
        orientation = Quaternion.LookRotation(tangent, Vector3.up) *
            Quaternion.Euler(-2.6f * cos, 0f, 4.5f + 2.4f * Mathf.Sin(angle * 2f + 0.65f));
    }

    public void Dispose()
    {
        foreach (Island island in islands) Object.Destroy(island.mesh);
        Object.Destroy(ship);
        Object.Destroy(propeller);
        Object.Destroy(material);
    }
}
