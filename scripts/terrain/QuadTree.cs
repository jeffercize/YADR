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

    public Image CreateQuadTreeImage(QuadTreeLOD root, int width, int height)
    {
        Image image = Image.Create(width, height, false, Image.Format.Rgba8);

        Godot.Color color = new Godot.Color(1, 0, 0); // Red color for the boxes
        DrawQuadTree(image, color, root);

        return image;
    }

    public Image CreateQuadTreeImageLevel(QuadTreeLOD root, int width, int height)
    {
        Image image = Image.Create(width, height, false, Image.Format.Rgba8);

        Godot.Color color = new Godot.Color(1, 0, 0); // Red color for the boxes
        DrawQuadTreeLevel(image, color, root);

        return image;
    }

    private void DrawQuadTree(Image image, Godot.Color color, QuadTreeLOD quadtree)
    {
        image.FillRect(new Rect2I(quadtree.bounds.X, quadtree.bounds.Y, quadtree.bounds.Width, quadtree.bounds.Height), color);
        Random random = new Random();
        color = new Godot.Color(random.NextSingle(), random.NextSingle(), random.NextSingle());
        foreach (QuadTreeLOD child in quadtree.nodes)
        {
            if (child != null)
            {
                DrawQuadTree(image, color, child);
            }
        }
    }

    private void DrawQuadTreeLevel(Image image, Godot.Color color, QuadTreeLOD quadtree)
    {
        image.FillRect(new Rect2I(quadtree.bounds.X, quadtree.bounds.Y, quadtree.bounds.Width, quadtree.bounds.Height), color);
        Random random = new Random();
        color = new Godot.Color(quadtree.level / 9.0f, quadtree.level / 9.0f, quadtree.level / 9.0f);
        foreach (QuadTreeLOD child in quadtree.nodes)
        {
            if (child != null)
            {
                DrawQuadTreeLevel(image, color, child);
            }
        }
    }

}