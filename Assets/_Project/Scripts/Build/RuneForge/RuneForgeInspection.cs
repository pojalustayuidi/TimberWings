using UnityEngine;

[DisallowMultipleComponent]
public sealed class RuneForgeInspection : MonoBehaviour, IInteractable, IPriorityInteractable
{
    public RuneForge forge;
    public bool inside;

    public Transform Anchor => transform;
    public Vector3 PromptPoint => transform.position + Vector3.up * (inside ? 1.05f : 1.35f);
    public string Prompt => inside ? ChopPrototypeLog.Text("открыть кузницу", "open the forge") : "осмотреть кузницу";
    public float Radius => inside ? 1.35f : 1.5f;
    public bool Priority => true;
    public bool CanInteract => forge != null && forge.CanInspect && forge.IsRestored == inside;

    void OnEnable() => Interactables.Register(this);
    void OnDisable() => Interactables.Unregister(this);

    internal bool CanUse(PlayerAxeEquipment axes)
    {
        if (!inside || !isActiveAndEnabled || !CanInteract || axes == null || axes != PlayerInteraction.CurrentAxes)
            return false;
        Vector3 delta = axes.transform.position - transform.position;
        float reach = Radius * 1.15f;
        return Mathf.Abs(delta.y) < 1.4f && delta.x * delta.x + delta.z * delta.z <= reach * reach;
    }

    public void Interact()
    {
        if (!CanInteract) return;
        if (inside)
        {
            var axes = PlayerInteraction.CurrentAxes;
            if (CanUse(axes)) AxeWindow.Open(axes, workshop: StoryFlow.Instance, table: this);
            return;
        }
        string message;
        if (forge.build.IsLocked)
            message = "Горн погас после шторма. К ремонту можно приступить, когда будут готовы хижина и рабочий двор.";
        else
            message = $"На верстаке осталась смета Гуннара: {forge.build.PlankTotal} досок и {forge.build.CoinTotal} монет.";
        SaveIndicator.ShowToast(message);
    }
}
