// Assets/_Project/Editor/UI/Menu/MainMenuSceneBuilder.cs (ТОЛЬКО в Editor)
// Tools > Timber Wings > Создать сцену MainMenu
// Tools > Timber Wings > Шаг 3: подключить фон в MainMenu   (картинка Assets/_Project/UI/Menu/MainMenu_BG.png станет спрайтом)
//   1. Берёт ОТКРЫТУЮ сцену как игровую (она должна быть сохранена в файл).
//   2. Создаёт Assets/_Project/Scenes/MainMenu.unity: камера + объект MainMenu с компонентом MainMenuStub.
//   3. Build Settings: MainMenu = 0, игровая = 1. Остальные сцены остаются в списке, но выключены (галочка снята).
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainMenuSceneBuilder
{
    const string MenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    const string BgPath = "Assets/_Project/UI/Menu/MainMenu_BG.png";

    [MenuItem("Tools/BuilderSim/UI/Главное меню/Создать сцену MainMenu", false, 920)]
    static void Create()
    {
        var active = SceneManager.GetActiveScene();
        string gamePath = active.path;
        if (string.IsNullOrEmpty(gamePath))
        {
            EditorUtility.DisplayDialog("Timber Wings", "Открытая сцена не сохранена в файл. Сохрани игровую сцену (Ctrl+S) и запусти пункт ещё раз.", "OK");
            return;
        }
        if (gamePath == MenuScenePath)
        {
            EditorUtility.DisplayDialog("Timber Wings", "Сейчас открыта сцена MainMenu. Открой ИГРОВУЮ сцену и запусти пункт ещё раз.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Directory.CreateDirectory("Assets/_Project/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.11f, 0.16f, 0.23f, 1f);

        var go = new GameObject("MainMenu");
        var stub = go.AddComponent<MainMenuStub>();
        stub.menuFont = FindCyrillicFontAsset();

        EditorSceneManager.SaveScene(scene, MenuScenePath);

        var list = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(gamePath, true),
        };
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != MenuScenePath && s.path != gamePath)
                list.Add(new EditorBuildSettingsScene(s.path, false));
        EditorBuildSettings.scenes = list.ToArray();

        Debug.Log("Timber Wings: готово. Build Settings: 0 = " + MenuScenePath + ", 1 = " + gamePath +
                  (stub.menuFont != null ? ". Шрифт меню: " + stub.menuFont.name : ". Шрифт с кириллицей не найден, перетащи его в поле Menu Font у объекта MainMenu."));
    }

    // Шаг 3: делает из PNG спрайт, открывает MainMenu и вешает на объект MainMenu компонент MainMenuBackground с этой картинкой
    [MenuItem("Tools/BuilderSim/UI/Главное меню/Шаг 3: подключить фон в MainMenu", false, 921)]
    static void SetupBackground()
    {
        var importer = AssetImporter.GetAtPath(BgPath) as TextureImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Timber Wings", "Не найдена картинка " + BgPath + ". Положи её туда (или скажи мне, где она лежит).", "OK");
            return;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.maxTextureSize = 2048;                       // картинка 1672x940: без уменьшения
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("Timber Wings", "Картинку не удалось загрузить как Sprite. Проверь " + BgPath + " в Inspector (Texture Type = Sprite).", "OK");
            return;
        }

        if (SceneManager.GetActiveScene().path != MenuScenePath)
        {
            if (!File.Exists(MenuScenePath))
            {
                EditorUtility.DisplayDialog("Timber Wings", "Нет сцены MainMenu. Сначала запусти Tools > Timber Wings > Создать сцену MainMenu.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        }

        var go = GameObject.Find("MainMenu");
        if (go == null) go = new GameObject("MainMenu");
        var bg = go.GetComponent<MainMenuBackground>();
        if (bg == null) bg = go.AddComponent<MainMenuBackground>();
        bg.background = sprite;
        if (go.GetComponent<MainMenuStub>() == null) go.AddComponent<MainMenuStub>();   // кнопки-заглушки должны быть на этом же объекте
        EditorUtility.SetDirty(bg);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Timber Wings: фон подключён (" + sprite.rect.width + "x" + sprite.rect.height + "). Нажми Play, F1 показывает маркеры точек.");
    }

    // ищет среди TMP-шрифтов проекта тот, где есть буква «Ж»; берёт первый подходящий
    static TMP_FontAsset FindCyrillicFontAsset()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (f != null && f.HasCharacter('Ж')) return f;
        }
        return null;
    }
}
#endif
