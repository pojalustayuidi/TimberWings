using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    // Editor-only dressing. Ground details have no colliders or runtime loops.
    internal static class TWGroveDetails
    {
        static readonly Color W = Color.white;
        static void Box(TWGroveModel m, string c, Vector3 p, Vector3 size, float yaw = 0)
        { m.Part(c).Box(p, size, Quaternion.Euler(0, yaw, 0), W); }
        static void Rod(TWGroveModel m, string c, Vector3 a, Vector3 b, float r)
        { m.Part(c).Cyl(a, b, r, r * 0.95f, W, 6); }

        public static void Build(TWGroveContext x, TWGroveAssets assets)
        {
            var handles = x.root.GetComponentsInChildren<GroveArtHandle>(true).ToDictionary(h => h.key);
            Addon("Gate", Entrance);
            Addon("Shed", Workshop);
            Addon("Cart", Cargo);
            Addon("AxeStump", StumpTools);
            Addon("Bench", Lunch);
            Addon("OldMark", Secret);
            Addon("DuckStool", DuckBadge);
            if (handles.TryGetValue("Cart", out var cart) && cart.GetComponent<GroveChopBlock>() == null)
            {
                var note = Undo.AddComponent<GroveChopBlock>(cart.gameObject);
                note.title = "Накладная"; note.prompt = "прочитать накладную"; note.radius = 1.9f;
                note.lines = new[] { "«Брёвна — в лагерь. Утку — на должность заведующего. Подпись: Мира».",
                    "Внизу приписано: «Одна минута потеряна между третьим и четвёртым деревом».", "Вес груза зачёркнут. Вместо него написано: «Достаточно, чтобы не везти самому»." };
            }
            var anchors = new List<Vector2>
            {
                new Vector2(-3.4f,-7.4f), new Vector2(3.4f,-7.0f), new Vector2(-4.0f,-0.8f), new Vector2(3.8f,1.4f),
                new Vector2(-3.5f,5.3f), new Vector2(3.6f,6.5f), new Vector2(-6.8f,6.2f), new Vector2(6.8f,3.8f),
                new Vector2(-7.5f,-3.3f), new Vector2(7.4f,-4.1f), new Vector2(-3.0f,9.5f), new Vector2(4.1f,10.2f)
            };
            if (handles.TryGetValue("Gate", out var gate))
            {
                anchors.Add(x.Local(gate.transform.TransformPoint(new Vector3(-3.5f,0,0.6f))));
                anchors.Add(x.Local(gate.transform.TransformPoint(new Vector3(3.5f,0,0.6f))));
            }
            var used = new List<Vector2>(); int count = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                string key = "DetailPatch_" + i;
                if (TWGroveProps.Reuse(x, key, out GameObject kept)) { used.Add(x.Local(kept.transform.position)); count++; continue; }
                if (!PatchPoint(x, anchors[i], used, out Vector3 point)) continue;
                float yaw = i * 137.5f;
                TWGroveProps.Finish(x, assets, Patch(i), key, "21_Подлесок_" + (i+1).ToString("00"), point,
                    x.rotation * Quaternion.Euler(0,yaw,0), new Vector2(0.62f,0.62f), 0.6f, false,
                    "Отдельная группа папоротников, грибов и камешков. Без коллизии; можно двигать и выключать.");
                used.Add(x.Local(point)); count++;
            }
            Debug.Log("GROVE ART: групп подлеска " + count + ". Детали закреплены за своими объектами; дополнительные коллайдеры и Light не создавались.");

            void Addon(string key, Func<TWGroveModel> factory)
            {
                if (!handles.TryGetValue(key, out var handle)) return;
                string name = "GroveDetails_" + key;
                if (handle.transform.Find(name) != null) return;
                var go = factory().Finish(assets, handle.transform, name, handle.transform.position, handle.transform.rotation);
                Undo.RegisterCreatedObjectUndo(go, "Add grove prop details");
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            }
        }

        static TWGroveModel Entrance()
        {
            var m = new TWGroveModel();
            foreach (int s in new[] {-1,1})
            {
                float x = s * 2.5f;
                Rod(m,"Wood",new Vector3(x,2.95f,-0.18f),new Vector3(x+s*0.62f,2.95f,-0.18f),0.045f);
                float cx=x+s*0.36f;
                m.Part("Banner").Fan(new[] { new Vector3(cx-0.22f,2.9f,-0.20f), new Vector3(cx+0.22f,2.9f,-0.20f),
                    new Vector3(cx+0.22f,2.12f,-0.20f),new Vector3(cx,2.25f,-0.20f),new Vector3(cx-0.22f,2.12f,-0.20f)},W,Vector3.back,true);
                Rod(m,"Copper",new Vector3(cx-0.055f,2.39f,-0.23f),new Vector3(cx+0.025f,2.75f,-0.23f),0.024f);
                m.Part("Copper").Puff(new Vector3(cx+0.06f,2.67f,-0.24f),new Vector3(0.12f,0.065f,0.027f),W,0,5,3,0);
                Box(m,"Rope",new Vector3(x,2.96f,-0.18f),new Vector3(0.12f,0.1f,0.11f));
            }
            return m;
        }

        static TWGroveModel Workshop()
        {
            var m=new TWGroveModel();
            // A framed handsaw on the back rail, a rolled plan, a vise and a crate.
            Rod(m,"Wood",new Vector3(0.0f,1.5f,0.94f),new Vector3(0.0f,2.03f,0.94f),0.035f);
            Rod(m,"Wood",new Vector3(0.9f,1.5f,0.94f),new Vector3(0.9f,2.03f,0.94f),0.035f);
            Rod(m,"Wood",new Vector3(-0.04f,1.91f,0.94f),new Vector3(0.94f,1.91f,0.94f),0.04f);
            Box(m,"Metal",new Vector3(0.45f,1.54f,0.92f),new Vector3(0.83f,0.075f,0.035f));
            for(int i=0;i<11;i++) m.Part("Metal").Tri(new Vector3(0.06f+i*0.075f,1.51f,0.9f),new Vector3(0.10f+i*0.075f,1.46f,0.9f),new Vector3(0.13f+i*0.075f,1.51f,0.9f),W,Vector3.back);
            Box(m,"Teal",new Vector3(0.45f,1.025f,0.12f),new Vector3(0.5f,0.025f,0.42f),-9);
            Rod(m,"Cup",new Vector3(0.24f,1.08f,-0.12f),new Vector3(0.71f,1.08f,-0.04f),0.053f);
            Box(m,"Metal",new Vector3(1.0f,1.12f,-0.26f),new Vector3(0.22f,0.17f,0.2f));
            Rod(m,"Metal",new Vector3(0.85f,1.13f,-0.26f),new Vector3(1.19f,1.13f,-0.26f),0.025f);
            for(int row=0;row<3;row++) foreach(int side in new[]{-1,1})
                Box(m,"Wood",new Vector3(0.75f+side*0.31f,0.15f+row*0.15f,0.65f),new Vector3(0.06f,0.11f,0.55f));
            Box(m,"DarkWood",new Vector3(0.75f,0.06f,0.65f),new Vector3(0.67f,0.1f,0.6f));
            for(int i=0;i<3;i++) Herb(m,new Vector3(-1.05f+i*0.12f,2.20f,0.95f));
            return m;
        }

        static void Herb(TWGroveModel m,Vector3 p)
        {
            Rod(m,"Rope",p,p+Vector3.down*0.15f,0.014f);
            for(int i=0;i<3;i++) m.Part("Leaf").Teardrop(p+new Vector3((i-1)*0.05f,-0.33f,0),0.23f,0.04f,new Vector3(0.03f,0,0.03f),W,W,5,3);
        }

        static TWGroveModel Cargo()
        {
            var m=new TWGroveModel();
            Box(m,"Banner",new Vector3(-0.864f,1.02f,-0.3f),new Vector3(0.025f,0.39f,0.7f));
            Box(m,"Copper",new Vector3(-0.882f,1.03f,-0.3f),new Vector3(0.015f,0.23f,0.05f));
            Box(m,"Cup",new Vector3(0.87f,1.16f,0.20f),new Vector3(0.025f,0.29f,0.34f),4);
            for(int i=0;i<3;i++) Box(m,"Wood",new Vector3(0.889f,1.22f-i*0.06f,0.2f),new Vector3(0.01f,0.012f,0.22f));
            return m;
        }

        static TWGroveModel StumpTools()
        {
            var m=new TWGroveModel();
            m.Part("Chips").Puff(new Vector3(0.64f,0.038f,0.18f),new Vector3(0.38f,0.045f,0.29f),W,23,8,3,0);
            for(int i=0;i<12;i++)
            {
                float a=i*2.4f; Box(m,"EndGrain",new Vector3(Mathf.Cos(a)*0.69f+0.25f,0.06f,Mathf.Sin(a)*0.55f),new Vector3(0.16f,0.035f,0.055f),i*43);
            }
            Rod(m,"Metal",new Vector3(0.29f,0.74f,-0.1f),new Vector3(0.4f,0.74f,0.15f),0.024f);
            return m;
        }

        static TWGroveModel Lunch()
        {
            var m=new TWGroveModel();
            Box(m,"Teal",new Vector3(0.52f,0.7f,0),new Vector3(0.42f,0.035f,0.4f),12);
            m.Part("Cup").Puff(new Vector3(0.52f,0.78f,0),new Vector3(0.18f,0.08f,0.14f),W,0,7,4,0);
            for(int i=0;i<3;i++) Rod(m,"Wood",new Vector3(0.43f+i*0.06f,0.85f,-0.04f),new Vector3(0.46f+i*0.06f,0.85f,0.04f),0.009f);
            return m;
        }

        static TWGroveModel Secret()
        {
            var m=new TWGroveModel();
            for(int i=0;i<5;i++) {float a=i*0.5f+0.4f;Mushroom(m,new Vector3(Mathf.Cos(a)*0.66f,0,Mathf.Sin(a)*0.45f),0.8f+i*0.08f);}
            return m;
        }
        static TWGroveModel DuckBadge()
        {
            var m=new TWGroveModel();Box(m,"Banner",new Vector3(0.0f,0.50f,-0.4f),new Vector3(0.26f,0.18f,0.025f));
            m.Part("Copper").Star(new Vector3(0,0.51f,-0.42f),Vector3.right,Vector3.up,0.065f,0.03f,5,W); return m;
        }

        static bool PatchPoint(TWGroveContext x,Vector2 desired,List<Vector2> used,out Vector3 point)
        {
            for(int ring=0;ring<4;ring++) for(int i=0;i<(ring==0?1:12);i++)
            {
                float a=i*Mathf.PI/6f; Vector2 p=desired+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*ring*0.55f;
                Vector3 wp=x.World(p);
                if(p.magnitude>16.5f || x.field.EdgeDistance(wp.x,wp.z)<1.8f || x.field.IsWater(wp.x,wp.z)) continue;
                if(x.trees.Any(t=>Vector2.Distance(t.point,p)<1.5f) || x.buildings.Any(b=>Vector2.Distance(b.point,p)<b.radius+0.7f)) continue;
                if(x.occupied.Any(f=>TWGrovePaths.PointDistance(p,f)<1.0f) || used.Any(q=>Vector2.Distance(p,q)<1.45f))continue;
                bool path=false;
                for(int j=0;j<x.trails.Count;j++)if(TWGrovePaths.Distance(p,x.trails[j])<(j==0?1.9f:1.4f)){path=true;break;}
                if(path || !x.TryGround(new Vector2(wp.x,wp.z),out float height))continue;
                bool supported=true;
                foreach(var d in new[]{Vector2.left,Vector2.right,Vector2.up,Vector2.down})
                {
                    Vector3 q=x.World(p+d*0.6f);
                    if(!x.TryGround(new Vector2(q.x,q.z),out float y)||Mathf.Abs(y-height)>0.18f){supported=false;break;}
                }
                if(!supported)continue;
                point=new Vector3(wp.x,height-0.018f,wp.z);return true;
            }
            point=default;return false;
        }

        static TWGroveModel Patch(int seed)
        {
            var m=new TWGroveModel();
            Fern(m,new Vector3(-0.22f,0,0.05f),0.85f);
            Fern(m,new Vector3(0.24f,0,0.24f),0.65f);
            m.Part("Stone").Puff(new Vector3(0.29f,0.055f,-0.23f),new Vector3(0.21f,0.11f,0.16f),W,seed*29,6,3,0.1f);
            if(seed%2==0)
            {
                Mushroom(m,new Vector3(-0.37f,0,-0.25f),0.85f);Mushroom(m,new Vector3(-0.13f,0,-0.37f),0.6f);
            }
            else
                for(int i=0;i<3;i++)
                {
                    Vector3 p=new Vector3(0.1f+i*0.14f,0.23f,-0.34f);Rod(m,"Leaf",p-Vector3.up*0.23f,p,0.012f);
                    for(int j=0;j<4;j++)m.Part(seed%3==0?"PurpleFlower":"CreamFlower").Puff(p+new Vector3(Mathf.Cos(j*1.57f)*0.05f,0,Mathf.Sin(j*1.57f)*0.05f),Vector3.one*0.045f,W,0,5,3,0);
                }
            return m;
        }
        static void Mushroom(TWGroveModel m,Vector3 p,float s)
        {
            Rod(m,"Cup",p,p+Vector3.up*0.19f*s,0.026f*s);
            m.Part("Mushroom").Puff(p+Vector3.up*0.2f*s,new Vector3(0.15f,0.08f,0.15f)*s,W,0,7,4,0);
            foreach(int side in new[]{-1,1})m.Part("Cup").Puff(p+new Vector3(side*0.05f,0.277f,0.012f)*s,Vector3.one*0.018f*s,W,0,5,3,0);
        }
        static void Fern(TWGroveModel m,Vector3 p,float s)
        {
            for(int f=0;f<5;f++)
            {
                float a=f*Mathf.PI*0.4f;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),r=Vector3.Cross(Vector3.up,d);
                Vector3 end=p+d*0.46f*s+Vector3.up*0.28f*s;Rod(m,"Leaf",p,end,0.012f*s);
                for(int k=1;k<=4;k++)foreach(int side in new[]{-1,1})
                {
                    float t=k/5f;Vector3 c=Vector3.Lerp(p,end,t);float w=(1-t)*0.18f*s;
                    m.Part(f%2==0?"Leaf":"LeafLight").Fan(new[]{c,c+r*side*w+d*0.07f*s+Vector3.up*0.025f,c+r*side*w*0.6f+d*0.15f*s,c+d*0.12f*s},W,Vector3.up,true);
                }
            }
        }
    }
}
