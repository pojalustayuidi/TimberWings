using System;
using UnityEngine;

// Numeric recipe shared by Unity pose sampling, baked preview clips and offline Blender QA.
public static class LeoMotionProfile
{
    [Serializable]public sealed class Book
    {
        public int version;
        public float windup=.28f,recovery=.26f,contact=.62f,holsterSeconds=.50f;
        public Gait walk,run;
        public Key[] chop;
    }
    [Serializable]public sealed class Gait{public float stride,stance,lift,drop,arm,bob;}
    [Serializable]public sealed class Key
    {
        public float t,left;
        public float[] tool,rotation,chest,head;
    }
    public struct ChopPose{public Vector3 tool,rotation,chest,head;public float left;}
    public struct GaitPose{public Vector3 foot;public bool stance;public float swing,phase;}
    public static Book Load()
    {
        var data=Resources.Load<TextAsset>("LeoAnimation/Motion");if(data==null)return null;
        try
        {
            var book=JsonUtility.FromJson<Book>(data.text);
            if(!Valid(book)){Debug.LogWarning("LEO ANIMATION: повреждена таблица Motion.json.");return null;}
            return book;
        }
        catch(Exception e){Debug.LogWarning("LEO ANIMATION: "+e.Message);return null;}
    }
    // Stride is the distance of two steps in model units. Both gaits use a similar
    // contact reach; the longer run stride comes from the time spent in the air.
    public static void SetDefaultGaits(Book book)
    {
        book.walk = new Gait { stride = 1.43f, stance = .59f, lift = .120f, drop = .084f, arm = 20f, bob = .032f };
        book.run = new Gait { stride = 3.62f, stance = .232f, lift = .182f, drop = .103f, arm = 31f, bob = .031f };
    }
    public static float RunBlend(float speed) => Smooth((speed - 1.55f) / 2.05f);
    public static float MaxCadence(float run) => Mathf.Lerp(1.85f, 2.52f, run);
    public static float PelvisHeight(Gait gait, float phase, float run)
    {
        float midStance = Mathf.Cos((phase - gait.stance * .5f) * Mathf.PI * 4f);
        return -gait.drop + gait.bob * midStance * Mathf.Lerp(1f, -.82f, run);
    }
    static bool Number(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    static bool Vector(float[] value){return value!=null&&value.Length==3&&Number(value[0])&&Number(value[1])&&Number(value[2]);}
    static bool ValidGait(Gait g){return g!=null&&Number(g.stride)&&g.stride>.1f&&Number(g.stance)&&g.stance>.1f&&g.stance<.9f&&Number(g.lift)&&g.lift>=0&&Number(g.drop)&&g.drop>=0&&Number(g.arm)&&Number(g.bob);}
    static bool Valid(Book b)
    {
        if(b==null||b.version!=1||!Number(b.windup)||b.windup<=0||!Number(b.recovery)||b.recovery<=0||!Number(b.contact)||b.contact<=0||b.contact>=1||!Number(b.holsterSeconds)||b.holsterSeconds<=0||!ValidGait(b.walk)||!ValidGait(b.run)||b.chop==null||b.chop.Length<2)return false;
        float previous=-1;
        foreach(var key in b.chop)
        {if(key==null||!Number(key.t)||key.t<=previous||key.t<0||key.t>1||!Number(key.left)||key.left<0||key.left>1||!Vector(key.tool)||!Vector(key.rotation)||!Vector(key.chest)||!Vector(key.head))return false;previous=key.t;}
        return b.chop[0].t==0&&b.chop[b.chop.Length-1].t==1;
    }
    public static Vector3 V(float[] x){return x!=null&&x.Length==3?new Vector3(x[0],x[1],x[2]):Vector3.zero;}
    public static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}
    public static ChopPose Chop(Book book,float phase)
    {
        if(book==null||book.chop==null||book.chop.Length==0)return default(ChopPose);
        Key[] keys = book.chop;
        int upper = 1;
        while (upper < keys.Length - 1 && phase > keys[upper].t) upper++;
        Key a = keys[upper - 1], b = keys[upper];
        Key prev = keys[Mathf.Max(0, upper - 2)], next = keys[Mathf.Min(keys.Length - 1, upper + 1)];
        float u = Mathf.Clamp01((phase - a.t) / (b.t - a.t));
        return new ChopPose
        {
            tool = Arc(prev.tool, a.tool, b.tool, next.tool, prev.t, a.t, b.t, next.t, u),
            rotation = Arc(prev.rotation, a.rotation, b.rotation, next.rotation, prev.t, a.t, b.t, next.t, u),
            chest = Arc(prev.chest, a.chest, b.chest, next.chest, prev.t, a.t, b.t, next.t, u),
            head = Arc(prev.head, a.head, b.head, next.head, prev.t, a.t, b.t, next.t, u),
            left = Mathf.Lerp(a.left, b.left, Smooth(u))
        };
    }

