using UnityEngine;

public sealed partial class StoryInspection
{
    const string CluePrefix = "chapter1/clue/";
    internal const string AllClues = "chapter1/clue/complete";

    internal static bool KnowsAnyChapterClue => KnowsChapterClue(Kind.Knife) ||
        KnowsChapterClue(Kind.Ribbon) || KnowsChapterClue(Kind.Witness);

    internal static bool KnowsChapterClue(Kind clue) => DialogueSeen.Has(CluePrefix + clue);

    internal static void ImportChapterClues()
    {
        foreach (var inspection in FindObjectsByType<StoryInspection>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (inspection.superseded || !IsCoreClue(inspection.kind)) continue;
            var poi = inspection.GetComponent<MapPoi>();
            if (WasDiscovered(inspection.transform, MapPoi.GetInspectionId(inspection.transform, poi != null ? poi.inspectionId : null)))
                DialogueSeen.Mark(CluePrefix + inspection.kind);
        }
        if (KnowsChapterClue(Kind.Knife) && KnowsChapterClue(Kind.Ribbon) && KnowsChapterClue(Kind.Witness))
            DialogueSeen.Mark(AllClues);
    }

    static bool IsCoreClue(Kind clue) => clue == Kind.Knife || clue == Kind.Ribbon || clue == Kind.Witness;

    ChapterOneText.Cue ChapterCue()
    {
        if (StoryFlow.Instance == null) return null;
        if (engraveBridge) return bridgeEngraving != null ? ChapterOneText.Find("twv3_e06_07") : null;
        string key = subject switch
        {
            Kind.Knife => KnowsChapterClue(Kind.Ribbon) || KnowsChapterClue(Kind.Witness) ? "twv3_e01_02" : "twv3_e01_01",
            Kind.Ribbon => KnowsChapterClue(Kind.Knife) ? "twv3_e02_02" : "twv3_e02_01",
            Kind.Witness => "twv3_e03_01",
            Kind.BenchMarks => "twv3_e05_08",
            Kind.Board when detail != null => "twv3_e05_06",
            Kind.CopperArrow => "twv3_e06_04",
            Kind.Duck => "twv3_e06_06",
            Kind.Cart => "twv3_e06_05",
            _ => null
        };
        return key == null ? null : ChapterOneText.Find(key);
    }

    void RecordChapterClue()
    {
        if (StoryFlow.Instance == null || engraveBridge || !IsCoreClue(subject)) return;
        DialogueSeen.Mark(CluePrefix + subject);
        if (DialogueSeen.Has(AllClues) || !KnowsChapterClue(Kind.Knife) ||
            !KnowsChapterClue(Kind.Ribbon) || !KnowsChapterClue(Kind.Witness)) return;
        DialogueSeen.Mark(AllClues);
        ChapterOneVoice.Queue("twv3_e04_01", delay: .5f, lifetime: 15f);
    }
}
