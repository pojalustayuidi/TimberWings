using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// TimberWings • Ribbon V5 — compact wooden marker with solid non-overlapping fabric.
// Replaces ONLY visuals of the existing Ribbon_Memory story object.
// No story source, save key, collider, POI, dialogue or inspection component is removed.
internal static class TWRibbonStoryBaker
{
    const string Menu = "Tools/TimberWings/Story Inspection/Ribbon V5 (Stable Camera)/";
    const string VisualName = "TW_Ribbon_Story_V2"; // Same name as V2/V3: rebuilding upgrades in place.
    const string Folder = "Assets/_Project/Art/StoryInspection/RibbonV5";

    static readonly Color32 Wood = Col(149, 93, 58);
    static readonly Color32 WoodLight = Col(188, 125, 78);
    static readonly Color32 WoodDark = Col(86, 54, 45);
    static readonly Color32 Rope = Col(223, 188, 126);
    static readonly Color32 RopeShadow = Col(181, 144, 90);
    static readonly Color32 Iron = Col(139, 132, 150);
    static readonly Color32 ClothRed = Col(156, 63, 69);
    static readonly Color32 ClothRedLit = Col(188, 82, 86);
    static readonly Color32 ClothDark = Col(117, 47, 60);
    static readonly Color32 ClothInner = Col(226, 201, 169);
    static readonly Color32 ClothShade = Col(201, 176, 146);
    static readonly Color32 Cut = Col(89, 65, 61);
    static readonly Color32 CutLight = Col(247, 225, 184);
    static readonly Color32 Moss = Col(105, 139, 64);
    static readonly Color32 MossLight = Col(145, 163, 75);
    static readonly Color32 Stone = Col(112, 108, 119);

    static Color32 Col(byte r, byte g, byte b) => new Color32(r, g, b, 255);

