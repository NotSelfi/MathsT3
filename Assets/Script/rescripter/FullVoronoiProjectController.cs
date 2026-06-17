using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FullVoronoiProjectController : MonoBehaviour
{
    public MeshFilter TargetMesh;
    public Material FragmentMaterial;

    public int RandomInternalPoints = 30;
    public int ImpactPoints = 12;

    public bool UseMeshVertices = true;
    public bool UseImpactLine = true;

    List<Vector3> points = new List<Vector3>();
    List<IncrementalDelaunay3D.Tetra> tets;
    List<VoronoiPolyhedron> cells;

    void Start()
    {
        Run();
    }

    [ContextMenu("Run Full Voronoi Project")]
    public void Run()
    {
        if (TargetMesh == null)
        {
            Debug.LogError("TargetMesh manquant");
            return;
        }

        BuildPointCloud();

        tets = IncrementalDelaunay3D.Build(points);
        DelaunayFlip3D.Legalize(points, tets);

        Debug.Log("Tétraèdres Delaunay : " + tets.Count);

        BuildVoronoiCells();

        BuildFragments();
    }

    void BuildPointCloud()
    {
        points.Clear();

        Mesh mesh = TargetMesh.sharedMesh;
        Bounds b = TargetMesh.GetComponent<Renderer>().bounds;

        if (UseMeshVertices)
        {
            foreach (Vector3 v in mesh.vertices)
                points.Add(TargetMesh.transform.TransformPoint(v));
        }

        Random.InitState(42);

        for (int i = 0; i < RandomInternalPoints; i++)
        {
            Vector3 p = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                Random.Range(b.min.z, b.max.z)
            );

            points.Add(p);
        }

        if (UseImpactLine)
        {
            Vector3 start = b.center + Vector3.up * b.extents.y;
            Vector3 dir = Vector3.down;

            ImpactPolylineSampler sampler = gameObject.AddComponent<ImpactPolylineSampler>();
            sampler.PointCount = ImpactPoints;

            points.AddRange(sampler.GenerateImpactLine(start, dir));
        }

        points = V3DGeometry.Unique(points, 0.001f);

        Debug.Log("Points : " + points.Count);
    }

    void BuildVoronoiCells()
    {
        cells = new List<VoronoiPolyhedron>();

        Bounds b = TargetMesh.GetComponent<Renderer>().bounds;

        for (int i = 0; i < points.Count; i++)
        {
            VoronoiPolyhedron cell = VoronoiPolyhedron.FromBounds(b);

            for (int j = 0; j < points.Count; j++)
            {
                if (i == j)
                    continue;

                V3DGeometry.Plane3 plane =
                    V3DGeometry.MedianPlane(points[i], points[j]);

                cell = cell.Clip(plane, true);

                if (cell == null)
                    break;
            }

            if (cell != null)
            {
                cell = MeshVoronoiClipper.ClipCellByMesh(
                    cell,
                    TargetMesh.sharedMesh,
                    TargetMesh.transform
                );

                if (cell != null)
                    cells.Add(cell);
            }
        }

        Debug.Log("Cellules Voronoï finales : " + cells.Count);
    }

    void BuildFragments()
    {
        foreach (VoronoiPolyhedron cell in cells)
        {
            GameObject go = new GameObject("Fragment_Voronoi");
            go.transform.SetParent(transform);

            Mesh mesh = cell.ToMesh();

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.7f, 1f);
            mr.sharedMaterial = mat;

            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.mass = 0.2f;
            rb.drag = 0.1f;
        }

        TargetMesh.gameObject.SetActive(false);
    }

    public void Explode(Vector3 impact, float force)
    {
        foreach (Transform child in transform)
        {
            Rigidbody rb = child.GetComponent<Rigidbody>();

            if (rb == null)
                continue;

            rb.isKinematic = false;

            Vector3 dir = child.position - impact;

            rb.AddForce(
                dir.normalized * force,
                ForceMode.Impulse
            );
        }
    }
}