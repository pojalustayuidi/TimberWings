using UnityEngine;

public sealed partial class LeoAnimationController
{
    struct PoseFrame
    {
        public float time,phase,move,run,carry,action,actionPhase,holster,turn,talk,eye,dash,loadPitch,loadRoll,adjust,full;
        public int quality,carried,capacity;
        public Vector3? bark;
        public LeoCelebration.Pose cheer;
        public LeoAirWaterMotion.Frame motion;
        public StoryInspection.ActorPose inspection;
    }

    LeoFootPlacement feet;

    void ApplyPose(PoseFrame f,bool worldPlant,bool ground)
    {
        rig.ResetPose();rig.EquipHammer(false);
        if(carryVisuals)rig.ShowCargo(f.capacity>0?f.carried:Mathf.RoundToInt(f.carry*21),f.capacity>0?f.capacity:21);
        float wave=Mathf.Sin(f.phase*Mathf.PI*2),sway=Mathf.Cos(f.phase*Mathf.PI*2);
        float breath=Mathf.Sin(f.time*Mathf.PI*.5f),loaded=LeoMotionProfile.Smooth((f.carry-.35f)/.65f);
        var chop=LeoMotionProfile.Chop(book,f.actionPhase);
        // Shift the hips over the stance foot and counter-rotate the shoulders.
        float shift = .024f * wave;
        float aim = LeoAxePose.ContactAim(f.actionPhase, book.contact);
        rig.hips.localPosition = rig.RestPosition(rig.hips) +
            new Vector3(-shift * f.move, LeoMotionProfile.PelvisHeight(gait, f.phase, f.run) * f.move - .018f * f.full, 0f) +
            LeoAxePose.BodyShift(chop, aim, transform, f.bark) * f.action;
        float yawA=4.5f+1.2f*f.run;                              // pelvis leads with the forward leg, shoulders counter-rotate
        rig.Rotate(rig.hips,new Vector3(0,yawA*sway*f.move,-(2+(1-f.run))*wave*f.move+2*f.turn*f.move));
        float lean=(2+4*f.run+5*loaded)*f.move+6*f.dash+3.5f*f.full; // full load: leans into the weight even when standing
        rig.Rotate(rig.spine,new Vector3(lean+.45f*breath,-yawA*.4f*sway*f.move,1.2f*wave*f.move-2.5f*f.turn));
        rig.Rotate(rig.chest,chop.chest*f.action+new Vector3(0,-(4+4*f.run)*sway*f.move+4*f.turn-8*f.adjust,0));
        rig.Rotate(rig.head,chop.head*f.action+new Vector3(-lean*.65f+Mathf.Sin(f.time*2.4f)*f.talk*2,breath*(1-f.move)*2+4*f.turn,Mathf.Sin(f.time*1.8f)*f.talk+1.3f*wave*f.move));
        rig.Rotate(rig.jaw,new Vector3(Mathf.Max(0,Mathf.Sin(f.time*20))*5*f.talk,0,0));
        float effort=f.action*Mathf.Max(0,1-Mathf.Abs(f.actionPhase-book.contact)/.16f)*(f.quality==2?4:2);
        rig.Rotate(rig.browL,new Vector3(0,0,-3*f.talk-effort));rig.Rotate(rig.browR,new Vector3(0,0,2*f.talk+effort));
        float arm = Mathf.Cos(f.phase * Mathf.PI * 2f - .24f);
        rig.Rotate(rig.upperArmL, new Vector3(arm * gait.arm * f.move * (1f - .26f * loaded),
            -2f * wave * f.move, -2.5f * f.move));
        rig.Rotate(rig.elbowL, new Vector3(-(5f + (1f - arm) * (6f + 7f * f.run)) * f.move - 8f * loaded * f.move, 0f, 0f));
        rig.Rotate(rig.upperArmR, new Vector3(-sway * (4f + 3f * f.run) * f.move, 0f, 3.5f * f.move));
        rig.Rotate(rig.elbowR, new Vector3(-5f - (2f + 5f * f.run) * f.move, 0f, 0f));
        f.cheer.ApplyBody(rig, f.carry);
        feet.Apply(gait, f.phase, f.move, f.run, Mathf.Abs(f.turn), f.action * (1f - f.move),
            worldPlant, ground, groundMask, Mathf.Min(Time.deltaTime, .1f), f.cheer);
        LeoAxePose.RestWrist(rig, f.move);
        f.motion.Apply(rig, f.holster);
        float palmRoll = LeoAxePose.RightArm(rig, transform, book, chop, f.actionPhase, f.action, f.bark);
        LeoAxePose.Place(rig, transform, f.holster, palmRoll);
        LeoAxePose.LeftArm(rig, transform, f.actionPhase, f.action * chop.left);
        f.cheer.ApplyHands(rig, f.holster);
        if(f.adjust>.001f&&f.action<.05f&&!f.motion.StowTool&&rig.cargoRoot!=null)
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
        StoryInspection.ApplyActorPose(rig, transform, f.holster, f.inspection);
        rig.PlaceHammer();
        if(rig.eyeL!=null)rig.eyeL.localScale=Vector3.Scale(rig.RestScale(rig.eyeL),new Vector3(1,f.eye,1));
        if(rig.eyeR!=null)rig.eyeR.localScale=Vector3.Scale(rig.RestScale(rig.eyeR),new Vector3(1,f.eye,1));
    }

}
