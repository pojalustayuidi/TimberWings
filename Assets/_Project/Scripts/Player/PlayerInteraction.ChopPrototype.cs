using UnityEngine;

public partial class PlayerInteraction
{
    ChopPrototypeLog processingLog;
    ChopPrototypeLog armedLog;
    readonly RaycastHit[] prototypeHits = new RaycastHit[64];
    public bool IsProcessingPrototypeLog => processingLog != null;

    bool PrototypeInputAllowed => !MobileInput.ModalOpen && !DialogueBusy() && !StoryInspection.Active &&
        !ArrivalIntroSequence.Active && !SaveSystem.IsLoading && !EndingSequence.Active &&
        !LeoCelebration.IsPlaying && !BuildWorkPose.MovementLocked && !WaterSwim.Swimming &&
        (movement == null || !movement.ControlLocked) && Application.isFocused && Time.timeScale > 0f;

    bool PrototypeStanding(ResourceItem item) => !RhythmChop && ChopPrototypeLog.Supports(item);

    void PrototypeToggle()
    {
        if (!Input.GetKeyDown(KeyCode.F6) || !PrototypeInputAllowed || IsChopping) return;
        if (RhythmChop && !ChopPrototypeLog.Enabled)
        {
            SaveIndicator.ShowToast("CHOP-01: включи обычную рубку с удержанием в настройках");
            return;
        }
        ChopPrototypeLog.Toggle();
    }

    int PrototypeFellingHits(ResourceItem item) => item.Size == TreeSize.Old ? 7 : 4;

    // Advance by real scheduled impacts rather than a timer; keep current upgrades and bonus meter.
    void TickPrototypeFelling()
    {
        if (TargetItem == null) return;
        int need = PrototypeFellingHits(TargetItem);
        if (Time.time >= nextHitAt)
        {
            nextHitAt = Time.time + Mathf.Max(.15f, hitInterval);
            float step = GameBalance.ChopTime * TargetItem.ChopMul / need;
            holdTime += step;
            // Animation-event setups already emit their own effect. Standard build uses scheduled hits.
            if (!useAnimationEvents) NotifyHit();
        }
        Progress = Mathf.Clamp01(holdTime / Mathf.Max(.1f,GatherTime));
        hitsDone = Mathf.FloorToInt(Progress * need + .0001f);
        if (progressUI != null) progressUI.Show(Progress,hitsDone,need);
        if (Progress >= .9999f) { CompleteChop(); return; }
        TickChopBonus();
    }

    // Called only after the old reward calculation (including double yield and silver XP).
    bool TryPrototypeFall(ResourceItem item,int wood,int xp,bool wasGolden)
    {
        if (!PrototypeStanding(item) || !ChopPrototypeLog.TryCreate(item,transform,wood,xp)) return false;
        // Exactly one felling event and no immediate resource payout. The pile owns the reward.
        if (leoImpactFrame != Time.frameCount) ReadLeoImpact(item);
        GameEvents.RaiseTreeChopped();
        if (wasGolden) SfxManager.Play("coin",.9f,.9f);
        savedItem=null;
        item.ChopDown();
        StopChop();
        clickArmed=false;
        armedLog=null;
        SaveIndicator.ShowToast("Дерево повалено — подойди к стволу и разделай его");
        FindTarget(true);
        return true;
    }

    ChopPrototypeLog PickPrototypeLog()
    {
        var camera=Camera.main;
        if (camera==null || !MouseActive() || OverUiButton(OrbitCamera.PointerPos)) return null;
        int n=Physics.RaycastNonAlloc(camera.ScreenPointToRay(OrbitCamera.PointerPos),prototypeHits,200f,~0,QueryTriggerInteraction.Ignore);
        float nearest=float.PositiveInfinity;
        Collider first=null;
        for(int i=0;i<n;i++)
        {
            var c=prototypeHits[i].collider;
            if(c==null || c.transform==transform || c.transform.IsChildOf(transform))continue;
            if(prototypeHits[i].distance>=nearest)continue;
            nearest=prototypeHits[i].distance;first=c;
        }
        if(first==null)return null;
        var log=first.GetComponentInParent<ChopPrototypeLog>();
        return log!=null && log.Ready && !log.Split && log.Distance(transform.position)<=radius+.15f ? log : null;
    }

