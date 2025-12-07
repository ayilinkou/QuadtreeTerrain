using System;
using System.Collections.Generic;
using UnityEditor.ShaderKeywordFilter;
using UnityEngine;

public enum HeightOffsetType
{
    None,
    Heightmap,
    Noise,
    HeightmapAndNoise
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

    [Header("Rendering")]
    public Material chunkMaterial;
    public HeightOffsetType heightOffsetType = HeightOffsetType.Noise;
    public float heightDisplacement;
    [Tooltip("Used when Height Offset Type is set to Heightmap And Noise. 0 = heightmap only, 1 = noise only")]
    public float heightmapToNoiseWeight = 0.5f;

    [Header("Heightmap")]
    public Texture2D heightmapTexture;
    private float[] heightmap;

    [Header("Noise")]
    public int seed = 12345;
    public bool randomiseSeed = true;
    public float scale = 1f;
    public int octaves = 1;
    public float persistence = 0.5f;
    public float lacunarity = 2f;
    private System.Random rng;
    private float noiseOffsetX;
    private float noiseOffsetY;

    [Header("Debug")]
    public bool drawBounds = true;
    public bool visualiseChunks = false; // this can only be toggled before starting play session
    private Color boundsColor = Color.red;
    private bool visualiseChunksCached = false;
    [SerializeField]
    private int chunkCount;

	private QuadtreeNode root;
    private Vector3 lastCamPos;
    private float lastUpdateTime = -999f;

    private ChunkPool pool;
    private Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>(64);

    private Material lineMaterial;
    private Color materialColor;
    
    void Start()
    {
        Vector3 pos = playerTransform.position;
        playerTransform.position = new Vector3(pos.x, pos.y + heightDisplacement, pos.z);

        InitRng();
        LoadHeightmap();
        BuildTree();
        CreateLineMaterial();
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
        heightmapToNoiseWeight = Mathf.Clamp01(heightmapToNoiseWeight);
	}

	private void OnDisable()
	{
        ReleaseAllChunks();
        ClearMeshCache();
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
    }

	private void OnRenderObject()
	{
        if (!drawBounds || root == null || Camera.current == null || Camera.current != Camera.main)
            return;

        lineMaterial.SetPass(0);

        GL.PushMatrix();

        GL.LoadProjectionMatrix(Camera.current.projectionMatrix);
        GL.modelview = Camera.current.worldToCameraMatrix;

        GL.Begin(GL.LINES);
        GL.Color(boundsColor);
        root.ForEachNode(node =>
        {
            DrawNodeBoundsGL(node);
        });
        GL.End();

        GL.PopMatrix();
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

    private void LoadHeightmap()
    {
        if (heightmapTexture == null)
        {
            Debug.LogWarning("heightmapTexture is null!");
            return;
        }

        Color[] values = heightmapTexture.GetPixels();

        heightmap = new float[values.Length];
        for (uint i = 0; i < values.Length; i++)
        {
            heightmap[i] = values[i].r;
        }
    }

    public float GetHeightmapValue(float u, float v)
    {
        int width = heightmapTexture.width;
        int height = heightmapTexture.height;

        int newU = Mathf.Clamp((int)(u * (width - 1)), 0, width - 1);
        int newV = Mathf.Clamp((int)(v * (height - 1)), 0, height - 1);

        int index = newV * height + newU;
        return heightmap[index];
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
        //return Mathf.Clamp01(noiseHeight);
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
            if (!meshCache.TryGetValue(key, out Mesh mesh))
            {
                mesh = MeshGenerator.CreateChunkMesh(this, desired, node.size, node.center);
                mesh.name = key;
                meshCache[key] = mesh;
            }

            chunk.SetMesh(mesh);
            chunk.SetMaterial(chunkMaterial != null ? chunkMaterial : DefaultMaterial());
            chunk.SetColor(visualiseChunksCached ? UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f) : materialColor);
            chunk.quadsPerSide = desired;
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

    private void ClearMeshCache()
    {
        foreach (Mesh mesh in meshCache.Values)
        {
            if (mesh != null)
            {
                if (Application.isPlaying)
                    DestroyImmediate(mesh);
                else
                    Destroy(mesh);
            }
        }
        meshCache.Clear();
    }

    private string MeshKey(int quads, float size, Vector2 center)
    {
        return $"q{quads}_s{size}_c{center.x}_{center.y}";
    }

    private Material DefaultMaterial()
    {
        return new Material(Shader.Find("Standard"));
    }

    private void CreateLineMaterial()
    {
        if (lineMaterial != null)
            return;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            lineMaterial = new Material(Shader.Find("Unlit/Color"));
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetColor("_Color", boundsColor);
        }
        else
        {
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_ZWrite", 0);

            // always pass depth test so that the terrain can't cover the debug lines
            lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
		}
    }

    private void DrawNodeBoundsGL(QuadtreeNode node)
    {
        float half = node.size * 0.5f;
        Vector3 c = new Vector3(node.center.x, 0f, node.center.y);

        Vector3[] corners = new Vector3[4];
        corners[0] = c + new Vector3(-half, 0f, -half);
        corners[1] = c + new Vector3( half, 0f, -half);
        corners[2] = c + new Vector3( half, 0f,  half);
        corners[3] = c + new Vector3(-half, 0f,  half);

        for (int i = 0; i < 4; i++)
        {
            GL.Vertex(corners[i]);
            GL.Vertex(corners[(i + 1) % 4]);
        }
	}
}
