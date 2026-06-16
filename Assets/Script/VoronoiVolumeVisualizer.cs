using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public class VoronoiVolumeVisualizer : MonoBehaviour
{
    [Header("Objet cible")]
    public GameObject TargetCube;

    [Header("Points")]
    public int PointCount = 20;
    public int RandomSeed = 42;
    public float PointRadius = 0.05f;

    [Header("Affichage")]
    public bool ShowPoints = true;
    public bool ShowDelaunay = true;
    public bool ShowVoronoi = true;
    public bool HideOriginalObject = false;

    [Header("Couleurs")]
    public Color PointColor = Color.yellow;
    public Color DelaunayColor = new Color(1f, 0.45f, 0f, 1f);
    public Color VoronoiColor = Color.red;

    List<Vector3> _points = new List<Vector3>();
    List<DelaunayVoronoi3D.Tetrahedron> _tetrahedra;
    HashSet<DelaunayVoronoi3D.Edge> _delaunayEdges;
    List<Polyhedron> _voronoiCells = new List<Polyhedron>();

    Material _lineMaterial;

    const float EPS = 0.0001f;

    class Polyhedron
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<List<int>> faces = new List<List<int>>();
    }

    void Start()
    {
        Generate();
    }

    [ContextMenu("Regenerate")]
    public void Generate()
    {
        _points.Clear();
        _voronoiCells.Clear();

        if (TargetCube == null)
        {
            Debug.LogError("Assigne un TargetCube.");
            return;
        }

        TargetCube.SetActive(true);

        Renderer r = TargetCube.GetComponent<Renderer>();
        if (r == null)
        {
            Debug.LogError("TargetCube doit avoir un Renderer.");
            return;
        }

        Bounds bounds = r.bounds;

        GenerateRandomPoints(bounds);

        _tetrahedra = DelaunayVoronoi3D.ComputeDelaunay(_points);
        _delaunayEdges = DelaunayVoronoi3D.GetDelaunayEdges(_tetrahedra);

        GenerateVoronoiCells(bounds);

        TargetCube.SetActive(!HideOriginalObject);

        Debug.Log("Points : " + _points.Count);
        Debug.Log("Tétraèdres Delaunay : " + _tetrahedra.Count);
        Debug.Log("Arêtes Delaunay : " + _delaunayEdges.Count);
        Debug.Log("Cellules Voronoï : " + _voronoiCells.Count);
    }

    void GenerateRandomPoints(Bounds bounds)
    {
        Random.InitState(RandomSeed);

        for (int i = 0; i < PointCount; i++)
        {
            Vector3 p = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );

            _points.Add(p);
        }
    }

    void GenerateVoronoiCells(Bounds bounds)
    {
        for (int i = 0; i < _points.Count; i++)
        {
            Polyhedron cell = CreateBoxPolyhedron(bounds);

            for (int j = 0; j < _points.Count; j++)
            {
                if (i == j) continue;

                Vector3 a = _points[i];
                Vector3 b = _points[j];

                Vector3 mid = (a + b) * 0.5f;
                Vector3 normal = (b - a).normalized;

                cell = ClipPolyhedron(cell, mid, normal);

                if (cell == null)
                    break;
            }

            if (cell != null && cell.vertices.Count >= 4 && cell.faces.Count >= 4)
                _voronoiCells.Add(cell);
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
            List<Vector3> polygon = new List<Vector3>();

            foreach (int index in face)
                polygon.Add(poly.vertices[index]);

            List<Vector3> clipped = ClipPolygon(polygon, planePoint, planeNormal, capPoints);

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

    List<Vector3> ClipPolygon(
        List<Vector3> polygon,
        Vector3 planePoint,
        Vector3 planeNormal,
        List<Vector3> capPoints)
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

    void CreateLineMaterial()
    {
        if (_lineMaterial != null)
            return;

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
        CreateLineMaterial();

        if (_lineMaterial == null)
            return;

        _lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.Begin(GL.LINES);

        if (ShowDelaunay && _delaunayEdges != null)
        {
            GL.Color(DelaunayColor);

            foreach (var edge in _delaunayEdges)
            {
                GL.Vertex(_points[edge.a]);
                GL.Vertex(_points[edge.b]);
            }
        }

        if (ShowVoronoi && _voronoiCells != null)
        {
            GL.Color(VoronoiColor);

            foreach (Polyhedron cell in _voronoiCells)
            {
                foreach (List<int> face in cell.faces)
                {
                    for (int i = 0; i < face.Count; i++)
                    {
                        Vector3 a = cell.vertices[face[i]];
                        Vector3 b = cell.vertices[face[(i + 1) % face.Count]];

                        GL.Vertex(a);
                        GL.Vertex(b);
                    }
                }
            }
        }

        GL.End();
        GL.PopMatrix();
    }

    void OnDrawGizmos()
    {
        if (ShowPoints && _points != null)
        {
            Gizmos.color = PointColor;

            foreach (Vector3 p in _points)
                Gizmos.DrawSphere(p, PointRadius);
        }

        if (ShowDelaunay && _delaunayEdges != null)
        {
            Gizmos.color = DelaunayColor;

            foreach (var edge in _delaunayEdges)
                Gizmos.DrawLine(_points[edge.a], _points[edge.b]);
        }

        if (ShowVoronoi && _voronoiCells != null)
        {
            Gizmos.color = VoronoiColor;

            foreach (Polyhedron cell in _voronoiCells)
            {
                foreach (List<int> face in cell.faces)
                {
                    for (int i = 0; i < face.Count; i++)
                    {
                        Vector3 a = cell.vertices[face[i]];
                        Vector3 b = cell.vertices[face[(i + 1) % face.Count]];

                        Gizmos.DrawLine(a, b);
                    }
                }
            }
        }
    }
}