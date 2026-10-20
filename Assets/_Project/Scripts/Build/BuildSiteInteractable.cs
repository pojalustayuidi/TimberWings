// Assets/_Project/Scripts/Build/BuildSiteInteractable.cs (НЕ в Editor)
// «Нажми E, чтобы строить»: делает из площадки стройки объект для InteractionHub и открывает BuildWindow.
// Вешается на тот же объект, что и BuildingController. Руками добавлять не нужно: контроллер добавляет его сам,
// если включено BuildingController.useWindow.
// Доступно, только пока игрок стоит на площадке (в триггере), стройка не закрыта, не достроена и не идёт показ стадии (BuildReveal).
// Пока этап строится, E раскрывает свёрнутую полоску обратно в окно (BuildWindow.Toggle).
using UnityEngine;

[RequireComponent(typeof(BuildingController))]
public class BuildSiteInteractable : MonoBehaviour, IInteractable, IPriorityInteractable, IHidesPrompt
{
    [Tooltip("Как далеко от центра площадки ищется эта стройка. Реально доступна только внутри триггера, это значение лишь верхний предел.")]
    public float radius = 6f;
    [Tooltip("Где над площадкой показывается «Нажми E». Метка с названием стоит выше (BuildSiteTagView.offset), эта ниже, чтобы не налезали друг на друга.")]
    public Vector3 promptOffset = new Vector3(0f, 1.6f, 0f);

    BuildingController bc;

    void Awake() { bc = GetComponent<BuildingController>(); }
    void OnEnable() { Interactables.Register(this); }
    void OnDisable() { Interactables.Unregister(this); }

    // ------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return transform.position + promptOffset; } }
    public float Radius { get { return radius; } }
    public bool Priority { get { return true; } }   // E принадлежит стройке, даже если рядом дерево

    public string Prompt { get { return bc != null && bc.StageRunning ? "открыть стройку" : "строить"; } }

    // пока окно свёрнуто в полоску, подсказку над зданием не рисуем (у полоски своя клавиша E), но E работает
    public bool PromptHidden
    {
        get { var w = BuildWindow.Instance; return bc != null && w != null && w.IsCollapsed && BuildWindow.IsOpenFor(bc); }
    }

    public bool CanInteract
    {
        get { return bc != null && bc.UseWindow && bc.PlayerInside && !bc.IsFinished && !bc.IsLocked && !bc.IsRevealing; }
    }

    public void Interact()
    {
        if (!CanInteract) return;
        BuildWindow.Toggle(bc);
    }
}
