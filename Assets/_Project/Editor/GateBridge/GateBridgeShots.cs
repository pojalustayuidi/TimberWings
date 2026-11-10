#if UNITY_EDITOR
// Assets/_Project/Editor/GateBridge/GateBridgeShots.cs (Editor)
// Tools > BuilderSim > Локация > Снимки моста и площадки: четыре кадра с ОДНОГО ракурса (сломан / опоры / настил / готов) и вид на финальную площадку
// с камерой в духе игровой (сзади и сверху от Лео, FOV 45). PNG кладутся в <проект>/Screenshots/GateBridge/. Сцену не меняет.
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GateBridgeShots
{
    [MenuItem("Tools/BuilderSim/Локация/Снимки моста и площадки", false, 105)]
    static void Run()
    {
        string err;
        var gb = GateBridgeBuilder.FindOrCreate(out err);
        var fi = gb != null ? gb.GetComponent<GateBridgePlacement>() : null;
        if (gb == null || gb.walkway == null || fi == null) { EditorUtility.DisplayDialog("GateBridge", "Мост не собран.", "OK"); return; }
        var p = GateBridgeBuilder.MakePlan(fi, gb, out err);
        if (p == null) { EditorUtility.DisplayDialog("GateBridge", err, "OK"); return; }
        string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots/GateBridge");
        Directory.CreateDirectory(dir);

        bool[] was = new bool[3];
        for (int i = 0; i < 3; i++) was[i] = gb.stages[i].activeSelf;
        bool stepWas = gb.deckStep2.activeSelf, walkWas = gb.walkway.activeSelf;

        var camGo = new GameObject("GB_ShotCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 45f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.78f, 0.95f);
        try
        {
            // один ракурс: с главного берега чуть сбоку и сверху, как камера игры за спиной Лео у ворот
            Vector3 mid = p.P(p.Lb * 0.5f);
            Vector3 from = p.Pw(-5.5f) + p.T3 * 2.5f + Vector3.up * 4.2f;
            string[] names = { "1_slomen", "2_oporyi", "3_nastil", "4_gotov" };
            int[,] st = { { 1, 0 }, { 2, 1 }, { 2, 2 }, { 3, 3 } };
            for (int i = 0; i < 4; i++)
            {
                gb.Preview(st[i, 0], st[i, 1], false);
                Shot(cam, from, mid + Vector3.up * 0.5f, Path.Combine(dir, "bridge_" + names[i] + ".png"));
            }
            if (gb.noteSpot != null)
            {
                gb.Preview(3, 3, false);
                Vector3 n = gb.noteSpot.position;
                Vector3 back = -gb.noteSpot.parent.forward;
                Shot(cam, n + back * 7f + Vector3.up * 4.5f, n + Vector3.up * 1.5f, Path.Combine(dir, "platform_game_view.png"));
                // с главного берега до стройки: виден ли дальний остров и арка
                gb.Preview(1, 0, false);
                Shot(cam, p.Pw(-4f) + Vector3.up * 2.2f, n + Vector3.up * 2.5f, Path.Combine(dir, "gate_to_far_island.png"));
            }
        }
        finally
        {
            Object.DestroyImmediate(camGo);
            for (int i = 0; i < 3; i++) gb.stages[i].SetActive(was[i]);
            gb.deckStep2.SetActive(stepWas); gb.walkway.SetActive(walkWas);
        }
        EditorUtility.RevealInFinder(dir);
        Debug.Log("GateBridge: снимки в " + dir);
    }

    static void Shot(Camera cam, Vector3 pos, Vector3 look, string path)
    {
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
#endif