    // Shared tangents keep the axe moving through the internal pose keys.
    static Vector3 Arc(float[] previous, float[] a, float[] b, float[] next,
        float before, float start, float end, float after, float t)
    {
        float span = end - start;
        Vector3 from = before < start ? (V(b) - V(previous)) * (.74f * span / (end - before)) : Vector3.zero;
        Vector3 to = after > end ? (V(next) - V(a)) * (.74f * span / (after - start)) : Vector3.zero;
        return Vector3.Lerp(V(a), V(b), Smooth(t)) + (t * (1f - t)) * ((1f - t) * from - t * to);
    }
    // Stance: foot moves back at exactly body speed. Swing: cubic Hermite whose end tangents equal the stance
    // velocity -> no speed jump at toe-off and almost zero ground speed at touchdown (no skating, crisp contact).
    public static GaitPose Foot(Gait gait,float phase,Vector3 rest)
    {
        phase=Mathf.Repeat(phase,1);bool stance=phase<gait.stance;float span=gait.stride*gait.stance*.5f;
        float swing=0,z,lift=0;
        if(stance)z=span-phase*gait.stride;
        else
        {
            float ds=Mathf.Max(.05f,1-gait.stance);swing=(phase-gait.stance)/ds;
            float s = swing;
            // Keep the ground velocity at the boundary, with a short toe-off
            // and heel catch so the long stride fits the actual leg length.
            float edge = 1f - Smooth(Mathf.Min(s, 1f - s) / .28f);
            z = Mathf.Lerp(-span, span, Smooth(s))
                - gait.stride * ds * s * (1f - s) * (1f - 2f * s) * edge;
            // The knee leads the recovery, with a longer reach and a softer heel catch at the end.
            lift = Mathf.Sin(Mathf.PI * s) * gait.lift * Mathf.Lerp(1.28f, .72f, s);
        }
        return new GaitPose{foot=rest+new Vector3(0,lift,z),stance=stance,swing=swing,phase=phase};
    }
    // Foot pitch in degrees, toe-down positive: heel strike (toe up) -> flat -> heel-off -> toe-off -> toe clearance.
    public static float FootPitch(Gait gait,float phase,float run)
    {
        phase = Mathf.Repeat(phase, 1f);
        float strike = Mathf.Lerp(16f, 9f, run), off = Mathf.Lerp(24f, 29f, run), clear = Mathf.Lerp(7f, 14f, run);
        if(phase<gait.stance)
        {
            float u=phase/gait.stance;
            if (u < .20f) return -strike * (1f - Smooth(u / .20f));
            return off * Smooth((u - .62f) / .38f);
        }
        float s=(phase-gait.stance)/Mathf.Max(.05f,1-gait.stance);
        return s < .55f ? Mathf.Lerp(off, -clear, Smooth(s / .55f)) : Mathf.Lerp(-clear, -strike, Smooth((s - .55f) / .45f));
    }
    public static Gait Mix(Gait a,Gait b,float k)
    {return new Gait{stride=Mathf.Lerp(a.stride,b.stride,k),stance=Mathf.Lerp(a.stance,b.stance,k),lift=Mathf.Lerp(a.lift,b.lift,k),drop=Mathf.Lerp(a.drop,b.drop,k),arm=Mathf.Lerp(a.arm,b.arm,k),bob=Mathf.Lerp(a.bob,b.bob,k)};}
    public static void MixInto(Gait result,Gait a,Gait b,float k)
    {
        result.stride = Mathf.Lerp(a.stride, b.stride, k);
        // Blend planted distance, so the mid-speed blend cannot overextend the legs.
        result.stance = Mathf.Lerp(a.stride * a.stance, b.stride * b.stance, k) / result.stride;
        result.lift = Mathf.Lerp(a.lift, b.lift, k);
        result.drop = Mathf.Lerp(a.drop, b.drop, k);
        result.arm = Mathf.Lerp(a.arm, b.arm, k);
        result.bob = Mathf.Lerp(a.bob, b.bob, k);
    }
    public static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
    {t=Mathf.Clamp01(t);float s=1-t;return s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;}
}
