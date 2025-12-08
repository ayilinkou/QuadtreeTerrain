using System;
using System.Collections.Generic;
using UnityEditor.ShaderKeywordFilter;
using UnityEngine;

public enum HeightOffsetType
{
    None,
    Noise
}

public class QuadtreeTerrain : MonoBehaviour
{    
    [Header("Quadtree")]
    public float totalSize = 128f;
    public int maxDepth = 5;
    public int baseResolution = 4;
    public float splitFactor = 1.5f;
    public bool ignoreHeight = true;

    [Header("Updating")]
    public Transform playerTransform;
    public float cameraMoveThreshold = 0.25f;
    public float updateInterval = 0.2f; // seconds
    [Tooltip("Time in milliseconds which will be used per frame towards generating terrain meshes. Any meshes remaining will be handled in the next frame.")]
    public float frameBudgetMilliseconds = 8f;
    [Tooltip("Higher means more smooth transitions to cached meshes, but higher memory cost.")]
    public int meshCacheSize = 128;

    [Header("Rendering")]
    public Material chunkMaterial;
    public HeightOffsetType heightOffsetType = HeightOffsetType.Noise;
    public float heightDisplacement;

    [Header("Noise")]
    public int seed = 12345;
    public bool randomiseSeed = true;
    public float scale = 1f;
    [Tooltip("Number of layers of noise.")]
    public int octaves = 1;
    [Tooltip("Multiplier for amplitude per octave of noise. Keep below 1 for fBm behaviour.")]
    public float persistence = 0.5f;
    [Tooltip("Multiplier for frequency per octave of noise. Keep above 1 for fBm behaviour.")]
    public float lacunarity = 2f;
    private System.Random rng;
    private float noiseOffsetX;
    private float noiseOffsetY;

    [Header("Debug")]
    [Tooltip("This only takes effect for meshes being generated after changing value. Ideally set before starting a play session")]
    public bool visualiseChunks = false;
    [SerializeField] private int chunkCount;
    [SerializeField] private int chunksInQueue;

	private QuadtreeNode root;
    private Vector3 lastCamPos;
    private float lastUpdateTime = -999f;

    private ChunkPool pool;

    [HideInInspector] public Color materialColor;
    [HideInInspector] public bool visualiseChunksCached = false;
    
    void Start()
    {
        Vector3 pos = playerTransform.position;
        playerTransform.position = new Vector3(pos.x, pos.y + heightDisplacement, pos.z);

        InitRng();
        MeshGenerator.Init(this, meshCacheSize);
        BuildTree();
        EnsurePool();
        lastCamPos = playerTransform != null ? playerTransform.position : Vector3.zero;
        lastUpdateTime = -999f;
        materialColor = chunkMaterial != null ? chunkMaterial.color : new Color(0f, 0.8f, 0f);
        visualiseChunksCached = visualiseChunks;
    }

	private void OnValidate()
	{
        totalSize = Mathf.Max(1f, totalSize);
        maxDepth = Mathf.Clamp(maxDepth, 0, 16);
        baseResolution = Mathf.Clamp(baseResolution, 1, 128);
        splitFactor = Mathf.Max(0.01f, splitFactor);
        octaves = Mathf.Clamp(octaves, 1, 32);
	}

	private void OnDisable()
	{
        ReleaseAllChunks();
        MeshGenerator.ClearMeshCache();
	}

	void Update()
    {
        if (root == null)
            BuildTree();

        if (playerTransform == null)
            return;

        Vector3 camPos = playerTransform.position;
        float now = Time.time;

        // only update if moved significantly or timer elapsed
        bool cameraMoved = (camPos -  lastCamPos).sqrMagnitude > cameraMoveThreshold * cameraMoveThreshold;
        bool intervalElapsed = now - lastUpdateTime > updateInterval;

        if (cameraMoved || intervalElapsed)
        {
            lastCamPos = camPos;
            lastUpdateTime = now;

            root.Evaluate(camPos, GetSplitThresholdForDepth, node =>
			{
				Chunk chunk = node.chunkData;
				if (chunk != null)
					pool.Return(chunk.gameObject);
			});

            UpdateMeshesForLeaves();
            CleanupNonLeafChunkData();
        }

        chunksInQueue = MeshGenerator.meshesInQueue;
    }

