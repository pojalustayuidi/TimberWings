using System;
using System.Collections.Generic;
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
            var lights=new List<Light>();
            for(int i=0;i<2;i++)
            {
                var l=new GameObject("Authored_Moonlight_"+i);l.transform.SetParent(go.transform,false);
                l.transform.localPosition=new Vector3(i==0?-3:4,i==0?3.4f:1.7f,i==0?-1:4);
                var light=l.AddComponent<Light>();light.type=LightType.Point;light.range=i==0?11:7;light.color=i==0?new Color(0.47f,0.68f,0.79f):new Color(0.68f,0.56f,0.83f);
                light.intensity=0.3f;light.shadows=LightShadows.None;lights.Add(light);
            }
            vibe.lights=lights.ToArray();
            var texture=SoftTexture(a.folder);
            var particleMat=new Material(a.veil){name="Mystic_Particles"};particleMat.SetTexture("_BaseMap",texture);particleMat.SetFloat("_Alpha",0.65f);
            AssetDatabase.CreateAsset(particleMat,a.folder+"/Particles.mat");
            vibe.particles=new[]{Particles(go.transform,particleMat,0),Particles(go.transform,particleMat,1),Particles(go.transform,particleMat,2)};
            vibe.runes=x.root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Rune_",StringComparison.Ordinal)).ToArray();
            vibe.apparitions=x.root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Apparition_",StringComparison.Ordinal)).ToArray();
            vibe.nightWords=new[]{Words(x,go.transform,"ЗДЕСЬ ЖДАЛИ",new Vector2(-4.0f,4.7f),-16),Words(x,go.transform,"НЕ ВСЕ ГОЛОСА — ВЕТЕР",new Vector2(3.6f,-4.8f),24)};
            var old=x.elder.GetComponent<GrandpaVibe>();if(old!=null){vibe.swing=old.swing;vibe.motes=old.motes;}
            vibe.ambience=Sound(go.transform,a.folder);
            var map=go.AddComponent<MapZone>();map.zoneName="Круг молчания";map.caption="СТАРЫЙ ЛЕС";map.description="Шесть мест вокруг Дедушки. Здесь до сих пор кого-то ждут.";
            map.radius=x.zoneRadius-0.5f;map.priority=24;map.color=new Color(0.45f,0.54f,0.64f);map.showOnMap=true;map.announce=true;
        }
        static Volume Profile(Transform parent,string folder,string key,bool night)
        {
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Mystic_"+key;
            var grade=profile.Add<ColorAdjustments>(true);grade.postExposure.Override(night?-0.68f:-0.48f);grade.contrast.Override(night?9:4);grade.saturation.Override(night?-17:-12);
            grade.colorFilter.Override(night?new Color(0.74f,0.81f,0.98f):new Color(0.86f,0.95f,0.98f));
            var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(night?0.38f:0.22f);vignette.smoothness.Override(0.6f);vignette.rounded.Override(true);
            AssetDatabase.CreateAsset(profile,folder+"/"+key+".asset");
            AssetDatabase.AddObjectToAsset(grade,profile);AssetDatabase.AddObjectToAsset(vignette,profile);EditorUtility.SetDirty(profile);
            var child=new GameObject("Authored_"+key+"_Volume");child.transform.SetParent(parent,false);
            var volume=child.AddComponent<Volume>();volume.isGlobal=true;volume.priority=night?97:96;volume.weight=0;volume.sharedProfile=profile;return volume;
        }
        static Texture2D SoftTexture(string folder)
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {float d=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;float alpha=Mathf.Pow(Mathf.Clamp01(1-d),2);texture.SetPixel(x,y,new Color(1,1,1,alpha));}
            texture.Apply();string path=folder+"/Soft.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static ParticleSystem Particles(Transform parent,Material material,int kind)
        {
            var go=new GameObject(new[]{"Authored_Low_Mist","Authored_Cold_Motes","Authored_Falling_Leaves"}[kind]);go.transform.SetParent(parent,false);
            go.transform.localPosition=Vector3.up*(kind==0?0.4f:kind==1?1.1f:11f);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=true;main.playOnAwake=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(8,13);main.startSpeed=new ParticleSystem.MinMaxCurve(0.025f,0.13f);
            main.startSize=kind==0?new ParticleSystem.MinMaxCurve(2.6f,4.6f):kind==1?new ParticleSystem.MinMaxCurve(0.06f,0.13f):new ParticleSystem.MinMaxCurve(0.11f,0.18f);
            main.startColor=kind==0?new Color(0.62f,0.76f,0.80f,0.12f):new Color(0.70f,0.84f,0.89f,0.55f);main.maxParticles=kind==0?26:kind==1?42:18;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(25,kind==0?0.25f:2,25);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=new ParticleSystem.MinMaxCurve(0.03f,0.13f);velocity.y=new ParticleSystem.MinMaxCurve(kind==2?-0.2f:0.01f,kind==2?-0.05f:0.06f);velocity.z=new ParticleSystem.MinMaxCurve(-0.02f,0.04f);
            var emission=ps.emission;emission.rateOverTime=0;
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,0.2f),new GradientAlphaKey(0.7f,0.75f),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return ps;
        }
        static TextMesh Words(TWMysticContext x,Transform parent,string text,Vector2 p,float yaw)
        {
            var go=new GameObject("Authored_Night_Inscription");go.transform.SetParent(parent,false);go.transform.position=x.InscriptionPoint(p)+Vector3.up*0.04f;
            go.transform.rotation=x.rotation*Quaternion.Euler(90,yaw,0);
            var label=go.AddComponent<TextMesh>();label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=48;label.characterSize=0.075f;
            label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(0.68f,0.83f,0.88f,0);
            go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;return label;
        }
        static AudioSource Sound(Transform parent,string folder)
        {
            const int rate=16000,seconds=12,count=rate*seconds;string path=folder+"/OldForest_Air.wav";
            using(var file=new BinaryWriter(File.Create(path)))
            {
                file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+count*2);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(rate);file.Write(rate*2);file.Write((short)2);file.Write((short)16);
                file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(count*2);
                for(int i=0;i<count;i++)
                {
                    double t=(double)i/rate;
                    double value=(Math.Sin(t*Math.PI*2*65)*0.28+Math.Sin(t*Math.PI*2*97)*0.13+Math.Sin(t*Math.PI*2*146)*0.035)*(0.7+0.3*Math.Cos(t*Math.PI*2/seconds));
                    file.Write((short)(value*8000));
                }
            }
            AssetDatabase.ImportAsset(path);var source=parent.gameObject.AddComponent<AudioSource>();source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);source.loop=true;source.playOnAwake=true;source.spatialBlend=0;source.volume=0;return source;
        }
    }
}
