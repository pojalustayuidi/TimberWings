#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using IslandV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Optional, additive visual pass for the EXISTING scene Campfire. Never recreates BuildingController,
/// stages, save IDs, physics or the island. It is deliberately opt-in: run the menu in Edit Mode.
/// All generated geometry has no colliders. Bake is repeatable; Remove only deletes our subtrees.
/// </summary>
public static class TWCampfireCozyPolishBaker
{
    private const string Root = "TW_CozyCampfire_ArtV1";
    private const string ArtFolder = "Assets/_Game/Campfire/TW_CozyPolish";
    private const string Menu = "Tools/TimberWings/Campfire/";

    private static readonly Color Bark = SoftMesh.C(0xB47A4C);
    private static readonly Color Cut = SoftMesh.C(0xFFE0A3);
    private static readonly Color Rope = SoftMesh.C(0xE5C78B);
    private static readonly Color StoneA = SoftMesh.C(0xDCCDB9);
    private static readonly Color StoneB = SoftMesh.C(0xF1DFCB);
    private static readonly Color StoneC = SoftMesh.C(0xC3BCBD);
    private static readonly Color Char = SoftMesh.C(0x59443B);
    private static readonly Color Ember = SoftMesh.C(0xFF793C);
    private static readonly Color Amber = SoftMesh.C(0xFFC65C);
    private static readonly Color Honey = SoftMesh.C(0xFFE9A2);

