using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

public partial class AStar : Node
{
    // Called when the cell enters the scene tree for the first time.
	public override void _Ready()
	{
        GeneratePaths();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}

    public List<Stack<Cell>> GeneratePaths()
    {
        Grid = new List<List<Cell>>();

        // Load the image
        Image image = new Image();
        image.Load("C:\\Users\\jeffe\\test_images\\final_output.png"); // Replace with the path to your image
        int cellSize = 32;
        // Iterate over the pixels in the image in 2x2 grids
        for (int x = 0; x < image.GetWidth(); x += cellSize)
        {
            List<Cell> row = new List<Cell>();
            for (int y = 0; y < image.GetHeight(); y += cellSize)
            {
                // Calculate the average red channel value for the 2x2 grid
                float totalRed = 0;
                int count = 0;
                for (int dx = 0; dx < cellSize; dx++)
                {
                    for (int dy = 0; dy < cellSize; dy++)
                    {
                        if (x + dx < image.GetWidth() && y + dy < image.GetHeight())
                        {
                            Color color = image.GetPixel(x + dx, y + dy);
                            totalRed += color.R;
                            count++;
                        }
                    }
                }
                float averageRed = totalRed / count;

                // Create a cell with the average red channel value as the height
                Vector3 position = new Vector3(x / cellSize, y / cellSize, averageRed);
                Cell cell = new Cell(position);
                row.Add(cell);
            }
            Grid.Add(row);
        }
        Stopwatch stopwatch = Stopwatch.StartNew();
        GD.Print("A* Calculation Start");
        List<Stack<Cell>> paths = new List<Stack<Cell>>
        {
            FindPath(new Vector2((int)(0.0f / cellSize), (int)(2048.0f / cellSize)), new Vector2((int)(580.0f / cellSize), (int)(1048.0f / cellSize))),
            FindPath(new Vector2((int)(580.0f / cellSize), (int)(1048.0f / cellSize)), new Vector2((int)(1180.0f / cellSize), (int)(2048.0f / cellSize))),
            FindPath(new Vector2((int)(1180.0f / cellSize), (int)(2048.0f / cellSize)), new Vector2((int)(4180.0f / cellSize), (int)(3448.0f / cellSize))),
            FindPath(new Vector2((int)(4180.0f / cellSize), (int)(3448.0f / cellSize)), new Vector2((int)(7180.0f / cellSize), (int)(3548.0f / cellSize))),
            FindPath(new Vector2((int)(7180.0f / cellSize), (int)(3548.0f / cellSize)), new Vector2((int)(8182.0f / cellSize), (int)(1408.0f / cellSize)))
        };
        float countP = 0.0f;
        foreach (var path in paths)
        {
            if (countP > 0.0f)
            {
                countP = 0.0f;
            }
            else
            {
                countP = 1.0f;
            }
            float countC = 0.0f;
            foreach (Cell cell in path)
            {
                if(countC >= 1.0f)
                {
                    countC = 0.0f;
                }
                else
                {
                    countC += 0.1f;
                }
                int row = (int)cell.Position.Y;
                int col = (int)cell.Position.X;
                for (int y = row * cellSize; y < (row + 1) * cellSize; y++)
                {
                    for (int x = col * cellSize; x < (col + 1) * cellSize; x++)
                    {
                        // Ensure the pixel is within the image bounds
                        if (x < image.GetWidth() && y < image.GetHeight())
                        {
                            image.SetPixel(x, y, new Color(cell.Position.Z, countP, countC));
                        }
                    }
                }
            }
        }
        GD.Print($"A* Time elapsed: {stopwatch.Elapsed}");
        image.SavePng("C:\\Users\\jeffe\\test_images\\pathfinding_output.png");
        return paths;
    }

    List<List<Cell>> Grid;
    int GridRows
    {
        get
        {
            return Grid[0].Count;
        }
    }
    int GridCols
    {
        get
        {
            return Grid.Count;
        }
    }

    public Stack<Cell> FindPath(Vector2 Start, Vector2 End)
    {
        Cell start = new Cell(new Vector3((int)(Start.X), (int)(Start.Y), 0.0f));
        Cell end = new Cell(new Vector3((int)(End.X), (int)(End.Y), 0.0f));

        Stack<Cell> Path = new Stack<Cell>();
        PriorityQueue<Cell, float> OpenList = new PriorityQueue<Cell, float>();
        List<Cell> ClosedList = new List<Cell>();
        List<Cell> adjacencies;
        Cell current = start;

        // add start cell to Open List
        OpenList.Enqueue(start, start.Cost);

        while (OpenList.Count != 0 && !ClosedList.Exists(x => x.Equals(end)))
        {
            current = OpenList.Dequeue();
            //GD.Print(current.Position);
            ClosedList.Add(current);
            adjacencies = GetAdjacentCells(current);
            foreach (Cell n in adjacencies)
            {
                //GD.Print(n.Position);
                if (!ClosedList.Any(x => x.Equals(n)))
                {
                    bool isFound = false;
                    foreach (var oLCell in OpenList.UnorderedItems)
                    {
                        if (oLCell.Equals(n))
                        {
                            isFound = true;
                        }
                    }
                    if (!isFound)
                    {
                        n.Parent = current;
                        n.Cost = CalculateCost(current, n, end);
                        OpenList.Enqueue(n, n.Cost);
                    }
                }
            }
        }

        //GD.Print("closedlist: " + !ClosedList.Exists(x => x.Equals(end)));
        //GD.Print("openlist: " + (OpenList.Count != 0));

        // construct path, if end was not closed return null
        if (!ClosedList.Exists(x => x.Equals(end)))
        {
            return null;
        }

        // if all good, return path
        Cell temp = ClosedList[ClosedList.IndexOf(current)];
        if (temp == null) return null;
        do
        {
            Path.Push(temp);
            temp = temp.Parent;
        } while (temp != start && temp != null);
        return Path;
    }


