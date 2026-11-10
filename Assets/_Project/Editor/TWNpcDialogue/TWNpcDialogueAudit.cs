using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class TWNpcDialogueAudit
{
    [MenuItem(TWNpcDialogueBuilder.Menu+"4. Проверить меши и портреты",false,323)]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("NPC DIALOGUE AUDIT: останови Play.");return;}
        int checks=0;var errors=new List<string>();
        Action<bool,string> check=(ok,message)=>{checks++;if(!ok&&errors.Count<30)errors.Add(message);};
        try
        {
            foreach(string actor in new[]{"mira","oskar"})
            {
                string path=TWNpcDialogueBuilder.Art+"/portrait_"+actor+".png";
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                try
                {
                    check(ImageConversion.LoadImage(texture,File.ReadAllBytes(path)),actor+": PNG не читается.");
                    check(texture.width==768&&texture.height==768,actor+": нужен портрет 768 × 768.");
                    var pixels=texture.GetPixels32();bool empty=false,solid=false,topClear=true;
                    for(int i=0;i<pixels.Length;i++){empty|=pixels[i].a==0;solid|=pixels[i].a>250;if(i>=(texture.height-8)*texture.width&&pixels[i].a>5)topClear=false;}
                    check(empty&&solid,actor+": прозрачный фон / непрозрачное лицо.");
                    check(topClear,actor+": верх головы обрезан кадром.");
                    var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                    check(importer!=null&&importer.textureType==TextureImporterType.Sprite&&importer.alphaIsTransparency,actor+": проверь импорт Sprite / Alpha Is Transparency.");
                }
                finally{Object.DestroyImmediate(texture);}
            }
            NpcDialogueSetup state=null;var scene=SceneManager.GetActiveScene();
            foreach(var root in scene.GetRootGameObjects())foreach(var candidate in root.GetComponentsInChildren<NpcDialogueSetup>(true))
            {check(state==null,"Несколько NpcDialogueSetup в сцене.");state=candidate;}
            if(state==null)throw new InvalidOperationException("Для проверки привязок сначала выполни пункт 1.");
            foreach(var error in TWNpcDialogueBuilder.Inspect(state))check(false,error);
            if(state.view!=null)check(state.view.GetComponents<NpcDialoguePortraits>().Length==1,"У окна должен быть один слой портретов.");
            var seen=new HashSet<Mesh>();
            if(state.outline!=null&&state.outline.actors!=null)foreach(var actor in state.outline.actors)
            {
                if(actor==null||actor.parts==null)continue;
                foreach(var part in actor.parts)
                {
                    if(part==null||part.mesh==null||part.transform==null)continue;
                    var filter=part.transform.GetComponent<MeshFilter>();
                    check(filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh!=part.mesh,"Обводка должна использовать копию меша.");
                    if(filter!=null&&filter.sharedMesh!=null)
                        check(filter.sharedMesh.vertexCount==part.mesh.vertexCount&&filter.sharedMesh.subMeshCount==part.mesh.subMeshCount,"Геометрия обводки не соответствует исходнику.");
                    if(!seen.Add(part.mesh))continue;
                    var normals=part.mesh.normals;check(normals.Length==part.mesh.vertexCount,"Нет нормалей: "+part.mesh.name);
                    bool finite=true;foreach(var normal in normals)
                    {
                        if(float.IsNaN(normal.x)||float.IsNaN(normal.y)||float.IsNaN(normal.z)||float.IsInfinity(normal.x)||float.IsInfinity(normal.y)||float.IsInfinity(normal.z)||normal.sqrMagnitude<.5f){finite=false;break;}
                    }
                    check(finite,"Неверные нормали обводки: "+part.mesh.name);
                    for(int sub=0;sub<part.mesh.subMeshCount;sub++)
                    {
                        bool indices=true;foreach(int index in part.mesh.GetIndices(sub))if(index<0||index>=part.mesh.vertexCount){indices=false;break;}
                        check(indices,"Индексы меша: "+part.mesh.name);
                    }
                }
            }
        }
        catch(Exception e){errors.Add(e.ToString());}
        if(errors.Count>0)Debug.LogError("NPC DIALOGUE AUDIT: "+errors.Count+" ошибок\n"+string.Join("\n",errors));
        else Debug.Log("NPC DIALOGUE AUDIT OK: "+checks+" проверок PNG, прозрачности, привязок, геометрии и нормалей. Игровая сцена и исходные меши не менялись.");
    }
}
