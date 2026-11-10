using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TWDialogueVoiceTools
{
    [MenuItem(TWDialoguePolishBuilder.Menu + "5. Выгрузить фразы для озвучки", false, 264)]
    static void Export()
    {
        var script=AssetDatabase.LoadAssetAtPath<TextAsset>(TWDialoguePolishBuilder.Root+"/Script.json");
        if(script==null){Debug.LogError("Нет сценария DialoguePolish/Script.json.");return;}
        var book=JsonUtility.FromJson<DialogueStorybook.Book>(script.text);
        var available=new HashSet<string>();
        foreach(string guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/_Project/Art/Audio/DialoguePolish/Spoken"}))
            available.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
        var all=new StringBuilder("id;персонаж;текст;приоритет;сцена;подача;статус\n");
        var missing=new StringBuilder("id;персонаж;текст;приоритет;сцена;подача\n");int total=0,need=0;
        Action<string,string,int,string,string> add=(who,text,priority,title,direction)=>
        {
            string id=VoiceId.Make(who,text);
            bool exists=available.Contains(id);
            string row=id+";"+Q(who)+";"+Q(text)+";"+priority+";"+Q(title)+";"+Q(direction);
            all.AppendLine(row+";"+(exists?"запись есть":"нужно записать"));total++;
            if(!exists){missing.AppendLine(row);need++;}
        };
        foreach(var scene in book.scenes)foreach(var line in scene.lines)add(line.hero?"Лео":scene.npc,line.text,line.priority,scene.title,line.direction);
        var extra=AssetDatabase.LoadAssetAtPath<TextAsset>(TWDialoguePolishBuilder.Root+"/PlayerReactions.json");
        if(extra!=null)
        {
            var thoughts=JsonUtility.FromJson<PlayerThoughtController.Book>(extra.text);
            if(thoughts!=null&&thoughts.lines!=null)foreach(var line in thoughts.lines)
                if(line!=null&&!string.IsNullOrWhiteSpace(line.text))add("Лео",line.text,line.priority,line.kind=="hint"?"Помощь по задаче":"Мысли Лео",line.trigger);
        }
        string folder=Path.GetFullPath("VoiceExport/DialoguePolish");Directory.CreateDirectory(folder);
        File.WriteAllText(folder+"/all_lines.csv",all.ToString(),new UTF8Encoding(true));File.WriteAllText(folder+"/missing_lines.csv",missing.ToString(),new UTF8Encoding(true));
        Debug.Log("DIALOGUE VOICE: "+total+" реплик; записать "+need+". Приоритет 1 — сначала. Папка: "+folder);
        EditorUtility.RevealInFinder(folder+"/missing_lines.csv");
    }
    static string Q(string text){return "\""+(text??"").Replace("\"","\"\"")+"\"";}
    [MenuItem(TWDialoguePolishBuilder.Menu + "6. Открыть сценарий", false, 265)]
    static void Script(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<TextAsset>(TWDialoguePolishBuilder.Root+"/Script.json");}
}
