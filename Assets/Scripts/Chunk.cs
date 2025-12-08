using UnityEngine;

public class Chunk : MonoBehaviour
{
    [HideInInspector] public int quadsPerSide = -1;
    [HideInInspector] public QuadtreeNode ownerNode;
    [HideInInspector] public bool isGenerationPending = true;

    private static MaterialPropertyBlock propBlock;

	private void Awake()
	{
	    if (propBlock == null)
            propBlock = new MaterialPropertyBlock();
	}

    public void SetColor(Color color)
    {
        propBlock.Clear();
        propBlock.SetColor("_Color", color);
        Renderer r = GetComponent<Renderer>();
        r.SetPropertyBlock(propBlock);
    }

	public void SetMesh(Mesh mesh)
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        mf.sharedMesh = mesh;
    }

    public void SetMaterial(Material mat)
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
    }
}
