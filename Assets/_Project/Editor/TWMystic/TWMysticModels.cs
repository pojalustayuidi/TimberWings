using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_6000_0_OR_NEWER
using Surface=UnityEngine.PhysicsMaterial;
#else
using Surface=UnityEngine.PhysicMaterial;
#endif

namespace IslandV2
{
    internal static class TWMysticModels
    {
        static readonly Color W=Color.white;
        internal static void Box(TWMysticModel m,string c,Vector3 p,Vector3 s,float yaw=0){m.Part(c).Box(p,s,Quaternion.Euler(0,yaw,0),W);}
        internal static void Rod(TWMysticModel m,string c,Vector3 a,Vector3 b,float r){m.Part(c).Cyl(a,b,r,r*0.87f,W,8);}
        static void Rock(TWMysticModel m,Vector3 p,Vector3 s,int seed=0){m.Part("Stone").Puff(p,s,W,seed,7,3,0.13f);}
        public static Surface Slip(string folder)
        {
            var p=new Surface("Mystic_Slide"){staticFriction=0,dynamicFriction=0,bounciness=0};
#if UNITY_6000_0_OR_NEWER
            p.frictionCombine=PhysicsMaterialCombine.Minimum;
#else
            p.frictionCombine=PhysicMaterialCombine.Minimum;
#endif
            AssetDatabase.CreateAsset(p,folder+"/Slide.physicMaterial");return p;
        }
        static void Capsule(GameObject go,Vector3 p,float radius,float height,int direction,Surface surface)
        {
            if(go.transform.Find("Mystic_Collision")!=null)return;
            var child=new GameObject("Mystic_Collision");child.transform.SetParent(go.transform,false);
            var c=child.AddComponent<CapsuleCollider>();c.center=p;c.radius=radius;c.height=height;c.direction=direction;c.sharedMaterial=surface;
            var n=child.AddComponent<NavMeshObstacle>();n.carving=true;n.shape=NavMeshObstacleShape.Box;n.center=p;
            n.size=direction==0?new Vector3(height,radius*2,radius*2):new Vector3(radius*2,height,radius*2);
        }
        public static void Build(TWMysticContext x,TWMysticAssets a,Surface surface)
        {
            for(int i=0;i<6;i++)
            {
                float angle=42+i*55.2f;var m=Bench(false);
                for(int mark=0;mark<=i;mark++)
                {
                    float xx=(mark-i*0.5f)*0.075f;
                    Rod(m,"DarkWood",new Vector3(xx-0.012f,0.93f,-0.241f),new Vector3(xx+0.012f,1.06f,-0.241f),0.007f);
                }
                Vector2 point=x.Site("Bench_"+i,TWMysticContext.Ring(angle,7.2f));float facing=Mathf.Atan2(point.x,point.y)*Mathf.Rad2Deg+180;
                var bench=x.Prop(a,m,"Bench_"+i,"01_Скамья_круга_"+(i+1),point,facing);
                Capsule(bench,new Vector3(0,0.48f,0),0.34f,3.2f,0,surface);
                if(i==1 || i==4)Ghost(a,bench.transform,i);
            }
            for(int i=0;i<3;i++)
            {
                float angle=new[]{77f,188f,282f}[i];var stone=new TWMysticModel();
                Rock(stone,Vector3.up*0.75f,new Vector3(0.46f,0.95f,0.36f),i*17);
                Rock(stone,new Vector3(0.18f,0.10f,0.1f),new Vector3(0.64f,0.15f,0.46f),i*11);
                Vector2 point=x.Site("Witness_"+i,TWMysticContext.Ring(angle,10.1f));
                var pillar=x.Prop(a,stone,"Witness_"+i,"02_Камень_свидетель_"+(i+1),point,Mathf.Atan2(point.x,point.y)*Mathf.Rad2Deg+180);
                Capsule(pillar,new Vector3(0,0.65f,0),0.29f,1.6f,1,surface);
                Rune(a,pillar.transform,"Rune_Stone_"+i,new Vector3(0,0.90f,0.34f));
            }
            Gate(x,a,surface);
            var circle=new TWMysticModel();int circlePieces=0;
            for(int i=0;i<19;i++)
            {
                float angle=26+i*17.8f;Vector2 p=TWMysticContext.Ring(angle,5.1f);
                if(!x.TryGround(p,out var world))continue;
                circlePieces++;
                Rock(circle,new Vector3(p.x,world.y-x.groupOrigin.y+0.035f,p.y),new Vector3(0.34f,0.08f,0.27f),i*9);
            }
            if(circlePieces>0 || x.keep.ContainsKey("Circle_Stones"))x.Prop(a,circle,"Circle_Stones","03_Прерванный_круг_камней",Vector2.zero);
            var ties=new TWMysticModel();int tiePieces=0;
            for(int i=0;i<6;i++)
            {
                float angle=30+i*60;Vector2 p=TWMysticContext.Ring(angle,4.5f);
                if(!x.TryGround(p,out var world))continue;float y=world.y-x.groupOrigin.y;
                tiePieces++;
                Rod(ties,"DarkWood",new Vector3(p.x,y,p.y),new Vector3(p.x,y+0.55f,p.y),0.045f);
                Box(ties,"Ribbon",new Vector3(p.x,y+0.46f,p.y),new Vector3(0.27f,0.08f,0.06f),angle);
            }
            if(tiePieces>0 || x.keep.ContainsKey("Ties"))x.Prop(a,ties,"Ties","04_Шесть_оберегов",Vector2.zero);
            Memory(x,a,"Ribbon_Memory","05_Оборванный_узел",new Vector2(-2.7f,12.6f),
                "осмотреть оборванную ленту",new[]{"Лента перерезана. На внутренней стороне — шесть коротких зарубок.","Лео","Кто-то отмечал тех, кто приходил сюда. Последняя отметка не закончена."});
            Memory(x,a,"Gunnar_Memory","06_След_Гуннара",new Vector2(4.2f,4.1f),
                "осмотреть старый нож",new[]{"На рукояти вырезана буква «Г». Как на вещах дяди.","Лео","Гуннар был здесь. Почему он ни разу не рассказывал об этом месте?"});
            Memory(x,a,"Bench_Memory","07_Незанятое_место",TWMysticContext.Ring(42+4*55.2f,8.5f),
                "осмотреть старую дощечку",new[]{"Скамьи отмечены зарубками. У этой лежит дощечка — имя на ней давно стёрлось.","Лео","«Если услышишь зов после заката — не отвечай из-за деревьев»."});
            Details(x,a);
        }
        internal static TWMysticModel Bench(bool broken)
        {
            var m=new TWMysticModel();
            for(int j=0;j<3;j++)
                Box(m,"Wood",new Vector3(0,0.51f,-0.20f+j*0.2f),new Vector3(3.0f,0.13f,0.18f));
            Box(m,"Wood",new Vector3(0,0.99f,-0.30f),new Vector3(2.94f,0.28f,0.11f));
            Box(m,"DarkWood",new Vector3(0,0.35f,0),new Vector3(2.65f,0.14f,0.13f));
            foreach(int s in new[]{-1,1})
            {
                Rod(m,"DarkWood",new Vector3(s*1.18f,0,-0.12f),new Vector3(s*1.18f,1.15f,-0.12f),0.075f);
                Rod(m,"DarkWood",new Vector3(s*1.18f,0,0.28f),new Vector3(s*1.18f,0.5f,0.28f),0.075f);
                Rock(m,new Vector3(s*1.18f,0.06f,0.08f),new Vector3(0.24f,0.09f,0.33f),s*17);
                Box(m,"Rope",new Vector3(s*1.18f,0.78f,-0.12f),new Vector3(0.18f,0.10f,0.18f));
            }
            return m;
        }
        static void Gate(TWMysticContext x,TWMysticAssets a,Surface surface)
        {
            var m=new TWMysticModel();
            foreach(int s in new[]{-1,1})
            {
                Rod(m,"DarkWood",new Vector3(s*2.55f,-0.12f,0),new Vector3(s*2.62f,3.45f,0),0.19f);
                Rock(m,new Vector3(s*2.55f,0.10f,0),new Vector3(0.43f,0.16f,0.42f),s*10);
                Rod(m,"Wood",new Vector3(s*2.6f,2.65f,0),new Vector3(s*1.9f,3.6f,0),0.11f);
                Box(m,"Rope",new Vector3(s*2.6f,3.06f,0),new Vector3(0.43f,0.16f,0.4f));
            }
            for(int i=0;i<9;i++)
            {
                float xx=-2.68f+i*0.67f;float yy=3.5f+0.37f*(1-Mathf.Abs(xx)/2.7f);
                Box(m,"Wood",new Vector3(xx,yy,0),new Vector3(0.77f,0.34f,0.42f),0);
                m.Part("Moss").Puff(new Vector3(xx,yy+0.18f,0),new Vector3(0.53f,0.13f,0.32f),W,i*13,7,3,0.05f);
            }
            Rod(m,"Rope",new Vector3(0,3.8f,0),new Vector3(0,2.92f,0),0.022f);
            Box(m,"Copper",new Vector3(0,2.84f,0),new Vector3(0.3f,0.26f,0.24f));
            Rod(m,"DarkWood",new Vector3(0,2.74f,0),new Vector3(0,2.6f,0),0.025f);
            Box(m,"Ribbon",new Vector3(0.38f,3.25f,0),new Vector3(0.13f,0.7f,0.035f));
            Vector2 p=x.Site("Entrance",x.gate!=null?x.Local(x.gate.position):new Vector2(0,18f));
            float yaw=Mathf.Atan2(p.x,p.y)*Mathf.Rad2Deg;
            var go=x.Prop(a,m,"Entrance","00_Порог_тихого_леса",p,yaw);
            foreach(int s in new[]{-1,1})
            {
                string key="GateSupport_"+s;if(go.transform.Find(key)!=null)continue;
                var c=new GameObject(key);c.transform.SetParent(go.transform,false);
                Capsule(c,new Vector3(s*2.55f,1.6f,0),0.17f,3.5f,1,surface);
            }
        }
        static void Rune(TWMysticAssets a,Transform parent,string key,Vector3 p)
        {
            if(parent.Find(key)!=null)return;
            var m=new TWMysticModel();
            Rod(m,"Rune",new Vector3(-0.13f,-0.12f,0),Vector3.zero,0.012f);
            Rod(m,"Rune",Vector3.zero,new Vector3(0.13f,0.12f,0),0.012f);
            Rod(m,"Rune",new Vector3(-0.10f,0.11f,0),new Vector3(0.10f,-0.11f,0),0.012f);
            var go=m.Finish(a,parent,key,parent.TransformPoint(p),parent.rotation);go.GetComponent<Renderer>().sharedMaterial=a.veil;
        }
        static void Ghost(TWMysticAssets a,Transform bench,int index)
        {
            string key="Apparition_"+index;if(bench.Find(key)!=null)return;
            var m=new TWMysticModel();
            m.Part("Ghost").Puff(new Vector3(0,1.20f,0.05f),new Vector3(0.17f,0.21f,0.15f),W,0,7,4,0.1f);
            m.Part("Ghost").Puff(new Vector3(0,0.86f,0),new Vector3(0.27f,0.32f,0.18f),W,0,7,4,0.08f);
            foreach(int s in new[]{-1,1})
            {
                Rod(m,"Ghost",new Vector3(s*0.20f,1,0),new Vector3(s*0.16f,0.67f,0.28f),0.055f);
                Rod(m,"Ghost",new Vector3(s*0.11f,0.53f,0.09f),new Vector3(s*0.11f,0.1f,0.28f),0.07f);
            }
            var go=m.Finish(a,bench,key,bench.position,bench.rotation);go.GetComponent<Renderer>().sharedMaterial=a.veil;
            var block=new MaterialPropertyBlock();block.SetFloat("_Alpha",0);go.GetComponent<Renderer>().SetPropertyBlock(block);
            go.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            go.GetComponent<Renderer>().enabled=false;
        }
        static void Memory(TWMysticContext x,TWMysticAssets a,string key,string name,Vector2 p,string prompt,string[] lines)
        {
            var m=new TWMysticModel();Rock(m,Vector3.up*0.1f,new Vector3(0.65f,0.18f,0.45f),7);
            Box(m,"DarkWood",new Vector3(0,0.30f,0),new Vector3(0.63f,0.10f,0.33f),-12);
            Rod(m,"Copper",new Vector3(-0.25f,0.37f,0),new Vector3(0.15f,0.37f,0),0.025f);
            Box(m,key=="Gunnar_Memory"?"Metal":"Ribbon",new Vector3(0.21f,0.38f,0),new Vector3(0.35f,0.035f,0.10f),-8);
            var go=x.Prop(a,m,key,name,p);
            if(go.GetComponent<DialogueSequence>()!=null)return;
            var d=go.AddComponent<DialogueSequence>();d.trigger=DialogueSequence.Trigger.OnInteract;d.prompt=prompt;d.radius=2.1f;d.freezePlayer=false;d.once=true;
            d.saveId="TW_Mystic_"+key;d.promptOffset=new Vector3(0,1.3f,0);
            d.lines=new[]{new DialogueSequence.Line{speaker="Лео",text=lines[0]},new DialogueSequence.Line{speaker=lines[1],text=lines[2]}};
        }
        static void Details(TWMysticContext x,TWMysticAssets a)
        {
            for(int sector=0;sector<6;sector++)
            {
                var m=new TWMysticModel();var rng=new System.Random(5309+sector*113);int pieces=0;
                for(int i=0;i<13;i++)
                {
                    float angle=32+sector*55+i*2.2f;float r=9.5f+(float)rng.NextDouble()*6.0f;Vector2 p=TWMysticContext.Ring(angle,r);
                    if(!x.TryGround(p,out Vector3 world))continue;
                    pieces++;
                    float ground=world.y-x.groupOrigin.y;
                    if(i%3==0)Rock(m,new Vector3(p.x,ground+0.08f,p.y),new Vector3(0.28f,0.16f,0.23f),i*7);
                    else
                    {
                        float h=0.16f+(float)rng.NextDouble()*0.2f;
                        Rod(m,"Moss",new Vector3(p.x,ground,p.y),new Vector3(p.x,ground+h,p.y),0.018f);
                        m.Part("Ribbon").Puff(new Vector3(p.x,ground+h,p.y),new Vector3(0.13f,0.06f,0.12f),W,i*11,6,3,0.06f);
                    }
                }
                if(pieces>0 || x.keep.ContainsKey("Understory_"+sector))x.Prop(a,m,"Understory_"+sector,"08_Тихий_подлесок_"+(sector+1),Vector2.zero);
            }
        }
        public static GameObject Tree(TWMysticAssets a,Transform parent,int index,TreeSize size)
        {
            var m=new TWMysticModel();float k=GameBalance.TreeScaleMul[(int)size];
            Rod(m,"Bark",Vector3.zero,Vector3.up*(2.7f*k),0.19f*k);
            foreach(int s in new[]{-1,1})Rod(m,"Bark",new Vector3(0,1.6f*k,0),new Vector3(s*0.75f*k,2.7f*k,0.1f),0.09f*k);
            m.Part(index%2==0?"Leaf":"LeafLight").Puff(new Vector3(0,3.1f*k,0),new Vector3(1.40f,1.0f,1.2f)*k,W,index*17,8,4,0.14f);
            m.Part("LeafLight").Puff(new Vector3(0.5f*k,3.8f*k,0.15f*k),new Vector3(0.95f,0.6f,0.9f)*k,W,index*31,7,4,0.12f);
            for(int j=0;j<3;j++)Box(m,"Ribbon",new Vector3(0.02f,0.7f*k+j*0.11f,0.19f*k),new Vector3(0.18f*k,0.028f,0.025f),j%2==0?-18:12);
            var go=m.Finish(a,parent,"MysticTree_Model_"+index,parent.position,parent.rotation);return go;
        }
    }
}
