#if UNITY_EDITOR
// Timber Wings — ONE-CLICK center art pack (ISL-023..033 integration).
// Inspected with Scripts(5).zip: CandyCampfire, CampfireFlicker, HutStages,
// IslandBlockout.Plaza, IslandField, DayNightCycle, FireSource / FireLightPool.
// READ ONLY audit + additive rebuild inside named roots; no world regeneration.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class ISL_CampCenterComplete
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ЦЕНТР — COMPLETE/";
        const string ArtName = "TW_CenterComplete_Art";
        const string FXName = "TW_CenterComplete_Atmosphere";
        const string Folder = "Assets/_Project/Generated/TWCenterComplete";
        const string RingPath = Folder + "/TW_CenterInlay.asset";
        const string DecoPath = Folder + "/TW_CenterBotanicals.asset";
        const string AuraPath = Folder + "/TW_CenterEmberBed.asset";
        const string Compact2Path = Folder + "/TW_CompactStage2.asset";
        const string Compact3Path = Folder + "/TW_CompactStage3.asset";
        const string CompactTripodPath = Folder + "/TW_CompactTripod.asset";
        const string CompactFlamePath = Folder + "/TW_CompactFlame.asset";
        const string SoundPath = "Assets/_Project/Audio/TW_WarmCampfireLoop.wav";
        const string ReportFile = "TW_CenterComplete_Audit.txt";

        struct Context
        {
            public Scene scene;
            public Transform campfire, stage3, plaza, sparks, smoke;
            public IslandField field;
            public MeshFilter stage2Mesh, stage3Mesh, extraTripod, extraFlame;
            public MeshRenderer duplicateKettle;
            public Material stone, camp, flame, particle;
            public AudioClip audio;
        }

        [MenuItem(Menu + "1. Проверить текущую сцену (без изменений)", false, 250)]
        static void Audit()
        {
            Context c;
            string reason;
            bool ready = GetContext(out c, out reason);
            var b = new StringBuilder();
            b.AppendLine("TIMBER WINGS / CENTER COMPLETE — READ-ONLY AUDIT");
            b.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            b.AppendLine("Unity: " + Application.unityVersion);
            b.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
            b.AppendLine("NOTE: audit creates only this external TXT report; it never changes Unity objects/assets.");
            b.AppendLine("PRECONDITIONS: " + (ready ? "OK" : "FAILED: " + reason));
            if (ready)
            {
                b.AppendLine("Campfire: " + PathOf(c.campfire));
                b.AppendLine("Plaza: " + PathOf(c.plaza));
                b.AppendLine("Existing FX: Sparks=" + (c.sparks != null) + " Smoke=" + (c.smoke != null));
                b.AppendLine("Existing plaza lights: " + (c.plaza.Find("Plaza_Lamps") != null));
                b.AppendLine("Campfire existing light: " + (c.stage3.Find("CampLight") != null));
                b.AppendLine("Existing Stone Rhythm: " + (c.plaza.Find("ISL023_StoneRhythm") != null) + " (retained)");
                b.AppendLine("Current center art add-on: " + (c.plaza.Find(ArtName) != null));
                b.AppendLine("Current atmosphere add-on: " + (c.stage3.Find(FXName) != null));
                b.AppendLine("Campfire compact mesh: " + (c.campfire.Find("Stage2/Mesh_Solid") != null ? AssetDatabase.GetAssetPath(c.campfire.Find("Stage2/Mesh_Solid").GetComponent<MeshFilter>().sharedMesh) : "unknown"));
                b.AppendLine("New effect adds ZERO realtime lights and colliders.");
                b.AppendLine("Compact transforms: only Stage2/Mesh_Solid and Stage3/Mesh_Solid MeshFilter sharedMesh may be reassigned; also optional ISL024B tripod/flame meshes. Backup references stored for removal.");
                b.AppendLine("New audio: " + SoundPath + " / " + (c.audio != null ? "found" : "MISSING; audio disabled"));
                b.AppendLine("Decor around radius >=5.5m placed only where IslandField says no path/water.");
                b.AppendLine("Command APPLY creates/replaces ONLY " + PathOf(c.plaza) + "/" + ArtName +
                             " and " + PathOf(c.stage3) + "/" + FXName);
                b.AppendLine("Generated mesh assets: " + RingPath + ", " + DecoPath + ", " + AuraPath + " and up to four private compact copies.");
            }
            if (SceneManager.GetActiveScene().path.Contains("_Recovery"))
                b.AppendLine("WARNING: CURRENT SCENE LIVES IN _Recovery. Backup/confirm the intended game scene.");
            b.AppendLine("Not a Play Mode, FPS Profiler or save-game test.");
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportFile);
            try { File.WriteAllText(path, b.ToString(), Encoding.UTF8); }
            catch (Exception ex) { Debug.LogWarning("CenterComplete: report write failed " + ex.Message); }
            Debug.Log(b + "\nReport: " + path);
        }

        [MenuItem(Menu + "2. СОБРАТЬ красивый центр (без ребилда острова)", false, 251)]
        static void Apply()
        {
            Context c;
            string error;
            if (!GetContext(out c, out error)) { Debug.LogError("CenterComplete: aborted — " + error); return; }
            string prompt = "SCENE: " + c.scene.path + "\n\n" +
                "CREATES/REPLACES EXACTLY TWO OWNED ROOTS:\n" +
                "  " + PathOf(c.plaza) + "/" + ArtName + "\n" +
                "  " + PathOf(c.stage3) + "/" + FXName + "\n\n" +
                "CREATES/UPDATES 3 ART MESHES AND UP TO 4 COMPACT MESHES:\n" +
                RingPath + "\n" + DecoPath + "\n" + AuraPath + "\n" +
                Compact2Path + "\n" + Compact3Path + "\n" + CompactTripodPath + "\n" + CompactFlamePath + "\n\n" +
                "MAY REASSIGN only Campfire Stage2/Stage3 visuals for shorter tripod/pot, and optional ISL024B overlay visuals.\n" +
                "EXISTING plaza slabs, ISL-023, paths, NPCs, save IDs, colliders, materials, lanterns, lights, camera unchanged.\n" +
                "Adds a tiny 3D fire ambience loop and light nighttime motes to Stage3 only.\n" +
                "No scene/terrain/campfire regeneration.\n\n" +
                "MAKE A BACKUP / GIT CHECKPOINT BEFORE APPLY. YOU WILL NEED TO SAVE THE SCENE.";
            if (c.scene.path.Contains("_Recovery"))
                prompt = "WARNING: ACTIVE SCENE IS IN _Recovery. Confirm this is the scene you intend to save!\n\n" + prompt;
            if (!EditorUtility.DisplayDialog("TW Center Complete — precise changes", prompt, "Build THIS center", "Cancel")) return;

            Mesh inlay = null, decor = null, aura = null;
            Mesh short2 = null, short3 = null, shortTripod = null, shortFlame = null;
            try
            {
                inlay = MakeInlay(c.field, c.campfire.position);
                decor = MakeDecor(c.field, c.campfire.position);
                aura = MakeEmberBed();
                if (!Valid(inlay) || !Valid(decor) || !Valid(aura))
                    throw new InvalidOperationException("One of the art meshes is empty.");
                // Reference originals from previous Complete build when reapplied, or use current meshes.
                var oldStateRoot = c.stage3.Find(FXName);
                var oldState = oldStateRoot != null ? oldStateRoot.GetComponent<TWCampCenterRestoreState>() : null;
                Mesh src2 = oldState != null && oldState.priorStage2 != null ? oldState.priorStage2 : c.stage2Mesh.sharedMesh;
                Mesh src3 = oldState != null && oldState.priorStage3 != null ? oldState.priorStage3 : c.stage3Mesh.sharedMesh;
                Mesh srcT = oldState != null && oldState.priorTripod != null ? oldState.priorTripod : (c.extraTripod != null ? c.extraTripod.sharedMesh : null);
                Mesh srcF = oldState != null && oldState.priorFlame != null ? oldState.priorFlame : (c.extraFlame != null ? c.extraFlame.sharedMesh : null);
                bool wasKettleEnabled = oldState != null ? oldState.duplicateKettleWasEnabled : (c.duplicateKettle != null && c.duplicateKettle.enabled);
                // Do not compact twice: preexisting ISL024C compact assets are already correct.
                bool precompact = AssetDatabase.GetAssetPath(src2).Contains("ISL024C_");
                if (!precompact)
                {
                    short2 = Compact(src2, false, false);
                    short3 = Compact(src3, false, true);
                    if (srcT != null) shortTripod = Compact(srcT, true, false);
                    if (srcF != null) shortFlame = CompactFlames(srcF);
                }
                EnsureFolder();
                Mesh inlayAsset = PersistMesh(RingPath, inlay);
                Mesh decorAsset = PersistMesh(DecoPath, decor);
                Mesh auraAsset = PersistMesh(AuraPath, aura);
                AssetDatabase.SaveAssetIfDirty(inlayAsset);
                AssetDatabase.SaveAssetIfDirty(decorAsset);
                AssetDatabase.SaveAssetIfDirty(auraAsset);
                Mesh s2Asset = short2 != null ? PersistMesh(Compact2Path, short2) : null;
                Mesh s3Asset = short3 != null ? PersistMesh(Compact3Path, short3) : null;
                Mesh triAsset = shortTripod != null ? PersistMesh(CompactTripodPath, shortTripod) : null;
                Mesh flameAsset = shortFlame != null ? PersistMesh(CompactFlamePath, shortFlame) : null;
                if(s2Asset!=null) AssetDatabase.SaveAssetIfDirty(s2Asset);
                if(s3Asset!=null) AssetDatabase.SaveAssetIfDirty(s3Asset);
                if(triAsset!=null) AssetDatabase.SaveAssetIfDirty(triAsset);
                if(flameAsset!=null) AssetDatabase.SaveAssetIfDirty(flameAsset);

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("TW Center Complete: one coherent art pass");
                try
                {
                    RemoveOwned(c.plaza, ArtName);
                    RemoveOwned(c.stage3, FXName);

                    Transform art = NewChild(c.plaza, ArtName);
                    art.position = c.campfire.position;
                    MeshChild(art, "01_HandlaidStoneInlay", inlayAsset, c.stone, true);
                    MeshChild(art, "02_LowGardenAccents", decorAsset, c.stone, true);

                    Transform fx = NewChild(c.stage3, FXName);
                    MeshChild(fx, "01_WarmEmbersAtGround", auraAsset, c.flame, false);
                    var audioObj = NewChild(fx, "02_CracklingFire_3D");
                    audioObj.localPosition = new Vector3(0f, 0.35f, 0f);
                    AudioSource aud = Undo.AddComponent<AudioSource>(audioObj.gameObject);
                    aud.clip = c.audio;
                    aud.loop = true;
                    aud.playOnAwake = true;
                    aud.spatialBlend = 1f;
                    aud.rolloffMode = AudioRolloffMode.Logarithmic;
                    aud.minDistance = 1.8f;
                    aud.maxDistance = 12f;
                    aud.volume = 0.17f;
                    aud.dopplerLevel = 0f;

                    ParticleSystem motes = null;
                    if (c.particle != null)
                    {
                        var motesObj = NewChild(fx, "03_RareAmberMotes");
                        motesObj.localPosition = new Vector3(0f, 0.70f, 0f);
                        motes = Undo.AddComponent<ParticleSystem>(motesObj.gameObject);
                        ConfigureMotes(motes, c.particle);
                    }
                    TWCampCenterAtmosphere driver = Undo.AddComponent<TWCampCenterAtmosphere>(fx.gameObject);
                    driver.Configure(aud, motes);
                    EditorUtility.SetDirty(driver);
                    var restore = Undo.AddComponent<TWCampCenterRestoreState>(fx.gameObject);
                    restore.stage2 = c.stage2Mesh; restore.stage3 = c.stage3Mesh;
                    restore.tripod = c.extraTripod; restore.flame = c.extraFlame;
                    restore.priorStage2 = src2; restore.priorStage3 = src3;
                    restore.priorTripod = srcT; restore.priorFlame = srcF;
                    restore.duplicateKettle = c.duplicateKettle;
                    restore.duplicateKettleWasEnabled = wasKettleEnabled;
                    EditorUtility.SetDirty(restore);
                    if (s2Asset != null) SwapMesh(c.stage2Mesh, s2Asset);
                    if (s3Asset != null) SwapMesh(c.stage3Mesh, s3Asset);
                    if (triAsset != null && c.extraTripod != null) SwapMesh(c.extraTripod, triAsset);
                    if (flameAsset != null && c.extraFlame != null) SwapMesh(c.extraFlame, flameAsset);
                    if (c.duplicateKettle != null)
                    {
                        Undo.RecordObject(c.duplicateKettle,"TW Center: avoid doubled kettle");
                        c.duplicateKettle.enabled = false;
                        EditorUtility.SetDirty(c.duplicateKettle);
                    }

                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(c.scene);
                    Selection.activeGameObject = art.gameObject;
                    Debug.Log("CenterComplete: cohesive center complete. Only existing Stage2/Stage3 MeshFilter REFERENCES may be swapped to compact copies; original mesh assets, NPCs, lights, colliders and saves remain untouched. " +
                              "Audio=" + (c.audio != null ? "ready" : "not found") + "; particle material=" + (c.particle != null ? "ready" : "absent") + ". " +
                              "Test 4 times of day in Game View. No Unity Play Mode or FPS Profiler test run by the package author.");
                }
                catch (Exception ex)
                {
                    Undo.RevertAllDownToGroup(group);
                    Debug.LogError("CenterComplete scene operation failed / reverted: " + ex);
                }
            }
            catch (Exception ex) { Debug.LogError("CenterComplete mesh prebuild failed; existing scene not changed: " + ex); }
            finally
            {
                if (inlay != null) UnityEngine.Object.DestroyImmediate(inlay);
                if (decor != null) UnityEngine.Object.DestroyImmediate(decor);
                if (aura != null) UnityEngine.Object.DestroyImmediate(aura);
                if (short2 != null) UnityEngine.Object.DestroyImmediate(short2);
                if (short3 != null) UnityEngine.Object.DestroyImmediate(short3);
                if (shortTripod != null) UnityEngine.Object.DestroyImmediate(shortTripod);
                if (shortFlame != null) UnityEngine.Object.DestroyImmediate(shortFlame);
            }
        }

        [MenuItem(Menu + "3. ОТМЕНИТЬ только Complete-декор", false, 252)]
        static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play Mode first."); return; }
            var scene = SceneManager.GetActiveScene();
            var plaza = FindByPath(scene, "IslandV2/_Generated/CampPlaza");
            var fire = FindUniqueRoot(scene, "Campfire");
            var stage3 = fire != null ? fire.Find("Stage3") : null;
            if (plaza == null || stage3 == null) { Debug.LogError("CenterComplete roots absent; no deletion."); return; }
            Transform a = plaza.Find(ArtName), b = stage3.Find(FXName);
            if (a == null && b == null) { Debug.Log("CenterComplete: no overlays exist."); return; }
            if (!EditorUtility.DisplayDialog("Remove ONLY Complete overlays",
                "DELETE ONLY:\n" + PathOf(plaza) + "/" + ArtName + "\n" + PathOf(stage3) + "/" + FXName +
                "\n\nGenerated mesh assets and WAV remain for undo/reapplication. Original center unaffected.", "Remove", "Cancel")) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("TW Center Complete: remove visual additions");
            if (b != null)
            {
                var record = b.GetComponent<TWCampCenterRestoreState>();
                if (record != null)
                {
                    if (record.stage2 != null && record.priorStage2 != null) SwapMesh(record.stage2, record.priorStage2);
                    if (record.stage3 != null && record.priorStage3 != null) SwapMesh(record.stage3, record.priorStage3);
                    if (record.tripod != null && record.priorTripod != null) SwapMesh(record.tripod, record.priorTripod);
                    if (record.flame != null && record.priorFlame != null) SwapMesh(record.flame, record.priorFlame);
                    if (record.duplicateKettle != null)
                    {
                        Undo.RecordObject(record.duplicateKettle,"TW Center: restore duplicate kettle state");
                        record.duplicateKettle.enabled = record.duplicateKettleWasEnabled;
                    }
                }
            }
            if (a != null) Undo.DestroyObjectImmediate(a.gameObject);
            if (b != null) Undo.DestroyObjectImmediate(b.gameObject);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CenterComplete: removed only two owned roots; original camp retained.");
        }

        static bool GetContext(out Context c, out string error)
        {
            c = new Context(); error = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { error = "Exit Play Mode."; return false; }
            c.scene = SceneManager.GetActiveScene();
            if (!c.scene.IsValid() || !c.scene.isLoaded || string.IsNullOrEmpty(c.scene.path))
            { error = "Save/open the intended game scene first."; return false; }
            c.campfire = FindUniqueRoot(c.scene, "Campfire");
            c.plaza = FindByPath(c.scene, "IslandV2/_Generated/CampPlaza");
            if (c.campfire == null || c.plaza == null)
            { error = "Need unique Campfire scene root and IslandV2/_Generated/CampPlaza."; return false; }
            if (c.campfire.GetComponent<HutStages>() == null)
            { error = "Campfire does not use expected HutStages; no changes."; return false; }
            if (c.campfire.Find("Stage1") == null || c.campfire.Find("Stage2") == null)
            { error = "Expected Stage1 / Stage2 under Campfire."; return false; }
            c.stage3 = c.campfire.Find("Stage3");
            var s2 = c.campfire.Find("Stage2/Mesh_Solid");
            var s3 = c.campfire.Find("Stage3/Mesh_Solid");
            c.stage2Mesh = s2 != null ? s2.GetComponent<MeshFilter>() : null;
            c.stage3Mesh = s3 != null ? s3.GetComponent<MeshFilter>() : null;
            if (c.stage2Mesh == null || c.stage3Mesh == null || c.stage2Mesh.sharedMesh == null || c.stage3Mesh.sharedMesh == null)
            { error = "Missing Stage2/Stage3 base mesh. Refusing camp compacting."; return false; }
            var tri = c.campfire.Find("Stage2/ISL024B_TripodHero");
            var extra = c.campfire.Find("Stage3/Flames/ISL024B_FlameHero");
            var kettle = c.campfire.Find("Stage2/ISL024B_Kettle");
            c.extraTripod = tri != null ? tri.GetComponent<MeshFilter>() : null;
            c.extraFlame = extra != null ? extra.GetComponent<MeshFilter>() : null;
            c.duplicateKettle = kettle != null ? kettle.GetComponent<MeshRenderer>() : null;
            if (c.stage3 == null || c.stage3.Find("Flames") == null || c.stage3.GetComponent<CampfireFlicker>() == null)
            { error = "Expected CandyCampfire Stage3 with Flames + CampfireFlicker."; return false; }
            if (c.plaza.Find("Plaza_Mesh") == null)
            { error = "CampPlaza/Plaza_Mesh missing."; return false; }
            MeshRenderer plaR = c.plaza.Find("Plaza_Mesh").GetComponent<MeshRenderer>();
            if (plaR == null || plaR.sharedMaterial == null)
            { error = "Plaza material missing."; return false; }
            c.stone = plaR.sharedMaterial;
            Transform f = c.stage3.Find("Flames/Mesh_Flame");
            var fr = f != null ? f.GetComponent<MeshRenderer>() : null;
            c.flame = fr != null ? fr.sharedMaterial : null;
            if (c.flame == null) { error = "Campfire flame material missing."; return false; }
            c.sparks = c.stage3.Find("Sparks");
            c.smoke = c.stage3.Find("Smoke");
            if (c.sparks == null || c.smoke == null)
            { error = "Expected existing Sparks + Smoke effects; stop before adding duplicate FX."; return false; }
            ParticleSystemRenderer pr = c.sparks.GetComponent<ParticleSystemRenderer>();
            c.particle = pr != null ? pr.sharedMaterial : null;
            c.audio = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
            // Existing scenes may have offset center. The build uses the campfire center in world space.
            if (Vector2.Distance(new Vector2(c.campfire.position.x,c.campfire.position.z), Vector2.zero) > 0.5f)
            { error = "Campfire farther than 2.5m from fixed Island PlazaCenter; verify geometry before building."; return false; }
            var layout = AssetDatabase.LoadAssetAtPath<IslandLayout>("Assets/_Project/IslandV2/IslandLayout.asset");
            if (layout == null) { error = "IslandLayout.asset missing; refuse placement near paths."; return false; }
            c.field = new IslandField(layout);
            return true;
        }

        static Transform FindUniqueRoot(Scene scene, string name)
        {
            Transform got = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name != name) continue;
                if (got != null) return null;
                got = go.transform;
            }
            return got;
        }
        static Transform FindByPath(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            Transform root = FindUniqueRoot(scene, parts[0]);
            for (int i=1;i<parts.Length && root != null;i++) root=root.Find(parts[i]);
            return root;
        }
        static string PathOf(Transform t)
        {
            var names = new List<string>();
            while (t != null) { names.Add(t.name); t=t.parent; }
            names.Reverse();return string.Join("/",names.ToArray());
        }
        static void SwapMesh(MeshFilter target, Mesh replacement)
        {
            if (target == null || replacement == null) return;
            Undo.RecordObject(target,"TW Center: swap only visual mesh reference");
            target.sharedMesh = replacement;
            EditorUtility.SetDirty(target);
        }
        static Mesh Compact(Mesh source, bool overlay, bool lowerPot)
        {
            var m = UnityEngine.Object.Instantiate(source);
            var verts = m.vertices;
            for (int i=0;i<verts.Length;i++)
            {
                Vector3 v = verts[i];
                if (lowerPot)
                {
                    if(v.x*v.x+v.z*v.z < 0.55f*0.55f && v.y >= 1.15f)
                    {
                        float alpha = Mathf.SmoothStep(0f,1f,Mathf.Clamp01((v.y-1.15f)/0.25f));
                        v.y-=0.38f*alpha;
                    }
                }
                else
                {
                    float r2 = v.x*v.x+v.z*v.z;
                    if(r2<1.75f*1.75f && v.y>0.95f)
                    {
                        float oldApex = overlay ? 2.82f : 2.75f;
                        float newApex = overlay ? 2.40f : 2.35f;
                        v.y=0.95f+(v.y-0.95f)*(newApex-0.95f)/(oldApex-0.95f);
                    }
                }
                verts[i]=v;
            }
            m.vertices=verts;m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        static Mesh CompactFlames(Mesh source)
        {
            var m = UnityEngine.Object.Instantiate(source);
            var verts=m.vertices;
            for(int i=0;i<verts.Length;i++)
            {
                Vector3 v=verts[i];
                if(v.y>0f)v.y*=0.88f;
                verts[i]=v;
            }
            m.vertices=verts;m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        static void RemoveOwned(Transform p, string name)
        {
            var old = p.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        static Transform NewChild(Transform p, string name)
        {
            var go=new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go,"TW Center: " + name);
            go.transform.SetParent(p,false);
            return go.transform;
        }
        static void MeshChild(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
        {
            if (mesh==null||mat==null) throw new InvalidOperationException("Missing mesh/material: " + name);
            Transform tr=NewChild(parent,name);
            var mf=Undo.AddComponent<MeshFilter>(tr.gameObject); mf.sharedMesh=mesh;
            var mr=Undo.AddComponent<MeshRenderer>(tr.gameObject); mr.sharedMaterial=mat;
            mr.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;
            mr.receiveShadows=shadows;
        }
        static bool Valid(Mesh m) { return m != null && m.vertexCount >= 3 && m.triangles.Length >= 3; }
        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Generated"))
                AssetDatabase.CreateFolder("Assets/_Project","Generated");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Generated","TWCenterComplete");
        }
        static Mesh PersistMesh(string path, Mesh candidate)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing!=null)
            {
                Undo.RecordObject(existing,"TW Center: update generated mesh");
                EditorUtility.CopySerialized(candidate,existing);
                existing.name=Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path)!=null)
                throw new InvalidOperationException("Non-Mesh asset already exists at " + path);
            var copy=UnityEngine.Object.Instantiate(candidate);
            copy.name=Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy,path);
            return copy;
        }
        static Color Col(string h)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(h,out c)?c.linear:Color.white;
        }
        static Mesh MakeInlay(IslandField field, Vector3 origin)
        {
            var m=new MeshInk();
            Color cream=Col("#FFF1D1"), softGold=Col("#EED1A0"), warm=Col("#F3DCBA"), mint=Col("#AFD6B8");
            // Four calm handlaid crescent arcs at plaza midring. Each has clear path gaps.
            for(int arm=0;arm<4;arm++)
            {
                float midpoint=(arm*90f+45f)*Mathf.Deg2Rad;
                for(int j=0;j<10;j++)
                {
                    float a0=midpoint+Mathf.Lerp(-0.32f,0.32f,j/10f);
                    float a1=midpoint+Mathf.Lerp(-0.32f,0.32f,(j+1)/10f);
                    float am=(a0+a1)*0.5f;
                    float r=3.77f;
                    if(field.PathAxisDistance(origin.x+Mathf.Cos(am)*r,origin.z+Mathf.Sin(am)*r)<1.9f) continue;
                    m.ArcStroke(r-0.040f,r+0.040f,a0,a1,0.138f,(j%3==0)?softGold:cream);
                }
            }
            // Handmade pebble diamonds on inner ring, sparse and never across a path.
            for(int i=0;i<16;i++)
            {
                float a=(i*22.5f+11.25f)*Mathf.Deg2Rad;
                float r=2.30f;
                if(field.PathAxisDistance(origin.x+Mathf.Cos(a)*r,origin.z+Mathf.Sin(a)*r)<1.6f) continue;
                float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r;
                m.Diamond(new Vector3(x,0.145f,z),0.07f,i%4==0?mint:warm);
            }
            return m.Export("TW_CenterInlay");
        }
        static Mesh MakeDecor(IslandField field, Vector3 origin)
        {
            var m=new MeshInk();
            Color moss=Col("#72A96B"), leaf=Col("#9ACD83"), wood=Col("#C69B70"), lid=Col("#F5D6A5");
            Color stone=Col("#E4D9DA"), blossom=Col("#F9B9C5");
            int count=0;
            for(int i=0;i<24 && count<4;i++)
            {
                float a=(24f + i*47f)*Mathf.Deg2Rad;
                float r=5.65f+((i%3)-1)*0.19f;
                float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r;
                if(!field.Inside(x+origin.x,z+origin.z)||field.IsWater(x+origin.x,z+origin.z)||field.PathAxisDistance(x+origin.x,z+origin.z)<2.65f)continue;
                float y=field.Height(x+origin.x,z+origin.z)-origin.y;
                Vector3 p=new Vector3(x,y+0.04f,z);
                // All decoration outside 5.4m. Low props, no new colliders.
                m.Dodeca(p+Vector3.up*0.08f,0.23f,0.12f,stone);
                Vector3 q=p+new Vector3(0.22f,0f,0.12f);
                m.Tuft(q,0.34f,moss,leaf);
                for(int j=0;j<3;j++)
                {
                    float b=a+j*2.1f;
                    Vector3 f=p+new Vector3(Mathf.Cos(b)*0.32f,0f,Mathf.Sin(b)*0.32f);
                    m.Tuft(f,0.20f,moss,leaf);
                    m.Dodeca(f+Vector3.up*0.19f,0.075f,0.065f,j==0?lid:blossom);
                }
                if(count==1 || count==3)
                {
                    Vector3 box=p+new Vector3(-0.32f,0.07f,-0.12f);
                    m.Crate(box,wood,lid);
                }
                count++;
            }
            if(count==0)
            {
                // Fallback creates a tiny benign inner ring marker so the mesh remains valid.
                m.Diamond(new Vector3(2.0f,0.16f,2.0f),0.025f,stone);
            }
            return m.Export("TW_CenterBotanicals");
        }
        static Mesh MakeEmberBed()
        {
            var m=new MeshInk();
            Color orange=Col("#FF9E58"), gold=Col("#FFD28A"), pearl=Col("#FFF0C4");
            for(int i=0;i<12;i++)
            {
                float a=(i*30f+13f)*Mathf.Deg2Rad;
                float r=0.56f+(i%3)*0.11f;
                Vector3 p=new Vector3(Mathf.Cos(a)*r,0.095f,Mathf.Sin(a)*r);
                m.Dodeca(p,0.040f,0.03f,i%3==0?pearl:(i%3==1?orange:gold));
            }
            return m.Export("TW_EmberBed");
        }

        static void ConfigureMotes(ParticleSystem ps, Material particleMaterial)
        {
            var main=ps.main;
            main.loop=true;
            main.playOnAwake=true;
            main.duration=5f;
            main.startLifetime=new ParticleSystem.MinMaxCurve(1.0f,2.5f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(0.22f,0.55f);
            main.startSize=new ParticleSystem.MinMaxCurve(0.04f,0.08f);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(1f,0.85f,0.48f,0.6f),new Color(1f,0.72f,0.52f,0.5f));
            main.gravityModifier=0f;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.maxParticles=20;
            var em=ps.emission;
            em.enabled=true;
            em.rateOverTime=2.8f;
            var shape=ps.shape;
            shape.enabled=true;
            shape.shapeType=ParticleSystemShapeType.Cone;
            shape.radius=0.30f;
            shape.angle=18f;
            var fade=ps.colorOverLifetime;
            fade.enabled=true;
            var grad=new Gradient();
            grad.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
                new[]{new GradientAlphaKey(0f,0f),new GradientAlphaKey(0.68f,0.2f),new GradientAlphaKey(0.48f,0.8f),new GradientAlphaKey(0f,1f)});
            fade.color=new ParticleSystem.MinMaxGradient(grad);
            var pr=ps.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial=particleMaterial;
            pr.renderMode=ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode=ShadowCastingMode.Off;
            pr.receiveShadows=false;
        }

        // Lightweight faceted procedural mesh builder. Vertex colors use the scene's existing materials.
        sealed class MeshInk
        {
            readonly List<Vector3> verts=new List<Vector3>();
            readonly List<int> tris=new List<int>();
            readonly List<Color> colors=new List<Color>();
            int V(Vector3 p,Color c){int i=verts.Count;verts.Add(p);colors.Add(c);return i;}
            void T(int a,int b,int c){tris.Add(a);tris.Add(b);tris.Add(c);}
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color co)
            {int i=V(a,co),j=V(b,co),k=V(c,co),l=V(d,co);T(i,j,k);T(i,k,l); T(k,j,i); T(l,k,i);}
            public void ArcStroke(float r0,float r1,float a0,float a1,float y,Color co)
            {
                Vector3 a=new Vector3(Mathf.Cos(a0)*r0,y,Mathf.Sin(a0)*r0);
                Vector3 b=new Vector3(Mathf.Cos(a0)*r1,y,Mathf.Sin(a0)*r1);
                Vector3 c=new Vector3(Mathf.Cos(a1)*r1,y,Mathf.Sin(a1)*r1);
                Vector3 d=new Vector3(Mathf.Cos(a1)*r0,y,Mathf.Sin(a1)*r0);
                Quad(a,b,c,d,co);
            }
            public void Diamond(Vector3 p,float size,Color c)
            {
                Vector3 a=p+new Vector3(0f,0f,size),b=p+new Vector3(size,0f,0f),d=p+new Vector3(-size,0f,0f),e=p+new Vector3(0f,0f,-size);
                Quad(a,b,e,d,c);
            }
            public void Dodeca(Vector3 p,float radius,float height,Color c)
            {
                const int n=8;
                int top=V(p+Vector3.up*height,c),bottom=V(p-Vector3.up*(height*0.35f),c*0.83f);
                int[] a=new int[n];
                for(int i=0;i<n;i++)
                {
                    float t=i*Mathf.PI*2f/n;
                    a[i]=V(p+new Vector3(Mathf.Cos(t)*radius,0f,Mathf.Sin(t)*radius),c);
                }
                for(int i=0;i<n;i++) {int j=(i+1)%n;T(top,a[i],a[j]);T(bottom,a[j],a[i]);}
            }
            public void Tuft(Vector3 p,float height,Color dark,Color light)
            {
                for(int i=0;i<5;i++)
                {
                    float a=i*Mathf.PI*2f/5f;
                    Vector3 o=new Vector3(Mathf.Cos(a)*0.13f,0f,Mathf.Sin(a)*0.13f);
                    int a0=V(p+o,dark),b0=V(p-o*0.25f,light),top=V(p+o*0.30f+Vector3.up*height,light);
                    T(a0,b0,top);T(top,b0,a0);
                }
            }
            public void Crate(Vector3 p,Color wood,Color cream)
            {
                Vector3 d=new Vector3(0.17f,0.13f,0.17f);
                Quad(p+new Vector3(-d.x,d.y,-d.z),p+new Vector3(d.x,d.y,-d.z),p+new Vector3(d.x,d.y,d.z),p+new Vector3(-d.x,d.y,d.z),cream);
                Quad(p+new Vector3(-d.x,0f,-d.z),p+new Vector3(d.x,0f,-d.z),p+new Vector3(d.x,d.y,-d.z),p+new Vector3(-d.x,d.y,-d.z),wood);
                Quad(p+new Vector3(d.x,0f,d.z),p+new Vector3(-d.x,0f,d.z),p+new Vector3(-d.x,d.y,d.z),p+new Vector3(d.x,d.y,d.z),wood);
                Quad(p+new Vector3(-d.x,0f,d.z),p+new Vector3(-d.x,0f,-d.z),p+new Vector3(-d.x,d.y,-d.z),p+new Vector3(-d.x,d.y,d.z),wood);
                Quad(p+new Vector3(d.x,0f,-d.z),p+new Vector3(d.x,0f,d.z),p+new Vector3(d.x,d.y,d.z),p+new Vector3(d.x,d.y,-d.z),wood);
            }
            public Mesh Export(string name)
            {
                var m=new Mesh{name=name};m.SetVertices(verts);m.SetColors(colors);m.SetTriangles(tris,0);
                m.RecalculateNormals();m.RecalculateBounds();return m;
            }
        }
    }
}
#endif
