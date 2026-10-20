// Кладите в Assets/_Game/Campfire (или любую НЕ-Editor папку), рядом с HutStages.cs
// Переключает этапы торговой лавки: 1 = помост, 2 = каркас + прилавок, 3 = готовая лавка.
using System;
using UnityEngine;

public class ShopStages : MonoBehaviour
{
    [Range(1, 3)] public int stage = 3;

    [Tooltip("Козелки, ящик, доски — видны на этапах 1–2")]
    public GameObject constructionProps;
    [Tooltip("Столбы, прилавок, стеллажи, поддоны")]
    public GameObject stage2;
    [Tooltip("Крыша, вывеска, бревна, реквизит на прилавке")]
    public GameObject stage3;

    public event Action<int> StageChanged;

    public int Stage => stage;

    void Awake() { Apply(); }

    public void SetStage(int s)
    {
        stage = Mathf.Clamp(s, 1, 3);
        Apply();
        StageChanged?.Invoke(stage);
    }

    public void Next() { if (stage < 3) SetStage(stage + 1); }

    // Загрузка сохранения: этап ставится без события StageChanged
    public void RestoreStage(int s)
    {
        stage = Mathf.Clamp(s, 1, 3);
        Apply();
    }

    public void Apply()
    {
        if (constructionProps) constructionProps.SetActive(stage <= 2);
        if (stage2) stage2.SetActive(stage >= 2);
        if (stage3) stage3.SetActive(stage >= 3);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
    }
#endif
}
