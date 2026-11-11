using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IslandV2
{
    internal static class TWMysticEffects
    {
        public static void Build(TWMysticContext x,TWMysticAssets a)
        {
            var go=new GameObject("09_Атмосфера_и_ночные_следы");go.transform.SetParent(x.root,false);
            var vibe=go.AddComponent<MysticAtmosphere>();vibe.radius=x.zoneRadius;vibe.feather=5;
            vibe.dayVolume=Profile(go.transform,a.folder,"Day",false);vibe.nightVolume=Profile(go.transform,a.folder,"Night",true);
            var camera=x.elder.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c=>c.CompareTag("MainCamera"));
            if(camera!=null)
            {
                var data=camera.GetComponent<UniversalAdditionalCameraData>();
                if(data==null){data=Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);x.state.createdCameraData=true;}
                if(data!=null)
                {
                    if(!x.state.adjustedCamera){x.state.effectCamera=camera;x.state.originalPostProcessing=data.renderPostProcessing;x.state.originalVolumeMask=data.volumeLayerMask.value;x.state.adjustedCamera=true;}
                    Undo.RecordObject(data,"Enable authored local atmosphere");data.renderPostProcessing=true;data.volumeLayerMask=data.volumeLayerMask.value | (1<<go.layer);
                }
            }
            var texture = SoftTexture(a.folder, "Soft", false);
            var particleMat = new Material(a.veil) { name = "Mystic_Particles" };
            particleMat.SetTexture("_BaseMap", texture);
            particleMat.SetFloat("_Alpha", 0.65f);
            AssetDatabase.CreateAsset(particleMat, a.folder + "/Particles.mat");
            vibe.particles = new[]
            {
                Particles(go.transform, particleMat, 0),
                Particles(go.transform, particleMat, 1),
                Particles(go.transform, particleMat, 2)
            };

            var fireflyMat = new Material(a.veil) { name = "Mystic_Fireflies" };
            fireflyMat.SetTexture("_BaseMap", SoftTexture(a.folder, "Firefly", true));
            fireflyMat.SetFloat("_Alpha", 0.94f);
            fireflyMat.SetFloat("_Intensity", 2.25f);
            AssetDatabase.CreateAsset(fireflyMat, a.folder + "/Fireflies.mat");

            vibe.runes = x.root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.StartsWith("Rune_", StringComparison.Ordinal)).ToArray();
            vibe.apparitions = x.root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.StartsWith("Apparition_", StringComparison.Ordinal)).ToArray();
            var signature = x.root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Signature_Inlay");
            vibe.signature = signature.GetComponentsInChildren<MeshRenderer>(true);
            vibe.signatureAnchor = signature;
            vibe.crown = x.root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.StartsWith("Crown_", StringComparison.Ordinal)).ToArray();
            vibe.swing = x.root.GetComponentsInChildren<Transform>(true).First(t => t.name == "SwingMotion");
            vibe.signatureFireflies = TWMysticNightDetails.Fireflies(signature, fireflyMat, "Signature_Fireflies",
                signature.TransformPoint(new Vector3(0f, 0.30f, -0.26f)), new Vector3(2.4f, 0.65f, 0.9f),
                new Color(1f, 0.76f, 0.40f), 5);

            TWMysticNightDetails.Build(x, a, vibe, fireflyMat);
            var map=go.AddComponent<MapZone>();map.zoneName="Круг молчания";map.caption="СТАРЫЙ ЛЕС";map.description="Шесть скамей вокруг хранителя. Одно место скрыто за стволом.";
            map.radius=x.zoneRadius-0.5f;map.priority=24;map.color=new Color(0.45f,0.54f,0.64f);map.showOnMap=true;map.announce=true;
            vibe.ApplyPreview(-1);
        }
        static Volume Profile(Transform parent,string folder,string key,bool night)
        {
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Mystic_"+key;
            var grade=profile.Add<ColorAdjustments>(true);grade.postExposure.Override(night?0.24f:0.02f);grade.contrast.Override(night?1:2);grade.saturation.Override(night?-3:1);
            grade.colorFilter.Override(night?new Color(0.94f,0.97f,1f):new Color(1f,0.99f,0.95f));
            var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(night?0.11f:0.06f);vignette.smoothness.Override(0.6f);vignette.rounded.Override(true);
            AssetDatabase.CreateAsset(profile,folder+"/"+key+".asset");
            AssetDatabase.AddObjectToAsset(grade,profile);AssetDatabase.AddObjectToAsset(vignette,profile);EditorUtility.SetDirty(profile);
            var child=new GameObject("Authored_"+key+"_Volume");child.transform.SetParent(parent,false);
            var volume=child.AddComponent<Volume>();volume.isGlobal=true;volume.priority=night?97:96;volume.weight=0;volume.sharedProfile=profile;return volume;
        }
        static Texture2D SoftTexture(string folder, string name, bool brightCore)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float distance = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                float edge = Mathf.Clamp01(1f - distance);
                float alpha = edge * edge;
                if (brightCore)
                    alpha = 0.39f * alpha + 0.61f * Mathf.Exp(-38f * distance * distance) * edge;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            string path = folder + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static ParticleSystem Particles(Transform parent,Material material,int kind)
        {
            var go=new GameObject(new[]{"Authored_Low_Mist","Authored_Cold_Motes","Authored_Falling_Leaves"}[kind]);go.transform.SetParent(parent,false);
            go.transform.localPosition=kind==0?new Vector3(-0.7f,0.32f,0.55f):Vector3.up*(kind==1?1.1f:11f);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=true;main.playOnAwake=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(8,13);main.startSpeed=new ParticleSystem.MinMaxCurve(0.025f,0.13f);
            main.startSize=kind==1?new ParticleSystem.MinMaxCurve(0.06f,0.13f):new ParticleSystem.MinMaxCurve(0.11f,0.18f);
            if (kind == 0)
            {
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(2.8f, 4.8f);
                main.startSizeY = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
                main.startSizeZ = 1f;
                main.startRotation = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            }
            main.startColor=kind==0?new Color(0.60f,0.76f,0.91f,0.22f):new Color(0.70f,0.84f,0.89f,0.55f);main.maxParticles=kind==0?14:kind==1?42:18;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;
            shape.scale = kind == 0 ? new Vector3(10.6f, 0.28f, 9.2f) : new Vector3(25f, 2f, 25f);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=new ParticleSystem.MinMaxCurve(0.03f,0.13f);velocity.y=new ParticleSystem.MinMaxCurve(kind==2?-0.2f:0.01f,kind==2?-0.05f:0.06f);velocity.z=new ParticleSystem.MinMaxCurve(-0.02f,0.04f);
            var emission=ps.emission;emission.rateOverTime=0;
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,0.2f),new GradientAlphaKey(0.7f,0.75f),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return ps;
        }
    }
}
