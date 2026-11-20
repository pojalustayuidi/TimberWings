using System;
using UnityEngine;

public partial class StoryFlow
{
    const string BladeKnown = "chapter1/clue/blade";
    const string ArchKnown = "chapter1/clue/arch";
    float pollChapterAt;

    void EnableChapterOneEvents()
    {
        GameLanguage.Changed += RefreshChapterLanguage;
        SaveSystem.Loaded += ChapterOneLoaded;
        GameEvents.TreeChopped += ChapterOneTreeFell;
        GameEvents.ChopQuality += ChapterOneChopQuality;
        GameEvents.LevelUp += ChapterOneLevelUp;
        WorkerManager.OnRosterChanged += ChapterOneWorkerAssigned;
        WorkerManager.OnWorkerLevelUp += ChapterOneWorkerPromoted;
        WorkerPackSystem.OnPackOpened += ChapterOneWorkerHired;
        MapPoi.Inspected += ChapterOneInspected;
    }

    void DisableChapterOneEvents()
    {
        GameLanguage.Changed -= RefreshChapterLanguage;
        SaveSystem.Loaded -= ChapterOneLoaded;
        GameEvents.TreeChopped -= ChapterOneTreeFell;
        GameEvents.ChopQuality -= ChapterOneChopQuality;
        GameEvents.LevelUp -= ChapterOneLevelUp;
        WorkerManager.OnRosterChanged -= ChapterOneWorkerAssigned;
        WorkerManager.OnWorkerLevelUp -= ChapterOneWorkerPromoted;
        WorkerPackSystem.OnPackOpened -= ChapterOneWorkerHired;
        MapPoi.Inspected -= ChapterOneInspected;
        ChapterOneVoice.Reset();
    }

    void ChapterOneLoaded()
    {
        ChapterOneVoice.Reset();
        pollChapterAt = 0f;
        StoryInspection.ImportChapterClues();
    }

    void RefreshChapterLanguage()
    {
        if (!IntroFinished()) { IntroFlow.Instance?.RefreshChapterLanguage(); return; }
        if (phase == Phase.Offer && ci < chapters.Count)
        {
            var ch = chapters[ci];
            Objective(string.Format(GameLanguage.T(string.IsNullOrEmpty(ch.talkText) ? talkToText : ch.talkText),
                ChapterOneText.SpeakerName(ch.giver)));
        }
        else RefreshHud();
        pollAt = 0f;
    }

    bool IsNear(Transform spot, float radius)
    {
        FindRefs();
        if (player == null || spot == null) return false;
        var delta = spot.position - player.position;
        return Mathf.Abs(delta.y) < 3f && delta.x * delta.x + delta.z * delta.z < radius * radius;
    }

    bool AtSpeaker(string name, Transform spot, float distance = 7f)
    {
        var speaker = Speaker.Find(name);
        return speaker != null && speaker.isActiveAndEnabled && spot != null &&
            (speaker.transform.position - spot.position).sqrMagnitude < distance * distance &&
            IsNear(speaker.transform, 6f);
    }

    internal void ChapterOneCampfireLit()
    {
        bool miraNear = AtSpeaker(miraName, IntroFlow.Instance != null ? IntroFlow.Instance.campfirePoint : null);
        if (miraNear)
        {
            ChapterOneVoice.Queue("twv3_s03_04", stillRelevant: () =>
                AtSpeaker(miraName, IntroFlow.Instance != null ? IntroFlow.Instance.campfirePoint : null));
            ChapterOneVoice.Queue("twv3_s03_05", delay: 1f, stillRelevant: () =>
                CurrentChapterId == "supply" && AtSpeaker(miraName, IntroFlow.Instance != null ? IntroFlow.Instance.campfirePoint : null));
        }
        else ChapterOneVoice.Queue("twv3_s03_01", stillRelevant: () => CurrentChapterId == "supply");
    }

    internal void ChapterOneStockpiled(SupplyPoint point)
    {
        if (point != supply || point.delivered <= 0 || !point.Accepting) return;
        ChapterOneVoice.Queue("twv3_s03_03", ChapterOneVoice.Priority.Lesson,
            stillRelevant: () => point != null && point.Accepting);
    }

