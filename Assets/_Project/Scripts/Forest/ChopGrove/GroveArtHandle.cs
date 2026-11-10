using UnityEngine;

[DisallowMultipleComponent]
[HelpURL("https://github.com/pojalustayuidi/TimberWings")]
public sealed class GroveArtHandle : MonoBehaviour
{
    [Tooltip("Объект можно свободно двигать, вращать и масштабировать. Play Mode не пересобирает поляну.")]
    public string description;
    [HideInInspector] public string key;
    [HideInInspector] public Vector3 defaultPosition;
    [HideInInspector] public Quaternion defaultRotation;
    [HideInInspector] public Vector3 defaultScale;
    [HideInInspector] public Vector2 halfSize;
    [HideInInspector] public float height;
    [HideInInspector] public bool blocksWalking;
}
