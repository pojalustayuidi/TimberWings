#if UNITY_EDITOR
// Assets/_Project/Editor/Story/VoiceBankTools.cs
// Tools > BuilderSim > Озвучка:
//   «Переименовать файлы по номерам»  ты скачал из генератора озвучки файлы вида 07.mp3, 07_Лео.mp3, 7-leo.wav:
//                                     инструмент находит номер в начале имени, берёт id реплики из Dialogue_Lines.csv,
//                                     переименовывает файл в <id> и переносит в Assets/_Project/Resources/Voice
//   «Проверить файлы озвучки»         показывает, каких реплик из таблицы ещё нет в Resources/Voice, по персонажам
// Таблица: Dialogue_Lines.csv (разделитель «;», колонки id, n, ..., персонаж, текст). Реплики, показанные в игре в редакторе,
// пишутся ещё и в <проект>/VoiceExport/seen_lines.csv, его можно проверить так же.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using static EditorAssets;

public static class VoiceBankTools
{
    const string VoiceDir = "Assets/_Project/Resources/Voice";
    static readonly string[] Ext = { ".mp3", ".wav", ".ogg", ".m4a", ".flac" };

    class Row { public string id; public int n; public string speaker; public string text; }

    // ------------------------------------------------------------ CSV
    static List<Row> Load(string path)
    {
        var rows = new List<Row>();
        var lines = SplitRecords(File.ReadAllText(path, Encoding.UTF8));
        for (int i = 1; i < lines.Count; i++)
        {
            var f = lines[i];
            if (f.Count < 3 || string.IsNullOrEmpty(f[0])) continue;
            var r = new Row { id = f[0].Trim() };
            if (f.Count >= 6)                       // основная таблица: id;n;когда;глава;персонаж;текст;...
            {
                int.TryParse(f[1], out r.n);
                r.speaker = f[4]; r.text = f[5];
            }
            else { r.speaker = f[1]; r.text = f[2]; }   // seen_lines.csv: id;персонаж;текст
            rows.Add(r);
        }
        return rows;
    }

    // читает CSV с кавычками и переводами строк внутри полей
    static List<List<string>> SplitRecords(string s)
    {
        var res = new List<List<string>>();
        var cur = new List<string>();
        var sb = new StringBuilder();
        bool q = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (q)
            {
                if (c == '"') { if (i + 1 < s.Length && s[i + 1] == '"') { sb.Append('"'); i++; } else q = false; }
                else sb.Append(c);
            }
            else if (c == '"') q = true;
            else if (c == ';') { cur.Add(sb.ToString()); sb.Length = 0; }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < s.Length && s[i + 1] == '\n') i++;
                cur.Add(sb.ToString()); sb.Length = 0;
                if (cur.Count > 1 || cur[0].Length > 0) res.Add(cur);
                cur = new List<string>();
            }
            else sb.Append(c);
        }
        if (sb.Length > 0 || cur.Count > 0) { cur.Add(sb.ToString()); res.Add(cur); }
        return res;
    }

    static string PickCsv()
    {
        return EditorUtility.OpenFilePanel("Таблица реплик (Dialogue_Lines.csv)", "", "csv");
    }

    // ------------------------------------------------------------ переименование
    [MenuItem("Tools/BuilderSim/Озвучка/Переименовать файлы по номерам")]
    static void Rename()
    {
        string csv = PickCsv();
        if (string.IsNullOrEmpty(csv)) return;
        var byN = new Dictionary<int, Row>();
        foreach (var r in Load(csv)) if (r.n > 0) byN[r.n] = r;
        if (byN.Count == 0) { EditorUtility.DisplayDialog("Озвучка", "В таблице нет колонки с номерами (n).", "OK"); return; }

        string folder = EditorUtility.OpenFolderPanel("Папка с файлами озвучки внутри Assets", Application.dataPath, "");
        if (string.IsNullOrEmpty(folder)) return;
        if (!folder.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/')))
        {
            EditorUtility.DisplayDialog("Озвучка", "Папка должна лежать внутри Assets: сначала перетащи файлы в проект.", "OK");
            return;
        }
        string rel = "Assets" + folder.Replace('\\', '/').Substring(Application.dataPath.Replace('\\', '/').Length);

        EnsureFolder(VoiceDir);
        int moved = 0, noNumber = 0, unknown = 0;
        var report = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { rel }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (System.Array.IndexOf(Ext, ext) < 0) continue;

            string name = Path.GetFileNameWithoutExtension(path);
            int digits = 0;
            while (digits < name.Length && char.IsDigit(name[digits])) digits++;
            if (digits == 0) { noNumber++; report.AppendLine("нет номера в имени: " + path); continue; }
            int n = int.Parse(name.Substring(0, digits));
            Row row;
            if (!byN.TryGetValue(n, out row)) { unknown++; report.AppendLine("в таблице нет реплики №" + n + ": " + path); continue; }

            string target = VoiceDir + "/" + row.id + ext;
            if (path == target) continue;
            string err = AssetDatabase.MoveAsset(path, target);   // если файл с таким id уже есть, Unity вернёт ошибку, мы её покажем в отчёте
            if (!string.IsNullOrEmpty(err)) { report.AppendLine("не удалось: " + path + " (" + err + ")"); continue; }
            moved++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Озвучка] Переименовано и перенесено: " + moved + ", без номера: " + noNumber + ", неизвестный номер: " + unknown +
                  (report.Length > 0 ? "\n" + report : ""));
        EditorUtility.DisplayDialog("Озвучка", "Готово: " + moved + " файлов в " + VoiceDir + ".\nБез номера: " + noNumber + ", номер не из таблицы: " + unknown + ".", "OK");
    }

    // ------------------------------------------------------------ проверка
    [MenuItem("Tools/BuilderSim/Озвучка/Проверить файлы озвучки")]
    static void Check()
    {
        string csv = PickCsv();
        if (string.IsNullOrEmpty(csv)) return;
        var rows = Load(csv);

        var have = new HashSet<string>();
        if (AssetDatabase.IsValidFolder(VoiceDir))
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { VoiceDir }))
                have.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));

        var missing = new Dictionary<string, List<Row>>();
        int ok = 0;
        foreach (var r in rows)
        {
            if (have.Contains(r.id)) { ok++; continue; }
            List<Row> l;
            if (!missing.TryGetValue(r.speaker, out l)) missing[r.speaker] = l = new List<Row>();
            l.Add(r);
        }

        var sb = new StringBuilder();
        sb.AppendLine("[Озвучка] Есть файл: " + ok + " из " + rows.Count + ". Папка: " + VoiceDir);
        foreach (var kv in missing)
        {
            sb.AppendLine("— " + kv.Key + ": не хватает " + kv.Value.Count);
            foreach (var r in kv.Value) sb.AppendLine("    №" + r.n + "  " + r.id + "  " + r.text);
        }
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Озвучка", "Есть " + ok + " из " + rows.Count + ". Список недостающих в Console.", "OK");
    }

}
#endif
