// Assets/_Project/Scripts/Location/GateBridgeOwned.cs (НЕ в Editor)
// Метка «этим корнем владеет сборка моста» (Tools > BuilderSim > Локация > Собрать подвесной мост и финальный остров).
// Повторная сборка пересоздаёт только объекты с этой меткой и больше ничего под GateBridge не трогает:
// ручные правки, чужие объекты и старые Stage1..3, StubIsland, Arch, Guards (их сборка только выключает) остаются как есть.
using UnityEngine;

public class GateBridgeOwned : MonoBehaviour
{
    [Tooltip("Версия сборки, которой создан корень (для отладки)")]
    public int version = 1;
}
