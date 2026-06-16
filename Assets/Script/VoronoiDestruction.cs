using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MateriauType { Verre, Pierre, Bois }

public class VoronoiDestruction : MonoBehaviour
{
    [Header("Objet à détruire")]
    public GameObject TargetObject;

    [Header("Voronoï")]
    public int PointCount = 25;
    public int RandomSeed = 42;

    [Header("Affichage Debug")]
    public bool HideOriginalObject = true;
    public bool ShowVoronoiWireframe = true;
    public bool ShowFragmentCenters = true;
    public Color WireColor = Color.red;
    public float CenterRadius = 0.04f;

    [Header("Matériau")]
    public MateriauType Materiau = MateriauType.Pierre;

    List<GameObject> _fragments = new List<GameObject>();
    List<Vector3> _seeds = new List<Vector3>();

    Material _lineMaterial;

    bool _explose = false;
    float _force, _rayon, _masse, _rebond, _drag;
    Color _couleur;

    const float EPS = 0.0001f;

    class Polyhedron
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<List<int>> faces = new List<List<int>>();
    }

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
            else if (TargetObject != null)
                Exploser(TargetObject.transform.position);
            else
                Exploser(transform.position);
        }
    }

    void AppliquerMateriau()
    {
        switch (Materiau)
        {
            case MateriauType.Verre:
                _force = 500f;
                _rayon = 5f;
                _masse = 0.05f;
                _rebond = 0.6f;
                _drag = 0.05f;
                _couleur = new Color(0.6f, 0.9f, 1f, 0.35f);
                break;

            case MateriauType.Pierre:
                _force = 150f;
                _rayon = 2f;
                _masse = 5f;
                _rebond = 0.05f;
                _drag = 0.8f;
                _couleur = new Color(0.5f, 0.45f, 0.4f, 0.65f);
                break;

            case MateriauType.Bois:
                _force = 250f;
                _rayon = 3f;
                _masse = 0.4f;
                _rebond = 0.15f;
                _drag = 2f;
                _couleur = new Color(0.55f, 0.35f, 0.15f, 0.65f);
                break;
        }
    }

    [ContextMenu("Regenerate")]
    public void GenererFragments()
    {
        foreach (var f in _fragments)
        {
            if (f != null)
            {
                if (Application.isPlaying)
                    Destroy(f);
                else
                    DestroyImmediate(f);
            }
        }

        _fragments.Clear();
        _seeds.Clear();
        _explose = false;

        if (TargetObject == null)
        {
            Debug.LogError("Assigne un TargetObject.");
            return;
        }

        TargetObject.SetActive(true);

        Renderer r = TargetObject.GetComponent<Renderer>();
        if (r == null)
        {
            Debug.LogError("TargetObject doit avoir un Renderer.");
            return;
        }

        Bounds bounds = r.bounds;
        GenererPointsDansBounds(bounds);

        for (int i = 0; i < _seeds.Count; i++)
        {
            Polyhedron cell = CreateBoxPolyhedron(bounds);

            for (int j = 0; j < _seeds.Count; j++)
            {
                if (i == j) continue;

                Vector3 a = _seeds[i];
                Vector3 b = _seeds[j];

                Vector3 mid = (a + b) * 0.5f;
                Vector3 normal = (b - a).normalized;

                cell = ClipPolyhedron(cell, mid, normal);

                if (cell == null || cell.vertices.Count < 4 || cell.faces.Count < 4)
                    break;
            }

            if (cell == null || cell.vertices.Count < 4 || cell.faces.Count < 4)
                continue;

            Mesh mesh = BuildMesh(cell);

            if (mesh == null || mesh.vertexCount < 4)
                continue;

            GameObject frag = new GameObject("VoronoiFragment_" + i);
            frag.transform.parent = transform;
            frag.transform.position = Vector3.zero;
            frag.transform.rotation = Quaternion.identity;
            frag.transform.localScale = Vector3.one;

            MeshFilter mf = frag.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = frag.AddComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = _couleur;
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mr.material = mat;

            MeshCollider mc = frag.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;

            PhysicMaterial physicMat = new PhysicMaterial();
            physicMat.bounciness = _rebond;
            physicMat.dynamicFriction = 1f - _rebond;
            physicMat.staticFriction = 1f - _rebond;
            mc.material = physicMat;

            Rigidbody rb = frag.AddComponent<Rigidbody>();
            rb.mass = _masse;
            rb.drag = _drag;
            rb.isKinematic = true;

            _fragments.Add(frag);
        }

        TargetObject.SetActive(!HideOriginalObject);

        Debug.Log("Fragments Voronoï générés : " + _fragments.Count);
    }

    void GenererPointsDansBounds(Bounds bounds)
    {
        Random.InitState(RandomSeed);

        for (int i = 0; i < PointCount; i++)
        {
            Vector3 p = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );

            _seeds.Add(p);
        }
    }

    Polyhedron CreateBoxPolyhedron(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;

        Polyhedron p = new Polyhedron();

        p.vertices.Add(new Vector3(min.x, min.y, min.z));
        p.vertices.Add(new Vector3(max.x, min.y, min.z));
        p.vertices.Add(new Vector3(max.x, max.y, min.z));
        p.vertices.Add(new Vector3(min.x, max.y, min.z));
        p.vertices.Add(new Vector3(min.x, min.y, max.z));
        p.vertices.Add(new Vector3(max.x, min.y, max.z));
        p.vertices.Add(new Vector3(max.x, max.y, max.z));
        p.vertices.Add(new Vector3(min.x, max.y, max.z));

        p.faces.Add(new List<int> { 0, 3, 2, 1 });
        p.faces.Add(new List<int> { 4, 5, 6, 7 });
        p.faces.Add(new List<int> { 0, 1, 5, 4 });
        p.faces.Add(new List<int> { 3, 7, 6, 2 });
        p.faces.Add(new List<int> { 1, 2, 6, 5 });
        p.faces.Add(new List<int> { 0, 4, 7, 3 });

        return p;
    }

    Polyhedron ClipPolyhedron(Polyhedron poly, Vector3 planePoint, Vector3 planeNormal)
    {
        Polyhedron result = new Polyhedron();
        List<Vector3> capPoints = new List<Vector3>();

        foreach (List<int> face in poly.faces)
        {
            List<Vector3> input = new List<Vector3>();

            foreach (int index in face)
                input.Add(poly.vertices[index]);

            List<Vector3> clipped = ClipPolygon(input, planePoint, planeNormal, capPoints);

            if (clipped.Count >= 3)
            {
                List<int> newFace = new List<int>();

                foreach (Vector3 v in clipped)
                    newFace.Add(AddUniqueVertex(result.vertices, v));

                result.faces.Add(newFace);
            }
        }

        List<Vector3> uniqueCap = UniquePoints(capPoints);

        if (uniqueCap.Count >= 3)
        {
            SortPointsOnPlane(uniqueCap, planeNormal);

            List<int> capFace = new List<int>();

            foreach (Vector3 v in uniqueCap)
                capFace.Add(AddUniqueVertex(result.vertices, v));

            result.faces.Add(capFace);
        }

        if (result.vertices.Count < 4 || result.faces.Count < 4)
            return null;

        return result;
    }

    List<Vector3> ClipPolygon(List<Vector3> polygon, Vector3 planePoint, Vector3 planeNormal, List<Vector3> capPoints)
    {
        List<Vector3> output = new List<Vector3>();

        if (polygon.Count == 0)
            return output;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 current = polygon[i];
            Vector3 next = polygon[(i + 1) % polygon.Count];

            float currentSide = Vector3.Dot(planeNormal, current - planePoint);
            float nextSide = Vector3.Dot(planeNormal, next - planePoint);

            bool currentInside = currentSide <= EPS;
            bool nextInside = nextSide <= EPS;

            if (currentInside && nextInside)
            {
                output.Add(next);
            }
            else if (currentInside && !nextInside)
            {
                Vector3 hit = SegmentPlaneIntersection(current, next, planePoint, planeNormal);
                output.Add(hit);
                capPoints.Add(hit);
            }
            else if (!currentInside && nextInside)
            {
                Vector3 hit = SegmentPlaneIntersection(current, next, planePoint, planeNormal);
                output.Add(hit);
                output.Add(next);
                capPoints.Add(hit);
            }
        }

        return output;
    }

    Vector3 SegmentPlaneIntersection(Vector3 a, Vector3 b, Vector3 planePoint, Vector3 planeNormal)
    {
        Vector3 ab = b - a;
        float denom = Vector3.Dot(planeNormal, ab);

        if (Mathf.Abs(denom) < EPS)
            return a;

        float t = Vector3.Dot(planeNormal, planePoint - a) / denom;
        t = Mathf.Clamp01(t);

        return a + ab * t;
    }

    int AddUniqueVertex(List<Vector3> vertices, Vector3 v)
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            if ((vertices[i] - v).sqrMagnitude < EPS * EPS)
                return i;
        }

        vertices.Add(v);
        return vertices.Count - 1;
    }

    List<Vector3> UniquePoints(List<Vector3> points)
    {
        List<Vector3> result = new List<Vector3>();

        foreach (Vector3 p in points)
        {
            bool found = false;

            foreach (Vector3 q in result)
            {
                if ((p - q).sqrMagnitude < EPS * EPS)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                result.Add(p);
        }

        return result;
    }

    void SortPointsOnPlane(List<Vector3> points, Vector3 normal)
    {
        Vector3 center = Vector3.zero;

        foreach (Vector3 p in points)
            center += p;

        center /= points.Count;

        Vector3 axisA = Vector3.Cross(normal, Vector3.up);

        if (axisA.sqrMagnitude < EPS)
            axisA = Vector3.Cross(normal, Vector3.right);

        axisA.Normalize();

        Vector3 axisB = Vector3.Cross(normal, axisA).normalized;

        points.Sort((p1, p2) =>
        {
            Vector3 d1 = p1 - center;
            Vector3 d2 = p2 - center;

            float a1 = Mathf.Atan2(Vector3.Dot(d1, axisB), Vector3.Dot(d1, axisA));
            float a2 = Mathf.Atan2(Vector3.Dot(d2, axisB), Vector3.Dot(d2, axisA));

            return a1.CompareTo(a2);
        });
    }

    Mesh BuildMesh(Polyhedron poly)
    {
        Mesh mesh = new Mesh();

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        foreach (List<int> face in poly.faces)
        {
            if (face.Count < 3) continue;

            int startIndex = verts.Count;

            foreach (int index in face)
                verts.Add(poly.vertices[index]);

            for (int i = 1; i < face.Count - 1; i++)
            {
                tris.Add(startIndex);
                tris.Add(startIndex + i);
                tris.Add(startIndex + i + 1);
            }
        }

        if (verts.Count < 4 || tris.Count < 12)
            return null;

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
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
            Vector3.Distance(a.GetComponent<Renderer>().bounds.center, impactPoint)
            .CompareTo(Vector3.Distance(b.GetComponent<Renderer>().bounds.center, impactPoint)));

        foreach (GameObject fragment in _fragments)
        {
            if (fragment == null) continue;

            Rigidbody rb = fragment.GetComponent<Rigidbody>();
            if (rb == null) continue;

            rb.isKinematic = false;
            rb.AddExplosionForce(_force, impactPoint, _rayon, 0.5f, ForceMode.Impulse);

            float dist = Vector3.Distance(fragment.GetComponent<Renderer>().bounds.center, impactPoint);

            yield return new WaitForSeconds(DelaiPropagation(dist));
        }
    }

    float DelaiPropagation(float dist)
    {
        switch (Materiau)
        {
            case MateriauType.Verre:
                return dist * 0.2f;

            case MateriauType.Pierre:
                return dist * 0.2f;

            case MateriauType.Bois:
                return dist * 0.2f;

            default:
                return dist * 0.2f;
        }
    }

    [ContextMenu("Reset")]
    public void ResetExplosion()
    {
        if (TargetObject != null)
            TargetObject.SetActive(true);

        GenererFragments();
    }

    void CreateLineMaterial()
    {
        if (_lineMaterial != null) return;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        _lineMaterial = new Material(shader);
        _lineMaterial.hideFlags = HideFlags.HideAndDontSave;

        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite", 0);
    }

    void OnRenderObject()
    {
        if (!ShowVoronoiWireframe || _fragments == null)
            return;

        CreateLineMaterial();

        if (_lineMaterial == null)
            return;

        _lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.Begin(GL.LINES);
        GL.Color(WireColor);

        foreach (GameObject frag in _fragments)
        {
            if (frag == null) continue;

            MeshFilter mf = frag.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh mesh = mf.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Transform tr = frag.transform;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = tr.TransformPoint(vertices[triangles[i]]);
                Vector3 b = tr.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = tr.TransformPoint(vertices[triangles[i + 2]]);

                GL.Vertex(a);
                GL.Vertex(b);

                GL.Vertex(b);
                GL.Vertex(c);

                GL.Vertex(c);
                GL.Vertex(a);
            }
        }

        GL.End();
        GL.PopMatrix();
    }

    void OnDrawGizmos()
    {
        if (ShowFragmentCenters && _seeds != null)
        {
            Gizmos.color = Color.yellow;

            foreach (Vector3 p in _seeds)
                Gizmos.DrawSphere(p, CenterRadius);
        }

        if (!ShowVoronoiWireframe || _fragments == null)
            return;

        Gizmos.color = WireColor;

        foreach (GameObject frag in _fragments)
        {
            if (frag == null) continue;

            MeshFilter mf = frag.GetComponent<MeshFilter>();

            if (mf == null || mf.sharedMesh == null)
                continue;

            Mesh mesh = mf.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Transform tr = frag.transform;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = tr.TransformPoint(vertices[triangles[i]]);
                Vector3 b = tr.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = tr.TransformPoint(vertices[triangles[i + 2]]);

                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(b, c);
                Gizmos.DrawLine(c, a);
            }
        }
    }
}