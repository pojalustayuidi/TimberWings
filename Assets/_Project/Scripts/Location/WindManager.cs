// Assets/_Project/Scripts/Location/WindManager.cs (НЕ в Editor)
// Общий ветер: ОДИН Update на все качающиеся объекты вместо Update на каждом дереве.
//   - создаётся сам при первой регистрации (вешать вручную не нужно);
//   - невидимые объекты (Renderer.isVisible == false) пропускаются;
//   - дерево, которое сейчас рубят, пропускается: его качает TreeHits, и два скрипта не дерутся за localRotation.
// ToonSway остался тем же компонентом (LocationForest и префабы продолжают его добавлять), но теперь он только регистрирует агента.
using System.Collections.Generic;
using UnityEngine;

public sealed class WindAgent
{
    public Transform t;
    public Quaternion r0;          // «стоячая» ориентация (качание считается от неё)
    public float phase, angle, speed;
    public Renderer rend;          // для пропуска невидимых; null = качать всегда
    public ResourceItem item;      // если дерево рубят, не качаем
    internal int index = -1;
}

[DefaultExecutionOrder(50)]
public sealed class WindManager : MonoBehaviour
{
    static WindManager inst;
    static readonly List<WindAgent> agents = new List<WindAgent>(128);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { inst = null; agents.Clear(); }

    public static void Add(WindAgent a)
    {
        if (a == null || a.index >= 0) return;
        if (inst == null)
        {
            var go = new GameObject("WindManager");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<WindManager>();
        }
        a.index = agents.Count;
        agents.Add(a);
    }

    public static void Remove(WindAgent a)
    {
        if (a == null || a.index < 0) return;
        int i = a.index;
        if (i < agents.Count && agents[i] == a) RemoveAt(i);
        a.index = -1;
    }

    // swap-remove: порядок не важен, индекс переставленного агента обновляем
    static void RemoveAt(int i)
    {
        int last = agents.Count - 1;
        var gone = agents[i];
        if (i != last)
        {
            var moved = agents[last];
            agents[i] = moved;
            moved.index = i;
        }
        agents.RemoveAt(last);
        gone.index = -1;
    }

    // формула та же, что была в ToonSway: фаза зависит от позиции, поэтому соседи качаются не в такт
    public static float PhaseAt(Vector3 p) { return p.x * 0.3f + p.z * 0.2f; }

    public static Quaternion Pose(WindAgent a, float time)
    {
        float tt = time * a.speed + a.phase;
        return a.r0 * Quaternion.Euler(Mathf.Sin(tt) * a.angle, 0f, Mathf.Cos(tt * 0.8f) * a.angle);
    }

    void Update()
    {
        float time = Time.time;
        for (int i = 0; i < agents.Count; i++)
        {
            var a = agents[i];
            if (a.t == null) { RemoveAt(i); i--; continue; }       // объект уничтожен
            if (a.item != null && a.item.IsChopping) continue;      // качает TreeHits
            if (a.rend != null && !a.rend.isVisible) continue;
            a.t.localRotation = Pose(a, time);
        }
    }
}
