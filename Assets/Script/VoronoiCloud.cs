using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Branche DelaunayVoronoi3D sur PointCloudEditor.
/// A chaque ajout/suppression de point, recalcule la triangulation et les aretes.
/// </summary>
public class VoronoiCloud : PointCloudEditor
{
    [Header("Delaunay / Voronoi")]
    public bool drawDelaunay = true;
    public bool drawVoronoi = true;
    public bool drawCircumcenters = false;
    public Color delaunayColor = Color.yellow;
    public Color voronoiColor = Color.magenta;
    public Color circumcenterColor = Color.green;
    public float epsilon = 0.0001f;

    List<DelaunayVoronoi3D.Tetrahedron> _tetra = new List<DelaunayVoronoi3D.Tetrahedron>();
    HashSet<DelaunayVoronoi3D.Edge> _delaunayEdges = new HashSet<DelaunayVoronoi3D.Edge>();
    List<DelaunayVoronoi3D.VoronoiEdge> _voronoiEdges = new List<DelaunayVoronoi3D.VoronoiEdge>();

    protected override void OnPointsChanged(List<Vector3> points)
    {
        _tetra = DelaunayVoronoi3D.ComputeDelaunay(points, epsilon);
        _delaunayEdges = DelaunayVoronoi3D.GetDelaunayEdges(_tetra);
        _voronoiEdges = DelaunayVoronoi3D.ComputeVoronoiEdges(_tetra);
    }

    // Cette methode masque celle de la base : on redessine donc aussi les points ici.
    void OnDrawGizmos()
    {
        // Points
        if (drawGizmos)
        {
            Gizmos.color = pointColor;
            for (int i = 0; i < Points.Count; i++)
                Gizmos.DrawSphere(Points[i], pointRadius);
        }

        // Aretes de Delaunay (entre les points, via leurs indices)
        if (drawDelaunay)
        {
            Gizmos.color = delaunayColor;
            foreach (var e in _delaunayEdges)
            {
                if (e.a < Points.Count && e.b < Points.Count)
                    Gizmos.DrawLine(Points[e.a], Points[e.b]);
            }
        }

        // Aretes de Voronoi (entre circoncentres des tetraedres voisins)
        if (drawVoronoi)
        {
            Gizmos.color = voronoiColor;
            foreach (var v in _voronoiEdges)
                Gizmos.DrawLine(v.from, v.to);
        }

        // Circoncentres (sommets de Voronoi)
        if (drawCircumcenters)
        {
            Gizmos.color = circumcenterColor;
            for (int i = 0; i < _tetra.Count; i++)
                Gizmos.DrawWireSphere(_tetra[i].circumcenter, pointRadius * 0.7f);
        }
    }
}
