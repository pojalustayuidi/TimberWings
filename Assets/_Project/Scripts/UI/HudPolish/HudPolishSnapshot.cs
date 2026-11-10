using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Typed object references survive saving/reopening a scene; no Editor JSON instance IDs are stored.
[Serializable] public sealed class HudPolishSnapshot
{
    [Serializable] public sealed class RectState
    {
        public RectTransform target;
        public Vector2 min, max, pivot, position, size;
        public Vector3 scale; public Quaternion rotation;
        public void Restore() { if (target == null) return; target.anchorMin = min; target.anchorMax = max; target.pivot = pivot; target.anchoredPosition = position; target.sizeDelta = size; target.localScale = scale; target.localRotation = rotation; }
    }
    [Serializable] public sealed class ImageState
    {
        public Image target; public Sprite sprite; public Color color;
        public Image.Type type; public float ppu; public bool enabled, aspect, raycast;
        public void Restore() { if (target == null) return; target.sprite = sprite; target.color = color; target.type = type; target.pixelsPerUnitMultiplier = ppu; target.enabled = enabled; target.preserveAspect = aspect; target.raycastTarget = raycast; }
    }
    [Serializable] public sealed class TextState
    {
        public TMP_Text target; public TMP_FontAsset font; public Material material;
        public float size, min, max, outline; public Color color; public FontStyles style;
        public bool autosize; public TextAlignmentOptions align; public TextWrappingModes wrap;
        public void Restore() { if (target == null) return; target.font = font; target.fontSharedMaterial = material; target.fontSize = size; target.fontSizeMin = min; target.fontSizeMax = max; target.color = color; target.fontStyle = style; target.enableAutoSizing = autosize; target.alignment = align; target.textWrappingMode = wrap; target.outlineWidth = outline; }
    }
    [Serializable] public sealed class EnabledState { public Behaviour target; public bool value; }
    public List<RectState> rects = new List<RectState>();
    public List<ImageState> images = new List<ImageState>();
    public List<TextState> texts = new List<TextState>();
    public List<EnabledState> behaviours = new List<EnabledState>();
    public CandyTaskView task; public float taskWidth, taskSide, taskTop, taskBottom, taskRow, taskGap;
    public HudMinimap map; public float stackGap;
}
