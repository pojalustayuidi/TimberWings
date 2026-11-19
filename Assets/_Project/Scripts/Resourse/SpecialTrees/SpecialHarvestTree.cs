using UnityEngine;

public enum SpecialTreeKind { Rune, Golden }

[DisallowMultipleComponent]
public sealed class SpecialHarvestTree : MonoBehaviour
{
    public const int RuneLogs = 2;
    public const int RuneEssence = 1;
    public const float RuneRegrowthSeconds = 240f;

    public SpecialTreeKind kind;
    [Tooltip("Постоянный ключ сохранения, независимый от положения дерева.")]
    public string saveId;
    public GameObject sectionPrefab;

    public bool IsGolden => kind == SpecialTreeKind.Golden;
    public int Logs => IsGolden ? GameBalance.SkillGoldenWood : RuneLogs;
    public int Essence => IsGolden ? 0 : RuneEssence;
    public int Cuts => IsGolden ? 0 : 1;
    public float RegrowthSeconds => IsGolden ? GameBalance.SkillGoldenInterval : RuneRegrowthSeconds;
}
