using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoronoiPolyhedron
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<List<int>> faces = new List<List<int>>();

    public static VoronoiPolyhedron FromBounds(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;

        VoronoiPolyhedron p = new VoronoiPolyhedron();

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

    public VoronoiPolyhedron Clip(V3DGeometry.Plane3 plane, bool keepNegativeSide)
    {
        VoronoiPolyhedron result = new VoronoiPolyhedron();
        List<Vector3> cap = new List<Vector3>();

        foreach (List<int> face in faces)
        {
            List<Vector3> poly = new List<Vector3>();

            foreach (int id in face)
                poly.Add(vertices[id]);

            List<Vector3> clipped = ClipPolygon(poly, plane, keepNegativeSide, cap);

            if (clipped.Count >= 3)
            {
                List<int> newFace = new List<int>();

                foreach (Vector3 v in clipped)
                    newFace.Add(AddUnique(result.vertices, v));

                result.faces.Add(newFace);
            }
        }

        cap = V3DGeometry.Unique(cap, V3DGeometry.EPS);

        if (cap.Count >= 3)
        {
            V3DGeometry.SortCoplanar(cap, plane.normal);

            List<int> capFace = new List<int>();

            foreach (Vector3 v in cap)
                capFace.Add(AddUnique(result.vertices, v));

            result.faces.Add(capFace);
        }

        return result.vertices.Count >= 4 && result.faces.Count >= 4 ? result : null;
    }

    static List<Vector3> ClipPolygon(
        List<Vector3> input,
        V3DGeometry.Plane3 plane,
        bool keepNeg,
        List<Vector3> cap
    )
    {
        List<Vector3> output = new List<Vector3>();

        if (input.Count == 0)
            return output;

        for (int i = 0; i < input.Count; i++)
        {
            Vector3 a = input[i];
            Vector3 b = input[(i + 1) % input.Count];

            float da = plane.SignedDistance(a);
            float db = plane.SignedDistance(b);

            bool ina = keepNeg ? da <= V3DGeometry.EPS : da >= -V3DGeometry.EPS;
            bool inb = keepNeg ? db <= V3DGeometry.EPS : db >= -V3DGeometry.EPS;

            if (ina && inb)
            {
                output.Add(b);
            }
            else if (ina && !inb)
            {
                if (V3DGeometry.SegmentPlane(a, b, plane, out Vector3 h))
                {
                    output.Add(h);
                    cap.Add(h);
                }
            }
            else if (!ina && inb)
            {
                if (V3DGeometry.SegmentPlane(a, b, plane, out Vector3 h))
                {
                    output.Add(h);
                    cap.Add(h);
                }

                output.Add(b);
            }
        }

        return output;
    }

    static int AddUnique(List<Vector3> list, Vector3 v)
    {
        float e2 = V3DGeometry.EPS * V3DGeometry.EPS;

        for (int i = 0; i < list.Count; i++)
        {
            if ((list[i] - v).sqrMagnitude <= e2)
                return i;
        }

        list.Add(v);
        return list.Count - 1;
    }

    public Mesh ToMesh()
    {
        Mesh mesh = new Mesh();
        List<int> tris = new List<int>();

        for (int f = 0; f < faces.Count; f++)
        {
            List<int> face = faces[f];

            for (int i = 1; i < face.Count - 1; i++)
            {
                tris.Add(face[0]);
                tris.Add(face[i]);
                tris.Add(face[i + 1]);
            }
        }

        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}