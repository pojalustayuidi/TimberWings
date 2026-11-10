using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class TWDialoguePolishBuilder
{
    static void BuildHistory(DialoguePolishView view,RectTransform panel,TMP_FontAsset font)
    {
        // Three distinct footer buttons. The whole panel remains a single interaction surface.
        Pin(view.skipButton.transform as RectTransform,new Vector2(1,0),new Vector2(1,0),new Vector2(-326,24),new Vector2(132,44));
        TMP_Text label;
        view.historyButton=Button("History",panel,font,"Журнал","Buttons/btn_blue_normal",new Vector2(-202,24),new Vector2(110,44),out label); label.fontSize=18;
        view.autoButton=Button("Auto",panel,font,"Авто: вкл","Buttons/btn_blue_normal",new Vector2(-140,0),new Vector2(124,32),out label); view.autoLabel=label; label.fontSize=16;
        Pin(view.autoButton.transform as RectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-140,-60),new Vector2(124,32));
        var hint=view.hintGroup.transform as RectTransform;FillHorizontal(hint,148,24,476,36);view.hint.fontSize=15;
        var history=UI("DialogueHistory",panel); view.historyRoot=history;
        history.anchorMin=new Vector2(0,1);history.anchorMax=new Vector2(1,1);history.pivot=new Vector2(.5f,0);history.anchoredPosition=new Vector2(0,10);history.sizeDelta=new Vector2(0,310);
        var backdrop=Image(history,"Panels/panel_window",Color.white);backdrop.raycastTarget=true;
        var title=Text("Title",history,font,25,Ink);Pin(title.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-18),new Vector2(400,34));title.text="Последние реплики";
        view.historyClose=Button("Close",history,font,"Закрыть","Buttons/btn_blue_normal",new Vector2(-24,-18),new Vector2(110,32),out label);
        Pin(view.historyClose.transform as RectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-18),new Vector2(110,32));label.fontSize=17;
        var viewport=UI("Viewport",history);Fill(viewport,28,22,28,62);Undo.AddComponent<RectMask2D>(viewport.gameObject);
        var surface=Image(viewport,"Panels/panel_chip",new Color(1,1,1,.01f));surface.raycastTarget=true;
        view.historyText=Text("Entries",viewport,font,23,Ink);view.historyText.fontStyle=FontStyles.Normal;view.historyText.textWrappingMode=TextWrappingModes.Normal;view.historyText.overflowMode=TextOverflowModes.Overflow;
        var content=view.historyText.rectTransform;content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
        var fitter=Undo.AddComponent<ContentSizeFitter>(content.gameObject);fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=Undo.AddComponent<ScrollRect>(viewport.gameObject);scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;view.historyScroll=scroll;
        history.gameObject.SetActive(false);
    }
    static GameObject BuildThoughts(Transform parent,TMP_FontAsset font,StoryFlow story)
    {
        var root=UI("PlayerThoughts",parent);var canvas=Undo.AddComponent<Canvas>(root.gameObject);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=UiLayers.Dialogue;
        var scaler=Undo.AddComponent<CanvasScaler>(root.gameObject);scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var panel=UI("ThoughtCard",root);Pin(panel,new Vector2(0,0),new Vector2(0,0),new Vector2(30,116),new Vector2(510,164));Image(panel,"Panels/panel_toast",Color.white);
        var group=Undo.AddComponent<CanvasGroup>(panel.gameObject);group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
        var caption=Text("Caption",panel,font,18,Muted);Pin(caption.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-16),new Vector2(454,26));caption.text="Лео · про себя";
        var text=Text("Text",panel,font,25,Ink);text.fontStyle=FontStyles.Normal;text.textWrappingMode=TextWrappingModes.Normal;Fill(text.rectTransform,28,20,28,47);text.text="Здесь даже ветер тише.";
        var controller=Undo.AddComponent<PlayerThoughtController>(root.gameObject);controller.story=story;controller.intro=One<IntroFlow>(story.gameObject.scene);controller.group=group;controller.body=text;controller.caption=caption;
        var spoken=Undo.AddComponent<AudioSource>(root.gameObject);spoken.playOnAwake=false;spoken.spatialBlend=0;controller.voice=spoken;
        var syllables=Undo.AddComponent<AudioSource>(root.gameObject);syllables.playOnAwake=false;syllables.spatialBlend=0;
        var babble=Undo.AddComponent<DialogueBabble>(root.gameObject);babble.source=syllables;controller.babble=babble;
        // Optional location hook: absence of the zone never cancels the dialogue builder.
        foreach(var aura in All<MonoBehaviour>(story.gameObject.scene))if(aura!=null&&aura.gameObject.activeInHierarchy&&aura.GetType().Name=="MysticAtmosphere")
        {
            controller.mysticAnchor=aura.transform;
            var serialized=new SerializedObject(aura);var radius=serialized.FindProperty("radius");
            if(radius!=null)controller.mysticRadius=Mathf.Max(7,radius.floatValue*.78f);
            break;
        }
        if(controller.mysticAnchor==null)foreach(var component in All<MonoBehaviour>(story.gameObject.scene))if(component!=null&&component.GetType().Name=="MysticGladeVibe"){controller.mysticAnchor=component.transform;break;}
        EditorUtility.SetDirty(controller);return root.gameObject;
    }
}
