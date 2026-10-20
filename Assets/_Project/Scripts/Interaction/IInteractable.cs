// Assets/_Project/Scripts/Interaction/IInteractable.cs (НЕ в Editor)
// «С этим можно что-то сделать по кнопке E». Реализуют NPC (DialogueSequence), позже здания, доска заданий, прилавок.
// Регистрируется в OnEnable через Interactables.Register(this), снимается в OnDisable.
// Выбирает и запускает ближайшее InteractionHub (висит на игроке).
using System.Collections.Generic;
using UnityEngine;

public interface IInteractable
{
    Transform Anchor { get; }          // откуда считается расстояние
    Vector3 PromptPoint { get; }       // где показать «Нажми E»
    string Prompt { get; }             // «поговорить» (подставляется в «Нажми E, чтобы …»)
    float Radius { get; }              // на каком расстоянии (по земле) доступно
    bool CanInteract { get; }          // сейчас можно? (разговор уже идёт или был, здание не готово и т.п.)
    void Interact();
}

// Необязательно. Если Priority = true, объект забирает E даже тогда, когда дерево ближе (NPC и прилавок рядом с лесом).
public interface IPriorityInteractable
{
    bool Priority { get; }
}

// Необязательно. PromptHidden = true: E по-прежнему работает, но подсказка «Нажми E» над объектом не рисуется
// (стройка, пока окно свёрнуто в полоску: у полоски своя подсказка с клавишей, а здание должно быть видно).
public interface IHidesPrompt
{
    bool PromptHidden { get; }
}

public static class Interactables
{
    public static readonly List<IInteractable> All = new List<IInteractable>();

    public static void Register(IInteractable i)
    {
        if (i != null && !All.Contains(i)) All.Add(i);
    }

    public static void Unregister(IInteractable i)
    {
        All.Remove(i);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAll() { All.Clear(); }
}
