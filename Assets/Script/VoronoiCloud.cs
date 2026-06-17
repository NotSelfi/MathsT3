using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class VoronoiCloud : PointCloudEditor
{
    [Header("Affichage Delaunay / Voronoi")]
    public bool drawDelaunay = true;
    public bool drawVoronoi = true;
    public bool drawCircumcenters = false;
    public Color delaunayColor = Color.yellow;
    public Color voronoiColor = Color.magenta;
    public Color circumcenterColor = Color.green;
    public float epsilon = 0.0001f;
    public float maxVoronoiEdgeLength = 20f;

    [Header("Mesh d'enveloppe")]
    public MeshFilter targetMeshFilter;
    public bool flipWinding = false;

    public Mesh HullMesh { get; private set; }

    List<DelaunayVoronoi3D.Tetrahedron> _tetra = new List<DelaunayVoronoi3D.Tetrahedron>();
    HashSet<DelaunayVoronoi3D.Edge> _delaunayEdges = new HashSet<DelaunayVoronoi3D.Edge>();
    List<DelaunayVoronoi3D.VoronoiEdge> _voronoiEdges = new List<DelaunayVoronoi3D.VoronoiEdge>();

    struct Face { public int i, j, k, opp; }

    protected override void OnPointsChanged(List<Vector3> points)
    {
        _tetra = DelaunayVoronoi3D.ComputeDelaunay(points, epsilon);
        _delaunayEdges = DelaunayVoronoi3D.GetDelaunayEdges(_tetra);
        _voronoiEdges = DelaunayVoronoi3D.ComputeVoronoiEdges(_tetra);

        if (_tetra.Count > 0) BuildHullMesh();
    }

    public Mesh BuildHullMesh()
    {
        var seen = new Dictionary<(int, int, int), int>();
        var first = new Dictionary<(int, int, int), Face>();

        void AddFace(int i, int j, int k, int opp)
        {
            var key = SortKey(i, j, k);
            if (seen.TryGetValue(key, out int c)) seen[key] = c + 1;
            else { seen[key] = 1; first[key] = new Face { i = i, j = j, k = k, opp = opp }; }
        }

        foreach (var t in _tetra)
        {
            AddFace(t.a, t.b, t.c, t.d);
            AddFace(t.a, t.b, t.d, t.c);
            AddFace(t.a, t.c, t.d, t.b);
            AddFace(t.b, t.c, t.d, t.a);
        }

        var tris = new List<int>();
        foreach (var kv in seen)
        {
            if (kv.Value != 1) continue;
            Face f = first[kv.Key];

            Vector3 p0 = Points[f.i], p1 = Points[f.j], p2 = Points[f.k];
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            bool versOppose = Vector3.Dot(n, Points[f.opp] - p0) > 0f;

            int A = f.i, B = f.j, C = f.k;
            if (versOppose) { int t = B; B = C; C = t; }
            if (flipWinding) { int t = B; B = C; C = t; }
            tris.Add(A); tris.Add(B); tris.Add(C);
        }

        if (HullMesh == null) HullMesh = new Mesh { name = "VoronoiHull" };
        HullMesh.Clear();
        HullMesh.indexFormat = Points.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        HullMesh.SetVertices(new List<Vector3>(Points));
        HullMesh.SetTriangles(tris, 0);
        HullMesh.RecalculateNormals();
        HullMesh.RecalculateBounds();

        var mf = targetMeshFilter != null ? targetMeshFilter : GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = HullMesh;

        return HullMesh;
    }

    static (int, int, int) SortKey(int a, int b, int c)
    {
        if (a > b) { int t = a; a = b; b = t; }
        if (b > c) { int t = b; b = c; c = t; }
        if (a > b) { int t = a; a = b; b = t; }
        return (a, b, c);
    }

    void OnDrawGizmos()
    {
        if (drawGizmos)
        {
            Gizmos.color = pointColor;
            for (int i = 0; i < Points.Count; i++)
                Gizmos.DrawSphere(Points[i], pointRadius);
        }

        if (drawDelaunay)
        {
            Gizmos.color = delaunayColor;
            foreach (var e in _delaunayEdges)
                if (e.a < Points.Count && e.b < Points.Count)
                    Gizmos.DrawLine(Points[e.a], Points[e.b]);
        }

        if (drawVoronoi)
        {
            Gizmos.color = voronoiColor;
            foreach (var v in _voronoiEdges)
            {
                if (maxVoronoiEdgeLength > 0f &&
                    (v.to - v.from).sqrMagnitude > maxVoronoiEdgeLength * maxVoronoiEdgeLength)
                    continue;
                Gizmos.DrawLine(v.from, v.to);
            }
        }

        if (drawCircumcenters)
        {
            Gizmos.color = circumcenterColor;
            for (int i = 0; i < _tetra.Count; i++)
                Gizmos.DrawWireSphere(_tetra[i].circumcenter, pointRadius * 0.7f);
        }
    }
}
