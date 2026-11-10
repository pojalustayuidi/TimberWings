using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class TWMiraLifeBuilder
{
    public const string Menu="Tools/BuilderSim/NPC/Мира — жизнь/";
    const string Kit="Assets/_Project/Art/UI/CandyKit/";
    [MenuItem(Menu+"1. Применить первый патч",false,280)]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("MIRA: останови Play.");return;}
        int undo=-1;
        try
        {
            var scene=SceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Сначала сохрани сцену.");
            Speaker mira=null;
            foreach(var s in All<Speaker>(scene))if(s.isActiveAndEnabled&&string.Equals(s.displayName.Trim(),"Мира",StringComparison.OrdinalIgnoreCase))
            {if(mira!=null)throw new InvalidOperationException("Найдено несколько активных Speaker «Мира». Выбери одну Миру в сцене.");mira=s;}
            if(mira==null)throw new InvalidOperationException("Не найдена Мира со Speaker «Мира».");
            var seq=mira.GetComponent<DialogueSequence>();if(seq==null)throw new InvalidOperationException("На Мире нет DialogueSequence. Сначала настрой старт игры.");
            MiraRig rig=null;
            foreach(var r in mira.GetComponentsInChildren<MiraRig>(true))if(r.gameObject.activeInHierarchy)
            {if(rig!=null)throw new InvalidOperationException("У Миры несколько активных MiraRig. Оставь одну модель.");rig=r;}
            if(rig==null)throw new InvalidOperationException("Нет процедурной модели MiraRig. Сначала собери модель Миры существующим сборщиком.");
            if(rig.transform==mira.transform)throw new InvalidOperationException("MiraRig должен быть на дочерней модели, чтобы не вращать коллайдеры и точку задания.");
            var font=TMP_Settings.defaultFontAsset;foreach(var ui in All<UIManager>(scene))if(ui.coinText!=null&&ui.coinText.font!=null){font=ui.coinText.font;break;}
            if(font==null)throw new InvalidOperationException("Нет TMP-шрифта с кириллицей. Назначь шрифт HUD.");
            if(AssetDatabase.LoadAssetAtPath<DialogueAudioLibrary>("Assets/_Project/Resources/DialoguePolish/AudioLibrary.asset")==null)
                throw new InvalidOperationException("Сначала примени последний патч диалогов: Tools > BuilderSim > UI > Диалоги и NPC > 1. Затем повтори сборку Миры.");
            Sprite panel=Need(Kit+"Panels/panel_toast.png"),talk=Need(Kit+"Icons/icon_star.png"),logs=Need(Kit+"Icons/icon_log.png"),report=Need(Kit+"Icons/icon_check.png"),chat=Need("Assets/_Project/Art/UI/MiraLife/icon_chat.png");
            var data=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Resources/MiraLife/MiraLines.json");
            if(data==null||JsonUtility.FromJson<MiraPresence.Book>(data.text).lines==null)throw new InvalidOperationException("Нет MiraLines.json. Скопируй Assets целиком.");
            string directory="UserSettings/TW_MiraBackups/"+scene.name;Directory.CreateDirectory(directory);
            string backup=directory+"/"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Не удалось сохранить копию сцены.");
            Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Mira Life patch 1");
            MiraLifeSetup setup=null;
            foreach(var item in All<MiraLifeSetup>(scene))if(item.target==mira){if(setup!=null)throw new InvalidOperationException("Дубли MiraLifeSetup. Верни оформление перед повторной сборкой.");setup=item;}
            if(setup==null)
            {
                var go=new GameObject("TW_MiraLife");go.transform.SetParent(mira.transform,false);Undo.RegisterCreatedObjectUndo(go,"Mira Life");
                setup=Undo.AddComponent<MiraLifeSetup>(go);setup.target=mira;
            }
            Undo.RegisterCompleteObjectUndo(setup,"Mira Life snapshot");setup.rig=rig;setup.backup=backup;
            if(!setup.captured)
            {
                setup.captured=true;setup.previousFace=mira.faceNearbyPlayer;setup.previousBubble=mira.bubbleOffset;setup.previousMarker=seq.marker;
                setup.marker=seq.marker!=null?seq.marker:mira.GetComponentInChildren<NpcMarker>(true);
                if(setup.marker!=null){setup.previousMarkerSize=setup.marker.size;setup.previousMarkerBob=setup.marker.bobHeight;setup.previousMarkerPosition=setup.marker.transform.localPosition;setup.previousSprite=setup.marker.GetComponent<SpriteRenderer>().sprite;}
            }
            ClearGenerated(setup);
            foreach(var life in mira.GetComponentsInChildren<NpcLife>(true))Disable(setup,life);
            foreach(var banter in mira.GetComponentsInChildren<NpcBanter>(true))Disable(setup,banter);
            foreach(var animator in rig.GetComponentsInChildren<Animator>(true))Disable(setup,animator);
            Undo.RecordObject(rig,"Mira joint binding");rig.Bind();
            if(rig.head==null||rig.spine==null||rig.handR==null||rig.eyeL==null||rig.eyeR==null)throw new InvalidOperationException("В MiraRig не хватает суставов. Сборка отменена, модель не заменялась.");
            Undo.RecordObject(mira,"Mira look ownership");mira.faceNearbyPlayer=false;
            float top=mira.transform.position.y+2f;
            foreach(var renderer in rig.GetComponentsInChildren<Renderer>())if(renderer.enabled)top=Mathf.Max(top,renderer.bounds.max.y);
            mira.bubbleOffset=Vector3.up*(top-mira.transform.position.y+.18f);
            var marker=setup.marker;
            if(marker==null)
            {
                var go=new GameObject("Mira_QuestMarker");go.transform.SetParent(mira.transform,false);Undo.RegisterCreatedObjectUndo(go,"Mira marker");setup.generated.Add(go);
                Undo.AddComponent<SpriteRenderer>(go).sprite=talk;marker=Undo.AddComponent<NpcMarker>(go);
            }
            Undo.RecordObject(marker,"Mira compact marker");Undo.RecordObject(marker.transform,"Mira marker height");Undo.RecordObject(marker.GetComponent<SpriteRenderer>(),"Mira marker icon");
            marker.size=.48f;marker.bobHeight=.035f;marker.transform.position=mira.transform.position+mira.bubbleOffset+Vector3.up*.28f;marker.GetComponent<SpriteRenderer>().sprite=talk;
            Undo.RecordObject(seq,"Mira marker reference");seq.marker=marker;
            var presence=Undo.AddComponent<MiraPresence>(mira.gameObject);setup.added.Add(presence);setup.presence=presence;
            var motion=Undo.AddComponent<MiraMotion>(rig.gameObject);setup.added.Add(motion);setup.motion=motion;
            presence.speaker=mira;presence.dialogue=seq;presence.motion=motion;presence.marker=marker;
            presence.radius=seq.radius;presence.talkIcon=talk;presence.deliverIcon=logs;presence.reportIcon=report;presence.chatIcon=chat;
            presence.story=One<StoryFlow>(scene);presence.intro=One<IntroFlow>(scene);presence.thoughts=One<PlayerThoughtController>(scene);
            motion.rig=rig;motion.speaker=mira;motion.dialogue=seq;motion.presence=presence;
            motion.manager=One<DialogueManager>(scene);
            BuildCard(setup,presence,font,panel);
            if(rig.mug!=null)BuildStamp(setup,rig.mug);
            EditorUtility.SetDirty(rig);EditorUtility.SetDirty(mira);EditorUtility.SetDirty(seq);EditorUtility.SetDirty(presence);EditorUtility.SetDirty(motion);EditorUtility.SetDirty(setup);
            var errors=Inspect(setup);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=setup.gameObject;
            Debug.Log("MIRA LIFE 1: готово. Дыхание, моргание, взгляд, жесты, горшочек, компактные цели и подпись komarov. Старые NpcLife/NpcBanter Миры выключены с сохранением настроек. Ctrl+S. Копия сцены: "+backup,setup);
        }
        catch(Exception ex){if(undo>=0)Undo.RevertAllDownToGroup(undo);Debug.LogError("MIRA LIFE: изменения сцены отменены. "+ex);}
    }
    static void Disable(MiraLifeSetup setup,Behaviour target)
    {
        if(!setup.disabled.Exists(x=>x.target==target))setup.disabled.Add(new MiraLifeSetup.BehaviourState{target=target,enabled=target.enabled});
        Undo.RecordObject(target,"Mira old animation");target.enabled=false;
    }
    static void ClearGenerated(MiraLifeSetup setup)
    {
        for(int i=setup.added.Count-1;i>=0;i--)if(setup.added[i]!=null)Undo.DestroyObjectImmediate(setup.added[i]);setup.added.Clear();
        for(int i=setup.generated.Count-1;i>=0;i--)if(setup.generated[i]!=null)Undo.DestroyObjectImmediate(setup.generated[i]);setup.generated.Clear();
    }
    static void BuildCard(MiraLifeSetup setup,MiraPresence presence,TMP_FontAsset font,Sprite panel)
    {
        var root=UI("Mira_Comments",setup.transform);setup.generated.Add(root.gameObject);
        var canvas=Undo.AddComponent<Canvas>(root.gameObject);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=UiLayers.Dialogue;
        var scaler=Undo.AddComponent<CanvasScaler>(root.gameObject);scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        // No raycaster: the card cannot intercept a click or become a modal.
        var card=UI("MiraCard",root);card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);card.sizeDelta=new Vector2(440,140);
        var image=Undo.AddComponent<Image>(card.gameObject);image.sprite=panel;image.type=Image.Type.Sliced;image.raycastTarget=false;
        var group=Undo.AddComponent<CanvasGroup>(card.gameObject);group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
        presence.card=card;presence.group=group;
        presence.caption=Text("Caption",card,font,18,new Color32(94,107,80,255));Place(presence.caption.rectTransform,22,104,396,24);presence.caption.text="Мира · Хранительница лагеря";
        presence.body=Text("Line",card,font,24,CandyTag.Ink);Place(presence.body.rectTransform,22,15,396,84);presence.body.text="В лагере снова тепло.";presence.body.textWrappingMode=TextWrappingModes.Normal;
        var voice=Undo.AddComponent<AudioSource>(root.gameObject);voice.playOnAwake=false;presence.voice=voice;
        var syllables=Undo.AddComponent<AudioSource>(root.gameObject);syllables.playOnAwake=false;
        var babble=Undo.AddComponent<DialogueBabble>(root.gameObject);babble.source=syllables;presence.babble=babble;
    }
    static void BuildStamp(MiraLifeSetup setup,Transform mug)
    {
        var go=new GameObject("Komarov_MakersMark");go.transform.SetParent(mug,false);Undo.RegisterCreatedObjectUndo(go,"komarov maker mark");setup.generated.Add(go);
        go.transform.localPosition=new Vector3(0,-.505f,0);go.transform.localRotation=Quaternion.Euler(90,0,0);
        var text=Undo.AddComponent<TextMesh>(go);text.text="komarov";text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=64;text.characterSize=.07f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color32(74,44,33,255);
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=text.font.material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    static RectTransform UI(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Mira UI");return go.transform as RectTransform;}
    static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,float size,Color color)
    {var r=UI(name,parent);var t=Undo.AddComponent<TextMeshProUGUI>(r.gameObject);t.font=font;t.fontSize=size;t.color=color;t.raycastTarget=false;t.richText=false;t.overflowMode=TextOverflowModes.Ellipsis;t.alignment=TextAlignmentOptions.MidlineLeft;return t;}
    static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
    static Sprite Need(string path){var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite==null)throw new InvalidOperationException("Нет UI-спрайта: "+path);return sprite;}
    static List<T> All<T>(Scene scene)where T:Component{var list=new List<T>();foreach(var root in scene.GetRootGameObjects())list.AddRange(root.GetComponentsInChildren<T>(true));return list;}
    static T One<T>(Scene scene)where T:Component{T found=null;foreach(var item in All<T>(scene))if(item.gameObject.activeInHierarchy){if(found!=null)throw new InvalidOperationException("Несколько активных "+typeof(T).Name+" в сцене.");found=item;}return found;}
    internal static List<string> Inspect(MiraLifeSetup setup)
    {
        var errors=new List<string>();
        if(setup==null||setup.target==null||setup.rig==null){errors.Add("Нет MiraLifeSetup / Speaker / MiraRig.");return errors;}
        if(setup.presence==null||setup.motion==null)errors.Add("Нет MiraPresence / MiraMotion.");
        else
        {
            var p=setup.presence;if(p.group==null||p.card==null||p.body==null||p.babble==null||p.voice==null)errors.Add("Не собрана карточка/речь.");
            if(p.group!=null&&(p.group.blocksRaycasts||p.group.interactable))errors.Add("Карточка перехватывает ввод.");
            if(p.marker==null||p.marker.size>.65f)errors.Add("Нет компактного значка.");
        }
        if(setup.target.faceNearbyPlayer)errors.Add("Speaker и MiraMotion одновременно вращают модель.");
        foreach(var b in setup.disabled)if(b.target!=null&&b.target.enabled)errors.Add("Снова включён старый аниматор: "+b.target.GetType().Name);
        if(setup.target.GetComponents<MiraPresence>().Length!=1||setup.rig.GetComponents<MiraMotion>().Length!=1)errors.Add("Дубли компонентов Миры.");
        if(setup.rig.eyeL==null||setup.rig.eyeR==null||setup.rig.eyeL.GetComponent<MeshFilter>()!=null||setup.rig.eyeR.GetComponent<MeshFilter>()!=null)errors.Add("Глаза привязаны к мешам вместо суставов.");
        if(setup.rig.mug!=null&&!setup.generated.Exists(x=>x!=null&&x.name=="Komarov_MakersMark"))errors.Add("Нет подписи komarov.");
        return errors;
    }
    [MenuItem(Menu+"2. Проверить сцену",false,281)]
    static void QA()
    {
        var setups=All<MiraLifeSetup>(SceneManager.GetActiveScene());if(setups.Count==0){Debug.LogError("MIRA QA: сначала примени первый патч.");return;}
        foreach(var setup in setups){var errors=Inspect(setup);if(errors.Count>0)Debug.LogError("MIRA QA: "+string.Join("\n",errors));else Debug.Log("MIRA QA: ссылки, риг, карточка, значок и отсутствие двойной анимации проверены. Движения, сдачу и камеру проверь в Play Mode.");}
    }
    [MenuItem(Menu+"4. Вернуть прежнее оформление",false,283)]
    static void Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("MIRA: останови Play.");return;}
        var scene=SceneManager.GetActiveScene();Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Restore Mira presentation");
        foreach(var setup in All<MiraLifeSetup>(scene))
        {
            ClearGenerated(setup);
            if(setup.target!=null)
            {
                Undo.RecordObject(setup.target,"Restore Mira Speaker");setup.target.faceNearbyPlayer=setup.previousFace;setup.target.bubbleOffset=setup.previousBubble;
                var seq=setup.target.GetComponent<DialogueSequence>();if(seq!=null){Undo.RecordObject(seq,"Restore Mira marker");seq.marker=setup.previousMarker;seq.reminderHandledExternally=false;}
            }
            if(setup.marker!=null){Undo.RecordObject(setup.marker,"Restore marker");Undo.RecordObject(setup.marker.transform,"Restore marker position");var sr=setup.marker.GetComponent<SpriteRenderer>();Undo.RecordObject(sr,"Restore marker sprite");setup.marker.size=setup.previousMarkerSize;setup.marker.bobHeight=setup.previousMarkerBob;setup.marker.transform.localPosition=setup.previousMarkerPosition;sr.sprite=setup.previousSprite;}
            foreach(var b in setup.disabled)if(b.target!=null){Undo.RecordObject(b.target,"Restore Mira behaviour");b.target.enabled=b.enabled;}
            Undo.DestroyObjectImmediate(setup.gameObject);
        }
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Debug.Log("MIRA: прежнее оформление возвращено. Задания, ресурсы и сохранения не менялись. Ctrl+S.");
    }
}
