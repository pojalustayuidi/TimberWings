using UnityEngine;

public class RandomModel : MonoBehaviour
{
    [Header("Модельки (дочерние объекты)")]
    public GameObject[] treeModels;

    [Header("Разнообразие")]
    public bool randomYRotation = true;
    public Vector2 scaleRange = new Vector2(0.9f, 1.15f);

    [Header("Оптимизация")]
    [Tooltip("Удалять ненужные модели. Выключи, если другой скрипт ссылается на них.")]
    public bool destroyUnused = true;

    void Awake()
    {
        if (treeModels == null || treeModels.Length == 0) return;

        // сгенерированная модель (TreeMeshFactory) заменяет все эти: ничего не выбираем и не трогаем
        var ri = GetComponentInParent<ResourceItem>();
        if (ri != null && ri.useGeneratedModel) return;

        // выбор модели, поворот и масштаб считаются от seed позиции: лес выглядит одинаково при каждом запуске
        // (раньше UnityEngine.Random: размеры «прыгали» между запусками)
        var rng = new System.Random((int)TreeSizing.Hash(transform.position));

        int pick = rng.Next(0, treeModels.Length);
        for (int i = 0; i < treeModels.Length; i++)
        {
            if (treeModels[i] == null) continue;
            if (i == pick) treeModels[i].SetActive(true);
            else if (destroyUnused) Destroy(treeModels[i]);
            else treeModels[i].SetActive(false);
        }

        Transform t = treeModels[pick] != null ? treeModels[pick].transform : null;
        if (t == null) return;

        if (randomYRotation)
        {
            // поворот вокруг вертикальной оси РОДИТЕЛЯ, исходный наклон модели сохраняется
            t.localRotation = Quaternion.AngleAxis((float)(rng.NextDouble() * 360.0), Vector3.up) * t.localRotation;
        }

        t.localScale *= Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
    }
}