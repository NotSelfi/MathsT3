using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MeshVoronoiClipper
{
    public static VoronoiPolyhedron ClipCellByMesh(
        VoronoiPolyhedron cell,
        Mesh mesh,
        Transform meshTransform)
    {
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = meshTransform.TransformPoint(verts[tris[i]]);
            Vector3 b = meshTransform.TransformPoint(verts[tris[i + 1]]);
            Vector3 c = meshTransform.TransformPoint(verts[tris[i + 2]]);

            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            V3DGeometry.Plane3 plane = new V3DGeometry.Plane3(a, normal);

            cell = cell.Clip(plane, true);

            if (cell == null)
                return null;
        }

        return cell;
    }

    public static List<VoronoiPolyhedron> ClipCellsByMesh(
        List<VoronoiPolyhedron> cells,
        Mesh mesh,
        Transform meshTransform)
    {
        List<VoronoiPolyhedron> result = new List<VoronoiPolyhedron>();

        foreach (VoronoiPolyhedron c in cells)
        {
            VoronoiPolyhedron clipped = ClipCellByMesh(c, mesh, meshTransform);

            if (clipped != null)
                result.Add(clipped);
        }

        return result;
    }
}