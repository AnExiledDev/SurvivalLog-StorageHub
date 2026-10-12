using System;

namespace StorageHub.Core;

/// <summary>Which cells of a bag are taken while a layout is built.</summary>
internal sealed class Occupancy
{
    private readonly GridSize _grid;
    private readonly bool[,] _taken;

    public Occupancy(GridSize grid) : this(grid, new bool[Math.Max(0, grid.Cols), Math.Max(0, grid.Rows)])
    {
    }

    private Occupancy(GridSize grid, bool[,] taken)
    {
        _grid = grid;
        _taken = taken;
    }

    public Occupancy Copy() => new(_grid, (bool[,])_taken.Clone());

    /// <summary>True when a w by h item at this cell lies inside the bag and covers no taken cell.</summary>
    public bool IsFree(Cell at, int w, int h)
    {
        if (w <= 0 || h <= 0 || at.X < 0 || at.Y < 0 || at.X + w > _grid.Cols || at.Y + h > _grid.Rows) return false;

        for (var x = at.X; x < at.X + w; x++)
        {
            for (var y = at.Y; y < at.Y + h; y++)
            {
                if (_taken[x, y]) return false;
            }
        }

        return true;
    }

    public void Take(Cell at, int w, int h)
    {
        for (var x = at.X; x < at.X + w; x++)
        {
            for (var y = at.Y; y < at.Y + h; y++) _taken[x, y] = true;
        }
    }

    /// <summary>The game's own search: the first free spot reading row by row from the top-left, inside the first cols by rows cells.</summary>
    public Cell? FirstFit(int w, int h, int cols, int rows)
    {
        for (var y = 0; y + h <= rows; y++)
        {
            for (var x = 0; x + w <= cols; x++)
            {
                if (IsFree(new Cell(x, y), w, h)) return new Cell(x, y);
            }
        }

        return null;
    }

    /// <summary>The same search run backwards, so the first spot tried is the bottom-right corner.</summary>
    public Cell? LastFit(int w, int h)
    {
        for (var y = _grid.Rows - h; y >= 0; y--)
        {
            for (var x = _grid.Cols - w; x >= 0; x--)
            {
                if (IsFree(new Cell(x, y), w, h)) return new Cell(x, y);
            }
        }

        return null;
    }
}
