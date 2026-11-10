using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TWDialoguePolishTests
{
    [MenuItem(TWDialoguePolishBuilder.Menu + "4. Тест приоритета сдачи брёвен", false, 263)]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("DIALOGUE TESTS: останови Play."); return; }
        var errors = new List<string>();
        Action<bool,string> check = (ok, text) => { if (!ok) errors.Add(text); };
        var registered = Interactables.All.ToArray();
        var current = typeof(InteractionHub).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        object previousCurrent = current.GetValue(null);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            Interactables.All.Clear(); current.SetValue(null, null);
            var npc = new GameObject("DialogueTest_NPC"); var player = new GameObject("DialogueTest_Player");
            npc.SetActive(false); player.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(npc,preview);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(player,preview);
            var supply = npc.AddComponent<SupplyPoint>(); supply.Begin(8,0);
            var seq = npc.AddComponent<DialogueSequence>(); seq.trigger=DialogueSequence.Trigger.OnInteract; seq.once=false;
            seq.lines=new[]{new DialogueSequence.Line{text="Эта болтовня не должна перехватить сдачу."}};
            var banter = npc.AddComponent<NpcBanter>();
            var inventory = player.AddComponent<PlayerInventory>(); inventory.woodCount=4;
            typeof(SupplyPoint).GetField("inv",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(supply,inventory);
            var hub = player.AddComponent<InteractionHub>();
            var pick = typeof(InteractionHub).GetMethod("Pick",BindingFlags.Instance|BindingFlags.NonPublic);
            // Normal components do not run their game lifecycle in the editor preview scene.
            npc.SetActive(true);
            check(supply.CanInteract,"Сдача доступна с четырьмя брёвнами");
            check(!seq.CanInteract,"Обычный разговор не конкурирует с активной сдачей");
            check(!banter.CanInteract,"Болтовня не конкурирует с активной сдачей");
            IInteractable[] candidates={banter,seq,supply};
            int[][] orders={new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
            foreach(var order in orders)
            {
                Interactables.All.Clear(); current.SetValue(null,null);
                foreach(int index in order)Interactables.Register(candidates[index]);
                pick.Invoke(hub,null);
                check(ReferenceEquals(InteractionHub.Current,supply),"Сдача выбрана при порядке регистрации "+string.Join(",",order));
            }
            supply.delivered=4; inventory.woodCount=0;
            check(supply.Accepting&&!supply.CanInteract&&!seq.CanInteract&&!banter.CanInteract,"После первого рейса пустой рюкзак не запускает болтовню");
            inventory.woodCount=4;
            check(supply.CanInteract,"Второй рейс снова доступен");
            supply.delivered=8;
            check(!supply.Accepting&&!supply.CanInteract&&seq.CanInteract,"После 8/8 блок сдачи снят для следующего сюжетного разговора");
            var book=DialogueStorybook.Data;
            check(book!=null&&book.scenes!=null&&book.scenes.Length>=13,"В сценарии не меньше 13 основных разговоров");
            if(book!=null&&book.scenes!=null)foreach(var scene in book.scenes)
                foreach(var line in scene.lines)check(!string.IsNullOrWhiteSpace(line.text)&&line.text.Length<=120,"Длина реплики: "+scene.id);
        }
        catch(Exception ex){errors.Add(ex.ToString());}
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            Interactables.All.Clear(); Interactables.All.AddRange(registered); current.SetValue(null,previousCurrent);
        }
        if(errors.Count>0)Debug.LogError("DIALOGUE TESTS: "+errors.Count+" ошибок\n"+string.Join("\n",errors));
        else Debug.Log("DIALOGUE TESTS OK: 6 порядков регистрации, сдача 4+4, блок болтовни и открытие следующего разговора, 13 сюжетных сцен. Реальные ресурсы и сохранение не изменялись.");
    }
}
