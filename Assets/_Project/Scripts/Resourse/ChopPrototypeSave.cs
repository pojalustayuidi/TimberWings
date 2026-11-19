using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// The tree, its outstanding wood, and the inventory are saved in the same save file.
internal sealed class ChopPrototypeSave : ISaveable
{
    internal const string Key = "felledTrees";

    [Serializable]
    internal struct Layout
    {
        public Vector3 center;
        public Quaternion rotation;
        public float length, radius;
        public Vector3 fallPivot;
        public float fallHeight, crownRadius;
    }

    [Serializable]
    internal sealed class Tree
    {
        public string id;
        public Layout layout;
        public float[] cuts;
        public int[] wood;
        public int xp, planks, summerStrikes;
        public AxeRuneId rewardRune;
        public bool golden, silver, layoutDiscount;
        public int essence;
        public bool anchored;
        public Vector3 anchorPosition;
        public Quaternion anchorRotation;
        public float respawnSeconds, grown, regrowthAge;

        public bool HasWood
        {
            get
            {
                foreach (int count in wood)
                    if (count > 0) return true;
                return false;
            }
        }

        internal bool HasHarvest => HasWood || planks > 0;

        internal bool HasSectionHarvest(int section) => wood[section] > 0 || section == wood.Length - 1 && planks > 0;
        internal int PlanksIn(int section) => section == wood.Length - 1 && AllCutsOpen ? planks : 0;

        internal bool LastPickup(int section)
        {
            if (!AllCutsOpen || PlanksIn(section) < planks) return false;
            for (int i = 0; i < wood.Length; i++)
                if (i != section && wood[i] > 0) return false;
            return true;
        }

        public bool AllCutsOpen
        {
            get
            {
                foreach (float cut in cuts)
                    if (cut < 1f) return false;
                return true;
            }
        }

        internal bool SectionFree(int section) => section >= 0 && section < wood.Length &&
            (section == 0 || cuts[section - 1] >= 1f) &&
            (section == cuts.Length || cuts[section] >= 1f);

        internal static Tree Create(string id, Layout layout, int cutCount, int wood, int xp)
        {
            var tree = new Tree
            {
                id = id, layout = layout, cuts = new float[cutCount], wood = new int[cutCount + 1], xp = xp
            };
            int perSection = wood / tree.wood.Length;
            int remainder = wood % tree.wood.Length;
            for (int i = 0; i < tree.wood.Length; i++)
                tree.wood[i] = perSection + (i < remainder ? 1 : 0);
            return tree;
        }

        internal bool AdvanceCut(int cut, float damage)
        {
            if (cut < 0 || cut >= cuts.Length || cuts[cut] >= 1f || !(damage > 0f)) return false;
            cuts[cut] = Chop03Balance.Advance(cuts[cut], damage);
            return true;
        }

        internal int TakeHarvest(int section, int logs, int boards, out int gainedEssence)
        {
            gainedEssence = 0;
            if (!SectionFree(section) || logs < 0 || boards < 0 || logs + boards == 0 ||
                logs > wood[section] || boards > PlanksIn(section))
                return 0;
            wood[section] -= logs;
            planks -= boards;
            int award = xp;
            xp = 0;
            if (essence > 0 && !HasHarvest)
            {
                gainedEssence = essence;
                essence = 0;
            }
            return award;
        }

        internal void MoveTo(Vector3 position, Quaternion rotation)
        {
            if (position == anchorPosition && rotation == anchorRotation) return;
            Quaternion delta = rotation * Quaternion.Inverse(anchorRotation);
            layout.center = position + delta * (layout.center - anchorPosition);
            layout.fallPivot = position + delta * (layout.fallPivot - anchorPosition);
            layout.rotation = delta * layout.rotation;
            anchorPosition = position;
            anchorRotation = rotation;
        }

        internal bool Valid()
        {
            if (string.IsNullOrEmpty(id) || cuts == null || cuts.Length > 2 || wood == null ||
                wood.Length != cuts.Length + 1 || xp < 0 || planks < 0 || planks > 2 || !AxeRunes.IsValid(rewardRune) ||
                summerStrikes < 0 || summerStrikes >= AxeRunes.SparkInterval ||
                essence < 0 || essence > SpecialHarvestTree.RuneEssence || !(respawnSeconds > 0f) ||
                !(layout.radius > 0f) || !(layout.length > 0f) || !(grown >= 0f && grown < 1f) ||
                !(regrowthAge >= 0f)) return false;
            foreach (float cut in cuts)
                if (!(cut >= 0f && cut <= 1f)) return false;
            foreach (int count in wood)
                if (count < 0) return false;
            return true;
        }
    }

    [Serializable]
    internal sealed class Standing
    {
        public string id;
        public Vector3 localPoint, localOutward;
        public float radius, damage;
        public int summerStrikes;

        internal bool Valid() => !string.IsNullOrEmpty(id) && damage > 0f && damage < 1f &&
            summerStrikes >= 0 && summerStrikes < AxeRunes.SparkInterval &&
            radius > .02f && radius <= 3f && localPoint.sqrMagnitude < 400f &&
            localOutward.sqrMagnitude > .90f && localOutward.sqrMagnitude < 1.10f &&
            Mathf.Abs(localOutward.y) < .01f;
    }