    void ChapterOneTreeFell()
    {
        if (IntroFlow.Instance != null && IntroFlow.Instance.Current == IntroFlow.Step.Chop)
            ChapterOneVoice.Queue("twv3_s02_04", ChapterOneVoice.Priority.Lesson, .55f, 7f);
        else if (UnityEngine.Random.value < .08f)
            ChapterOneVoice.Queue("twv3_amb_02", ChapterOneVoice.Priority.Atmosphere, .65f, 4f);
    }

    void ChapterOneChopQuality(int quality)
    {
        if (IntroFlow.Instance != null && IntroFlow.Instance.Current == IntroFlow.Step.Chop)
        {
            if (quality >= 2) ChapterOneVoice.Queue("twv3_s02_02", ChapterOneVoice.Priority.Lesson, .2f, 6f, exclusiveGroup: "timing");
            if (quality == 0 && !ChapterOneVoice.Heard("twv3_s02_02"))
                ChapterOneVoice.Queue("twv3_s02_03", ChapterOneVoice.Priority.Lesson, .2f, 3f, exclusiveGroup: "timing");
        }
        else if (quality >= 2 && UnityEngine.Random.value < .08f)
            ChapterOneVoice.Queue("twv3_amb_01", ChapterOneVoice.Priority.Atmosphere, .2f, 4f);
    }

    void ChapterOneLevelUp(int level)
    {
        if (level == GameBalance.UpgradeLevel) ChapterOneVoice.Queue("twv3_s05_01", ChapterOneVoice.Priority.Lesson);
        if (level == 3) ChapterOneVoice.Queue("twv3_s09_01", ChapterOneVoice.Priority.Lesson);
    }

    void OfferChapterCrew()
    {
        if (tent != null && AtSpeaker(oskarName, tent.transform))
        {
            ChapterOneVoice.Queue("twv3_s10_intro_01", stillRelevant: () => tent != null && AtSpeaker(oskarName, tent.transform));
            ChapterOneVoice.Queue("twv3_s10_intro_02", delay: .4f, stillRelevant: () =>
                tent != null && AtSpeaker(oskarName, tent.transform));
        }
        else ChapterOneVoice.Queue("twv3_s10_01");
    }

    void OfferChapterBridge()
    {
        if (bridge == null || bridge.IsFinished) return;
        if (AtSpeaker(oskarName, bridge.transform))
            ChapterOneVoice.Queue("twv3_s15_intro_01", stillRelevant: () => bridge != null && AtSpeaker(oskarName, bridge.transform));
        ChapterOneVoice.Queue("twv3_s15_01", stillRelevant: () => bridge != null && IsNear(bridge.transform, 12f));
    }

    void ChapterOneBuilt(BuildingController site)
    {
        if (site == null || SaveSystem.IsLoading) return;
        if (site == bridge && !site.IsFinished)
        {
            if (site.StepsDone == 1) ChapterOneVoice.Queue("twv3_s15_done_01");
            if (site.StepsDone == 2) ChapterOneVoice.Queue("twv3_s15_02");
        }
        if (site == hut && site.IsFinished && AtSpeaker(oskarName, site.transform))
            ChapterOneVoice.Queue("twv3_s06_repair_01", ChapterOneVoice.Priority.Lesson, 1f, 8f,
                () => site != null && AtSpeaker(oskarName, site.transform));
    }

    void ChapterOneCompleted(string id)
    {
        switch (id)
        {
            case "supply": ChapterOneVoice.Queue("twv3_s03_02"); break;
            case "hut": ChapterOneVoice.Queue("twv3_s06_01"); break;
            case "planks":
                ChapterOneVoice.Queue("twv3_s07_02");
                var fire = IntroFlow.Instance != null ? IntroFlow.Instance.campfirePoint : null;
                if (AtSpeaker(miraName, fire))
                    ChapterOneVoice.Queue("twv3_s07_mira_01", ChapterOneVoice.Priority.Lesson, 1f, 8f,
                        () => AtSpeaker(miraName, fire));
                break;
            case "market": ChapterOneVoice.Queue("twv3_s07_03"); break;
            case GunnarAxeChapter:
                ChapterOneVoice.Queue("twv3_s12_04");
                if (AtSpeaker(oskarName, GunnarAxeTarget()))
                    ChapterOneVoice.Queue("twv3_s12_05", ChapterOneVoice.Priority.Lesson, 1f, 8f,
                        () => AtSpeaker(oskarName, GunnarAxeTarget()));
                break;
            case "bridge": ChapterOneVoice.Queue("twv3_s15_done_03"); break;
        }
    }

