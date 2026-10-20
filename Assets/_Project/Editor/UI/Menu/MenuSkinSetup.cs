// Assets/_Project/Editor/UI/Menu/MenuSkinSetup.cs (ТОЛЬКО в Editor)
// Tools > Timber Wings > Шаг 4: скин меню
//   1. Настраивает импорт PNG из Assets/_Project/UI/Menu/Skin (Sprite, без сжатия, PPU 200, у доски рамка 9-slice).
//   2. Открывает MainMenu, назначает спрайты в поле Skin компонента MainMenuStub и ставит размеры доски и плашек.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MenuSkinSetup
{
    const string Dir = "Assets/_Project/UI/Menu/Skin/";
    const string MenuScene = "Assets/_Project/Scenes/MainMenu.unity";

    [MenuItem("Tools/Timber Wings/Шаг 4: скин меню")]
    static void Run()
    {
        if (!Directory.Exists(Dir))
        {
            EditorUtility.DisplayDialog("Timber Wings", "Нет папки " + Dir + ". Скопируй туда PNG из архива MenuSkin.", "OK");
            return;
        }
        foreach (string f in Directory.GetFiles(Dir, "*.png")) Import(f.Replace('\\', '/'));
        AssetDatabase.Refresh();

        if (SceneManager.GetActiveScene().path != MenuScene)
        {
            if (!File.Exists(MenuScene)) { Debug.LogError("Нет сцены MainMenu."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        }
        var go = GameObject.Find("MainMenu");
        var stub = go != null ? go.GetComponent<MainMenuStub>() : null;
        if (stub == null) { Debug.LogError("На объекте MainMenu нет MainMenuStub."); return; }

        var k = stub.skin ?? (stub.skin = new MenuSkin());
        k.board = S("board");
        k.amberNormal = S("plate_amber_normal"); k.amberHover = S("plate_amber_hover"); k.amberPressed = S("plate_amber_pressed");
        k.greenNormal = S("plate_green_normal"); k.greenHover = S("plate_green_hover"); k.greenPressed = S("plate_green_pressed");
        k.grayDisabled = S("plate_gray_disabled");
        k.iconPlayNavy = S("icon_play_navy"); k.iconLeaf = S("icon_leaf_cream");
        k.iconGear = S("icon_gear_cream"); k.iconExit = S("icon_exit_cream");
        k.boardPad = new Vector4(30f, 52f, 38f, 36f);
        k.plateSpriteSize = new Vector2(508f, 153f);
        k.plateFacePivot = new Vector2(0.5f, 0.5719f);

        // раскладка под этот скин (в сцене могли остаться старые значения)
        stub.boardAnchor = new Vector2(0.231f, 0.407f);
        stub.boardInPicture = true;
        stub.boardWidth = 514f;
        stub.plateSize = new Vector2(448f, 87f);
        stub.plateStep = 116f;
        stub.firstPlateY = 78f;
        stub.subLineGap = 16f;

        EditorUtility.SetDirty(stub);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Timber Wings: скин меню подключён. Skin готов: " + k.Ready);
    }

    static Sprite S(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(Dir + name + ".png"); }

    static void Import(string path)
    {
        var im = AssetImporter.GetAtPath(path) as TextureImporter;
        if (im == null) return;
        string n = Path.GetFileNameWithoutExtension(path);
        bool big = n == "board" || n.StartsWith("plate_");
        im.textureType = TextureImporterType.Sprite;
        im.spriteImportMode = SpriteImportMode.Single;
        im.spritePixelsPerUnit = big ? 200f : 100f;     // большие спрайты нарисованы в 2x
        im.mipmapEnabled = false;
        im.alphaIsTransparency = true;
        im.textureCompression = TextureImporterCompression.Uncompressed;
        im.filterMode = FilterMode.Bilinear;
        im.wrapMode = TextureWrapMode.Clamp;
        im.maxTextureSize = 2048;
        // рамка 9-slice доски в пикселях текстуры (лево, низ, право, верх): поля + 40 дизайн-пикселей, всё x2
        if (n == "board") im.spriteBorder = new Vector4(140f, 184f, 156f, 152f);
        im.SaveAndReimport();
    }
}
#endif
