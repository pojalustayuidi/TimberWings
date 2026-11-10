using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class TreePolishController
{
    sealed class Surface
    {
        public MeshFilter filter;
        public Renderer renderer;
        public Mesh original, shell, liveMesh;
        public Material[] materials;
        public bool radial;
    }
    readonly List<Surface> surfaces = new List<Surface>(24);
    readonly List<MeshFilter> filters = new List<MeshFilter>(32);
    readonly Dictionary<Mesh, Mesh> shellMap = new Dictionary<Mesh, Mesh>();
    readonly Dictionary<MeshFilter, MeshBinding> bindingMap = new Dictionary<MeshFilter, MeshBinding>();
    MaterialPropertyBlock drawBlock;
    MaterialPropertyBlock sourceBlock;
    MaterialPropertyBlock rendererBlock;
    bool shellMapReady;
    static readonly int Width = Shader.PropertyToID("_FocusWidth");
    static readonly int Radial = Shader.PropertyToID("_FocusRadial");
    static readonly int Center = Shader.PropertyToID("_FocusCenter");
    static readonly int Wind = Shader.PropertyToID("_FocusTreeWind");
    static readonly int Rand = Shader.PropertyToID("_Rand");
    static readonly int WindDir = Shader.PropertyToID("_WindDir");
    static readonly int[] WindFloats = { Shader.PropertyToID("_WindStrength"), Shader.PropertyToID("_WindSpeed"), Shader.PropertyToID("_WindStart"), Shader.PropertyToID("_WindHeight"), Shader.PropertyToID("_Flutter"), Shader.PropertyToID("_FlutterSpeed") };

    void ClearOutline() { surfaces.Clear(); filters.Clear(); }
    void BindOutline(ResourceItem item)
    {
        ClearOutline();
        if (item == null) return;
        if (!shellMapReady)
        {
            shellMapReady = true;
            if (shells != null) foreach (var pair in shells) if (pair != null && pair.source != null && pair.shell != null) shellMap[pair.source] = pair.shell;
            if (bindings != null) foreach (var pair in bindings) if (pair != null && pair.filter != null && pair.source != null) bindingMap[pair.filter] = pair;
        }
        item.GetComponentsInChildren(false, filters);
        foreach (var filter in filters)
        {
            if (surfaces.Count >= 24) break;
            if (filter.sharedMesh == null || filter.name == "Hull" || filter.name == "HighlightRing") continue;
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null || !renderer.enabled) continue;
            Mesh sourceMesh = filter.sharedMesh;
            MeshBinding binding;
            if (renderer.isPartOfStaticBatch)
            {
                if (!bindingMap.TryGetValue(filter, out binding) || binding.source == null) continue;
                sourceMesh = binding.source; // Never outline an entire combined forest batch.
            }
            Mesh shell;
            bool baked = shellMap.TryGetValue(sourceMesh, out shell) && shell != null;
            surfaces.Add(new Surface { filter = filter, renderer = renderer, original = sourceMesh, liveMesh = filter.sharedMesh,
                shell = baked ? shell : sourceMesh, radial = !baked, materials = renderer.sharedMaterials });
        }
    }
    void DrawOutline(Camera camera, Color color, float pixels)
    {
        if (outline == null || outline.shader == null || !outline.shader.isSupported || pixels <= 0 || color.a <= .005f) return;
        int budget = 48;
        foreach (var surface in surfaces)
        {
            if (surface.filter == null || surface.renderer == null || !surface.renderer.enabled || !surface.renderer.gameObject.activeInHierarchy) continue;
            if (surface.filter.sharedMesh != surface.liveMesh) { BindOutline(Focus); return; }
            if (!surface.renderer.isVisible) continue; // Respect disabled/culled LOD renderers.
            rendererBlock.Clear(); surface.renderer.GetPropertyBlock(rendererBlock);
            int count = Mathf.Min(surface.shell.subMeshCount, surface.materials.Length);
            for (int sub = 0; sub < count && budget > 0; sub++, budget--)
            {
                var source = surface.materials[sub];
                drawBlock.Clear(); sourceBlock.Clear();
                surface.renderer.GetPropertyBlock(sourceBlock, sub);
                bool wind = source != null && source.shader != null && source.shader.name == "Project/TreeToon";
                drawBlock.SetFloat(Wind, wind ? 1f : 0f);
                if (wind)
                {
                    foreach (int id in WindFloats)
                        drawBlock.SetFloat(id, sourceBlock.HasFloat(id) ? sourceBlock.GetFloat(id) : rendererBlock.HasFloat(id) ? rendererBlock.GetFloat(id) : source.HasProperty(id) ? source.GetFloat(id) : 0f);
                    drawBlock.SetVector(WindDir, sourceBlock.HasVector(WindDir) ? sourceBlock.GetVector(WindDir) : rendererBlock.HasVector(WindDir) ? rendererBlock.GetVector(WindDir) : source.GetVector(WindDir));
                    drawBlock.SetFloat(Rand, sourceBlock.HasFloat(Rand) ? sourceBlock.GetFloat(Rand) : rendererBlock.HasFloat(Rand) ? rendererBlock.GetFloat(Rand) : source.HasProperty(Rand) ? source.GetFloat(Rand) : -1f);
                }
                drawBlock.SetFloat(Radial, surface.radial ? 1f : 0f);
                drawBlock.SetVector(Center, surface.original.bounds.center);
                drawBlock.SetFloat(Width, pixels);
                drawBlock.SetColor(Tint, color);
                Graphics.DrawMesh(surface.shell, surface.filter.transform.localToWorldMatrix, outline,
                    surface.renderer.gameObject.layer, camera, sub, drawBlock, ShadowCastingMode.Off, false);
            }
        }
    }
}
