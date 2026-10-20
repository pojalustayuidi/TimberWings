// Assets/_Project/Scripts/Resource/TreeFx.cs (НЕ в Editor)
// Облачко листьев и пыли у корней, когда дерево выросло (ResourceItem.PlaySpring).
// Пул из GameBalance.TreePuffPool систем частиц создаётся один раз при первом облачке; дальше только ps.Emit (без аллокаций на дерево).
// Если все системы заняты, берётся самая старая: облачко просто обрывается, ничего не ломается.
using UnityEngine;

public static class TreeFx
{
    static ParticleSystem[] pool;
    static Material mat;
    static GameObject root;
    static int next;

    static readonly Color Leaf = new Color(0.45f, 0.78f, 0.30f, 1f);
    static readonly Color Dust = new Color(0.82f, 0.72f, 0.52f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { pool = null; mat = null; root = null; next = 0; }

    // size: 0 молодое, 1 зрелое, 2 старое (больше частиц и шире облачко)
    public static void Puff(Vector3 pos, int size)
    {
        if (!EnsurePool()) return;

        var ps = pool[next];
        next = (next + 1) % pool.Length;
        if (ps == null) return;

        float k = 0.8f + 0.25f * size;
        var tr = ps.transform;
        tr.position = pos + Vector3.up * 0.15f;
        var sh = ps.shape;
        sh.radius = 0.35f * k;
        ps.Emit(8 + 4 * size);
    }

    static bool EnsurePool()
    {
        if (pool != null && root != null) return true;
        if (mat == null) mat = VibeGfx.ParticleMat(VibeGfx.Square);   // Sprites/Default: цвет частицы = её цвет
        if (mat == null) return false;                                 // нет шейдера: лучше без облачков, чем розовые квадраты

        root = new GameObject("TreeFx");
        int n = Mathf.Max(1, GameBalance.TreePuffPool);
        pool = new ParticleSystem[n];
        for (int i = 0; i < n; i++) pool[i] = Make(i);
        next = 0;
        return true;
    }

    static ParticleSystem Make(int i)
    {
        var go = new GameObject("Puff" + i);
        go.transform.SetParent(root.transform, false);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // конус смотрит вверх

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // иначе нельзя менять duration

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(Leaf, Dust);
        main.gravityModifier = 0.25f;
        main.maxParticles = 48;

        var em = ps.emission;
        em.enabled = false;            // частицы только через Emit()

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 38f;
        sh.radius = 0.35f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }
}
