using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(VoxelVolume))]
public class VoxelFragmenter : MonoBehaviour
{
    [ContextMenu("Construct Fragments")]
    public void ConstructFragments()
    {
        VoxelVolume volume = GetComponent<VoxelVolume>();
        Vector3Int Size = volume.Size;
        Matrix4x4 Transform = volume.Transform;

        List<Vector3> points = DistributePoints(500, Vector3.zero, Size);
        List<Voxel>[] cells = KMeansClustering(volume.Voxels, points);
        //Voronoi(data.voxels, points);

        for (int i = 0; i < cells.Length; i++)
        {
            List<Voxel> fragment = cells[i];
            if (fragment == null || fragment.Count == 0)
                continue;

            Mesh mesh = Voxel.Mesh(fragment, Transform);

            GameObject gameObject = new GameObject(transform.name + "_mesh_" + i);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
            
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;

            foreach (Voxel voxel in fragment)
            {
                BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                Vector3 center = new Vector3(voxel.x, voxel.y, voxel.z) + Vector3.one * 0.5f;
                center = Transform.MultiplyPoint(center);
                Vector3 size = Vector3.one;
                size = Transform.MultiplyVector(size);
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                collider.center = center;
                collider.size = size;
            }

            gameObject.transform.SetParent(transform);
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
        }   
    }

    public List<Vector3> DistributePoints(int numPoints, Vector3 min, Vector3 size)
    {
        // minimise the sum of the distances between each point and its nearest point
        
        Random.InitState(0);
        List<Vector3> points = new List<Vector3>(numPoints);
        for (int i = 0; i < numPoints; i++)
            points.Add(new Vector3
            (
                Random.value * size.x + min.x,
                Random.value * size.y + min.y,
                Random.value * size.z + min.z
            ));


        return points;
    }

    public static List<Voxel>[] KMeansClustering(IEnumerable<Voxel> voxels, List<Vector3> points)
    {
        List<Voxel>[] CreateClusters()
        {
        List<Voxel>[] clusters = new List<Voxel>[points.Count];
        for (int i = 0; i < points.Count; i++)
            clusters[i] = new List<Voxel>();

        foreach (var voxel in voxels)
        {
            float minDistance = float.MaxValue;
            int minIndex = -1;

            Vector3Int position = new Vector3Int(voxel.x, voxel.y, voxel.z); 
            for (int j = 0; j < points.Count; j++)
            {
                Vector3 point = points[j];
                float distance = Vector3.Distance(point, position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    minIndex = j;
                }
            }

            clusters[minIndex].Add(voxel);
        }

        return clusters;
        }

        List<Vector3> UpdatePoints(List<Voxel>[] clusters)
        {
        List<Vector3> newPoints = new List<Vector3>(points.Count);
        for (int i = 0; i < clusters.Length; i++)
        {
            List<Voxel> cluster = clusters[i];
            if (cluster.Count == 0)
                newPoints.Add(points[i]);
            else
            {
                Vector3 sum = Vector3.zero;
                foreach (Voxel voxel in cluster)
                    sum += new Vector3(voxel.x, voxel.y, voxel.z);

                newPoints.Add(sum / cluster.Count);
            }
        }

        return newPoints;
        }

            List<Voxel>[] clusters = CreateClusters();
            points = UpdatePoints(clusters);
            for (int i = 0; i < 100; i++)
            {
                clusters = CreateClusters();
                points = UpdatePoints(clusters);
            }

            return clusters;
    }

    public static List<Voxel>[] Voronoi(List<Voxel> voxels, List<Vector3> points)
    {
        List<Voxel>[] cells = new List<Voxel>[points.Count];
        for (int i = 0; i < points.Count; i++)
            cells[i] = new List<Voxel>();

        foreach (var voxel in voxels)
        {
            float minDistance = float.MaxValue;
            int minIndex = -1;

            Vector3Int position = new Vector3Int(voxel.x, voxel.y, voxel.z); 
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                float distance = Vector3.Distance(point, position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    minIndex = i;
                }
            }

            cells[minIndex].Add(voxel);
        }

        return cells;
    }
    
}
