using System;
using UnityEngine;

[Serializable]
internal sealed class LeoFirstPersonHandMesh
{
    public int version;
    public float[] relaxed, gripped;
    public int[] skin, nails;
    public float[] watch;
    public int[] leather, brass, dial, markings;
    public float[] sleeveVertices;
    public int[] sleeveCloth, sleeveFold, sleeveHem;

    internal static readonly Vector3 GripCenter = new Vector3(0f, -.051f, .050f);

    internal static LeoFirstPersonHandMesh Load()
    {
        var asset = Resources.Load<TextAsset>("LeoAnimation/FirstPersonHands");
        if (asset != null)
        {
            var shape = JsonUtility.FromJson<LeoFirstPersonHandMesh>(asset.text);
            if (shape != null && (shape.version == 1 || shape.version == 2) &&
                shape.relaxed != null && shape.gripped != null &&
                shape.relaxed.Length > 0 && shape.relaxed.Length % 3 == 0 && shape.relaxed.Length == shape.gripped.Length &&
                shape.skin != null && shape.nails != null)
                return shape;
        }
        Debug.LogError("Leo first-person hand model is missing or invalid.", asset);
        return null;
    }

    internal Mesh Create(Matrix4x4 forearmBind, bool left)
    {
        Vector3[] open = Decode(relaxed, left);
        Vector3[] closed = Decode(gripped, left);
        var mesh = new Mesh { name = left ? "Leo left hand (First Person)" : "Leo right hand (First Person)" };
        mesh.vertices = closed;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(Faces(skin, left), 0, false);
        mesh.SetTriangles(Faces(nails, left), 1, false);
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
            // Split the roll across two joints so linear skinning cannot pinch the wrist
            // when the hand turns close to a half revolution around the grip.
            float forearm = LeoMotionProfile.Smooth((open[i].y - .006f) / .056f);
            bool proximal = forearm >= .5f;
            float weight = proximal ? 2f * forearm - 1f : 2f * forearm;
            weights[i] = new BoneWeight
            {
                boneIndex0 = proximal ? 2 : 0,
                boneIndex1 = proximal ? 1 : 2,
                weight0 = 1f - weight,
                weight1 = weight
            };
            bounds.Encapsulate(open[i]);
            bounds.Encapsulate(closed[i]);
        }
        mesh.bindposes = new[] { Matrix4x4.identity, forearmBind, Matrix4x4.identity };
        mesh.boneWeights = weights;
        mesh.AddBlendShapeFrame("Grip", 100f, displacement, gripNormals, null);
        bounds.Expand(.055f);
        mesh.bounds = bounds;
        return mesh;
    }

    internal Mesh CreateWatch()
    {
        if (watch == null || leather == null || brass == null || dial == null || markings == null) return null;
        var mesh = new Mesh { name = "Leo trail watch (First Person)", vertices = Decode(watch, false), subMeshCount = 4 };
        mesh.SetTriangles(leather, 0, false);
        mesh.SetTriangles(brass, 1, false);
        mesh.SetTriangles(dial, 2, false);
        mesh.SetTriangles(markings, 3, false);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    internal Mesh CreateSleeve()
    {
        if (sleeveVertices == null || sleeveCloth == null || sleeveFold == null || sleeveHem == null) return null;
        var mesh = new Mesh { name = "Leo rolled forearm sleeve", vertices = Decode(sleeveVertices, false), subMeshCount = 3 };
        mesh.SetTriangles(sleeveCloth, 0, false);
        mesh.SetTriangles(sleeveFold, 1, false);
        mesh.SetTriangles(sleeveHem, 2, false);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static int[] Faces(int[] faces, bool mirror)
    {
        if (!mirror) return faces;
        var reversed = (int[])faces.Clone();
        for (int i = 0; i < reversed.Length; i += 3)
            (reversed[i + 1], reversed[i + 2]) = (reversed[i + 2], reversed[i + 1]);
        return reversed;
    }

    static Vector3[] Decode(float[] values, bool mirror)
    {
        var positions = new Vector3[values.Length / 3];
        float flip = mirror ? -1f : 1f;
        for (int i = 0; i < positions.Length; i++)
            positions[i] = new Vector3(values[i * 3] * flip, values[i * 3 + 1], values[i * 3 + 2]);
        return positions;
    }
}
