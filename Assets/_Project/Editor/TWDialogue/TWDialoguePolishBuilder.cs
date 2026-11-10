using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class TWDialoguePolishBuilder
{
    public const string Menu = "Tools/BuilderSim/UI/Диалоги и NPC/";
    public const string Root = "Assets/_Project/Resources/DialoguePolish";
    const string Audio = "Assets/_Project/Art/Audio/DialoguePolish";
    static Color Ink { get { return CandyTag.Ink; } }
    static Color Muted { get { return CandyTag.Muted; } }

    [MenuItem(Menu + "1. Применить диалоги и торговлю у лавки", false, 260)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("DIALOGUE POLISH: останови Play."); return; }
        var scene = SceneManager.GetActiveScene(); int undo = -1;
        try
        {
            if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Сначала сохрани игровую сцену.");
            var story = One<StoryFlow>(scene); var ui = One<UIManager>(scene);
            if (story == null || ui == null || ui.coinText == null || ui.coinText.font == null) throw new InvalidOperationException("Нужны StoryFlow и HUD с назначенным шрифтом.");
            var market = story.market != null ? story.market : StoryFlow.FindSite(StoryFlow.Site.Market, All<BuildingController>(scene).ToArray());
            if (market == null) throw new InvalidOperationException("Не найден рынок. Назначь StoryFlow.market: перенос торговли без здания отменён.");
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Script.json") == null) throw new InvalidOperationException("Нет сценария Script.json. Скопируй Assets целиком и дождись импорта.");
            // Resolve all sprites before making scene changes.
            Need("Panels/panel_window"); Need("Panels/panel_chip"); Need("Buttons/btn_mint_normal"); Need("Buttons/btn_blue_normal");
            string directory = "UserSettings/TW_DialogueBackups/" + scene.name; Directory.CreateDirectory(directory);
            string backup = directory + "/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Не удалось сохранить копию сцены.");
            Undo.IncrementCurrentGroup(); undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Dialogue Polish");
            var setup = One<DialoguePolishSetup>(scene);
            if (setup == null) { var go = new GameObject("TW_DialoguePolish"); Undo.RegisterCreatedObjectUndo(go, "Dialogue Polish"); setup = Undo.AddComponent<DialoguePolishSetup>(go); }
            Undo.RegisterCompleteObjectUndo(setup, "Dialogue Polish snapshot"); setup.backup = backup;
            var manager = setup.manager != null ? setup.manager : One<DialogueManager>(scene);
            if (manager == null) { manager = Undo.AddComponent<DialogueManager>(setup.gameObject); setup.ownsManager = true; }
            if (!setup.captured)
            {
                setup.captured = true; setup.previousView = manager.presentation; setup.previousBabble = manager.babble;
                setup.previousBabbleSource = manager.babble != null ? manager.babble.source : null;
                setup.previousBuildingTrade = story.tradeAtBuilding; setup.previousMarketTrading = story.marketTrading; setup.previousMerchant = story.merchant;
            }
            if (setup.viewRoot != null) Undo.DestroyObjectImmediate(setup.viewRoot);
            if (setup.thoughtsRoot != null) Undo.DestroyObjectImmediate(setup.thoughtsRoot);
            setup.manager = manager; setup.story = story;
            var library = MakeLibrary();
            var view = BuildView(setup.transform, ui.coinText.font, library); setup.viewRoot = view.gameObject;
            Undo.RecordObject(manager, "Dialogue Polish view"); manager.presentation = view;
            if (manager.babble == null) { manager.babble = Undo.AddComponent<DialogueBabble>(manager.gameObject); setup.ownsBabble = true; }
            Undo.RecordObject(manager.babble, "Dialogue Polish voice");
            var voice = Undo.AddComponent<AudioSource>(view.gameObject); voice.playOnAwake = false; voice.spatialBlend = 0; voice.volume = 0;
            manager.babble.source = voice;
            setup.thoughtsRoot = BuildThoughts(setup.transform, ui.coinText.font, story);
            foreach (var follow in All<CameraFollow>(scene))
                if (follow.GetComponent<Camera>() != null && follow.GetComponent<Camera>().CompareTag("MainCamera") && follow.GetComponent<CameraVibe>() == null)
                    setup.addedComponents.Add(Undo.AddComponent<CameraVibe>(follow.gameObject));
            Undo.RecordObject(story, "Dialogue Polish market"); story.market = market; story.tradeAtBuilding = true;
            if (setup.counterRoot == null)
            {
                setup.counterRoot = new GameObject("TW_TradeCounter"); Undo.RegisterCreatedObjectUndo(setup.counterRoot, "Trade at building");
                setup.counterRoot.transform.SetParent(market.transform, false);
            }
            var stall = setup.counterRoot.GetComponent<TradingStall>(); if (stall == null) stall = Undo.AddComponent<TradingStall>(setup.counterRoot);
            Undo.RecordObject(stall, "Trade at building"); stall.prompt = "открыть продажу"; stall.promptOffset = new Vector3(0, 1.8f, 0); stall.requireIntroDone = true; stall.radius = 3f;
            story.marketTrading = stall;
            foreach (var speaker in All<Speaker>(scene)) if (string.Equals(speaker.displayName, story.merchantName, StringComparison.OrdinalIgnoreCase))
            {
                if (story.merchant == null) story.merchant = speaker.gameObject;
                bool separate = speaker.gameObject != market.gameObject && !market.transform.IsChildOf(speaker.transform);
                if (separate) DisableObject(setup, speaker.gameObject);
                else foreach (var component in speaker.GetComponents<Behaviour>())
                    if (component is Speaker || component is DialogueSequence || component is NpcBanter || component is TradingStall || component is NpcMarker) Disable(setup, component);
            }
            if (One<EventSystem>(scene) == null)
            {
                var input = new GameObject("Dialogue_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                input.transform.SetParent(setup.transform, false); Undo.RegisterCreatedObjectUndo(input, "Dialogue UI input");
            }
            EditorUtility.SetDirty(manager); EditorUtility.SetDirty(story); EditorUtility.SetDirty(setup); EditorUtility.SetDirty(manager.babble);
            var errors = Inspect(setup); if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = setup.gameObject;
            Debug.Log("DIALOGUE POLISH 2: готово. Камера разговора, журнал, автопереход, мысли Лео и редкая помощь. Выбор речи — в настройках. Ctrl+S. Копия сцены: " + backup);
        }
        catch (Exception ex) { if (undo >= 0) Undo.RevertAllDownToGroup(undo); Debug.LogError("DIALOGUE POLISH: изменения сцены отменены. " + ex); }
    }

    static DialogueAudioLibrary MakeLibrary()
    {
        string path = Root + "/AudioLibrary.asset";
        var library = AssetDatabase.LoadAssetAtPath<DialogueAudioLibrary>(path);
        if (library == null) { library = ScriptableObject.CreateInstance<DialogueAudioLibrary>(); AssetDatabase.CreateAsset(library, path); }
        Undo.RecordObject(library, "Dialogue voice library");
        var entries = new List<DialogueAudioLibrary.Voice>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Audio + "/Spoken" }))
        {
            string clipPath = AssetDatabase.GUIDToAssetPath(guid);
            entries.Add(new DialogueAudioLibrary.Voice { id = Path.GetFileNameWithoutExtension(clipPath), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath) });
        }
        if (entries.Count == 0) Debug.LogWarning("DIALOGUE VOICE: записей нет. Доступны текст и бормотание. Проверь импорт WAV из архива.");
        var actors = new List<DialogueAudioLibrary.Character>();
        string[] keys = { "mira", "leo", "oskar" }, roles = { "Хранительница лагеря", "Племянник Гуннара", "Мастер и друг Гуннара" }, icons = { "star", "axe", "saw" };
        Color[] colors = { new Color(.77f,.96f,.84f), new Color(.82f,.85f,1f), new Color(1f,.88f,.68f) };
        for (int i = 0; i < keys.Length; i++)
        {
            var syllables = new AudioClip[6]; for (int n = 0; n < 6; n++)
            {
                syllables[n] = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "/Babble/" + keys[i] + "_" + (n + 1).ToString("00") + ".wav");
                if (syllables[n] == null) throw new InvalidOperationException("Нет слога бормотания " + keys[i] + "/" + (n + 1));
            }
            actors.Add(new DialogueAudioLibrary.Character { key = keys[i], role = roles[i], syllables = syllables, tint = colors[i], emblem = Need("Icons/icon_" + icons[i]) });
        }
        library.spoken = entries.ToArray(); library.characters = actors.ToArray(); library.replaceLegacyVoices = true; library.Invalidate();
        EditorUtility.SetDirty(library); return library;
    }

    static DialoguePolishView BuildView(Transform parent, TMP_FontAsset font, DialogueAudioLibrary library)
    {
        var root = UI("DialogueCanvas", parent); var canvas = Undo.AddComponent<Canvas>(root.gameObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = UiLayers.Dialogue;
        var scaler = Undo.AddComponent<CanvasScaler>(root.gameObject); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        Undo.AddComponent<GraphicRaycaster>(root.gameObject);
        var view = Undo.AddComponent<DialoguePolishView>(root.gameObject); view.canvas = canvas; view.library = library;
        var panel = UI("Conversation", root); view.panel = panel; panel.sizeDelta = new Vector2(1080,260); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f,0); panel.anchoredPosition = new Vector2(0,46);
        Image(panel,"Panels/panel_window",Color.white);
        view.group = Undo.AddComponent<CanvasGroup>(panel.gameObject); view.group.alpha = 0; view.group.blocksRaycasts = false; view.group.interactable = false;
        var avatar = UI("SpeakerBadge",panel); Pin(avatar,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-30),new Vector2(94,94)); view.avatarBack = Image(avatar,"Panels/panel_chip",Color.white);
        view.monogram = Text("Initial",avatar,font,40,Ink); Fill(view.monogram.rectTransform,8,8,8,8); view.monogram.alignment = TextAlignmentOptions.Center;
        var emblem = UI("Emblem",avatar); Pin(emblem,new Vector2(1,0),new Vector2(.5f,.5f),new Vector2(-4,4),new Vector2(32,32)); view.emblem = Image(emblem,"Icons/icon_star",Color.white); view.emblem.preserveAspect = true;
        view.speakerName = Text("Name",panel,font,30,Ink); Pin(view.speakerName.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(148,-24),new Vector2(360,38)); view.speakerName.text = "Мира";
        view.role = Text("Role",panel,font,18,Muted); Pin(view.role.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(148,-63),new Vector2(420,26)); view.role.text = "Хранительница лагеря";
        view.topic = Text("Topic",panel,font,17,Muted); Pin(view.topic.rectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-32,-30),new Vector2(300,24)); view.topic.alignment = TextAlignmentOptions.Right;
        view.progress = Text("LineProgress",panel,font,18,Muted); Pin(view.progress.rectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-32,-63),new Vector2(90,24)); view.progress.alignment = TextAlignmentOptions.Right;
        view.body = Text("Text",panel,font,30,Ink); Fill(view.body.rectTransform,148,76,32,98); view.body.fontStyle = FontStyles.Normal; view.body.textWrappingMode = TextWrappingModes.Normal; view.body.overflowMode = TextOverflowModes.Overflow; view.body.text = "В лагере снова тепло. Теперь можно приниматься за дело.";
        var hint = UI("InputHint",panel); FillHorizontal(hint,148,24,340,36); view.hint = Text("Text",hint,font,17,Muted); Fill(view.hint.rectTransform,0,0,0,0); view.hintGroup = Undo.AddComponent<CanvasGroup>(hint.gameObject);
        var hold = UI("HoldSkip",panel); Pin(hold,new Vector2(0,0),new Vector2(0,0),new Vector2(108,30),new Vector2(28,28));
        Image(hold,"Panels/panel_chip",new Color(.88f,.90f,1f)); var fill = UI("Fill",hold); Fill(fill,2,2,2,2);
        view.holdRing = Image(fill,"Panels/panel_chip",new Color(.45f,.85f,.65f)); view.holdRing.type = UnityEngine.UI.Image.Type.Filled; view.holdRing.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360; view.holdRing.fillOrigin = 2; view.holdRing.fillAmount = 0;
        var key = Text("Key",hold,font,17,Ink); Fill(key.rectTransform,0,0,0,0); key.text = "E"; key.alignment = TextAlignmentOptions.Center;
        view.holdGroup = Undo.AddComponent<CanvasGroup>(hold.gameObject);
        TMP_Text nextLabel, skipLabel;
        view.nextButton = Button("Next",panel,font,"Дальше","Buttons/btn_mint_normal",new Vector2(-24,24),new Vector2(164,44),out nextLabel); view.nextLabel = nextLabel;
        view.skipButton = Button("Skip",panel,font,"Пропустить","Buttons/btn_blue_normal",new Vector2(-202,24),new Vector2(132,44),out skipLabel); skipLabel.fontSize = 17;
        BuildHistory(view, panel, font);
        view.voiceBars = new RectTransform[3]; for (int i = 0; i < 3; i++)
        { var bar = UI("Voice_"+i,panel); Pin(bar,new Vector2(0,1),new Vector2(.5f,.5f),new Vector2(60+i*10,-142),new Vector2(5,4)); Image(bar,"Panels/panel_chip",new Color(.45f,.65f,.67f)); view.voiceBars[i] = bar; }
        var note = UI("SkipNote",root); Pin(note,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,46),new Vector2(540,54)); Image(note,"Panels/panel_toast",Color.white); view.noteGroup = Undo.AddComponent<CanvasGroup>(note.gameObject); view.noteGroup.alpha = 0; view.noteGroup.blocksRaycasts = false;
        view.noteText = Text("Text",note,font,23,Ink); Fill(view.noteText.rectTransform,16,8,16,8); view.noteText.alignment = TextAlignmentOptions.Center; view.noteText.text = "Разговор пропущен · цель вверху экрана";
        EditorUtility.SetDirty(view); return view;
    }
    static RectTransform UI(string name,Transform parent) { var go = new GameObject(name,typeof(RectTransform)); go.layer=5; go.transform.SetParent(parent,false); Undo.RegisterCreatedObjectUndo(go,"Dialogue UI"); return go.transform as RectTransform; }
    static Sprite Need(string key) { var s=AssetDatabase.LoadAssetAtPath<Sprite>(CandyUiKitGenerator.OutDir+"/"+key+".png"); if(s==null)throw new InvalidOperationException("Нет Candy UI: "+key);return s; }
    static Image Image(RectTransform r,string key,Color color) { var image=Undo.AddComponent<Image>(r.gameObject);image.sprite=Need(key);image.type=UnityEngine.UI.Image.Type.Sliced;image.color=color;image.raycastTarget=false;return image; }
    static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,float size,Color color) { var r=UI(name,parent);var t=Undo.AddComponent<TextMeshProUGUI>(r.gameObject);t.font=font;t.fontSize=size;t.color=color;t.fontStyle=FontStyles.Bold;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;return t; }
    static void Pin(RectTransform r,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size) { r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size; }
    static void Fill(RectTransform r,float l,float b,float right,float top) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(-right,-top); }
    static void FillHorizontal(RectTransform r,float l,float b,float right,float height) { r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(1,0);r.pivot=new Vector2(.5f,0);r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(-right,b+height); }
    static Button Button(string name,Transform parent,TMP_FontAsset font,string label,string sprite,Vector2 position,Vector2 size,out TMP_Text text)
    { var r=UI(name,parent);Pin(r,new Vector2(1,0),new Vector2(1,0),position,size);var image=Image(r,sprite,Color.white);image.raycastTarget=true;var button=Undo.AddComponent<UnityEngine.UI.Button>(r.gameObject);button.targetGraphic=image;var nav=button.navigation;nav.mode=Navigation.Mode.None;button.navigation=nav;text=Text("Text",r,font,20,Ink);Fill(text.rectTransform,8,7,8,7);text.alignment=TextAlignmentOptions.Center;return button; }
    static List<T> All<T>(Scene scene) where T:Component { var items=new List<T>();foreach(var root in scene.GetRootGameObjects())items.AddRange(root.GetComponentsInChildren<T>(true));return items; }
    static T One<T>(Scene scene) where T:Component { T found=null;foreach(var item in All<T>(scene))if(item.gameObject.activeInHierarchy){if(found!=null)throw new InvalidOperationException("Несколько активных "+typeof(T).Name);found=item;}return found; }
    static void Disable(DialoguePolishSetup setup,Behaviour target) { if(!setup.behaviours.Exists(s=>s.target==target))setup.behaviours.Add(new DialoguePolishSetup.BehaviourState{target=target,enabled=target.enabled});Undo.RecordObject(target,"Old merchant interaction");target.enabled=false; }
    static void DisableObject(DialoguePolishSetup setup,GameObject target) { if(!setup.objects.Exists(s=>s.target==target))setup.objects.Add(new DialoguePolishSetup.ObjectState{target=target,active=target.activeSelf});Undo.RecordObject(target,"Old merchant");target.SetActive(false); }
    static List<string> Inspect(DialoguePolishSetup setup)
    {
        var errors=new List<string>();if(setup==null){errors.Add("Нет TW_DialoguePolish. Запусти пункт 1.");return errors;}
        var view=setup.manager!=null?setup.manager.presentation:null;
        if(view==null||view.body==null||view.group==null||view.holdRing==null||view.nextButton==null||view.skipButton==null||view.library==null||view.historyRoot==null||view.autoButton==null)errors.Add("Неполные ссылки диалога. Повтори пункт 1 для версии 2.");
        if(setup.thoughtsRoot==null||setup.thoughtsRoot.GetComponent<PlayerThoughtController>()==null)errors.Add("Не создана карточка мыслей Лео.");
        if(setup.story==null||!setup.story.tradeAtBuilding||setup.story.marketTrading==null||setup.story.marketTrading.GetComponent<Speaker>()!=null)errors.Add("Торговля не перенесена на отдельную точку здания.");
        if(view!=null&&view.library!=null&&view.library.characters==null)errors.Add("Нет настроек голосов персонажей.");
        if(setup.manager!=null&&(setup.manager.babble==null||setup.manager.babble.source==null))errors.Add("Нет источника бормотания.");
        return errors;
    }
    [MenuItem(Menu+"2. Проверить диалоги",false,261)]
    static void QA() { try { var errors=Inspect(One<DialoguePolishSetup>(SceneManager.GetActiveScene()));if(errors.Count>0)Debug.LogError("DIALOGUE QA: "+string.Join("\n",errors));else Debug.Log("DIALOGUE QA OK: ссылки, библиотека, кнопки, источник голоса и прилавок. Сдачу, пропуск и звук проверь в Play Mode."); }catch(Exception ex){Debug.LogError("DIALOGUE QA: "+ex.Message);} }
    [MenuItem(Menu+"3. Вернуть оформление сцены",false,262)]
    static void Revert()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;var scene=SceneManager.GetActiveScene();var setup=One<DialoguePolishSetup>(scene);if(setup==null)return;
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Dialogue Polish revert");
        if(setup.story!=null){Undo.RecordObject(setup.story,"Story trade");setup.story.tradeAtBuilding=setup.previousBuildingTrade;setup.story.marketTrading=setup.previousMarketTrading;setup.story.merchant=setup.previousMerchant;}
        foreach(var s in setup.behaviours)if(s.target!=null){Undo.RecordObject(s.target,"Merchant");s.target.enabled=s.enabled;}
        foreach(var s in setup.objects)if(s.target!=null){Undo.RecordObject(s.target,"Merchant");s.target.SetActive(s.active);}
        if(setup.manager!=null&&!setup.ownsManager){Undo.RecordObject(setup.manager,"Dialogue view");if(setup.ownsBabble&&setup.manager.babble!=null)Undo.DestroyObjectImmediate(setup.manager.babble);setup.manager.presentation=setup.previousView;setup.manager.babble=setup.previousBabble;if(setup.previousBabble!=null){Undo.RecordObject(setup.previousBabble,"Babble source");setup.previousBabble.source=setup.previousBabbleSource;}}
        foreach(var added in setup.addedComponents)if(added!=null)Undo.DestroyObjectImmediate(added);
        if(setup.counterRoot!=null)Undo.DestroyObjectImmediate(setup.counterRoot);Undo.DestroyObjectImmediate(setup.gameObject);Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);Debug.Log("DIALOGUE POLISH: оформление сцены возвращено. Ctrl+S. Полный откат кода и аудиопрофиля описан в README.");
    }
}
