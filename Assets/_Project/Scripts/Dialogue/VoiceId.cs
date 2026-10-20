// Assets/_Project/Scripts/Dialogue/VoiceId.cs (НЕ в Editor)
// Стабильный id реплики: «персонаж_хеш текста». Этим именем называется файл озвучки (Resources/Voice/<id>.mp3),
// и этот же id стоит в первой колонке Dialogue_Lines.csv. Реплики в коде править можно: если текст изменился,
// id изменится, и игра просто вернётся к бормотанию, пока файл не перезапишут (Tools > BuilderSim > Озвучка > Проверить файлы).
// Хеш считается по тексту без лишних пробелов, регистр и знаки важны.
using System.Text.RegularExpressions;

public static class VoiceId
{
    public static string SpeakerKey(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return "npc";
        switch (displayName.Trim().ToLowerInvariant())
        {
            case "лео": return "leo";
            case "мира": return "mira";
            case "оскар": return "oskar";
            case "таис": return "tais";
            case "гуннар": return "gunnar";
            case "торговец": return "merchant";   // имя берётся из StoryFlow.merchantName: если переименуете, поправьте и здесь
            default: return "npc";
        }
    }

    public static string Normalize(string text)
    {
        return Regex.Replace((text ?? "").Trim(), "\\s+", " ");
    }

    // FNV-1a 32 бит по UTF-16 (младший байт, старший байт каждого символа)
    public static string Make(string speakerDisplayName, string text)
    {
        unchecked
        {
            uint h = 2166136261u;
            string t = Normalize(text);
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                h ^= (byte)(c & 0xFF); h *= 16777619u;
                h ^= (byte)(c >> 8); h *= 16777619u;
            }
            return SpeakerKey(speakerDisplayName) + "_" + h.ToString("x8");
        }
    }
}
