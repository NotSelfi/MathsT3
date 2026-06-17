using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MaterialType3D
{
    Verre,
    Pierre,
    Bois
}

public class VoronoiDestructionComplete : MonoBehaviour
{
    public VoronoiVolumeCutter Cutter;

    public MaterialType3D MaterialType;

    public float GlassForce = 1500f;
    public float StoneForce = 350f;
    public float WoodForce = 700f;

    bool exploded;

    void Start()
    {
        if (Cutter != null)
            Cutter.Generate();
    }

    void Update()
    {
        if (exploded)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray =
                Camera.main.ScreenPointToRay(
                    Input.mousePosition
                );

            if (Physics.Raycast(
                ray,
                out RaycastHit hit))
            {
                Explode(hit.point);
            }
        }
    }

    public void Explode(Vector3 impact)
    {
        exploded = true;

        float force =
            GetForce();

        foreach (Transform child in transform)
        {
            MeshFilter mf =
                child.GetComponent<MeshFilter>();

            if (mf == null)
                continue;

            MeshCollider mc =
                child.gameObject.AddComponent<MeshCollider>();

            mc.sharedMesh =
                mf.sharedMesh;

            mc.convex = true;

            Rigidbody rb =
                child.gameObject.AddComponent<Rigidbody>();

            rb.mass =
                GetMass();

            rb.drag =
                GetDrag();

            Vector3 dir =
                child.position - impact;

            float dist =
                Mathf.Max(
                    dir.magnitude,
                    0.1f
                );

            float attenuation =
                WavePropagation(dist);

            rb.AddForce(
                dir.normalized *
                force *
                attenuation,
                ForceMode.Impulse
            );
        }
    }

    float WavePropagation(float d)
    {
        switch (MaterialType)
        {
            case MaterialType3D.Verre:
                return Mathf.Exp(-d * 0.15f);

            case MaterialType3D.Pierre:
                return Mathf.Exp(-d * 0.8f);

            case MaterialType3D.Bois:
                return Mathf.Exp(-d * 0.4f);
        }

        return 1f;
    }

    float GetForce()
    {
        switch (MaterialType)
        {
            case MaterialType3D.Verre:
                return GlassForce;

            case MaterialType3D.Pierre:
                return StoneForce;

            case MaterialType3D.Bois:
                return WoodForce;
        }

        return 500f;
    }

    float GetMass()
    {
        switch (MaterialType)
        {
            case MaterialType3D.Verre:
                return 0.1f;

            case MaterialType3D.Pierre:
                return 5f;

            case MaterialType3D.Bois:
                return 1f;
        }

        return 1f;
    }

    float GetDrag()
    {
        switch (MaterialType)
        {
            case MaterialType3D.Verre:
                return 0.05f;

            case MaterialType3D.Pierre:
                return 1.2f;

            case MaterialType3D.Bois:
                return 0.4f;
        }

        return 0.5f;
    }
}