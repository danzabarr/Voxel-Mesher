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

    public class Map : Dictionary<Vector3Int, int>
    {
        public Map(IEnumerable<Voxel> data, VoxelData.Filter filter = null)
        {
            if (data == null)
                throw new System.ArgumentNullException("data");

            foreach (var v in data)
                if (filter == null || filter(new Vector3Int(v.x, v.y, v.z), v.i))
                    this[new Vector3Int(v.x, v.y, v.z)] = v.i;
        }
    }

    public static Mesh Mesh(List<Voxel> voxels, Matrix4x4 transform = default, VoxelData.Filter filter = null, IndexFormat indexFormat = IndexFormat.UInt32)
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

        // Insert voxels into a map, applying the filter.
        Map map = new Map(voxels, filter);

        // The faces for each direction are represented as a dictionary of slices,
        // where each slice is a index key to a dictionary of 2D positions and color indexes.
        Dictionary<int, Dictionary<Vector2Int, int>> forwardFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> backFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> rightFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> leftFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> upFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();
        Dictionary<int, Dictionary<Vector2Int, int>> downFaces = new Dictionary<int, Dictionary<Vector2Int, int>>();

        // Iterate over the filtered voxels and generate faces.
        foreach (KeyValuePair<Vector3Int, int> voxel in map)
        {
            Vector3Int p = voxel.Key;
            int colorIndex = voxel.Value;

            Vector3Int forward = p + Vector3Int.forward;
            Vector3Int back = p + Vector3Int.back;
            Vector3Int right = p + Vector3Int.right;
            Vector3Int left = p + Vector3Int.left;
            Vector3Int up = p + Vector3Int.up;
            Vector3Int down = p + Vector3Int.down;

            // We only need generate a face if the neighbouring voxel in that direction is empty.

            if (!map.TryGetValue(forward, out int color) || color <= 0)
            {
                if (!forwardFaces.ContainsKey(p.z))
                    forwardFaces[p.z] = new Dictionary<Vector2Int, int>();

                forwardFaces[p.z][new Vector2Int(p.x, p.y)] = colorIndex;
            }

            if (!map.TryGetValue(back, out color) || color <= 0)
            {
                if (!backFaces.ContainsKey(p.z))
                    backFaces[p.z] = new Dictionary<Vector2Int, int>();

                backFaces[p.z][new Vector2Int(p.x, p.y)] = colorIndex;
            }

            if (!map.TryGetValue(right, out color) || color <= 0)
            {
                if (!rightFaces.ContainsKey(p.x))
                    rightFaces[p.x] = new Dictionary<Vector2Int, int>();

                rightFaces[p.x][new Vector2Int(p.z, p.y)] = colorIndex;
            }

            if (!map.TryGetValue(left, out color) || color <= 0)
            {
                if (!leftFaces.ContainsKey(p.x))
                    leftFaces[p.x] = new Dictionary<Vector2Int, int>();

                leftFaces[p.x][new Vector2Int(p.z, p.y)] = colorIndex;
            }

            if (!map.TryGetValue(up, out color) || color <= 0)
            {
                if (!upFaces.ContainsKey(p.y))
                    upFaces[p.y] = new Dictionary<Vector2Int, int>();

                upFaces[p.y][new Vector2Int(p.x, p.z)] = colorIndex;
            }

            if (!map.TryGetValue(down, out color) || color <= 0)
            {
                if (!downFaces.ContainsKey(p.y))
                    downFaces[p.y] = new Dictionary<Vector2Int, int>();

                downFaces[p.y][new Vector2Int(p.x, p.z)] = colorIndex;
            }
        }

        // Greedy meshing algorithm.
        // For each slice, we greedily expand and combine faces into larger quads.

        // Function to find the tile with the minimum x and y coordinates in a slice.
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
            for (; faces.TryGetValue(new Vector2Int(min.x + size.x, min.y), out int index) && index == colorIndex; size.x++)
                faces.Remove(new Vector2Int(min.x + size.x, min.y));  // Remove as we expand

            // Expand height (greedy in y direction)
            bool canExpand = true;
            while (canExpand)
            {
                for (int x = min.x; x < min.x + size.x; x++)
                    if (!faces.TryGetValue(new Vector2Int(x, min.y + size.y), out int index) || index != colorIndex)
                    {
                        canExpand = false;
                        break;
                    }

                if (canExpand)
                {
                    for (int x = min.x; x < min.x + size.x; x++)
                        faces.Remove(new Vector2Int(x, min.y + size.y));  // Remove as we expand

                    size.y++;
                }
            }

            return size;
        }

        // Function to add a face to the mesh, given a position, size, normal, right, up and color index.
        void AddFace(Vector3Int p, Vector2Int size, Vector3Int normal, Vector3Int right, Vector3Int up, int colorIndex)
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

            Vector3 transformedNormal = transform.MultiplyVector(normal);
            for (int i = 0; i < 4; i++)
                normals.Add(transformedNormal);

            uvs.Add(new Vector2((colorIndex + 0) / 256.0f, 0));
            uvs.Add(new Vector2((colorIndex + 1) / 256.0f, 0));
            uvs.Add(new Vector2((colorIndex + 1) / 256.0f, 1));
            uvs.Add(new Vector2((colorIndex + 0) / 256.0f, 1));
        }

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in forwardFaces)
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

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in backFaces)
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

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in leftFaces)
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

        foreach (KeyValuePair<int, Dictionary<Vector2Int, int>> slice in rightFaces)
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

        // Create a new mesh and assign the vertices, triangles, normals and uvs

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