using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DelaunayAdjacency3D
{
    public class TetAdjacency
    {
        public int tetraIndex;
        public List<int> neighbors = new List<int>();
    }

    public static List<TetAdjacency> BuildAdjacency(List<IncrementalDelaunay3D.Tetra> tets)
    {
        var result = new List<TetAdjacency>();

        for (int i = 0; i < tets.Count; i++)
            result.Add(new TetAdjacency { tetraIndex = i });

        var faceMap = IncrementalDelaunay3D.FaceAdjacency(tets);

        foreach (var kv in faceMap)
        {
            if (kv.Value.Count == 2)
            {
                int a = kv.Value[0];
                int b = kv.Value[1];

                if (!result[a].neighbors.Contains(b))
                    result[a].neighbors.Add(b);

                if (!result[b].neighbors.Contains(a))
                    result[b].neighbors.Add(a);
            }
        }

        return result;
    }

    public static List<IncrementalDelaunay3D.Face> BoundaryFaces(List<IncrementalDelaunay3D.Tetra> tets)
    {
        var faces = new List<IncrementalDelaunay3D.Face>();
        var map = IncrementalDelaunay3D.FaceAdjacency(tets);

        foreach (var kv in map)
        {
            if (kv.Value.Count == 1)
                faces.Add(kv.Key);
        }

        return faces;
    }
}