using UnityEngine;

// Service marker written by the Editor installer (Tools > BuilderSim > UI > Главное меню > Дневник).
// It remembers the rect values the installer applied last time. On the next update the installer compares the
// current rect with this stamp: equal => the object was not touched by hand and may be re-laid out,
// different => it was adjusted manually and is preserved. Contains no runtime logic.
[DisallowMultipleComponent, AddComponentMenu("")]
public sealed class TWJournalLayoutStamp : MonoBehaviour
{
    public Vector2 position;
    public Vector2 size;
    public float rotationZ;
    public int revision;
}
