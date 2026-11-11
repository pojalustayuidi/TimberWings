using System;
using UnityEngine;
using UnityEngine.UI;

// Optional presentation layer. Existing dialogue, voice, history and buttons retain ownership.
[DefaultExecutionOrder(410),DisallowMultipleComponent]
public sealed class NpcDialoguePortraits : MonoBehaviour
{
    public DialoguePolishView view;
    public RectTransform decoration;
    public Image portrait,progressFill;
    public RectTransform progressRoot;
    public Image[] pips;
    public Sprite mira,oskar,leo,rico;
    public string miraName="Мира",oskarName="Оскар";
    string lastSpeaker;
    float pop,fill;
    bool initialized,shownBefore;
    RectState bodyRest,avatarRest,nextRest,skipRest,historyRest,autoRest,nameRest,roleRest,countRest;
    bool roleActive,autoActive,initialEnabled;
    float nameFont;
    sealed class RectState
    {
        readonly RectTransform rect;readonly Vector2 min,max,pivot,position,size;
        public RectState(RectTransform target){rect=target;min=target.anchorMin;max=target.anchorMax;pivot=target.pivot;position=target.anchoredPosition;size=target.sizeDelta;}
        public void Restore(){if(rect!=null){rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;}}
    }
    public Sprite PortraitFor(string name)
    {
        if(string.Equals(name,miraName,StringComparison.OrdinalIgnoreCase))return mira;
        if(string.Equals(name,oskarName,StringComparison.OrdinalIgnoreCase))return oskar;
        if(string.Equals(name,RicoMerchant.DisplayName,StringComparison.OrdinalIgnoreCase))return Resources.Load<Sprite>("Rico/RicoPortrait");
        return null;
    }
    void Initialize()
    {
        if(initialized||view==null||view.body==null||view.avatarBack==null)return;
        bodyRest=new RectState(view.body.rectTransform);avatarRest=new RectState(view.avatarBack.rectTransform);
        nextRest=new RectState(view.nextButton.transform as RectTransform);skipRest=new RectState(view.skipButton.transform as RectTransform);
        if(view.historyButton!=null)historyRest=new RectState(view.historyButton.transform as RectTransform);
        if(view.autoButton!=null){autoRest=new RectState(view.autoButton.transform as RectTransform);autoActive=view.autoButton.gameObject.activeSelf;}
        nameRest=new RectState(view.speakerName.rectTransform);roleRest=new RectState(view.role.rectTransform);countRest=new RectState(view.progress.rectTransform);
        roleActive=view.role.gameObject.activeSelf;initialEnabled=view.monogram.enabled;nameFont=view.speakerName.fontSize;initialized=true;
    }
    void OnEnable(){lastSpeaker=null;shownBefore=false;if(decoration!=null)decoration.gameObject.SetActive(true);}
    void OnDisable()
    {
        if(decoration!=null)decoration.gameObject.SetActive(false);
        if(portrait!=null)portrait.enabled=false;
        if(!initialized||view==null)return;
        bodyRest.Restore();avatarRest.Restore();nextRest.Restore();skipRest.Restore();historyRest?.Restore();autoRest?.Restore();nameRest.Restore();roleRest.Restore();countRest.Restore();
        if(view.monogram!=null)view.monogram.enabled=initialEnabled;
        if(view.role!=null)view.role.gameObject.SetActive(roleActive);
        if(view.autoButton!=null)view.autoButton.gameObject.SetActive(autoActive);
        if(view.speakerName!=null)view.speakerName.fontSize=nameFont;
        if(view.skipButton!=null)view.skipButton.gameObject.SetActive(DialogueSequence.ActiveLineCount>1);
        view.Layout();
    }
    void LateUpdate()
    {
        Initialize();if(!initialized||portrait==null||view.panel==null)return;
        bool shown=view.group!=null&&view.group.alpha>.01f;
        string who=view.speakerName.text;
        if(lastSpeaker!=who||shown&&!shownBefore)
        {
            lastSpeaker=who;portrait.sprite=PortraitFor(who);portrait.enabled=portrait.sprite!=null;
            view.monogram.enabled=portrait.sprite==null;pop=VibeSettings.ReduceEffects?0:1;
            // A new actor starts at the actual line index; only subsequent progress interpolates.
            fill=Ratio(DialogueSequence.ActiveLine,DialogueSequence.ActiveLineCount);
        }
        shownBefore=shown;if(!shown)return;
        float dt=Mathf.Min(.1f,Time.unscaledDeltaTime);pop=Mathf.MoveTowards(pop,0,dt*4f);
        portrait.rectTransform.localScale=Vector3.one*(1+pop*.035f);
        float target=Ratio(DialogueSequence.ActiveLine,DialogueSequence.ActiveLineCount);
        fill=Mathf.MoveTowards(fill,target,dt*3f);if(progressFill!=null)progressFill.fillAmount=fill;
        int total=DialogueSequence.ActiveLineCount,line=DialogueSequence.ActiveLine;
        if(progressRoot!=null)progressRoot.gameObject.SetActive(total>1);
        if(pips!=null)for(int i=0;i<pips.Length;i++)if(pips[i]!=null)
        {
            bool visible=total>1&&total<=pips.Length&&i<total;pips[i].gameObject.SetActive(visible);
            pips[i].color=i<line?new Color(.32f,.63f,.96f,1):new Color(.62f,.65f,.86f,.65f);
        }
        Layout();
    }
    public static float Ratio(int line,int count){return count>0?Mathf.Clamp01((float)line/count):0;}
    void Layout()
    {
        float width=view.panel.rect.width;bool narrow=width<600f;
        float left=narrow?104f:138f,portraitSize=narrow?72f:86f;
        Pin(view.avatarBack.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(narrow?16:28,-18),new Vector2(portraitSize,portraitSize));
        float nameRight=narrow?112:width>=940?390:240;
        Pin(view.speakerName.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(left,-22),new Vector2(Mathf.Max(80,width-left-nameRight),38));
        view.speakerName.fontSize=narrow?25:nameFont;
        view.role.gameObject.SetActive(roleActive&&!narrow);
        Pin(view.role.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(left,-62),new Vector2(Mathf.Max(80,width-left-360),28));
        if(view.autoButton!=null)view.autoButton.gameObject.SetActive(autoActive&&!narrow);
        Pin(view.progress.rectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(narrow?-16:-32,-62),new Vector2(80,24));
        if(progressRoot!=null)Pin(progressRoot,new Vector2(1,1),new Vector2(1,1),new Vector2(narrow?-16:-32,-96),new Vector2(narrow?92:194,8));
        float top=narrow?112:120,bottom=narrow?128:84;
        float preferred=view.body.GetPreferredValues(view.body.text,Mathf.Max(180,width-64),0).y;
        var size=view.panel.sizeDelta;size.y=Mathf.Clamp(top+bottom+preferred+8,narrow?320:272,520);view.panel.sizeDelta=size;
        view.body.rectTransform.anchorMin=Vector2.zero;view.body.rectTransform.anchorMax=Vector2.one;
        view.body.rectTransform.offsetMin=new Vector2(32,bottom);view.body.rectTransform.offsetMax=new Vector2(-32,-top);
        if(narrow)
        {
            Pin(view.nextButton.transform as RectTransform,new Vector2(1,0),new Vector2(1,0),new Vector2(-16,16),new Vector2(width-32,40));
            float half=(width-44)*.5f;
            Pin(view.skipButton.transform as RectTransform,new Vector2(0,0),Vector2.zero,new Vector2(16,64),new Vector2(half,34));
            if(view.historyButton!=null)Pin(view.historyButton.transform as RectTransform,new Vector2(1,0),new Vector2(1,0),new Vector2(-16,64),new Vector2(half,34));
            if(view.hintGroup!=null)view.hintGroup.alpha=0;
            if(view.holdGroup!=null)view.holdGroup.alpha=0;
        }
        else
        {
            nextRest.Restore();skipRest.Restore();historyRest?.Restore();
            if(width<840&&view.hintGroup!=null)view.hintGroup.alpha=0;
            // The existing hint group is controlled by DialogueManager; leave its alpha alone.
        }
    }
    static void Pin(RectTransform rect,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;}
}
