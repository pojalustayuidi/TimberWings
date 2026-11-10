using UnityEngine;

public sealed class WorkyardProp : MonoBehaviour
{
    [Tooltip("Можно двигать весь объект вместе с его рабочим местом и коллизией.")]
    public string description;
    [HideInInspector] public string key;
    [HideInInspector] public Vector3 defaultPosition,defaultScale;
    [HideInInspector] public Quaternion defaultRotation;
}
