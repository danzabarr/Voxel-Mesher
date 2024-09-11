using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

[ExecuteAlways]
public class VoxelRasteriser : MonoBehaviour
{
    public Vector3Int size = Vector3Int.one;
    public LayerMask layerMask = -1;
    public QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal;
    public byte color = 1;
    public string path = "Assets/Models/voxel_rasteriser_out.vox";
    public List<Voxel> voxels = new List<Voxel>();
   
    [ContextMenu("Capture")]
    public void Capture()
    {
        voxels = Rasterise(size, transform.localToWorldMatrix, layerMask, queryTriggerInteraction, color);
    }
    public static List<Voxel> Rasterise(Vector3Int size, Matrix4x4 transform, LayerMask layerMask, QueryTriggerInteraction queryTriggerInteraction, byte color)
    {
        List<Voxel> voxels = new List<Voxel>();

        for (byte x = 0; x < size.x; x++)
        {
            for (byte y = 0; y < size.y; y++)
            {
                for (byte z = 0; z < size.z; z++)
                {
                    // get the world position of the voxel
                    Vector3 position = new Vector3(x, y, z) + Vector3.one * 0.5f;
                    Vector3 halfExtents = Vector3.one * 0.5f;
                    position = transform.MultiplyPoint(position);
                    halfExtents = transform.MultiplyVector(halfExtents);
                    Quaternion orientation = transform.rotation;
                    
                    Collider[] colliders = Physics.OverlapBox(position, halfExtents, orientation, layerMask, queryTriggerInteraction);

                    if (colliders.Length > 0)
                        voxels.Add(new Voxel(x, y, z, color));
                }
            }
        }

        return voxels;
        
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        voxels.Clear();
    }

    [ContextMenu("Export to VOX")]
    public void ExportToVox()
    {
        VoxelData.Write(path, 150, size, voxels, null);
    }

    public void OnDrawGizmos()
    {
        if (voxels == null)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube((Vector3)size * 0.5f, size);

        foreach (Voxel voxel in voxels)
        {
            Vector3 position = new Vector3(voxel.x, voxel.y, voxel.z) + Vector3.one * 0.5f;
            Gizmos.DrawWireCube(position, Vector3.one);
        }
    }

    public void Update()
    {
        Capture();
    }
}
