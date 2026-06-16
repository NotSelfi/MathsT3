using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestructionDelaunay : MonoBehaviour
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

    [Header("Cube cible")]
    public GameObject TargetObject;
    public int PointCount = 15;
    public bool ShowDelaunay = false;
    public bool ShowVoronoi = true;

    [Header("Matériau")]
    public MateriauType Materiau = MateriauType.Pierre;

    List<GameObject> _fragments = new List<GameObject>();
    bool _explose = false;
    float _force, _rayon, _masse, _rebond, _drag;
    Color _couleur;

    List<Vector3> _points = new List<Vector3>();
    List<DelaunayVoronoi3D.Tetrahedron> _tetrahedra;
    List<DelaunayVoronoi3D.VoronoiEdge> _voronoiEdges;



    [ContextMenu("Regenerate")]
    void Generate()
    {
        if (TargetObject == null) { Debug.LogError("Assigne un objet !"); return; }

        _points.Clear();

        // Récupérer les vertices du mesh
        Mesh mesh = TargetObject.GetComponent<MeshFilter>().sharedMesh;
        Transform t = TargetObject.transform;

        // Dédoublonner les vertices (un cube Unity a des vertices dupliqués)
        var seen = new HashSet<Vector3>();
        foreach (var v in mesh.vertices)
        {
            Vector3 world = t.TransformPoint(v);
            // Arrondir pour dédoublonner
            Vector3 rounded = new Vector3(
                Mathf.Round(world.x * 100f) / 100f,
                Mathf.Round(world.y * 100f) / 100f,
                Mathf.Round(world.z * 100f) / 100f);

            if (seen.Add(rounded))
                _points.Add(world);
        }

        // Sous-échantillonner si trop de points
        int maxPoints = 20;
        if (_points.Count > maxPoints)
        {
            var sampled = new List<Vector3>();
            float step = (float)_points.Count / maxPoints;
            for (int i = 0; i < maxPoints; i++)
                sampled.Add(_points[Mathf.FloorToInt(i * step)]);
            _points = sampled;
        }

        Debug.Log($"Points après sous-échantillonnage : {_points.Count}");

        Debug.Log($"Points depuis le mesh : {_points.Count}");

        _tetrahedra = DelaunayVoronoi3D.ComputeDelaunay(_points);
        _voronoiEdges = DelaunayVoronoi3D.ComputeVoronoiEdges(_tetrahedra);

        Debug.Log($"Delaunay : {_tetrahedra.Count} tétraèdres");
        Debug.Log($"Voronoï : {_voronoiEdges.Count} arêtes");
    }

    void OnDrawGizmos()
    {
        if (_points == null || _points.Count == 0) return;

        Gizmos.color = Color.yellow;
        foreach (var p in _points)
            Gizmos.DrawSphere(p, 0.05f);

        if (ShowDelaunay && _tetrahedra != null)
        {
            Gizmos.color = new Color(1, 0.4f, 0, 0.5f);
            foreach (var tet in _tetrahedra)
            {
                Gizmos.DrawLine(_points[tet.a], _points[tet.b]);
                Gizmos.DrawLine(_points[tet.a], _points[tet.c]);
                Gizmos.DrawLine(_points[tet.a], _points[tet.d]);
                Gizmos.DrawLine(_points[tet.b], _points[tet.c]);
                Gizmos.DrawLine(_points[tet.b], _points[tet.d]);
                Gizmos.DrawLine(_points[tet.c], _points[tet.d]);
            }
        }

        if (ShowVoronoi && _voronoiEdges != null)
        {
            Gizmos.color = Color.green;
            foreach (var edge in _voronoiEdges)
                Gizmos.DrawLine(edge.from, edge.to);
        }
    }

    /****
     * -------- DESTRUCTION ------
     ****/


    void Start()
    {
        Generate();
        AppliquerMateriau();
        GenererFragments();
    }

    void Update()
    {
        if (_explose) return;
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
                Exploser(hit.point);
            else
                Exploser(transform.position);
        }
    }

    void AppliquerMateriau()
    {
        switch (Materiau)
        {
            case MateriauType.Verre:
                _force = 1200f; _rayon = 8f;
                _masse = 0.05f; _rebond = 0.6f; _drag = 0.05f;
                _couleur = new Color(0.6f, 0.9f, 1f, 0.5f);
                break;
            case MateriauType.Pierre:
                _force = 300f; _rayon = 2f;
                _masse = 5f; _rebond = 0.05f; _drag = 0.8f;
                _couleur = new Color(0.5f, 0.45f, 0.4f, 1f);
                break;
            case MateriauType.Bois:
                _force = 500f; _rayon = 4f;
                _masse = 0.4f; _rebond = 0.15f; _drag = 2f;
                _couleur = new Color(0.55f, 0.35f, 0.15f, 1f);
                break;
        }
    }

    void GenererFragments()
    {
        foreach (var f in _fragments) Destroy(f);
        _fragments.Clear();

        if (TargetObject == null) { Debug.LogError("Assigne un objet !"); return; }

        // Récupérer les vertices du mesh
        Mesh mesh = TargetObject.GetComponent<MeshFilter>().sharedMesh;
        Transform t = TargetObject.transform;

        // Dédoublonner + convertir en world space
        var seen = new HashSet<Vector3>();
        var points = new List<Vector3>();
        foreach (var v in mesh.vertices)
        {
            Vector3 world = t.TransformPoint(v);
            Vector3 rounded = new Vector3(
                Mathf.Round(world.x * 100f) / 100f,
                Mathf.Round(world.y * 100f) / 100f,
                Mathf.Round(world.z * 100f) / 100f);
            if (seen.Add(rounded))
                points.Add(world);
        }

        // Sous-échantillonner si trop de points
        int maxPoints = 20;
        if (points.Count > maxPoints)
        {
            var sampled = new List<Vector3>();
            float step = (float)points.Count / maxPoints;
            for (int i = 0; i < maxPoints; i++)
                sampled.Add(points[Mathf.FloorToInt(i * step)]);
            points = sampled;
        }

        Debug.Log($"Points : {points.Count}");

        // Delaunay
        var tetrahedra = DelaunayVoronoi3D.ComputeDelaunay(points);
        Debug.Log($"Tétraèdres : {tetrahedra.Count}");

        if (tetrahedra.Count == 0) { Debug.LogWarning("Pas de tétraèdres !"); return; }

        // Cellules Voronoï = circumcenters par point
        var circumcentersByPoint = new Dictionary<int, List<Vector3>>();
        for (int i = 0; i < points.Count; i++)
            circumcentersByPoint[i] = new List<Vector3>();

        foreach (var tet in tetrahedra)
        {
            circumcentersByPoint[tet.a].Add(tet.circumcenter);
            circumcentersByPoint[tet.b].Add(tet.circumcenter);
            circumcentersByPoint[tet.c].Add(tet.circumcenter);
            circumcentersByPoint[tet.d].Add(tet.circumcenter);
        }

        // Fragment par cellule
        for (int i = 0; i < points.Count; i++)
        {
            var cellVerts = circumcentersByPoint[i];
            if (cellVerts.Count < 4) continue;

            // Filtrer outliers
            Vector3 site = points[i];
            var distances = new List<float>();
            foreach (var v in cellVerts)
                distances.Add(Vector3.Distance(site, v));
            distances.Sort();
            float maxDist = distances[distances.Count / 2] * 3f;

            var filtered = new List<Vector3>();
            foreach (var v in cellVerts)
                if (Vector3.Distance(site, v) <= maxDist)
                    filtered.Add(v);

            if (filtered.Count < 4) continue;

            var faces = ConvexHull3D.ComputeHull(filtered);
            if (faces.Count == 0) continue;

            Mesh fragMesh = ConvexHull3D.BuildMesh(filtered, faces);
            if (fragMesh == null) continue;

            var go = new GameObject($"Fragment_{i}");
            go.transform.parent = transform;
            go.transform.position = Vector3.zero;

            go.AddComponent<MeshFilter>().mesh = fragMesh;

            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.color = _couleur;
            mr.material = mat;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = fragMesh;
            mc.convex = true;

            var physicMat = new PhysicMaterial();
            physicMat.bounciness = _rebond;
            physicMat.dynamicFriction = 1f - _rebond;
            physicMat.staticFriction = 1f - _rebond;
            mc.material = physicMat;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = _masse;
            rb.drag = _drag;
            rb.isKinematic = true;

            _fragments.Add(go);
        }

        // Cacher l'objet original
        TargetObject.SetActive(false);

        Debug.Log($"[{Materiau}] {_fragments.Count} fragments générés");
    }


    public void Exploser(Vector3 impactPoint)
    {
        if (_explose) return;
        _explose = true;
        StartCoroutine(PropagerExplosion(impactPoint));
    }

    IEnumerator PropagerExplosion(Vector3 impactPoint)
    {

        _fragments.Sort((a, b) =>
            Vector3.Distance(a.transform.position, impactPoint)
            .CompareTo(Vector3.Distance(b.transform.position, impactPoint)));

        for (int i = 0; i < _fragments.Count; i++)
        {
            var fragment = _fragments[i];
            if (fragment == null) continue;

            var rb = fragment.GetComponent<Rigidbody>();
            if (rb == null) continue;

            // Détacher le fragment
            rb.isKinematic = false;
            rb.AddExplosionForce(_force, impactPoint, _rayon, 0.5f, ForceMode.Impulse);
            Destroy(fragment, 3f);

            // Recalculer Delaunay + Voronoï sur les fragments restants
            var remainingPoints = new List<Vector3>();
            for (int j = i + 1; j < _fragments.Count; j++)
            {
                if (_fragments[j] != null)
                {
                    // Utiliser le centre du mesh plutôt que transform.position
                    var mf = _fragments[j].GetComponent<MeshFilter>();
                    if (mf != null)
                        remainingPoints.Add(_fragments[j].transform.TransformPoint(mf.mesh.bounds.center));
                }
            }

            if (remainingPoints.Count >= 4)
            {
                _points = remainingPoints;
                _tetrahedra = DelaunayVoronoi3D.ComputeDelaunay(_points);
                _voronoiEdges = DelaunayVoronoi3D.ComputeVoronoiEdges(_tetrahedra);
            }
            else
            {
                _points.Clear();
                _tetrahedra = null;
                _voronoiEdges = null;
            }
            if (remainingPoints.Count >= 4)
            {
                _points = remainingPoints;
                _tetrahedra = DelaunayVoronoi3D.ComputeDelaunay(_points);
                _voronoiEdges = DelaunayVoronoi3D.ComputeVoronoiEdges(_tetrahedra);
                Debug.Log($"Recalcul : {_points.Count} points, {_tetrahedra.Count} tétraèdres, {_voronoiEdges.Count} arêtes");
            }
            float dist = Vector3.Distance(fragment.transform.position, impactPoint);

            yield return new WaitForSeconds(DelaiPropagation(dist));
        }
        
    }

    float DelaiPropagation(float dist)
    {
        switch (Materiau)
        {
            case MateriauType.Verre: return dist * 0.001f;
            case MateriauType.Pierre: return dist * 0.05f;
            case MateriauType.Bois: return dist * 0.03f;
            default: return 0f;
        }
    }

    [ContextMenu("Reset")]
    public void ResetExplosion()
    {
        if (TargetObject != null) TargetObject.SetActive(true);
        _explose = false;
        GenererFragments();
    }
}
