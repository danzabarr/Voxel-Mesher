using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class SerializablePlane
{
    public Vector3 point;

    public Vector3 normal => point.normalized;
    public float distance => point.magnitude;
    public List<Vector3> vertices = new List<Vector3>();

    public SerializablePlane(Plane plane)
    {
        point = plane.normal * plane.distance;
    }

    public Plane ToPlane()
    {
        return new Plane(point.normalized, point.magnitude);
    }

    public static implicit operator Plane(SerializablePlane sp)
    {
        return sp.ToPlane();
    }
}

public class RockGenerator : MonoBehaviour
{
    public Mesh mesh;
    public SerializablePlane[] planes;

    public static bool Intersects(Plane a, Plane b, Plane c, out Vector3 intersection)
    {
        Vector3 n1 = a.normal;
        Vector3 n2 = b.normal;
        Vector3 n3 = c.normal;

        float denom = Vector3.Dot(Vector3.Cross(n1, n2), n3);

        if (Mathf.Approximately(denom, 0))
        {
            intersection = Vector3.zero;
            return false;
        }

        float d1 = a.distance;
        float d2 = b.distance;
        float d3 = c.distance;

        intersection = (Vector3.Cross(Vector3.Cross(n2, n3), n1) * d1 +
                        Vector3.Cross(Vector3.Cross(n3, n1), n2) * d2 +
                        Vector3.Cross(Vector3.Cross(n1, n2), n3) * d3) / denom;

        return true;
    }

    public void OnValidate()
    {
        IdentifyFaces();
    }

    public void IdentifyFaces()
    {
        foreach (SerializablePlane plane in planes)
            plane.vertices.Clear();

        foreach (SerializablePlane a in planes)
        foreach (SerializablePlane b in planes)
        foreach (SerializablePlane c in planes)
        {
            if (a == b || b == c || a == c)
                continue;

            Vector3 intersection;
            if (Intersects(a, b, c, out intersection))
            {
                a.vertices.Add(intersection);
                b.vertices.Add(intersection);
                c.vertices.Add(intersection);
            }
        }
    }

    public void OnDrawGizmos()
    {
        if (mesh == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            mesh = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
        }

        if (planes == null)
            return;

        Gizmos.color = Color.red;
        foreach (SerializablePlane plane in planes)
        {
            Vector3 normal = plane.normal;
            float distance = plane.distance;
            Vector3 point = normal * distance;

            Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, normal).normalized;

            //point = transform.TransformPoint(point);

            Gizmos.DrawLine(transform.position, transform.position + normal * distance);
            Gizmos.DrawLine(transform.position + normal * distance, transform.position + normal * distance + right);
            Gizmos.DrawLine(transform.position + normal * distance, transform.position + normal * distance + up);

            foreach (Vector3 vertex in plane.vertices)
            {
                Gizmos.DrawSphere(transform.TransformPoint(vertex), 0.5f);
            }
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawMesh(mesh, transform.position + normal * distance, Quaternion.LookRotation(up, normal), Vector3.one * 1f);
        }
    }
}
