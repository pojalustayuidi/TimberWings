using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    internal static class TWWorkyardModels
    {
        static readonly Color W=Color.white;
        static void Box(TWWorkyardModel m,string c,Vector3 p,Vector3 size,float yaw=0)
        {m.Part(c).Box(p,size,Quaternion.Euler(0,yaw,0),W);}
        static void Rod(TWWorkyardModel m,string c,Vector3 a,Vector3 b,float r)
        {m.Part(c).Cyl(a,b,r,r*0.94f,W,8);}
        static void Log(TWWorkyardModel m,Vector3 a,Vector3 b,float r)
        {
            m.Part("Bark").Cyl(a,b,r,r,W,8);Vector3 d=(b-a).normalized*0.009f;
            foreach(var p in new[]{a-d,b+d})
            {m.Part("EndGrain").Cyl(p-d,p+d,r*0.86f,r*0.86f,W,8);m.Part("Wood").Cyl(p-d*1.3f,p+d*1.3f,r*0.36f,r*0.36f,W,8);}
        }
        static void Stone(TWWorkyardModel m,Vector3 p,Vector3 scale)
        {m.Part("Stone").Puff(p,scale,W,17,7,3,0.12f);}
        static GameObject Child(Transform parent,string name,Vector3 p)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=p;return go;}
        static GameObject Local(TWWorkyardAssets a,TWWorkyardModel m,Transform parent,string key,Vector3 p)
        {return m.Finish(a,parent,key,parent.TransformPoint(p),parent.rotation);}

        public static void Site(TWWorkyardContext x,TWWorkyardAssets a,Transform abandoned,Transform frame)
        {
            var m=new TWWorkyardModel();
            for(int i=0;i<8;i++)Box(m,"Wood",new Vector3(-1.5f+i*0.36f,0.06f,-0.5f+i*0.06f),new Vector3(0.27f,0.10f,2.5f),i%2==0?-8:11);
            for(int i=0;i<3;i++)Log(m,new Vector3(2.0f,0.16f+i*0.19f,-0.65f),new Vector3(3.0f,0.16f+i*0.19f,0.1f),0.13f);
            m.Part("CanvasShade").Puff(new Vector3(-1.1f,0.15f,0.8f),new Vector3(0.82f,0.16f,0.58f),W,13,8,3,0.05f);
            Rod(m,"DarkWood",new Vector3(-1.8f,0,-1.3f),new Vector3(-1.0f,1.1f,-1.1f),0.1f);
            Box(m,"Banner",new Vector3(-0.9f,0.7f,-1.1f),new Vector3(0.7f,0.4f,0.06f),-14);
            foreach(int s in new[]{-1,1}){Stone(m,new Vector3(s*1.85f,0.12f,-1.4f),new Vector3(0.32f,0.2f,0.3f));Rod(m,"Wood",new Vector3(s*1.85f,0,1.0f),new Vector3(s*1.85f,0.5f,1.0f),0.045f);}
            x.Finish(a,abandoned,m,"Site_Broken","00_Заброшенная_стоянка",Vector2.zero);
            x.Finish(a,frame,Canopy(false),"Site_Frame","01_Каркас_артели",new Vector2(0,-1.1f));
            var stacked=new TWWorkyardModel();for(int i=0;i<5;i++)Box(stacked,"EndGrain",new Vector3(0,0.1f+i*0.09f,0),new Vector3(1.8f,0.065f,0.55f),i%2==0?3:-3);
            x.Finish(a,frame,stacked,"Site_Materials","02_Материалы_стройки",new Vector2(2.6f,1.1f));
        }

        static TWWorkyardModel Canopy(bool done)
        {
            var m=new TWWorkyardModel();
            foreach(int s in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                Vector3 p=new Vector3(s*2.0f,0,z*1.24f);Rod(m,"DarkWood",p-Vector3.up*0.10f,p+Vector3.up*2.15f,0.12f);
                Stone(m,p+Vector3.up*0.1f,new Vector3(0.25f,0.16f,0.25f));
                Rod(m,"Wood",p+Vector3.up*1.65f,p+new Vector3(-s*0.55f,2.2f,0),0.065f);
            }
            foreach(int z in new[]{-1,1})
            {
                Rod(m,"Wood",new Vector3(-2.13f,2.15f,z*1.24f),new Vector3(0,2.7f,z*1.24f),0.10f);
                Rod(m,"Wood",new Vector3(0,2.7f,z*1.24f),new Vector3(2.13f,2.15f,z*1.24f),0.10f);
            }
            Rod(m,"Wood",new Vector3(0,2.7f,-1.4f),new Vector3(0,2.7f,1.4f),0.10f);
            if(!done)return m;
            for(int side=-1;side<=1;side+=2)
            {
                m.Part("Canvas").Quad(new Vector3(0,2.76f,-1.5f),new Vector3(side*2.25f,2.15f,-1.5f),new Vector3(side*2.25f,2.15f,1.45f),new Vector3(0,2.76f,1.45f),W,Vector3.up);
                m.Part("CanvasShade").Quad(new Vector3(0,2.75f,-1.5f),new Vector3(side*2.25f,2.14f,-1.5f),new Vector3(side*2.25f,2.14f,1.45f),new Vector3(0,2.75f,1.45f),W,Vector3.down);
                m.Part("Banner").Fan(new[]{new Vector3(0,2.76f,1.47f),new Vector3(side*2.25f,2.15f,1.47f),new Vector3(side*2.25f,1.99f,1.47f),new Vector3(0,2.60f,1.47f)},W,Vector3.forward,true);
                Box(m,"Rope",new Vector3(side*2.0f,1.87f,1.24f),new Vector3(0.28f,0.12f,0.28f));
            }
            // Open front: the working crew remains visible from the approach.
            for(int i=0;i<9;i++)Box(m,"Wood",new Vector3(-1.8f+i*0.45f,0.08f,0),new Vector3(0.42f,0.12f,2.6f));
            Box(m,"DarkWood",new Vector3(0,2.17f,1.51f),new Vector3(1.35f,0.38f,0.12f));
            foreach(int s in new[]{-1,1})Rod(m,"Rope",new Vector3(s*0.48f,2.60f,1.5f),new Vector3(s*0.48f,2.35f,1.5f),0.018f);
            m.Part("Copper").Star(new Vector3(0,2.17f,1.59f),Vector3.right,Vector3.up,0.15f,0.065f,5,W);
            foreach(int s in new[]{-1,1})Box(m,"Copper",new Vector3(s*0.36f,2.16f,1.59f),new Vector3(0.34f,0.035f,0.025f));
            return m;
        }

        public static void Built(TWWorkyardContext x,TWWorkyardAssets a)
        {
            var canopy=x.Finish(a,x.built,Canopy(true),"Built_Canopy","01_Открытая_мастерская",new Vector2(0,-1.1f));
            Lamp(a,canopy.transform,"Workyard_EntranceLamp",new Vector3(-1.62f,1.85f,1.3f));
            for(int i=0;i<WorkerBalance.MaxPerRole;i++)
            {
                Station(x,a,WorkerRole.Sawyer,i,new Vector2((i-1)*1.25f,-1.9f),0);
                Station(x,a,WorkerRole.Clerk,i,new Vector2((i-1)*1.05f,0.2f),0);
                Vector2 p=i==0?new Vector2(-3.6f,-2.4f):i==1?new Vector2(-4.0f,0):new Vector2(-2.9f,2.0f);
                Station(x,a,WorkerRole.Lumberjack,i,p,i==2?-50:-90);
            }
            Warehouse(x,a);
        }

        static void Station(TWWorkyardContext x,TWWorkyardAssets a,WorkerRole role,int index,Vector2 p,float yaw)
        {
            var m=new TWWorkyardModel();string key="Station_"+role+"_"+index;
            if(role==WorkerRole.Lumberjack)
            {
                Rod(m,"Wood",new Vector3(-0.55f,0,0.8f),new Vector3(-0.55f,0.6f,0.8f),0.045f);
                Box(m,"Banner",new Vector3(-0.55f,0.55f,0.85f),new Vector3(0.32f,0.22f,0.04f));
                for(int i=0;i<6;i++)Box(m,"Chips",new Vector3(Mathf.Cos(i*2.4f)*0.44f,0.035f,1+Mathf.Sin(i*2.4f)*0.4f),new Vector3(0.12f,0.025f,0.04f),i*41);
            }
            else
            {
                float z=role==WorkerRole.Sawyer?0.55f:0.60f;
                for(int j=0;j<3;j++)Box(m,"Wood",new Vector3(0,0.66f,z-0.2f+j*0.20f),new Vector3(0.98f,0.10f,0.18f));
                foreach(int s in new[]{-1,1})Box(m,"DarkWood",new Vector3(s*0.36f,0.32f,z),new Vector3(0.12f,0.68f,0.5f));
                if(role==WorkerRole.Sawyer)Log(m,new Vector3(-0.39f,0.82f,z),new Vector3(0.4f,0.82f,z),0.10f);
                else
                {
                    Box(m,"Cup",new Vector3(-0.12f,0.73f,z),new Vector3(0.42f,0.04f,0.31f),-10);
                    for(int j=0;j<3;j++)Box(m,"Wood",new Vector3(-0.15f,0.755f,z-0.08f+j*0.055f),new Vector3(0.24f,0.008f,0.009f),-10);
                    for(int j=0;j<3;j++)m.Part("Copper").Cyl(new Vector3(0.3f,0.73f+j*0.02f,z),new Vector3(0.3f,0.745f+j*0.02f,z),0.055f,0.055f,W,8);
                }
            }
            var go=x.Finish(a,x.built,m,key,(role==WorkerRole.Lumberjack?"03_Дерево_дровосека_":role==WorkerRole.Sawyer?"02_Станок_пильщика_":"04_Стол_приказчика_")+(index+1),p,yaw);
            if(go.GetComponent<WorkyardStation>()!=null)return;
            var station=go.AddComponent<WorkyardStation>();station.role=role;station.index=index;
            station.actorPoint=Child(go.transform,"Место_рабочего",Vector3.zero).transform;
            if(role==WorkerRole.Lumberjack)
            {
                var tree=Child(go.transform,"Дерево_артели",new Vector3(0,0,1.0f));station.tree=tree.AddComponent<WorkyardTree>();
                var crown=new TWWorkyardModel();Rod(crown,"Bark",Vector3.zero,Vector3.up*1.25f,0.13f);
                crown.Part("Leaf").Puff(new Vector3(0,1.5f,0),new Vector3(0.66f,0.65f,0.61f),W,11,7,4,0.15f);
                crown.Part("LeafLight").Puff(new Vector3(0.2f,1.85f,0.05f),new Vector3(0.48f,0.45f,0.44f),W,38,7,4,0.12f);
                station.tree.crown=Local(a,crown,tree.transform,key+"_Crown",Vector3.zero).transform;
                var stump=new TWWorkyardModel();Log(stump,Vector3.zero,Vector3.up*0.27f,0.14f);
                station.tree.stump=Local(a,stump,tree.transform,key+"_Stump",Vector3.zero);station.tree.stump.SetActive(false);
            }
            else
            {
                if(role==WorkerRole.Sawyer)
                {
                    var saw=new TWWorkyardModel();Box(saw,"Metal",Vector3.zero,new Vector3(0.055f,0.18f,0.56f));
                    Box(saw,"Wood",new Vector3(0,0.1f,-0.28f),new Vector3(0.08f,0.12f,0.13f));
                    station.movingSaw=Local(a,saw,go.transform,key+"_Saw",new Vector3(0,0.91f,0.55f)).transform;
                }
            }
        }

        static void Warehouse(TWWorkyardContext x,TWWorkyardAssets a)
        {
            var m=new TWWorkyardModel();
            foreach(int s in new[]{-1,1})foreach(int z in new[]{-1,1})Rod(m,"DarkWood",new Vector3(s*1.38f,0,z*0.7f),new Vector3(s*1.38f,1.5f,z*0.7f),0.07f);
            Box(m,"Wood",new Vector3(0,0.1f,0),new Vector3(2.85f,0.13f,1.5f));
            for(int i=0;i<8;i++)Box(m,"Wood",new Vector3(-1.28f+i*0.365f,1.55f,0),new Vector3(0.34f,0.1f,1.65f));
            Rod(m,"Wood",new Vector3(0,0.1f,-0.65f),new Vector3(0,1.4f,-0.65f),0.06f);
            Box(m,"Banner",new Vector3(0,1.39f,0.75f),new Vector3(1.65f,0.26f,0.05f));
            Log(m,new Vector3(-0.55f,1.39f,0.79f),new Vector3(-0.25f,1.39f,0.79f),0.075f);
            Box(m,"EndGrain",new Vector3(0.42f,1.39f,0.79f),new Vector3(0.36f,0.09f,0.025f));
            var go=x.Finish(a,x.built,m,"Warehouse","05_Склад_артели",new Vector2(2.9f,1.8f));
            if(go.GetComponent<WorkyardStockDisplay>()!=null)return;
            var stock=go.AddComponent<WorkyardStockDisplay>();stock.logUnits=new GameObject[12];stock.plankUnits=new GameObject[10];
            for(int i=0;i<stock.logUnits.Length;i++)
            {
                var unit=new TWWorkyardModel();Log(unit,new Vector3(0,0,-0.53f),new Vector3(0,0,0.53f),0.105f);
                stock.logUnits[i]=Local(a,unit,go.transform,"LogUnit_"+i,new Vector3(-1.02f+(i%4)*0.23f,0.27f+(i/4)*0.22f,0));stock.logUnits[i].SetActive(false);
            }
            for(int i=0;i<stock.plankUnits.Length;i++)
            {
                var unit=new TWWorkyardModel();Box(unit,"EndGrain",Vector3.zero,new Vector3(1.03f,0.06f,1.25f));
                stock.plankUnits[i]=Local(a,unit,go.transform,"PlankUnit_"+i,new Vector3(0.7f,0.22f+i*0.075f,0));stock.plankUnits[i].SetActive(false);
            }
            stock.logsFull=Full(a,go.transform,"LogsFull",new Vector3(-0.64f,1.39f,0.80f));
            stock.planksFull=Full(a,go.transform,"PlanksFull",new Vector3(0.64f,1.39f,0.80f));
            var spot=Child(go.transform,"Взаимодействие_со_складом",new Vector3(0,0,1.05f)).AddComponent<WorkyardWorkSpot>();spot.camp=x.camp;
        }
        static GameObject Full(TWWorkyardAssets a,Transform parent,string key,Vector3 p)
        {
            var m=new TWWorkyardModel();Box(m,"Warning",Vector3.zero,new Vector3(0.21f,0.24f,0.025f));
            Box(m,"DarkWood",new Vector3(0,0.035f,0.02f),new Vector3(0.025f,0.10f,0.015f));Box(m,"DarkWood",new Vector3(0,-0.065f,0.02f),Vector3.one*0.027f);
            var go=Local(a,m,parent,key,p);go.SetActive(false);return go;
        }
        static void Lamp(TWWorkyardAssets a,Transform parent,string key,Vector3 p)
        {
            if(parent.Find(key)!=null)return;
            var m=new TWWorkyardModel();
            foreach(int s in new[]{-1,1})Box(m,"DarkWood",new Vector3(0,s*0.16f,0),new Vector3(0.3f,0.045f,0.3f));
            foreach(int s in new[]{-1,1})foreach(int z in new[]{-1,1})Rod(m,"Metal",new Vector3(s*0.12f,-0.16f,z*0.12f),new Vector3(s*0.12f,0.16f,z*0.12f),0.018f);
            var frame=Local(a,m,parent,key,p);Undo.RegisterCreatedObjectUndo(frame,"Workyard lantern");
            var glass=new TWWorkyardModel();Box(glass,"Glass",Vector3.zero,new Vector3(0.21f,0.27f,0.21f));
            var glow=Local(a,glass,frame.transform,key+"Glass",Vector3.zero);
            var source=frame.AddComponent<FireSource>();source.emissive=new[]{glow.GetComponent<Renderer>()};source.range=4.5f;source.intensity=1.5f;source.glowSize=0.55f;
        }

        public static WorkyardActor Actor(TWWorkyardAssets a,WorkerRole role)
        {
            var root=new GameObject("Worker_"+role);
            try
            {
                string shirt=role==WorkerRole.Lumberjack?"GreenShirt":role==WorkerRole.Sawyer?"BlueShirt":"OchreShirt";
                var body=new TWWorkyardModel();Box(body,shirt,new Vector3(0,0.72f,0),new Vector3(0.36f,0.37f,0.24f));
                Box(body,"DarkWood",new Vector3(0,0.53f,0),new Vector3(0.37f,0.06f,0.25f));
                foreach(int s in new[]{-1,1}){Rod(body,"CanvasShade",new Vector3(s*0.1f,0.13f,0),new Vector3(s*0.1f,0.53f,0),0.066f);Box(body,"Boots",new Vector3(s*0.1f,0.07f,0.04f),new Vector3(0.15f,0.14f,0.27f));}
                body.Part("Skin").Puff(new Vector3(0,1.08f,0),new Vector3(0.17f,0.18f,0.15f),W,0,8,5,0.1f);
                body.Part("Hair").Puff(new Vector3(0,1.20f,-0.02f),new Vector3(0.175f,0.09f,0.15f),W,0,8,3,0.05f);
                foreach(int s in new[]{-1,1})body.Part("Metal").Puff(new Vector3(s*0.065f,1.1f,0.142f),Vector3.one*0.018f,W,0,5,3,0);
                body.Part("Skin").Puff(new Vector3(0,1.07f,0.161f),Vector3.one*0.033f,W,0,5,3,0);
                Box(body,shirt,new Vector3(0,1.28f,0),new Vector3(0.34f,0.07f,0.29f));
                Box(body,"Copper",new Vector3(0.08f,0.82f,0.132f),new Vector3(0.04f,0.06f,0.012f));
                var actor=root.AddComponent<WorkyardActor>();actor.role=role;
                actor.body=Local(a,body,root.transform,"ActorBody_"+role,Vector3.zero).transform;
                foreach(int side in new[]{-1,1})
                {
                    var pivot=Child(actor.body,"Arm_"+side,new Vector3(side*0.235f,0.89f,0));
                    var arm=new TWWorkyardModel();Rod(arm,shirt,Vector3.zero,new Vector3(0,-0.27f,0),0.055f);
                    arm.Part("Skin").Puff(new Vector3(0,-0.30f,0),Vector3.one*0.055f,W,0,6,4,0);
                    Local(a,arm,pivot.transform,"ActorArm_"+role+"_"+side,Vector3.zero);
                    if(side<0)actor.leftArm=pivot.transform;else actor.rightArm=pivot.transform;
                }
                var tool=new TWWorkyardModel();
                if(role==WorkerRole.Lumberjack){Rod(tool,"Wood",Vector3.zero,new Vector3(0,0.43f,0),0.023f);Box(tool,"Metal",new Vector3(0.09f,0.37f,0),new Vector3(0.23f,0.13f,0.055f));}
                else if(role==WorkerRole.Sawyer)Box(tool,"Wood",Vector3.zero,new Vector3(0.1f,0.1f,0.2f));
                else{Box(tool,"Banner",Vector3.zero,new Vector3(0.23f,0.04f,0.19f));Box(tool,"Cup",Vector3.up*0.027f,new Vector3(0.18f,0.02f,0.15f));}
                actor.tool=Local(a,tool,actor.rightArm,"ActorTool_"+role,new Vector3(0,-0.28f,0.12f)).transform;
                string path=a.folder+"/Actor_"+role+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<WorkyardActor>();
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
