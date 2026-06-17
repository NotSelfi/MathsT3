using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class IncrementalDelaunay3D
{
    public struct Edge
    {
        public int a, b;

        public Edge(int a, int b)
        {
            this.a = Mathf.Min(a, b);
            this.b = Mathf.Max(a, b);
        }

        public override int GetHashCode()
        {
            unchecked { return a * 73856093 ^ b * 19349663; }
        }

        public override bool Equals(object o)
        {
            return o is Edge e && a == e.a && b == e.b;
        }
    }

    public struct Face
    {
        public int a, b, c;

        public Face(int a, int b, int c)
        {
            int[] v = { a, b, c };
            System.Array.Sort(v);
            this.a = v[0];
            this.b = v[1];
            this.c = v[2];
        }

        public override int GetHashCode()
        {
            unchecked { return a * 73856093 ^ b * 19349663 ^ c * 83492791; }
        }

        public override bool Equals(object o)
        {
            return o is Face f && a == f.a && b == f.b && c == f.c;
        }

        public bool Has(int i)
        {
            return a == i || b == i || c == i;
        }
    }

    public struct Tetra
    {
        public int a, b, c, d;
        public Vector3 circumcenter;
        public float radius2;

        public Tetra(int a, int b, int c, int d, List<Vector3> pts)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;

            V3DGeometry.Circumsphere(
                pts[a], pts[b], pts[c], pts[d],
                out circumcenter,
                out radius2
            );
        }

        public bool HasVertex(int i)
        {
            return a == i || b == i || c == i || d == i;
        }

        public bool SharesFace(Tetra other)
        {
            int n = 0;

            if (other.HasVertex(a)) n++;
            if (other.HasVertex(b)) n++;
            if (other.HasVertex(c)) n++;
            if (other.HasVertex(d)) n++;

            return n == 3;
        }

        public Face[] Faces()
        {
            return new Face[]
            {
                new Face(a, b, c),
                new Face(a, b, d),
                new Face(a, c, d),
                new Face(b, c, d)
            };
        }
    }

    public struct VoronoiEdge
    {
        public Vector3 from, to;

        public VoronoiEdge(Vector3 f, Vector3 t)
        {
            from = f;
            to = t;
        }
    }

    public static List<Tetra> Build(List<Vector3> input, float eps = 1e-5f)
    {
        List<Vector3> pts = new List<Vector3>(input);
        List<Tetra> tets = new List<Tetra>();

        if (pts.Count < 4)
            return tets;

        AddSuperTetrahedron(pts, out int s0, out int s1, out int s2, out int s3);
        tets.Add(new Tetra(s0, s1, s2, s3, pts));

        for (int pi = 0; pi < input.Count; pi++)
        {
            List<int> bad = new List<int>();

            for (int i = 0; i < tets.Count; i++)
            {
                float dist2 = (pts[pi] - tets[i].circumcenter).sqrMagnitude;

                if (dist2 <= tets[i].radius2 + eps)
                    bad.Add(i);
            }

            Dictionary<Face, int> faceCount = new Dictionary<Face, int>();

            foreach (int id in bad)
            {
                foreach (Face f in tets[id].Faces())
                {
                    if (!faceCount.ContainsKey(f))
                        faceCount[f] = 0;

                    faceCount[f]++;
                }
            }

            bad.Sort();

            for (int i = bad.Count - 1; i >= 0; i--)
                tets.RemoveAt(bad[i]);

            foreach (var kv in faceCount)
            {
                if (kv.Value != 1)
                    continue;

                Face f = kv.Key;

                if (Mathf.Abs(V3DGeometry.SignedTetraVolume6(pts[f.a], pts[f.b], pts[f.c], pts[pi])) < eps)
                    continue;

                tets.Add(new Tetra(f.a, f.b, f.c, pi, pts));
            }
        }

        tets.RemoveAll(t =>
            t.HasVertex(s0) ||
            t.HasVertex(s1) ||
            t.HasVertex(s2) ||
            t.HasVertex(s3)
        );

        return tets;
    }

    static void AddSuperTetrahedron(List<Vector3> pts, out int a, out int b, out int c, out int d)
    {
        Bounds bounds = new Bounds(pts[0], Vector3.zero);

        foreach (Vector3 p in pts)
            bounds.Encapsulate(p);

        Vector3 center = bounds.center;
        float r = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * 20f + 10f;

        a = pts.Count;
        pts.Add(center + new Vector3(r, r, r));

        b = pts.Count;
        pts.Add(center + new Vector3(-r, -r, r));

        c = pts.Count;
        pts.Add(center + new Vector3(-r, r, -r));

        d = pts.Count;
        pts.Add(center + new Vector3(r, -r, -r));
    }

    public static HashSet<Edge> Edges(List<Tetra> tets)
    {
        HashSet<Edge> e = new HashSet<Edge>();

        foreach (Tetra t in tets)
        {
            e.Add(new Edge(t.a, t.b));
            e.Add(new Edge(t.a, t.c));
            e.Add(new Edge(t.a, t.d));
            e.Add(new Edge(t.b, t.c));
            e.Add(new Edge(t.b, t.d));
            e.Add(new Edge(t.c, t.d));
        }

        return e;
    }

    public static Dictionary<Face, List<int>> FaceAdjacency(List<Tetra> tets)
    {
        Dictionary<Face, List<int>> map = new Dictionary<Face, List<int>>();

        for (int i = 0; i < tets.Count; i++)
        {
            foreach (Face f in tets[i].Faces())
            {
                if (!map.ContainsKey(f))
                    map[f] = new List<int>();

                map[f].Add(i);
            }
        }

        return map;
    }

    public static List<VoronoiEdge> VoronoiEdges(List<Tetra> tets)
    {
        List<VoronoiEdge> res = new List<VoronoiEdge>();

        foreach (var kv in FaceAdjacency(tets))
        {
            if (kv.Value.Count == 2)
            {
                res.Add(new VoronoiEdge(
                    tets[kv.Value[0]].circumcenter,
                    tets[kv.Value[1]].circumcenter
                ));
            }
        }

        return res;
    }

    public static bool AdjacentPairViolatesDelaunay(Tetra t1, Tetra t2, List<Vector3> pts)
    {
        int p1 = OppositeVertex(t1, t2);
        int p2 = OppositeVertex(t2, t1);

        if (p1 < 0 || p2 < 0)
            return false;

        return V3DGeometry.ViolatesDelaunay(pts[t1.a], pts[t1.b], pts[t1.c], pts[t1.d], pts[p2]) ||
               V3DGeometry.ViolatesDelaunay(pts[t2.a], pts[t2.b], pts[t2.c], pts[t2.d], pts[p1]);
    }

    static int OppositeVertex(Tetra t, Tetra other)
    {
        if (!other.HasVertex(t.a)) return t.a;
        if (!other.HasVertex(t.b)) return t.b;
        if (!other.HasVertex(t.c)) return t.c;
        if (!other.HasVertex(t.d)) return t.d;

        return -1;
    }
}