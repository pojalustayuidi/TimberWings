#if UNITY_EDITOR
// TimberWings Voice172 follow-up: reliable dialogue portraits + inspection voice wiring.
// One-time editor patch: modifies only known source anchors and backs them up outside Assets.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TWVoiceInspectionFix
{
    const string Root = "Assets/_Project/";
    const string Menu = "Tools/TimberWings/Voice 172 Hotfix/";
    const string SessionPending = "TWVoiceInspectionFix.Rebind";
    static readonly List<string> Warnings = new List<string>();
    static string backupFolder;
    static int fileChanges;

    [MenuItem(Menu + "1. Fix portraits and inspection thoughts")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("Voice Hotfix: stop Play Mode before applying."); return; }
        Warnings.Clear(); fileChanges = 0;
        backupFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "VoicePortraitFix_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        ImportPortraits();
        PatchPortraitResolver();
        PatchPortraitBuilder();
        PatchThoughtsApi();
        PatchEasterEgg();
        // When source files change, the delayed script-reload callback rebinds the scene references.
        SessionState.SetBool(SessionPending, true);
        AssetDatabase.Refresh();
        if (fileChanges == 0) FinishBindings();
        Debug.Log("Voice Hotfix: source files changed=" + fileChanges + ", backup=" + backupFolder);
        foreach (var warning in Warnings) Debug.LogWarning("Voice Hotfix: " + warning);
        EditorUtility.DisplayDialog("TimberWings • Voice Hotfix",
            "Patched " + fileChanges + " source files. After any compilation, run menu item 2 to verify scene portrait bindings. " +
            "New inspection WAV files still need to be generated with Gemini.\nBackups: " + backupFolder, "OK");
    }

    [DidReloadScripts]
    static void AfterReload()
    {
        if (SessionState.GetBool(SessionPending, false)) FinishBindings();
    }

    static void FinishBindings()
    {
        SessionState.SetBool(SessionPending, false);
        BindScenePortraits();
    }

    static string FilePath(string relative) { return Root + relative; }
    static string Read(string relative)
    {
        string p = FilePath(relative);
        if (!File.Exists(p)) { Warnings.Add("Source missing: " + p); return null; }
        return File.ReadAllText(p, Encoding.UTF8);
    }
    static void Save(string relative, string before, string after)
    {
        if (before == null || before == after) return;
        string p = FilePath(relative);
        string copy = Path.Combine(backupFolder, p.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(copy));
        File.WriteAllText(copy, before, new UTF8Encoding(false));
        File.WriteAllText(p, after, new UTF8Encoding(false));
        fileChanges++;
    }

    static void PatchPortraitResolver()
    {
        const string path = "Scripts/Dialogue/NpcPresentation/NpcDialoguePortraits.cs";
        string old = Read(path); if (old == null) return;
        string s = old;
        if (!s.Contains("public Sprite mira,oskar,leo,rico;"))
        {
            if (s.Contains("public Sprite mira,oskar;"))
                s = s.Replace("public Sprite mira,oskar;", "public Sprite mira,oskar,leo,rico;");
            else { Warnings.Add("Unknown portrait fields, skipping code patch. Recheck NpcDialoguePortraits.cs."); return; }
        }
        const string start = "    public Sprite PortraitFor(string name)";
        const string end = "    void Initialize()";
        int a = s.IndexOf(start, StringComparison.Ordinal), b = s.IndexOf(end, StringComparison.Ordinal);
        if (a < 0 || b <= a) { Warnings.Add("PortraitFor method anchor missing"); return; }
        const string resolver = @"    // Lazy Resources loading is deliberate: scene builders and old prefabs may leave
    // newly added Leo/Rico fields empty. Never hide their portraits for that reason.
    public Sprite PortraitFor(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string key = VoiceId.SpeakerKey(name);
        if (key == ""mira"" || string.Equals(name.Trim(), miraName, StringComparison.OrdinalIgnoreCase)) return mira;
        if (key == ""oskar"" || string.Equals(name.Trim(), oskarName, StringComparison.OrdinalIgnoreCase)) return oskar;
        if (key == ""leo"") return leo != null ? leo : Resources.Load<Sprite>(""DialoguePortraits/Leo"");
        if (key == ""rico"" || key == ""merchant"") return rico != null ? rico : Resources.Load<Sprite>(""DialoguePortraits/Rico"");
        return null;
    }
";
        s = s.Substring(0, a) + resolver + "\n" + s.Substring(b);
        Save(path, old, s);
    }

    static void PatchPortraitBuilder()
    {
        const string path = "Editor/TWNpcDialogue/TWNpcDialogueBuilder.cs";
        string old = Read(path); if (old == null) return;
        const string find = "module.view=view;module.mira=mira;module.oskar=oskar;";
        const string repl = "module.view=view;module.mira=mira;module.oskar=oskar;module.leo=Resources.Load<Sprite>(\"DialoguePortraits/Leo\");module.rico=Resources.Load<Sprite>(\"DialoguePortraits/Rico\");";
        if (old.Contains(repl)) return;
        if (!old.Contains(find)) { Warnings.Add("Portrait builder anchor missing"); return; }
        Save(path, old, old.Replace(find, repl));
    }

    static void PatchThoughtsApi()
    {
        const string path = "Scripts/Dialogue/Polish/PlayerThoughtController.cs";
        string old = Read(path); if (old == null) return;
        string s = old;
        if (!s.Contains("public static bool TryInspection("))
        {
            const string hook = "    void OnEnable()";
            if (!s.Contains(hook)) { Warnings.Add("ThoughtController OnEnable anchor missing"); return; }
            const string method = @"    // Explicit E-interaction: one-off thought for a readable environment clue.
    // Does not freeze input, move the player, or interrupt a quest conversation.
    public static bool TryInspection(string key, string text)
    {
        var t = instance;
        if (t == null || !t.isActiveAndEnabled || t.Blocked || t.current != null ||
            string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key)) return false;
        t.pending = null;
        t.Show(new Line { id = ""inspect_"" + key, text = text, kind = ""inspection"", priority = 0 }, Time.unscaledTime);
        return true;
    }
";
            s = s.Replace(hook, method + "\n" + hook);
        }
        const string from = "caption.text=line.kind==\"hint\"?\"Лео · мысль о задаче\":\"Лео · про себя\";";
        const string to = "caption.text=line.kind==\"hint\"?\"Лео · мысль о задаче\":line.kind==\"inspection\"?\"Лео · осматривает\":\"Лео · про себя\";";
        if (s.Contains(from)) s = s.Replace(from, to);
        else if (!s.Contains(to)) Warnings.Add("Caption styling anchor missing; inspection still works using default caption");
        Save(path, old, s);
    }

    static void PatchEasterEgg()
    {
        const string path = "Scripts/Location/Finale/FinalIslandEasterEgg.cs";
        string old = Read(path); if (old == null) return;
        const string find = "if (CanInteract) SaveIndicator.ShowToast(\"VII. Седьмую скамью я всё-таки унёс. — Гуннар\");";
        const string repl = @"if (CanInteract)
        {
            SaveIndicator.ShowToast(""VII. Седьмую скамью я всё-таки унёс. — Гуннар"");
            PlayerThoughtController.TryInspection(""island_seventh_bench"", ""«Седьмую скамью я всё-таки унёс»... Дядя, о чём ты?"");
        }";
        if (old.Contains("TryInspection(\"island_seventh_bench\"")) return;
        if (!old.Contains(find)) { Warnings.Add("FinalIslandEasterEgg interaction anchor missing"); return; }
        Save(path, old, old.Replace(find, repl));
    }

    static Sprite ImportPortrait(string name)
    {
        string path = FilePath("Resources/DialoguePortraits/" + name + ".png");
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Warnings.Add("Missing PNG: " + path); return null; }
        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (importer.maxTextureSize < 1024) { importer.maxTextureSize = 1024; dirty = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
        if (dirty) importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Warnings.Add("PNG is not a Unity Sprite: " + path);
        return sprite;
    }
    static void ImportPortraits() { ImportPortrait("Leo"); ImportPortrait("Rico"); }

    [MenuItem(Menu + "2. Bind portraits in active scene")]
    public static void BindScenePortraits()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("Voice Hotfix: stop Play Mode before scene binding."); return; }
        Sprite leo = ImportPortrait("Leo"), rico = ImportPortrait("Rico");
        if (leo == null || rico == null) { Debug.LogError("Voice Hotfix: portrait PNG sprite import failed."); return; }
        var all = UnityEngine.Object.FindObjectsByType<NpcDialoguePortraits>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int bound = 0;
        var scene = SceneManager.GetActiveScene();
        foreach (var p in all)
        {
            if (p == null || p.gameObject.scene != scene) continue;
            var so = new SerializedObject(p);
            var le = so.FindProperty("leo"); var ri = so.FindProperty("rico");
            if (le == null || ri == null)
            { Debug.LogWarning("Voice Hotfix: fields not compiled yet; wait for C# compilation then rerun menu item 2."); continue; }
            le.objectReferenceValue = leo; ri.objectReferenceValue = rico;
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(p);
            bound++;
        }
        if (bound > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Voice Hotfix: Leo and Rico portraits rebound on " + bound + " dialogue components. Save the scene (Ctrl+S).");
        }
        else Debug.LogWarning("Voice Hotfix: no NpcDialoguePortraits found in active scene. Open the gameplay scene and run the existing NPC portrait builder, then menu item 2.");
    }

    [MenuItem(Menu + "3. Audit missing inspection voices")]
    public static void Audit()
    {
        ImportPortraits();
        int portraitModules = 0;
        foreach (var p in UnityEngine.Object.FindObjectsByType<NpcDialoguePortraits>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p == null || p.gameObject.scene != SceneManager.GetActiveScene()) continue;
            portraitModules++;
            var data = new SerializedObject(p);
            var leoProp = data.FindProperty("leo"); var ricoProp = data.FindProperty("rico");
            if (leoProp == null || ricoProp == null || leoProp.objectReferenceValue == null || ricoProp.objectReferenceValue == null)
                Debug.LogWarning("Voice Hotfix: portrait references not bound on " + p.name + ". Run menu item 2.");
            if (p.portrait == null)
                Debug.LogWarning("Voice Hotfix: UI Image portrait is missing on " + p.name + ". Existing NPC UI builder may need to be applied.");
        }
        if (portraitModules == 0)
            Debug.LogWarning("Voice Hotfix: no NpcDialoguePortraits in the active scene; open the gameplay scene and run its NPC portrait builder.");
        else Debug.Log("Voice Hotfix: checked " + portraitModules + " portrait module(s) in active scene.");
        string[] texts = {
            "Лента перерезана. На внутренней стороне — шесть коротких зарубок.",
            "Кто-то отмечал тех, кто приходил сюда. Последняя отметка не закончена.",
            "На рукояти вырезана буква «Г». Как на вещах дяди.",
            "Гуннар был здесь. Почему он ни разу не рассказывал об этом месте?",
            "Скамьи отмечены зарубками. У этой лежит дощечка — имя на ней давно стёрлось.",
            "«Если услышишь зов после заката — не отвечай из-за деревьев».",
            "«Седьмую скамью я всё-таки унёс»... Дядя, о чём ты?"
        };
        int found = 0;
        foreach (string line in texts)
        {
            string id = VoiceId.Make("Лео", line);
            string path = FilePath("Resources/Voice21/" + id + ".wav");
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null) found++;
            else Debug.LogWarning("Voice Hotfix: missing inspection voice " + id + ".wav  /  " + line);
        }
        Debug.Log("Voice Hotfix: examination audio coverage " + found + "/" + texts.Length +
            ". Missing clips show text and babble until you add Gemini WAV files using the exact IDs.");
    }
}
#endif
