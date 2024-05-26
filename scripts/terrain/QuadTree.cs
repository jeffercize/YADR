using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;

public class QuadTreeLOD
{
    private const int MAX_LEVELS = 5;

    public int level;
    public Rectangle bounds;
    public QuadTreeLOD[] nodes;

    public QuadTreeLOD(int level, Rectangle bounds)
    {
        this.level = level;
        this.bounds = bounds;
        this.nodes = new QuadTreeLOD[4];
    }

    public void Clear()
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] != null)
            {
                nodes[i].Clear();
                nodes[i] = null;
            }
        }
    }

    public void SplitNode()
    {
        int subWidth = bounds.Width / 2;
        int subHeight = bounds.Height / 2;
        int x = bounds.X;
        int y = bounds.Y;

        nodes[0] = new QuadTreeLOD(level + 1, new Rectangle(x + subWidth, y, subWidth, subHeight));
        nodes[1] = new QuadTreeLOD(level + 1, new Rectangle(x, y, subWidth, subHeight));
        nodes[2] = new QuadTreeLOD(level + 1, new Rectangle(x, y + subHeight, subWidth, subHeight));
        nodes[3] = new QuadTreeLOD(level + 1, new Rectangle(x + subWidth, y + subHeight, subWidth, subHeight));
    }

    private int GetIndex(Vector3 playerPosition)
    {
        int index = -1;
        double verticalMidpoint = bounds.X + (bounds.Width / 2);
        double horizontalMidpoint = bounds.Y + (bounds.Height / 2);

        // Player is in top quadrants
        bool topQuadrant = (playerPosition.Y < horizontalMidpoint);
        // Player is in bottom quadrants
        bool bottomQuadrant = (playerPosition.Y > horizontalMidpoint);

        // Player is in left quadrants
        if (playerPosition.X < verticalMidpoint)
        {
            if (topQuadrant)
            {
                index = 1;
            }
            else if (bottomQuadrant)
            {
                index = 2;
            }
        }
        // Player is in right quadrants
        else if (playerPosition.X > verticalMidpoint)
        {
            if (topQuadrant)
            {
                index = 0;
            }
            else if (bottomQuadrant)
            {
                index = 3;
            }
        }

        return index;
    }
}