    void ChapterOneWorkerAssigned()
    {
        var workers = WorkerManager.Instance;
        if (!SaveSystem.IsLoading && workers != null && workers.Sim != null && workers.WorkingCount > 0)
            ChapterOneVoice.Queue("twv3_s10_03");
    }

    void ChapterOneWorkerPromoted(WorkerState state)
    {
        if (state != null && state.level >= 6)
            ChapterOneVoice.Queue("twv3_r06", ChapterOneVoice.Priority.Reward, 1f, 60f);
    }

    void ChapterOneWorkerHired(PackResult result)
    {
        if (result.isNew && result.rarity == WorkerRarity.Legendary)
            ChapterOneVoice.Queue("twv3_r03", ChapterOneVoice.Priority.Reward, 1f, 85f);
    }

    internal void ChapterOnePetRolled(CrateType crate, PetInstance pet, bool firstFree, bool duplicate)
    {
        if (pet?.data == null) return;
        if (pet.data.id == "gunnar_wisp" && (crate == CrateType.River || crate == CrateType.Sky))
        {
            ChapterOneVoice.Queue("twv3_r02", ChapterOneVoice.Priority.Reward, 1f, 85f);
            return;
        }
        var rare = pet.data.rarity == PetRarity.Legendary && !ChapterOneVoice.Heard("twv3_r01") && DialoguePreferences.PlayerComments;
        if (rare) ChapterOneVoice.Queue("twv3_r01", ChapterOneVoice.Priority.Reward, 1f, 85f);
        else if (firstFree) ChapterOneVoice.Queue("twv3_s11_03", delay: 1f, lifetime: 120f);
        else if (duplicate) ChapterOneVoice.Queue("twv3_s11_07", ChapterOneVoice.Priority.Reward, 1f, 60f);
    }

    void PollChapterOne()
    {
        if (SaveSystem.IsLoading || BuildingController.AnyRestorePending || BuildingController.AnyRevealing) return;
        float now = Time.unscaledTime;
        if (now < pollChapterAt) return;
        pollChapterAt = now + .3f;

        if (CurrentChapterId == "bridge" && bridge != null && bridge.StepsDone == 2 && !bridge.IsFinished &&
            IsNear(bridge.transform, 7f) && !ChapterOneVoice.Heard("twv3_s15_02"))
            ChapterOneVoice.Queue("twv3_s15_alt_02", ChapterOneVoice.Priority.Lesson, .55f, 5f,
                () => bridge != null && !bridge.IsFinished && !ChapterOneVoice.Heard("twv3_s15_02"));

        if (CurrentChapterId == "pets")
        {
            var crate = PetCrateTarget();
            if (AtSpeaker(oskarName, crate))
                ChapterOneVoice.Queue("twv3_s11_oskar_01", stillRelevant: () =>
                    CurrentChapterId == "pets" && AtSpeaker(oskarName, PetCrateTarget()));
        }

        if (CurrentChapterId == "planks" && workshop != null && workshop.totalCollected == 1)
            ChapterOneVoice.Queue("twv3_s07_01", ChapterOneVoice.Priority.Lesson, .25f, 6f,
                () => CurrentChapterId == "planks" && !ChapterOneVoice.Heard("twv3_s07_oskar_01"));

        if (CurrentChapterId != "gunnar" || noteStarted || noteRead || finalBridge == null || finalBridge.Portal == null) return;
        var portal = finalBridge.Portal;
        FindRefs();
        if (player == null || !portal.OnFarIsland(player.position)) return;

        ChapterOneVoice.Queue("twv3_s16_arrive_01", stillRelevant: () => !noteStarted && !noteRead);
        if (!DialogueSeen.Has(ArchKnown) && IsNear(portal.Anchor, 4.5f))
        {
            DialogueSeen.Mark(ArchKnown);
            ChapterOneVoice.Queue(ArchVoice(), stillRelevant: () => !noteStarted && !noteRead);
        }
    }

