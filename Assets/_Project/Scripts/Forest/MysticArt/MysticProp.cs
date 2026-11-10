using UnityEngine;

[DisallowMultipleComponent]
public sealed class MysticProp : MonoBehaviour
{
    public string key;
    [TextArea] public string description;
    [HideInInspector] public Vector3 defaultPosition,defaultScale;
    [HideInInspector] public Quaternion defaultRotation;
}
