#if UNITY_EDITOR
// One-time installer for the TimberWings 172-line Gemini pack.
// Only patches the intended text/logic; saves originals outside Assets.
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TWVoice172Setup
{
    const string Base = "Assets/_Project/";
    static readonly Encoding Utf8 = new UTF8Encoding(false);
    static string backupFolder;
    static readonly List<string> warnings = new List<string>();
    static int changed;

    [Serializable] public sealed class MiraBook { public int version; public MiraLine[] lines; }
    [Serializable] public sealed class MiraLine { public string id, kind, text, condition; }
    [Serializable] public sealed class ThoughtBook { public int version; public ThoughtLine[] lines; }
    [Serializable] public sealed class ThoughtLine { public string id, text, kind, trigger; public int priority; }

    [MenuItem("Tools/TimberWings/Voice 172/1. Apply dialogue integration")]
    public static void Apply()
    {
        changed = 0; warnings.Clear();
        backupFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Voice172_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        PatchJson();
        PatchVoiceBank();
        PatchNpcBanter();
        PatchThoughts();
        PatchMiraPresence();
        PatchPortraits();
        PatchVoiceId();
        SetPortraitImports();
        AssetDatabase.Refresh();
        string message = "Voice172: changed " + changed + " files. Backups: " + backupFolder +
            ". Warnings: " + warnings.Count;
        Debug.Log(message);
        foreach (var warning in warnings) Debug.LogWarning("Voice172: " + warning);
        EditorUtility.DisplayDialog("TimberWings • Voice 172", message +
            "\nNow wait for Unity to compile, then run the audit from the same menu.", "OK");
    }

    static string PathFor(string relative) { return Base + relative; }

    static string Read(string path)
    {
        if (!File.Exists(path)) { warnings.Add("Missing file: " + path); return null; }
        return File.ReadAllText(path, Encoding.UTF8);
    }

    static void Save(string path, string old, string updated)
    {
        if (old == null || old == updated) return;
        string backup = Path.Combine(backupFolder, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        File.WriteAllText(backup, old, Utf8);
        File.WriteAllText(path, updated, Utf8);
        changed++;
    }

    static string Replace(string src, string old, string replacement, string description)
    {
        if (src == null || src.Contains(replacement)) return src;
        if (!src.Contains(old)) { warnings.Add("Text not found: " + description); return src; }
        return src.Replace(old, replacement);
    }

    static void ChangeText(string relative, string[,] edits)
    {
        string path = PathFor(relative), old = Read(path);
        if (old == null) return;
        string s = old;
        for (int i=0; i<edits.GetLength(0); i++) s = Replace(s, edits[i,0], edits[i,1], relative + " / " + i);
        Save(path, old, s);
    }

    static void PatchJson()
    {
        ChangeText("Resources/DialoguePolish/Script.json", new string[,] {
            {"Лавка готова. Рико уже у прилавка. Продать ресурсы можно там же, по E.",
             "Лавка готова! Рико уже у прилавка. Подойди к нему и нажми E, чтобы продать ресурсы."}
        });
        ChangeText("Resources/MiraLife/MiraLines.json", new string[,] {
            {"Подойди поближе и поговорим по E. Ты ведь Лео?",
             "Подойди поближе и нажми E, чтобы поговорить. Ты ведь Лео?"},
            {"Принёс брёвна? Передавай по E. Приму столько, сколько ещё нужно.",
             "Принёс брёвна? Нажми E, чтобы передать их мне. Приму сколько нужно."},
            {"Мост готов. Подойди к нему — там осталась записка Гуннара.",
             "Мост готов. Перейди на дальний остров. Кажется, Гуннар оставил там что-то важное."}
        });
        ChangeText("Resources/DialoguePolish/PlayerReactions.json", new string[,] {
            {"Брёвна уже со мной. Осталось отдать их Мире, по E.", "Брёвна собрал, осталось отдать их Мире."},
            {"Продажа открывается у самой лавки, по E.", "Ресурсы можно продать у Рико. Загляну к нему."},
            {"Перейду мост. Может, у перил что-нибудь осталось.",
             "Перейду мост и осмотрю дальний остров. Может, найду след дяди."}
        });
        AppendMiraLines(); AppendThoughtLines();
    }

    static void AppendMiraLines()
    {
        string path = PathFor("Resources/MiraLife/MiraLines.json");
        string old = Read(path); if (old == null) return;
        MiraBook book;
        try { book = JsonUtility.FromJson<MiraBook>(old); }
        catch (Exception ex) { warnings.Add("Mira json error: " + ex.Message); return; }
        if (book == null || book.lines == null) { warnings.Add("Mira json missing lines"); return; }
        var lines = new List<MiraLine>(book.lines);
        AddMira(lines,"greet_bridge_done_new","greet","Лео! Ты построил мост. Гуннар бы тобой гордился.","bridge_done");
        AddMira(lines,"chat_gunnar_memory_new","chat","Гуннар мог часами смотреть на дальний берег. Будто ждал оттуда ответа.","once");
        AddMira(lines,"chat_camp_return_new","chat","Каждый новый дом здесь — ещё одна причина людям вернуться.","once");
        book.lines = lines.ToArray();
        // JsonUtility normalizes formatting, but does not alter the content of existing entries.
        Save(path, old, JsonUtility.ToJson(book, true) + "\n");
    }

    static void AddMira(List<MiraLine> lines,string id,string kind,string text,string condition)
    {
        if (lines.Exists(x => x != null && x.id == id)) return;
        lines.Add(new MiraLine {id=id, kind=kind, text=text, condition=condition});
    }

    static void AppendThoughtLines()
    {
        string path = PathFor("Resources/DialoguePolish/PlayerReactions.json");
        string old = Read(path); if (old == null) return;
        ThoughtBook book;
        try { book = JsonUtility.FromJson<ThoughtBook>(old); }
        catch (Exception ex) { warnings.Add("Thought json error: " + ex.Message); return; }
        if (book == null || book.lines == null) { warnings.Add("Thought json missing lines"); return; }
        var lines = new List<ThoughtLine>(book.lines);
        AddThought(lines,"bridge_first_stage","Первая часть готова. Уже похоже на мост, который выдержит шторм.",
            "После первого этапа строительства моста.");
        AddThought(lines,"bridge_last_stage","Осталось совсем немного. Дядя, надеюсь, ты там.",
            "На заключительных этапах строительства моста.");
        book.lines=lines.ToArray(); Save(path,old,JsonUtility.ToJson(book,true)+"\n");
    }
    static void AddThought(List<ThoughtLine> lines,string id,string text,string trigger)
    {
        if (lines.Exists(x => x != null && x.id == id)) return;
        lines.Add(new ThoughtLine{id=id,text=text,trigger=trigger,kind="reaction",priority=3});
    }

    static void PatchVoiceBank()
    {
        string file="Scripts/Dialogue/VoiceBank.cs";
        ChangeText(file,new string[,] {
            {"        if (newer != null) return newer;\n        var library = DialogueAudioLibrary.Current;",
             "        if (newer != null) return newer;\n        // Dynamic Charon NPC barks use legacy dynamic_* names rather than a VoiceId hash.\n        var archived = VoiceArchiveLookup.Find(speakerDisplayName, text);\n        if (archived != null) return archived;\n        var library = DialogueAudioLibrary.Current;"}
        });
    }

    static void PatchNpcBanter()
    {
        string file = PathFor("Scripts/Npc/NpcBanter.cs");
        string old=Read(file); if (old==null) return;
        string s=old;
        s=Replace(s,"Знаешь, в лагере тихо только до первой щепки.",
            "Знаешь, в лагере тихо только до первого удара топора.","NPC idle 03");
        s=Replace(s,"        ResetBarkTimer();\n    }\n\n    void OnEnable()",
            "        ConfigureRecordedCharacterPools();\n        ResetBarkTimer();\n    }\n\n    void OnEnable()","NPC configure call");
        if (!s.Contains("void ConfigureRecordedCharacterPools()"))
        {
            const string anchor = "    void ResetBarkTimer()";
            if(!s.Contains(anchor)) warnings.Add("Cannot insert NPC voice pools method");
            else s=s.Replace(anchor, NpcPoolMethod + "\n" + anchor);
        }
        Save(file,old,s);
    }

    // Only Oskar gets the 21 Charon dynamic recordings. Mira and Rico have their own recorded lines.
    const string NpcPoolMethod = @"    void ConfigureRecordedCharacterPools()
    {
        if (speaker == null) return;
        string key = VoiceId.SpeakerKey(speaker.displayName);
        if (key == ""oskar"")
        {
            // Scene-serialized arrays override C# default initializers; repair the old line at runtime too.
            if (idleLines != null)
                for (int i = 0; i < idleLines.Length; i++)
                    if (idleLines[i] == ""Знаешь, в лагере тихо только до первой щепки."")
                        idleLines[i] = ""Знаешь, в лагере тихо только до первого удара топора."";
            annoyedLines = new[]
            {
                ""Лео, я чертёж держу. Не отвлекай меня каждые пять секунд."",
                ""Ещё раз отвлечёшь - сам будешь считать доски!""
            };
        }
        else if (key == ""mira"")
        {
            // MiraPresence owns her quest reminders and idle greetings; do not duplicate them.
            idleBarks = false; reactToChopping = false; greetBarkChance = 0f;
            pokeLines = new[]
            {
                ""Я с цветами разговариваю. Они хотя бы не перебивают."",
                ""Не кружка, а маленький сад. Просто сад с ручкой.""
            };
            annoyedLines = new[]
            {
                ""Лео, я тебя вижу. Дай мне хоть минутку, ладно?"",
                ""Ещё раз ткнёшь - отправлю тебя за новой партией брёвен!""
            };
        }
        else if (key == ""rico"" || key == ""merchant"")
        {
            idleBarks = false; reactToChopping = false; greetBarkChance = 0f;
            pokeLines = new[]
            {
                ""Вот именно! Беру брёвна и доски. Цены увидишь у прилавка, нажми E."",
                ""Молодец, думаешь. Оставь запас на стройку. Монеты тоже понадобятся — за мост никто не заплатит.""
            };
            annoyedLines = new[]
            {
                ""Бесплатные разговоры закончились. Следующий - за монету!"",
                ""Ещё раз ткнёшь - выпишу счёт за консультацию!""
            };
        }
    }
";

    static void PatchThoughts()
    {
        string file=PathFor("Scripts/Dialogue/Polish/PlayerThoughtController.cs"),old=Read(file);
        if(old==null)return;string s=old;
        s=Replace(s,"        gate.Reset(Time.unscaledTime); if (group != null)",
            "        ConfigureCompactCard();\n        gate.Reset(Time.unscaledTime); if (group != null)","Thought layout call");
        if(!s.Contains("void ConfigureCompactCard()"))
        {
            string anchor="    void OnEnable()";
            if(!s.Contains(anchor))warnings.Add("Cannot insert thought card method");
            else s=s.Replace(anchor, @"    // Short caption below the action, not a large dialogue bubble over Leo's head.
    void ConfigureCompactCard()
    {
        if (group == null) return;
        var rect = group.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 148f);
            rect.sizeDelta = new Vector2(650f, 122f);
        }
        if (body != null) body.fontSize = 24f;
        if (caption != null) caption.fontSize = 17f;
    }
"+"\n"+anchor);
        }
        // Track bridge stages separately so two already-recorded reactions actually play.
        s=Replace(s,"int blips, lastWood, lastPlanks, lastXp, lastSupply, lastStages, initialPlanks;",
            "int blips, lastWood, lastPlanks, lastXp, lastSupply, lastStages, lastBridgeStages, initialPlanks;",
            "bridge stage tracking field");
        s=Replace(s,"initialPlanks = made; wasFull = full; soldPending = false; return;",
            "initialPlanks = made; lastBridgeStages = story != null && story.bridge != null ? story.bridge.StepsDone : 0; wasFull = full; soldPending = false; return;",
            "bridge stage initial state");
        s=Replace(s,"        if (full && !wasFull) Queue(\"bag_full\",now); wasFull = full;",
            "        int bridgeStages = story != null && story.bridge != null ? story.bridge.StepsDone : 0;\n        if (bridgeStages > lastBridgeStages && story != null && story.bridge != null && !story.bridge.IsFinished)\n            Queue(bridgeStages == 1 ? \"bridge_first_stage\" : \"bridge_last_stage\", now);\n        lastBridgeStages = bridgeStages;\n        if (full && !wasFull) Queue(\"bag_full\",now); wasFull = full;",
            "bridge stage events");
        Save(file,old,s);
    }

    static void PatchMiraPresence()
    {
        ChangeText("Scripts/Npc/Mira/MiraPresence.cs",new string[,] {
            {"    int letters, delivered=-1, chatCounter;", "    int letters, delivered=-1, chatCounter;\n    int freeTalkStreak; float lastFreeTalk = -999f; bool alternateAnnoyed;"},
            {"        Refresh();if(!CanInteract)return;nextManual=Time.time+2.2f;\n        var line=mode==MiraInteractionRules.Mode.Free?NextChat():Find(taskKey);\n        if(line!=null)Show(line);",
             "        Refresh();if(!CanInteract)return;nextManual=Time.time+2.2f;\n        Line line;\n        if(mode==MiraInteractionRules.Mode.Free)\n        {\n            freeTalkStreak=Time.time-lastFreeTalk<10f?freeTalkStreak+1:1;lastFreeTalk=Time.time;\n            if(freeTalkStreak>=4)\n            {\n                line=new Line{id=alternateAnnoyed?\"mira_annoyed_2\":\"mira_annoyed_1\",kind=\"annoyed\",condition=\"always\",text=alternateAnnoyed?\"Ещё раз ткнёшь - отправлю тебя за новой партией брёвен!\":\"Лео, я тебя вижу. Дай мне хоть минутку, ладно?\"};\n                alternateAnnoyed=!alternateAnnoyed;freeTalkStreak=0;\n            }\n            else line=NextChat();\n        }\n        else{freeTalkStreak=0;line=Find(taskKey);}\n        if(line!=null)Show(line);"},
            {"Передай брёвна по E.","Нажми E, чтобы передать брёвна."},
            {"                if(mode==MiraInteractionRules.Mode.Free){var greeting=NextGreeting();if(greeting!=null)Show(greeting,true);}",
             "                if(story!=null&&story.bridge!=null&&story.bridge.IsFinished&&!DialogueSeen.Has(\"mira_bridge_voice_once\")){var greeting=Find(\"greet_bridge_done_new\");if(greeting!=null){Show(greeting,true);DialogueSeen.Mark(\"mira_bridge_voice_once\");}}\n                else if(mode==MiraInteractionRules.Mode.Free){var greeting=NextGreeting();if(greeting!=null)Show(greeting,true);}"},
            {"        if(mode!=MiraInteractionRules.Mode.Free)return false;\n        bool night=DayNightCycle.Night01>.5f;",
             "        if(mode!=MiraInteractionRules.Mode.Free)return false;\n        if(line.condition==\"bridge_done\")return story!=null&&story.bridge!=null&&story.bridge.IsFinished;\n        bool night=DayNightCycle.Night01>.5f;"}
        });
    }

    static void PatchPortraits()
    {
        string file="Scripts/Dialogue/NpcPresentation/NpcDialoguePortraits.cs";
        ChangeText(file,new string[,] {
            {"    public Sprite mira,oskar;", "    public Sprite mira,oskar,leo,rico;"},
            {"        if(string.Equals(name,oskarName,StringComparison.OrdinalIgnoreCase))return oskar;\n        return null;",
             "        if(string.Equals(name,oskarName,StringComparison.OrdinalIgnoreCase))return oskar;\n        if(string.Equals(name,\"Лео\",StringComparison.OrdinalIgnoreCase))return leo!=null?leo:Resources.Load<Sprite>(\"DialoguePortraits/Leo\");\n        if(string.Equals(name,\"Рико\",StringComparison.OrdinalIgnoreCase)||string.Equals(name,\"Торговец\",StringComparison.OrdinalIgnoreCase))return rico!=null?rico:Resources.Load<Sprite>(\"DialoguePortraits/Rico\");\n        return null;"}
        });
    }

    static void PatchVoiceId()
    {
        ChangeText("Scripts/Dialogue/VoiceId.cs",new string[,] {
            {"            case \"гуннар\": return \"gunnar\";",
             "            case \"гуннар\": case \"дядя гуннар\": return \"gunnar\";"}
        });
    }

    static void SetPortraitImports()
    {
        foreach(string name in new[]{"Leo","Rico"})
        {
            string path=PathFor("Resources/DialoguePortraits/"+name+".png");
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null){warnings.Add("Portrait missing for import: "+path);continue;}
            if(importer.textureType!=TextureImporterType.Sprite || !importer.alphaIsTransparency)
            {
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;
                importer.SaveAndReimport();
            }
        }
    }

    [MenuItem("Tools/TimberWings/Voice 172/2. Audit WAV and dialogue coverage")]
    public static void Audit()
    {
        var asset=Resources.Load<TextAsset>("Voice21/voice_archive_manifest");
        if(asset==null){Debug.LogError("Voice172: manifest not found. Extract the package under the Unity project root first.");return;}
        VoiceFile manifest;
        try{manifest=JsonUtility.FromJson<VoiceFile>(asset.text);}
        catch(Exception ex){Debug.LogError(ex.Message);return;}
        if(manifest==null||manifest.lines==null){Debug.LogError("Voice172: empty manifest");return;}
        int found=0, missing=0;
        foreach(var entry in manifest.lines)
        {
            if(entry==null)continue;
            string path=PathFor("Resources/Voice21/"+entry.file);
            if(AssetDatabase.LoadAssetAtPath<AudioClip>(path)!=null) found++;
            else {missing++;Debug.LogWarning("Voice172: missing audio "+path);}
        }
        int textMissing=0, textTotal=0;
        var indexed=new HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in manifest.lines)
            if(entry!=null&&!entry.id.StartsWith("dynamic_")&&!entry.id.StartsWith("finale_"))
            {
                string person=entry.character=="Дядя Гуннар"?"Гуннар":entry.character;
                indexed.Add(VoiceId.Make(person,entry.text));
            }
        CheckThoughtBook(indexed,ref textTotal,ref textMissing);
        CheckMiraBook(indexed,ref textTotal,ref textMissing);
        CheckScript(indexed,ref textTotal,ref textMissing);
        Debug.Log("Voice172 AUDIT: clips "+found+"/"+manifest.lines.Length+"; scripted JSON text matches "+(textTotal-textMissing)+"/"+textTotal+
            "; dynamic lines: Charon/Oskar only; finale film and unused Gunnar voice are optional. Check the Unity Console for gaps.");
    }

    [Serializable] public sealed class VoiceFile { public VoiceEntry[] lines; }
    [Serializable] public sealed class VoiceEntry { public string id, file, character, text; }
    [Serializable] public sealed class StoryBook { public Scene[] scenes; }
    [Serializable] public sealed class Scene { public string npc; public SceneLine[] lines; }
    [Serializable] public sealed class SceneLine { public bool hero; public string text; }

    static void Validate(HashSet<string> set,string who,string text,ref int total,ref int missing)
    {
        if(string.IsNullOrEmpty(text))return;
        total++;string id=VoiceId.Make(who,text);
        if(!set.Contains(id)){missing++;Debug.LogWarning("Voice172: NO matching recorded text: "+id+" / "+text);}
    }
    static void CheckThoughtBook(HashSet<string> set,ref int total,ref int missing)
    {
        var a=Resources.Load<TextAsset>("DialoguePolish/PlayerReactions"); if(a==null)return;
        var book=JsonUtility.FromJson<ThoughtBook>(a.text);
        if(book?.lines==null)return;
        foreach(var line in book.lines) if(line!=null)Validate(set,"Лео",line.text,ref total,ref missing);
    }
    static void CheckMiraBook(HashSet<string> set,ref int total,ref int missing)
    {
        var a=Resources.Load<TextAsset>("MiraLife/MiraLines"); if(a==null)return;
        var book=JsonUtility.FromJson<MiraBook>(a.text);
        if(book?.lines==null)return;
        foreach(var line in book.lines) if(line!=null)Validate(set,"Мира",line.text,ref total,ref missing);
    }
    static void CheckScript(HashSet<string> set,ref int total,ref int missing)
    {
        var a=Resources.Load<TextAsset>("DialoguePolish/Script"); if(a==null)return;
        var book=JsonUtility.FromJson<StoryBook>(a.text);
        if(book?.scenes==null)return;
        foreach(var scene in book.scenes)if(scene?.lines!=null)
            foreach(var line in scene.lines)if(line!=null)Validate(set,line.hero?"Лео":scene.npc,line.text,ref total,ref missing);
    }
}
#endif
