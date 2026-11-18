using System;
using UnityEngine;

[Serializable]
internal sealed class LeoFirstPersonHandMesh
{
    public int version;
    public float[] relaxed, gripped;
    public int[] skin, nails;

    internal static readonly Vector3 GripCenter = new Vector3(0f, -.051f, .050f);

    internal static LeoFirstPersonHandMesh Load()
    {
        var asset = Resources.Load<TextAsset>("LeoAnimation/FirstPersonHands");
        if (asset != null)
        {
            var shape = JsonUtility.FromJson<LeoFirstPersonHandMesh>(asset.text);
            if (shape != null && shape.version == 1 && shape.relaxed != null && shape.gripped != null &&
                shape.relaxed.Length > 0 && shape.relaxed.Length % 3 == 0 && shape.relaxed.Length == shape.gripped.Length &&
                shape.skin != null && shape.nails != null)
                return shape;
        }
        Debug.LogError("Leo first-person hand model is missing or invalid.", asset);
        return null;
    }

    internal Mesh Create(bool left, Matrix4x4 forearmBind)
    {
        Vector3[] open = Decode(relaxed, left);
        Vector3[] closed = Decode(gripped, left);
        var mesh = new Mesh { name = left ? "Leo left hand (First Person)" : "Leo right hand (First Person)" };
        mesh.vertices = closed;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(Indices(skin, left), 0, false);
        mesh.SetTriangles(Indices(nails, left), 1, false);
        mesh.RecalculateNormals();
        Vector3[] gripNormals = mesh.normals;
        mesh.vertices = open;
        mesh.RecalculateNormals();
        Vector3[] openNormals = mesh.normals;

        var displacement = new Vector3[open.Length];
        var weights = new BoneWeight[open.Length];
        var bounds = new Bounds(open[0], Vector3.zero);
        for (int i = 0; i < open.Length; i++)
        {
            displacement[i] = closed[i] - open[i];
            gripNormals[i] -= openNormals[i];
            // The proximal wrist stays seated in the forearm as the palm rolls over.
            float forearm = LeoMotionProfile.Smooth((open[i].y - .008f) / .046f);
            weights[i] = new BoneWeight
            {
                boneIndex0 = 0,
                boneIndex1 = 1,
                weight0 = 1f - forearm,
                weight1 = forearm
            };
            bounds.Encapsulate(open[i]);
            bounds.Encapsulate(closed[i]);
        }
        mesh.bindposes = new[] { Matrix4x4.identity, forearmBind };
        mesh.boneWeights = weights;
        mesh.AddBlendShapeFrame("Grip", 100f, displacement, gripNormals, null);
        bounds.Expand(.055f);
        mesh.bounds = bounds;
        return mesh;
    }

    static Vector3[] Decode(float[] values, bool left)
    {
        var positions = new Vector3[values.Length / 3];
        float mirror = left ? -1f : 1f;
        for (int i = 0; i < positions.Length; i++)
            positions[i] = new Vector3(values[i * 3] * mirror, values[i * 3 + 1], values[i * 3 + 2]);
        return positions;
    }

    static int[] Indices(int[] triangles, bool left)
    {
        if (!left) return triangles;
        var reversed = (int[])triangles.Clone();
        for (int i = 0; i < reversed.Length; i += 3)
            (reversed[i], reversed[i + 2]) = (reversed[i + 2], reversed[i]);
        return reversed;
    }
}