    float curvaturePenaltyFactor = 5.0f;
    float slopePenaltyFactor = 50.0f;
    //careful changing distancePenalty could be super fucky
    float distancePenaltyFactor = 1.0f;
    private float CalculateCost(Cell current, Cell cell, Cell end)
    {
        // Calculate the direction of movement from current to cell
        Vector3 direction = cell.Position - current.Position;
        Vector2 direction2D = new Vector2(direction.X, direction.Y).Normalized();

        // Calculate the change in direction from the previous move
        Vector2 previousDirection2D;
        if (current.Parent != null)
        {
            Vector3 previousDirection = current.Position - current.Parent.Position;
            previousDirection2D = new Vector2(previousDirection.X, previousDirection.Y).Normalized();
        }
        else
        {
            previousDirection2D = direction2D; // If there is no previous move, use the current direction
        }
        //GD.Print("tuh");
        //GD.Print(previousDirection2D);
        //GD.Print(direction2D);

        // Calculate the cosine of the angle between reversedPreviousDirection2D and direction2D
        float cosAngle = previousDirection2D.Dot(direction2D);

        // Calculate the difference in direction
        float directionDifference = Mathf.Acos(cosAngle);

        // Normalize directionDifference to the range [0, π/2] radians
        if (directionDifference > Mathf.Pi / 2)
        {
            directionDifference = Mathf.Pi - directionDifference;
        }

        // Calculate the slope
        float distance3D = cell.Position.DistanceTo(end.Position);
        float distance2DToEnd = new Vector2(end.Position.X, end.Position.Y).DistanceTo(new Vector2(cell.Position.X, cell.Position.Y));
        float heightDifference = cell.Position.Z - current.Position.Z;
        float distance2D = new Vector2(current.Position.X, current.Position.Y).DistanceTo(new Vector2(cell.Position.X, cell.Position.Y));
        float slope = heightDifference / distance2D;

        // Add penalties for changes in direction and slope
        float curvaturePenalty = directionDifference * curvaturePenaltyFactor; // curvaturePenaltyFactor is a constant that determines how much penalty is applied for changes in direction
        float slopePenalty = slope * slopePenaltyFactor; // slopePenaltyFactor is a constant that determines how much penalty is applied for slope
        distance3D = distance3D * distancePenaltyFactor;
        // Calculate the cost as the distance from current to cell plus the penalties
        //GD.Print(distance2D);
        //GD.Print(curvaturePenalty);
        //GD.Print(current.Position);
        //GD.Print(cell.Position);
        //GD.Print(slopePenalty);
        float cost = distance2DToEnd + curvaturePenalty + slopePenalty;

        return cost;
    }

    private List<Cell> GetAdjacentCells(Cell n)
    {
        List<Cell> temp = new List<Cell>();

        int row = (int)n.Position.Y;
        int col = (int)n.Position.X;

        // Check the four cardinal directions
        if (row + 1 < GridRows)
        {
            temp.Add(Grid[col][row + 1]);
        }
        if (row - 1 >= 0)
        {
            temp.Add(Grid[col][row - 1]);
        }
        if (col - 1 >= 0)
        {
            temp.Add(Grid[col - 1][row]);
        }
        if (col + 1 < GridCols)
        {
            temp.Add(Grid[col + 1][row]);
        }

        // Check the four diagonal directions
        if (row + 1 < GridRows && col - 1 >= 0)
        {
            temp.Add(Grid[col - 1][row + 1]);
        }
        if (row + 1 < GridRows && col + 1 < GridCols)
        {
            temp.Add(Grid[col + 1][row + 1]);
        }
        if (row - 1 >= 0 && col - 1 >= 0)
        {
            temp.Add(Grid[col - 1][row - 1]);
        }
        if (row - 1 >= 0 && col + 1 < GridCols)
        {
            temp.Add(Grid[col + 1][row - 1]);
        }

        return temp;
    }
}

public class Cell
{
    // Change this depending on what the desired size is for each element in the grid
    public static int NODE_SIZE = 32;
    public Cell Parent;
    public Vector3 Position;
    public float Cost;

    public Cell(Vector3 pos, float cost = 1)
    {
        Parent = null;
        Position = pos;
        Cost = cost;
    }

    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        Cell other = (Cell)obj;
        return Position.X == other.Position.X && Position.Y == other.Position.Y;
    }

    public int GetHashCode(Cell obj)
    {
        return obj.Position.GetHashCode();
    }
}

public class CellEqualityComparer : IEqualityComparer<Cell>
{
    public bool Equals(Cell x, Cell y)
    {
        if (x == null && y == null)
            return true;
        else if (x == null || y == null)
            return false;
        else
            return x.Position.X == y.Position.X && x.Position.Y == y.Position.Y;
    }

    public int GetHashCode(Cell obj)
    {
        return obj.Position.GetHashCode();
    }
}
