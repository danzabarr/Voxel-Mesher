using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;
using UnityEngine.Rendering;


[ScriptedImporter(1, "vox", AllowCaching = true)]
public class VoxImporter : ScriptedImporter
{
    public enum ColliderType
    {
        None,
        Bounds,
        TightBounds,
        Boxes,
        Cubes,
        Mesh
    }
    
    public Vector3 anchor;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;
    public IndexFormat meshIndexFormat = IndexFormat.UInt32;
    public ColliderType colliderType = ColliderType.None;
    public bool importData = true;
    public bool createMaterial = true;

    public struct Volume
    {
        public Vector3Int position;
        public Vector3Int size;
    }

    public Material defaultMaterial;
    public Texture2D defaultPaletteTexture;

    public override void OnImportAsset(AssetImportContext ctx)
    {
        string path = ctx.assetPath;
        string name = Path.GetFileNameWithoutExtension(path);

        // Data
        VoxelData data = VoxelData.Read(path);
        data.name = name + "_data";

        // Prefab
        GameObject prefab = new GameObject(name + "_prefab");

        // Texture
        Texture2D texture = defaultPaletteTexture;
        if (data.palette != null)
        {
            texture = new Texture2D(256, 1, TextureFormat.RGBA32, false)
            {
                name = name + "_palette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(data.palette);
            texture.Apply();
        }

        // Material
        MeshRenderer renderer = prefab.AddComponent<MeshRenderer>();
        if (createMaterial)
        {
            Material material = new Material(Shader.Find("Standard"))
            {
                name = name + "_material",
                mainTexture = texture
            };
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(material);
            ctx.AddObjectToAsset("material", material);
        }
        else
        {
            renderer.sharedMaterial = defaultMaterial;
        }

        // Mesh
        Matrix4x4 Transform = Matrix4x4.TRS(position, Quaternion.Euler(rotation), scale) 
        * Matrix4x4.Translate(new Vector3(-anchor.x * data.size.x, -anchor.y * data.size.y, -anchor.z * data.size.z));
        Mesh mesh = Voxel.Mesh(data.voxels, Transform, null, meshIndexFormat);
        mesh.name = name + "_mesh";
        prefab.AddComponent<MeshFilter>().sharedMesh = mesh;

        // Volume component and data
        if (importData)
        {    
            VoxelVolume volume = prefab.AddComponent<VoxelVolume>();
            volume.SetOffsets(anchor, position, rotation, scale);
            volume.SetData(data);
            
            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(data);
            ctx.AddObjectToAsset("data", data);
        }

        // Colliders
        switch (colliderType)
        {
            case ColliderType.Boxes:
            {
            //List<Volume> GreedyVolumes(List<Voxel> voxels)
            //{
                Dictionary<Vector3Int, int> voxels = new Dictionary<Vector3Int, int>();
                foreach (Voxel voxel in data.voxels)
                    voxels[new Vector3Int(voxel.x, voxel.y, voxel.z)] = voxel.i;
                // Greedily merge voxels into volumes
                List<Volume> volumes = new List<Volume>();
                
                void Expand(Vector3Int p, ref Vector3Int size, int axis)
                {
                    bool canExpand = true;

                    // Keep expanding while possible along the given axis
                    while (canExpand)
                    {
                        Vector3Int nextSize = size;
                        nextSize[axis] += 1;

                        // Check if the expanded cuboid face along this axis is fully valid
                        for (int z = p.z; z < p.z + nextSize.z; z++)
                        {
                            for (int y = p.y; y < p.y + nextSize.y; y++)
                            {
                                for (int x = p.x; x < p.x + nextSize.x; x++)
                                {
                                    Vector3Int checkPos = new Vector3Int(x, y, z);
                                    // Stop expanding if any voxel in the new boundary is invalid
                                    if (!voxels.ContainsKey(checkPos) || voxels[checkPos] <= 0)
                                    {
                                        canExpand = false;
                                        break;
                                    }
                                }
                                if (!canExpand) 
                                    break;
                            }
                            if (!canExpand) 
                                break;
                        }

                        // Update the size if we could expand
                        if (canExpand)
                            size[axis] += 1;
                    }
                }

                Vector3Int Min()
                {
                    Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
                    foreach (Vector3Int key in voxels.Keys)
                        if (key.z < min.z || (key.z == min.z && (key.y < min.y || (key.y == min.y && key.x < min.x))))
                            min = key;
                    return min;
                }

                while (voxels.Count > 0)
                {
                    Vector3Int p = Min();
                    if (voxels[p] <= 0)
                        continue;

                    // Start with a single voxel volume
                    Vector3Int size = new Vector3Int(1, 1, 1);

                    // Attempt to expand the volume along the x, y, and z axes
                    Expand(p, ref size, 1); // Expand along y-axis
                    Expand(p, ref size, 2); // Expand along z-axis
                    Expand(p, ref size, 0); // Expand along x-axis

                    // remove the volume from the copy
                    for (int z = p.z; z < p.z + size.z; z++)
                        for (int y = p.y; y < p.y + size.y; y++)
                            for (int x = p.x; x < p.x + size.x; x++)
                                voxels.Remove(new Vector3Int(x, y, z));

                    volumes.Add(new Volume { position = p, size = size });
                }

                //    return volumes;
                //}
                //List<Volume> volumes = GreedyVolumes(data.voxels);
                foreach (Volume volume in volumes)
                {
                    Vector3 volumeCenter = volume.position + (volume.size + Vector3.zero) * 0.5f;

                    volumeCenter = Transform.MultiplyPoint(volumeCenter);
                    Vector3 volumeSize = new Vector3(volume.size.x, volume.size.y, volume.size.z);
                    volumeSize = Transform.MultiplyVector(volumeSize);
                    volumeSize = new Vector3(Mathf.Abs(volumeSize.x), Mathf.Abs(volumeSize.y), Mathf.Abs(volumeSize.z));
                    BoxCollider box = prefab.AddComponent<BoxCollider>();
                    box.name = $"Volume_{volume.position.x}_{volume.position.y}_{volume.position.z} - {volume.size.x}_{volume.size.y}_{volume.size.z}";
                    EditorUtility.SetDirty(box);
                    box.center = volumeCenter;
                    box.size = volumeSize;
                }
                break;
            }

            case ColliderType.Cubes:
            {
                foreach (Voxel voxel in data.voxels)
                {
                    Vector3 center = new Vector3(voxel.x, voxel.y, voxel.z) + Vector3.one * 0.5f;
                    center = Transform.MultiplyPoint(center);
                    Vector3 size = Vector3.one;
                    size = Transform.MultiplyVector(size);
                    size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                    BoxCollider box = prefab.AddComponent<BoxCollider>();
                    box.name = $"Voxel_{voxel.x}_{voxel.y}_{voxel.z}";
                    EditorUtility.SetDirty(box);
                    box.center = center;
                    box.size = size;
                }
                break;
            }

            case ColliderType.Bounds:
            {
                Vector3 center = new Vector3(data.size.x, data.size.y, data.size.z) * 0.5f;
                center = Transform.MultiplyPoint(center);
                Vector3 size = new Vector3(data.size.x, data.size.y, data.size.z);
                size = Transform.MultiplyVector(size);
                Bounds bounds = new Bounds(center, size);
                BoxCollider boundsCollider = prefab.AddComponent<BoxCollider>();
                boundsCollider.center = bounds.center;
                boundsCollider.size = bounds.size;
                break;
            }

            case ColliderType.TightBounds:
            {
                Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
                Vector3Int max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
                foreach (Voxel voxel in data.voxels)
                {
                    min.x = Mathf.Min(min.x, voxel.x);
                    min.y = Mathf.Min(min.y, voxel.y);
                    min.z = Mathf.Min(min.z, voxel.z);

                    max.x = Mathf.Max(max.x, voxel.x + 1);
                    max.y = Mathf.Max(max.y, voxel.y + 1);
                    max.z = Mathf.Max(max.z, voxel.z + 1);
                }

                Vector3 center = new Vector3(min.x + max.x, min.y + max.y, min.z + max.z) * 0.5f;
                center = Transform.MultiplyPoint(center);

                Vector3 size = new Vector3(max.x - min.x, max.y - min.y, max.z - min.z);
                size = Transform.MultiplyVector(size);

                Bounds bounds = new Bounds(center, size);
                BoxCollider boundsCollider = prefab.AddComponent<BoxCollider>();
                boundsCollider.center = bounds.center;
                boundsCollider.size = bounds.size;
                break;
            }

            case ColliderType.Mesh:
            {
                MeshCollider collider = prefab.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                break;
            }

            case ColliderType.None:
            default:
                break;
        }

        EditorUtility.SetDirty(mesh);
        EditorUtility.SetDirty(texture);
        EditorUtility.SetDirty(prefab);

        // Add all assets with fixed names to ensure consistency
        ctx.AddObjectToAsset("mesh", mesh);
        ctx.AddObjectToAsset("palette", texture);
        ctx.AddObjectToAsset("prefab", prefab);

        // Set the main object (prefab) for the asset
        ctx.SetMainObject(prefab);

        // Ensure that the context saves consistently
        ctx.DependsOnSourceAsset(ctx.assetPath);
    }
}
