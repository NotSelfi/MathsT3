using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImpactPolylineSampler : MonoBehaviour
{
    public int PointCount = 12;
    public float Length = 3f;
    public float Noise = 0.2f;

    public List<Vector3> GenerateImpactLine(Vector3 start, Vector3 direction)
    {
        List<Vector3> points = new List<Vector3>();

        direction.Normalize();

        Vector3 side = Vector3.Cross(direction, Vector3.up);

        if (side.sqrMagnitude < 0.001f)
            side = Vector3.Cross(direction, Vector3.right);

        side.Normalize();

        for (int i = 0; i < PointCount; i++)
        {
            float t = i / (float)(PointCount - 1);

            Vector3 p =
                start +
                direction * Length * t +
                side * Random.Range(-Noise, Noise);

            points.Add(p);
        }

        return points;
    }
}