    [MenuItem(Menu + "1. Apply Cozy Polish to EXISTING campfire", false, 1050)]
    public static void Apply()
    {
        if (!SafeToEdit()) return;
        GameObject camp = FindCampfire();
        if (!ValidateRoot(camp, true)) return;
        var scene = camp.scene;
        // Existing candy/campfire materials already use vertex colours, preserving the game's palette.
        Material baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Campfire/Candy_Camp.mat");
        Material flameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Campfire/Candy_Flame.mat");
        if (baseMat == null || flameMat == null)
        {
            EditorUtility.DisplayDialog("Campfire Cozy Polish", "Нет Candy_Camp.mat или Candy_Flame.mat. Сначала собери оригинальный костёр Candy через BuilderSim, не заменяя стройку вручную.", "OK");
            return;
        }
        EnsureFolder();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("TimberWings - Cozy Campfire Art (visual only)");
        try
        {
            var s1 = camp.transform.Find("Stage1");
            var s2 = camp.transform.Find("Stage2");
            var s3 = camp.transform.Find("Stage3");
            foreach (Transform s in new[] { s1, s2, s3 })
            {
                var prev = s.Find(Root);
                if (prev != null) Undo.DestroyObjectImmediate(prev.gameObject);
            }

            // STAGE 1: warm, recognizable foundation. Existing stone ring and logs remain untouched.
            Transform a = Group(s1);
            SoftMesh pebbles = new SoftMesh();
            const int count = 9;
            for (int i = 0; i < count; i++)
            {
                float angle = (i * 360f / count + ((i % 3) - 1) * 4f + 11f) * Mathf.Deg2Rad;
                float radius = 1.59f + (i % 3) * 0.055f;
                Vector3 center = new Vector3(Mathf.Cos(angle) * radius, 0.06f, Mathf.Sin(angle) * radius);
                pebbles.Puff(center, new Vector3(.09f + (i % 3) * .018f, .055f, .10f),
                    i % 3 == 0 ? StoneA : i % 3 == 1 ? StoneB : StoneC, i * 31f, 7, 4, .12f);
            }
            // A few split kindling pieces behind the center. Never block the player's south-side work spot.
            for (int i = 0; i < 3; i++)
            {
                float x = .68f + i * .18f;
                Vector3 st = new Vector3(x, .10f, 1.63f + (i % 2) * .08f);
                Vector3 en = st + new Vector3(.28f, .015f, -.07f);
                pebbles.Cyl(st, en, .053f, .047f, Bark, Cut, true, true, 7);
            }
            CreateMesh(a, "Pebble_And_Kindling_Details", pebbles, baseMat, "cozy_stage1");

            // STAGE 2: a quiet handcrafted accent to make the crossbar feel intentionally assembled.
            Transform b = Group(s2);
            SoftMesh handmade = new SoftMesh();
            // Tiny hanging wooden emblem on the LEFT end of the existing crossbar; right end has hourglass.
            handmade.Cyl(new Vector3(-.83f, 2.15f, .75f), new Vector3(-.83f, 1.90f, .75f),
                .015f, .015f, Rope, Rope, true, true, 6);
            handmade.Puff(new Vector3(-.83f, 1.83f, .75f), new Vector3(.135f, .155f, .06f),
                Cut, 0f, 8, 5, .12f);
            handmade.Star(new Vector3(-.83f, 1.83f, .682f), Vector3.right, Vector3.up, .085f, .038f, 5, Amber);
            // A little bundle of split wood near the FAR side of the pit (not a collision object).
            for (int i = 0; i < 3; i++)
            {
                Vector3 start = new Vector3(-.22f + i * .11f, .115f + (i == 2 ? .10f : 0f), 1.82f);
                Vector3 end = start + new Vector3(.46f, 0f, -.045f);
                handmade.Cyl(start, end, .068f, .064f, i == 2 ? Cut : Bark, Cut, true, true, 7);
            }
            CreateMesh(b, "Handmade_Emblem_And_Fuel", handmade, baseMat, "cozy_stage2");

            // STAGE 3: visible orange coals through the four main beams + low, breathing embers.
            Transform c = Group(s3);
            SoftMesh coals = new SoftMesh();
            coals.Puff(new Vector3(0f, .15f, 0f), new Vector3(.52f, .045f, .52f), Char, 0f, 11, 4, .08f);
            for (int i = 0; i < 13; i++)
            {
                float t = (i * 137.51f + 27f) * Mathf.Deg2Rad;
                float r = .17f + .31f * ((i % 5) / 4f);
                coals.Puff(new Vector3(Mathf.Cos(t) * r, .19f + (i % 3) * .016f, Mathf.Sin(t) * r),
                    new Vector3(.055f + (i % 3) * .015f, .026f, .048f), i % 3 == 0 ? Honey : i % 2 == 0 ? Amber : Ember,
                    i * 47f, 7, 4, .08f);
            }
            CreateMesh(c, "Coals_Inside_Hearth", coals, baseMat, "cozy_stage3_coals");

            // Small translucent-looking emissive tongues low in the hearth; existing tall fire remains unchanged.
            SoftMesh glows = new SoftMesh();
            for (int i = 0; i < 6; i++)
            {
                float t = (i * 60f + 17f) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(t) * .33f, .19f, Mathf.Sin(t) * .33f);
                glows.Teardrop(p, .16f + (i % 3) * .075f, .07f,
                    new Vector3(Mathf.Cos(t) * .045f, 0f, Mathf.Sin(t) * .045f), Ember, Honey, 7, 4);
            }
            GameObject glowGo = CreateMesh(c, "Warm_Low_Embers", glows, flameMat, "cozy_stage3_glow");

            // Only the NEW ember mesh moves. Existing fire is still driven by CampfireFlicker.
            var breathe = Undo.AddComponent<TWCampfireCozyEmbers>(c.gameObject);
            Undo.RecordObject(breathe, "Assign campfire embers");
            breathe.Assign(glowGo.transform);
            EditorUtility.SetDirty(breathe);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = camp;
            Debug.Log("[Cozy Campfire] Applied to existing Campfire. Added 3 stage-specific art groups; " +
                "NO building, save, stage, physics, camera or lighting code changed. Save the scene (Ctrl+S).", camp);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Cozy Campfire] Bake failed: " + ex);
            // Editor Undo backs out scene changes; stable generated mesh assets can remain safely on disk.
            Undo.RevertAllDownToGroup(group);
            EditorUtility.DisplayDialog("Campfire Cozy Polish", "Ошибка запекания, изменения сцены откатились. См. Console.\n" + ex.Message, "OK");
        }
    }

    [MenuItem(Menu + "2. Preview stage 1 (cold)", false, 1051)]
    private static void Preview1() { Preview(1); }
    [MenuItem(Menu + "3. Preview stage 2 (camp)", false, 1052)]
    private static void Preview2() { Preview(2); }
    [MenuItem(Menu + "4. Preview stage 3 (fire)", false, 1053)]
    private static void Preview3() { Preview(3); }

    private static void Preview(int stage)
    {
        if (!SafeToEdit()) return;
        GameObject root = FindCampfire();
        if (!ValidateRoot(root, true)) return;
        var stages = root.GetComponent<HutStages>();
        Undo.RecordObject(stages, "Cozy campfire stage preview");
        stages.SetStage(stage);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[Cozy Campfire] Preview Stage " + stage + ". For normal scene editing restore Stage 1 before saving; in Play Mode HutStages uses its initialStage.");
    }

    [MenuItem(Menu + "5. Validate stage art and physics", false, 1054)]
    private static void Audit()
    {
        GameObject camp = FindCampfire();
        if (!ValidateRoot(camp, true)) return;
        var stageGroupCount = 0;
        var physical = new List<Component>();
        for (int i = 1; i <= 3; ++i)
        {
            Transform stage = camp.transform.Find("Stage" + i);
            Transform grp = stage.Find(Root);
            if (grp == null) { Debug.LogWarning("[Cozy Campfire] Missing art for stage " + i, camp); continue; }
            stageGroupCount++;
            physical.AddRange(grp.GetComponentsInChildren<Collider>(true));
            physical.AddRange(grp.GetComponentsInChildren<Rigidbody>(true));
            foreach (var mf in grp.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh == null) Debug.LogError("[Cozy Campfire] Missing mesh: " + mf.name, mf);
        }
        var controllers = camp.GetComponentsInChildren<BuildingController>(true);
        if (controllers.Length != 1) Debug.LogError("[Cozy Campfire] Expected 1 BuildingController; found " + controllers.Length, camp);
        if (physical.Count != 0) Debug.LogError("[Cozy Campfire] Cosmetic meshes have " + physical.Count + " forbidden physics components.", camp);
        else Debug.Log("[Cozy Campfire] Groups: " + stageGroupCount + "/3; controllers: " + controllers.Length +
            "; added colliders: 0. Scene animation/reveal/save still require Play Mode testing.", camp);
    }

    [MenuItem(Menu + "6. Remove ONLY Cozy Polish additions", false, 1055)]
    private static void Remove()
    {
        if (!SafeToEdit()) return;
        GameObject camp = FindCampfire();
        if (camp == null) { Debug.LogWarning("[Cozy Campfire] No Campfire scene object."); return; }
        if (!EditorUtility.DisplayDialog("Remove Cozy Campfire", "Удалить только дополнительные декоративные объекты CozyPolish из трёх стадий? Основная модель и система строительства сохранятся.", "Remove", "Cancel")) return;
        int deleted = 0;
        for (int i = 1; i <= 3; i++)
        {
            Transform stage = camp.transform.Find("Stage" + i);
            if (stage == null) continue;
            Transform art = stage.Find(Root);
            if (art == null) continue;
            Undo.DestroyObjectImmediate(art.gameObject);
            deleted++;
        }
        if (deleted > 0) EditorSceneManager.MarkSceneDirty(camp.scene);
        Debug.Log("[Cozy Campfire] Removed " + deleted + " cosmetic groups; building and source art untouched. Generated assets intentionally retained to preserve GUIDs.", camp);
    }

    private static bool SafeToEdit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[Cozy Campfire] Stop Play Mode and let scripts compile.");
            return false;
        }
        return true;
    }

    private static GameObject FindCampfire()
    {
        GameObject found = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (HutStages stages in root.GetComponentsInChildren<HutStages>(true))
            {
                if (stages.gameObject.name != "Campfire") continue;
                if (found != null) { Debug.LogError("[Cozy Campfire] More than one Campfire with HutStages in active scene. No changes made."); return null; }
                found = stages.gameObject;
            }
        }
        return found;
    }

    private static bool ValidateRoot(GameObject camp, bool dialog)
    {
        bool ok = camp != null && camp.GetComponent<HutStages>() != null &&
            camp.transform.Find("Stage1") != null && camp.transform.Find("Stage2") != null && camp.transform.Find("Stage3") != null &&
            camp.GetComponentsInChildren<BuildingController>(true).Length == 1;
        if (!ok)
        {
            const string message = "Нужен ровно один существующий Campfire с HutStages, Stage1/2/3 и одной зоной BuildingController в активной сцене. Никакие объекты не изменены.";
            Debug.LogError("[Cozy Campfire] " + message);
            if (dialog) EditorUtility.DisplayDialog("Campfire Cozy Polish", message, "OK");
        }
        return ok;
    }

    private static Transform Group(Transform stage)
    {
        var go = new GameObject(Root);
        Undo.RegisterCreatedObjectUndo(go, "Create cozy campfire visuals");
        go.transform.SetParent(stage, false);
        return go.transform;
    }

    private static GameObject CreateMesh(Transform root, string name, SoftMesh shape, Material material, string assetName)
    {
        if (shape.Tris == 0) throw new InvalidOperationException("Empty geometry " + name);
        Mesh mesh = shape.ToMesh(assetName);
        Mesh asset = SaveMesh(assetName, mesh);
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create campfire decoration");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = asset;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    private static Mesh SaveMesh(string name, Mesh value)
    {
        string path = ArtFolder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            // Maintain GUID on rebakes, rather than deleting assets used by a saved scene.
            EditorUtility.CopySerialized(value, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(value);
            return existing;
        }
        AssetDatabase.CreateAsset(value, path);
        return value;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game")) AssetDatabase.CreateFolder("Assets", "_Game");
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Campfire")) AssetDatabase.CreateFolder("Assets/_Game", "Campfire");
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/_Game/Campfire", "TW_CozyPolish");
    }
}
#endif
