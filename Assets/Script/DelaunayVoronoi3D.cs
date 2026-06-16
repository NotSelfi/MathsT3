using System.Collections.Generic;
using UnityEngine;

public static class DelaunayVoronoi3D
{
    public struct Tetrahedron
    {
        public int a, b, c, d;
        public Vector3 circumcenter;
        public float radiusSquared;

        public Tetrahedron(int a, int b, int c, int d, Vector3 center, float radiusSquared)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.circumcenter = center;
            this.radiusSquared = radiusSquared;
        }

        public bool HasVertex(int index)
        {
            return a == index || b == index || c == index || d == index;
        }

        public bool SharesFace(Tetrahedron other)
        {
            int shared = 0;
            if (other.HasVertex(a)) shared++;
            if (other.HasVertex(b)) shared++;
            if (other.HasVertex(c)) shared++;
            if (other.HasVertex(d)) shared++;
            return shared == 3;
        }
    }

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

        public override bool Equals(object obj)
        {
            if (!(obj is Edge)) return false;
            Edge e = (Edge)obj;
            return a == e.a && b == e.b;
        }
    }

    public struct VoronoiEdge
    {
        public Vector3 from;
        public Vector3 to;

        public VoronoiEdge(Vector3 from, Vector3 to)
        {
            this.from = from;
            this.to = to;
        }
    }

    public static List<Tetrahedron> ComputeDelaunay(List<Vector3> points, float epsilon = 0.0001f)
    {
        List<Tetrahedron> tetrahedra = new List<Tetrahedron>();
        int n = points.Count;

        if (n < 4) return tetrahedra;

        for (int i = 0; i < n - 3; i++)
        for (int j = i + 1; j < n - 2; j++)
        for (int k = j + 1; k < n - 1; k++)
        for (int l = k + 1; l < n; l++)
        {
            float volume = Geometry3D.SignedVolume(points[i], points[j], points[k], points[l]);
            if (Mathf.Abs(volume) < epsilon) continue;

            if (!TryCircumsphere(points[i], points[j], points[k], points[l], out Vector3 center, out float radiusSquared))
                continue;

            bool valid = true;
            for (int p = 0; p < n; p++)
            {
                if (p == i || p == j || p == k || p == l) continue;

                float distSquared = (points[p] - center).sqrMagnitude;
                if (distSquared < radiusSquared - epsilon)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                tetrahedra.Add(new Tetrahedron(i, j, k, l, center, radiusSquared));
            }
        }

        return tetrahedra;
    }

    public static HashSet<Edge> GetDelaunayEdges(List<Tetrahedron> tetrahedra)
    {
        HashSet<Edge> edges = new HashSet<Edge>();

        foreach (Tetrahedron t in tetrahedra)
        {
            edges.Add(new Edge(t.a, t.b));
            edges.Add(new Edge(t.a, t.c));
            edges.Add(new Edge(t.a, t.d));
            edges.Add(new Edge(t.b, t.c));
            edges.Add(new Edge(t.b, t.d));
            edges.Add(new Edge(t.c, t.d));
        }

        return edges;
    }

    public static List<VoronoiEdge> ComputeVoronoiEdges(List<Tetrahedron> tetrahedra)
    {
        List<VoronoiEdge> edges = new List<VoronoiEdge>();

        for (int i = 0; i < tetrahedra.Count; i++)
        for (int j = i + 1; j < tetrahedra.Count; j++)
        {
            if (tetrahedra[i].SharesFace(tetrahedra[j]))
            {
                edges.Add(new VoronoiEdge(tetrahedra[i].circumcenter, tetrahedra[j].circumcenter));
            }
        }

        return edges;
    }

    public static bool TryCircumsphere(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 center, out float radiusSquared)
    {
        Vector3 ba = b - a;
        Vector3 ca = c - a;
        Vector3 da = d - a;

        float[,] m = new float[3, 4]
        {
            { 2f * ba.x, 2f * ba.y, 2f * ba.z, b.sqrMagnitude - a.sqrMagnitude },
            { 2f * ca.x, 2f * ca.y, 2f * ca.z, c.sqrMagnitude - a.sqrMagnitude },
            { 2f * da.x, 2f * da.y, 2f * da.z, d.sqrMagnitude - a.sqrMagnitude }
        };

        for (int col = 0; col < 3; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < 3; row++)
            {
                if (Mathf.Abs(m[row, col]) > Mathf.Abs(m[pivot, col])) pivot = row;
            }

            if (Mathf.Abs(m[pivot, col]) < 0.000001f)
            {
                center = Vector3.zero;
                radiusSquared = 0f;
                return false;
            }

            if (pivot != col)
            {
                for (int x = col; x < 4; x++)
                {
                    float tmp = m[col, x];
                    m[col, x] = m[pivot, x];
                    m[pivot, x] = tmp;
                }
            }

            float div = m[col, col];
            for (int x = col; x < 4; x++) m[col, x] /= div;

            for (int row = 0; row < 3; row++)
            {
                if (row == col) continue;
                float factor = m[row, col];
                for (int x = col; x < 4; x++) m[row, x] -= factor * m[col, x];
            }
        }

        center = new Vector3(m[0, 3], m[1, 3], m[2, 3]);
        radiusSquared = (center - a).sqrMagnitude;
        return true;
    }
}
