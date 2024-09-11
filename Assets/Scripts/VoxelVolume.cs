using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelVolume : MonoBehaviour
{
    [SerializeField] private Vector3 anchor;
    [SerializeField] private Vector3 position;
    [SerializeField] private Vector3 rotation;
    [SerializeField] private Vector3 scale = Vector3.one;
    [SerializeField] private VoxelData data;
    private Voxel.Map map;

    public int Version => data.version;
    public Vector3Int Size => data.size;
    public Bounds Bounds => new Bounds((Vector3)Size * 0.5f, Size);
    public IEnumerable<Voxel> Voxels => data.voxels;
    public Voxel.Map Map => map == null ? map = new Voxel.Map(data.voxels) : map;
    public Vector3 Anchor => anchor;
    public Vector3 Position => position;
    public Vector3 Rotation => rotation;
    public Vector3 Scale => scale;
    public Matrix4x4 Transform => Matrix4x4.TRS(Position, Quaternion.Euler(Rotation), Scale) * Matrix4x4.Translate(new Vector3(-Anchor.x * Size.x, -Anchor.y * Size.y, -Anchor.z * Size.z));
    
    public void SetOffsets(Vector3 anchor, Vector3 position, Vector3 rotation, Vector3 scale)
    {
        this.anchor = anchor;
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
    }

    public void SetData(VoxelData data)
    {
        this.data = data;
        map = new Voxel.Map(data.voxels);
    }

    public Color GetPaletteColor(int index) 
    {
        if (index < 0 || index >= data.palette.Length)
            return Color.clear;
        return data.palette[index];
    }

    public bool TryGetVoxel(Vector3Int pos, out int color)
    {
        return Map.TryGetValue(pos, out color);
    }

    public void Traverse(Ray ray, Traversal traversal)
    {
        if (data == null)
            return;

        Matrix4x4 inverse = (transform.localToWorldMatrix * Transform).inverse;
        ray.origin = inverse.MultiplyPoint(ray.origin);
        ray.direction = inverse.MultiplyVector(ray.direction);        

        if (!Bounds.IntersectRay(ray))
            return;

        Traverse(ray, Map, data.size, traversal);
    }

    public delegate bool Traversal(Vector3Int pos, int index, Vector3Int normal, float distance, int steps);

    public static void Traverse(Ray ray, Dictionary<Vector3Int, int> voxels, Vector3Int size, Traversal traversal)
    {
        Vector3 voxelSize = new Vector3(1, 1, 1);
        Vector3 voxelOffset = new Vector3(0, 0, 0);
        Vector3 origin = ray.origin;    
        Vector3 direction = ray.direction;

        Vector3 currentPosition = origin;
        Vector3Int currentVoxel = new Vector3Int
        (
            Mathf.FloorToInt((currentPosition.x - voxelOffset.x) / voxelSize.x),
            Mathf.FloorToInt((currentPosition.y - voxelOffset.y) / voxelSize.y),
            Mathf.FloorToInt((currentPosition.z - voxelOffset.z) / voxelSize.z)
        );

        // Calculate the step direction
        Vector3Int step = new Vector3Int
        (
            direction.x > 0 ? 1 : -1,
            direction.y > 0 ? 1 : -1,
            direction.z > 0 ? 1 : -1
        );

        // Calculate tMax and tDelta
        Vector3 tMax = new Vector3
        (
            ((step.x > 0 ? (currentVoxel.x + 1) : currentVoxel.x) * voxelSize.x + voxelOffset.x - currentPosition.x) / direction.x,
            ((step.y > 0 ? (currentVoxel.y + 1) : currentVoxel.y) * voxelSize.y + voxelOffset.y - currentPosition.y) / direction.y,
            ((step.z > 0 ? (currentVoxel.z + 1) : currentVoxel.z) * voxelSize.z + voxelOffset.z - currentPosition.z) / direction.z
        );

        Vector3 tDelta = new Vector3
        (
            voxelSize.x / Mathf.Abs(direction.x),
            voxelSize.y / Mathf.Abs(direction.y),
            voxelSize.z / Mathf.Abs(direction.z)
        );

        int steps = 0;
        Vector3Int normal = new Vector3Int(0, 0, 0);

        bool insideBounds = false;

        while (true)
        {
            // Visit the voxel
            //Vector3 intersection = origin + direction * Mathf.Min(tMax.x, tMax.y, tMax.z);

            bool inside = currentVoxel.x >= 0 && currentVoxel.x < size.x &&
                            currentVoxel.y >= 0 && currentVoxel.y < size.y &&
                            currentVoxel.z >= 0 && currentVoxel.z < size.z;

            if (insideBounds && !inside)
                break;

            insideBounds = inside;

            int index = voxels.GetValueOrDefault(currentVoxel, -1);

            if (index > 0 && traversal.Invoke(currentVoxel, index, normal, Mathf.Min(tMax.x, tMax.y, tMax.z), steps))
                break;

            // Increment tMax and step to the next voxel
            if (tMax.x < tMax.y)
            {
                if (tMax.x < tMax.z)
                {
                    currentVoxel.x += step.x;
                    tMax.x += tDelta.x;
                    normal = new Vector3Int(-step.x, 0, 0);
                }
                else
                {
                    currentVoxel.z += step.z;
                    tMax.z += tDelta.z;
                    normal = new Vector3Int(0, 0, -step.z);
                }
            }
            else
            {
                if (tMax.y < tMax.z)
                {
                    currentVoxel.y += step.y;
                    tMax.y += tDelta.y;
                    normal = new Vector3Int(0, -step.y, 0);
                }
                else
                {
                    currentVoxel.z += step.z;
                    tMax.z += tDelta.z;
                    normal = new Vector3Int(0, 0, -step.z);
                }
            }

            steps++;
        }
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix * Transform;
        Gizmos.DrawWireCube(Bounds.center, Bounds.size);
        // Traverse(ray, (Vector3Int pos, int index, float distance, int steps) =>
        // {
        //     DrawCube(Transform, pos, Vector3Int.one, GetColor(index));
        //     return false;
        // });
    }
}
