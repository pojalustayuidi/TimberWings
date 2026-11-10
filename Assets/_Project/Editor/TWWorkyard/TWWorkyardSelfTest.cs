using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    internal static class TWWorkyardSelfTest
    {
        [MenuItem("Tools/BuilderSim/Здания/WORKYARD ART/8. Проверить производство и отображение",false,327)]
        public static void RunMenu()
        {
            try
            {
                var log=new StringBuilder();int errors=WorkerProductionSelfTest.Run(log);
                void Check(bool value,string message){log.AppendLine((value?"OK ":"FAIL ")+message);if(!value)errors++;}
                var sim=new CampSimulation(new System.Random(7241));
                var lumber=new WorkerState("test_lumber",WorkerRole.Lumberjack,WorkerRarity.Common,null);
                var sawyer=new WorkerState("test_sawyer",WorkerRole.Sawyer,WorkerRarity.Common,null);
                var clerk=new WorkerState("test_clerk",WorkerRole.Clerk,WorkerRarity.Common,null);
                sim.AddToRoster(lumber);sim.AddToRoster(sawyer);sim.AddToRoster(clerk);
                sim.AutoAssign(lumber);sim.AutoAssign(sawyer);sim.AutoAssign(clerk);
                Check(WorkyardReadout.Activity(sim,null,true)==WorkyardActivity.Empty,"Пустой слот не показывает рабочего");
                Check(WorkyardReadout.Activity(sim,lumber,false)==WorkyardActivity.Closed,"До открытия артели NPC не работает");
                Check(WorkyardReadout.Activity(sim,sawyer,true)==WorkyardActivity.NoLogs,"Без брёвен пильщик ждёт");
                sim.logs=WorkerBalance.SawyerLogsPerPlank;
                Check(WorkyardReadout.Activity(sim,sawyer,true)==WorkyardActivity.Working,"Сырьё запускает визуальный распил");
                sim.Step(0.25f);sim.logs=0;
                Check(sawyer.sawActive && WorkyardReadout.Activity(sim,sawyer,true)==WorkyardActivity.Working,"Уже оплаченный распил продолжается без новых брёвен");
                sim.logs=sim.LogCap;sim.planks=sim.PlankCap;
                Check(WorkyardReadout.Activity(sim,lumber,true)==WorkyardActivity.Full,"Полный склад брёвен останавливает дровосека");
                Check(WorkyardReadout.Activity(sim,sawyer,true)==WorkyardActivity.Full,"Полный склад досок останавливает анимацию пильщика");
                Check(WorkyardReadout.Activity(sim,clerk,true)==WorkyardActivity.Working,"Доход приказчика сохраняет исходный баланс при полном складе");
                int logs=sim.logs,planks=sim.planks;double xp=lumber.totalXp,progress=sawyer.sawProgress;
                for(int i=0;i<100;i++)
                {
                    WorkyardReadout.Activity(sim,lumber,true);WorkyardReadout.Activity(sim,sawyer,true);
                    WorkyardTree.Timing.CycleSeconds(sim.LumberRate(lumber));WorkyardTree.Timing.LumberSwing(WorkyardTree.Timing.Phase(lumber.logAcc));
                }
                Check(sim.logs==logs && sim.planks==planks && lumber.totalXp==xp && sawyer.sawProgress==progress,"Отображение не меняет склад, опыт и прогресс производства");
                Check(WorkyardReadout.VisibleUnits(0,30,12)==0,"Пустой склад визуально пуст");
                Check(WorkyardReadout.VisibleUnits(1,30,12)>0,"Первый ресурс уже виден");
                Check(WorkyardReadout.VisibleUnits(30,30,12)==12,"Полный склад заполняет все секции");
                Check(WorkyardReadout.VisibleUnits(99,30,12)==12,"Видимые секции ограничены вместимостью");
                Check(WorkyardReadout.VisibleUnits(1,0,12)==0,"Нулевая вместимость безопасна");
                int previous=0;
                for(int i=0;i<=90;i++)
                {
                    int n=WorkyardReadout.VisibleUnits(i,90,12);if(n<previous || n>12){errors++;log.AppendLine("FAIL монотонное заполнение");break;}previous=n;
                }
                Check(Mathf.Abs(WorkyardTree.Timing.CycleSeconds(0.2)-5)<0.0001f && Mathf.Abs(WorkyardTree.Timing.CycleSeconds(0.4)-2.5f)<0.0001f,
                    "Темп рубки читает реальную скорость: 0.2 бр/с = 5 с, 0.4 бр/с = 2.5 с");
                Check(WorkyardTree.Timing.Phase(double.NaN)==0 && WorkyardTree.Timing.CycleSeconds(0)>0,"Некорректные визуальные входы безопасны");
                foreach(double rate in new[]{0.01,0.2,0.4,1.4,12.5})
                {
                    float cycle=WorkyardTree.Timing.CycleSeconds(rate);
                    Check(WorkyardTree.Timing.FallSeconds(cycle)>0 && WorkyardTree.Timing.FallSeconds(cycle)<WorkyardTree.Timing.GrowStart(cycle) &&
                        WorkyardTree.Timing.ReadySeconds(cycle)<cycle/3f,"Дерево готово до следующего удара при скорости "+rate+" бр/с");
                }
                var testObject=new GameObject("Workyard_Timing_Test"){hideFlags=HideFlags.HideAndDontSave};
                try
                {
                    var tree=testObject.AddComponent<WorkyardTree>();tree.crown=new GameObject("Crown").transform;tree.crown.SetParent(testObject.transform,false);
                    tree.stump=new GameObject("Stump");tree.stump.transform.SetParent(testObject.transform,false);tree.stump.SetActive(false);
                    var timing=new CampSimulation(new System.Random(97));var worker=new WorkerState("timed",WorkerRole.Lumberjack,WorkerRarity.Common,null);
                    timing.AddToRoster(worker);timing.AutoAssign(worker);
                    timing.Produced+=p=>{if(p.resource==CampResource.Logs)tree.Produced(WorkyardTree.Timing.CycleSeconds(timing.LumberRate(worker)));};
                    for(int i=0;i<19;i++){timing.Step(0.25f);tree.Tick(0.25f);}
                    Check(timing.logs==0 && Quaternion.Angle(tree.crown.localRotation,Quaternion.identity)<0.01f && !tree.stump.activeSelf,
                        "До выработки бревна дерево не падает");
                    timing.Step(0.25f);tree.Tick(0.1f);
                    Check(timing.logs==1 && Quaternion.Angle(tree.crown.localRotation,Quaternion.identity)>0.1f && tree.stump.activeSelf,
                        "Реальное бревно на пятой секунде запускает падение дерева");
                    Quaternion paused=tree.crown.localRotation;tree.Tick(0);
                    Check(Quaternion.Angle(paused,tree.crown.localRotation)<0.001f,"Пауза не двигает падение дерева");
                    tree.Produced(2);tree.Tick(0.7f);
                    Check(tree.crown.gameObject.activeSelf && !tree.stump.activeSelf && Vector3.Distance(tree.crown.localScale,Vector3.one)<0.001f,
                        "У быстрого рабочего дерево восстанавливается раньше нового цикла");
                    tree.Produced(5);tree.Tick(0.7f);
                    Check(!tree.crown.gameObject.activeSelf && tree.stump.activeSelf,"Восстановление дерева зависит от темпа рабочего");
                    Check(timing.logs==1 && timing.planks==0,"Анимация дерева сама не выдаёт ресурсы");
                }
                finally{UnityEngine.Object.DestroyImmediate(testObject);}
                Check(TWWorkyardArt.ChooseWorkerManagerIndex(new bool[0],-1)==-1,"Без менеджеров проверка физики не выбирает случайный объект");
                Check(TWWorkyardArt.ChooseWorkerManagerIndex(new[]{false,true,false},-1)==1,"Неактивные менеджеры не мешают выбору единственного активного");
                Check(TWWorkyardArt.ChooseWorkerManagerIndex(new[]{true,true},1)==1,"При нескольких активных сохраняется явная связь установленного двора");
                Check(TWWorkyardArt.ChooseWorkerManagerIndex(new[]{false,true},0)==1,"Активный менеджер имеет приоритет над выключенной старой связью");
                Check(TWWorkyardArt.ChooseWorkerManagerIndex(new[]{true,true},-1)==-1,"Несколько активных без связи не выбираются по порядку Hierarchy");
                Check(TWWorkyardArt.ChooseWorkerManagerIndex(new[]{false,false},1)==1,"Сохранённый неактивный менеджер доступен для проверки его настроек");
                Check(TWWorkyardArt.ChooseWorkerManagerIndex(new[]{false,false},-1)==-1,"Несколько неактивных без связи не создают ложный выбор");
                Check(TWWorkyardArt.ChooseWorkerManagerIndex(new[]{true},99)==0,"Устаревший индекс связи безопасно игнорируется");
                if(errors!=0)throw new InvalidOperationException(errors+" ошибок\n"+log);
                Debug.Log("WORKYARD TESTS: PASS. Штатные тесты производства и проверки нового отображения.\n"+log);
            }
            catch(Exception e){Debug.LogError("WORKYARD TESTS: "+e);}
        }
    }
}
