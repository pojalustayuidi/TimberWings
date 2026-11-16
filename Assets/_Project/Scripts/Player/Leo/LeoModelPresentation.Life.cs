using UnityEngine;

public sealed partial class LeoModelPresentation
{
    readonly LeoBuildMotion work = new LeoBuildMotion();

    [Tooltip("Показывать в консоли причины, по которым Лео не начинает удар")]
    public bool debugWork;
}
