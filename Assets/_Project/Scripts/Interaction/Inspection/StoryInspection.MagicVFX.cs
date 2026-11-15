using UnityEngine;
using UnityEngine.Rendering;

// 3.4 Story & Magic VFX. This is part of the existing StoryInspection component,
// not a second inspection system. All particle objects are temporary and local.
public sealed partial class StoryInspection
{
    ParticleSystem storyMagic;
    Material storyMagicMaterial;
    GameObject storyMagicObject;

    bool WantsStoryMagic()
    {
        if (engraveBridge) return true;
        switch (subject)
        {
            case Kind.Knife:
            case Kind.Ribbon:
            case Kind.Board:
            case Kind.Finale:
            case Kind.BenchMarks:
            case Kind.Witness:
            case Kind.ThreeMarks:
            case Kind.CopperArrow:
                return true;
            default:
                return false; // no sparks over ordinary props, ducks or carts
        }
    }

    void StartStoryMagic()
    {
        if (storyMagic != null || !WantsStoryMagic() || VibeSettings.ReduceEffects) return;
        storyMagicMaterial = VibeGfx.ParticleMat(VibeGfx.Soft);
        if (storyMagicMaterial == null) return; // shader stripped from build
        storyMagicObject = new GameObject("StoryInspection_MagicMotes");
        storyMagicObject.transform.SetParent(transform, false);
        storyMagicObject.transform.position = Focus;
        storyMagic = storyMagicObject.AddComponent<ParticleSystem>();
        storyMagic.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = storyMagic.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.035f, 0.095f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        main.startColor = engraveBridge || subject == Kind.Finale
            ? new Color(0.40f, 0.98f, 0.88f, 0.72f)
            : new Color(0.89f, 0.76f, 1.0f, 0.58f);
        main.maxParticles = 24;
        main.gravityModifier = 0f;

        var shape = storyMagic.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.22f;
        var emission = storyMagic.emission;
        emission.rateOverTime = 2.1f;

        var velocity = storyMagic.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // Same curve mode for all axes; keeps the 3.3.1 Unity fix intact.
        velocity.x = new ParticleSystem.MinMaxCurve(-0.025f, 0.025f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.045f, 0.12f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.025f, 0.025f);
        var noise = storyMagic.noise;
        noise.enabled = true;
        noise.strength = 0.025f;
        noise.frequency = 0.28f;

        var color = storyMagic.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.28f),
                    new GradientAlphaKey(0.75f, 0.75f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var renderer = storyMagic.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = storyMagicMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        storyMagic.Play();
    }

    void PulseStoryMagic()
    {
        if (storyMagic == null || VibeSettings.ReduceEffects) return;
        storyMagic.Emit(5);
    }

    void UpdateStoryMagic()
    {
        if (storyMagic == null) return;
        storyMagic.transform.position = Focus;
        var emission = storyMagic.emission;
        emission.rateOverTime = releasing || VibeSettings.ReduceEffects ? 0f : 2.1f;
    }

    void StopStoryMagic()
    {
        if (storyMagic != null) storyMagic.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    void DisposeStoryMagic()
    {
        if (storyMagicObject != null) Destroy(storyMagicObject);
        storyMagic = null;
        storyMagicObject = null;
        if (storyMagicMaterial != null) Destroy(storyMagicMaterial);
        storyMagicMaterial = null;
    }
}
