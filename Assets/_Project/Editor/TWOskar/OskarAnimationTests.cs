using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tests use a temporary preview scene. They never animate or save the user's NPC.
public static class OskarAnimationTests
{
    [MenuItem(OskarAnimationTools.Menu+"3. Тест движений и переходов",false,312)]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("OSKAR MOTION TESTS: останови Play.");return;}
        var errors=new List<string>();int checks=0;
        Action<bool,string> check=(ok,message)=>{checks++;if(!ok&&errors.Count<30)errors.Add(message);};
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var book=OskarMotionProfile.Load();
            check(book!=null&&book.version==1&&book.clips!=null&&book.clips.Length==5,"Нужен профиль версии 1 с пятью жестами.");
            if(book==null||book.clips==null)throw new InvalidOperationException("Нет Motion.json.");
            var ids=new HashSet<string>();
            foreach(var clip in book.clips)
            {
                check(clip!=null&&ids.Add(clip.id),"Уникальные идентификаторы жестов.");
                if(clip==null)continue;
                check(clip.duration>0&&clip.holdStart>0&&clip.holdEnd<1&&clip.holdStart<clip.holdEnd,clip.id+": длительность и границы.");
                check(clip.frames!=null&&clip.frames.Length>=4,clip.id+": кадры профиля.");
                if(clip.frames==null)continue;
                float previous=-1;
                foreach(var frame in clip.frames){check(frame!=null&&frame.t>previous&&frame.t>=0&&frame.t<=1,clip.id+": возрастающее время.");if(frame!=null)previous=frame.t;}
                check(clip.frames[0].t==0&&clip.frames[clip.frames.Length-1].t==1,clip.id+": обе границы.");
                check(Zero(OskarMotionProfile.Sample(clip,0))&&Zero(OskarMotionProfile.Sample(clip,1)),clip.id+": возврат в покой.");
                var held=OskarMotionProfile.Sample(clip,clip.holdStart);
                var end=OskarMotionProfile.Sample(clip,clip.holdEnd);
                check(Equal(held,end),clip.id+": выход из удержания не меняет позу скачком.");
                foreach(float phase in new[]{.1f,.25f,.5f,.85f})
                {
                    var clock=new OskarMotionProfile.Clock();clock.Start(clip);clock.Tick(clip.duration*phase);clock.Return();
                    var before=OskarMotionProfile.Sample(clip,phase);var after=OskarMotionProfile.Sample(clip,clock.phase);
                    check(Equal(before,after),clip.id+": отмена не телепортирует реквизит.");
                    for(int i=0;i<240&&clock.clip!=null;i++)clock.Tick(.025f);
                    check(clock.clip==null,clip.id+": отменённое движение завершается.");
                }
                var normal=new OskarMotionProfile.Clock();normal.Start(clip);normal.Tick(clip.duration+.01f);
                check(normal.clip==null,clip.id+": действие не зациклено.");
            }
            var approach=new OskarMotionProfile.Approach();approach.Reset(0);
            check(!approach.Enter(2,5,10,true),"При загрузке рядом приветствие не стартует.");
            approach.Enter(8,5,10,true);check(approach.Enter(4,5,11,true),"Новый подход приветствуется.");
            check(!approach.Enter(4,5,12,true),"Приветствие не повторяется, пока игрок рядом.");
            approach.Enter(8,5,13,true);check(!approach.Enter(4,5,14,true),"Пауза между приветствиями — 35 секунд.");
            approach.Enter(8,5,50,true);check(!approach.Enter(4,5,51,false),"Во время диалога приветствие запрещено.");
            check(!approach.Enter(4,5,52,true),"Приветствие не догоняет игрока после закрытия окна.");
            var data=OskarModelData.Load();var root=new GameObject("Oskar_Animation_Test");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
            var nodes=new Dictionary<string,Transform>();
            foreach(var def in data.nodes)
            {
                var go=new GameObject(def.name);go.transform.SetParent(string.IsNullOrEmpty(def.parent)?root.transform:nodes[def.parent],false);
                go.transform.localPosition=OskarModelData.V(def.position);go.transform.localRotation=OskarModelData.Q(def.rotation);nodes.Add(def.name,go.transform);
            }
            var rig=root.AddComponent<OskarRig>();rig.Bind();
            var pencil=new GameObject("WorkingPencil").transform;pencil.SetParent(rig.pocket,false);pencil.position=root.transform.TransformPoint(new Vector3(-.29f,1,.345f));pencil.localRotation=Quaternion.Euler(0,0,-12);pencil.localScale=Vector3.one*.6f;
            rig.CaptureRest();var actor=root.AddComponent<OskarIdle>();actor.rig=rig;actor.workingPencil=pencil;
            Vector3 leftFoot=rig.ankleL.position,rightFoot=rig.ankleR.position,rootPosition=root.transform.position,pencilHome=pencil.localPosition,toolHome=rig.planer.localPosition;
            Quaternion pencilRotation=pencil.localRotation,toolRotation=rig.planer.localRotation;
            float upperL=Vector3.Distance(rig.shoulderL.position,rig.elbowL.position),lowerL=Vector3.Distance(rig.elbowL.position,rig.handL.position);
            float upperR=Vector3.Distance(rig.shoulderR.position,rig.elbowR.position),lowerR=Vector3.Distance(rig.elbowR.position,rig.handR.position);
            foreach(var clip in book.clips)
            {
                for(int i=0;i<=100;i++)
                {
                    float phase=i/100f;check(actor.SamplePreview(clip.id,phase),clip.id+": семпл.");
                    check(Vector3.Distance(root.transform.position,rootPosition)<.00001f&&Vector3.Distance(rig.ankleL.position,leftFoot)<.00001f&&Vector3.Distance(rig.ankleR.position,rightFoot)<.00001f,clip.id+": ноги и корень неподвижны.");
                    check(Mathf.Abs(Vector3.Distance(rig.shoulderL.position,rig.elbowL.position)-upperL)<.00005f&&Mathf.Abs(Vector3.Distance(rig.elbowL.position,rig.handL.position)-lowerL)<.00005f,clip.id+": левая рука не растягивается.");
                    check(Mathf.Abs(Vector3.Distance(rig.shoulderR.position,rig.elbowR.position)-upperR)<.00005f&&Mathf.Abs(Vector3.Distance(rig.elbowR.position,rig.handR.position)-lowerR)<.00005f,clip.id+": правая рука не растягивается.");
                    check(Vector3.Distance(rig.planer.localPosition,toolHome)<.00001f&&Quaternion.Angle(rig.planer.localRotation,toolRotation)<.01f,clip.id+": рубанок закреплён в кисти.");
                    var pose=OskarMotionProfile.Sample(clip,phase);
                    if(clip.id=="blueprint"&&pose.grip>.999f)
                        check(Vector3.Distance(pencil.position,rig.handL.TransformPoint(new Vector3(0,0,.055f)))<.0001f,"Карандаш следует за ладонью.");
                    if(clip.id=="blueprint"&&pose.carry>.999f)
                        check(Vector3.Distance(pencil.TransformPoint(Vector3.up*.1935f),rig.blueprint.TransformPoint(OskarMotionProfile.V(clip.leftPoint)))<.012f,"Кончик карандаша касается бумаги.");
                }
                check(Vector3.Distance(pencil.localPosition,pencilHome)<.0001f&&Quaternion.Angle(pencil.localRotation,pencilRotation)<.01f,clip.id+": карандаш возвращён в карман.");
            }
            root.transform.SetPositionAndRotation(new Vector3(7,2,-3),Quaternion.Euler(0,37,0));root.transform.localScale=Vector3.one*1.15f;
            actor.SamplePreview("blueprint",.5f);
            var paperClip=OskarMotionProfile.Find(book,"blueprint");
            check(Vector3.Distance(pencil.TransformPoint(Vector3.up*.1935f),rig.blueprint.TransformPoint(OskarMotionProfile.V(paperClip.leftPoint)))<.014f,"Поворот, перенос и равномерный масштаб модели сохраняют контакт с бумагой.");
            actor.Restore();check(rig.HasRest&&rig.JointCount==39,"Риг сохраняет исходные узлы и рабочий карандаш.");
        }
        catch(Exception e){errors.Add(e.ToString());}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        if(errors.Count>0)Debug.LogError("OSKAR MOTION TESTS: "+errors.Count+" ошибок\n"+string.Join("\n",errors));
        else Debug.Log("OSKAR MOTION TESTS OK: "+checks+" проверок. Руки, реквизит, переходы, тихий старт и возврат позы. Игровая сцена не менялась.");
    }
    static bool Zero(OskarMotionProfile.Pose p){return Equal(p,default(OskarMotionProfile.Pose));}
    static bool Equal(OskarMotionProfile.Pose a,OskarMotionProfile.Pose b)
    {
        return Mathf.Abs(a.leftWeight-b.leftWeight)<.0001f&&Mathf.Abs(a.rightWeight-b.rightWeight)<.0001f&&Mathf.Abs(a.carry-b.carry)<.0001f&&Mathf.Abs(a.grip-b.grip)<.0001f&&
            Vector3.Distance(a.head,b.head)<.0001f&&Vector3.Distance(a.spine,b.spine)<.0001f&&Vector3.Distance(a.leftWrist,b.leftWrist)<.0001f&&Vector3.Distance(a.rightWrist,b.rightWrist)<.0001f&&Vector3.Distance(a.squirrel,b.squirrel)<.0001f&&Vector3.Distance(a.planerGrip,b.planerGrip)<.0001f;
    }
}
