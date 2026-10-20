using UnityEngine;

// Раньше добавлял SmartWorldTag к игроку, чтобы табличка рюкзака не уезжала под HUD.
// Теперь рюкзак рисует PlayerHeadHud (сам держит себя на экране), поэтому компонент ничего не делает.
// Файл оставлен, чтобы не терялась ссылка в сцене и в Editor/BuilderSimPolishSetup.
public class BackpackTagDistinct : MonoBehaviour
{
    public Vector3 offset = new Vector3(0, 2.9f, 0);
    public Color frame = new Color32(0x6B, 0xCB, 0x5A, 255);
}