    [MenuItem(Menu + "Build / Replace in Open Scene", false, 20)]
    static void Build()
    {
        if (!FindRibbon(out var target, out var scene)) return;
        EnsureFolder();
        var material = FindOrCreateMaterial();
        if (material == null) return;

        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("TimberWings Ribbon V5 — wooden marker");
        try
        {
            var controller = target.GetComponent<TWRibbonStoryModel>();
            if (controller == null) controller = Undo.AddComponent<TWRibbonStoryModel>(target.gameObject);

            // Capture original renderer states only once, BEFORE any visual replacement.
            if (!controller.HasCapturedOriginals)
            {
                var originals = target.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r != null && !IsGeneratedVisual(r.transform, target.transform)).ToArray();
                Undo.RecordObject(controller, "Preserve original ribbon renderers");
                controller.CaptureOriginals(originals);
            }

            RemoveGeneratedVisuals(target.transform);
            var root = new GameObject(VisualName);
            Undo.RegisterCreatedObjectUndo(root, "Create wooden ribbon marker");
            root.transform.SetParent(target.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            // Original MysticArt generator may scale Ribbon_Memory by an arbitrary value.
            // Keep the new marker a consistent ~1m world height regardless of the parent.
            Vector3 sourceScale = target.transform.lossyScale;
            root.transform.localScale = new Vector3(
                .90f / Mathf.Max(.01f, Mathf.Abs(sourceScale.x)),
                .90f / Mathf.Max(.01f, Mathf.Abs(sourceScale.y)),
                .90f / Mathf.Max(.01f, Mathf.Abs(sourceScale.z)));
            root.layer = target.gameObject.layer;

            AddMesh(root.transform, "01 · Timber frame", "WoodFrame", BuildTimber(), material);
            AddMesh(root.transform, "02 · Ground details", "Understory", BuildGround(), material);
            AddMesh(root.transform, "03 · Rope and pin", "Fastening", BuildFasteners(), material);
            AddMesh(root.transform, "04 · Hanging wooden tag", "Tag", BuildTag(), material);

            var leftPivot = AddAnimatedMesh(root.transform, "05 · Left torn red tail",
                "RibbonLeft", BuildLeftTail(), material, new Vector3(-.065f, .724f, .130f));
            var rightPivot = AddAnimatedMesh(root.transform, "06 · Inner side with six cuts",
                "RibbonRight", BuildRightRibbon(), material, new Vector3(.010f, .714f, .145f));

            // Existing StoryInspection camera reads detail.position rather than bounding box.
            var focus = new GameObject("Inspection focus · five complete, sixth unfinished");
            Undo.RegisterCreatedObjectUndo(focus, "Create ribbon inspection focus");
            focus.transform.SetParent(root.transform, false);
            focus.transform.localPosition = new Vector3(.175f, .550f, .239f);
            var inspection = target.GetComponent<StoryInspection>();
            if (inspection != null && inspection.kind == StoryInspection.Kind.Ribbon)
            {
                Undo.RecordObject(inspection, "Aim inspection camera at tally marks");
                inspection.detail = focus.transform;
                PrefabUtility.RecordPrefabInstancePropertyModifications(inspection);
            }

            Undo.RecordObject(controller, "Link ribbon cloth animation");
            controller.Configure(leftPivot, rightPivot);
            // Camera director runs after the existing StoryInspection LateUpdate, but
            // feeds its final pose back into StoryInspection for seamless recovery.
            if (target.GetComponent<TWRibbonInspectionCamera>() == null)
                Undo.AddComponent<TWRibbonInspectionCamera>(target.gameObject);
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                if (renderer != null && !renderer.transform.IsChildOf(root.transform))
                    Undo.RecordObject(renderer, "Hide previous ribbon graphics");
            controller.HideOriginals();
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("TimberWings Ribbon V4: wooden marker installed on the existing Ribbon_Memory. " +
                "Five complete incisions and one unfinished mark on the cloth reverse. " +
                "StoryInspection, dialogue, MapPoi and colliders preserved. Save the scene.", target);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, target);
            Undo.RevertAllDownToGroup(group);
        }
    }

    [MenuItem(Menu + "Restore Original Visual", false, 21)]
    static void Restore()
    {
        if (!FindRibbon(out var target, out var scene)) return;
        var controller = target.GetComponent<TWRibbonStoryModel>();
        if (controller == null)
        {
            Debug.Log("Ribbon V4: no V2/V3/V4 model controller to restore.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore original ribbon visuals");
        Undo.RecordObject(controller, "Restore ribbon renderers");
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            if (renderer != null && !IsGeneratedVisual(renderer.transform, target.transform))
                Undo.RecordObject(renderer, "Restore source renderer state");
        controller.RestoreOriginals();
        var inspection = target.GetComponent<StoryInspection>();
        if (inspection != null && inspection.detail != null && IsGeneratedVisual(inspection.detail, target.transform))
        {
            Undo.RecordObject(inspection, "Restore inspection focus");
            inspection.detail = null;
            PrefabUtility.RecordPrefabInstancePropertyModifications(inspection);
        }
        RemoveGeneratedVisuals(target.transform);
        var cameraDirector = target.GetComponent<TWRibbonInspectionCamera>();
        if (cameraDirector != null) Undo.DestroyObjectImmediate(cameraDirector);
        Undo.DestroyObjectImmediate(controller);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Ribbon V4: original graphics restored. Save the scene.", target);
    }

    [MenuItem(Menu + "Validate Active Scene", false, 22)]
    static void Validate()
    {
        if (!FindRibbon(out var target, out _)) return;
        var controller = target.GetComponent<TWRibbonStoryModel>();
        var director = target.GetComponent<TWRibbonInspectionCamera>();
        var inspection = target.GetComponent<StoryInspection>();
        var sequence = target.GetComponent<DialogueSequence>();
        bool rootPresent = target.transform.Find(VisualName) != null;
        bool okay = controller != null && controller.HasVisual && director != null && rootPresent &&
            inspection != null && inspection.kind == StoryInspection.Kind.Ribbon &&
            inspection.source == sequence && !inspection.superseded && sequence != null;
        if (okay) Debug.Log("Ribbon V5 validation OK: one marker, inspection bound, dialogue source intact.", target);
        else Debug.LogWarning("Ribbon V5 validation incomplete: run Story Inspection > Install / Update, " +
            "then Ribbon V5 > Build / Replace. See Inspector for bindings.", target);
    }

    static void RemoveGeneratedVisuals(Transform target)
    {
        for (int i = target.childCount - 1; i >= 0; --i)
        {
            var child = target.GetChild(i);
            if (child.name == VisualName || child.name == "TW_Ribbon_Story_V4")
                Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    static bool IsGeneratedVisual(Transform item, Transform target)
    {
        for (Transform t = item; t != null && t != target; t = t.parent)
            if (t.name == VisualName || t.name == "TW_Ribbon_Story_V4") return true;
        return false;
    }

    static bool FindRibbon(out MysticProp chosen, out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        chosen = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() ||
            !scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogWarning("Ribbon V4: open your SAVED gameplay scene in Edit Mode.");
            return false;
        }
        var candidates = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<MysticProp>(true))
            .Where(p => p != null && p.key == "Ribbon_Memory" &&
                p.GetComponent<DialogueSequence>() != null)
            .OrderByDescending(p => p.GetComponent<StoryInspection>() != null &&
                p.GetComponent<StoryInspection>().enabled && !p.GetComponent<StoryInspection>().superseded)
            .ThenByDescending(p => p.gameObject.activeInHierarchy).ToArray();
        if (candidates.Length == 0)
        {
            Debug.LogWarning("Ribbon V4: Ribbon_Memory with DialogueSequence not found. " +
                "Open the authored scene; Story Inspection > Install / Update can restore its binding.");
            return false;
        }
        chosen = candidates[0];
        if (candidates.Length > 1)
            Debug.LogWarning("Ribbon V4: found " + candidates.Length +
                " Ribbon_Memory sources. Updating only selected active authored source.", chosen);
        return true;
    }

    static void EnsureFolder()
    {
        string parent = "Assets";
        foreach (var part in Folder.Substring("Assets/".Length).Split('/'))
        {
            string dest = parent + "/" + part;
            if (!AssetDatabase.IsValidFolder(dest)) AssetDatabase.CreateFolder(parent, part);
            parent = dest;
        }
    }

    static Material FindOrCreateMaterial()
    {
        const string filename = Folder + "/RibbonV5VertexColors.mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>(filename);
        if (old != null) return old;
        var shader = Shader.Find("Project/CandyToon");
        if (shader == null)
        {
            Debug.LogError("Ribbon V4: requires the project's Project/CandyToon shader (vertex-colour mesh).");
            return null;
        }
        var result = new Material(shader) { name = "TW Ribbon V5 · CandyToon vertex colours" };
        if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", Color.white);
        if (result.HasProperty("_VertexColorStrength")) result.SetFloat("_VertexColorStrength", 1f);
        result.renderQueue = (int)RenderQueue.Geometry;
        if (result.HasProperty("_Surface")) result.SetFloat("_Surface", 0f);
        if (result.HasProperty("_AlphaClip")) result.SetFloat("_AlphaClip", 0f);
        if (result.HasProperty("_PeekSupport")) result.SetFloat("_PeekSupport", 0f);
        AssetDatabase.CreateAsset(result, filename);
        return result;
    }

    static Transform AddMesh(Transform parent, string objectName, string assetName, Mesh mesh, Material material)
    {
        mesh.name = "TW Ribbon V5 " + assetName;
        string path = Folder + "/" + assetName + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
        }
        else AssetDatabase.CreateAsset(mesh, path);

        var obj = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(obj, "Create ribbon prop part");
        obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return obj.transform;
    }

    static Transform AddAnimatedMesh(Transform root, string name, string asset, Mesh mesh,
        Material material, Vector3 pivotPoint)
    {
        var pivot = new GameObject(name + " · pivot");
        Undo.RegisterCreatedObjectUndo(pivot, "Create subtle ribbon motion pivot");
        pivot.transform.SetParent(root, false);
        pivot.transform.localPosition = pivotPoint;
        pivot.layer = root.gameObject.layer;
        var part = AddMesh(pivot.transform, name, asset, mesh, material);
        part.localPosition = -pivotPoint; // Mesh authored in Ribbon_Memory local coordinates.
        return pivot.transform;
    }

    static Mesh BuildTimber()
    {
        var b = new Geo();
        // Smaller, uneven wooden waymarker: not a giant upright construction.
        b.Beam(new Vector3(-.105f,.015f,-.034f), new Vector3(-.089f,.934f,-.032f),
            .078f, 7, Wood, WoodLight);
        b.Beam(new Vector3(-.375f,.735f,-.024f), new Vector3(.432f,.755f,-.020f),
            .048f, 6, Wood, WoodLight);
        b.Beam(new Vector3(-.084f,.92f,-.032f), new Vector3(-.067f,.952f,-.032f),
            .039f, 6, WoodLight, Wood);
        // A few deliberate age cracks, no floating slabs or fake boards.
        b.Stroke(new Vector3(-.045f,.32f,.055f),new Vector3(-.053f,.42f,.059f), .003f,WoodDark);
        b.Stroke(new Vector3(-.054f,.43f,.058f),new Vector3(-.045f,.47f,.062f), .002f,WoodDark);
        b.Stroke(new Vector3(.27f,.788f,.024f),new Vector3(.35f,.778f,.028f), .003f,WoodDark);
        return b.Finish("Compact old timber crossbar");
    }

    static Mesh BuildGround()
    {
        var b = new Geo();
        // Keep terrain visible: only tiny moss accents and grass, NO stone pedestal.
        b.Grass(new Vector3(-.20f,.01f,.12f), Moss);
        b.Grass(new Vector3(.14f,.01f,.10f), MossLight);
        b.Leaf(new Vector3(-.11f,.092f,.06f),.09f,.018f,Moss);
        b.Leaf(new Vector3(-.08f,.940f,.038f),.056f,.019f,MossLight);
        return b.Finish("Sparse moss — no stone base");
    }

    static Mesh BuildFasteners()
    {
        var b = new Geo();
        // Rope follows the wooden beam rather than being flat yellow rectangles.
        for (int wrap=0;wrap<3;wrap++)
        {
            float x=-.135f + wrap*.018f;
            b.Rod(new Vector3(x,.695f,.053f),new Vector3(x+.019f,.795f,.055f),
                .008f,7,wrap==1?RopeShadow:Rope);
        }
        for (int wrap=0;wrap<2;wrap++)
        {
            float x=.305f+wrap*.021f;
            b.Rod(new Vector3(x,.708f,.053f),new Vector3(x+.010f,.800f,.049f),.008f,7,Rope);
        }
        b.Ring(new Vector3(-.216f,.626f,.079f),.033f,.006f,Iron,10);
        b.Rod(new Vector3(-.216f,.661f,.077f),new Vector3(-.212f,.701f,.066f),.007f,6,RopeShadow);
        return b.Finish("Twine and forged ring");
    }

    static Mesh BuildTag()
    {
        var b = new Geo();
        const float z=.116f;
        b.PolyDouble(new [] {
            new Vector3(-.248f,.588f,z),new Vector3(-.185f,.590f,z),
            new Vector3(-.176f,.555f,z),new Vector3(-.182f,.424f,z),
            new Vector3(-.246f,.426f,z)
        },Col(170,119,80));
        b.Stroke(new Vector3(-.214f,.551f,z+.009f),new Vector3(-.214f,.467f,z+.009f),.004f,WoodDark);
        b.TriDouble(new Vector3(-.240f,.498f,z+.010f),new Vector3(-.214f,.545f,z+.010f),
            new Vector3(-.187f,.498f,z+.010f), Moss);
        b.TriDouble(new Vector3(-.235f,.475f,z+.010f),new Vector3(-.214f,.518f,z+.010f),
            new Vector3(-.192f,.475f,z+.010f), Moss);
        b.Rod(new Vector3(-.216f,.590f,z),new Vector3(-.216f,.607f,.106f),.006f,6,Rope);
        return b.Finish("Small story tag");
    }

    static Mesh BuildLeftTail()
    {
        var b = new Geo();
        // Short red strip. Different front/back depth: no coplanar triangles.
        const int segments=6;
        var l=new Vector3[segments+1];var r=new Vector3[segments+1];
        for(int i=0;i<=segments;i++)
        {
            float t=i/(float)segments;
            float x=-.035f+.019f*t+.012f*Mathf.Sin(t*2.5f);
            float y=.725f-.332f*t;
            float z=.132f+.024f*Mathf.Sin(t*2.2f);
            float width=.105f-.018f*t;
            l[i]=new Vector3(x-width*.5f,y-(i==segments?.032f:0f),z);
            r[i]=new Vector3(x+width*.5f,y+(i==segments?.026f:0f),z);
        }
        for(int i=0;i<segments;i++)
        {
            b.QuadFace(r[i],l[i],l[i+1],r[i+1],i%2==0?ClothRed:ClothRedLit);
            // Back face is physically offset so it cannot fight with the front.
            Vector3 dz=new Vector3(0,0,-.015f);
            b.QuadFace(l[i]+dz,r[i]+dz,r[i+1]+dz,l[i+1]+dz,ClothDark);
        }
        b.Stroke(l[segments],l[segments]+new Vector3(-.010f,-.033f,.006f),.0015f,ClothShade);
        b.Stroke(r[segments],r[segments]+new Vector3(.010f,-.027f,.006f),.0015f,ClothShade);
        return b.Finish("Opaque narrow red tail");
    }

    static Mesh BuildRightRibbon()
    {
        var b = new Geo();
        // Sturdy folded fabric, NOT intersecting red and cream layers.
        // Cream face is in front by 3cm, so the markings are always readable.
        const int count=12;
        var top=new Vector3[count+1]; var bottom=new Vector3[count+1];
        var creamTop=new Vector3[count+1]; var creamBottom=new Vector3[count+1];
        for(int i=0;i<=count;i++)
        {
            float t=i/(float)count;
            float x=.005f+.442f*t;
            float mid=.691f-.155f*t+.007f*Mathf.Sin(t*4f);
            float z=.155f+.050f*t+.007f*Mathf.Sin(t*5.2f);
            float half=.093f+.007f*Mathf.Sin(t*3.7f);
            // Last edge is chopped off, not the symmetrical edge of a banner.
            float ripped=i==count?-.036f:0f;
            top[i]=new Vector3(x,mid+half+ripped*.2f,z);
            bottom[i]=new Vector3(x,mid-half+ripped,z+.008f);
            creamTop[i]=new Vector3(x,mid+half-.031f+ripped*.2f,z+.037f);
            creamBottom[i]=new Vector3(x,mid-half+.023f+ripped,z+.044f);
        }
        for(int i=0;i<count;i++)
        {
            b.QuadFace(bottom[i],bottom[i+1],top[i+1],top[i],i%3==0?ClothRedLit:ClothRed);
            // Opaque physical back of cloth (1.5cm behind the front sheet).
            Vector3 back = new Vector3(0f,0f,-.015f);
            b.QuadFace(top[i]+back,top[i+1]+back,bottom[i+1]+back,bottom[i]+back,ClothDark);
            b.QuadFace(creamBottom[i],creamBottom[i+1],creamTop[i+1],creamTop[i],
                i%4==0?ClothShade:ClothInner);
        }
        // Only five complete knife cuts, followed by one short interrupted stroke.
        // Markings sit in front of the cream surface by another 8 mm.
        for(int i=0;i<6;i++)
        {
            float t=(i+1f)/7.45f;
            float x=.005f+.442f*t;
            float mid=.691f-.155f*t+.007f*Mathf.Sin(t*4f);
            float z=.155f+.050f*t+.007f*Mathf.Sin(t*5.2f)+.054f;
            float length=i==5?.026f:.058f;
            var a=new Vector3(x-.009f,mid-length,z);
            var c=new Vector3(x+.015f,mid+length,z);
            b.Stroke(a,c,i==5?.004f:.005f,Cut);
        }
        // Exact severed border: uneven and frayed, with three loose fibres.
        b.TriDouble(bottom[count],top[count],top[count]+new Vector3(.019f,-.040f,.006f),ClothRed);
        for(int i=0;i<3;i++)
        {
            var p=Vector3.Lerp(top[count],bottom[count],(i+1f)/4f);
            b.Rod(p,p+new Vector3(.018f,-.016f*(i+1f),.007f),.0015f,5,ClothShade);
        }
        // Short red end, deliberately much smaller than the V4 curtain.
        var c0=new Vector3(.419f,.589f,.207f);
        var c1=new Vector3(.468f,.570f,.215f);
        var c2=new Vector3(.481f,.338f,.241f);
        var c3=new Vector3(.438f,.322f,.240f);
        b.QuadFace(c3,c2,c1,c0,ClothDark);
        b.Stroke(c2,c2+new Vector3(.008f,-.03f,.005f),.0015f,ClothShade);
        b.Stroke(c3,c3+new Vector3(-.004f,-.025f,.004f),.0015f,ClothShade);
        return b.Finish("Opaque red cream cloth, five tallies and incomplete sixth");
    }

    sealed class Geo
    {
        readonly List<Vector3> verts=new List<Vector3>();
        readonly List<Color32> cols=new List<Color32>();
        readonly List<int> triangles=new List<int>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            int index=verts.Count;
            verts.Add(a);verts.Add(b);verts.Add(c);
            cols.Add(color);cols.Add(color);cols.Add(color);
            triangles.Add(index);triangles.Add(index+1);triangles.Add(index+2);
        }
        public void TriDouble(Vector3 a,Vector3 b,Vector3 c,Color32 color)
        {
            Tri(a,b,c,color);Tri(c,b,a,color);
        }
        public void QuadFace(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color32 color)
        {
            Tri(a,b,c,color);Tri(a,c,d,color);
        }
        public void QuadDouble(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color32 color)
        {
            TriDouble(a,b,c,color);TriDouble(a,c,d,color);
        }
        public void PolyDouble(IList<Vector3> points,Color32 color)
        {
            for(int i=1;i<points.Count-1;i++)TriDouble(points[0],points[i],points[i+1],color);
        }
        public void Stroke(Vector3 a,Vector3 b,float width,Color32 color)
        {
            Vector3 axis=b-a;
            if(axis.sqrMagnitude<.0000001f)return;
            Vector3 normal=Vector3.Cross(axis.normalized,Vector3.forward);
            if(normal.sqrMagnitude<.000001f)normal=Vector3.Cross(axis.normalized,Vector3.up);
            normal=normal.normalized*width;
            QuadDouble(a-normal,a+normal,b+normal,b-normal,color);
        }
        public void Rod(Vector3 a,Vector3 b,float radius,int sides,Color32 color)
        {
            Vector3 axis=b-a;
            if(axis.sqrMagnitude<.0000001f)return;
            axis.Normalize();
            Vector3 u=Vector3.Cross(axis,Vector3.up);
            if(u.sqrMagnitude<.01f)u=Vector3.Cross(axis,Vector3.forward);
            u.Normalize();
            var v=Vector3.Cross(axis,u).normalized;
            for(int i=0;i<sides;i++)
            {
                float p=(i*2f*Mathf.PI)/sides;
                float q=((i+1)*2f*Mathf.PI)/sides;
                Vector3 offset1=(Mathf.Cos(p)*u+Mathf.Sin(p)*v)*radius;
                Vector3 offset2=(Mathf.Cos(q)*u+Mathf.Sin(q)*v)*radius;
                QuadDouble(a+offset1,a+offset2,b+offset2,b+offset1,color);
                TriDouble(a,a+offset1,a+offset2,color);
                TriDouble(b,b+offset2,b+offset1,color);
            }
        }
        public void Beam(Vector3 from,Vector3 to,float radius,int sides,Color32 main,Color32 highlight)
        {
            Vector3 axis=(to-from).normalized;
            Vector3 u=Vector3.Cross(axis,Vector3.forward);
            if(u.sqrMagnitude<.01f)u=Vector3.Cross(axis,Vector3.up);
            u.Normalize();Vector3 v=Vector3.Cross(axis,u).normalized;
            for(int i=0;i<sides;i++)
            {
                float p=(2*Mathf.PI*i)/sides;
                float q=(2*Mathf.PI*(i+1))/sides;
                Vector3 r=(Mathf.Cos(p)*u+Mathf.Sin(p)*v)*radius;
                Vector3 s=(Mathf.Cos(q)*u+Mathf.Sin(q)*v)*radius;
                QuadDouble(from+r,from+s,to+s,to+r,i%3==0?highlight:main);
                TriDouble(from,from+r,from+s,main);
                TriDouble(to,to+s,to+r,highlight);
            }
        }
        public void Ring(Vector3 center,float radius,float tube,Color32 color,int segments)
        {
            for(int i=0;i<segments;i++)
            {
                float p=2f*Mathf.PI*i/segments,q=2f*Mathf.PI*(i+1)/segments;
                var a=center+new Vector3(Mathf.Cos(p)*radius,Mathf.Sin(p)*radius,0f);
                var b=center+new Vector3(Mathf.Cos(q)*radius,Mathf.Sin(q)*radius,0f);
                Rod(a,b,tube,5,color);
            }
        }
        public void Rock(Vector3 center,float rx,float rz,float h,int sides,Color32 side,Color32 top)
        {
            Vector3[] low=new Vector3[sides],high=new Vector3[sides];
            for(int i=0;i<sides;i++)
            {
                float theta=2f*Mathf.PI*i/sides;
                float w=1f+.06f*Mathf.Sin(i*4.1f+sides);
                float x=Mathf.Cos(theta)*rx*w,z=Mathf.Sin(theta)*rz*w;
                low[i]=center+new Vector3(x*1.04f,0f,z*1.04f);
                high[i]=center+new Vector3(x*.89f,h+.012f*Mathf.Sin(i*2.1f),z*.88f);
            }
            Vector3 mid=center+Vector3.up*h;
            for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides;
                QuadDouble(low[i],low[j],high[j],high[i],side);
                TriDouble(mid,high[i],high[j],top);
                TriDouble(center,low[j],low[i],side);
            }
        }
        public void Grass(Vector3 p,Color32 color)
        {
            for(int i=0;i<3;i++)
            {
                float dx=(i-1)*.052f;
                TriDouble(p+new Vector3(dx-.021f,0f,0f),
                    p+new Vector3(dx,.12f-(i%2)*.023f,.018f),
                    p+new Vector3(dx+.021f,0f,0f),color);
            }
        }
        public void Leaf(Vector3 p,float width,float height,Color32 color)
        {
            TriDouble(p+Vector3.left*width*.5f,
                p+Vector3.up*height,
                p+Vector3.right*width*.5f,color);
        }
        public Mesh Finish(string name)
        {
            var mesh=new Mesh{name="TW Ribbon V5 · "+name};
            if(verts.Count>65535) mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
