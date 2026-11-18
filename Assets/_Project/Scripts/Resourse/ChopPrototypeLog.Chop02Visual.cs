using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class ChopPrototypeLog
{
    static Mesh cutMarkMesh;
    static readonly int MarkBaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int MarkColor = Shader.PropertyToID("_Color");
    static readonly Color IdleMark = new Color(.82f, .64f, .32f);
    static readonly Color FocusMark = new Color(.65f, .83f, .42f);

    MeshRenderer CreateCutMark(Transform parent, Material material)
    {
        if (cutMarkMesh == null)
        {
            cutMarkMesh = new Mesh { name = "Cut shoulder markers", hideFlags = HideFlags.HideAndDontSave };
            // Two solid-facing chevrons leave the actual cut visible between them.
            cutMarkMesh.vertices = new[]
            {
                new Vector3(-.51f, -.165f, -.065f), new Vector3(-.51f, -.165f, .065f),
                new Vector3(-.18f, .081f, 0f), new Vector3(-.34f, -.028f, 0f),
                new Vector3(.51f, -.165f, -.065f), new Vector3(.51f, -.165f, .065f),
                new Vector3(.18f, .081f, 0f), new Vector3(.34f, -.028f, 0f)
            };
            cutMarkMesh.triangles = new[] { 0, 1, 3, 1, 2, 3, 2, 0, 3, 4, 7, 5, 5, 7, 6, 6, 7, 4 };
            cutMarkMesh.RecalculateNormals();
            cutMarkMesh.RecalculateBounds();
        }

        var mark = new GameObject("Cut markers");
        mark.transform.SetParent(parent, false);
        mark.transform.localPosition = Vector3.up * (state.layout.radius * .15f + .012f);
        mark.transform.localScale = new Vector3(state.layout.radius * 1.67f,
            state.layout.radius, Mathf.Max(.18f, state.layout.radius * .90f));
        mark.AddComponent<MeshFilter>().sharedMesh = cutMarkMesh;
        var renderer = mark.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        return renderer;
    }

    void StyleCut(int cut)
    {
        var renderer = cutMarks[cut];
        renderer.enabled = CanCut(cut);
        if (!renderer.enabled) return;
        if (markerProperties == null) markerProperties = new MaterialPropertyBlock();
        Color tint = focusCut == cut ? FocusMark : IdleMark;
        markerProperties.SetColor(MarkBaseColor, tint);
        markerProperties.SetColor(MarkColor, tint);
        renderer.SetPropertyBlock(markerProperties);
    }
}