    ChopPrototypeLog NearestPrototypeLog()
    {
        ChopPrototypeLog best=null;float dist=1.65f;
        foreach(var log in ChopPrototypeLog.Active)
        {
            if(log==null || !log.Ready || log.Split)continue;
            float d=log.Distance(transform.position);
            if(d>dist)continue;
            dist=d;best=log;
        }
        return best;
    }

    void StopPrototypeWork()
    {
        if (ReferenceEquals(processingLog,null)) return;
        processingLog=null;
        StopChop();
        CurrentTarget=null;TargetItem=null;
        findTimer=0f;lastTarget=null;lastItem=null;
    }

    // Shares the existing animator, movement lock, camera viewmodel and inventory reservation path.
    // This is a prototype: third-person uses the existing standing swing, not a finished ground-cut animation.
    bool TickPrototypeLog()
    {
        if (!PrototypeInputAllowed || !ChoppingEnabled)
        {
            armedLog=null;StopPrototypeWork();return false;
        }
        if (ChopPrototypeLog.Active.Count == 0)
        {
            armedLog=null;StopPrototypeWork();return false;
        }
        // Collect only after full processing, only within reach, only into actually free slots.
        for(int i=ChopPrototypeLog.Active.Count-1;i>=0;i--)
        {
            var log=ChopPrototypeLog.Active[i];
            if(log==null || !log.Ready || !log.Split || log.Distance(transform.position)>1.15f || inventory==null || inventory.FreeSlots<=0)continue;
            int got=GrantReward(log.transform.position+Vector3.up*.25f,log.Remaining,log.Xp);
            log.Collected(got);
        }
        var aim=PickPrototypeLog();
        if(Input.GetMouseButtonDown(0))armedLog=aim;
        if(!Input.GetMouseButton(0) || aim!=armedLog)armedLog=null;
        var wanted=armedLog;
        if(wanted==null && !OrbitCamera.FirstPersonActive && !InteractionHub.OwnsInput && MobileInput.ActionHeld)
            wanted=NearestPrototypeLog();
        if(wanted==null || wanted.Split)
        {
            StopPrototypeWork();
            return false;
        }
        if(processingLog!=wanted)
        {
            StopChop();
            processingLog=wanted;
            if(movement!=null)movement.CancelWalk();
            WalkTarget=null;leoWalkArrival=leoApproachAttempt=null;leoEnterRhythmOnArrival=false;
            clickArmed=false;HoverItem=null;
            TargetItem=null;CurrentTarget=wanted.transform;lastTarget=null;lastItem=null;
            IsChopping=true;waitingForChopAxe=true;
            holdTime=0f;
            Progress=wanted.Progress;
        }
        if(!LeoAxeReady)return true;
        if(waitingForChopAxe)
        {
            waitingForChopAxe=false;
            nextHitAt=Time.time+Mathf.Max(.05f,firstHitDelay);
            ResolveChopParam();
            if(animator!=null && !string.IsNullOrEmpty(chopBool))animator.SetBool(chopBool,true);
            if(animator!=null && !string.IsNullOrEmpty(chopTrigger))animator.SetTrigger(chopTrigger);
        }
        if(Time.time>=nextHitAt)
        {
            nextHitAt=Time.time+Mathf.Max(.15f,hitInterval);
            float strength=upgrades!=null ? Mathf.Max(1f,GameBalance.ChopTime/Mathf.Max(.1f,upgrades.ChopTime)) : 1f;
            leoImpactFrame=Time.frameCount;leoHasImpact=TryReadPrototypeContact(out leoImpact);
            GameEvents.RaiseChopHit();
            bool finished=wanted.Hit(strength);
            Progress=wanted.Progress;
            if(finished)
            {
                StopPrototypeWork();armedLog=null;
                SaveIndicator.ShowToast("Ствол разделан — подойди к брёвнам, чтобы собрать");
            }
        }
        return true;
    }
}
