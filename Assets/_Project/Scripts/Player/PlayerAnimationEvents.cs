// Assets/_Project/Scripts/Player/PlayerAnimationEvents.cs
// Повесь на объект с Animator (модель персонажа). Animation Events в клипах зовут эти методы.
//   OnChopHit    - кадр, где топор бьёт по дереву. Включи PlayerInteraction.useAnimationEvents, и звук, тряска и щепки
//                  будут ровно в этом кадре, а не по таймеру.
//   OnBuildHit   - удар молотка при стройке (звук "build").
//   OnFootstep   - пустышка: шаги сейчас считаются по пройденному пути в SfxManager. Метод нужен, чтобы клипы Run/Walk
//                  со своими событиями шагов не сыпали ошибками «has no receiver».
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimationEvents : MonoBehaviour
{
    PlayerInteraction pi;

    void Awake() { pi = GetComponentInParent<PlayerInteraction>(); }

    public void OnChopHit()
    {
        if (pi == null) pi = GetComponentInParent<PlayerInteraction>();
        if (pi != null && pi.useAnimationEvents) pi.NotifyHit();
    }

    public void OnBuildHit() { SfxManager.Play("build", 0.8f, Random.Range(0.95f, 1.05f)); }

    public void OnFootstep() { }
}
