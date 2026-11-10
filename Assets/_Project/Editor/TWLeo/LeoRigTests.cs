using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LeoRigTests
{
    [MenuItem(LeoModelBuilder.Menu+"5. Тест данных и каркаса",false,354)]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("LEO TESTS: останови Play.");return;}
        var errors=new List<string>();int checks=0;Action<bool,string> check=(ok,message)=>{checks++;if(!ok)errors.Add(message);};
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var data=LeoModelData.Load();check(data.nodes.Length>=70,"Полный риг героя.");
            var root=new GameObject("Leo_Rig_Test");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);var nodes=new Dictionary<string,Transform>();
            foreach(var def in data.nodes)
            {
                var go=new GameObject(def.name);go.transform.SetParent(string.IsNullOrEmpty(def.parent)?root.transform:nodes[def.parent],false);
                go.transform.localPosition=LeoModelData.V(def.position);go.transform.localRotation=LeoModelData.Q(def.rotation);go.SetActive(def.active);nodes.Add(def.name,go.transform);
            }
            var rig=root.AddComponent<LeoRig>();rig.Bind();rig.CaptureRest();int nodeCount=root.GetComponentsInChildren<Transform>(true).Length;
            check(rig.JointCount==data.nodes.Length,"Поза покоя содержит все узлы.");
            check(rig.fingersL.Length==4&&rig.fingersR.Length==4&&rig.fingerTipsL.Length==4&&rig.fingerTipsR.Length==4,"Отдельные фаланги четырёх пальцев и большие пальцы.");
            Vector3 leftFoot=rig.ankleL.position,rightFoot=rig.ankleR.position,rootPosition=root.transform.position,handBefore=rig.handR.position;
            foreach(LeoPosePreview.Pose pose in Enum.GetValues(typeof(LeoPosePreview.Pose)))
            {
                LeoPosePreview.Apply(rig,pose);
                check(Vector3.Distance(leftFoot,rig.ankleL.position)<.00001f&&Vector3.Distance(rightFoot,rig.ankleR.position)<.00001f&&root.transform.position==rootPosition,pose+": поза не двигает корень и ноги.");
                check(root.GetComponentsInChildren<Transform>(true).Length==nodeCount,pose+": объекты не генерируются.");
                if(pose==LeoPosePreview.Pose.TPose)check(Vector3.Distance(handBefore,rig.handR.position)>.2f,"Плечо/локоть двигают кисть в контрольной T-позе.");
                if(pose==LeoPosePreview.Pose.Talk||pose==LeoPosePreview.Pose.Compass)check(Vector3.Distance(rig.axeRoot.position,rig.axeHolster.position)<.0001f,pose+": топор на креплении пояса.");
                if(pose==LeoPosePreview.Pose.AxeReady||pose==LeoPosePreview.Pose.AxeContact)check(Vector3.Distance(rig.axeRoot.TransformPoint(LeoAxePose.MainGrip),LeoAxePose.Grip(rig.handR))<.0001f,pose+": топор следует за кистью.");
                if (pose == LeoPosePreview.Pose.BuildReady)
                {
                    check(rig.hammerRoot.gameObject.activeSelf && !rig.axeRoot.gameObject.activeSelf,
                        "В позе стройки в руке только молоток.");
                    check(Vector3.Distance(rig.hammerRoot.position, rig.handGripR.position) < .0001f,
                        "Молоток совпадает с хватом после поворота руки.");
                }
            }
            LeoPosePreview.Apply(rig, LeoPosePreview.Pose.Rest);
            check(rig.axeRoot.gameObject.activeSelf && rig.hammerRoot.gameObject.activeSelf,
                "В Rest топор в руке, молоток виден на поясе.");
            check(rig.HammerHolster != null && Vector3.Distance(rig.hammerRoot.position, rig.HammerHolster.position) < .0001f,
                "Тот же молоток возвращается на пояс.");
            check(rig.hammerRoot.parent == rig.handGripR, "Путь HammerRoot в клипах сохранён.");
            rig.ShowCargo(0,21);check(Active(rig.cargo)==0,"Пустая переноска визуально пуста.");
            rig.ShowCargo(3,21);check(Active(rig.cargo)==0,"0-3 бревна: только пустые крепления.");
            rig.ShowCargo(4,21);check(Active(rig.cargo)==2,"4 бревна: небольшая связка.");
            rig.ShowCargo(15,21);check(Active(rig.cargo)==6,"15 брёвен: максимум малой связки.");
            rig.ShowCargo(16,21);check(Active(rig.cargo)==6&&Active(rig.frames)==1,"16 брёвен/вместимость 21: стопка и первая ступень рамы.");
            rig.ShowCargo(31,64);check(Active(rig.cargo)==8&&Active(rig.frames)==3,"Рама 64+ остаётся при любой загрузке.");
            rig.ShowCargo(0,32);check(Active(rig.cargo)==0&&Active(rig.frames)==2,"Рама 32+ остаётся, когда брёвна сданы.");
            rig.ShowCargo(32,42);check(Active(rig.cargo)==8&&rig.cargoTies2.gameObject.activeSelf,"32 бревна: вторая стяжка.");
            rig.ShowCargo(31,42);check(!rig.cargoTies2.gameObject.activeSelf,"До 32 брёвен второй стяжки нет.");
            rig.ShowCargo(64,64);check(Active(rig.cargo)==12,"64+: дополнительный ряд, 12 брёвен.");
            rig.ShowCargo(999,64);check(Active(rig.cargo)==12,"Переполнение не создаёт лишних брёвен.");
            rig.ShowCargo(1,0);check(Active(rig.cargo)==0,"Нулевая вместимость безопасна.");
            check(LeoRig.FrameTier(15)==0&&LeoRig.FrameTier(16)==1&&LeoRig.FrameTier(32)==2&&LeoRig.FrameTier(64)==3,"Ступени рамы 16/32/64.");
            check(LeoRig.CargoSections(0,21)==0&&LeoRig.CargoSections(3,21)==0,"0-3: нет брёвен.");
            check(LeoRig.CargoSections(4,21)==2&&LeoRig.CargoSections(15,21)==6,"4-15: от 2 до 6.");
            check(LeoRig.CargoSections(16,21)==6&&LeoRig.CargoSections(31,42)==8,"16-31: от 6 до 8.");
            check(LeoRig.CargoSections(32,42)==8&&LeoRig.CargoSections(63,64)==10,"32-63: от 8 до 10.");
            check(LeoRig.CargoSections(64,64)==12&&LeoRig.CargoSections(999,64)==12,"64+: 12.");
            check(LeoRig.CargoSections(1,0)==0,"Нулевая вместимость безопасна.");
            // Leo v3.4: shorter legs, boots keep the ground, jacket hem clears the thighs.
            Vector3 hipToAnkle=rig.ankleR.position-rig.hipR.position;
            check(hipToAnkle.magnitude<.64f&&hipToAnkle.magnitude>.60f,"Ноги укорочены примерно на 8% (бедро-лодыжка .60-.64).");
            check(Mathf.Abs(rig.ankleR.position.y-.16f)<.002f&&Mathf.Abs(rig.ankleL.position.y-.16f)<.002f,"Лодыжки на прежней высоте: подошвы остаются на опоре.");
            check(rig.hemFrontR!=null&&rig.hemFrontL!=null&&rig.hemBackR!=null&&rig.hemBackL!=null,"Четыре панели подола найдены.");
            check(rig.hemFrontR.parent==rig.hips&&rig.hemBackL.parent==rig.hips,"Панели подола сидят на тазу.");
            {
                Quaternion frontRest=rig.hemFrontR.localRotation,backRest=rig.hemBackR.localRotation;
                rig.UpdateJacketHem();
                check(Quaternion.Angle(rig.hemFrontR.localRotation,frontRest)<.01f&&Quaternion.Angle(rig.hemBackR.localRotation,backRest)<.01f,"Подол в стойке не отклоняется.");
                rig.hipR.localRotation=rig.RestRotation(rig.hipR)*Quaternion.Euler(-45f,0f,0f);   // thigh forward
                rig.UpdateJacketHem();
                check(Quaternion.Angle(rig.hemFrontR.localRotation,frontRest)>15f&&Quaternion.Angle(rig.hemBackR.localRotation,backRest)<.01f,"Шаг вперёд уводит передний подол с бедра.");
                rig.hipR.localRotation=rig.RestRotation(rig.hipR)*Quaternion.Euler(35f,0f,0f);    // thigh back
                rig.UpdateJacketHem();
                check(Quaternion.Angle(rig.hemBackR.localRotation,backRest)>10f&&Quaternion.Angle(rig.hemFrontR.localRotation,frontRest)<.01f,"Шаг назад уводит задний подол.");
                rig.ResetPose();rig.UpdateJacketHem();
                check(Quaternion.Angle(rig.hemFrontR.localRotation,frontRest)<.01f,"ResetPose возвращает подол.");
            }
            Vector3 compassHand=rig.handL.position;rig.Rotate(rig.compassNeedle,new Vector3(0,0,70));check(rig.handL.position==compassHand,"Стрелка компаса независима от кисти.");
            for(int i=0;i<200;i++){rig.Rotate(rig.head,new Vector3(12,30,-4));rig.Rotate(rig.upperArmR,new Vector3(-50,0,10));rig.ResetPose();}
            check(Vector3.Distance(handBefore,rig.handR.position)<.00001f&&Quaternion.Angle(rig.head.localRotation,rig.RestRotation(rig.head))<.01f,"Повторные движения не накапливают смещение.");
            check(Array.Exists(data.parts,p=>p.name=="Axe_TW_Right")&&Array.Exists(data.parts,p=>p.name=="Axe_TW_Left"),"Гравировка TW на обеих сторонах.");
            check(root.GetComponentsInChildren<Collider>(true).Length==0&&root.GetComponentsInChildren<Rigidbody>(true).Length==0,"У визуала нет физики.");
        }
        catch(Exception e){errors.Add(e.ToString());}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        if(errors.Count>0)Debug.LogError("LEO TESTS: "+errors.Count+" ошибок\n"+string.Join("\n",errors));
        else Debug.Log("LEO TESTS OK: "+checks+" проверок каркаса, реквизита, поз, груза и возврата. Игровая сцена не менялась.");
    }
    static int Active(Transform[] parts){int count=0;foreach(var part in parts)if(part!=null&&part.gameObject.activeSelf)count++;return count;}
}