    private void InitRng()
    {
        if (randomiseSeed)
        {
            System.Random tempRng = new System.Random();
            seed = tempRng.Next(int.MinValue, int.MaxValue);
        }

        rng = new System.Random(seed);
        noiseOffsetX = (float)(rng.NextDouble() * 2000.0 - 1000.0);
        noiseOffsetY = (float)(rng.NextDouble() * 2000.0 - 1000.0);
    }

    public float GetPerlinNoise(float u, float v, int noiseSeed)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float noiseHeight = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = (u * scale * frequency) + noiseOffsetX;
            float sampleY = (v * scale * frequency) + noiseOffsetY;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
            noiseHeight += perlinValue * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }
        return noiseHeight;
    }

    private void BuildTree()
    {
        ReleaseAllChunks();
        root = new QuadtreeNode(Vector2.zero, totalSize, 0, maxDepth, this);
    }

    private float GetSplitThresholdForDepth(int depth)
    {
        float tileSize = totalSize / (1 << depth);
        return tileSize * splitFactor;
    }

    private void EnsurePool()
    {
        if (pool == null)
            pool = new ChunkPool(transform, chunkMaterial);
    }

    private void UpdateMeshesForLeaves()
    {
        EnsurePool();
        chunkCount = 0;

        root.ForEachLeaf(node =>
        {
            Chunk chunk = node.chunkData;
            int desired = MeshGenerator.GetResolutionForDepth(node.depth, maxDepth, baseResolution, maxResolution: 256, node.size);
            chunkCount++;

            if (chunk == null)
            {
                // get chunk GO from pool
                GameObject go = pool.Rent();

                // chunk from pool might have incorrect mesh, so we hide while generating
                go.SetActive(false);
                
                go.name = $"Chunk_d{node.depth}_c{node.center.x}_{node.center.y}";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(node.center.x, 0f, node.center.y);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                chunk = go.GetComponent<Chunk>();
                chunk.quadsPerSide = -1;

                node.chunkData = chunk;
                chunk.ownerNode = node;
            }
            else
            {
                // ensure chunk transform is correct in case of reuse
                GameObject go = chunk.gameObject;
				go.transform.SetParent(transform, false);
				go.transform.localPosition = new Vector3(node.center.x, 0f, node.center.y);
				go.transform.localRotation = Quaternion.identity;
				go.transform.localScale = Vector3.one;
			}

            // skip regeneration if mesh resolution matches desired
            if (chunk.quadsPerSide == desired)
                return;

            string key = MeshKey(desired, node.size, node.center);
            if (!MeshGenerator.meshCache.TryGetValue(key, out Mesh mesh))
            {
                chunk.isGenerationPending = true;
                MeshGenerator.EnqueueMesh(key, desired, node.size, node.center, chunk);
                if (!MeshGenerator.isGenerating)
                    StartCoroutine(MeshGenerator.ProcessMeshQueue(frameBudgetMilliseconds));
                return;
            }

            chunk.SetMesh(mesh);
            chunk.SetMaterial(chunkMaterial != null ? chunkMaterial : DefaultMaterial());
            chunk.SetColor(visualiseChunksCached ? UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f) : materialColor);
            chunk.quadsPerSide = desired;
            chunk.isGenerationPending = false;
            chunk.gameObject.SetActive(true);
        });
    }

    private void CleanupNonLeafChunkData()
    {
        root.ForEachNode(node =>
        {
            if (!node.isLeaf && node.chunkData != null)
            {
                // release chunk if present
                Chunk chunk = node.chunkData;
                if (chunk != null)
                {
                    pool.Return(chunk.gameObject);
                }
                node.chunkData = null;
            }
        });
    }

    private void ReleaseAllChunks()
    {
		if (root != null)
		{
            root.ForEachNode(node =>
            {
                if (node.chunkData != null)
                {
                    Chunk chunk = node.chunkData;
                    if (chunk != null)
                    {
                        if (pool != null)
                            pool.Return(chunk.gameObject);
                        else if (Application.isPlaying)
                            Destroy(chunk);
                        else
                            DestroyImmediate(chunk);
                    }
                    node.chunkData = null;
                }
            });
		}
        chunkCount = 0;
	}

    private string MeshKey(int quads, float size, Vector2 center)
    {
        return $"q{quads}_s{size}_c{center.x}_{center.y}";
    }

    public static Material DefaultMaterial()
    {
        return new Material(Shader.Find("Standard"));
    }
}
