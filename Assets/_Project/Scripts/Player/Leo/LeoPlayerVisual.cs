using System;
using UnityEngine;
using Object = UnityEngine.Object;

static class LeoPlayerVisual
{
    public const string ResourcePath = "LeoAnimation/Leo_Model";

    public static void Install(PlayerMovement player)
    {
        Transform previous = player.model;
        if (previous == null || !string.Equals(previous.name, "characterMedium", StringComparison.Ordinal) ||
            previous.GetComponentInChildren<LeoRig>(true) != null ||
            !player.TryGetComponent<PlayerStats>(out _) ||
            !player.TryGetComponent<CapsuleCollider>(out var capsule) || !capsule.enabled) return;

        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null || !prefab.TryGetComponent<LeoRig>(out _))
        {
            Debug.LogWarning("LEO: не найден префаб визуала игрока.", player);
            return;
        }

        Vector3 facing = Vector3.ProjectOnPlane(previous.forward, Vector3.up);
        Quaternion rotation = facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : player.transform.rotation;
        var visual = Object.Instantiate(prefab, previous.position, rotation, player.transform);
        visual.name = prefab.name;
        float bottom = float.PositiveInfinity;
        foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.name.EndsWith("__Sole", StringComparison.Ordinal) &&
                renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        }
        if (float.IsPositiveInfinity(bottom))
        {
            Object.Destroy(visual);
            Debug.LogWarning("LEO: в префабе нет геометрии подошв.", player);
            return;
        }
        visual.transform.position += Vector3.up * (capsule.bounds.min.y - bottom);

        foreach (var joint in visual.GetComponentsInChildren<Transform>(true))
            joint.gameObject.layer = player.gameObject.layer;

        // The original Animator still owns gameplay events, including the tree hit frame.
        foreach (var renderer in previous.GetComponentsInChildren<Renderer>(true))
        {
            if ((renderer is SkinnedMeshRenderer || renderer is MeshRenderer) &&
                renderer.GetComponentInParent<Canvas>(true) == null) renderer.enabled = false;
        }
        PreserveEvents(player.animator);
        var interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null) PreserveEvents(interaction.animator);

        player.model = visual.transform;
        var speaker = player.GetComponent<Speaker>();
        if (speaker != null && speaker.turnRoot != null && speaker.turnRoot.IsChildOf(previous))
            speaker.turnRoot = visual.transform;
    }

    static void PreserveEvents(Animator animator)
    {
        if (animator == null) return;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }
}
