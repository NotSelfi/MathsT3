using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SurfaceVoronoi : MonoBehaviour
{
    [Header("Paramètres")]
    public int SeedCount = 30;

    [Header("Affichage")]
    public bool ShowBorders = true;
    public bool ShowSeeds = true;
    public float GizmoSphereRadius = 0.02f;

    Vector3[] _vertices;
    int[] _triangles;
    int _triCount;
    int[] _canonical;

    int[] _vertexToSeed;
    int[] _triToSeed;
    List<int> _seedCanonicals;
    Color[] _seedColors;

    List<DelaunayVoronoi3D.VoronoiEdge> _borderEdges;

    Material _lineMat;
    MeshFilter _mf;
    MeshRenderer _mr;

    void Start()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();

        Mesh src = _mf.sharedMesh;
        if (src == null) { Debug.LogError("Pas de mesh !"); return; }

        Mesh m = Instantiate(src);
        _mf.mesh = m;
        _vertices = m.vertices;
        _triangles = m.triangles;
        _triCount = _triangles.Length / 3;
        _canonical = WeldVertices(_vertices, 1e-5f);

        _lineMat = new Material(Shader.Find("Hidden/Internal-Colored"));
        _lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        _lineMat.SetInt("_ZWrite", 1);

        Debug.Log($"Mesh : {_vertices.Length} vertices, {_triCount} triangles");
        Generate();
    }

    [ContextMenu("Regenerate")]
    public void Generate()
    {
        if (_vertices == null) return;
        PlaceSeeds();
        DijkstraAssign();
        AssignTriangles();
        BuildBorders();
        ColorMesh();
    }

   static int[] WeldVertices(Vector3[] verts, float eps)
    {
        int[] canon = new int[verts.Length];
        var map = new Dictionary<(int, int, int), int>();
        float inv = 1f / eps;
        for (int i = 0; i < verts.Length; i++)
        {
            var key = (Mathf.RoundToInt(verts[i].x * inv),
                       Mathf.RoundToInt(verts[i].y * inv),
                       Mathf.RoundToInt(verts[i].z * inv));
            if (!map.TryGetValue(key, out int master)) { master = i; map[key] = i; }
            canon[i] = master;
        }
        return canon;
    }

    void PlaceSeeds()
    {
        var uniqueSet = new HashSet<int>(_canonical);
        var uniqueList = new List<int>(uniqueSet);
        int count = Mathf.Min(SeedCount, uniqueList.Count);

        Random.InitState(42);
        for (int i = uniqueList.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (uniqueList[i], uniqueList[j]) = (uniqueList[j], uniqueList[i]);
        }

        _seedCanonicals = uniqueList.GetRange(0, count);
        _seedColors = new Color[count];
        Random.InitState(123);
        for (int i = 0; i < count; i++)
            _seedColors[i] = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.7f, 1f);

        Debug.Log($"Seeds : {count}");
    }

    void DijkstraAssign()
    {
        var graph = new Dictionary<int, List<(int nb, float dist)>>();
        for (int t = 0; t < _triCount; t++)
        {
            int[] ci = {
                _canonical[_triangles[t * 3]],
                _canonical[_triangles[t * 3 + 1]],
                _canonical[_triangles[t * 3 + 2]]
            };
            for (int e = 0; e < 3; e++)
            {
                int a = ci[e], b = ci[(e + 1) % 3];
                float d = Vector3.Distance(_vertices[a], _vertices[b]);
                if (!graph.ContainsKey(a)) graph[a] = new List<(int, float)>();
                if (!graph.ContainsKey(b)) graph[b] = new List<(int, float)>();
                graph[a].Add((b, d));
                graph[b].Add((a, d));
            }
        }

        var dist = new Dictionary<int, float>();
        var owner = new Dictionary<int, int>();
        foreach (var key in graph.Keys) { dist[key] = float.MaxValue; owner[key] = -1; }

        var pq = new List<(float d, int v)>();
        for (int s = 0; s < _seedCanonicals.Count; s++)
        {
            int sc = _seedCanonicals[s];
            if (!dist.ContainsKey(sc)) continue;
            dist[sc] = 0f;
            owner[sc] = s;
            pq.Add((0f, sc));
        }
        pq.Sort((x, y) => x.d.CompareTo(y.d));

        while (pq.Count > 0)
        {
            var (d, u) = pq[0];
            pq.RemoveAt(0);
            if (d > dist[u]) continue;

            if (!graph.ContainsKey(u)) continue;
            foreach (var (nb, edgeLen) in graph[u])
            {
                float nd = dist[u] + edgeLen;
                if (nd < dist[nb])
                {
                    dist[nb] = nd;
                    owner[nb] = owner[u];
                    int idx = pq.BinarySearch((nd, nb),
                        Comparer<(float, int)>.Create((a, b) => a.Item1.CompareTo(b.Item1)));
                    if (idx < 0) idx = ~idx;
                    pq.Insert(idx, (nd, nb));
                }
            }
        }

        _vertexToSeed = new int[_vertices.Length];
        for (int i = 0; i < _vertices.Length; i++)
            _vertexToSeed[i] = owner.ContainsKey(_canonical[i]) ? owner[_canonical[i]] : 0;
    }

    void AssignTriangles()
    {
        _triToSeed = new int[_triCount];
        for (int t = 0; t < _triCount; t++)
        {
            int s0 = _vertexToSeed[_triangles[t * 3]];
            int s1 = _vertexToSeed[_triangles[t * 3 + 1]];
            int s2 = _vertexToSeed[_triangles[t * 3 + 2]];

            if (s0 == s1 || s0 == s2) _triToSeed[t] = s0;
            else _triToSeed[t] = s1;
        }
    }

    void BuildBorders()
    {
        _borderEdges = new List<DelaunayVoronoi3D.VoronoiEdge>();

        var edgeToTris = new Dictionary<DelaunayVoronoi3D.Edge, List<int>>();

        for (int t = 0; t < _triCount; t++)
        {
            int i0 = _canonical[_triangles[t * 3]];
            int i1 = _canonical[_triangles[t * 3 + 1]];
            int i2 = _canonical[_triangles[t * 3 + 2]];

            AddToEdgeMap(edgeToTris, new DelaunayVoronoi3D.Edge(i0, i1), t);
            AddToEdgeMap(edgeToTris, new DelaunayVoronoi3D.Edge(i1, i2), t);
            AddToEdgeMap(edgeToTris, new DelaunayVoronoi3D.Edge(i2, i0), t);
        }

        foreach (var kvp in edgeToTris)
        {
            var triList = kvp.Value;
            if (triList.Count != 2) continue;

            int t1 = triList[0], t2 = triList[1];
            if (_triToSeed[t1] != _triToSeed[t2])
            {
                Vector3 a = transform.TransformPoint(_vertices[kvp.Key.a]);
                Vector3 b = transform.TransformPoint(_vertices[kvp.Key.b]);
                _borderEdges.Add(new DelaunayVoronoi3D.VoronoiEdge(a, b));
            }
        }

        Debug.Log($"Bordures : {_borderEdges.Count} arêtes");
    }

    void AddToEdgeMap(Dictionary<DelaunayVoronoi3D.Edge, List<int>> map,
                      DelaunayVoronoi3D.Edge e, int tri)
    {
        if (!map.ContainsKey(e)) map[e] = new List<int>();
        map[e].Add(tri);
    }

    void ColorMesh()
    {
        Vector3[] verts = new Vector3[_triCount * 3];
        int[] tris = new int[_triCount * 3];
        Color[] colors = new Color[_triCount * 3];
        Vector3[] norms = _mf.mesh.normals;

        for (int t = 0; t < _triCount; t++)
        {
            Color col = _seedColors[_triToSeed[t]];
            for (int k = 0; k < 3; k++)
            {
                int dst = t * 3 + k;
                verts[dst] = _vertices[_triangles[t * 3 + k]];
                tris[dst] = dst;
                colors[dst] = col;
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        _mf.mesh = mesh;

        var mat = new Material(Shader.Find("Unlit/VertexColorShader"));
        if (mat.shader == null || !mat.shader.isSupported)
            mat = new Material(Shader.Find("Particles/Standard Unlit"));
        _mr.material = mat;

        Debug.Log($"Voronoï : {_seedCanonicals.Count} cellules, {_triCount} triangles");
    }

    void OnRenderObject()
    {
        if (!ShowBorders || _borderEdges == null || _lineMat == null) return;

        _lineMat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINES);
        GL.Color(Color.black);

        foreach (var edge in _borderEdges)
        {
            GL.Vertex(edge.from);
            GL.Vertex(edge.to);
        }

        GL.End();
        GL.PopMatrix();
    }

    void OnDrawGizmos()
    {
        if (!ShowSeeds || _seedCanonicals == null) return;
        Gizmos.color = Color.white;
        foreach (int sc in _seedCanonicals)
            Gizmos.DrawSphere(transform.TransformPoint(_vertices[sc]), GizmoSphereRadius);
    }
}