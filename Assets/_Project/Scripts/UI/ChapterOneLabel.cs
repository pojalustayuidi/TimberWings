using TMPro;
using UnityEngine;

// Bind only known static labels. Dynamic counters and captions keep control of their own text.
internal sealed class ChapterOneLabel : MonoBehaviour
{
    TMP_Text label;
    string source, shown;

    internal static void Bind(TMP_Text label, string text)
    {
        if (string.IsNullOrEmpty(text) || ChapterOneText.TranslateLegacy(text) == text) return;
        var localizer = label.gameObject.AddComponent<ChapterOneLabel>();
        localizer.label = label;
        localizer.source = text;
        localizer.shown = text;
        localizer.Refresh();
    }

    void OnEnable()
    {
        GameLanguage.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => GameLanguage.Changed -= Refresh;

    void Refresh()
    {
        if (label == null || label.text != shown) return;
        shown = GameLanguage.T(source);
        label.text = shown;
    }
}
