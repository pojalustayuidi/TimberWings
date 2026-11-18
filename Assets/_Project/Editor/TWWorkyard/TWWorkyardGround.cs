using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    internal static class TWWorkyardGround
    {
        internal const float SupportRadius = 13f;
        const float FullHeightRadius = 8f;
        const float FloorInset = 0.010f;
        const float FloorRadiusX = 6.2f;
        const float FloorRadiusZ = 5.5f;
        const float SeamOverlap = 0.045f;

        // Real terrain triangles, clipped and feathered; no floating ground quad.
        public static void Build(TWWorkyardContext x,TWWorkyardAssets assets,Transform parent,string key)
        {
            var mesh=new Mesh{name=key};var v=new List<Vector3>();var colours=new List<Color>();var t=new List<int>();
            ForEachGroundTriangle(x, FloorRadiusX, FloorRadiusZ, (a,b,c) => Add(a,b,c,0));
            if(v.Count>65000)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);mesh.SetColors(colours);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(IslandBlockout.MaterialPath)){name=key};
            AssetDatabase.CreateAsset(material,assets.folder+"/"+key+".mat");
            assets.Object(parent,key,assets.Save(mesh,key),new[]{material},false);

            void Add(Vector3 a,Vector3 b,Vector3 c,int depth)
            {
                Vector3 normal=Vector3.Cross(b-a,c-a);if(normal.sqrMagnitude<1e-12f)return;
                if(depth<6 && Mathf.Max((b-a).sqrMagnitude,Mathf.Max((c-b).sqrMagnitude,(a-c).sqrMagnitude))>0.6f*0.6f)
                {
                    Vector3 ab=(a+b)*0.5f,bc=(b+c)*0.5f,ca=(c+a)*0.5f;
                    Add(a,ab,ca,depth+1);Add(ab,b,bc,depth+1);Add(ca,bc,c,depth+1);Add(ab,bc,ca,depth+1);return;
                }
                if(normal.y<0){Vector3 tmp=b;b=c;c=tmp;}
                int start=v.Count;
                foreach(var p in new[]{a,b,c})
                {
                    v.Add(parent.InverseTransformPoint(p+Vector3.up*0.014f));
                    Vector3 local=Quaternion.Inverse(x.rotation)*(p-x.origin);
                    float r=RelativeRadius(local);
                    float wear=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.43f,0.96f,r));
                    Color colour=Color.Lerp(new Color(0.75f,0.65f,0.46f),new Color(0.85f,0.76f,0.57f),Mathf.PerlinNoise(p.x*0.6f,p.z*0.6f)).linear;
                    colour.a=wear*0.65f;colours.Add(colour);
                }
                t.Add(start);t.Add(start+1);t.Add(start+2);
            }
        }

        internal static List<TWWorkyardContext.Face> ReadFloor(MeshFilter floor)
        {
            var vertices = floor.sharedMesh.vertices;
            var indices = floor.sharedMesh.triangles;
            var matrix = floor.transform.localToWorldMatrix;
            var surface = new List<TWWorkyardContext.Face>(indices.Length / 3);
            for (int i = 0; i < indices.Length; i += 3)
                surface.Add(new TWWorkyardContext.Face
                {
                    a = matrix.MultiplyPoint3x4(vertices[indices[i]]),
                    b = matrix.MultiplyPoint3x4(vertices[indices[i+1]]),
                    c = matrix.MultiplyPoint3x4(vertices[indices[i+2]])
                });
            return surface;
        }

        internal static Mesh BuildSupport(TWWorkyardContext yard, Transform space, TerrainCollider terrain,
            IReadOnlyList<TWWorkyardContext.Face> floor)
        {
            var waters = yard.tent.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WaterVolume>())
                .Where(water => water.isActiveAndEnabled).ToArray();
            var points = new List<Vector3>();
            var indices = new List<int>();
            var colors = new List<Color>();
            var samples = new Dictionary<Vector3, (Vector3 position, bool usable, bool raised)>();
            float ceiling = terrain.bounds.max.y + 1f;
            float castDistance = ceiling - terrain.bounds.min.y + 1f;
            Quaternion toLocal = Quaternion.Inverse(yard.rotation);
            ForEachGroundTriangle(yard, SupportRadius, SupportRadius, (a,b,c) => Add(a,b,c,0));

            var mesh = new Mesh { name = "Workyard_Support" };
            if (points.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(points);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            void Add(Vector3 a, Vector3 b, Vector3 c, int depth)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (Mathf.Abs(normal.y) < 0.000001f) return;
                const float maxEdge = 0.72f;
                float edge = Mathf.Max((a-b).sqrMagnitude, Mathf.Max((b-c).sqrMagnitude, (c-a).sqrMagnitude));
                if (depth < 6 && edge > maxEdge * maxEdge)
                {
                    Vector3 ab = (a+b)*0.5f, bc = (b+c)*0.5f, ca = (c+a)*0.5f;
                    Add(a,ab,ca,depth+1);
                    Add(ab,b,bc,depth+1);
                    Add(ca,bc,c,depth+1);
                    Add(ab,bc,ca,depth+1);
                    return;
                }

                var pa = Project(a);
                var pb = Project(b);
                var pc = Project(c);
                if (!pa.usable || !pb.usable || !pc.usable || !(pa.raised || pb.raised || pc.raised)) return;
                // Stay on the dry side even on triangles whose vertices straddle a narrow water volume.
                if (UnderWater((a+b+c)/3f)) return;
                int start = points.Count;
                points.Add(space.InverseTransformPoint(pa.position));
                points.Add(space.InverseTransformPoint(pb.position));
                points.Add(space.InverseTransformPoint(pc.position));
                colors.Add(new Color(1f, 1f, 1f, 0f));
                colors.Add(new Color(1f, 1f, 1f, 0f));
                colors.Add(new Color(1f, 1f, 1f, 0f));
                indices.Add(start);
                indices.Add(start + (normal.y > 0 ? 1 : 2));
                indices.Add(start + (normal.y > 0 ? 2 : 1));
            }

            (Vector3 position, bool usable, bool raised) Project(Vector3 surface)
            {
                if (samples.TryGetValue(surface, out var sample)) return sample;
                var ray = new Ray(new Vector3(surface.x, ceiling, surface.z), Vector3.down);
                bool hasGround = terrain.Raycast(ray, out var hit, castDistance);
                float dx = surface.x - yard.origin.x;
                float dz = surface.z - yard.origin.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                bool usable = !UnderWater(surface) && (hasGround || distance <= FullHeightRadius);
                Vector3 point = surface;
                Vector3 local = toLocal * (surface - yard.origin);
                float radius = RelativeRadius(local);
                // Sample a point just inside the actual baked floor near its edge.
                float scale = radius > 0.985f ? 0.985f / radius : 1f;
                Vector3 floorPoint = yard.World(new Vector3(local.x * scale, 0f, local.z * scale));
                float rise = 0f;
                if (TWWorkyardContext.TryHeight(floor, new Vector2(floorPoint.x, floorPoint.z), out float floorHeight))
                    rise = Mathf.Max(0f, floorHeight - FloorInset - surface.y);
                float approach = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 1.77f, radius));
                point.y = surface.y + rise * approach;
                if (hasGround)
                {
                    float exit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FullHeightRadius, SupportRadius, distance));
                    point.y = Mathf.Lerp(point.y, hit.point.y - SeamOverlap, exit);
                }
                sample = (point, usable, !hasGround || point.y > hit.point.y - SeamOverlap);
                samples.Add(surface, sample);
                return sample;
            }

            bool UnderWater(Vector3 surface)
            {
                foreach (var water in waters)
                    if (water.Sample(surface, out var hit) && hit.submersion >= -0.03f)
                        return true;
                return false;
            }
        }

        static float RelativeRadius(Vector3 local) => new Vector2(local.x / FloorRadiusX, local.z / FloorRadiusZ).magnitude;

        static void ForEachGroundTriangle(TWWorkyardContext yard, float radiusX, float radiusZ,
            Action<Vector3, Vector3, Vector3> add)
        {
            const int sides = 48;
            var edge = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 p = yard.World(new Vector3(Mathf.Cos(angle)*radiusX, 0f, Mathf.Sin(angle)*radiusZ));
                edge[i] = new Vector2(p.x, p.z);
            }
            foreach (var face in yard.faces)
            {
                var polygon = new List<Vector3> { face.a, face.b, face.c };
                for (int i = 0; i < sides && polygon.Count > 0; i++)
                    polygon = Clip(polygon, edge[i], edge[(i + 1) % sides]);
                for (int i = 1; i + 1 < polygon.Count; i++)
                    add(polygon[0], polygon[i], polygon[i+1]);
            }
        }

        static List<Vector3> Clip(List<Vector3> p,Vector2 a,Vector2 b)
        {
            var result=new List<Vector3>();Vector2 edge=b-a;
            float Side(Vector3 v){return edge.x*(v.z-a.y)-edge.y*(v.x-a.x);}
            for(int i=0;i<p.Count;i++)
            {
                Vector3 u=p[i],v=p[(i+1)%p.Count];float x=Side(u),y=Side(v);bool inside=x>=0,other=y>=0;
                if(inside)result.Add(u);if(inside!=other)result.Add(Vector3.Lerp(u,v,x/(x-y)));
            }
            return result;
        }
    }
}
