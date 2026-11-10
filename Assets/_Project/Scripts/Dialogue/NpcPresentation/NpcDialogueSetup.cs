using UnityEngine;

// Editor-authored ownership record. No runtime construction or gameplay logic.
public sealed class NpcDialogueSetup : MonoBehaviour
{
    public DialoguePolishView view;
    public NpcDialoguePortraits portraits;
    public NpcDialogueOutline outline;
    public GameObject decoration,portraitObject;
    public string backup;
}
