// TIMBER WINGS / ISL-004 / Release Composition v0.2.
// Characterful micro-scenography guided by real IslandLayout anchors and existing paths.
// Geometry is EDITOR-BAKED, cosmetic, deterministic and never owns physics/gameplay.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal static class TWIslandReleaseComposition
    {
        internal sealed class Result
        {
            internal Mesh camp, trails, craft;
            internal int triangles;
            internal string report;
        }
        sealed class Draw
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Color> colors = new List<Color>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int> indices = new List<int>();
            internal int Triangles { get { return indices.Count / 3; } }

            internal void Tri(Vector3 a, Vector3 b, Vector3 c, Color tint)
            {
                int k = vertices.Count;
                Vector3 n = Vector3.Cross(b-a,c-a).normalized;
                if (n.sqrMagnitude < 0.1f) n = Vector3.up;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                colors.Add(tint); colors.Add(tint); colors.Add(tint);
                indices.Add(k); indices.Add(k+1); indices.Add(k+2);
            }
            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color tint)
            { Tri(a,b,c,tint); Tri(a,c,d,tint); }
            internal Mesh Bake(string name)
            {
                if (vertices.Count == 0) return null;
                var m = new Mesh { name=name };
                if (vertices.Count >= 65535) m.indexFormat=IndexFormat.UInt32;
                m.SetVertices(vertices);
                m.SetNormals(normals);
                m.SetColors(colors);
                m.SetTriangles(indices,0);
                m.RecalculateBounds();
                return m;
            }
        }
        static Vector2 Polar(float r,float a) { return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r); }
        static Vector3 P(Vector2 xz,float h) { return new Vector3(xz.x,h,xz.y); }
        static float Pick(System.Random r,float a,float b) { return Mathf.Lerp(a,b,(float)r.NextDouble()); }

        static bool OutsideReservedAreas(IslandField field,Vector2 pos,float extra,string allowAnchor)
        {
            var anchors=field.L.anchors;
            if (anchors == null) return true;
            foreach (var anchor in anchors)
            {
                if (anchor == null || anchor.name == allowAnchor) continue;
                if (Vector2.Distance(pos,field.AnchorWorld(anchor)) < Mathf.Max(4.7f,anchor.flatRadius+extra))
                    return false;
            }
            return true;
        }
        static bool AwayFromColliders(List<TWIslandReleaseGeometryV2.Avoid> obstacles,Vector2 pos,float add)
        {
            if (obstacles == null) return true;
            for (int i=0;i<obstacles.Count;i++)
            {
                float rad=obstacles[i].radius+add;
                if ((pos-obstacles[i].p).sqrMagnitude < rad*rad) return false;
            }
            return true;
        }
        static bool Dry(IslandField f,Vector2 p,float pathDist,List<TWIslandReleaseGeometryV2.Avoid> obstacles,string allowedAnchor,float obstacleMargin)
        {
            if (!f.Inside(p.x,p.y) || f.EdgeDistance(p.x,p.y)<5.1f) return false;
            if (f.WaterDistance(p.x,p.y)<5f || f.FordWeight(p.x,p.y)>0.01f) return false;
            if (f.PathDistance(p.x,p.y)<pathDist || f.Slope(p.x,p.y)>19f) return false;
            return OutsideReservedAreas(f,p,2f,allowedAnchor) && AwayFromColliders(obstacles,p,obstacleMargin);
        }

        // Thin, ground-hugging tessellated pavers: should not imply climbable obstacles.
        static void Paver(Draw draw,IslandField f,Vector2 center,float radius,float yaw,Color stone,System.Random rng)
        {
            const int segments=7;
            float y=f.Height(center.x,center.y)+0.038f;
            Vector3[] top=new Vector3[segments];
            Vector3[] bottom=new Vector3[segments];
            for (int i=0;i<segments;i++)
            {
                float a=yaw+i*Mathf.PI*2f/segments;
                float r=radius*Pick(rng,0.88f,1.06f);
                var p=center+Polar(r,a);
                float ground=f.Height(p.x,p.y);
                top[i]=P(p,Mathf.Max(y,ground+0.05f));
                bottom[i]=P(p,Mathf.Min(y-0.055f,ground-0.02f));
            }
            for (int i=0;i<segments;i++)
            {
                int j=(i+1)%segments;
                draw.Tri(P(center,y+0.008f),top[j],top[i],Color.Lerp(stone,Color.white,i%2==0?0.07f:0f));
                draw.Quad(bottom[i],bottom[j],top[j],top[i],stone*0.83f);
            }
        }
        static void CylinderLog(Draw draw,Vector3 center,Vector3 forward,float length,float radius,Color bark,Color cut)
        {
            const int n=6;
            Vector3 side=Vector3.Cross(Vector3.up,forward).normalized;
            if (side.sqrMagnitude<0.1f) side=Vector3.right;
            Vector3[] a=new Vector3[n],b=new Vector3[n];
            for (int i=0;i<n;i++)
            {
                float angle=i*Mathf.PI*2f/n;
                Vector3 r=side*Mathf.Cos(angle)*radius + Vector3.up*Mathf.Sin(angle)*radius;
                a[i]=center-forward*(length*0.5f)+r;
                b[i]=center+forward*(length*0.5f)+r;
            }
            for (int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                draw.Quad(a[i],a[j],b[j],b[i],bark);
                draw.Tri(center-forward*length*0.5f,a[j],a[i],cut);
                draw.Tri(center+forward*length*0.5f,b[i],b[j],cut);
            }
        }
        static Vector2 SegmentPoint(Vector2[] path,float meters,out Vector2 dir)
        {
            dir=Vector2.up;
            if (path==null || path.Length==0) return Vector2.zero;
            for (int i=1;i<path.Length;i++)
            {
                Vector2 v=path[i]-path[i-1]; float d=v.magnitude;
                if (d<0.001f) continue;
                if (meters<=d || i==path.Length-1)
                {
                    dir=v/d; return path[i-1]+v*Mathf.Clamp01(meters/d);
                }
                meters-=d;
            }
            return path[path.Length-1];
        }
        static float PolylineLength(Vector2[] line)
        {
            float d=0;
            if (line==null) return d;
            for (int i=1;i<line.Length;i++) d+=Vector2.Distance(line[i-1],line[i]);
            return d;
        }

        internal static Result Make(IslandField f,List<TWIslandReleaseGeometryV2.Avoid> obstacles)
        {
            var rnd=new System.Random(unchecked(f.L.seed*73939+707));
            var camp=new Draw(); var trails=new Draw(); var craft=new Draw();
            int campPavers=0,trailMarks=0,craftGroups=0;
            var l=f.L;

            // A. Central campfire. Ground-only circular accent, leaving traversable route gaps.
            var campAnchor=l.FindAnchor("CAMPFIRE_Point");
            if (campAnchor!=null)
            {
                Vector2 origin=f.AnchorWorld(campAnchor);
                for(int i=0;i<44;i++)
                {
                    float angle=i*Mathf.PI*2f/44f;
                    float radius=Mathf.Max(l.plazaRadius+1.1f,6.1f)+Pick(rnd,-0.24f,0.34f);
                    Vector2 p=origin+Polar(radius,angle);
                    if (!Dry(f,p,1.2f,obstacles,"CAMPFIRE_Point",0.65f)) continue;
                    Paver(camp,f,p,Pick(rnd,0.32f,0.68f),Pick(rnd,0,Mathf.PI*2f),
                          Color.Lerp(l.plazaStone,l.plazaRim,Pick(rnd,0.2f,0.75f)),rnd);
                    campPavers++;
                }
            }

            // B. Trail identity. Tiny low ground markers on the shoulder, not inside paths or story approaches.
            if (l.paths!=null)
            {
                for(int i=0;i<l.paths.Count;i++)
                {
                    var line=f.PathPolyline(i);
                    if (line==null || line.Length<2) continue;
                    float len=PolylineLength(line);
                    float halfWidth=Mathf.Max(1.2f,l.paths[i].width*0.5f);
                    for(float m=11f;m<len-10f;m+=12.5f)
                    {
                        Vector2 dir;
                        Vector2 axis=SegmentPoint(line,m,out dir);
                        var side=new Vector2(-dir.y,dir.x);
                        int sign=(((int)(m/12.5f)+i)&1)==0?1:-1;
                        Vector2 center=axis+side*(sign*(halfWidth+1.58f));
                        if(!Dry(f,center,0.65f,obstacles,null,0.82f)) continue;
                        if(f.PathDistance(center.x,center.y)>4.7f) continue;
                        Color tint=Color.Lerp(l.pathRim,l.plazaStone,Pick(rnd,0.21f,0.62f));
                        int n=2+rnd.Next(2);
                        for(int k=0;k<n;k++)
                        {
                            Vector2 q=center+dir*Pick(rnd,-0.9f,0.9f)+side*(sign*Pick(rnd,0f,0.4f));
                            if (!Dry(f,q,0.52f,obstacles,null,0.65f)) continue;
                            Paver(trails,f,q,Pick(rnd,0.16f,0.30f),Pick(rnd,0,6.283f),tint,rnd);
                            trailMarks++;
                        }
                    }
                }
            }

            // C. Production zones. Read existing PAD markers; no new fake shops, harvestables or interactables.
            string[] pads={"PAD_Hut","PAD_Market","PAD_Tent","PAD_QuestBoard"};
            for (int ai=0;ai<pads.Length;ai++)
            {
                var anchor=l.FindAnchor(pads[ai]);
                if(anchor==null) continue;
                var anchorPos=f.AnchorWorld(anchor);
                bool placed=false;
                for(int i=0;i<18 && !placed;i++)
                {
                    float angle=2f*Mathf.PI*(i+0.5f*ai)/18f;
                    float radius=Mathf.Max(8.2f,anchor.flatRadius+3.0f);
                    Vector2 center=anchorPos+Polar(radius,angle);
                    if (!Dry(f,center,2.1f,obstacles,anchor.name,1.6f)) continue;
                    Vector2 tangent=Polar(1f,angle+Mathf.PI*0.5f);
                    bool footprint=true;
                    for(int j=-1;j<=1;j++)
                        if(!Dry(f,center+tangent*j*0.47f,1.9f,obstacles,anchor.name,1.15f)) footprint=false;
                    if(!footprint) continue;
                    // Low cut timber laid on soil; extremely small and cosmetic (no interaction implication).
                    for(int j=-1;j<=1;j++)
                    {
                        Vector2 pp=center+tangent*(j*0.34f);
                        float h=f.Height(pp.x,pp.y);
                        CylinderLog(craft,P(pp,h+0.13f),new Vector3(tangent.x,0,tangent.y),
                            Pick(rnd,0.75f,1.05f),0.11f,
                            new Color(0.52f,0.38f,0.28f,1f),new Color(0.88f,0.70f,0.49f,1f));
                    }
                    Paver(craft,f,center+Polar(0.85f,angle),0.30f,angle,Color.Lerp(l.plazaStone,l.rockTop,0.45f),rnd);
                    craftGroups++;
                    placed=true;
                }
            }
            var result=new Result {
                camp=camp.Bake("TW_Release_Campfire_GroundMosaic"),
                trails=trails.Bake("TW_Release_TrailShoulder_Rhythm"),
                craft=craft.Bake("TW_Release_Worksite_Details"),
                triangles=camp.Triangles+trails.Triangles+craft.Triangles,
                report="camp pavers="+campPavers+"; trail pebbles="+trailMarks+"; worksite motifs="+craftGroups+
                       "; composition triangles="+(camp.Triangles+trails.Triangles+craft.Triangles)
            };
            return result;
        }
    }
}
