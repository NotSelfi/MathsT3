using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeoUtils : MonoBehaviour
{
    // ─────────────────────────────────────────
    // Plan médiateur entre deux points
    // Retourne un point sur le plan (milieu) et la normale
    // ─────────────────────────────────────────
    public static void MedianPlane(Vector3 a, Vector3 b, out Vector3 point, out Vector3 normal)
    {
        point = (a + b) / 2f;
        normal = (b - a).normalized;
    }

    // ─────────────────────────────────────────
    // Intersection d'un plan avec un segment [a, b]
    // Retourne false si le segment est parallèle au plan
    // ─────────────────────────────────────────
    public static bool PlanSegmentIntersect(Vector3 planePoint, Vector3 planeNormal,
                                             Vector3 a, Vector3 b, out Vector3 hit)
    {
        hit = Vector3.zero;
        Vector3 ab = b - a;
        float denom = Vector3.Dot(planeNormal, ab);

        if (Mathf.Abs(denom) < 1e-6f) return false; // parallèle

        float t = Vector3.Dot(planeNormal, planePoint - a) / denom;
        if (t < 0f || t > 1f) return false; // hors segment

        hit = a + t * ab;
        return true;
    }

    // ─────────────────────────────────────────
    // Intersection d'un plan avec un triangle
    // Retourne 0, 1 ou 2 points d'intersection
    // ─────────────────────────────────────────
    public static int PlanTriangleIntersect(Vector3 planePoint, Vector3 planeNormal,
                                             Vector3 tA, Vector3 tB, Vector3 tC,
                                             out Vector3 hit1, out Vector3 hit2)
    {
        hit1 = hit2 = Vector3.zero;
        Vector3[] hits = new Vector3[2];
        int count = 0;

        Vector3[] verts = { tA, tB, tC };
        for (int i = 0; i < 3 && count < 2; i++)
        {
            Vector3 p = verts[i];
            Vector3 q = verts[(i + 1) % 3];
            if (PlanSegmentIntersect(planePoint, planeNormal, p, q, out Vector3 h))
                hits[count++] = h;
        }

        if (count > 0) hit1 = hits[0];
        if (count > 1) hit2 = hits[1];
        return count;
    }

    // ─────────────────────────────────────────
    // Sphère circonscrite d'un tétraèdre
    // Retourne centre et rayon
    // ─────────────────────────────────────────
    public static bool CircumsphereTetrahedron(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                                             out Vector3 center, out float radius)
    {
        center = Vector3.zero;
        radius = 0f;

        Vector3 ab = b - a, ac = c - a, ad = d - a;

        float det = ab.x * (ac.y * ad.z - ac.z * ad.y)
                  - ab.y * (ac.x * ad.z - ac.z * ad.x)
                  + ab.z * (ac.x * ad.y - ac.y * ad.x);

        if (Mathf.Abs(det) < 1e-10f) return false;

        float bx = ab.sqrMagnitude / 2f;
        float by = ac.sqrMagnitude / 2f;
        float bz = ad.sqrMagnitude / 2f;

        float cx = (bx * (ac.y * ad.z - ac.z * ad.y)
                  - by * (ab.y * ad.z - ab.z * ad.y)
                  + bz * (ab.y * ac.z - ab.z * ac.y)) / det;

        float cy = (ab.x * (by * ad.z - bz * ad.y)
                  - bx * (ac.x * ad.z - ac.z * ad.x)
                  + bz * (ac.x * ad.y - ac.y * ad.x)) / det;

        float cz = (ab.x * (ac.y * bz - by * ac.z)
                  - ab.y * (ac.x * bz - bx * ac.z)
                  + bx * (ac.x * ad.y - ac.y * ad.x)) / det;

        center = a + new Vector3(cx, cy, cz);
        radius = Vector3.Distance(center, a);
        return true;
    }

    // ─────────────────────────────────────────
    // Critère de Delaunay 3D
    // Retourne true si le point p est DANS la sphère circonscrite du tétraèdre (a,b,c,d)
    // → il faut alors flipper
    // ─────────────────────────────────────────
    public static bool ViolatesDelaunay(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 p)
    {
        if (!CircumsphereTetrahedron(a, b, c, d, out Vector3 center, out float radius))
            return false;
        return Vector3.Distance(center, p) < radius - 1e-6f;
    }
}
