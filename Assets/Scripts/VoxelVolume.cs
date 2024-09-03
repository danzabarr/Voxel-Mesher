using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class VoxelVolume : MonoBehaviour
{
    [SerializeField] private Vector3 anchor;
    [SerializeField] private Vector3 position;
    [SerializeField] private Vector3 rotation;
    [SerializeField] private Vector3 scale = Vector3.one;
    [SerializeField] private VoxelData data;

    private VoxelMap map;
    public Transform ray;

    public VoxelVolume Initialise(VoxelData data, Vector3 anchor, Vector3 position, Vector3 rotation, Vector3 scale)
    {
        this.data = data;
        this.anchor = anchor;
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
        return this;
    }

    public int Version => data.version;
    public Vector3Int Size => data.size;
    public Bounds Bounds => new Bounds((Vector3)Size * 0.5f, Size);
    public IEnumerator<Voxel> Voxels => data.voxels.GetEnumerator();

    public Vector3 Anchor => anchor;
    public Vector3 Position => position;
    public Vector3 Rotation => rotation;
    public Vector3 Scale => scale;
    
    public Matrix4x4 Transform => Matrix4x4.TRS(Position, Quaternion.Euler(Rotation), Scale) 
        * Matrix4x4.Translate(new Vector3(-Anchor.x * Size.x, -Anchor.y * Size.y, -Anchor.z * Size.z));
    
    public Color GetColor(int index) 
    {
        if (index < 0 || index >= data.palette.Length)
            return Color.clear;
        return data.palette[index];
    }

    public bool TryGetValue(Vector3Int pos, out int color)
    {
        if (map == null)
            map = new VoxelMap(data.voxels);

        return map.TryGetValue(pos, out color);
    }

    public void Traverse(Ray ray, Voxel.Traversal traversal)
    {
        if (data == null)
            return;

        Matrix4x4 inverse = (transform.localToWorldMatrix * Transform).inverse;
        ray.origin = inverse.MultiplyPoint(ray.origin);
        ray.direction = inverse.MultiplyVector(ray.direction);        

        if (!Bounds.IntersectRay(ray))
            return;

        if (map == null)
            map = new VoxelMap(data.voxels);

        Voxel.Traverse(ray, map, data.size, traversal);
    }

    public void Start()
    {
        GameObject gameObject = new GameObject(data.name + "_mesh");
        gameObject.AddComponent<MeshFilter>().sharedMesh = ConstructMesh((Vector3Int pos, int color) => color ==254);
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        gameObject.transform.SetParent(transform);
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
    }


    [ContextMenu("Construct Fragments")]
    public void ConstructFragments()
    {
        List<Vector3> points = DistributePoints(500, Vector3.zero, Size);
        List<Voxel>[] cells = KMeansClustering(data.voxels, points);
        //Voronoi(data.voxels, points);

        for (int i = 0; i < cells.Length; i++)
        {
            List<Voxel> fragment = cells[i];
            if (fragment == null || fragment.Count == 0)
                continue;

            VoxelMap map = new VoxelMap(fragment);
            Mesh mesh = Voxel.Mesh(map, Transform);

            GameObject gameObject = new GameObject(data.name + "_mesh_" + i);
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

    public static List<Voxel>[] KMeansClustering(List<Voxel> voxels, List<Vector3> points)
    {
        List<Voxel>[] CreateClusters()
        {
        List<Voxel>[] clusters = new List<Voxel>[points.Count];
        for (int i = 0; i < points.Count; i++)
            clusters[i] = new List<Voxel>();

        for (int i = 0; i < voxels.Count; i++)
        {
            Voxel voxel = voxels[i];
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

    public Mesh ConstructMesh(VoxelData.Filter filter = null, IndexFormat indexFormat = IndexFormat.UInt32)
    {
        return Voxel.Mesh(new VoxelMap(data.voxels, filter), Transform, indexFormat);
    }

    public LayerMask collidersLayerMask;
    public QueryTriggerInteraction collidersQueryTriggerInteraction;
    public byte collidersColor;

    [ContextMenu("Generate Data From World Colliders")]
    public void GenerateDataFromWorldColliders()
    {
        List<Voxel> voxels = FromWorldColliders(collidersLayerMask, collidersQueryTriggerInteraction, collidersColor);
        Mesh mesh = Voxel.Mesh(new VoxelMap(voxels), Transform, IndexFormat.UInt32);
        GameObject child = new GameObject();
        child.transform.parent = transform;
        child.transform.localPosition = Vector3.zero;
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        child.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"));
    }

    public List<Voxel> FromWorldColliders(LayerMask layerMask, QueryTriggerInteraction queryTriggerInteraction, byte color)
    {
        List<Voxel> voxels = new List<Voxel>();
        // for each voxel in the volume, do a box cast to see if it intersects with any colliders
        // if it does, set the voxel to be solid
        // if it doesn't, set the voxel to be empty

        Matrix4x4 transform = this.transform.localToWorldMatrix * Transform;

        // for each voxel in the volume
        for (byte z = 0; z < data.size.z; z++)
        for (byte y = 0; y < data.size.y; y++)
        for (byte x = 0; x < data.size.x; x++)
        {
            // get the world position of the voxel
            Vector3 position = new Vector3(x, y, z);
            Vector3 worldPosition = transform.MultiplyPoint(position);
            Vector3 center = worldPosition + Vector3.one * 0.5f;
            Vector3 halfExtents = Vector3.one * 0.5f;
            Quaternion orientation = transform.rotation;

            Collider[] colliders = Physics.OverlapBox(center, halfExtents, orientation, layerMask, queryTriggerInteraction);

            bool solid = false;

            foreach (Collider collider in colliders)
            {
                if (collider.gameObject == this.gameObject)
                    continue;

                if (collider.gameObject.transform.IsChildOf(this.transform))
                    continue;

                solid = true;
                break;
            }   

            if (solid)
                voxels.Add(new Voxel(x, y, z, color));
        }

        return voxels;
    }

    private Mesh cube;
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
    public void OnDrawGizmosSelected()
    {
        if (cube == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
        }

        foreach (var v in data.voxels)
        {
            Vector3 pos = new Vector3Int(v.x, v.y, v.z) + Vector3.one * 0.5f;
            int color = v.i;
            Gizmos.color = GetColor(color);
            Gizmos.DrawMesh(cube, pos, Quaternion.identity, Vector3.one);
        }
    }
}
