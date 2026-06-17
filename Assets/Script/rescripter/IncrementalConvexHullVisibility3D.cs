using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class IncrementalConvexHullVisibility3D
{
    public struct HullFace
    {
        public int a;
        public int b;
        public int c;
        public Vector3 normal;

        public HullFace(int a, int b, int c, List<Vector3> pts)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            normal = Vector3.Cross(pts[b] - pts[a], pts[c] - pts[a]).normalized;
        }

        public bool VisibleFrom(Vector3 p, List<Vector3> pts)
        {
            return Vector3.Dot(normal, p - pts[a]) > V3DGeometry.EPS;
        }
    }

    struct EdgeKey
    {
        public int a;
        public int b;

        public EdgeKey(int a, int b)
        {
            this.a = Mathf.Min(a, b);
            this.b = Mathf.Max(a, b);
        }

        public override int GetHashCode()
        {
            return a * 73856093 ^ b * 19349663;
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey e && e.a == a && e.b == b;
        }
    }

    public static List<HullFace> Build(List<Vector3> pts)
    {
        List<HullFace> faces = new List<HullFace>();

        if (pts.Count < 4)
            return faces;

        int i0 = 0;
        int i1 = 1;
        int i2 = 2;
        int i3 = -1;

        for (int i = 3; i < pts.Count; i++)
        {
            if (Mathf.Abs(V3DGeometry.SignedTetraVolume6(pts[i0], pts[i1], pts[i2], pts[i])) > V3DGeometry.EPS)
            {
                i3 = i;
                break;
            }
        }

        if (i3 == -1)
            return faces;

        AddInitialTetraFaces(faces, pts, i0, i1, i2, i3);

        for (int p = 0; p < pts.Count; p++)
        {
            if (p == i0 || p == i1 || p == i2 || p == i3)
                continue;

            InsertPoint(pts, faces, p);
        }

        return faces;
    }

    static void AddInitialTetraFaces(
        List<HullFace> faces,
        List<Vector3> pts,
        int a,
        int b,
        int c,
        int d)
    {
        AddOrientedFace(faces, pts, a, b, c, d);
        AddOrientedFace(faces, pts, a, d, b, c);
        AddOrientedFace(faces, pts, b, d, c, a);
        AddOrientedFace(faces, pts, c, d, a, b);
    }

    static void AddOrientedFace(
        List<HullFace> faces,
        List<Vector3> pts,
        int a,
        int b,
        int c,
        int opposite)
    {
        HullFace f = new HullFace(a, b, c, pts);

        if (f.VisibleFrom(pts[opposite], pts))
            f = new HullFace(a, c, b, pts);

        faces.Add(f);
    }

    static void InsertPoint(List<Vector3> pts, List<HullFace> faces, int p)
    {
        List<int> visible = new List<int>();

        for (int i = 0; i < faces.Count; i++)
        {
            if (faces[i].VisibleFrom(pts[p], pts))
                visible.Add(i);
        }

        if (visible.Count == 0)
            return;

        Dictionary<EdgeKey, int> edgeCount = new Dictionary<EdgeKey, int>();

        foreach (int id in visible)
        {
            HullFace f = faces[id];
            AddEdge(edgeCount, f.a, f.b);
            AddEdge(edgeCount, f.b, f.c);
            AddEdge(edgeCount, f.c, f.a);
        }

        visible.Sort();

        for (int i = visible.Count - 1; i >= 0; i--)
            faces.RemoveAt(visible[i]);

        foreach (var kv in edgeCount)
        {
            if (kv.Value != 1)
                continue;

            int a = kv.Key.a;
            int b = kv.Key.b;

            HullFace nf = new HullFace(a, b, p, pts);

            Vector3 center = Vector3.zero;

            foreach (Vector3 v in pts)
                center += v;

            center /= pts.Count;

            if (nf.VisibleFrom(center, pts))
                nf = new HullFace(b, a, p, pts);

            faces.Add(nf);
        }
    }

    static void AddEdge(Dictionary<EdgeKey, int> map, int a, int b)
    {
        EdgeKey e = new EdgeKey(a, b);

        if (!map.ContainsKey(e))
            map[e] = 0;

        map[e]++;
    }
}