using System.Collections;
using UnityEngine;

public class ClickExplodeVoronoi : MonoBehaviour
{
    public FullVoronoiProjectController Controller;
    public float Force = 20f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Controller.Explode(hit.point, Force);
            }
        }
    }
}