    [Serializable]
    sealed class Data
    {
        public string goldenStanding;
        public float goldenCooldown;
        public int goldenSlot;
        public List<Tree> trees = new List<Tree>();
        public List<Standing> standing = new List<Standing>();
    }

    static ChopPrototypeSave instance;
    string goldenStanding;
    float goldenCooldown;

    internal static string GoldenStanding => instance?.goldenStanding;
    internal static float GoldenCooldown => instance != null ? instance.goldenCooldown : 0f;

    internal static void SetGolden(string standingId, float cooldown)
    {
        Boot();
        bool changed = instance.goldenStanding != standingId;
        instance.goldenStanding = standingId;
        instance.goldenCooldown = Mathf.Max(0f, cooldown);
        if (changed) SaveSystem.RequestSave(Key);
    }

    int goldenSlot;

    internal static int GoldenSlot => instance?.goldenSlot ?? 0;

    internal static void SetGoldenSlot(int slot)
    {
        Boot();
        instance.goldenSlot = slot;
        SaveSystem.RequestSave(Key);
    }
    readonly Dictionary<string, Tree> trees = new Dictionary<string, Tree>();
    readonly Dictionary<string, Standing> standing = new Dictionary<string, Standing>();
    readonly HashSet<ResourceItem> sources = new HashSet<ResourceItem>();

    public string SaveKey => Key;
    public int LoadOrder => 110;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new ChopPrototypeSave();
        SaveSystem.Register(instance);
    }

    internal static string TreeId(ResourceItem item)
    {
        var scene = item.gameObject.scene;
        var at = item.transform.position;
        // Take the authored identity before TreeSizing applies its deterministic jitter.
        string hierarchy = string.Empty;
        for (Transform part = item.transform; part != null; part = part.parent)
        {
            int ordinal = 0;
            Transform parent = part.parent;
            if (parent != null)
                for (int i = 0; i < part.GetSiblingIndex(); i++)
                    if (parent.GetChild(i).name == part.name) ordinal++;
            hierarchy = "/" + part.name + "[" + ordinal.ToString(CultureInfo.InvariantCulture) + "]" + hierarchy;
        }
        return string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2},{3},{4}",
            string.IsNullOrEmpty(scene.path) ? scene.name : scene.path, hierarchy,
            Mathf.RoundToInt(at.x * 1000f), Mathf.RoundToInt(at.y * 1000f), Mathf.RoundToInt(at.z * 1000f));
    }

    internal static void Attach(ResourceItem item)
    {
        Boot();
        if (!instance.sources.Add(item)) return;
        instance.RestoreSource(item);
    }

    internal static void Detach(ResourceItem item) { instance?.sources.Remove(item); }

    internal static void Record(Tree tree)
    {
        Boot();
        instance.trees.Add(tree.id, tree);
        instance.standing.Remove(tree.id);
        SaveSystem.RequestSave(Key);
    }

    internal static void RecordStanding(Standing damage)
    {
        Boot();
        instance.standing[damage.id] = damage;
        SaveSystem.RequestSave(Key);
    }

    internal static void RemoveStanding(string id)
    {
        if (instance != null && !string.IsNullOrEmpty(id) && instance.standing.Remove(id))
            SaveSystem.RequestSave(Key);
    }

    internal static bool HasUncollectedGolden()
    {
        if (instance == null) return false;
        foreach (var tree in instance.trees.Values)
            if (tree.golden && tree.HasWood) return true;
        return false;
    }

    internal static void Finish(Tree tree)
    {
        if (instance == null || tree == null ||
            !instance.trees.TryGetValue(tree.id, out var current) || current != tree) return;
        instance.trees.Remove(tree.id);
        SaveSystem.RequestSave(Key);
    }

    public string Save() => JsonUtility.ToJson(new Data
    {
        goldenStanding = goldenStanding, goldenCooldown = goldenCooldown,
        goldenSlot = goldenSlot,
        trees = new List<Tree>(trees.Values),
        standing = new List<Standing>(standing.Values)
    });

    public void Load(string json, int formatVersion)
    {
        var data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Data>(json);
        goldenStanding = data?.goldenStanding;
        goldenCooldown = Mathf.Clamp(data?.goldenCooldown ?? 0f, 0f, GameBalance.SkillGoldenInterval);
        goldenSlot = Mathf.Max(0, data?.goldenSlot ?? 0);
        trees.Clear();
        standing.Clear();
        if (data?.trees != null)
            foreach (var tree in data.trees)
                if (tree != null && tree.Valid()) trees[tree.id] = tree;
        if (data?.standing != null)
            foreach (var notch in data.standing)
                if (notch != null && notch.Valid() && !trees.ContainsKey(notch.id)) standing[notch.id] = notch;
        RestoreSources();
    }

    public void ResetToDefault()
    {
        goldenStanding = null;
        goldenCooldown = 0f;
        goldenSlot = 0;
        trees.Clear();
        standing.Clear();
        RestoreSources();
    }

    void RestoreSources()
    {
        foreach (var source in new List<ResourceItem>(sources))
        {
            if (source != null) RestoreSource(source);
        }
    }

    void RestoreSource(ResourceItem source)
    {
        trees.TryGetValue(source.ChopSaveId, out var tree);
        source.RestoreChopState(tree);
        standing.TryGetValue(source.ChopSaveId, out var notch);
        source.RestoreStandingNotch(notch);
    }
}