    void ChapterOneInspected(Transform target, string id)
    {
        if (target == null || SaveSystem.IsLoading) return;
        var marker = target.GetComponent<MapPoi>();
        if (marker == null) return;
        // Explicit tags keep observation clips attached to the scenery they describe.
        string cue = marker.inspectionId switch
        {
            "chapter1.great_cedar" => "twv3_e05_01",
            "chapter1.rune_stones" => "twv3_e05_02",
            "chapter1.forest_bench" => "twv3_e05_03",
            "chapter1.hidden_bench" => "twv3_e05_04",
            "chapter1.swing" => "twv3_e05_05",
            "chapter1.gunnar_sign" => "twv3_e05_07",
            "chapter1.river_trail" => "twv3_e06_01",
            "chapter1.pond" => "twv3_e06_02",
            "chapter1.stump_tracks" => "twv3_e06_03",
            "chapter1.copper_tag" => "twv3_e06_08",
            "chapter1.viewpoint" => "twv3_amb_05",
            _ => null
        };
        if (cue == null) return;
        if (marker.inspectionId == "chapter1.copper_tag") DialogueSeen.Mark("chapter1/lore/copper_tag");
        ChapterOneVoice.Queue(cue, ChapterOneVoice.Priority.Lesson, .5f, 8f,
            () => target != null && IsNear(target, 12f));
    }

    internal string ChapterJournalPage()
    {
        var page = new System.Text.StringBuilder();
        string current = UIManager.Instance != null ? UIManager.Instance.CurrentObjectiveTitle : "";
        if (!string.IsNullOrEmpty(current))
            page.Append("<b>").Append(ChapterOneText.Pick("Текущая цель", "Current objective")).Append("</b>\n")
                .Append(current).Append("\n\n");

        void Note(string key) => page.Append(ChapterOneText.Text("journal." + key)).Append("\n\n");

        if (IntroFinished() || IntroFlow.Instance != null && IntroFlow.Instance.Current >= IntroFlow.Step.Chop) Note("s01");
        if (MapChapterCompleted("supply")) Note("s03");
        if (MapChapterCompleted("hut")) Note("s06");
        if (MapChapterCompleted("market")) Note("s07");
        if (MapChapterCompleted("tent")) Note("s10");
        if (PetManager.Instance != null && PetManager.Instance.FreeCrateUsed) Note("s11");
        if (PlayerInteraction.CurrentAxes != null && PlayerInteraction.CurrentAxes.ForgeRestored) Note("s12");
        if (DialogueSeen.Has(BladeKnown)) Note("s14");
        if (StoryInspection.KnowsChapterClue(StoryInspection.Kind.Knife)) Note("e01");
        if (StoryInspection.KnowsChapterClue(StoryInspection.Kind.Ribbon)) Note("e02");
        if (StoryInspection.KnowsChapterClue(StoryInspection.Kind.Witness)) Note("e03");
        if (DialogueSeen.Has(StoryInspection.AllClues))
        {
            page.Append("<b>").Append(ChapterOneText.Text("journal.e04.title")).Append("</b>\n");
            Note("e04.body");
            if (DialogueSeen.Has(ArchKnown)) Note("e04.arch");
        }
        if (MapChapterCompleted("bridge")) Note("s15");
        if (DialogueSeen.Has("chapter1/lore/copper_tag")) Note("copper_tag");
        if (noteRead) Note("s16");

        if (page.Length == 0) page.Append(ChapterOneText.Pick("Записи появятся по ходу главы.", "Entries appear as the chapter progresses."));
        return page.ToString().TrimEnd();
    }

    string ArchVoice() => DialogueSeen.Has(BladeKnown) ? "twv3_s16_01a" : "twv3_s16_01b";

