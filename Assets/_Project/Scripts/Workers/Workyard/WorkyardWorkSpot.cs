using UnityEngine;

// Opens the existing camp window, with its existing Take/Sell/Pack actions.
public sealed class WorkyardWorkSpot : MonoBehaviour,IInteractable
{
    public WorkerCamp camp;
    public string prompt="открыть склад артели";
    public float radius=1f;
    void OnEnable(){Interactables.Register(this);}
    void OnDisable(){Interactables.Unregister(this);}
    public Transform Anchor {get{return transform;}}
    public Vector3 PromptPoint {get{return transform.position+Vector3.up*1.4f;}}
    public string Prompt {get{return prompt;}}
    public float Radius {get{return radius;}}
    public bool CanInteract {get{return camp!=null && camp.CanInteract;}}
    public void Interact(){if(CanInteract)camp.Interact();}
}
