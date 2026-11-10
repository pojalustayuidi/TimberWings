using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TWMiraLifeTests
{
    [MenuItem(TWMiraLifeBuilder.Menu+"3. Тест состояний и рига",false,282)]
    static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("MIRA TESTS: останови Play.");return;}
        var errors=new List<string>();int checks=0;
        Action<bool,string> check=(ok,label)=>{checks++;if(!ok)errors.Add(label);};
        check(MiraInteractionRules.Resolve(true,true,true,true,true,true)==MiraInteractionRules.Mode.Hidden,"Окно / разговор скрывают вспомогательное взаимодействие");
        check(MiraInteractionRules.Resolve(false,true,true,true,false,true)==MiraInteractionRules.Mode.Deliver,"Брёвна важнее разговора");
        check(!MiraInteractionRules.OwnsInteraction(MiraInteractionRules.Mode.Deliver),"Новый компонент не забирает E у SupplyPoint");
        check(MiraInteractionRules.Resolve(false,true,false,false,false,true)==MiraInteractionRules.Mode.Reminder,"Пустой рюкзак даёт напоминание");
        check(MiraInteractionRules.Resolve(false,false,false,true,true,true)==MiraInteractionRules.Mode.Report,"Готовый отчёт важнее напоминания");
        check(!MiraInteractionRules.OwnsInteraction(MiraInteractionRules.Mode.Report)&&!MiraInteractionRules.OwnsInteraction(MiraInteractionRules.Mode.Talk),"Основной разговор остаётся у DialogueSequence");
        check(MiraInteractionRules.Resolve(false,false,false,false,false,true)==MiraInteractionRules.Mode.Reminder,"Активное задание запрещает случайную болтовню");
        check(MiraInteractionRules.Resolve(false,false,false,false,false,false)==MiraInteractionRules.Mode.Free,"После заданий доступно необязательное общение");
        var gate=new MiraInteractionRules.Approach();gate.Reset(0);
        check(!gate.Step(2,4.5f,20,true),"Загрузка рядом не запускает приветствие");
        gate.Step(7,4.5f,21,true);check(gate.Step(4,4.5f,22,true),"Новый подход допускает приветствие");
        check(!gate.Step(4,4.5f,200,true),"Стояние рядом не повторяет приветствие");
        gate.Step(5,4.5f,201,true);check(!gate.Step(4,4.5f,202,true),"Дрожание у внутренней границы не повторяет фразу");
        gate.Step(7,4.5f,203,true);check(gate.Step(4,4.5f,204,true),"Отдельный подход после паузы снова разрешён");
        gate.Step(7,4.5f,205,true);check(!gate.Step(4,4.5f,206,true),"Ранний повторный подход молчит");
        check(MiraInteractionRules.Blink(-1)==1&&MiraInteractionRules.Blink(.3f)==1&&MiraInteractionRules.Blink(.08f)<.1f,"Моргание закрывает и возвращает глаза");
        var asset=Resources.Load<TextAsset>("MiraLife/MiraLines");var book=asset!=null?JsonUtility.FromJson<MiraPresence.Book>(asset.text):null;
        check(book!=null&&book.lines!=null&&book.lines.Length==34,"Каталог содержит 34 реплики");
        if(book!=null&&book.lines!=null)
        {
            var ids=new HashSet<string>();var voiceIds=new HashSet<string>();bool credit=false;
            foreach(var line in book.lines)
            {
                check(ids.Add(line.id),"Повтор ID: "+line.id);check(!string.IsNullOrWhiteSpace(line.text)&&line.text.Length<=120,"Длина: "+line.id);
                check(voiceIds.Add(VoiceId.Make("Мира",line.text)),"Повтор voice ID: "+line.id);if(line.id=="chat_komarov")credit=line.text.Contains("komarov");
            }
            check(credit,"Отсылка к komarov есть в каталоге");
        }
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var go=new GameObject("MiraRig_Test");go.SetActive(false);SceneManager.MoveGameObjectToScene(go,preview);
            var joint=new GameObject("EyeR");joint.transform.SetParent(go.transform,false);
            var mesh=new GameObject("EyeR");mesh.transform.SetParent(joint.transform,false);mesh.AddComponent<MeshFilter>();
            var shine=new GameObject("EyeShineR");shine.transform.SetParent(joint.transform,false);
            var rig=go.AddComponent<MiraRig>();rig.Bind();check(rig.eyeR==joint.transform,"Bind выбирает сустав, а не одноимённый меш глаза");
            rig.CaptureRest();joint.transform.localScale=new Vector3(1,.05f,1);rig.ResetPose();check(joint.transform.localScale==Vector3.one,"Сброс позы восстанавливает моргание вместе с бликом");
            var seq=go.AddComponent<DialogueSequence>();seq.trigger=DialogueSequence.Trigger.OnInteract;seq.once=true;seq.lines=new[]{new DialogueSequence.Line{text="Сюжетный разговор"}};
            seq.reminderHandledExternally=true;seq.isQuestReminder=true;check(!seq.CanInteract,"Старая реплика-напоминание не конкурирует с карточкой");
            seq.isQuestReminder=false;check(seq.CanInteract,"Внешняя карточка не блокирует настоящий сюжетный разговор");
        }
        catch(Exception ex){errors.Add(ex.ToString());}
        finally{EditorSceneManager.ClosePreviewScene(preview);}
        if(errors.Count>0)Debug.LogError("MIRA TESTS: "+errors.Count+" ошибок\n"+string.Join("\n",errors));
        else Debug.Log("MIRA TESTS OK: "+checks+" проверок. Приоритет сдачи/отчёта, ограничение приветствий, моргание, привязка глаз и каталог. Ресурсы и сохранения не изменялись.");
    }
}
