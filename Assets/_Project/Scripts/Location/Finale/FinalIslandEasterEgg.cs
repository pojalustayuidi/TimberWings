using UnityEngine;

public sealed class FinalIslandEasterEgg : MonoBehaviour, IInteractable
{
    public Transform Anchor => transform;
    public Vector3 PromptPoint => transform.position + Vector3.up * 1.05f;
    public string Prompt => "рассмотреть зарубку";
    public float Radius => 1.45f;
    public bool CanInteract
    {
        get
        {
            var player = PlayerMovement.Instance;
            return player != null && Mathf.Abs(player.transform.position.y - transform.position.y) < 1.4f;
        }
    }

    void OnEnable() => Interactables.Register(this);
    void OnDisable() => Interactables.Unregister(this);

    public void Interact()
    {
        if (!CanInteract) return;
        var inspection = StoryInspection.For(this);
        if (inspection != null)
        {
            inspection.TryPlay(this, StoryInspection.FinaleLine);
            return;
        }
        SaveIndicator.ShowToast("VII. Седьмую скамью я всё-таки унёс. — Гуннар");
        MapPoi.ReportInspection(transform);
    }
}
