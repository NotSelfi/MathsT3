using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nuage de points cliquable en 3D.
/// Clic gauche  : ajoute un point.
/// Clic droit   : supprime le point le plus proche du rayon souris.
/// Molette      : regle la profondeur de placement (mode Depth).
/// Touche G     : remplit un nuage 3D aleatoire (test rapide).
/// Touche C     : efface tout.
/// </summary>
public class PointCloudEditor : MonoBehaviour
{
    public enum PlacementMode
    {
        Surface, // pose sur un collider (Voronoi surfacique : points coplanaires possibles)
        Depth    // pose le long du rayon camera a 'placeDistance' (vrai nuage 3D)
    }

    [Header("Placement")]
    public Camera cam;                       // par defaut Camera.main
    public PlacementMode mode = PlacementMode.Depth;
    public LayerMask clickMask = ~0;         // surfaces cliquables (mode Surface)
    public float placeDistance = 5f;         // distance camera->point (mode Depth)
    public float scrollSpeed = 0.5f;

    [Header("Test aleatoire (touche G)")]
    public int randomCount = 12;
    public Vector3 randomBounds = new Vector3(4, 4, 4);

    [Header("Visuel (Gizmos)")]
    public float pointRadius = 0.1f;
    public Color pointColor = Color.cyan;
    public bool drawGizmos = true;

    public List<Vector3> Points { get; } = new List<Vector3>();

    public System.Action<List<Vector3>> onPointsChanged;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        // Molette : ajuste la profondeur en mode Depth
        float scroll = Input.mouseScrollDelta.y;
        if (mode == PlacementMode.Depth && Mathf.Abs(scroll) > 0.01f)
            placeDistance = Mathf.Max(0.5f, placeDistance + scroll * scrollSpeed);

        if (Input.GetMouseButtonDown(0))
        {
            if (TryGetWorldPoint(out Vector3 p)) AddPoint(p);
        }
        else if (Input.GetMouseButtonDown(1))
        {
            RemoveNearest();
        }
        else if (Input.GetKeyDown(KeyCode.G))
        {
            RandomFill();
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            Clear();
        }
    }

    bool TryGetWorldPoint(out Vector3 point)
    {
        point = Vector3.zero;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (mode == PlacementMode.Surface)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, clickMask))
            {
                point = hit.point;
                return true;
            }
            return false;
        }

        // Mode Depth : on pose le long du rayon, a placeDistance de la camera
        point = ray.origin + ray.direction * placeDistance;
        return true;
    }

    public void AddPoint(Vector3 p)
    {
        Points.Add(p);
        Recompute();
    }

    public void RemoveNearest()
    {
        if (Points.Count == 0) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Points.Count; i++)
        {
            float d = Vector3.Cross(ray.direction, Points[i] - ray.origin).magnitude;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        if (best >= 0 && bestDist < pointRadius * 5f)
        {
            Points.RemoveAt(best);
            Recompute();
        }
    }

    public void RandomFill()
    {
        Points.Clear();
        Vector3 origin = transform.position;
        for (int i = 0; i < randomCount; i++)
        {
            Vector3 p = origin + new Vector3(
                Random.Range(-randomBounds.x, randomBounds.x) * 0.5f,
                Random.Range(-randomBounds.y, randomBounds.y) * 0.5f,
                Random.Range(-randomBounds.z, randomBounds.z) * 0.5f);
            Points.Add(p);
        }
        Recompute();
    }

    public void Clear()
    {
        Points.Clear();
        Recompute();
    }

    void Recompute()
    {
        OnPointsChanged(Points);
        onPointsChanged?.Invoke(Points);
    }

    protected virtual void OnPointsChanged(List<Vector3> points) { }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = pointColor;
        for (int i = 0; i < Points.Count; i++)
            Gizmos.DrawSphere(Points[i], pointRadius);
    }
}