    internal bool CanChapterChat(string key)
    {
        if (!IntroFinished() || DialogueManager.IsBusy || DialogueSequence.AnyPlaying || StoryInspection.Active ||
            BuildingController.AnyRevealing || SaveSystem.IsLoading || DialogueSeen.Has("chapter1/chat/" + key)) return false;

        switch (key)
        {
            case "h01":
                return workshop != null && (workshop.totalCollected > 0 || workshop.readyPlanks > 0) &&
                    AtSpeaker(miraName, IntroFlow.Instance != null ? IntroFlow.Instance.campfirePoint : null);
            case "h02": return MapChapterCompleted("hut") && !MapChapterCompleted("bridge") &&
                (AtSpeaker(oskarName, hut != null ? hut.transform : null) || AtSpeaker(oskarName, GunnarAxeTarget()));
            case "h03": return firstSale && AtSpeaker(merchantName, market != null ? market.transform : null);
            case "s05": return Level() == GameBalance.UpgradeLevel && !MapChapterCompleted("hut") &&
                AtSpeaker(miraName, IntroFlow.Instance != null ? IntroFlow.Instance.campfirePoint : null);
            case "s10": return tent != null && tent.IsFinished && AtSpeaker(oskarName, tent.transform);
            case "s07": return CurrentChapterId == "planks" && workshop != null && workshop.totalCollected == 0 &&
                AtSpeaker(oskarName, workshop.transform);
            case "s12": return CurrentChapterId == GunnarAxeChapter && AtSpeaker(oskarName, GunnarAxeTarget());
            case "s13": return PlayerInteraction.CurrentAxes != null && PlayerInteraction.CurrentAxes.ForgeRestored &&
                AtSpeaker(oskarName, GunnarAxeTarget());
            case "s14": return PlayerInteraction.CurrentAxes != null && PlayerInteraction.CurrentAxes.ForgeRestored &&
                (AtSpeaker(oskarName, GunnarAxeTarget()) || AtSpeaker(oskarName, hut != null ? hut.transform : null));
            case "crates":
                if (!PetsOpen || !AtSpeaker(oskarName, Speaker.Find(oskarName)?.transform)) return false;
                foreach (var crate in FindObjectsByType<PetCrateSite>(FindObjectsSortMode.None))
                    if (crate.crate == CrateType.Sky && crate.IsPondSite()) return true;
                return false;
            default: return false;
        }
    }

    internal void PlayChapterChat(string key)
    {
        if (!CanChapterChat(key)) return;
        var npc = Speaker.Find(key == "h01" || key == "s05" ? miraName : key == "h03" ? merchantName : oskarName);
        if (npc == null) return;
        DialogueSeen.Mark("chapter1/chat/" + key);
        void Talk(params string[] ids) => ChapterOneVoice.SpeakSequence(null,
            () => npc != null && npc.isActiveAndEnabled && IsNear(npc.transform, 7f), ids);
        switch (key)
        {
            case "h01":
                Talk("twv3_h01_01", "twv3_h01_02", "twv3_h01_03", "twv3_h01_04"); break;
            case "h02":
                Talk("twv3_h02_01", "twv3_h02_02", "twv3_h02_03", "twv3_h02_04"); break;
            case "h03":
                Talk("twv3_h03_01", "twv3_h03_02", "twv3_h03_03", "twv3_h03_04"); break;
            case "s05":
                Talk("twv3_s05_03", "twv3_s05_04", "twv3_s05_05"); break;
            case "s10":
                Talk("twv3_s10_02"); break;
            case "s07":
                Talk("twv3_s07_oskar_01", "twv3_s07_oskar_02"); break;
            case "s12":
                Talk("twv3_s12_01", "twv3_s12_02", "twv3_s12_03"); break;
            case "s13":
                Talk("twv3_s13_01"); break;
            case "s14":
                DialogueSeen.Mark(BladeKnown);
                Talk("twv3_s14_01", "twv3_s14_02", "twv3_s14_03",
                    StoryInspection.KnowsAnyChapterClue ? "twv3_s14_04a" : "twv3_s14_04b",
                    "twv3_s14_05", "twv3_s14_06"); break;
            case "crates":
                Talk("twv3_s11_06"); break;
        }
    }
}
