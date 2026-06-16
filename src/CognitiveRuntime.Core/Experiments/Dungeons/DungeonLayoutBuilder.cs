namespace CognitiveRuntime.Core.Experiments.Dungeons;

internal static class DungeonLayoutBuilder
{
    public static DungeonLayout Build(DungeonPlan plan)
    {
        var walkable = new bool[plan.Width, plan.Height];
        var centers = plan.Rooms.ToDictionary(
            room => room.Id,
            GetCenter,
            StringComparer.Ordinal);

        foreach (var room in plan.Rooms)
        {
            FillRectangle(
                walkable,
                room.X,
                room.Y,
                room.Width,
                room.Height);
        }

        foreach (var corridor in plan.Corridors)
        {
            if (!centers.TryGetValue(corridor.FromRoomId, out var from) ||
                !centers.TryGetValue(corridor.ToRoomId, out var to))
            {
                continue;
            }

            CarveHorizontal(walkable, from.X, to.X, from.Y, corridor.Width);
            CarveVertical(walkable, from.Y, to.Y, to.X, corridor.Width);
        }

        return new DungeonLayout(
            plan.Width,
            plan.Height,
            walkable,
            centers);
    }

    private static DungeonPoint GetCenter(DungeonRoom room) =>
        new(
            room.X + (room.Width / 2),
            room.Y + (room.Height / 2));

    private static void FillRectangle(
        bool[,] grid,
        int x,
        int y,
        int width,
        int height)
    {
        for (var column = x; column < x + width; column++)
        {
            for (var row = y; row < y + height; row++)
            {
                SetWalkable(grid, column, row);
            }
        }
    }

    private static void CarveHorizontal(
        bool[,] grid,
        int fromX,
        int toX,
        int y,
        int width)
    {
        var start = Math.Min(fromX, toX);
        var end = Math.Max(fromX, toX);
        for (var x = start; x <= end; x++)
        {
            for (var offset = 0; offset < width; offset++)
            {
                SetWalkable(grid, x, y + offset);
            }
        }
    }

    private static void CarveVertical(
        bool[,] grid,
        int fromY,
        int toY,
        int x,
        int width)
    {
        var start = Math.Min(fromY, toY);
        var end = Math.Max(fromY, toY);
        for (var y = start; y <= end; y++)
        {
            for (var offset = 0; offset < width; offset++)
            {
                SetWalkable(grid, x + offset, y);
            }
        }
    }

    private static void SetWalkable(bool[,] grid, int x, int y)
    {
        if (x >= 0 &&
            y >= 0 &&
            x < grid.GetLength(0) &&
            y < grid.GetLength(1))
        {
            grid[x, y] = true;
        }
    }
}
