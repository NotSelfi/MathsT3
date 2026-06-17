using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class SurfaceTriangleVoronoi : MonoBehaviour
{
    public bool DrawVoronoi = true;

    struct Edge
    {
        public int a;
        public int b;

        public Edge(int x, int y)
        {
            a = Mathf.Min(x, y);
            b = Mathf.Max(x, y);
        }
    }

    class EdgeComparer : IEqualityComparer<Edge>
    {
        public bool Equals(Edge x, Edge y)
        {
            return x.a == y.a &&
                   x.b == y.b;
        }

        public int GetHashCode(Edge e)
        {
            return e.a * 73856093 ^
                   e.b * 19349663;
        }
    }

    List<Vector3> circumcenters =
        new List<Vector3>();

    List<(int,int)> adjacency =
        new List<(int,int)>();

    void Start()
    {
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        circumcenters.Clear();
        adjacency.Clear();

        Mesh mesh =
            GetComponent<MeshFilter>().sharedMesh;

        Vector3[] verts =
            mesh.vertices;

        int[] tris =
            mesh.triangles;

        Dictionary<Edge,List<int>> edgeMap =
            new Dictionary<Edge,List<int>>(
                new EdgeComparer()
            );

        int triCount =
            tris.Length / 3;

        for (int t = 0; t < triCount; t++)
        {
            int i0 = tris[t * 3];
            int i1 = tris[t * 3 + 1];
            int i2 = tris[t * 3 + 2];

            Vector3 c =
                TriangleCircumcenter(
                    verts[i0],
                    verts[i1],
                    verts[i2]
                );

            circumcenters.Add(
                transform.TransformPoint(c)
            );

            AddEdge(edgeMap,new Edge(i0,i1),t);
            AddEdge(edgeMap,new Edge(i1,i2),t);
            AddEdge(edgeMap,new Edge(i2,i0),t);
        }

        foreach (var kv in edgeMap)
        {
            if (kv.Value.Count == 2)
            {
                adjacency.Add(
                    (kv.Value[0],kv.Value[1])
                );
            }
        }

        Debug.Log(
            "Voronoi surface : "
            + adjacency.Count
            + " arêtes"
        );
    }

    void AddEdge(
        Dictionary<Edge,List<int>> map,
        Edge e,
        int tri)
    {
        if (!map.ContainsKey(e))
            map[e] = new List<int>();

        map[e].Add(tri);
    }

    Vector3 TriangleCircumcenter(
        Vector3 A,
        Vector3 B,
        Vector3 C)
    {
        Vector3 a = B - A;
        Vector3 b = C - A;

        Vector3 cross =
            Vector3.Cross(a,b);

        float denom =
            2f * cross.sqrMagnitude;

        Vector3 term1 =
            Vector3.Cross(cross,a)
            * b.sqrMagnitude;

        Vector3 term2 =
            Vector3.Cross(b,cross)
            * a.sqrMagnitude;

        return A + (term1 + term2) / denom;
    }

    void OnDrawGizmos()
    {
        if (!DrawVoronoi)
            return;

        Gizmos.color = Color.red;

        foreach (var a in adjacency)
        {
            Gizmos.DrawLine(
                circumcenters[a.Item1],
                circumcenters[a.Item2]
            );
        }
    }
}
