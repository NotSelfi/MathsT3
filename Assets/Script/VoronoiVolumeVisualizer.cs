using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public class VoronoiVolumeVisualizer : MonoBehaviour
{

    [Header("Cube cible")]
    public GameObject TargetCube;
    public int PointCount = 15;
    public bool ShowDelaunay = false;
    public bool ShowVoronoi = true;

    List<Vector3> _points = new List<Vector3>();
    List<DelaunayVoronoi3D.Tetrahedron> _tetrahedra;
    List<DelaunayVoronoi3D.VoronoiEdge> _voronoiEdges;

    void Start() => Generate();

    [ContextMenu("Regenerate")]
    void Generate()
    {
        if (TargetCube == null) { Debug.LogError("Assigne un objet !"); return; }

        _points.Clear();

        // Récupérer les vertices du mesh
        Mesh mesh = TargetCube.GetComponent<MeshFilter>().sharedMesh;
        Transform t = TargetCube.transform;

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
        if (_points == null) return;

        // Points
        Gizmos.color = Color.yellow;
        foreach (var p in _points)
            Gizmos.DrawSphere(p, 0.05f);

        // Delaunay
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

        // Voronoï
        if (ShowVoronoi && _voronoiEdges != null)
        {
            Gizmos.color = Color.green;
            foreach (var edge in _voronoiEdges)
                Gizmos.DrawLine(edge.from, edge.to);
        }
    }
}
