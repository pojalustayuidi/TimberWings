// Assets/_Project/Scripts/Core/Telemetry.cs (НЕ в Editor)
// Лёгкая телеметрия: события игры одной строкой. Сейчас никуда не отправляется: пишется в Debug (если включено) и в кольцо
// последних событий; к SDK площадки (Яндекс Игры и т.п.) подключается одной подпиской на OnEvent.
//   Telemetry.Track("pack_opened", "pack", "Artel", "rarity", "Rare");   // пары «ключ, значение»
//   Telemetry.OnEvent += (name, json) => sdk.Send(name, json);
// Никаких персональных данных: только игровые события.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class Telemetry
{
    public static bool logToConsole = false;               // true = каждое событие в Console (для отладки)
    public static event Action<string, string> OnEvent;    // имя события, параметры в JSON

    const int RingSize = 64;
    static readonly Queue<string> ring = new Queue<string>();
    static readonly StringBuilder sb = new StringBuilder(256);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { OnEvent = null; ring.Clear(); }

    // Последние события (для отладочных панелей и smoke-тестов)
    public static IEnumerable<string> Recent { get { return ring; } }

    public static void Track(string name, params object[] keyValues)
    {
        string json = ToJson(keyValues);
        if (ring.Count >= RingSize) ring.Dequeue();
        ring.Enqueue(name + " " + json);
        if (logToConsole) Debug.Log("[Телеметрия] " + name + " " + json);
        if (OnEvent == null) return;
        foreach (Delegate d in OnEvent.GetInvocationList())
        {
            try { ((Action<string, string>)d)(name, json); }
            catch (Exception e) { Debug.LogException(e); }     // сбой чужого SDK не должен ломать игру
        }
    }

    static string ToJson(object[] kv)
    {
        sb.Length = 0;
        sb.Append('{');
        for (int i = 0; kv != null && i + 1 < kv.Length; i += 2)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(Esc(Convert.ToString(kv[i], CultureInfo.InvariantCulture))).Append("\":");
            object v = kv[i + 1];
            if (v == null) sb.Append("null");
            else if (v is bool) sb.Append((bool)v ? "true" : "false");
            else if (v is int || v is long || v is short || v is byte) sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
            else if (v is float || v is double) sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
            else sb.Append('"').Append(Esc(Convert.ToString(v, CultureInfo.InvariantCulture))).Append('"');
        }
        sb.Append('}');
        return sb.ToString();
    }

    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
