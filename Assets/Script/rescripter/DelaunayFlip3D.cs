using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DelaunayFlip3D
{
    public static void Legalize(List<Vector3> points, List<IncrementalDelaunay3D.Tetra> tets)
    {
        bool changed = true;
        int safety = 0;

        while (changed && safety < 500)
        {
            changed = false;
            safety++;

            var adjacency = IncrementalDelaunay3D.FaceAdjacency(tets);

            foreach (var kv in adjacency)
            {
                if (kv.Value.Count != 2)
                    continue;

                int t0 = kv.Value[0];
                int t1 = kv.Value[1];

                if (t0 >= tets.Count || t1 >= tets.Count)
                    continue;

                if (IncrementalDelaunay3D.AdjacentPairViolatesDelaunay(tets[t0], tets[t1], points))
                {
                    if (Flip23(points, tets, t0, t1))
                    {
                        changed = true;
                        break;
                    }
                }
            }
        }
    }

    static bool Flip23(
        List<Vector3> points,
        List<IncrementalDelaunay3D.Tetra> tets,
        int idA,
        int idB)
    {
        var A = tets[idA];
        var B = tets[idB];

        List<int> shared = new List<int>();
        List<int> unique = new List<int>();

        int[] av = { A.a, A.b, A.c, A.d };
        int[] bv = { B.a, B.b, B.c, B.d };

        foreach (int x in av)
        {
            bool found = false;

            foreach (int y in bv)
            {
                if (x == y)
                {
                    found = true;
                    break;
                }
            }

            if (found) shared.Add(x);
            else unique.Add(x);
        }

        foreach (int y in bv)
        {
            bool found = false;

            foreach (int x in av)
            {
                if (x == y)
                {
                    found = true;
                    break;
                }
            }

            if (!found) unique.Add(y);
        }

        if (shared.Count != 3 || unique.Count != 2)
            return false;

        int s0 = shared[0];
        int s1 = shared[1];
        int s2 = shared[2];

        int u0 = unique[0];
        int u1 = unique[1];

        var n0 = MakeTet(points, u0, u1, s0, s1);
        var n1 = MakeTet(points, u0, u1, s1, s2);
        var n2 = MakeTet(points, u0, u1, s2, s0);

        if (!Valid(points, n0) || !Valid(points, n1) || !Valid(points, n2))
            return false;

        if (idA > idB)
        {
            int tmp = idA;
            idA = idB;
            idB = tmp;
        }

        tets.RemoveAt(idB);
        tets.RemoveAt(idA);

        tets.Add(n0);
        tets.Add(n1);
        tets.Add(n2);

        return true;
    }

    static IncrementalDelaunay3D.Tetra MakeTet(
        List<Vector3> pts,
        int a,
        int b,
        int c,
        int d)
    {
        if (V3DGeometry.SignedTetraVolume6(pts[a], pts[b], pts[c], pts[d]) < 0f)
            return new IncrementalDelaunay3D.Tetra(a, c, b, d, pts);

        return new IncrementalDelaunay3D.Tetra(a, b, c, d, pts);
    }

    static bool Valid(List<Vector3> pts, IncrementalDelaunay3D.Tetra t)
    {
        float v = Mathf.Abs(V3DGeometry.SignedTetraVolume6(
            pts[t.a],
            pts[t.b],
            pts[t.c],
            pts[t.d]
        ));

        return v > V3DGeometry.EPS;
    }
}