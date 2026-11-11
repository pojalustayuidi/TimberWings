using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class FinalIslandPortal
{
    const int TrailCount = 19;
    const int TrailSegments = 6;
    static readonly Color MintTrail = new Color(.24f, .98f, .78f);
    static readonly Color VioletTrail = new Color(.63f, .53f, 1f);
    static readonly Color GoldTrail = new Color(1f, .80f, .48f);

    Material originalSurface;
    Material upgradedSurface;
    Material trailMaterial;
    Mesh trailMesh;
    MeshRenderer trails;
    Vector3[] trailVertices;
    Color[] trailColors;
    bool triedVortex;
    float vortexClock;
    AudioSource windSource;
    AudioClip windClip;

    void UpgradeBakedSurface()
    {
        if (field == null) return;
        var current = field.sharedMaterial;
        if (current != null && current.shader != null && current.shader.name == "TimberWings/PortalSurface") return;

        var shader = Resources.Load<Shader>("Finale/PortalSurface");
        if (shader == null || !shader.isSupported) return;
        originalSurface = current;
        upgradedSurface = new Material(shader) { name = "Portal surface" };
        field.sharedMaterial = upgradedSurface;
    }

    void CreateVortex()
    {
        triedVortex = true;
        var shader = Resources.Load<Shader>("Finale/PortalTrail");
        if (shader == null || !shader.isSupported) return;

        var holder = new GameObject("PortalWind");
        holder.layer = gameObject.layer;
        holder.transform.SetParent(transform, false);
        trailMaterial = new Material(shader) { name = "Portal wind" };
        trailMesh = new Mesh { name = "Portal wind ribbons" };
        trailMesh.MarkDynamic();
        trails = holder.AddComponent<MeshRenderer>();
        trails.sharedMaterial = trailMaterial;
        trails.shadowCastingMode = ShadowCastingMode.Off;
        trails.receiveShadows = false;
        trails.lightProbeUsage = LightProbeUsage.Off;
        holder.AddComponent<MeshFilter>().sharedMesh = trailMesh;

        int quads = TrailCount * TrailSegments;
        trailVertices = new Vector3[quads * 4];
        trailColors = new Color[trailVertices.Length];
        var uv = new Vector2[trailVertices.Length];
        var indices = new int[quads * 6];
        for (int i = 0; i < quads; i++)
        {
            int v = i * 4, t = i * 6;
            uv[v] = uv[v + 3] = Vector2.zero;
            uv[v + 1] = uv[v + 2] = Vector2.right;
            indices[t] = v;
            indices[t + 1] = v + 1;
            indices[t + 2] = v + 2;
            indices[t + 3] = v;
            indices[t + 4] = v + 2;
            indices[t + 5] = v + 3;
        }
        trailMesh.vertices = trailVertices;
        trailMesh.uv = uv;
        trailMesh.triangles = indices;
        trailMesh.bounds = new Bounds(new Vector3(0f, 1.6f, -1.85f), new Vector3(7f, 4.5f, 5.6f));
    }

    static Vector3 VortexPoint(int strand, float phase)
    {
        float u = Mathf.Clamp01(phase);
        float outerRadius = 1.08f + .88f * Mathf.Repeat(strand * .467f, 1f);
        float turn = strand * 2.399963f - u * (3.6f + (strand % 3) * .45f);
        float radius = Mathf.Lerp(outerRadius, .10f, u * u);
        float depth = Mathf.Lerp(3.95f, 2.55f, Mathf.Repeat(strand * .631f, 1f));
        return new Vector3(Mathf.Cos(turn) * radius,
            Mathf.Max(.12f, 1.46f + Mathf.Sin(turn) * radius * .64f),
            -.07f - depth * (1f - u) * (1f - u));
    }

    void UpdateVortex(Camera view, bool calm)
    {
        if (calm || charge < .28f)
        {
            if (trails != null) trails.enabled = false;
            return;
        }
        if (!triedVortex) CreateVortex();
        if (trails == null) return;
        trails.enabled = true;
        vortexClock += Time.unscaledDeltaTime * Mathf.Lerp(.38f, 1.45f, drawIn);
        Vector3 cameraForward = transform.InverseTransformDirection(view.transform.forward);
        for (int i = 0; i < TrailCount; i++)
        {
            float head = Mathf.Repeat(vortexClock * (.81f + .055f * (i % 5)) + i * .618f, 1f);
            float length = Mathf.Lerp(.09f, .22f, drawIn) * (.80f + .12f * (i % 4));
            Color tint = i % 7 == 0 ? GoldTrail : Color.Lerp(MintTrail, VioletTrail, Mathf.Repeat(i * .382f, 1f));
            tint.a = Mathf.SmoothStep(0f, 1f, head / .15f) *
                (1f - LeoPortalPose.Ease(head, .88f, 1f)) *
                Mathf.Lerp(.27f, .85f, drawIn) * LeoPortalPose.Ease(charge, .28f, .77f);
            for (int k = 0; k < TrailSegments; k++)
            {
                float from = k / (float)TrailSegments, to = (k + 1f) / TrailSegments;
                Vector3 a = VortexPoint(i, head - length * (1f - from));
                Vector3 b = VortexPoint(i, head - length * (1f - to));
                Vector3 across = Vector3.Cross(b - a, cameraForward).normalized;
                float width = (.014f + .018f * drawIn) * (1f + .22f * (i % 3));
                int v = (i * TrailSegments + k) * 4;
                trailVertices[v] = a - across * (width * from);
                trailVertices[v + 1] = a + across * (width * from);
                trailVertices[v + 2] = b + across * (width * to);
                trailVertices[v + 3] = b - across * (width * to);
                trailColors[v] = trailColors[v + 1] = new Color(tint.r, tint.g, tint.b, tint.a * from * from);
                trailColors[v + 2] = trailColors[v + 3] = new Color(tint.r, tint.g, tint.b, tint.a * to * to);
            }
        }
        trailMesh.SetVertices(trailVertices, 0, trailVertices.Length, MeshUpdateFlags.DontRecalculateBounds);
        trailMesh.colors = trailColors;
    }

    void PlayInhale()
    {
        float volume = GameAudio.SfxLevel;
        if (volume <= 0f) return;
        if (windSource == null)
        {
            windSource = gameObject.AddComponent<AudioSource>();
            windSource.playOnAwake = false;
            windSource.spatialBlend = 0f;
        }
        if (windClip == null) windClip = MakeWindClip();
        windSource.PlayOneShot(windClip, .52f * volume);
    }

    static AudioClip MakeWindClip()
    {
        const float length = 1.56f;
        var random = new System.Random(81);
        float low = 0f, mid = 0f;
        return VibeSynth.Make("PortalInhale", length, time =>
        {
            float u = time / length;
            float noise = (float)random.NextDouble() * 2f - 1f;
            low += .009f * (noise - low);
            mid += Mathf.Lerp(.032f, .12f, u) * (noise - mid);
            float phase = Mathf.PI * 2f * time * (66f + 38f * u * u);
            float envelope = Mathf.Sin(Mathf.PI * u);
            return ((mid - low) * .64f + Mathf.Sin(phase) * .15f + Mathf.Sin(phase * .5f) * .07f)
                * envelope * envelope * (.38f + .62f * u);
        }, .39f);
    }

    void StopEffects()
    {
        if (trails != null) trails.enabled = false;
        if (windSource != null) windSource.Stop();
        vortexClock = 0f;
    }

    void DisposeEffects()
    {
        if (upgradedSurface != null)
        {
            if (field != null) field.sharedMaterial = originalSurface;
            Destroy(upgradedSurface);
        }
        if (trailMesh != null) Destroy(trailMesh);
        if (trailMaterial != null) Destroy(trailMaterial);
        if (windClip != null) Destroy(windClip);
    }
}
