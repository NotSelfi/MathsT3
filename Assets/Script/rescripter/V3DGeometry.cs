using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class V3DGeometry
{
    public const float EPS = 1e-5f;

    public struct Plane3
    {
        public Vector3 point;
        public Vector3 normal;

        public Plane3(Vector3 point, Vector3 normal)
        {
            this.point = point;
            this.normal = normal.normalized;
        }

        public float SignedDistance(Vector3 p)
        {
            return Vector3.Dot(normal, p - point);
        }
    }

    public static Plane3 MedianPlane(Vector3 a, Vector3 b)
    {
        return new Plane3((a + b) * 0.5f, b - a);
    }

    public static float SignedTetraVolume6(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        return Vector3.Dot(Vector3.Cross(b - a, c - a), d - a);
    }

    public static bool SegmentPlane(Vector3 a, Vector3 b, Plane3 plane, out Vector3 hit)
    {
        float da = plane.SignedDistance(a);
        float db = plane.SignedDistance(b);
        hit = Vector3.zero;

        float den = da - db;
        if (Mathf.Abs(den) < EPS) return false;

        float t = da / den;
        if (t < -EPS || t > 1f + EPS) return false;

        hit = a + Mathf.Clamp01(t) * (b - a);
        return true;
    }

    public static int PlaneTriangle(Plane3 plane, Vector3 a, Vector3 b, Vector3 c, out Vector3 h1, out Vector3 h2)
    {
        h1 = h2 = Vector3.zero;
        List<Vector3> hits = new List<Vector3>();

        AddTriangleHit(a, b, plane, hits);
        AddTriangleHit(b, c, plane, hits);
        AddTriangleHit(c, a, plane, hits);

        hits = Unique(hits, EPS);

        if (hits.Count > 0) h1 = hits[0];
        if (hits.Count > 1) h2 = hits[1];

        return Mathf.Min(hits.Count, 2);
    }

    static void AddTriangleHit(Vector3 a, Vector3 b, Plane3 plane, List<Vector3> hits)
    {
        float da = plane.SignedDistance(a);
        float db = plane.SignedDistance(b);

        if (Mathf.Abs(da) < EPS) hits.Add(a);

        if (da * db < -EPS && SegmentPlane(a, b, plane, out Vector3 h))
            hits.Add(h);
    }

    public static bool Circumsphere(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 center, out float r2)
    {
        center = Vector3.zero;
        r2 = 0f;

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
                if (Mathf.Abs(m[row, col]) > Mathf.Abs(m[pivot, col]))
                    pivot = row;
            }

            if (Mathf.Abs(m[pivot, col]) < 1e-8f)
                return false;

            if (pivot != col)
            {
                for (int k = col; k < 4; k++)
                {
                    float tmp = m[col, k];
                    m[col, k] = m[pivot, k];
                    m[pivot, k] = tmp;
                }
            }

            float div = m[col, col];

            for (int k = col; k < 4; k++)
                m[col, k] /= div;

            for (int row = 0; row < 3; row++)
            {
                if (row == col) continue;

                float f = m[row, col];

                for (int k = col; k < 4; k++)
                    m[row, k] -= f * m[col, k];
            }
        }

        center = new Vector3(m[0, 3], m[1, 3], m[2, 3]);
        r2 = (center - a).sqrMagnitude;
        return true;
    }

    public static bool ViolatesDelaunay(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 p)
    {
        if (!Circumsphere(a, b, c, d, out Vector3 cc, out float r2))
            return false;

        return (p - cc).sqrMagnitude < r2 - EPS;
    }

    public static List<Vector3> Unique(List<Vector3> pts, float eps)
    {
        List<Vector3> res = new List<Vector3>();
        float e2 = eps * eps;

        foreach (Vector3 p in pts)
        {
            bool found = false;

            foreach (Vector3 q in res)
            {
                if ((p - q).sqrMagnitude <= e2)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                res.Add(p);
        }

        return res;
    }

    public static void SortCoplanar(List<Vector3> pts, Vector3 normal)
    {
        if (pts.Count < 3) return;

        Vector3 center = Vector3.zero;

        foreach (Vector3 p in pts)
            center += p;

        center /= pts.Count;

        Vector3 axisA = Vector3.Cross(normal, Vector3.up);

        if (axisA.sqrMagnitude < EPS)
            axisA = Vector3.Cross(normal, Vector3.right);

        axisA.Normalize();

        Vector3 axisB = Vector3.Cross(normal, axisA).normalized;

        pts.Sort((u, v) =>
            Mathf.Atan2(Vector3.Dot(u - center, axisB), Vector3.Dot(u - center, axisA))
            .CompareTo(
                Mathf.Atan2(Vector3.Dot(v - center, axisB), Vector3.Dot(v - center, axisA))
            )
        );
    }
}