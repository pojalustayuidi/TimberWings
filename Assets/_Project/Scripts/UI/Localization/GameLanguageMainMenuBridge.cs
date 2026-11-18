// Minimal language bridge for the *existing* MainMenuStub: no need to replace
// its script or modify the main menu scene. Narrative/local HUD remain RU-only.
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameLanguageMainMenuBridge : MonoBehaviour
{
    static GameLanguageMainMenuBridge instance;
    float nextCheck;
    readonly List<GameObject> menuRoots = new List<GameObject>();
    readonly List<TMP_Text> labels = new List<TMP_Text>();
    readonly List<TMP_Text> scratch = new List<TMP_Text>();
    int sceneHandle = -1, hierarchyCount = -1;
    float nextLabelScan;

    void OnEnable()
    {
        GameLanguage.Changed += LanguageChanged;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    void OnDisable()
    {
        GameLanguage.Changed -= LanguageChanged;
        SceneManager.sceneLoaded -= SceneLoaded;
        if (instance == this) instance = null;
    }

    void LanguageChanged() { nextCheck = 0f; }
    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneHandle = -1;
        nextCheck = 0f;
        menuRoots.Clear();
        labels.Clear();
    }

    void RefreshLabels(Scene scene, float now)
    {
        if (sceneHandle != scene.handle || menuRoots.Count != scene.rootCount || now >= nextLabelScan)
        {
            scene.GetRootGameObjects(menuRoots);
            sceneHandle = scene.handle;
            hierarchyCount = -1;
        }
        int count = 0;
        foreach (var root in menuRoots)
            if (root != null)
                count += root.transform.hierarchyCount;
        if (count == hierarchyCount && now < nextLabelScan) return;
        hierarchyCount = count;
        nextLabelScan = now + 2f;
        labels.Clear();
        foreach (var root in menuRoots)
        {
            if (root == null) continue;
            scratch.Clear();
            root.GetComponentsInChildren(true, scratch);
            labels.AddRange(scratch);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("_GameLanguageMainMenuBridge");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameLanguageMainMenuBridge>();
    }

    void Update()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex != MenuFlow.MainMenuScene) return;
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .4f;
        RefreshLabels(scene, Time.unscaledTime);
        bool english = GameLanguage.English;
        bool haveSummary = SaveSummary.TryGet(out int level, out int day);
        for (int i = 0; i < labels.Count; i++)
        {
            TMP_Text t = labels[i];
            if (t == null) continue;
            Transform parent = t.transform.parent;
            if (parent == null) continue;

            // These labels are created by MainMenuStub.MakeItem() in a Face child.
            if (parent.name == "Face" && parent.parent != null && parent.parent.name.StartsWith("Item_"))
            {
                string original = parent.parent.name.Substring(5);
                string translated = GameLanguage.T(original);
                if (t.text != translated) t.text = translated;
                continue;
            }

            // The tiny save summary is a direct child of the Continue item.
            if (parent.name == "Item_Продолжить")
            {
                if (!haveSummary) continue;
                string summary;
                if (english)
                {
                    if (level > 0 && day > 0) summary = "Level " + level + ", day " + day;
                    else if (level > 0) summary = "Level " + level;
                    else summary = "Day " + day;
                }
                else
                {
                    if (level > 0 && day > 0) summary = "Уровень " + level + ", день " + day;
                    else if (level > 0) summary = "Уровень " + level;
                    else summary = "День " + day;
                }
                if (t.text != summary) t.text = summary;
                continue;
            }

            // Translate confirmation copy only while in the main-menu scene.
            if (english && InNamedAncestor(parent, "Confirm"))
            {
                string translated = GameLanguage.T(t.text);
                if (t.text != translated) t.text = translated;
            }
        }
    }

    static bool InNamedAncestor(Transform t, string name)
    {
        while (t != null)
        {
            if (t.name == name) return true;
            t = t.parent;
        }
        return false;
    }
}
