using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoronoiVolumeCutter : MonoBehaviour
{
    public MeshFilter TargetMesh;
    public int SeedCount = 30;
    public int RandomSeed = 42;

    public bool BuildFragments = true;

    List<Vector3> _sites = new List<Vector3>();
    List<VoronoiPolyhedron> _cells = new List<VoronoiPolyhedron>();

    [ContextMenu("Generate Voronoi Volume")]
    public void Generate()
    {
        if (TargetMesh == null)
        {
            Debug.LogError("TargetMesh manquant");
            return;
        }

        Mesh mesh = TargetMesh.sharedMesh;
        Bounds bounds = mesh.bounds;

        GenerateSites(bounds);

        _cells.Clear();

        for (int i = 0; i < _sites.Count; i++)
        {
            VoronoiPolyhedron cell =
                VoronoiPolyhedron.FromBounds(bounds);

            for (int j = 0; j < _sites.Count; j++)
            {
                if (i == j)
                    continue;

                V3DGeometry.Plane3 plane =
                    V3DGeometry.MedianPlane(
                        _sites[i],
                        _sites[j]
                    );

                cell = cell.Clip(plane, true);

                if (cell == null)
                    break;
            }

            if (cell != null)
                _cells.Add(cell);
        }

        Debug.Log("Cellules Voronoï : " + _cells.Count);

        if (BuildFragments)
            BuildMeshes();
    }

    void GenerateSites(Bounds b)
    {
        _sites.Clear();

        Random.InitState(RandomSeed);

        for (int i = 0; i < SeedCount; i++)
        {
            Vector3 p = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                Random.Range(b.min.z, b.max.z)
            );

            _sites.Add(p);
        }
    }

    void BuildMeshes()
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            GameObject go =
                new GameObject("VoronoiCell_" + i);

            go.transform.SetParent(transform);

            MeshFilter mf =
                go.AddComponent<MeshFilter>();

            MeshRenderer mr =
                go.AddComponent<MeshRenderer>();

            mf.sharedMesh =
                _cells[i].ToMesh();

            Material mat =
                new Material(
                    Shader.Find("Standard")
                );

            mat.color =
                Random.ColorHSV();

            mr.sharedMaterial = mat;
        }
    }

    public List<VoronoiPolyhedron> Cells
    {
        get { return _cells; }
    }

    public List<Vector3> Sites
    {
        get { return _sites; }
    }
}
