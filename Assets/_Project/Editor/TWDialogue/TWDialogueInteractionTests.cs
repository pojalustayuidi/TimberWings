using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TWDialogueInteractionTests
{
    [MenuItem(TWDialoguePolishBuilder.Menu+"7. Тест редких подсказок и режимов речи",false,266)]
    static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("DIALOGUE TESTS: останови Play.");return;}
        var errors=new List<string>();Action<bool,string> check=(ok,message)=>{if(!ok)errors.Add(message);};
        var gate=new PlayerThoughtGate();gate.Reset(0);gate.Stage("supply_Do",0);gate.Input(74.9f);
        check(!gate.CanShow(74.9f,true),"Раньше 75 секунд подсказки нет");
        gate.Input(75);check(gate.CanShow(75,true),"После 75 секунд без прогресса и при активности помощь доступна");
        gate.Shown(75,true);gate.Input(194.9f);check(!gate.CanShow(194.9f,true),"До истечения 120 секунд вторая подсказка запрещена");
        gate.Input(195);check(gate.CanShow(195,true),"Через 120 секунд допускается вторая подсказка");gate.Shown(195,true);
        gate.Input(500);check(!gate.CanShow(500,true),"Третьей подсказки на том же этапе нет");
        gate.Stage("upgrade_Do",500);gate.Input(575);check(gate.CanShow(575,true),"Новый этап имеет отдельный лимит");
        gate.Reset(0);check(!gate.CanShow(100,true),"AFK не вызывает помощь");
        gate.Input(100);gate.Progress(90);check(!gate.CanShow(100,true),"Свежий прогресс откладывает помощь");
        gate.Reset(0);check(gate.CanShow(20,false),"Редкая реакция возможна после стартовой паузы");gate.Shown(20,false);check(!gate.CanShow(79.9f,false)&&gate.CanShow(80,false),"Между любыми репликами минимум 60 секунд");
        gate.Blocked(100);check(!gate.CanShow(111.9f,false)&&gate.CanShow(112,false),"После окна выдерживается 12 секунд тишины");
        check(DialogueSpeechRules.AllowsRecorded(DialogueSpeechMode.Recorded),"Озвучка разрешает записанный голос");
        check(!DialogueSpeechRules.AllowsRecorded(DialogueSpeechMode.Babble)&&!DialogueSpeechRules.AllowsRecorded(DialogueSpeechMode.Off),"Бормотание и Выкл не запускают записанные фразы");
        check(DialogueSpeechRules.AllowsBabble(DialogueSpeechMode.Recorded,true)&&!DialogueSpeechRules.AllowsBabble(DialogueSpeechMode.Recorded,false),"Озвучка использует разрешённое запасное бормотание");
        check(DialogueSpeechRules.AllowsBabble(DialogueSpeechMode.Babble,false),"Режим бормотания работает без записи");
        check(!DialogueSpeechRules.AllowsBabble(DialogueSpeechMode.Off,true),"Выкл запрещает бормотание");
        if(errors.Count>0)Debug.LogError("DIALOGUE INTERACTION TESTS: "+errors.Count+" ошибок\n"+string.Join("\n",errors));
        else Debug.Log("DIALOGUE INTERACTION TESTS OK: задержки, AFK, прогресс, лимит этапа, пауза после окна и три режима речи. Настройки и сохранение не изменялись.");
    }
}
