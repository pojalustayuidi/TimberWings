using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Diagnostics only: no scene or asset mutations.
public static class TWNpcDialogueAudit
{
    const int MaxReportedErrors = 30;

    [MenuItem(TWNpcDialogueBuilder.Menu + "4. Проверить меши и портреты", false, 323)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("NPC DIALOGUE AUDIT: останови Play.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("NPC DIALOGUE AUDIT: открой игровую сцену.");
            return;
        }

        int checks = 0;
        int failures = 0;
        var errors = new List<string>();
        Action<bool, string> check = (ok, message) =>
        {
            checks++;
            if (ok) return;
            failures++;
            if (errors.Count < MaxReportedErrors) errors.Add(message);
        };

        // An invalid/missing PNG must not prevent inspecting the other portrait or scene.
        foreach (string actor in new[] { "mira", "oskar" })
            CheckPortrait(actor, check);

        NpcDialogueSetup state = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (NpcDialogueSetup candidate in root.GetComponentsInChildren<NpcDialogueSetup>(true))
            {
                if (state == null) state = candidate;
                else check(false, "Несколько NpcDialogueSetup в сцене: " + candidate.name);
            }
        }

        if (state == null)
        {
            check(false, "NpcDialogueSetup не найден. Для проверки привязок сначала выполни пункт 1.");
        }
        else
        {
            try
            {
                foreach (string error in TWNpcDialogueBuilder.Inspect(state)) check(false, error);
                if (state.view != null)
                    check(state.view.GetComponents<NpcDialoguePortraits>().Length == 1,
                        "У окна должен быть один слой портретов.");
            }
            catch (Exception exception)
            {
                check(false, "Ошибка проверки привязок: " + exception.Message);
            }

            CheckOutline(state, check);
        }

        if (failures > 0)
        {
            string omitted = failures > errors.Count
                ? "\n... и ещё " + (failures - errors.Count) + " замечаний." : "";
            Debug.LogError("NPC DIALOGUE AUDIT: " + failures + " ошибок (" + checks +
                " проверок)\n" + string.Join("\n", errors) + omitted);
        }
        else
        {
            Debug.Log("NPC DIALOGUE AUDIT OK: " + checks +
                " проверок PNG, прозрачности, привязок, геометрии и нормалей. Сцена и меши не менялись.");
        }
    }

    static void CheckPortrait(string actor, Action<bool, string> check)
    {
        string path = TWNpcDialogueBuilder.Art + "/portrait_" + actor + ".png";
        bool exists = File.Exists(path);
        check(exists, actor + ": отсутствует PNG по пути " + path);
        if (!exists) return;

        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
            {
                check(false, actor + ": PNG не читается.");
                return;
            }

            check(texture.width == 768 && texture.height == 768,
                actor + ": нужен портрет 768 × 768.");
            Color32[] pixels = texture.GetPixels32();
            bool hasTransparent = false;
            bool hasOpaque = false;
            bool topClear = true;
            int topRowStart = (texture.height - 8) * texture.width;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte alpha = pixels[i].a;
                if (alpha == 0) hasTransparent = true;
                if (alpha > 250) hasOpaque = true;
                if (i >= topRowStart && alpha > 5) topClear = false;
            }
            check(hasTransparent && hasOpaque, actor + ": прозрачный фон / непрозрачное лицо.");
            check(topClear, actor + ": верх головы обрезан кадром.");

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            check(importer != null && importer.textureType == TextureImporterType.Sprite &&
                importer.alphaIsTransparency, actor + ": проверь импорт Sprite / Alpha Is Transparency.");
        }
        catch (Exception exception)
        {
            check(false, actor + ": ошибка чтения PNG: " + exception.Message);
        }
        finally
        {
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }

    static void CheckOutline(NpcDialogueSetup state, Action<bool, string> check)
    {
        if (state.outline == null || state.outline.actors == null) return;
        var seen = new HashSet<Mesh>();
        foreach (var actor in state.outline.actors)
        {
            if (actor == null || actor.parts == null) continue;
            foreach (var part in actor.parts)
            {
                if (part == null || part.mesh == null || part.transform == null) continue;
                try
                {
                    var filter = part.transform.GetComponent<MeshFilter>();
                    check(filter != null && filter.sharedMesh != null && filter.sharedMesh != part.mesh,
                        "Обводка должна использовать копию меша.");
                    if (filter != null && filter.sharedMesh != null)
                        check(filter.sharedMesh.vertexCount == part.mesh.vertexCount &&
                            filter.sharedMesh.subMeshCount == part.mesh.subMeshCount,
                            "Геометрия обводки не соответствует исходнику.");
                    if (!seen.Add(part.mesh)) continue;

                    Vector3[] normals = part.mesh.normals;
                    check(normals.Length == part.mesh.vertexCount, "Нет нормалей: " + part.mesh.name);
                    bool finite = true;
                    foreach (Vector3 normal in normals)
                    {
                        if (float.IsNaN(normal.x) || float.IsNaN(normal.y) || float.IsNaN(normal.z) ||
                            float.IsInfinity(normal.x) || float.IsInfinity(normal.y) || float.IsInfinity(normal.z) ||
                            normal.sqrMagnitude < .5f)
                        {
                            finite = false;
                            break;
                        }
                    }
                    check(finite, "Неверные нормали обводки: " + part.mesh.name);
                    for (int sub = 0; sub < part.mesh.subMeshCount; sub++)
                    {
                        bool validIndices = true;
                        foreach (int index in part.mesh.GetIndices(sub))
                        {
                            if (index < 0 || index >= part.mesh.vertexCount)
                            {
                                validIndices = false;
                                break;
                            }
                        }
                        check(validIndices, "Индексы меша: " + part.mesh.name);
                    }
                }
                catch (Exception exception)
                {
                    check(false, "Проверка меша " + part.mesh.name + ": " + exception.Message);
                }
            }
        }
    }
}
