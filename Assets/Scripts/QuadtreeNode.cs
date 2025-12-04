using System;
using UnityEngine;

public class QuadtreeNode
{
    public readonly Vector2 center;
    public readonly float size;
    public readonly int depth;
    public readonly int maxDepth;

    public QuadtreeNode[] children;

    private QuadtreeTerrain qtTerrain;

    public bool isLeaf => children == null;

    public object userData;

    public QuadtreeNode(Vector2 center, float size, int depth, int maxDepth, QuadtreeTerrain qtTerrain)
    {
        this.center = center;
        this.size = size;
        this.depth = depth;
        this.maxDepth = maxDepth;
        this.qtTerrain = qtTerrain;
        children = null;
        userData = null;
    }

    // evaluate if splitting or merging should occur
    // recurses to children
    public void Evaluate(Vector3 cameraPos, Func<int, float> getSplitThresholdForDepth, Action<QuadtreeNode> releaseUserData)
    {
        float dist;
        if (qtTerrain.ignoreHeight)
        {
			Vector2 camXZ = new Vector2(cameraPos.x, cameraPos.z);
			dist = Vector2.Distance(camXZ, center);
		}
		else
		{
            Vector3 centerWithHeight = new Vector3(center.x, 0f, center.y);
            dist = Vector3.Distance(cameraPos, centerWithHeight);
		}

		float threshold = getSplitThresholdForDepth != null ? getSplitThresholdForDepth(depth) : size * 1.5f;
        bool shouldSplit = (depth < maxDepth) && (dist < threshold);

        if (shouldSplit)
        {
            if (isLeaf)
                Split();

            // recurse evaluation to children
            for (int i = 0; i < 4; i++)
            {
                children[i].Evaluate(cameraPos, getSplitThresholdForDepth, releaseUserData);
            }
        }
        else
        {
            // if we have children, we merge
            if (!isLeaf && children != null)
            {
                Merge(releaseUserData);
            }
            // if it's a leaf, do nothing
        }
    }

    private void Split()
    {
        if (!isLeaf)
            return;

        children = new QuadtreeNode[4];
        float half = size * 0.5f;
        float quarter = size * 0.25f;

        children[0] = new QuadtreeNode(center + new Vector2(-quarter, -quarter), half, depth + 1, maxDepth, qtTerrain);
        children[1] = new QuadtreeNode(center + new Vector2( quarter, -quarter), half, depth + 1, maxDepth, qtTerrain);
        children[2] = new QuadtreeNode(center + new Vector2(-quarter,  quarter), half, depth + 1, maxDepth, qtTerrain);
        children[3] = new QuadtreeNode(center + new Vector2( quarter,  quarter), half, depth + 1, maxDepth, qtTerrain);
	}

    // recursively destroy children and set as leaf
    private void Merge(Action<QuadtreeNode> releaseUserData)
    {
        if (isLeaf)
            return;

		for (int i = 0; i < 4; i++)
		{
            if (children[i] == null)
                continue;
            
            children[i].Merge(releaseUserData);

			// after merging, return the chunk back into the pool
			// any released chunks will be garbage collected
			if (children[i].userData != null)
                releaseUserData?.Invoke(children[i]);

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

        if (isLeaf)
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

		if (!isLeaf)
		{
			for (int i = 0; i < 4; i++)
			{
				children[i].ForEachNode(action);
			}
		}
	}

    public int CountLeaves()
    {
        if (isLeaf) return 1;

        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            count += children[i].CountLeaves();
        }
        return count;
    }
}
