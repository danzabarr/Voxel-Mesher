using System.Collections.Generic;
using UnityEngine;

public class VoxelMap : Dictionary<Vector3Int, int>
{
    public VoxelMap(IEnumerable<Voxel> data, VoxelData.Filter filter = null)
    {
        if (data == null)
            throw new System.ArgumentNullException("data");

        foreach (var v in data)
            if (filter == null || filter(new Vector3Int(v.x, v.y, v.z), v.i))
                this[new Vector3Int(v.x, v.y, v.z)] = v.i;
    }
}
