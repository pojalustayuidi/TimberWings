// Minimal language bridge for the *existing* MainMenuStub: no need to replace
// its script or modify the main menu scene. Narrative/local HUD remain RU-only.
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameLanguageMainMenuBridge : MonoBehaviour
{
    static GameLanguageMainMenuBridge instance;
    float nextCheck;

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
        if (SceneManager.GetActiveScene().buildIndex != MenuFlow.MainMenuScene) return;
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .4f;
        var texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null) continue;
            Transform parent = t.transform.parent;
            if (parent == null) continue;

            // These labels are created by MainMenuStub.MakeItem() in a Face child.
            if (parent.name == "Face" && parent.parent != null && parent.parent.name.StartsWith("Item_"))
            {
                string original = parent.parent.name.Substring(5);
                t.text = GameLanguage.T(original);
                continue;
            }

            // The tiny save summary is a direct child of the Continue item.
            if (parent.name == "Item_Продолжить")
            {
                int level, day;
                if (!SaveSummary.TryGet(out level, out day)) continue;
                if (GameLanguage.English)
                {
                    if (level > 0 && day > 0) t.text = "Level " + level + ", day " + day;
                    else if (level > 0) t.text = "Level " + level;
                    else t.text = "Day " + day;
                }
                else
                {
                    if (level > 0 && day > 0) t.text = "Уровень " + level + ", день " + day;
                    else if (level > 0) t.text = "Уровень " + level;
                    else t.text = "День " + day;
                }
                continue;
            }

            // Translate confirmation copy only while in the main-menu scene.
            if (GameLanguage.English && InNamedAncestor(parent, "Confirm"))
                t.text = GameLanguage.T(t.text);
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
