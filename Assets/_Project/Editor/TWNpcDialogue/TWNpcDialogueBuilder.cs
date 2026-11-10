using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class TWNpcDialogueBuilder
{
    public const string Menu="Tools/BuilderSim/NPC/Диалоги — портреты и обводка/";
    public const string Art="Assets/_Project/Art/UI/NpcDialogue";
    public const string Generated="Assets/_Project/Art/NpcDialogueGenerated";
    [MenuItem(Menu+"1. Обновить оформление",false,320)]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("NPC DIALOGUE: останови Play.");return;}
        int undo=-1;
        try
        {
            Scene scene=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Открой сохранённую игровую сцену.");
            var view=One<DialoguePolishView>(scene);if(view==null||view.panel==null||view.body==null||view.speakerName==null||view.avatarBack==null||view.monogram==null||view.nextButton==null||view.skipButton==null||view.role==null||view.progress==null||view.group==null)throw new InvalidOperationException("Нет полного окна DialoguePolishView. Сначала подключи существующий патч диалогов.");
            var shader=Shader.Find("TimberWings/NPC Conversation Outline");if(shader==null)throw new InvalidOperationException("Нет шейдера NPC Conversation Outline. Скопируй Assets целиком.");
            var mira=Portrait("portrait_mira");var oskar=Portrait("portrait_oskar");var chip=Need("Panels/panel_chip");
            // Build persistent shell assets before touching scene ownership. Source meshes are never modified.
            Directory.CreateDirectory(Generated);AssetDatabase.Refresh();var actors=Actors(scene);
            if(actors.Length==0)throw new InvalidOperationException("Нет процедурных моделей MiraRig / OskarRig. Сначала поставь модели NPC.");
            var edge=OutlineMaterial("NpcEdge",shader,3008);var halo=OutlineMaterial("NpcHalo",shader,3007);
            string directory="UserSettings/TW_NpcDialogueBackups/"+scene.name;Directory.CreateDirectory(directory);string backup=directory+"/"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Не удалось сохранить копию сцены.");
            Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("NPC portraits and outline");
            var state=One<NpcDialogueSetup>(scene);
            if(state!=null&&state.view!=view)throw new InvalidOperationException("Окно диалога было заменено. Сначала выполни пункт 3, затем повтори подключение.");
            if(state==null)
            {
                var go=new GameObject("TW_NpcDialoguePresentation");Undo.RegisterCreatedObjectUndo(go,"NPC presentation");state=Undo.AddComponent<NpcDialogueSetup>(go);
            }
            Undo.RecordObject(state,"NPC presentation ownership");ClearPresentation(state);
            state.view=view;state.backup=backup;
            var outline=state.GetComponent<NpcDialogueOutline>();if(outline==null)outline=Undo.AddComponent<NpcDialogueOutline>(state.gameObject);
            Undo.RecordObject(outline,"NPC outline bindings");outline.actors=actors;outline.edge=edge;outline.halo=halo;outline.view=view;state.outline=outline;
            var duplicate=view.GetComponent<NpcDialoguePortraits>();if(duplicate!=null)throw new InvalidOperationException("На окне уже есть независимый NpcDialoguePortraits. Дубли не удаляются автоматически.");
            var module=Undo.AddComponent<NpcDialoguePortraits>(view.gameObject);module.view=view;module.mira=mira;module.oskar=oskar;
            var story=One<StoryFlow>(scene);if(story!=null){module.miraName=story.miraName;module.oskarName=story.oskarName;}
            var decoration=UI("NPC_DialogueDecoration",view.panel);Fill(decoration);state.decoration=decoration.gameObject;module.decoration=decoration;
            var portrait=UI("NPC_Portrait",view.avatarBack.transform);Fill(portrait,-8,-4,-8,-10);module.portrait=Image(portrait,null,Color.white);module.portrait.preserveAspect=true;module.portrait.enabled=false;
            portrait.SetSiblingIndex(view.monogram.transform.GetSiblingIndex()+1);state.portraitObject=portrait.gameObject;
            var bar=UI("ConversationProgress",decoration);Pin(bar,new Vector2(1,1),new Vector2(1,1),new Vector2(-32,-96),new Vector2(194,8));module.progressRoot=bar;
            Image(bar,chip,new Color(.48f,.48f,.72f,.22f));var fill=UI("Fill",bar);Fill(fill);module.progressFill=Image(fill,chip,new Color(.32f,.63f,.96f,1));module.progressFill.type=UnityEngine.UI.Image.Type.Filled;module.progressFill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;module.progressFill.fillAmount=0;
            module.pips=new Image[8];for(int i=0;i<8;i++)
            {
                var pip=UI("Line_"+(i+1),bar);Pin(pip,new Vector2(0,1),new Vector2(0,1),new Vector2(i*14,-14),new Vector2(8,8));module.pips[i]=Image(pip,chip,Color.white);pip.gameObject.SetActive(false);
            }
            // Keep all decorative graphics below the original actionable controls.
            decoration.SetAsFirstSibling();state.portraits=module;
            EditorUtility.SetDirty(module);EditorUtility.SetDirty(outline);EditorUtility.SetDirty(state);
            var errors=Inspect(state);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=state.gameObject;
            Debug.Log("NPC DIALOGUE: два процедурных портрета, адаптивные отступы, индикатор реплик и обводка выбранного собеседника подключены. Ctrl+S. Копия сцены: "+backup,state);
        }
        catch(Exception e){if(undo>=0)Undo.RevertAllDownToGroup(undo);Debug.LogError("NPC DIALOGUE: изменения сцены отменены. "+e);}
    }
    static Sprite Portrait(string name)
    {
        string path=Art+"/"+name+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Нет портрета "+path);
        if(importer.textureType!=TextureImporterType.Sprite||importer.spriteImportMode!=SpriteImportMode.Single||!importer.alphaIsTransparency||importer.mipmapEnabled||importer.maxTextureSize<768||importer.textureCompression!=TextureImporterCompression.Uncompressed)
        {
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        }
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite==null)throw new InvalidOperationException("Портрет не импортировался как Sprite: "+path);return sprite;
    }
    static Material OutlineMaterial(string name,Shader shader,int queue)
    {
        string path=Generated+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        mat.shader=shader;mat.renderQueue=queue;EditorUtility.SetDirty(mat);return mat;
    }
    static void ClearPresentation(NpcDialogueSetup state)
    {
        if(state.portraits!=null)Undo.DestroyObjectImmediate(state.portraits);
        if(state.portraitObject!=null)Undo.DestroyObjectImmediate(state.portraitObject);
        if(state.decoration!=null)Undo.DestroyObjectImmediate(state.decoration);
        state.portraits=null;state.portraitObject=null;state.decoration=null;
    }
    public static List<string> Inspect(NpcDialogueSetup state)
    {
        var errors=new List<string>();if(state==null||state.view==null||state.portraits==null||state.outline==null){errors.Add("Нет полного подключения. Выполни пункт 1.");return errors;}
        var p=state.portraits;
        if(p.mira==null||p.oskar==null||p.portrait==null||p.progressFill==null||p.pips==null||p.pips.Length!=8)errors.Add("Нет портретов / индикатора реплик.");
        if(state.decoration!=null)foreach(var graphic in state.decoration.GetComponentsInChildren<Graphic>(true))if(graphic.raycastTarget)errors.Add("Декор перехватывает клики: "+graphic.name);
        if(p.portrait!=null&&p.portrait.raycastTarget)errors.Add("Портрет перехватывает клики.");
        var o=state.outline;if(o.edge==null||o.halo==null||o.actors==null||o.actors.Length==0)errors.Add("Нет шейдера / моделей обводки.");
        if(o.actors!=null)foreach(var actor in o.actors)
        {
            if(actor==null||actor.speaker==null||actor.parts==null||actor.parts.Length==0){errors.Add("Пустая привязка NPC.");continue;}
            if(actor.speaker.GetComponent<PlayerMovement>()!=null)errors.Add("В обводку ошибочно включён игрок.");
            foreach(var part in actor.parts)if(part==null||part.mesh==null||part.renderer==null||part.transform==null||!part.transform.IsChildOf(actor.speaker.transform))errors.Add("Потерян меш или узел NPC: "+actor.speaker.displayName);
        }
        return errors;
    }
    [MenuItem(Menu+"2. Проверить оформление",false,321)]
    public static void QA()
    {
        try{var state=One<NpcDialogueSetup>(SceneManager.GetActiveScene());var errors=Inspect(state);if(errors.Count>0)Debug.LogError("NPC DIALOGUE QA: "+string.Join("\n",errors));else Debug.Log("NPC DIALOGUE QA OK: портреты, индикатор, привязки мешей и свободный ввод. В Play Mode проверь смену говорящего, стену перед NPC, анимации и масштаб окна.");}
        catch(Exception e){Debug.LogError("NPC DIALOGUE QA: "+e.Message);}
    }
    [MenuItem(Menu+"3. Убрать это оформление",false,322)]
    public static void Remove()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;var scene=SceneManager.GetActiveScene();var state=One<NpcDialogueSetup>(scene);if(state==null)return;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("NPC presentation removal");ClearPresentation(state);Undo.DestroyObjectImmediate(state.gameObject);Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Debug.Log("NPC DIALOGUE: портреты, дополнительная верстка и обводка отключены. Ctrl+S.");
    }
    static Sprite Need(string key){var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(CandyUiKitGenerator.OutDir+"/"+key+".png");if(sprite==null)throw new InvalidOperationException("Нет Candy UI: "+key);return sprite;}
    static RectTransform UI(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"NPC dialogue decoration");return go.transform as RectTransform;}
    static Image Image(RectTransform rect,Sprite sprite,Color color){var image=Undo.AddComponent<Image>(rect.gameObject);image.sprite=sprite;image.type=sprite!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.color=color;image.raycastTarget=false;return image;}
    static void Fill(RectTransform rect,float left=0,float bottom=0,float right=0,float top=0){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);}
    static void Pin(RectTransform rect,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;}
    static List<T> All<T>(Scene scene)where T:Component{var all=new List<T>();foreach(var root in scene.GetRootGameObjects())all.AddRange(root.GetComponentsInChildren<T>(true));return all;}
    static T One<T>(Scene scene)where T:Component{T found=null;foreach(var item in All<T>(scene))if(item.gameObject.activeInHierarchy){if(found!=null)throw new InvalidOperationException("Несколько активных "+typeof(T).Name+". Дубли не удаляются автоматически.");found=item;}return found;}
}
