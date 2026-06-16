using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConvexHull3D
{
    public struct Face
    {
        public int a;
        public int b;
        public int c;
        public Vector3 normal;

        public Face(int a, int b, int c, Vector3 normal)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.normal = normal;
        }
    }

    public static List<Face> ComputeHull(List<Vector3> points)
    {
        List<Face> faces = new List<Face>();

        if (points.Count < 4)
        {
            return faces;
        }

        int n = points.Count;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                for (int k = j + 1; k < n; k++)
                {
                    Vector3 a = points[i];
                    Vector3 b = points[j];
                    Vector3 c = points[k];

                    Vector3 normal = Vector3.Cross(b - a, c - a);

                    if (normal.magnitude < 0.0001f)
                    {
                        continue;
                    }

                    int positive = 0;
                    int negative = 0;

                    for (int p = 0; p < n; p++)
                    {
                        if (p == i || p == j || p == k)
                        {
                            continue;
                        }

                        float side = Vector3.Dot(normal, points[p] - a);

                        if (side > 0.0001f)
                        {
                            positive++;
                        }
                        else if (side < -0.0001f)
                        {
                            negative++;
                        }
                    }

                    if (positive == 0 || negative == 0)
                    {
                        if (positive == 0)
                        {
                            normal = -normal;
                            faces.Add(new Face(i, k, j, normal.normalized));
                        }
                        else
                        {
                            faces.Add(new Face(i, j, k, normal.normalized));
                        }
                    }
                }
            }
        }

        return faces;
    }

    public static Mesh BuildMesh(List<Vector3> points, List<Face> faces)
    {
        Mesh mesh = new Mesh();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();

        foreach (Face face in faces)
        {
            int index = vertices.Count;

            vertices.Add(points[face.a]);
            vertices.Add(points[face.b]);
            vertices.Add(points[face.c]);

            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);

            normals.Add(face.normal);
            normals.Add(face.normal);
            normals.Add(face.normal);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetNormals(normals);

        mesh.RecalculateBounds();

        return mesh;
    }
}