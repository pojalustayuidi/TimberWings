using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The CHOP-01 fallen trunk, now with individually cut joints and collectible sections.
public sealed partial class ChopPrototypeLog : MonoBehaviour
{
    internal static readonly List<ChopPrototypeLog> Active = new List<ChopPrototypeLog>();
    static bool? modeOverride;
    internal static bool Enabled => modeOverride ?? OrbitCamera.FirstPersonActive;
    // Existing fallen trunks retain the processing flow even if F6 switches camera modes.
    internal static bool UsesProcessing => Enabled || Active.Count > 0;
    internal bool Ready { get; private set; }
    internal bool HasWood => state.HasWood;
    internal bool HasHarvest => state.HasHarvest;
    internal int CutCount => state.cuts.Length;
    internal int SectionCount => state.wood.Length;
    internal ChopPrototypeSave.Layout Layout => state.layout;

    ChopPrototypeSave.Tree state;
    float collectAt;
    float[] separation;
    int focusCut = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active.Clear();
        modeOverride = null;
        if (cutMarkMesh != null) Destroy(cutMarkMesh);
        cutMarkMesh = null;
    }

    internal static void Toggle()
    {
        modeOverride = !Enabled;
        SaveIndicator.ShowToast(Enabled
            ? Text("CHOP-03 · зелёная зона", "CHOP-03 · green zone")
            : Text("Обычная рубка · лежащие стволы доступны", "Regular chopping · felled logs remain"));
    }

    internal static string Text(string russian, string english) => GameLanguage.English ? english : russian;

    internal static bool Supports(ResourceItem tree) =>
        tree != null && (Enabled || tree.SpecialHarvest != null) &&
        tree.respawn && !string.IsNullOrEmpty(tree.ChopSaveId);

    internal static int CutsFor(TreeSize size) => size == TreeSize.Old ? 2 : size == TreeSize.Mature ? 1 : 0;

    internal static ChopPrototypeLog Create(ResourceItem source, ChopPrototypeSave.Tree tree, bool falling)
    {
        var go = new GameObject("Fallen trunk");
        go.SetActive(false);
        SceneManager.MoveGameObjectToScene(go, source.gameObject.scene);
        go.transform.SetPositionAndRotation(tree.layout.center, tree.layout.rotation);
        var log = go.AddComponent<ChopPrototypeLog>();
        log.state = tree;
        log.separation = new float[tree.cuts.Length];
        for (int i = 0; i < tree.cuts.Length; i++)
            log.separation[i] = tree.cuts[i] >= 1f ? 1f : 0f;

        log.BuildSections(source.SpecialHarvest != null ? source.SpecialHarvest.sectionPrefab : null);
        log.UpdateSections();
        if (!falling) log.Land(false);
        go.SetActive(true);
        return log;
    }

    void OnEnable() { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    internal void Land(bool effects)
    {
        if (Ready) return;
        Ready = true;
        collectAt = effects ? Time.time + .44f : Time.time;
        UpdateSections();
        if (effects)
        {
            SfxManager.Play("trunkLand", .68f, 1f);
            ImpactFx.PlayLogDebris(transform.position - Vector3.up * state.layout.radius * .7f, true);
        }
    }

    void Update()
    {
        if (!Ready) return;
        bool moved = false;
        for (int i = 0; i < separation.Length; i++)
        {
            if (state.cuts[i] < 1f || separation[i] >= 1f) continue;
            separation[i] = Mathf.MoveTowards(separation[i], 1f, Time.deltaTime / .27f);
            moved = true;
        }
        if (moved) UpdateSections();
    }

    internal bool CanCut(int cut) => Ready && cut >= 0 && cut < CutCount && state.cuts[cut] < 1f;
    internal bool CanCollect(int section) => Ready && Time.time >= collectAt && state.SectionFree(section) &&
        state.HasSectionHarvest(section) && (section == 0 || separation[section - 1] >= 1f) &&
        (section == CutCount || separation[section] >= 1f);
    internal float CutProgress(int cut) => state.cuts[cut];
    internal int WoodIn(int section) => state.wood[section];
    internal int BoardsIn(int section) => state.PlanksIn(section);
    internal Transform CutAnchor(int cut) => cutAnchors[cut];

    internal void FocusCut(int cut)
    {
        if (focusCut == cut) return;
        int old = focusCut;
        focusCut = CanCut(cut) ? cut : -1;
        if (old >= 0) StyleCut(old);
        if (focusCut >= 0) StyleCut(focusCut);
    }

    internal int RequiredCutHits(int cut) =>
        cut == 1 && state.layoutDiscount ? 6 : Chop03Balance.HitsPerCut;

    internal bool Hit(int cut, float damage, bool precise)
    {
        if (!Ready || !state.AdvanceCut(cut, damage)) return false;
        RebuildSection(cut);
        RebuildSection(cut + 1);
        UpdateCutAnchor(cut);
        LumberSkills.TouchProcessing(state);
        SaveSystem.RequestSave(ChopPrototypeSave.Key);
        if (CanCut(cut)) return false;

        if (precise && cut == 0 && CutCount == 2 && PlayerSkills.Has(SkillIds.CutLayout) && !state.layoutDiscount)
        {
            state.layoutDiscount = true;
            state.cuts[1] = Chop03Balance.Advance(0, state.cuts[1] * (8f / 6f));
            RebuildSection(1);
            RebuildSection(2);
            UpdateCutAnchor(1);
            StyleCut(1);
        }
        Vector3 point = CutAnchor(cut).position;
        if (focusCut == cut) FocusCut(-1);
        StyleCut(cut);
        ImpactFx.PlayLogDebris(point, true);
        SfxManager.Play("woodCrack", .64f, 1.12f);
        return true;
    }

    internal Vector3 Closest(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        local.x = local.y = 0f;
        local.z = Mathf.Clamp(local.z, -state.layout.length * .5f, state.layout.length * .5f);
        return transform.TransformPoint(local);
    }

    internal Vector3 SectionPoint(int section, Vector3 point)
    {
        var root = sections[section].root.transform;
        Vector3 local = root.InverseTransformPoint(point);
        float margin = Mathf.Min(.045f, sections[section].length * .15f);
        return root.TransformPoint(new Vector3(0f, state.layout.radius * .72f,
            Mathf.Clamp(local.z, -sections[section].length * .5f + margin, sections[section].length * .5f - margin)));
    }

    internal int FindSection(Collider collider)
    {
        for (int i = 0; i < sections.Length; i++)
            if (sections[i].solid == collider) return i;
        return -1;
    }

    internal int FindCut(Vector3 point)
    {
        int best = -1;
        float distance = Mathf.Clamp(.18f + state.layout.radius * .44f, .20f, .39f);
        float z = transform.InverseTransformPoint(point).z;
        for (int i = 0; i < CutCount; i++)
        {
            if (!CanCut(i)) continue;
            float delta = Mathf.Abs(z - cutAnchors[i].localPosition.z);
            if (delta > distance) continue;
            distance = delta;
            best = i;
        }
        return best;
    }

    internal int Collect(int section, PlayerInventory inventory, PlayerStats stats, Vector3 point)
    {
        if (!CanCollect(section) || inventory == null || state.xp > 0 && stats == null) return 0;

        int logsTaken = 0, boardsTaken = 0, essenceTaken = 0;
        bool completed = false;
        AxeRuneId rune = state.rewardRune;
        bool careful = rune == AxeRuneId.CarefulCollect;
        int start = careful ? 0 : section;
        int end = careful ? SectionCount : section + 1;
        for (int i = start; i < end; i++)
        {
            if (!CanCollect(i)) continue;
            int index = i;
            var goods = inventory.ReceiveProduction(state.wood[index], state.PlanksIn(index), (logs, boards) =>
            {
                // Consume the same amounts the wallet accepted before any observer sees a change.
                int xp = state.TakeHarvest(index, logs, boards, out int essence);
                if (essence > 0)
                {
                    inventory.ReceiveRuneEssence(essence);
                    essenceTaken += essence;
                }
                if (xp > 0 && stats != null) stats.AddXP(xp);
                LumberSkills.TouchProcessing(state);
                completed = !state.HasHarvest;
                SaveSystem.RequestSave(ChopPrototypeSave.Key);
            });
            logsTaken += goods.logs;
            boardsTaken += goods.planks;
            if (!careful) break;
        }
        if (logsTaken + boardsTaken <= 0) return 0;

        // Cycle bonuses happen once, only when the complete trunk has been claimed.
        var axes = PlayerInteraction.CurrentAxes;
        if (completed && axes != null && axes.CountRuneHarvest(rune))
        {
            switch (rune)
            {
                case AxeRuneId.SturdyWood:
                    logsTaken += inventory.AddWood(1);
                    break;
                case AxeRuneId.ForestSpark:
                    inventory.ReceiveRuneEssence(1);
                    essenceTaken++;
                    break;
                case AxeRuneId.CleanStock:
                    boardsTaken += inventory.ReceiveProduction(0, 1, null).planks;
                    break;
            }
        }

        if (essenceTaken > 0)
            SaveIndicator.ShowToast(Text("Рунная эссенция", "Rune essence") + $" +{essenceTaken} · {inventory.RuneEssence}");
        if (boardsTaken > 0)
            SaveIndicator.ShowToast(Text("Доски", "Boards") + $" +{boardsTaken}");

        if (!HasHarvest) Discard();
        else UpdateSections();
        if (logsTaken > 0) ChopRewards.Give(point, logsTaken, 0); // cosmetic flight: items are already committed.
        return logsTaken + boardsTaken;
    }

    internal void Discard()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
