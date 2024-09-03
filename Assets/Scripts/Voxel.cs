using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public struct Voxel
{
    public byte x;
    public byte y;
    public byte z;
    public byte i;

    public Voxel(byte x, byte y, byte z, byte i)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.i = i;
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

    public static Mesh Mesh(Dictionary<Vector3Int, int> voxels, Matrix4x4 transform, IndexFormat indexFormat = IndexFormat.UInt32)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        bool flipNormals = Vector3.Dot
        (
            new Vector3(transform.m00, transform.m10, transform.m20), 
            Vector3.Cross
            (
                new Vector3(transform.m01, transform.m11, transform.m21), 
                new Vector3(transform.m02, transform.m12, transform.m22)
            )
        ) < 0;

        Dictionary<int, Dictionary<Vector2Int, int>> northFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> southFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> eastFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> westFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> upFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> downFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();

        Vector2Int Min(Dictionary<Vector2Int, int> slice)
        {
            Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
            foreach (Vector2Int key in slice.Keys)
                if (key.y < min.y || (key.y == min.y && key.x < min.x))
                    min = key;
            return min;
        }

        Vector2Int Expand(Dictionary<Vector2Int, int> faces, Vector2Int min, int colorIndex)
        {
            Vector2Int size = new Vector2Int(1, 1);
            faces.Remove(min);  // Remove as we expand

            // Expand width (greedy in x direction)
            for (; faces.TryGetValue(new Vector2Int(min.x + size.x, min.y), out int nextColor) && nextColor == colorIndex; size.x++)
                faces.Remove(new Vector2Int(min.x + size.x, min.y));  // Remove as we expand

            // Expand height (greedy in y direction)
            bool heightExpansionPossible = true;
            while (heightExpansionPossible)
            {
                for (int x = min.x; x < min.x + size.x; x++)
                {
                    if (!faces.TryGetValue(new Vector2Int(x, min.y + size.y), out int nextColorInRow) || nextColorInRow != colorIndex)
                    {
                        heightExpansionPossible = false;
                        break;
                    }
                }

                if (heightExpansionPossible)
                {
                    for (int x = min.x; x < min.x + size.x; x++)
                        faces.Remove(new Vector2Int(x, min.y + size.y));  // Remove as we expand

                    size.y++;
                }
            }

            return size;
        }


        void AddFace(Vector3Int p, Vector2Int size, Vector3Int nrml, Vector3Int right, Vector3Int up, int colorIndex)
        {
            int index = vertices.Count;

            vertices.Add(transform.MultiplyPoint(p));                           // Bottom left
            vertices.Add(transform.MultiplyPoint(p + right * size.x));          // Bottom right
            vertices.Add(transform.MultiplyPoint(p + right * size.x + up * size.y)); // Top right
            vertices.Add(transform.MultiplyPoint(p + up * size.y));             // Top left

            // we need to flip the triangles if we have an odd number of negative scalars
            // otherwise the mesh will be inside out
            if (flipNormals)
            {
                triangles.Add(index + 0);
                triangles.Add(index + 2);
                triangles.Add(index + 1);

                triangles.Add(index + 0);
                triangles.Add(index + 3);
                triangles.Add(index + 2);
            }
            else
            {
                triangles.Add(index + 0);
                triangles.Add(index + 1);
                triangles.Add(index + 2);

                triangles.Add(index + 0);
                triangles.Add(index + 2);
                triangles.Add(index + 3);
            }

            Vector3 normal = transform.MultiplyVector(nrml);
            for (int i = 0; i < 4; i++)
                normals.Add(normal);

            uvs.Add(new Vector2((colorIndex + 0) / 256.0f, 0));
            uvs.Add(new Vector2((colorIndex + 1) / 256.0f, 0));
            uvs.Add(new Vector2((colorIndex + 1) / 256.0f, 1));
            uvs.Add(new Vector2((colorIndex + 0) / 256.0f, 1));
        }

        foreach (KeyValuePair<Vector3Int, int> voxel in voxels)
        {
            Vector3Int p = voxel.Key;
            int colorIndex = voxel.Value;

            Vector3Int north = p + Vector3Int.forward;
            Vector3Int south = p + Vector3Int.back;
            Vector3Int east = p + Vector3Int.right;
            Vector3Int west = p + Vector3Int.left;
            Vector3Int up = p + Vector3Int.up;
            Vector3Int down = p + Vector3Int.down;

            if (!voxels.TryGetValue(north, out int northColor) || northColor <= 0)
            {
                if (!northFaces.ContainsKey(p.z))
                    northFaces[p.z] = new Dictionary<Vector2Int, int>();

                northFaces[p.z][new Vector2Int(p.x, p.y)] = colorIndex;
            }

            if (!voxels.TryGetValue(south, out int southColor) || southColor <= 0)
            {
                if (!southFaces.ContainsKey(p.z))
                    southFaces[p.z] = new Dictionary<Vector2Int, int>();

                southFaces[p.z][new Vector2Int(p.x, p.y)] = colorIndex;
            }

            if (!voxels.TryGetValue(east, out int eastColor) || eastColor <= 0)
            {
                if (!eastFaces.ContainsKey(p.x))
                    eastFaces[p.x] = new Dictionary<Vector2Int, int>();

                eastFaces[p.x][new Vector2Int(p.z, p.y)] = colorIndex;
            }

            if (!voxels.TryGetValue(west, out int westColor) || westColor <= 0)
            {
                if (!westFaces.ContainsKey(p.x))
                    westFaces[p.x] = new Dictionary<Vector2Int, int>();

                westFaces[p.x][new Vector2Int(p.z, p.y)] = colorIndex;
            }

            if (!voxels.TryGetValue(up, out int upColor) || upColor <= 0)
            {
                if (!upFaces.ContainsKey(p.y))
                    upFaces[p.y] = new Dictionary<Vector2Int, int>();

                upFaces[p.y][new Vector2Int(p.x, p.z)] = colorIndex;
            }

            if (!voxels.TryGetValue(down, out int downColor) || downColor <= 0)
            {
                if (!downFaces.ContainsKey(p.y))
                    downFaces[p.y] = new Dictionary<Vector2Int, int>();

                downFaces[p.y][new Vector2Int(p.x, p.z)] = colorIndex;
            }
        }

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in northFaces)
        {
            int fallout = 1000;
            Dictionary<Vector2Int, int> faces = slice.Value;
            while (faces.Count > 0 && fallout-- > 0)
            {
                Vector2Int min = Min(faces);
                int colorIndex = faces[min];

                Vector2Int size = Expand(faces, min, colorIndex);
                Vector3Int p = new Vector3Int(min.x, min.y, slice.Key) + Vector3Int.forward;

                AddFace(p, size, Vector3Int.forward, Vector3Int.right, Vector3Int.up, colorIndex);
            }
        }

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in southFaces)
        {
            int fallout = 1000;
            Dictionary<Vector2Int, int> faces = slice.Value;
            while (faces.Count > 0 && fallout-- > 0)
            {
                Vector2Int min = Min(faces);
                int colorIndex = faces[min];

                Vector2Int size = Expand(faces, min, colorIndex);
                Vector3Int p = new Vector3Int(min.x, min.y, slice.Key) + Vector3Int.right * size.x;

                AddFace(p, size, Vector3Int.back, Vector3Int.left, Vector3Int.up, colorIndex);
            }
        }

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in westFaces)
        {
            int fallout = 1000;
            Dictionary<Vector2Int, int> faces = slice.Value;
            while (faces.Count > 0 && fallout-- > 0)
            {
                Vector2Int min = Min(faces);
                int colorIndex = faces[min];

                Vector2Int size = Expand(faces, min, colorIndex);
                Vector3Int p = new Vector3Int(slice.Key, min.y, min.x);

                AddFace(p, size, Vector3Int.left, Vector3Int.forward, Vector3Int.up, colorIndex);
            }
        }

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in eastFaces)
        {
            int fallout = 1000;
            Dictionary<Vector2Int, int> faces = slice.Value;
            while (faces.Count > 0 && fallout-- > 0)
            {
                Vector2Int min = Min(faces);
                int colorIndex = faces[min];

                Vector2Int size = Expand(faces, min, colorIndex);
                Vector3Int p = new Vector3Int(slice.Key, min.y, min.x) + Vector3Int.right + Vector3Int.forward * size.x;

                AddFace(p, size, Vector3Int.right, Vector3Int.back, Vector3Int.up, colorIndex);
            }
        }

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in upFaces)
        {
            int fallout = 1000;
            Dictionary<Vector2Int, int> faces = slice.Value;
            while (faces.Count > 0 && fallout-- > 0)
            {
                Vector2Int min = Min(faces);
                int colorIndex = faces[min];

                Vector2Int size = Expand(faces, min, colorIndex);
                Vector3Int p = new Vector3Int(min.x, slice.Key, min.y) + Vector3Int.up + Vector3Int.right * size.x;

                AddFace(p, size, Vector3Int.up, Vector3Int.left, Vector3Int.forward, colorIndex);
            }
        }

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in downFaces)
        {
            int fallout = 1000;
            Dictionary<Vector2Int, int> faces = slice.Value;
            while (faces.Count > 0 && fallout-- > 0)
            {
                Vector2Int min = Min(faces);
                int colorIndex = faces[min];

                Vector2Int size = Expand(faces, min, colorIndex);
                Vector3Int p = new Vector3Int(min.x, slice.Key, min.y);

                AddFace(p, size, Vector3Int.down, Vector3Int.right, Vector3Int.forward, colorIndex);
            }
        }

        Mesh voxelMesh = new Mesh();
        //voxelMesh.Clear();  // Clear the mesh to avoid any existing data
   
        voxelMesh.indexFormat = indexFormat;
        voxelMesh.vertices = vertices.ToArray();
        voxelMesh.triangles = triangles.ToArray();
        voxelMesh.normals = normals.ToArray();
        voxelMesh.uv = uvs.ToArray();

        // Recalculate bounds and normals
        //voxelMesh.RecalculateBounds();
        //voxelMesh.RecalculateNormals();
        //voxelMesh.RecalculateTangents();


        return voxelMesh;

    }
}