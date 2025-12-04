using UnityEngine;
using System.Collections.Generic;

public class ChunkPool
{
    private readonly Stack<GameObject> stack = new Stack<GameObject>();
    private readonly Transform parent;
    private readonly Material material;

    public ChunkPool(Transform poolParent = null,  Material mat = null)
    {
        parent = poolParent;
        material = mat;
    }

    public GameObject Rent()
    {
        GameObject go;
		if (stack.Count > 0)
		{
            go = stack.Pop();
            go.SetActive(true);
		}
        else
        {
            go = CreateNew();
        }
        return go;
	}

    public void Return(GameObject go)
    {
        if (go == null)
            return;

        go.SetActive(false);
        go.transform.SetParent(parent, false);
        stack.Push(go);
    }

    private GameObject CreateNew()
    {
        GameObject go = new GameObject("Chunk");
        if (parent != null)
            go.transform.SetParent(parent, false);

        go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material != null ? material : new Material(Shader.Find("Standard"));
        Chunk chunk = go.AddComponent<Chunk>();
        chunk.quadsPerSide = -1;
        return go;
    }
}
