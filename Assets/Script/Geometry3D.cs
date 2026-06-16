using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Geometry3D
{
    public static Vector3 Cross(Vector3 a, Vector3 b)
    {
        return Vector3.Cross(a, b);
    }

    public static float Dot(Vector3 a, Vector3 b)
    {
        return Vector3.Dot(a, b);
    }

    public static Vector3 Normal(Vector3 a, Vector3 b, Vector3 c)
    {
        return Vector3.Cross(b - a, c - a).normalized;
    }

    public static float SignedVolume(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        return Vector3.Dot(Vector3.Cross(b - a, c - a), d - a);
    }

    public static bool IsVisibleFrom(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
    {
        return SignedVolume(a, b, c, p) > 0f;
    }
}