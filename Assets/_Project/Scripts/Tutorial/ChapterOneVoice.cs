using System;
using System.Collections.Generic;
using UnityEngine;

internal static class ChapterOneVoice
{
    internal enum Priority { Story, Lesson, Reward, Atmosphere }

    sealed class Pending
    {
        internal ChapterOneText.Cue cue;
        internal Priority priority;
        internal float ready, expires;
        internal Func<bool> stillRelevant;
        internal string exclusiveGroup;
    }

    static readonly List<Pending> pending = new();
    const string SeenPrefix = "chapter1/voice/";
    static float nextOptional, nextAtmosphere;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void Reset()
    {
        pending.Clear();
        nextOptional = nextAtmosphere = 0f;
    }

    internal static bool Heard(string id) => DialogueSeen.Has(SeenPrefix + id);

    internal static void Mark(string id) => DialogueSeen.Mark(SeenPrefix + id);

    internal static void Queue(string id, Priority priority = Priority.Story, float delay = .15f,
        float lifetime = 12f, Func<bool> stillRelevant = null, string exclusiveGroup = null)
    {
        if (StoryFlow.Instance == null || SaveSystem.IsLoading || Heard(id)) return;
        if (priority >= Priority.Reward && !DialoguePreferences.PlayerComments) return;

        var cue = ChapterOneText.Find(id);
        if (cue == null) return;

        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var other = pending[i];
            if (other.cue.id == id) return;
            if (!string.IsNullOrEmpty(exclusiveGroup) && other.exclusiveGroup == exclusiveGroup)
                pending.RemoveAt(i);
        }

        float now = Time.unscaledTime;
        pending.Add(new Pending
        {
            cue = cue,
            priority = priority,
            ready = now + delay,
            expires = now + delay + lifetime,
            stillRelevant = stillRelevant,
            exclusiveGroup = exclusiveGroup
        });
    }

    internal static void Tick()
    {
        if (pending.Count == 0) return;
        float now = Time.unscaledTime;
        pending.RemoveAll(line => now > line.expires || Heard(line.cue.id) ||
            (line.stillRelevant != null && !line.stillRelevant()));

        if (pending.Count == 0 || !Application.isFocused || Time.timeScale <= 0f || MobileInput.ModalOpen ||
            SaveSystem.IsLoading || DialogueManager.IsBusy || DialogueSequence.AnyPlaying ||
            StoryInspection.Active || BuildingController.AnyRevealing || BuildingController.AnyRestorePending) return;

        Pending best = null;
        foreach (var line in pending)
        {
            if (now < line.ready) continue;
            if (line.priority >= Priority.Reward && (!DialoguePreferences.PlayerComments || now < nextOptional))
                continue;
            if (line.priority == Priority.Atmosphere && now < nextAtmosphere) continue;
            if (best == null || line.priority < best.priority) best = line;
        }

        if (best == null || !DialogueManager.TryChapterCue(best.cue)) return;
        pending.Remove(best);
        Mark(best.cue.id);
        if (best.priority >= Priority.Reward) nextOptional = now + 75f;
        if (best.priority == Priority.Atmosphere) nextAtmosphere = now + 240f;
    }

    internal static void SpeakSequence(Action done, params string[] ids) => SpeakSequence(done, null, ids);

    internal static void SpeakSequence(Action done, Func<bool> inContext, params string[] ids)
    {
        DialogueManager.BeginConversation();
        Speak(0);

        void Speak(int index)
        {
            if (DialogueManager.SkipAllRequested || index >= ids.Length || inContext != null && !inContext())
            {
                DialogueManager.EndConversation();
                done?.Invoke();
                return;
            }

            var cue = ChapterOneText.Find(ids[index]);
            if (cue == null)
            {
                Speak(index + 1);
                return;
            }

            Mark(cue.id);
            DialogueManager.SayAs(cue.speaker, cue.ru, null, () => Speak(index + 1));
        }
    }
}
