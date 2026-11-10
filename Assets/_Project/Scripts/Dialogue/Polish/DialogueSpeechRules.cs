public static class DialogueSpeechRules
{
    public static bool AllowsRecorded(DialogueSpeechMode mode) { return mode == DialogueSpeechMode.Recorded; }
    public static bool AllowsBabble(DialogueSpeechMode mode, bool fallback)
    { return mode == DialogueSpeechMode.Babble || (mode == DialogueSpeechMode.Recorded && fallback); }
}
