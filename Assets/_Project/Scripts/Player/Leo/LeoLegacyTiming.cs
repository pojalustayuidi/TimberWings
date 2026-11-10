using System.Collections.Generic;
using UnityEngine;

// Forecasts an existing legacy clip event. Never plays/restarts that Animator or calls its events.
public sealed class LeoLegacyTiming
{
    readonly List<AnimatorClipInfo> clips=new List<AnimatorClipInfo>(2);
    readonly Dictionary<AnimationClip,float[]> events=new Dictionary<AnimationClip,float[]>();
    public void Clear(){clips.Clear();}
    public float Next(Animator animator,float now)
    {
        if(animator==null||!animator.isActiveAndEnabled||animator.runtimeAnimatorController==null||animator.speed<=0||animator.IsInTransition(0))return -1;
        clips.Clear();animator.GetCurrentAnimatorClipInfo(0,clips);if(clips.Count==0)return -1;
        AnimationClip clip=clips[0].clip;float weight=clips[0].weight;
        for(int i=1;i<clips.Count;i++)if(clips[i].weight>weight){clip=clips[i].clip;weight=clips[i].weight;}
        if(clip==null||clip.length<=.001f)return -1;
        float[] times;if(!events.TryGetValue(clip,out times))
        {
            var hits=new List<float>();foreach(var entry in clip.events)if(entry.functionName=="OnChopHit")hits.Add(Mathf.Clamp01(entry.time/clip.length));
            hits.Sort();times=hits.ToArray();events[clip]=times;
        }
        if(times.Length==0)return -1;
        var state=animator.GetCurrentAnimatorStateInfo(0);float speed=animator.speed*state.speed*state.speedMultiplier;if(speed<=.001f)return -1;
        float cycle=Mathf.Repeat(state.normalizedTime,1),remaining=float.MaxValue;
        foreach(float hit in times)
        {
            float gap=hit-cycle;if(gap<-.001f){if(!state.loop)continue;gap+=1;}
            if(gap>=-.001f)remaining=Mathf.Min(remaining,Mathf.Max(0,gap));
        }
        return remaining<float.MaxValue?now+remaining*clip.length/speed:-1;
    }
}
