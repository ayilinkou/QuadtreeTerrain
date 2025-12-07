using System;
using Unity.VisualScripting;
using UnityEngine;

public static class MeshGenerator
{
	
    // calculate number of quads per side
	public static int GetResolutionForDepth(int depth, int maxDepth, int baseResolution, int maxResolution, float chunkSize,
                                            float targetSpacing = 1f)
    {
        // compute depth factor: 0 = root, 1 = deepest
        float t = (float)depth / maxDepth;

        // depth-based resolution interpolation
        float depthRes = Mathf.Lerp(baseResolution, maxResolution, t);

        int quadsPerSide = Mathf.Clamp(Mathf.RoundToInt(depthRes), baseResolution, maxResolution);

        return quadsPerSide;
    }

	public static Mesh CreateChunkMesh(QuadtreeTerrain qtTerrain, int quadsPerSide, float size, Vector2 chunkCenter)
	{
		if (quadsPerSide < 1)
			quadsPerSide = 1;

		int vertsPerSide = quadsPerSide + 1;
		int baseVerts = vertsPerSide * vertsPerSide;
		int baseTris = quadsPerSide * quadsPerSide * 2;

		Vector3[] vertices = new Vector3[baseVerts];
		Vector2[] uv = new Vector2[vertices.Length];

		int[] tris = new int[(baseTris) * 3];

		float step = size / quadsPerSide;
		int idx = 0;

        float halfWorld = qtTerrain.totalSize / 2f;

		for (int z = 0; z < vertsPerSide; z++)
		{
			for (int x = 0; x < vertsPerSide; x++)
			{
                // local XZ in chunk space
                float vx = -size * 0.5f + x * step;
				float vz = -size * 0.5f + z * step;

                // vertex world space coords
                float worldX = chunkCenter.x + vx;
                float worldZ = chunkCenter.y + vz;

                // map from world space
                float u = (worldX + halfWorld) / qtTerrain.totalSize;
                float v = (worldZ + halfWorld) / qtTerrain.totalSize;

				float vy = 0f;

				switch (qtTerrain.heightOffsetType)
				{
					case HeightOffsetType.Heightmap:
						vy += qtTerrain.GetHeightmapValue(u, v) * qtTerrain.heightDisplacement;
						break;
					case HeightOffsetType.Noise:
						vy += qtTerrain.GetPerlinNoise(u, v, qtTerrain.seed) * qtTerrain.heightDisplacement;
						break;
					case HeightOffsetType.HeightmapAndNoise:
						vy += qtTerrain.GetHeightmapValue(u, v) * (1f - qtTerrain.heightmapToNoiseWeight);
						vy += qtTerrain.GetPerlinNoise(u, v, qtTerrain.seed) * qtTerrain.heightmapToNoiseWeight;
						vy *= qtTerrain.heightDisplacement;
						break;
					default:
						break;
				}

				vertices[idx] = new Vector3(vx, vy, vz);
				uv[idx] = new Vector2(u, v);
				idx++;
			}
		}

		int triIndex = 0;
		for (int z = 0; z < quadsPerSide; z++)
		{
			for (int x = 0; x < quadsPerSide; x++)
			{
				int v00 = z * vertsPerSide + x;
				int v10 = v00 + 1;
				int v01 = v00 + vertsPerSide;
				int v11 = v01 + 1;

				tris[triIndex++] = v00;
				tris[triIndex++] = v01;
				tris[triIndex++] = v11;

				tris[triIndex++] = v00;
				tris[triIndex++] = v11;
				tris[triIndex++] = v10;
			}
		}

		Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		mesh.name = $"Grid_q{quadsPerSide}_s{size}";
		mesh.vertices = vertices;
		mesh.uv = uv;

		mesh.triangles = tris;

		mesh.RecalculateBounds();
		mesh.RecalculateNormals();

		return mesh;
	}
}
