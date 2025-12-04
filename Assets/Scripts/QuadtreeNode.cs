using System;
using UnityEngine;

public class QuadtreeNode
{
    public readonly Vector2 center;
    public readonly float size;
    public readonly int depth;
    public readonly int maxDepth;

    public QuadtreeNode[] children;

    public bool bIsLeaf => children == null;

    public object userData;

    public QuadtreeNode(Vector2 center, float size, int depth, int maxDepth)
    {
        this.center = center;
        this.size = size;
        this.depth = depth;
        this.maxDepth = maxDepth;
        children = null;
        userData = null;
    }

    // evaluate if splitting or merging should occur
    // recurses to children
    public void Evaluate(Vector3 cameraPos, Func<int, float> getSplitThresholdForDepth)
    {
        Vector2 camXZ = new Vector2(cameraPos.x, cameraPos.z);
        float dist = Vector2.Distance(camXZ, center);
        float threshold = getSplitThresholdForDepth != null ? getSplitThresholdForDepth(depth) : size * 1.5f;
        bool bShouldSplit = (depth < maxDepth) && (dist < threshold);

        if (bShouldSplit)
        {
            if (bIsLeaf)
                Split();

            // recurse evaluation to children
            for (int i = 0; i < 4; i++)
            {
                children[i].Evaluate(cameraPos, getSplitThresholdForDepth);
            }
        }
        else
        {
            // don't split, if we have children we merge them
            if (!bIsLeaf)
            {
                Merge();
            }
            // if it's a leaf, do nothing
        }
    }

    private void Split()
    {
        if (!bIsLeaf)
            return;

        children = new QuadtreeNode[4];
        float half = size * 0.5f;
        float quarter = size * 0.25f;

        children[0] = new QuadtreeNode(center + new Vector2(-quarter, -quarter), half, depth + 1, maxDepth);
        children[1] = new QuadtreeNode(center + new Vector2( quarter, -quarter), half, depth + 1, maxDepth);
        children[2] = new QuadtreeNode(center + new Vector2(-quarter,  quarter), half, depth + 1, maxDepth);
        children[3] = new QuadtreeNode(center + new Vector2( quarter,  quarter), half, depth + 1, maxDepth);
	}

    // recursively destroy children and set as leaf
    private void Merge()
    {
        if (bIsLeaf)
            return;

		for (int i = 0; i < 4; i++)
		{
            children[i].Merge();
            children[i].userData = null;
		}
        children = null;
	}

    // recursively walks the tree and invokes action on every leaf node
    // used for creating and updating meshes for leaves
    public void ForEachLeaf(Action<QuadtreeNode> action)
    {
        if (action == null)
            return;

        if (bIsLeaf)
        {
            action(this);
        }
		else
		{
			for (int i = 0; i < 4; i++)
			{
                children[i].ForEachLeaf(action);
			}
		}
	}

    // recursively walks the tree and invokes action on every node
    public void ForEachNode(Action<QuadtreeNode> action)
    {
		if (action == null)
			return;

		action(this);

		if (!bIsLeaf)
		{
			for (int i = 0; i < 4; i++)
			{
				children[i].ForEachNode(action);
			}
		}
	}

    public int CountLeaves()
    {
        if (bIsLeaf) return 1;

        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            count += children[i].CountLeaves();
        }
        return count;
    }
}
