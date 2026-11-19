using UnityEngine;

[DisallowMultipleComponent]
public sealed class RuneForgeInspection : MonoBehaviour, IInteractable, IPriorityInteractable
{
    public RuneForge forge;
    public bool inside;

    public Transform Anchor => transform;
    public Vector3 PromptPoint => transform.position + Vector3.up * (inside ? 1.05f : 1.35f);
    public string Prompt => inside ? "осмотреть рунический стол" : "осмотреть кузницу";
    public float Radius => inside ? 1.35f : 1.5f;
    public bool Priority => true;
    public bool CanInteract => forge != null && forge.CanInspect && forge.IsRestored == inside;

    void OnEnable() => Interactables.Register(this);
    void OnDisable() => Interactables.Unregister(this);

    public void Interact()
    {
        if (!CanInteract) return;
        string message;
        if (inside)
            message = "На руническом столе остались зарисовки Гуннара и гнездо для эссенции.";
        else if (forge.build.IsLocked)
            message = "Горн погас после шторма. К ремонту можно приступить, когда будут готовы хижина и рабочий двор.";
        else
            message = $"На верстаке осталась смета Гуннара: {forge.build.PlankTotal} досок и {forge.build.CoinTotal} монет.";
        SaveIndicator.ShowToast(message);
    }
}
