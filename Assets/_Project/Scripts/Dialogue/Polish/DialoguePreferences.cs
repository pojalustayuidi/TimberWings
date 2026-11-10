using UnityEngine;

public static class DialoguePreferences
{
    const string CommentsKey = "tw_dialogue_comments";
    const string AutoKey = "tw_dialogue_auto";
    static int comments = -1, automatic = -1;
    public static bool PlayerComments
    {
        get { if (comments < 0) comments = PlayerPrefs.GetInt(CommentsKey, 1); return comments == 1; }
        set { comments = value ? 1 : 0; PlayerPrefs.SetInt(CommentsKey, comments); }
    }
    public static bool AutoAdvance
    {
        get { if (automatic < 0) automatic = PlayerPrefs.GetInt(AutoKey, 1); return automatic == 1; }
        set { automatic = value ? 1 : 0; PlayerPrefs.SetInt(AutoKey, automatic); }
    }
    // A real conversation releases the cursor. Non-blocking player thoughts never enter this state.
    public static bool ConversationOwnsCamera
    { get { return DialogueSequence.AnyPlaying && DialogueSequence.FreezesPlayer && DialogueSequence.Focus != null; } }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { comments = automatic = -1; }
}
