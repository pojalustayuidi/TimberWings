using System;
using UnityEngine;

// Shared, numeric animation recipe. Also consumed by the offline geometry preview.
public static class OskarMotionProfile
{
    [Serializable] public sealed class Book { public int version; public Clip[] clips; }
    [Serializable] public sealed class Clip
    {
        public string id,leftReference;
        public float duration,holdStart=.3f,holdEnd=.7f;
        public float[] leftPoint,rightPoint;
        public Frame[] frames;
    }
    [Serializable] public sealed class Frame
    {
        public float t,leftWeight,rightWeight,carry,grip;
        public float[] head,spine,leftWrist,rightWrist,squirrel,planerGrip;
    }
    public struct Pose
    {
        public float leftWeight,rightWeight,carry,grip;
        public Vector3 head,spine,leftWrist,rightWrist,squirrel,planerGrip;
    }
    public static Vector3 V(float[] value) {return value!=null&&value.Length==3?new Vector3(value[0],value[1],value[2]):Vector3.zero;}
    public static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
    public static Pose Sample(Clip clip,float t)
    {
        if(clip==null||clip.frames==null||clip.frames.Length==0)return default(Pose);
        var a=clip.frames[0];var b=a;
        for(int i=1;i<clip.frames.Length;i++){b=clip.frames[i];if(t<=b.t)break;a=b;}
        float k=b.t>a.t?Smooth((t-a.t)/(b.t-a.t)):0;
        return new Pose{leftWeight=Mathf.Lerp(a.leftWeight,b.leftWeight,k),rightWeight=Mathf.Lerp(a.rightWeight,b.rightWeight,k),carry=Mathf.Lerp(a.carry,b.carry,k),grip=Mathf.Lerp(a.grip,b.grip,k),head=Vector3.Lerp(V(a.head),V(b.head),k),spine=Vector3.Lerp(V(a.spine),V(b.spine),k),leftWrist=Vector3.Lerp(V(a.leftWrist),V(b.leftWrist),k),rightWrist=Vector3.Lerp(V(a.rightWrist),V(b.rightWrist),k),squirrel=Vector3.Lerp(V(a.squirrel),V(b.squirrel),k),planerGrip=Vector3.Lerp(V(a.planerGrip),V(b.planerGrip),k)};
    }
    public static Book Load()
    {
        var asset=Resources.Load<TextAsset>("OskarAnimation/Motion");
        if(asset==null)return null;
        try{return JsonUtility.FromJson<Book>(asset.text);}catch(Exception e){Debug.LogWarning("OSKAR MOTION: "+e.Message);return null;}
    }
    public static Clip Find(Book book,string id){if(book!=null&&book.clips!=null)foreach(var c in book.clips)if(c!=null&&c.id==id)return c;return null;}
    // Returning uses the same path backwards/forwards. A held pencil never teleports home.
    public sealed class Clock
    {
        public Clip clip {get;private set;}
        public float phase {get;private set;}
        public bool returning {get;private set;}
        bool reverse;
        public void Start(Clip value){clip=value;phase=0;returning=false;reverse=false;}
        public void Clear(){clip=null;phase=0;returning=false;reverse=false;}
        public void Return()
        {
            if(clip==null||returning)return;
            returning=true;reverse=phase<clip.holdStart;
            if(!reverse&&phase<clip.holdEnd)phase=clip.holdEnd;
        }
        public void Tick(float dt)
        {
            if(clip==null||dt<=0)return;
            float step=dt/Mathf.Max(.1f,clip.duration)*(returning?3f:1f);
            phase+=reverse?-step:step;
            if(phase>=1||phase<0)Clear();
        }
    }
    public sealed class Approach
    {
        bool inside=true;float next;
        public void Reset(float now){inside=true;next=now+8;}
        public bool Enter(float distance,float radius,float now,bool allowed)
        {
            if(distance>radius+1.5f)inside=false;
            if(distance>radius||inside)return false;
            inside=true;if(!allowed||now<next)return false;next=now+35;return true;
        }
    }
}
