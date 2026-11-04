// Timber Wings / ISL-004 / v0.2 — non-mutating release-gate inspection.
// This inspection is NOT evidence of Play Mode, Windows build performance or save compatibility.
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class TWIslandReleaseGateV2
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISLAND RELEASE ART v0.2/";
        [MenuItem(Menu + "13 · RELEASE GATE / Проверка сцены", false, 223)]
        public static void Audit()
        {
            var scene=SceneManager.GetActiveScene();
            var root=IslandBlockout.FindRoot();
            var sb=new StringBuilder();
            int hard=0, warnings=0;
            Action<string> fail = msg => { hard++;sb.AppendLine("BLOCK: "+msg); };
            Action<string> warn = msg => { warnings++;sb.AppendLine("REVIEW: "+msg); };
            Action<string> pass = msg => sb.AppendLine("INFO: "+msg);
            sb.AppendLine("TIMBER WINGS / ISLAND RELEASE GATE v0.2 / "+DateTime.UtcNow.ToString("O"));
            pass("Unity="+Application.unityVersion+"; scene="+scene.path);
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) fail("Active scene has no saved .unity asset");
            if (scene.name!="Island_v2") fail("Active scene is not Island_v2 (possible _Recovery scene)");
            if (scene.isDirty) fail("Active scene has unsaved changes");
            if (Application.isPlaying) warn("Run this edit-mode check outside Play Mode to inspect authored scene");
            if (root==null || root.scene!=scene) fail("IslandV2 root is missing in active scene");
            else
            {
                Transform gen=root.transform.Find(IslandBlockout.GeneratedName);
                Transform man=root.transform.Find(IslandBlockout.ManualName);
                if (gen==null || man==null) fail("_Generated / _Manual hierarchy is incomplete");
                else
                {
                    foreach (string name in new[] {IslandCliff.ObjectName, IslandRiverWater.ObjectName,
                                                     IslandRiverWater.FallObjectName, IslandBlockout.BridgeName})
                        if (gen.Find(name)==null) fail("Missing native generated object "+name);
                    foreach(string name in new[]{"TW_RIVER_OVERHAUL_V1","TW_RIVER_OVERHAUL_V2","TW_RIVER_OVERHAUL_V3"})
                    {
                        Transform t=man.Find(name);
                        if (t!=null && t.gameObject.activeInHierarchy)
                            warn("Active River Overhaul overlay: "+name+". Confirm no duplicate waterfall sheets.");
                    }
                    int releaseCount=0;
                    foreach(string name in new[]{"TW_ISLAND_RELEASE_ART_V1","TW_ISLAND_RELEASE_ART_V2"})
                    {
                        Transform t=man.Find(name);
                        if(t!=null && t.gameObject.activeInHierarchy) releaseCount++;
                    }
                    if(releaseCount>1) fail("Multiple active island-release overlays");
                    if(man.Find("TW_ISLAND_RELEASE_ART_V2")==null) warn("v0.2 art not applied to this scene");
                }
                var colliders=root.GetComponentsInChildren<Collider>(true);
                var terrains=root.GetComponentsInChildren<TerrainCollider>(true);
                var lights=root.GetComponentsInChildren<Light>(true);
                var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
                pass("Island Colliders="+colliders.Length+"; TerrainColliders="+terrains.Length+
                     "; Lights="+lights.Length+"; MeshRenderers="+renderers.Length);
                if(terrains.Length==0) fail("No TerrainCollider attached to IslandV2");
                if(gen!=null)
                {
                    var br=gen.Find(IslandBlockout.BridgeName);
                    int bridgeColliders=br==null?0:br.GetComponentsInChildren<Collider>(true).Length;
                    pass("Native river bridge colliders="+bridgeColliders);
                    if(bridgeColliders==0) fail("River bridge colliders are missing");
                }
                int missingScripts=0;
                foreach(var tr in root.GetComponentsInChildren<Transform>(true))
                    missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject);
                if(missingScripts>0) fail("Missing scripts within IslandV2="+missingScripts);
                else pass("Missing MonoBehaviour scripts within IslandV2=0");
            }
            var layout=AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if(layout==null) fail("Missing IslandLayout.asset");
            else
            {
                pass("Anchors="+(layout.anchors==null?0:layout.anchors.Count)+
                     "; Paths="+(layout.paths==null?0:layout.paths.Count)+
                     "; River points="+(layout.riverPoints==null?0:layout.riverPoints.Count));
                if(layout.riverPoints==null || layout.riverPoints.Count<2) fail("Layout river geometry missing");
            }
            string activeScene=scene.path;
            var enabledScenes=EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>x.path).ToList();
            pass("EditorBuildSettings enabled scenes="+enabledScenes.Count+
                 "; active scene listed="+enabledScenes.Contains(activeScene));
            if(!enabledScenes.Contains(activeScene)) warn("Active scene is not enabled in EditorBuildSettings; check Unity 6 Build Profiles explicitly");
            if(Shader.Find("Project/IslandV2/TW_ReleaseVertexLit")==null) fail("Release art shader not found");
            if(Shader.Find("Project/CandyToon")!=null) pass("CandyToon shader found (compilation status NOT verified)");
            pass("NOT VERIFIED: real shader compiler output, day/night, controls, navigation, save migration, Windows build FPS, RAM and license audit");
            pass("GDD targets (not measurements): recommended PC ~60 FPS 1080p; weak PC ~30 FPS 720p");
            sb.AppendLine("RESULT: "+(hard==0?"STRUCTURAL CHECK COMPLETE":"BLOCKED")+
                          "; structural blocks="+hard+"; review warnings="+warnings);
            string project=Directory.GetParent(Application.dataPath).FullName;
            string dir=Path.Combine(project,"TW_IslandRelease_Reports");Directory.CreateDirectory(dir);
            string file=Path.Combine(dir,"ReleaseGate_v0_2.txt");
            File.WriteAllText(file,sb.ToString(),Encoding.UTF8);
            if(hard>0) Debug.LogError("TW RELEASE GATE v0.2: structural blocks="+hard+". "+file+"\n"+sb);
            else Debug.Log("TW RELEASE GATE v0.2: structural check completed; manual testing remains mandatory. "+file+"\n"+sb);
        }
    }
}
