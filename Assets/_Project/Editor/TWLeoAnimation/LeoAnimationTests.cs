using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class LeoAnimationTests
{
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    [MenuItem(LeoAnimationBuilder.Menu+"3. Тесты поз и тайминга",false,372)]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=EditorSceneManager.NewPreviewScene();GameObject instance=null;
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(LeoModelBuilder.PrefabPath);Require(prefab!=null,"Сначала собери Model01.");
            instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.SetActive(false);
            var old=instance.GetComponent<LeoModelPresentation>();if(old!=null)old.enabled=false;
            var rig=instance.GetComponent<LeoRig>();var actor=instance.AddComponent<LeoAnimationController>();actor.rig=rig;
            Require(actor.Initialize(),"Не удалось открыть риг.");Validate(actor,rig,LeoAnimationClips.Bones(rig));
            Debug.Log("LEO MOTION TESTS OK: контакт по исходному расписанию, замкнутые циклы, длины костей, хват топора и компактная переноска до 10 слотов. Игровые награды/сейвы тест не вызывает.");
        }
        catch(Exception e){Debug.LogError("LEO MOTION TESTS: "+e);}
        finally{if(instance!=null)Object.DestroyImmediate(instance);EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static void Validate(LeoAnimationController actor,LeoRig rig,List<Transform> bones)
    {
        Clock();var book=LeoMotionProfile.Load();var lengths=new float[bones.Count];rig.ResetPose();
        for(int i=0;i<bones.Count;i++)lengths[i]=bones[i].localPosition.magnitude;
        Vector3 position=rig.transform.position,scale=rig.transform.localScale;Quaternion rotation=rig.transform.rotation;
        foreach(string name in LeoAnimationBuilder.Clips)
        {
            for(int n=0;n<=60;n++)
            {
                Require(actor.SamplePreview(name,n/60f),"Нет позы "+name);
                for(int i=0;i<bones.Count;i++)
                {
                    var bone=bones[i];var p=bone.localPosition;var q=bone.localRotation;var s=bone.localScale;
                    Require(Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w)&&Finite(s.x)&&Finite(s.y)&&Finite(s.z),name+": неконечная поза "+bone.name);
                    bool translated=bone==rig.hips||bone==rig.axeRoot||bone==rig.hammerRoot||bone==rig.cargoTies||bone==rig.cargoTies2||Array.Exists(rig.cargo,c=>c==bone);
                    if(!translated)Require(Mathf.Abs(p.magnitude-lengths[i])<.0001f,name+": вытянулся сегмент "+bone.name);
                }
                Require(Vector3.Distance(position,rig.transform.position)<.00001f&&Quaternion.Angle(rotation,rig.transform.rotation)<.01f&&Vector3.Distance(scale,rig.transform.localScale)<.00001f,"Поза сдвинула корень игрока.");
                if(name!="Sheathe"&&name!="Draw"&&name!="IdleHolstered")
                {
                    Require(Vector3.Distance(rig.axeRoot.TransformPoint(LeoAxePose.MainGrip),LeoAxePose.Grip(rig.handR))<.0002f,name+": топор вышел из правой руки.");
                    Require(Vector3.Dot(rig.handR.right, rig.axeRoot.up)<-.999f,name+": рукоять должна лежать поперёк ладони.");
                }
                if (rig.HammerHolster != null)
                    Require(Vector3.Distance(rig.hammerRoot.position, rig.HammerHolster.position) < .0002f,
                        name + ": молоток оторвался от пояса.");
                if(name=="HeavyWalk"||name=="HeavyRun"){int visible=0;foreach(var cargo in rig.cargo)if(cargo.gameObject.activeSelf)visible++;Require(visible==LeoRig.CargoSections(21,21,12),"Переноска 21/21 показывает число брёвен по ступени 16-31.");}
            }
            if(LeoAnimationBuilder.Loop(name))
            {
                actor.SamplePreview(name,0);var points=new Vector3[bones.Count];var rotations=new Quaternion[bones.Count];var sizes=new Vector3[bones.Count];
                for(int i=0;i<bones.Count;i++){points[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;sizes[i]=bones[i].localScale;}
                actor.SamplePreview(name,1);
                for(int i=0;i<bones.Count;i++)Require(Vector3.Distance(points[i],bones[i].localPosition)<.0002f&&Quaternion.Angle(rotations[i],bones[i].localRotation)<.15f&&Vector3.Distance(sizes[i],bones[i].localScale)<.0002f,name+": разрыв цикла "+bones[i].name);
            }
        }
        actor.SamplePreview("Chop",book.windup/(book.windup+book.recovery));
        Require(Vector3.Distance(LeoAxePose.Grip(rig.handL),rig.axeRoot.TransformPoint(LeoAxePose.SupportGrip))<.025f,"Левая рука не держит рукоять в контакте.");
        actor.SamplePreview("Sheathe",1);Require(Vector3.Distance(rig.axeRoot.position,rig.axeHolster.position)<.0002f&&Quaternion.Angle(rig.axeRoot.rotation,rig.axeHolster.rotation)<.15f,"Топор не закрепился на поясе.");
        Require(rig.logUpgrade!=null&&rig.cargo!=null&&rig.cargo.Length==12&&rig.frames!=null&&rig.frames.Length==3,"Нет 12 слотов брёвен или рамы переноски.");
        rig.ShowCargo(36,42);int larger=0;foreach(var section in rig.cargo)if(section.gameObject.activeSelf)larger++;
        Require(larger==LeoRig.CargoSections(36,42,12)&&!rig.logUpgrade.gameObject.activeSelf,"36/42: число брёвен по ступени 32-63, без устаревшей рамы-расширения.");
        rig.ShowCargo(21,21);Require(!rig.logUpgrade.gameObject.activeSelf,"Вместимость не должна показывать расширение.");
        actor.SamplePreview("Idle",0);foreach(var cargo in rig.cargo)Require(!cargo.gameObject.activeSelf,"Пустой рюкзак показывает груз.");
        Require(rig.GetComponentsInChildren<Collider>(true).Length==0&&rig.GetComponentsInChildren<Rigidbody>(true).Length==0,"У визуала появилась физика.");
    }
    static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    static void Clock()
    {
        var clock=new LeoImpactClock();clock.Forecast(0,.25f,.28f);
        Require(clock.Phase(.12f,.62f,.26f,false)>0&&clock.Phase(.24f,.62f,.26f,false)<.62f,"Первый удар не имеет подготовки.");
        clock.Hit(.25f);Require(Mathf.Abs(clock.Phase(.25f,.62f,.26f,true)-.62f)<.00001f,"Контакт отстаёт от игрового удара.");
        clock.Forecast(.25f,.87f,.28f);Require(clock.Phase(.38f,.62f,.26f,false)>.62f,"Нет восстановления после удара.");
        Require(clock.Phase(.75f,.62f,.26f,false)>0&&clock.Phase(.75f,.62f,.26f,false)<.62f,"Нет подготовки следующего удара.");
        clock.Clear();clock.Hit(1);Require(clock.Phase(1,.62f,.26f,true)==.62f,"Нулевой impactDelay не показал контакт.");
        clock.Forecast(1,1.15f,.28f);
        for(int i=0;i<=150;i++){float phase=clock.Phase(1+i*.001f,.62f,.26f,false);Require(Finite(phase)&&phase>=0&&phase<=1,"Быстрый интервал даёт неверную фазу.");}
        clock.ForgetForecast();Require(clock.Phase(2,.62f,.26f,false)==0,"После отмены остался запланированный замах.");
        clock.Clear();Require(!clock.Recovering(2,.26f),"Загрузка не очистила предыдущий удар.");
    }
}
