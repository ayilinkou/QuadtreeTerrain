using System;
using System.Collections.Generic;
using UnityEditor.ShaderKeywordFilter;
using UnityEngine;

public class QuadtreeTerrain : MonoBehaviour
{
    [Header("Quadtree")]
    public float totalSize = 128f;
    public int maxDepth = 5;
    public int baseResolution = 4;
    public float splitFactor = 1.5f;
    public bool ignoreHeight = true;

    [Header("Updating")]
    public Transform player;
    public float cameraMoveThreshold = 0.25f;
    public float updateInterval = 0.2f; // seconds

    [Header("Rendering")]
    public Material chunkMaterial;
    public bool includeSkirt = true;
    public float skirtHeight = 0.1f;

    [Header("Debug")]
    public bool drawBounds = true;
    public Color boundsColor = Color.cyan;

    private QuadtreeNode root;
    private Vector3 lastCamPos;
    private float lastUpdateTime = -999f;

    private ChunkPool pool;
    private Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>(64);

    private Material lineMaterial;
    
    void Start()
    {
        BuildTree();
        CreateLineMaterial();
        EnsurePool();
        lastCamPos = player != null ? player.position : Vector3.zero;
        lastUpdateTime = -999f;
    }

	private void OnValidate()
	{
        totalSize = Mathf.Max(1f, totalSize);
        maxDepth = Mathf.Clamp(maxDepth, 0, 16);
        baseResolution = Mathf.Clamp(baseResolution, 1, 128);
        splitFactor = Mathf.Max(0.01f, splitFactor);
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

        if (player == null)
            return;

        Vector3 camPos = player.position;
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
				Chunk chunk = node.userData as Chunk;
				if (chunk != null)
					pool.Return(chunk.gameObject);
			});

            UpdateMeshesForLeaves();
            CleanupNonLeafUserData();
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

        root.ForEachLeaf(node =>
        {
            Chunk chunk = node.userData as Chunk; // TODO: maybe just make UserData a Chunk?
            int desired = MeshGenerator.GetResolutionForDepth(node.depth, maxDepth, baseResolution, maxResolution: 256);

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

                node.userData = chunk;
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

            string key = MeshKey(desired, node.size, includeSkirt, skirtHeight);
            if (!meshCache.TryGetValue(key, out Mesh mesh))
            {
                mesh = MeshGenerator.CreateFlatGrid(desired, node.size, includeSkirt, skirtHeight);
                mesh.name = key;
                meshCache[key] = mesh;
            }

            chunk.SetMesh(mesh);
            chunk.SetMaterial(chunkMaterial != null ? chunkMaterial : DefaultMaterial());
            chunk.quadsPerSide = desired;
        });
    }

    private void CleanupNonLeafUserData()
    {
        root.ForEachNode(node =>
        {
            if (!node.isLeaf && node.userData != null)
            {
                // release chunk if present
                Chunk chunk = node.userData as Chunk; // TODO: same here
                if (chunk != null)
                {
                    pool.Return(chunk.gameObject);
                }
                node.userData = null;
            }
        });
    }

    private void ReleaseAllChunks()
    {
		if (root != null)
		{
            root.ForEachNode(node =>
            {
                if (node.userData != null)
                {
                    Chunk chunk = node.userData as Chunk; // TODO: same here
                    if (chunk != null)
                    {
                        if (pool != null)
                            pool.Return(chunk.gameObject);
                        else if (Application.isPlaying)
                            Destroy(chunk);
                        else
                            DestroyImmediate(chunk);
                    }
                    node.userData = null;
                }
            });
		}
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

    private string MeshKey(int quads, float size, bool drawSkirt, float skirtH)
    {
        return $"q{quads}_s{size}_sk{(drawSkirt ? 1 : 0)}_h{skirtH}";
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
