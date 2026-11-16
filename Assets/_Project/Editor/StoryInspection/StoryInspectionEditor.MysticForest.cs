using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Kind = StoryInspection.Kind;
using Object = UnityEngine.Object;

static partial class StoryInspectionEditor
{
    const string ForestBakeMenu = Menu + "Bake Gunnar Knife and Ribbon";
    const string ForestUndoLabel = "Bake Gunnar knife and torn ribbon";
    const string ForestArtFolder = "Assets/_Project/Art/StoryInspection/MysticForestPolish/";
    const string RibbonRootName = "TW_Ribbon_Story_V2";

    [MenuItem(ForestBakeMenu, false, 13)]
    static void BakeMysticForestFinds()
    {
        if (!TryScene(out var scene)) return;

        if (!EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == scene.path))
        {
            Debug.LogWarning("Story Inspection: открой игровую сцену из Build Settings. Обычно это Assets/_Recovery/0 (8).unity.");
            return;
        }

        int undoGroup = -1;
        try
        {
            var bindings = FindCandidates(scene).Where(b => b.kind == Kind.Knife || b.kind == Kind.Ribbon).ToList();
            var knife = FindForestBakeTarget(bindings, Kind.Knife);
            var ribbon = FindForestBakeTarget(bindings, Kind.Ribbon);

            Material woodAndStone = ForestAsset<Material>("Forest_Find_Wood_Stone", ".mat");
            Material metal = ForestAsset<Material>("Gunnar_Worn_Metal", ".mat");
            Material cloth = ForestAsset<Material>("Ribbon_Dusty_Mauve", ".mat");

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(ForestUndoLabel);

            BakeGunnarKnife(knife, bindings, woodAndStone, metal);
            BakeTornRibbon(ribbon, woodAndStone, cloth);
            foreach (var other in bindings)
            {
                if (other == knife || other == ribbon) continue;
                BindForestInspection(other, active: false);
                foreach (var renderer in other.source.GetComponentsInChildren<Renderer>(true))
                    HideForestRenderer(renderer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Story Inspection: нож Гуннара и оборванная лента запечены в " + scene.path + ". Сохрани сцену (Ctrl+S).");
        }
        catch (Exception error)
        {
            if (undoGroup >= 0)
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(undoGroup);
            }
            Debug.LogError("Story Inspection: не удалось запечь находки. " + error);
        }
    }

    static Binding FindForestBakeTarget(List<Binding> bindings, Kind kind)
    {
        var available = bindings.Where(b => b.kind == kind && b.Available).ToArray();
        var authored = available.Where(b => b.authored).ToArray();
        if (authored.Length != 0) available = authored;

        var installed = available.Where(b => b.source.TryGetComponent<StoryInspection>(out var inspection) &&
            inspection.enabled && !inspection.superseded && inspection.source == b.source).ToArray();
        if (installed.Length != 0) available = installed;

        if (available.Length == 1) return available[0];
        string name = kind == Kind.Knife ? "Gunnar_Memory" : "Ribbon_Memory";
        throw new InvalidOperationException(available.Length == 0
            ? $"В открытой сцене нет активной находки {name} с DialogueSequence."
            : $"В открытой сцене несколько равнозначных {name}. Оставь одну активную оригинальную находку и повтори запекание.");
    }

