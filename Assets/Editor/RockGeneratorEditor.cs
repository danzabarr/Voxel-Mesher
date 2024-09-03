using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(RockGenerator))]
public class RockGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
    }

    private void OnSceneGUI()
    {
        RockGenerator rockGenerator = target as RockGenerator;
        if (rockGenerator == null)
            return;

        if (rockGenerator.mesh == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            rockGenerator.mesh = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
        }

        if (rockGenerator.planes == null)
            return;

        Gizmos.color = Color.red;
        foreach (SerializablePlane plane in rockGenerator.planes)
        {
            Vector3 normal = plane.normal;
            float distance = plane.distance;
            Vector3 point = normal * distance;

            point = rockGenerator.transform.TransformPoint(point);

            Vector3 newPoint = rockGenerator.transform.InverseTransformPoint(Handles.PositionHandle(point, Quaternion.identity));

            if (plane.point != newPoint)
            {
                plane.point = newPoint;
                rockGenerator.IdentifyFaces();
            }
        }
    }

}
