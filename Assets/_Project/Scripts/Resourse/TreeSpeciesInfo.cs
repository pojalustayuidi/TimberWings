// Assets/_Project/Scripts/Resource/TreeSpeciesInfo.cs (НЕ в Editor)
// Габариты хитбоксов дерева-вида (Cedar, Birch, Autumn, Willow_Dead, Tree_Random из Tools → Forest → Trees → Build Species).
// Префаб с таким компонентом и useGeneratedModel = false получает от TreeCollision.Setup зону выбора и ствол по этим числам,
// а не по bounds рендереров (по bounds у берёзы с двумя тонкими стволами ствол вышел бы слишком толстым).
// Значения в локальных метрах корня (масштаб размера TreeScaleMul применяется поверх, как у сгенерированных деревьев).
// Ничего не делает в рантайме сам: это только данные.
using UnityEngine;

public class TreeSpeciesInfo : MonoBehaviour
{
    [Tooltip("Вид дерева (для отладки и поиска)")]
    public string species = "";
    [Tooltip("Зона выбора по кроне (триггер на корне): радиус и высота")]
    public float pickRadius = 0.7f;
    public float pickHeight = 4.3f;
    [Tooltip("Сплошной ствол (TrunkSolid): радиус и высота")]
    public float trunkRadius = 0.32f;
    public float trunkHeight = 1.8f;
}
