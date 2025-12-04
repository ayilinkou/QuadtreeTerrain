using System;
using Unity.VisualScripting;
using UnityEngine;

public static class MeshGenerator
{
	// calculate number of quads per side
	public static int GetResolutionForDepth(int depth, int maxDepth, int baseResolution, int maxResolution = 256)
	{
		int shiftCount = Math.Max(0, maxDepth - depth);
		int res = baseResolution << shiftCount; // same as baseRes * 2^(shiftCount)

		if (res < 1)
			res = 1;

		if (res > maxResolution)
			res = maxResolution;

		return res;
	}

	public static Mesh CreateFlatGrid(int quadsPerSide, float size, bool bIncludeSkirt = true, float skirtHeight = 0.1f)
	{
		if (quadsPerSide < 1)
			quadsPerSide = 1;

		int vertsPerSide = quadsPerSide + 1;
		int baseVerts = vertsPerSide * vertsPerSide;
		int baseTris = quadsPerSide * quadsPerSide * 2;

		int skirtVerts = 0;
		int skirtTris = 0;
		if (bIncludeSkirt)
		{
			Debug.Log("Include skirt logic not yet added!");
			//skirtVerts = vertsPerSide * 4;
			//skirtTris = quadsPerSide * 4 * 2;
		}

		Vector3[] vertices = new Vector3[baseVerts + skirtVerts];
		Vector3[] normals = new Vector3[vertices.Length];
		Vector2[] uv = new Vector2[vertices.Length];

		int[] tris = new int[(baseTris + skirtTris) * 3];

		float step = size / quadsPerSide;
		int idx = 0;

		for (int z = 0; z < vertsPerSide; z++)
		{
			for (int x = 0; x < vertsPerSide; x++)
			{
				float vx = -size * 0.5f + x * step;
				float vz = -size * 0.5f + z * step;
				vertices[idx] = new Vector3(vx, 0f, vz);
				normals[idx] = Vector3.up;
				uv[idx] = new Vector2((float)x / quadsPerSide, (float)z / quadsPerSide);
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

		if (bIncludeSkirt)
		{
			AddSkirts(baseVerts, skirtVerts, vertsPerSide, quadsPerSide, skirtHeight, vertices, normals, uv, tris, triIndex);
		}

		Mesh mesh = new Mesh();
		mesh.name = $"Grid_q{quadsPerSide}_s{size}";
		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.uv = uv;

		mesh.triangles = tris;

		mesh.RecalculateBounds();
		// no need to call mesh.RecalculateNormals() since we added them manually already

		return mesh;
	}

    private static void AddSkirts(int baseVerts, int skirtVerts, int vertsPerSide, int quadsPerSide, float skirtHeight, Vector3[] vertices, Vector3[] normals, Vector2[] uv,
        int[] tris, int triIndex)
	{
        int skirtStartVert = baseVerts;
        // order: bottom edge (z=0, x=0..n), right edge (x=n, z=0..n), top edge (z=n, x=n..0), left edge (x=0, z=n..0)
        // for each edge we create a row of verts at y = -skirtHeight, mapping to corresponding base edge verts
        // we'll create triangles between the base edge and the skirt row

        // Helper to get base vertex index at (x,z)
        Func<int, int, int> baseIndex = (x, z) => z * vertsPerSide + x;

        int sv = skirtStartVert;
        // bottom edge (z = 0) x = 0..n
        for (int x = 0; x < vertsPerSide; x++) vertices[sv++] = vertices[baseIndex(x, 0)] + Vector3.down * skirtHeight;
        // right edge (x = n) z = 0..n
        for (int z = 0; z < vertsPerSide; z++) vertices[sv++] = vertices[baseIndex(quadsPerSide, z)] + Vector3.down * skirtHeight;
        // top edge (z = n) x = n..0
        for (int x = quadsPerSide; x >= 0; x--) vertices[sv++] = vertices[baseIndex(x, quadsPerSide)] + Vector3.down * skirtHeight;
        // left edge (x = 0) z = n..0
        for (int z = quadsPerSide; z >= 0; z--) vertices[sv++] = vertices[baseIndex(0, z)] + Vector3.down * skirtHeight;

        // Normals & uvs for skirt verts
        for (int i = baseVerts; i < baseVerts + skirtVerts; i++)
        {
            normals[i] = Vector3.up; // we keep upward normal; shading with skirt low makes seam invisible
            uv[i] = Vector2.zero;
        }

        // Now create skirt triangles. We'll iterate each edge and create quads between base and skirt verts.
        // For each edge segment we add two triangles.

        // helper indices
        int skirtIdx = baseVerts;
        // bottom edge: base (x,0) for x in 0..n-1, skirt verts in same order
        for (int x = 0; x < quadsPerSide; x++)
        {
            int baseA = baseIndex(x, 0);
            int baseB = baseIndex(x + 1, 0);
            int skirtA = skirtIdx + x;
            int skirtB = skirtIdx + x + 1;

            // tri: baseA, skirtA, skirtB
            tris[triIndex++] = baseA;
            tris[triIndex++] = skirtA;
            tris[triIndex++] = skirtB;
            // baseA, skirtB, baseB
            tris[triIndex++] = baseA;
            tris[triIndex++] = skirtB;
            tris[triIndex++] = baseB;
        }
        skirtIdx += vertsPerSide;

        // right edge: z=0..n-1, base index at (n,z)
        for (int z = 0; z < quadsPerSide; z++)
        {
            int baseA = baseIndex(quadsPerSide, z);
            int baseB = baseIndex(quadsPerSide, z + 1);
            int skirtA = skirtIdx + z;
            int skirtB = skirtIdx + z + 1;

            tris[triIndex++] = baseA;
            tris[triIndex++] = skirtA;
            tris[triIndex++] = skirtB;

            tris[triIndex++] = baseA;
            tris[triIndex++] = skirtB;
            tris[triIndex++] = baseB;
        }
        skirtIdx += vertsPerSide;

        // top edge: x = n..1 (our skirt verts are in order x=n..0)
        for (int x = 0; x < quadsPerSide; x++)
        {
            // baseA corresponds to base at (n-x, n)
            int baseA = baseIndex(quadsPerSide - x, quadsPerSide);
            int baseB = baseIndex(quadsPerSide - (x + 1), quadsPerSide);
            int skirtA = skirtIdx + x;
            int skirtB = skirtIdx + x + 1;

            tris[triIndex++] = baseA;
            tris[triIndex++] = skirtA;
            tris[triIndex++] = skirtB;

            tris[triIndex++] = baseA;
            tris[triIndex++] = skirtB;
            tris[triIndex++] = baseB;
        }
        skirtIdx += vertsPerSide;

        // left edge: z = n..1
        for (int z = 0; z < quadsPerSide; z++)
        {
            int baseA = baseIndex(0, quadsPerSide - z);
            int baseB = baseIndex(0, quadsPerSide - (z + 1));
            int skirtA = skirtIdx + z;
            int skirtB = skirtIdx + z + 1;

            tris[triIndex++] = baseA;
            tris[triIndex++] = skirtA;
            tris[triIndex++] = skirtB;

            tris[triIndex++] = baseA;
            tris[triIndex++] = skirtB;
            tris[triIndex++] = baseB;
        }
	}
}
