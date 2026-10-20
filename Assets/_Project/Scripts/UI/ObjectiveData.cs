// Assets/_Project/Scripts/UI/ObjectiveData.cs (НЕ в Editor)
// Данные цели для виджета ObjectiveView: заголовок + список требований {иконка, имя, есть, нужно}.
// Строят её StoryFlow и IntroFlow, отдают в UIManager.SetObjective(data).
// Требование может быть «живым»: если задан live, число «есть» каждый кадр берётся оттуда (инвентарь, BuildingController),
// поэтому потоку не нужно пересобирать цель на каждое бревно.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class ObjectiveReq
{
    public UiIcon icon = UiIcon.Log;
    public Sprite sprite;            // пусто = UiKit.Icon(icon)
    public string name;              // «Бревна»
    public int have;                 // сколько есть
    public int need;                 // сколько нужно
    public Func<int> live;           // если задан: «есть» берётся отсюда
    public string note;              // мелкая строка под требованием («в рюкзаке 4 · сдано 3/10»)
    public Func<string> liveNote;    // если задан: note берётся отсюда

    public bool Done { get { return need > 0 && have >= need; } }

    // «есть» для показа не превышает «нужно»: рюкзак с 8 брёвнами при цели 4 показывает 4/4
    public int Shown { get { return need > 0 ? Mathf.Min(have, need) : have; } }

    public void Refresh()
    {
        if (live != null) have = Mathf.Max(0, live());
        if (liveNote != null) note = liveNote();
    }
}

public sealed class ObjectiveData
{
    public string title;                                   // «Нарубить дров»
    public string footer;                                  // мелкая подсказка под списком (TMP rich text разрешён)
    public int stage, stageCount;                          // стадия стройки: номер с 1 и всего (stageCount <= 1: не показывается)
    public string stageName;                               // название стадии («Опоры»)
    public readonly List<ObjectiveReq> reqs = new List<ObjectiveReq>(4);

    public ObjectiveData(string title) { this.title = title; }

    // статичное требование
    public ObjectiveData Req(UiIcon icon, string name, int have, int need)
    {
        reqs.Add(new ObjectiveReq { icon = icon, name = name, have = have, need = need });
        return this;
    }

    // живое требование: «есть» берётся из live каждый кадр
    public ObjectiveData Req(UiIcon icon, string name, int need, Func<int> live, Func<string> liveNote = null)
    {
        var r = new ObjectiveReq { icon = icon, name = name, need = need, live = live, liveNote = liveNote };
        r.Refresh();
        reqs.Add(r);
        return this;
    }

    public bool HasStage { get { return stageCount > 1 && stage >= 1; } }

    public ObjectiveData Stage(int stage, int total, string name)
    {
        this.stage = stage; stageCount = total; stageName = name;
        return this;
    }

    public bool HasReqs { get { return reqs.Count > 0; } }

    public bool AllDone
    {
        get
        {
            if (reqs.Count == 0) return false;
            for (int i = 0; i < reqs.Count; i++) if (!reqs[i].Done) return false;
            return true;
        }
    }

    // «Форма» цели без чисел «есть»: если она не изменилась, виджет только обновляет счётчики (с пульсом), а не пересобирается
    public string Signature
    {
        get
        {
            var sb = new StringBuilder(64);
            sb.Append(title).Append('|').Append(HasStage ? stage : 0).Append('/').Append(HasStage ? stageCount : 0).Append('|').Append(string.IsNullOrEmpty(footer) ? '0' : '1');   // текст подсказки в форму не входит: он меняется на месте
            for (int i = 0; i < reqs.Count; i++)
                sb.Append('|').Append((int)reqs[i].icon).Append(':').Append(reqs[i].name).Append(':').Append(reqs[i].need);
            return sb.ToString();
        }
    }

    // Простой текст без тегов: его читают QuestGuide (по смене текста понимает, что игрок продвинулся) и запасной путь
    public string ToPlain()
    {
        var sb = new StringBuilder(64);
        sb.Append(title);
        if (HasStage) sb.Append(" (стадия ").Append(stage).Append('/').Append(stageCount).Append(')');
        for (int i = 0; i < reqs.Count; i++)
        {
            sb.Append(i == 0 ? ": " : " · ");
            sb.Append(reqs[i].name).Append(' ').Append(reqs[i].Shown).Append('/').Append(reqs[i].need);
        }
        return sb.ToString();
    }
}
