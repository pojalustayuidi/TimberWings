using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OskarModelTests
{
    [MenuItem(OskarModelBuilder.Menu+"5. Тест данных и рига",false,294)]
    static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("OSKAR TESTS: останови Play.");return;}
        var errors=new List<string>();int checks=0;Action<bool,string> check=(ok,message)=>{checks++;if(!ok)errors.Add(message);};
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var data=OskarModelData.Load();check(data.nodes.Length==38,"В исходнике 38 узлов рига");
            var root=new GameObject("Oskar_Rig_Test");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,preview);
            var nodes=new Dictionary<string,Transform>();
            foreach(var def in data.nodes)
            {
                var go=new GameObject(def.name);go.transform.SetParent(string.IsNullOrEmpty(def.parent)?root.transform:nodes[def.parent],false);
                go.transform.localPosition=OskarModelData.V(def.position);go.transform.localRotation=OskarModelData.Q(def.rotation);nodes.Add(def.name,go.transform);
            }
            var glint=new GameObject("Glint_Test");glint.transform.SetParent(nodes["EyeR"],false);glint.AddComponent<MeshFilter>();
            var rig=root.AddComponent<OskarRig>();rig.Bind();rig.CaptureRest();
            check(rig.HasRest&&rig.JointCount==38,"Полная поза покоя сохранена");
            check(rig.planer.IsChildOf(rig.handR)&&rig.planerKnob.IsChildOf(rig.planer),"Рубанок следует за кистью, рукоятка отдельная");
            check(rig.squirrelHead.IsChildOf(rig.squirrel)&&rig.squirrelTail.IsChildOf(rig.squirrel),"У белки отдельные голова и хвост");
            Vector3 plane=rig.planer.position,feet=rig.ankleR.position,head=rig.squirrelHead.position;
            rig.elbowR.localRotation*=Quaternion.Euler(20,15,0);
            check(Vector3.Distance(plane,rig.planer.position)>.01f,"Поворот локтя двигает рубанок вместе с рукой");
            check(Vector3.Distance(feet,rig.ankleR.position)<.00001f,"Жест руки не двигает ноги");
            rig.squirrelTail.localRotation*=Quaternion.Euler(0,0,25);
            check(Vector3.Distance(head,rig.squirrelHead.position)<.00001f,"Хвост белки независим от головы");
            rig.eyeR.localScale=new Vector3(1,.05f,1);check(Mathf.Abs(glint.transform.lossyScale.y-.05f)<.0001f,"Блик моргает вместе с глазом");
            rig.ResetPose();check(Vector3.Distance(plane,rig.planer.position)<.00001f&&rig.eyeR.localScale==Vector3.one,"Сброс возвращает позу без накопления смещений");
            for(int i=0;i<200;i++){rig.head.localRotation=rig.RestRotation(rig.head)*Quaternion.Euler(0,35,0);rig.ResetPose();}
            check(Quaternion.Angle(rig.head.localRotation,rig.RestRotation(rig.head))<.001f,"Повторные движения не накапливают вращение");
            foreach(var mesh in data.meshes)
            {
                bool finite=true;foreach(float f in mesh.normals)if(float.IsNaN(f)||float.IsInfinity(f)){finite=false;break;}
                check(finite,"Нормали меша "+mesh.name);
            }
            check(Array.Exists(data.parts,p=>p.name=="Flopy_Engraving"&&p.node=="SquirrelTail"),"flopy привязан к деревянной белке");
            check(Array.Exists(data.parts,p=>p.name=="PaperRoll"&&p.node=="Blueprint"),"Чертёж отдельный");
        }
        catch(Exception e){errors.Add(e.ToString());}
        finally{EditorSceneManager.ClosePreviewScene(preview);}
        if(errors.Count>0)Debug.LogError("OSKAR TESTS: "+errors.Count+" ошибок\n"+string.Join("\n",errors));
        else Debug.Log("OSKAR TESTS OK: "+checks+" проверок данных, иерархии, реквизита, моргания и возврата позы. Сцена, ресурсы и сохранения не менялись.");
    }
}
