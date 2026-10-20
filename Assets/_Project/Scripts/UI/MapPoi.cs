// Assets/_Project/Scripts/UI/MapPoi.cs (НЕ в Editor)
// Ручная метка на карте: повесь на любой объект, и HudMinimap покажет его значком.
//   kind  = ключ типа из HudMinimap.kinds (например PAD_Market, PAD_QuestBoard, CAMPFIRE_Point)
//   label = подпись на большой карте (пусто = подпись типа)
// Добавь этот файл, только если в проекте ещё нет MapPoi.
using UnityEngine;

public class MapPoi : MonoBehaviour
{
    public string kind = "PAD_Market";
    public string label = "";
}