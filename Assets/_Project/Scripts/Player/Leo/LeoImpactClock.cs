using UnityEngine;

// A visual clock; it cannot emit an impact, shorten cooldowns or complete a tree.
public sealed class LeoImpactClock
{
    float due=-1,start,last=-1000;
    public float LastImpact{get{return last;}}
    public void Clear(){due=-1;start=0;last=-1000;}
    public void Hit(float now){last=now;}
    public void Forecast(float now,float value,float windup)
    {
        if(value<now-.02f||float.IsNaN(value)||float.IsInfinity(value)){due=-1;return;}
        if(due<0||Mathf.Abs(value-due)>.02f){due=value;start=Mathf.Max(now,value-windup);}
        else due=value;
    }
    public void ForgetForecast(){due=-1;}
    public float Phase(float now,float contact,float recovery,bool impactFrame)
    {
        if(impactFrame)return contact;
        float recover = RecoverySpan(recovery);
        float age=now-last;
        if (age >= 0f && age < recover)
        {
            float brace = Mathf.Min(.025f, recover * .25f);
            return Mathf.Lerp(contact, 1f, (age - brace) / (recover - brace));
        }
        if(due>=now&&due>start)
        {
            float begin=Mathf.Max(start,last+recover);
            if(now>=begin&&due>begin)return contact * Mathf.Clamp01((now - begin) / (due - begin));
        }
        return 0;
    }
    public bool Recovering(float now, float recovery) => now >= last && now - last < RecoverySpan(recovery);
    float RecoverySpan(float recovery)
    {
        float interval = due > last ? due - last : float.PositiveInfinity;
        return Mathf.Max(.025f, Mathf.Min(recovery, interval * .48f));
    }
}
