using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Generic Transform clips for Animation-window inspection/export. Runtime samples the same pose recipe.
public static class LeoAnimationClips
{
    static readonly string[] Properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
    public static void Bake()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(LeoModelBuilder.PrefabPath);
        if(prefab==null)throw new InvalidOperationException("Нет Leo_Model.prefab. Сначала собери модель Model01.");
        var book=LeoMotionProfile.Load();if(book==null)throw new InvalidOperationException("Нет профиля движений.");
        var scene=EditorSceneManager.NewPreviewScene();GameObject instance=null;
        try
        {
            instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.SetActive(false);
            var prototype=instance.GetComponent<LeoModelPresentation>();if(prototype!=null)prototype.enabled=false;
            var rig=instance.GetComponent<LeoRig>();var actor=instance.AddComponent<LeoAnimationController>();actor.rig=rig;
            if(!actor.Initialize())throw new InvalidOperationException("Не удалось открыть риг для записи клипов.");
            var bones=Bones(rig);LeoAnimationTests.Validate(actor,rig,bones);
            LeoAnimationBuilder.EnsureFolder(LeoAnimationBuilder.Folder);
            foreach(string name in LeoAnimationBuilder.Clips)
            {
                float duration=LeoAnimationController.ClipDuration(name,book);int frames=Mathf.CeilToInt(duration*60);
                var times=new SortedSet<float>();for(int i=0;i<=frames;i++)times.Add(duration*i/frames);
                if(name=="Chop")times.Add(book.windup); // Exact contact even when it is between sample frames.
                var keys=new List<Keyframe>[bones.Count,10];for(int b=0;b<bones.Count;b++)for(int p=0;p<10;p++)keys[b,p]=new List<Keyframe>();
                var active=new Dictionary<string,List<Keyframe>>();foreach(var bone in bones)if(CargoOrTool(bone))active.Add(AnimationUtility.CalculateTransformPath(bone,rig.transform),new List<Keyframe>());
                foreach(float time in times)
                {
                    actor.SamplePreview(name,time/duration);
                    for(int b=0;b<bones.Count;b++)
                    {
                        var bone=bones[b];Vector3 v=bone.localPosition,s=bone.localScale;Quaternion q=bone.localRotation;
                        float[] values={v.x,v.y,v.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};
                        for(int p=0;p<10;p++)keys[b,p].Add(new Keyframe(time,values[p]));
                        if(CargoOrTool(bone))active[AnimationUtility.CalculateTransformPath(bone,rig.transform)].Add(new Keyframe(time,bone.gameObject.activeSelf?1:0));
                    }
                }
                var clip=new AnimationClip{name="Leo_"+name,frameRate=60,legacy=false};
                for(int b=0;b<bones.Count;b++)for(int p=0;p<10;p++)
                {
                    string path=AnimationUtility.CalculateTransformPath(bones[b],rig.transform);var curve=Curve(keys[b,p],false);
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),Properties[p]),curve);
                }
                foreach(var item in active)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(item.Key,typeof(GameObject),"m_IsActive"),Curve(item.Value,true));
                clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=LeoAnimationBuilder.Loop(name);settings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
                AnimationUtility.SetAnimationEvents(clip,new AnimationEvent[0]);
                string destination=LeoAnimationBuilder.Folder+"/Leo_"+name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
                if(existing==null)AssetDatabase.CreateAsset(clip,destination);else{EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(clip);}
            }
            AssetDatabase.SaveAssets();
        }
        finally{if(instance!=null)Object.DestroyImmediate(instance);EditorSceneManager.ClosePreviewScene(scene);}
    }
    static bool CargoOrTool(Transform bone){return bone.name.StartsWith("Cargo",StringComparison.Ordinal)||bone.name.StartsWith("FrameT",StringComparison.Ordinal)||bone.name=="AxeRoot"||bone.name=="HammerRoot";}
    static AnimationCurve Curve(List<Keyframe> values,bool discrete)
    {
        bool constant=true;float first=values[0].value;foreach(var key in values)if(Mathf.Abs(key.value-first)>.000001f){constant=false;break;}
        var curve=constant?new AnimationCurve(values[0],values[values.Count-1]):new AnimationCurve(values.ToArray());
        for(int i=0;i<curve.length;i++)
        {
            var mode=discrete?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.Linear;
            AnimationUtility.SetKeyLeftTangentMode(curve,i,mode);AnimationUtility.SetKeyRightTangentMode(curve,i,mode);
        }
        return curve;
    }
    public static List<Transform> Bones(LeoRig rig)
    {
        // Generated Cargo6..9 and the compatibility LogUpgrade pivot are real
        // rig joints too. Use the same traversal as LeoRig.CaptureRest so clip
        // baking and pose tests never silently discard those attachments.
        var result=new List<Transform>();foreach(var bone in rig.GetComponentsInChildren<Transform>(true))if(bone!=rig.transform&&bone.GetComponent<MeshFilter>()==null)result.Add(bone);
        if(result.Count!=rig.JointCount)throw new InvalidOperationException("Риг изменён после Model01; число суставов не совпадает.");return result;
    }
}
