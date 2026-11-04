// Timber Wings / release art v0.2 — repeatable Scene View framing helpers.
// Camera is an EDITOR SCENE VIEW only. No main game camera edits, no scene mutations.
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class TWIslandReleaseViewsV2
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISLAND RELEASE ART v0.2/";
        [MenuItem(Menu + "9 · FRAME / Остров целиком", false, 219)]
        static void Whole()
        {
            if (!Check()) return;
            var l = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (l == null) return;
            var f = new IslandField(l);
            float spread = Mathf.Max(f.Max.x-f.Min.x, f.Max.y-f.Min.y);
            Look(new Vector3(l.centerOffset.x, 0f, l.centerOffset.y), Mathf.Max(80f,spread * 0.86f), 54f, 18f);
        }
        [MenuItem(Menu + "10 · FRAME / Центральный костёр", false, 220)]
        static void Camp() { FrameAnchor("CAMPFIRE_Point",24f,51f,26f); }
        [MenuItem(Menu + "11 · FRAME / Рынок", false, 221)]
        static void Market() { FrameAnchor("PAD_Market",29f,55f,-34f); }
        [MenuItem(Menu + "12 · FRAME / Пруд", false, 222)]
        static void Pond()
        {
            if (!Check()) return;
            var l=AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (l==null) return;
            Look(new Vector3(l.pondPos.x,0,l.pondPos.y),31f,50f,20f);
        }
        static void FrameAnchor(string id,float size,float pitch,float yaw)
        {
            if (!Check()) return;
            var l=AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (l==null) return;
            var anchor=l.FindAnchor(id);
            if (anchor==null) { Debug.LogWarning("TW Island Release: отсутствует якорь " + id); return; }
            var pos=new IslandField(l).AnchorWorld(anchor);
            Look(new Vector3(pos.x,0,pos.y),size,pitch,yaw);
        }
        static bool Check()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().name!="Island_v2")
            {
                Debug.LogWarning("TW Island Release: открой сохранённую Island_v2 вне Play Mode.");
                return false;
            }
            return true;
        }
        static void Look(Vector3 pivot,float size,float pitch,float yaw)
        {
            SceneView sceneView=SceneView.lastActiveSceneView;
            if (sceneView==null) return;
            sceneView.LookAt(pivot,Quaternion.Euler(pitch,yaw,0f),size);
            sceneView.Repaint();
        }
    }
}
