// TimberWings: one-click, idempotent collision retrofit for the existing Lumberjack Hut V2.
// Keeps HutStages and all visual meshes. Can be re-run after the original generator.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HutCollisionSafetyBaker
{
    const string RootName = "__TW_HutColliders";
    const string FenceName = "__TW_FenceCollision";
    const string DetailName = "__TW_HutSafetyDetails";

    // These dimensions are from LumberjackHutBuilder; stages share the same local coordinates.
    const float W = 4.4f, D = 3.8f, Foundation = 0.28f;
    const float DoorHalf = 0.78f, DoorHeight = 2.60f;
    const float RoofBase = 3.15f, RoofRise = 1.7f, RoofHalfSpan = D / 2f + 0.45f;
    const float FenceX0 = -5.6f, FenceX1 = 10.6f, FenceZ0 = -4.6f, FenceZ1 = 4.8f;
    const float GateX = 2.4f, GateW = 2.4f;

    [MenuItem("Tools/BuilderSim/Хижина лесоруба v2/Исправить коллизии (без пересборки)")]
    public static void BakeSelected()
    {
        HutStages hut = LumberjackHutBuilder.FindSelectedHut();
        if (hut == null)
        {
            EditorUtility.DisplayDialog("TimberWings — коллизии хижины",
                "Выдели в Hierarchy корень хижины с HutStages и повтори команду.", "OK");
            return;
        }
        if (!Bake(hut, true))
            EditorUtility.DisplayDialog("TimberWings — коллизии хижины",
                "Не удалось исправить хижину. Посмотри предупреждение в Console.", "OK");
    }

    public static bool Bake(HutStages hut, bool blockingFence = true)
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[TW Hut Safety] Сначала выйди из Play Mode.");
            return false;
        }
        if (hut == null) return false;

        Transform a = hut.GetStageObject(1)?.transform;
        Transform b = hut.GetStageObject(2)?.transform;
        Transform c = hut.GetStageObject(3)?.transform;
        if (a == null || b == null || c == null ||
            a.Find("__HutV2") == null || b.Find("__HutV2") == null || c.Find("__HutV2") == null)
        {
            Debug.LogWarning("[TW Hut Safety] Нужна хижина V2 со всеми Stage1/Stage2/Stage3. Сначала собери её текущим LumberjackHutBuilder.", hut);
            return false;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("TW: безопасные коллизии хижины");
        int group = Undo.GetCurrentGroup();

        // Keep the existing stage visible after the bake; no construction state is changed.
        bool one = a.gameObject.activeSelf, two = b.gameObject.activeSelf, three = c.gameObject.activeSelf;
        Transform yard = a.Find("YardGround");
        float groundY = yard != null ? yard.localPosition.y - 0.025f : 0f;

        try
        {
            Transform ac = CleanRoot(a, RootName);
            Transform bc = CleanRoot(b, RootName);
            Transform cc = CleanRoot(c, RootName);

            // STAGE 1: true walkable physical surface, foundation and gap-free sawmill deck.
            AddMeshCollider(a.Find("YardGround"));
            AddBoxCollider(a.Find("HutFoundation"));
            Box(ac, "SawmillDeck_FullFootprint", new Vector3(6.43f, 0.043f + groundY, -0.3f), new Vector3(5.00f, 0.17f, 3.55f));
            // A continuous deck collider avoids falling through spaces between decorative boards.

            Transform fence = a.Find("Fence");
            if (fence != null)
            {
                Transform old = fence.Find(FenceName);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                if (blockingFence)
                {
                    Transform fc = NewRoot(fence, FenceName);
                    float h = 1.12f;
                    float fx0 = GateX - GateW * .5f, fx1 = GateX + GateW * .5f;
                    Box(fc, "FrontLeft", new Vector3((FenceX0 + fx0) * .5f, groundY + h * .5f, FenceZ1),
                        new Vector3(fx0 - FenceX0, h, .18f));
                    Box(fc, "FrontRight", new Vector3((FenceX1 + fx1) * .5f, groundY + h * .5f, FenceZ1),
                        new Vector3(FenceX1 - fx1, h, .18f));
                    Box(fc, "Back", new Vector3((FenceX0 + FenceX1) * .5f, groundY + h * .5f, FenceZ0),
                        new Vector3(FenceX1 - FenceX0, h, .18f));
                    Box(fc, "Left", new Vector3(FenceX0, groundY + h * .5f, (FenceZ0 + FenceZ1) * .5f),
                        new Vector3(.18f, h, FenceZ1 - FenceZ0));
                    Box(fc, "Right", new Vector3(FenceX1, groundY + h * .5f, (FenceZ0 + FenceZ1) * .5f),
                        new Vector3(.18f, h, FenceZ1 - FenceZ0));
                    // The gate stays open. The entire fence root has StageVisibility (stages 1–2).
                }
            }

            // STAGE 2: replace the one giant collider which blocked the whole interior.
            Transform oldWall = b.Find("Walls/HutCollider");
            if (oldWall != null) Undo.DestroyObjectImmediate(oldWall.gameObject);
            AddBoxCollider(b.Find("Walls/Floor"));
            float lowerTop = Foundation + .15f + 4f * .30f + .17f; // upper edge of course #4
            WallRing(bc, Foundation, lowerTop, false);
            // Collide against real visible sawmill beams, not an invisible solid box.
            Transform skeleton = b.Find("SawSkeleton");
            if (skeleton != null)
                foreach (Transform part in skeleton.GetComponentsInChildren<Transform>(true))
                    if (part.name == "TableLeg" || part.name == "ArchPost" ||
                        part.name == "WheelPost" || part.name.StartsWith("FlumeRim"))
                        AddBoxCollider(part);

            // STAGE 3: wall extensions with a REAL doorway, roof, porch, furniture, machinery.
            WallRing(cc, lowerTop - .02f, RoofBase, true);
            float slope = Mathf.Atan2(RoofRise, RoofHalfSpan) * Mathf.Rad2Deg;
            float slopeLength = Mathf.Sqrt(RoofHalfSpan * RoofHalfSpan + RoofRise * RoofRise);
            for (int sign = -1; sign <= 1; sign += 2)
                Box(cc, sign < 0 ? "RoofSlopeBack" : "RoofSlopeFront",
                    new Vector3(0f, RoofBase + RoofRise * .5f, sign * RoofHalfSpan * .5f),
                    new Vector3(5.5f, .13f, slopeLength), new Vector3(sign * slope, 0f, 0f));

            Transform porch = c.Find("Porch");
            AddBoxCollider(porch != null ? porch.Find("PorchDeck") : null);
            AddBoxCollider(porch != null ? porch.Find("PorchStep") : null);
            AddBoxCollider(porch != null ? porch.Find("PlanterBox") : null);
            // A low, walkable approach to the porch. Rigidbody locomotion cannot climb vertical box steps.
            Box(cc, "PorchRamp", new Vector3(0f, .145f + groundY, 3.18f),
                new Vector3(1.45f, .10f, .99f), new Vector3(15.3f, 0f, 0f));
            Box(cc, "DoorThreshold", new Vector3(0f, .315f, 2.04f),
                new Vector3(1.40f, .09f, .85f), new Vector3(6f, 0f, 0f));

            // Restrict only substantial static props. No colliders on animated saw, wheel, workers or signs.
            AddBoxCollider(c.Find("Interior/BedFrame"));
            AddBoxCollider(c.Find("Interior/BedMattress"));
            AddBoxCollider(c.Find("Interior/TableTop"));
            AddBoxCollider(c.Find("Interior/Stool"));
            AddBoxCollider(c.Find("Chimney/ChimneyBase"));
            Box(cc, "ChimneySolid", new Vector3(-2.6f, 2.36f, -.4f), new Vector3(.85f, 4.5f, .85f));
            Transform machine = c.Find("Sawmill");
            if (machine != null)
                foreach (Transform part in machine.GetComponentsInChildren<Transform>(true))
                    if (part.name == "TableTop" || part.name == "BladeHood")
                        AddBoxCollider(part);
            // Static machine guard only: do not attach a collider to the spinning wheel mesh.
            Box(cc, "WaterWheelGuard", new Vector3(8.4f, 1.05f, -2.75f), new Vector3(2.1f, 2.1f, .95f));

            // Visually grounded low-poly wooden entry ramp, matching the existing hut plank material.
            Transform details = CleanRoot(c, DetailName);
            Transform reference = porch != null ? porch.Find("PorchDeck") : null;
            Mesh mesh = reference != null ? reference.GetComponent<MeshFilter>()?.sharedMesh : null;
            Material mat = reference != null ? reference.GetComponent<MeshRenderer>()?.sharedMaterial : null;
            if (mesh != null && mat != null)
            {
                for (int i = -1; i <= 1; i++)
                    VisualBoard(details, "ApproachPlank", new Vector3(i * .47f, .145f + groundY, 3.18f),
                        new Vector3(.43f, .085f, .99f), Quaternion.Euler(15.3f, 0f, 0f), mesh, mat);
                VisualBoard(details, "DoorSill", new Vector3(0f, .33f, 1.99f),
                    new Vector3(1.43f, .07f, .32f), Quaternion.Euler(5f, 0f, 0f), mesh, mat);
            }

            // IMPORTANT HOTFIX: never relocate a player to solve a collider issue.
            // The v1 hut-local fall guard compared the PLAYER PIVOT to the yard height,
            // so on certain character rigs it interpreted ordinary walking as a fall
            // and teleported the hero. Remove the old serialized component.
            RemoveLegacyGuard(hut);

            // CCD matters for high-speed carry-dash: do not phase through thin wall colliders.
            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (player != null && player.GetComponent<PlayerPhysicsSafety>() == null)
                Undo.AddComponent<PlayerPhysicsSafety>(player.gameObject);

            a.gameObject.SetActive(one);
            b.gameObject.SetActive(two);
            c.gameObject.SetActive(three);
            EditorUtility.SetDirty(hut);
            EditorSceneManager.MarkSceneDirty(hut.gameObject.scene);
            Physics.SyncTransforms();
            Debug.Log("[TW Hut Safety v2] Коллизии исправлены; устаревшая телепортация удалена. " +
                      "Никакой автоматический перенос игрока патч не выполняет. Стадии не пересобраны.", hut);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[TW Hut Safety] Bake error: " + e, hut);
            return false;
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
    }

    [MenuItem("Tools/BuilderSim/Хижина лесоруба v2/ОТКЛЮЧИТЬ телепорты старого патча")]
    public static void DisableOldTeleports()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[TW Hut Safety v2] Выйди из Play Mode перед исправлением сцены. " +
                             "Замена HutLocalFallGuard.cs уже отключает телепортацию в runtime.");
            return;
        }

        int removed = 0;
        foreach (HutLocalFallGuard guard in Object.FindObjectsByType<HutLocalFallGuard>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (guard == null) continue;
            Undo.DestroyObjectImmediate(guard);
            removed++;
        }

        if (removed > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log("[TW Hut Safety v2] Удалены устаревшие локальные телепорты: " + removed +
                  ". Теперь хижина не перемещает героя программно.");
    }

    static void RemoveLegacyGuard(HutStages hut)
    {
        if (hut == null) return;
        HutLocalFallGuard guard = hut.GetComponent<HutLocalFallGuard>();
        if (guard != null) Undo.DestroyObjectImmediate(guard);
    }

    [MenuItem("Tools/BuilderSim/Хижина лесоруба v2/Проверить коллизии (аудит)")]
    static void AuditSelected()
    {
        HutStages hut = LumberjackHutBuilder.FindSelectedHut();
        if (hut == null) { Debug.LogWarning("[TW Hut Safety] Выдели хижину с HutStages."); return; }
        bool okay = true;
        int total = 0;
        for (int i = 1; i <= 3; i++)
        {
            GameObject obj = hut.GetStageObject(i);
            int count = obj != null ? obj.GetComponentsInChildren<Collider>(true).Length : 0;
            total += count;
            if (obj == null || count == 0 || obj.transform.Find(RootName) == null)
            {
                okay = false;
                Debug.LogWarning("[TW Hut Safety] Stage" + i + ": не найдены запечённые коллизии.", hut);
            }
        }
        if (hut.GetStageObject(2)?.transform.Find("Walls/HutCollider") != null)
        {
            okay = false;
            Debug.LogWarning("[TW Hut Safety] Остался старый глухой HutCollider: он закрывает проход через дверь.", hut);
        }
        if (hut.GetComponent<HutLocalFallGuard>() != null)
        {
            okay = false;
            Debug.LogWarning("[TW Hut Safety] На хижине остался устаревший HutLocalFallGuard. " +
                             "Запусти «Исправить коллизии (без пересборки)». В новой версии он не телепортирует игрока.", hut);
        }
        Debug.Log("[TW Hut Safety] Аудит: " + (okay ? "базовые проверки пройдены" : "нужно исправить") +
                  ", физических Collider = " + total + ". Проверь вручную ходьбу и строительство в Play Mode.", hut);
    }

    static Transform CleanRoot(Transform parent, string name)
    {
        Transform prev = parent.Find(name);
        if (prev != null) Undo.DestroyObjectImmediate(prev.gameObject);
        return NewRoot(parent, name);
    }

    static Transform NewRoot(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "TW Hut: " + name);
        return go.transform;
    }

    static BoxCollider Box(Transform parent, string name, Vector3 pos, Vector3 size, Vector3? rotation = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        if (rotation.HasValue) go.transform.localRotation = Quaternion.Euler(rotation.Value);
        BoxCollider col = go.AddComponent<BoxCollider>();
        col.size = size;
        return col;
    }

    static void AddBoxCollider(Transform t)
    {
        if (t == null || t.GetComponent<BoxCollider>() != null) return;
        BoxCollider col = Undo.AddComponent<BoxCollider>(t.gameObject);
        col.center = Vector3.zero;
        col.size = Vector3.one; // Existing HutMeshLib.SoftCube is unit-sized, scaled by the Transform.
    }

    static void AddMeshCollider(Transform t)
    {
        if (t == null) return;
        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        MeshCollider col = t.GetComponent<MeshCollider>();
        if (col == null) col = Undo.AddComponent<MeshCollider>(t.gameObject);
        col.sharedMesh = mf.sharedMesh;
        col.convex = false;
        col.isTrigger = false;
        EditorUtility.SetDirty(col);
    }

    static void WallRing(Transform root, float bottom, float top, bool doorLintel)
    {
        if (top <= bottom) return;
        const float thickness = .35f;
        float mid = (bottom + top) * .5f;
        float h = top - bottom;
        Box(root, "BackWall", new Vector3(0f, mid, -D * .5f), new Vector3(W + .25f, h, thickness));
        Box(root, "LeftWall", new Vector3(-W * .5f, mid, 0f), new Vector3(thickness, h, D + .25f));
        Box(root, "RightWall", new Vector3(W * .5f, mid, 0f), new Vector3(thickness, h, D + .25f));
        float segment = W * .5f - DoorHalf;
        Box(root, "FrontWallLeft", new Vector3(-(DoorHalf + segment * .5f), mid, D * .5f),
            new Vector3(segment, h, thickness));
        Box(root, "FrontWallRight", new Vector3(DoorHalf + segment * .5f, mid, D * .5f),
            new Vector3(segment, h, thickness));
        if (doorLintel)
        {
            float y = Foundation + DoorHeight;
            if (top > y)
                Box(root, "DoorLintel", new Vector3(0f, (top + y) * .5f, D * .5f),
                    new Vector3(DoorHalf * 2f, top - y, thickness));
        }
    }

    static void VisualBoard(Transform parent, string name, Vector3 pos, Vector3 scale,
        Quaternion rotation, Mesh mesh, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rotation;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
    }
}
#endif
