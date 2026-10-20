// Assets/_Project/Scripts/UI/ChopRewardsTester.cs
// Повесь на любой объект, нажми Play и жми G (или ПКМ по компоненту -> «Дать награду»).
// Нужен только чтобы проверить анимацию, не рубя дерево. Потом можно удалить.
using UnityEngine;

public class ChopRewardsTester : MonoBehaviour
{
    public int logs = 2;
    public int xp = 2;
    [Tooltip("Откуда вылетают брёвна. Пусто = точка перед камерой")]
    public Transform at;

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.gKey.wasPressedThisFrame) Fire();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.G)) Fire();
#endif
    }

    [ContextMenu("Дать награду")]
    public void Fire()
    {
        Vector3 pos;
        if (at != null) pos = at.position;
        else if (Camera.main != null) pos = Camera.main.transform.position + Camera.main.transform.forward * 10f;
        else pos = Vector3.zero;

        // В настоящей игре плашки показывает UIManager при AddWood/AddXP.
        // Тестер счётчики не трогает, поэтому показывает плашки сам.
        ChopRewards.Give(pos, logs, xp,
            () => HudGain.Show(HudGain.Kind.Wood, 1),
            () => HudGain.Show(HudGain.Kind.Xp, xp));
    }
}