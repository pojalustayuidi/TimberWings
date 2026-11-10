using UnityEngine;

public sealed partial class LeoAnimationController
{
    struct PoseFrame
    {
        public float time,phase,move,run,carry,action,actionPhase,holster,turn,talk,eye,dash,loadPitch,loadRoll,adjust,full;
        public int quality,carried,capacity;
        public Vector3? bark;
    }
    sealed class FootState {public bool planted,smoothed;public Vector3 anchor,previousGoal;}
    readonly FootState leftFoot=new FootState(),rightFoot=new FootState();
    // Managed arrays are safe in field initializers. No Unity native objects are constructed here.
    readonly RaycastHit[] groundHits=new RaycastHit[12];
    Vector3 leftAnkleRest,rightAnkleRest;
    Quaternion leftAnkleRotation,rightAnkleRotation;
    float soleOffset;
    void CachePose()
    {
        leftAnkleRest=transform.InverseTransformPoint(rig.ankleL.position);
        rightAnkleRest=transform.InverseTransformPoint(rig.ankleR.position);
        leftAnkleRotation=Quaternion.Inverse(transform.rotation)*rig.ankleL.rotation;
        rightAnkleRotation=Quaternion.Inverse(transform.rotation)*rig.ankleR.rotation;
        // Baked sole plane in model units. The editor fit places it at the capsule support.
        soleOffset=Mathf.Min(leftAnkleRest.y,rightAnkleRest.y)-.0035f;
    }
    void ResetFeet(){leftFoot.planted=rightFoot.planted=false;leftFoot.smoothed=rightFoot.smoothed=false;}
    void ApplyPose(PoseFrame f,bool worldPlant,bool ground)
    {
        rig.ResetPose();rig.EquipHammer(false);
        if(carryVisuals)rig.ShowCargo(f.capacity>0?f.carried:Mathf.RoundToInt(f.carry*21),f.capacity>0?f.capacity:21);
        float wave=Mathf.Sin(f.phase*Mathf.PI*2),sway=Mathf.Cos(f.phase*Mathf.PI*2);
        float breath=Mathf.Sin(f.time*Mathf.PI*.5f),loaded=LeoMotionProfile.Smooth((f.carry-.35f)/.65f);
        var chop=LeoMotionProfile.Chop(book,f.actionPhase);
        // Pelvis rides over the stance leg: highest at mid-stance when walking, lowest at mid-stance when running (flight phase).
        float mid=gait.stance*.5f,walkSign=Mathf.Clamp((gait.stance-.5f)/.08f,-1,1);
        float bounce=Mathf.Cos((f.phase-mid)*Mathf.PI*4)*walkSign;
        float shift=(.010f+.006f*(1-f.run))*wave;          // weight moves over the stance foot
        float aim = LeoAxePose.ContactAim(f.actionPhase, book.contact);
        rig.hips.localPosition = rig.RestPosition(rig.hips) +
            new Vector3(-shift * f.move, -gait.drop * f.move + gait.bob * bounce * f.move - .018f * f.full, 0f) +
            LeoAxePose.BodyShift(chop, aim, transform, f.bark) * f.action;
        float yawA=3+3*f.run;                              // pelvis leads with the forward leg, shoulders counter-rotate
        rig.Rotate(rig.hips,new Vector3(0,yawA*sway*f.move,-(2+(1-f.run))*wave*f.move+2*f.turn*f.move));
        float lean=(2+4*f.run+5*loaded)*f.move+6*f.dash+3.5f*f.full; // full load: leans into the weight even when standing
        rig.Rotate(rig.spine,new Vector3(lean+.45f*breath,-yawA*.4f*sway*f.move,1.2f*wave*f.move-2.5f*f.turn));
        rig.Rotate(rig.chest,chop.chest*f.action+new Vector3(0,-(4+4*f.run)*sway*f.move+4*f.turn-8*f.adjust,0));
        rig.Rotate(rig.head,chop.head*f.action+new Vector3(-lean*.65f+Mathf.Sin(f.time*2.4f)*f.talk*2,breath*(1-f.move)*2+4*f.turn,Mathf.Sin(f.time*1.8f)*f.talk+1.3f*wave*f.move));
        rig.Rotate(rig.jaw,new Vector3(Mathf.Max(0,Mathf.Sin(f.time*20))*5*f.talk,0,0));
        float effort=f.action*Mathf.Max(0,1-Mathf.Abs(f.actionPhase-book.contact)/.16f)*(f.quality==2?4:2);
        rig.Rotate(rig.browL,new Vector3(0,0,-3*f.talk-effort));rig.Rotate(rig.browR,new Vector3(0,0,2*f.talk+effort));
        // Free arm: counter-swing, elbow folds while the hand travels forward, forearm trails the shoulder.
        float lag=Mathf.Cos(f.phase*Mathf.PI*2-.45f);
        rig.Rotate(rig.upperArmL,new Vector3(sway*gait.arm*f.move*(1-.35f*loaded),-2*wave*f.move,-3*loaded*f.move));
        rig.Rotate(rig.elbowL,new Vector3(-(8+22*f.run)*(.5f-.5f*lag)*f.move-8*loaded*f.move,0,0));
        // A low axe swing keeps the blade away from the face during locomotion.
        rig.Rotate(rig.upperArmR,new Vector3(-sway*8*f.move,0,3*loaded*f.move));
        rig.Rotate(rig.elbowR,new Vector3(-8*f.run*f.move,0,0));
        Foot(rig.hipL,rig.kneeL,rig.ankleL,rig.toeL,leftAnkleRest,leftAnkleRotation,leftFoot,f.phase,f,worldPlant,ground);
        Foot(rig.hipR,rig.kneeR,rig.ankleR,rig.toeR,rightAnkleRest,rightAnkleRotation,rightFoot,f.phase+.5f,f,worldPlant,ground);
        LeoAxePose.RestWrist(rig, f.move);
        float palmRoll = LeoAxePose.RightArm(rig, transform, book, chop, f.actionPhase, f.action, f.bark);
        PlaceTool(f.holster, palmRoll);
        LeoAxePose.LeftArm(rig, transform, f.actionPhase, f.action * chop.left);
        if(f.adjust>.001f&&f.action<.05f&&rig.cargoRoot!=null)
        {
            // Leo reaches back with the free hand and straightens the bundle (hand rests on top of the stack).
            int rows=(LeoRig.CargoSections(f.carried,f.capacity,12)+1)/2;
            float top=Mathf.Max(0,rows-1)*LeoRig.LogPitch+LeoRig.LogPitch*.5f;
            Vector3 hand=rig.cargoRoot.TransformPoint(new Vector3(-.10f,top+.045f,-.06f));
            LeoRig.SolveTwoBone(rig.upperArmL,rig.elbowL,rig.handL,hand,transform.TransformPoint(new Vector3(-.62f,1.25f,-.30f)),rig.handL.rotation,f.adjust*(1-f.action));
        }
        rig.Rotate(rig.backpack,new Vector3(-.8f*sway*f.move,0,-1.0f*wave*f.move));
        rig.Rotate(rig.cargoRoot,new Vector3(f.loadPitch,0,f.loadRoll)); // logs lag behind the body on a damped spring
        rig.Rotate(rig.backpackFlap,new Vector3(2*Mathf.Sin(f.phase*Mathf.PI*4)*f.move,0,0));
        rig.Rotate(rig.hair,new Vector3(.6f*breath+1.2f*f.move*sway,0,0));
        rig.Rotate(rig.cowlick,new Vector3(2*wave*f.move,0,1.5f*Mathf.Sin(f.time*Mathf.PI)));
        rig.PlaceHammer();
        if(rig.eyeL!=null)rig.eyeL.localScale=Vector3.Scale(rig.RestScale(rig.eyeL),new Vector3(1,f.eye,1));
        if(rig.eyeR!=null)rig.eyeR.localScale=Vector3.Scale(rig.RestScale(rig.eyeR),new Vector3(1,f.eye,1));
    }
    void PlaceTool(float amount, float palmRoll)
    {
        if (rig.axeRoot == null || rig.handR == null || rig.axeHolster == null) return;
        amount = Mathf.Clamp01(amount);
        if (amount > 0f)
        {
            Vector3 start = LeoAxePose.Grip(rig.handR);
            Quaternion startRotation = LeoAxePose.ToolRotation(rig.handR, palmRoll);
            float path = LeoMotionProfile.Smooth(amount / .76f);
            Vector3 goal = LeoMotionProfile.Bezier(start, transform.TransformPoint(new Vector3(.53f, .94f, .10f)),
                transform.TransformPoint(new Vector3(.49f, 1.06f, -.11f)), rig.axeHolster.TransformPoint(LeoAxePose.MainGrip), path);
            Quaternion tool = Quaternion.Slerp(startRotation, rig.axeHolster.rotation, path);
            Quaternion wrist = LeoAxePose.RightWrist(tool * Quaternion.AngleAxis(palmRoll, Vector3.up));
            float reach = LeoMotionProfile.Smooth(amount / .12f) * (1f - LeoMotionProfile.Smooth((amount - .76f) / .24f));
            LeoRig.SolveTwoBone(rig.upperArmR, rig.elbowR, rig.handR, LeoAxePose.WristGoal(rig.handR, goal, wrist),
                transform.TransformPoint(new Vector3(.70f, 1.05f, -.15f)), wrist, reach);
        }
        rig.PlaceAxe(LeoMotionProfile.Smooth((amount - .76f) / .24f), palmRoll);
    }
    void Foot(Transform hip,Transform knee,Transform ankle,Transform toe,Vector3 rest,Quaternion restRotation,FootState state,float cycle,PoseFrame f,bool worldPlant,bool ground)
    {
        var step=LeoMotionProfile.Foot(gait,cycle,rest);
        Vector3 local = Vector3.Lerp(rest, step.foot, f.move);
        float lift = local.y - rest.y;
        local.y = rest.y;
        Vector3 goal = transform.TransformPoint(local);
        if(worldPlant&&f.move>.65f&&step.stance)
        {
            if(!state.planted)state.anchor=goal;
            state.planted=true;
            // Phase is distance-driven, so the anchor normally equals the goal. The clamp only absorbs
            // turns / cadence-limited top speed, continuously and never as a one-frame replant.
            float reach=.55f*Mathf.Max(.01f,transform.lossyScale.y);
            state.anchor=Vector3.MoveTowards(state.anchor,goal,Mathf.Max(0,Vector3.Distance(state.anchor,goal)-reach));
            goal=state.anchor;
        }
        else state.planted=false;
        Quaternion rotation=transform.rotation*restRotation;
        if(ground)
        {
            float size=Mathf.Max(.01f,transform.lossyScale.y);
            int count=Physics.RaycastNonAlloc(goal+Vector3.up*.35f*size,Vector3.down,groundHits,.85f*size,groundMask,QueryTriggerInteraction.Ignore);
            float distance=float.MaxValue;int found=-1;
            for(int i=0;i<count;i++)
            {
                var hit=groundHits[i];if(hit.collider==null||hit.normal.y<.65f||hit.collider.attachedRigidbody!=null||hit.collider.transform.IsChildOf(player!=null?player.transform:transform))continue;
                float candidate=hit.point.y+soleOffset*size;
                if(Mathf.Abs(candidate-goal.y)>.18f*size||hit.distance>=distance)continue;distance=hit.distance;found=i;
            }
            if(found>=0)
            {
                goal.y=groundHits[found].point.y+soleOffset*size;
                Vector3 normal=Vector3.Slerp(Vector3.up,groundHits[found].normal,.65f);
                rotation=Quaternion.FromToRotation(Vector3.up,normal)*rotation;
            }
        }
        if(worldPlant)
        {
            // Smooth ONLY the ground height. Smoothing x/z made the foot lag behind the body and look "mushy".
            if(!state.smoothed){state.previousGoal=goal;state.smoothed=true;}
            else state.previousGoal.y=Mathf.Lerp(state.previousGoal.y,goal.y,1-Mathf.Exp(-22f*Mathf.Min(.1f,Time.deltaTime)));
            goal.y=state.previousGoal.y;state.previousGoal=goal;
        }
        else state.smoothed=false;
        // Heel strike -> flat -> heel-off -> toe-off. The ankle is shifted so the heel / toe stays on the ground.
        float pitch=LeoMotionProfile.FootPitch(gait,cycle,f.run)*f.move;
        float rad=pitch*Mathf.Deg2Rad,s=Mathf.Sin(rad),c=Mathf.Cos(rad);
        Vector3 pivot=pitch>0?new Vector3(0,.082f*(c-1)+.13f*s,.082f*s+.13f*(1-c)):new Vector3(0,-.06f*s+.082f*(c-1),.082f*s+.06f*(c-1));
        goal += transform.TransformVector(pivot + Vector3.up * lift);
        rotation=Quaternion.AngleAxis(pitch,transform.right)*rotation;
        Vector3 pole=transform.TransformPoint(new Vector3(rest.x*1.2f,.6f,.65f));
        LeoRig.SolveTwoBone(hip,knee,ankle,goal,pole,rotation,1);
        rig.Rotate(toe,new Vector3(-Mathf.Max(0,pitch)*.8f-4*Mathf.Sin(step.swing*Mathf.PI)*f.move,0,0));
    }
}
