using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MateriauType { Verre, Pierre, Bois }

public class VoronoiDestruction : MonoBehaviour
{
    [Header("Objet à détruire")]
    public GameObject TargetObject;

    [Header("Matériau")]
    public MateriauType Materiau = MateriauType.Pierre;

    List<GameObject> _fragments = new List<GameObject>();
    bool _explose = false;
    float _force, _rayon, _masse, _rebond, _drag;
    Color _couleur;

    void Start()
    {
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

        foreach (var fragment in _fragments)
        {
            if (fragment == null) continue;
            var rb = fragment.GetComponent<Rigidbody>();
            if (rb == null) continue;

            rb.isKinematic = false;
            rb.AddExplosionForce(_force, impactPoint, _rayon, 0.5f, ForceMode.Impulse);

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