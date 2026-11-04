using UnityEngine;

/// <summary>
/// Timber Wings / unified camp center: softly adjusts only the art add-on's
/// audio and particle emission to the game's DayNightCycle.
/// It does not write saves, change lighting, move the camera or affect NPCs.
/// Only attached to the final Campfire/Stage3 hierarchy.
/// </summary>
[DisallowMultipleComponent]
public sealed class TWCampCenterAtmosphere : MonoBehaviour
{
    [SerializeField] AudioSource campfireLoop;
    [SerializeField] ParticleSystem warmMotes;
    [Range(0f, 1f)] public float baseVolume = 0.19f;
    [Range(0f, 20f)] public float baseMotesPerSecond = 2.8f;
    float nextUpdate;

    public void Configure(AudioSource source, ParticleSystem motes)
    {
        campfireLoop = source;
        warmMotes = motes;
    }

    void OnEnable()
    {
        nextUpdate = 0f;
        if (campfireLoop != null && campfireLoop.clip != null && !campfireLoop.isPlaying)
            campfireLoop.Play();
    }

    void Update()
    {
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 0.25f;
        float night = Mathf.Clamp01(DayNightCycle.Night01);
        if (campfireLoop != null)
        {
            // A fire is audible during the day, only slightly warmer at night.
            float target = baseVolume * Mathf.Lerp(0.83f, 1f, night);
            campfireLoop.volume = Mathf.MoveTowards(campfireLoop.volume, target, 0.05f);
        }
        if (warmMotes != null)
        {
            var emission = warmMotes.emission;
            emission.rateOverTime = baseMotesPerSecond * Mathf.Lerp(0.35f, 1f, night);
        }
    }
}