    static void BakeGunnarKnife(Binding binding, List<Binding> bindings, Material woodAndStone, Material metal)
    {
        Transform owner = binding.source.transform;
        bool firstBake = owner.Find(StoryInspection.GunnarKnifeModelName) == null;
        var inspection = BindForestInspection(binding, active: true);
        Transform copiedBridge = inspection.copiedBridge;
        Transform bridge = inspection.bridgeEngraving;
        if (bridge == null)
            bridge = SelectDetail(binding, bindings, null, ref copiedBridge, b => b.bridge);

        // Only move the stock scene placement. Custom positions and every
        // already-baked hierarchy keep their existing transforms on repeat.
        if (firstBake && binding.authored &&
            (owner.localPosition - new Vector3(3.9723f, 0f, 2.51213f)).sqrMagnitude < .02f * .02f)
        {
            bool moveUnderKnife = bridge != null && bridge != owner && bridge.IsChildOf(owner);
            Vector3 bridgePosition = moveUnderKnife ? bridge.position : Vector3.zero;
            Undo.RecordObject(owner, ForestUndoLabel);
            owner.localPosition = new Vector3(3.04f, .014f, 2.73f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            if (moveUnderKnife)
            {
                Undo.RecordObject(bridge, ForestUndoLabel);
                bridge.position = bridgePosition;
                PrefabUtility.RecordPrefabInstancePropertyModifications(bridge);
            }
        }

        Transform knife = GetForestNode(owner, StoryInspection.GunnarKnifeModelName,
            new Vector3(-.05f, .351f, .047f), Quaternion.Euler(-90f, -76f, 0f), Vector3.one * .85f);
        AssignForestMesh(owner, "Gunnar_Moss_Stone", woodAndStone);
        AssignForestMesh(knife, "Gunnar_Blade", metal);
        AssignForestMesh(knife, "Gunnar_Bronze", metal);
        AssignForestMesh(knife, "Gunnar_Walnut", woodAndStone);
        AssignForestMesh(knife, "Gunnar_Leather_Wrap", woodAndStone);
        AssignForestMesh(knife, "Gunnar_Leaf_Charm", metal);
        Transform engraving = GetForestNode(knife, StoryInspection.GunnarEngravingFocusName, new Vector3(0f, -.121f, .03f));

        Undo.RecordObject(inspection, ForestUndoLabel);
        inspection.detail = engraving;
        inspection.bridgeEngraving = bridge;
        inspection.copiedBridge = copiedBridge;
        PrefabUtility.RecordPrefabInstancePropertyModifications(inspection);
        HideForestRenderer(owner.GetComponent<Renderer>());
        if (firstBake) UpdateForestPrompt((DialogueSequence)binding.source, new Vector3(0f, .72f, .036f));
        if (bridge == null)
            Debug.LogWarning("Story Inspection: в сцене не найдена исходная гравировка Bridge_Engraving.", owner);
    }

    static void BakeTornRibbon(Binding binding, Material woodAndStone, Material cloth)
    {
        Transform owner = binding.source.transform;
        Transform old = owner.Find(RibbonRootName);
        bool alreadyBaked = old != null && old.Find(TWRibbonStoryModel.BakedClothName) != null;
        if (!alreadyBaked) RetireRibbonVisual(old);
        RetireRibbonVisual(owner.Find("TW_Ribbon_Story_V4"));

        Transform ribbon = GetForestNode(owner, RibbonRootName, new Vector3(0f, .014f, 0f));
        AssignForestMesh(ribbon, "Ribbon_Stone_and_Twig", woodAndStone);
        Transform clothNode = AssignForestMesh(ribbon, "Ribbon_Faded_Cloth", cloth, TWRibbonStoryModel.BakedClothName);
        Transform tallies = GetForestNode(ribbon, "Ribbon_Tally_Focus", new Vector3(.103f, .308f, .124f));

        var inspection = BindForestInspection(binding, active: true);
        Undo.RecordObject(inspection, ForestUndoLabel);
        inspection.detail = tallies;
        PrefabUtility.RecordPrefabInstancePropertyModifications(inspection);

        if (!owner.TryGetComponent<TWRibbonStoryModel>(out var controller))
            controller = Undo.AddComponent<TWRibbonStoryModel>(owner.gameObject);
        var original = owner.GetComponent<Renderer>();
        Undo.RecordObject(controller, ForestUndoLabel);
        if (original != null) controller.CaptureOriginals(new[] { original });
        controller.Configure(null, clothNode);
        controller.enabled = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        HideForestRenderer(original);

        if (owner.TryGetComponent<TWRibbonInspectionCamera>(out var legacyCamera) && legacyCamera.enabled)
        {
            Undo.RecordObject(legacyCamera, ForestUndoLabel);
            legacyCamera.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(legacyCamera);
        }

        if (!alreadyBaked) UpdateForestPrompt((DialogueSequence)binding.source, new Vector3(.09f, .73f, .06f));
    }

    static void RetireRibbonVisual(Transform visual)
    {
        if (visual == null) return;
        var go = visual.gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsAddedGameObjectOverride(go))
        {
            Undo.RecordObject(go, ForestUndoLabel);
            go.name += "_Legacy";
            go.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        }
        else Undo.DestroyObjectImmediate(go);
    }

    static StoryInspection BindForestInspection(Binding binding, bool active)
    {
        var target = binding.source.gameObject;
        var inspection = target.GetComponent<StoryInspection>();
        if (inspection == null)
        {
            inspection = Undo.AddComponent<StoryInspection>(target);
            Undo.RecordObject(inspection, ForestUndoLabel);
            inspection.detailOffset = binding.offset;
        }
        else Undo.RecordObject(inspection, ForestUndoLabel);

        inspection.kind = binding.kind;
        inspection.source = binding.source;
        inspection.superseded = !active;
        inspection.enabled = active;
        PrefabUtility.RecordPrefabInstancePropertyModifications(inspection);
        return inspection;
    }

    static void HideForestRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled) return;
        Undo.RecordObject(renderer, ForestUndoLabel);
        renderer.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
    }

    static void UpdateForestPrompt(DialogueSequence source, Vector3 position)
    {
        // Preserve customized prompt anchors in scenes edited after the first bake.
        if ((source.promptOffset - Vector3.up * 1.3f).sqrMagnitude > .001f) return;
        Undo.RecordObject(source, ForestUndoLabel);
        source.promptOffset = position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(source);
    }

    static Transform GetForestNode(Transform parent, string name, Vector3 position = default,
        Quaternion? rotation = null, Vector3? scale = null)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;
        var child = new GameObject(name) { layer = parent.gameObject.layer };
        // With additive scenes, New GameObject belongs to the active scene
        // before it is parented. The command edits only that active scene.
        Undo.RegisterCreatedObjectUndo(child, ForestUndoLabel);
        Undo.SetTransformParent(child.transform, parent, ForestUndoLabel);
        Undo.RecordObject(child.transform, ForestUndoLabel);
        child.transform.SetLocalPositionAndRotation(position, rotation ?? Quaternion.identity);
        child.transform.localScale = scale ?? Vector3.one;
        return child.transform;
    }

    static Transform AssignForestMesh(Transform parent, string meshName, Material material, string nodeName = null)
    {
        Mesh mesh = ForestAsset<Mesh>(meshName, ".asset");
        var node = GetForestNode(parent, nodeName ?? meshName);
        if (!node.TryGetComponent<MeshFilter>(out var filter))
            filter = Undo.AddComponent<MeshFilter>(node.gameObject);
        if (filter.sharedMesh != mesh)
        {
            Undo.RecordObject(filter, ForestUndoLabel);
            filter.sharedMesh = mesh;
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        }

        if (!node.TryGetComponent<MeshRenderer>(out var renderer))
            renderer = Undo.AddComponent<MeshRenderer>(node.gameObject);
        if (renderer.sharedMaterial != material)
        {
            Undo.RecordObject(renderer, ForestUndoLabel);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        return node;
    }

    static T ForestAsset<T>(string name, string extension) where T : Object
    {
        string path = ForestArtFolder + name + extension;
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Не найден ресурс " + path);
        return asset;
    }
}
