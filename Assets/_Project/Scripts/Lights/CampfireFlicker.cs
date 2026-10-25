using UnityEngine;

/// <summary>
/// Раньше сам мерцал светом и пламенем костра. Теперь тонкая обёртка над единым LightFlicker (Scripts/Lights/LightFlicker.cs).
/// Класс, поля и Configure(Light, Transform) остались прежними, поэтому ссылки в сцене и CampfireBuilder не ломаются.
/// Висит на Stage3, поэтому работает только когда костёр горит. Сам Light ведёт FireSource (пул), здесь он нужен как ссылка.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Campfire Flicker")]
public class CampfireFlicker : MonoBehaviour
{
    [SerializeField] private Light fireLight;
    [SerializeField] private Transform flames;
    [SerializeField] private float flickerAmount = 0.35f;
    [SerializeField] private float speed = 6f;
    [SerializeField] private float scaleAmount = 0.08f;

    public void Configure(Light l, Transform f)
    {
        fireLight = l;
        flames = f;
        Push();
    }

    private void Awake()
    {
        Push();
    }

    private void Push()
    {
        if (!Application.isPlaying) return;   // в редакторе (CampfireBuilder) лишний компонент в сцену не кладём
        var lf = GetComponent<LightFlicker>();
        if (lf == null) lf = gameObject.AddComponent<LightFlicker>();
        lf.Configure(FlickerPreset.Campfire, fireLight, flames != null ? new[] { flames } : null,
                     flickerAmount, speed, scaleAmount);
    }
}
