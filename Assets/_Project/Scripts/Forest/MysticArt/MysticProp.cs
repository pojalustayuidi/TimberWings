using UnityEngine;

[DisallowMultipleComponent]
public sealed class MysticProp : MonoBehaviour, IInteractable
{
    public string key;
    [TextArea] public string description;
    [HideInInspector] public Vector3 defaultPosition,defaultScale;
    [HideInInspector] public Quaternion defaultRotation;

    public string InspectionTitle
    {
        get
        {
            if (key == "Bench_4" && transform.Find("Engraving_Bench") != null) return "Зарубки на скамье";
            if (key == "Witness_1" && transform.Find("Engraving_Witness") != null) return "Камень-свидетель";
            if (key == "Gunnar_Memory" && transform.Find("Bridge_Engraving") != null) return "Знак Гуннара";
            return null;
        }
    }

    public Transform Anchor => transform;
    public Vector3 PromptPoint => transform.position + Vector3.up * 1.05f;
    public string Prompt => "осмотреть гравировку";
    public float Radius => 2.2f;
    public bool CanInteract
    {
        get
        {
            var player = PlayerMovement.Instance;
            return player != null && Mathf.Abs(player.transform.position.y - transform.position.y) < 2f
                && InspectionTitle != null;
        }
    }

    void OnEnable()
    {
        if (key == "Bench_4" || key == "Witness_1" || key == "Gunnar_Memory") Interactables.Register(this);
    }

    void OnDisable() { Interactables.Unregister(this); }

    public void Interact()
    {
        if (!CanInteract) return;
        string line;
        switch (key)
        {
            case "Bench_4": line = "Зарубки идут по порядку. После шестой на скамье осталось пустое место."; break;
            case "Witness_1": line = "Рядом со старой надписью выбит знак моста."; break;
            default: line = "Кто-то вырезал на дереве мост с двумя башнями."; break;
        }
        SaveIndicator.ShowToast(line);
        MapPoi.ReportInspection(transform);
    }
}
