using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The CHOP-01 fallen trunk, now with individually cut joints and collectible sections.
public sealed partial class ChopPrototypeLog : MonoBehaviour
{
    internal static readonly List<ChopPrototypeLog> Active = new List<ChopPrototypeLog>();
    internal static bool Enabled { get; private set; }
    internal bool Ready { get; private set; }
    internal bool HasWood => state.HasWood;
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
        Enabled = false;
        if (cutMarkMesh != null) Destroy(cutMarkMesh);
        cutMarkMesh = null;
    }

    internal static void Toggle()
    {
        Enabled = !Enabled;
        SaveIndicator.ShowToast(Enabled
            ? Text("CHOP-03 · зелёная зона", "CHOP-03 · green zone")
            : Text("Обычная рубка · лежащие стволы доступны", "Regular chopping · felled logs remain"));
    }

    internal static string Text(string russian, string english) => GameLanguage.English ? english : russian;

    internal static bool Supports(ResourceItem tree) =>
        Enabled && tree != null && tree.respawn && !string.IsNullOrEmpty(tree.ChopSaveId);

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

        log.BuildSections();
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
        state.wood[section] > 0 && (section == 0 || separation[section - 1] >= 1f) &&
        (section == CutCount || separation[section] >= 1f);
    internal float CutProgress(int cut) => state.cuts[cut];
    internal int WoodIn(int section) => state.wood[section];
    internal Transform CutAnchor(int cut) => cutAnchors[cut];

    internal void FocusCut(int cut)
    {
        if (focusCut == cut) return;
        int old = focusCut;
        focusCut = CanCut(cut) ? cut : -1;
        if (old >= 0) StyleCut(old);
        if (focusCut >= 0) StyleCut(focusCut);
    }

    internal bool Hit(int cut, float damage)
    {
        if (!Ready || !state.AdvanceCut(cut, damage)) return false;
        RebuildSection(cut);
        RebuildSection(cut + 1);
        UpdateCutAnchor(cut);
        LumberSkills.TouchProcessing(state);
        SaveSystem.RequestSave(ChopPrototypeSave.Key);
        if (CanCut(cut)) return false;

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

        int taken = inventory.AddProcessedWood(state.wood[section], count =>
        {
            // Store the remainder before notifying inventory or level subscribers.
            int xp = state.TakeWood(section, count);
            LumberSkills.TouchProcessing(state);
            if (xp > 0) stats.AddXP(xp);
            SaveSystem.RequestSave(ChopPrototypeSave.Key);
        });
        if (taken <= 0) return 0;

        if (!HasWood) Discard();
        else UpdateSections();
        ChopRewards.Give(point, taken, 0); // HUD flight is cosmetic, the transfer is already committed.
        return taken;
    }

    internal void Discard()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
