using UnityEngine;

public class Chunk : MonoBehaviour
{
    [HideInInspector] public int quadsPerSide = -1;
    [HideInInspector] public QuadtreeNode ownerNode;
    
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
