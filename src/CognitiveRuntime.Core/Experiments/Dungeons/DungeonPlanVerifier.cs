using System.Text.Json;

namespace CognitiveRuntime.Core.Experiments.Dungeons;

public sealed class DungeonPlanVerifier
{
    public DungeonPlanEvaluation Evaluate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ParseFailure("Dungeon plan output was empty.");
        }

        DungeonPlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<DungeonPlan>(
                json.Trim(),
                DungeonJson.Options);
        }
        catch (JsonException exception)
        {
            return ParseFailure($"Dungeon plan JSON is invalid: {exception.Message}");
        }

        if (plan is null)
        {
            return ParseFailure("Dungeon plan JSON did not contain a plan.");
        }

        return new DungeonPlanEvaluation(plan, Verify(plan));
    }

    public DungeonVerificationReport Verify(DungeonPlan plan)
    {
        var checks = new List<DungeonVerificationCheck>();
        Add(checks, "schema.version", plan.SchemaVersion == 1,
            $"Expected schema version 1; found {plan.SchemaVersion}.");
        Add(checks, "title.present", !string.IsNullOrWhiteSpace(plan.Title),
            "A non-empty title is required.");
        Add(checks, "dimensions.bounded",
            plan.Width is >= 12 and <= 48 &&
            plan.Height is >= 12 and <= 48,
            $"Dimensions are {plan.Width}x{plan.Height}; each side must be 12-48.");
        Add(checks, "rooms.count", plan.Rooms.Count is >= 3 and <= 6,
            $"Found {plan.Rooms.Count} rooms; expected 3-6.");
        Add(checks, "rooms.ids", HaveUniqueNonBlankIds(plan.Rooms.Select(room => room.Id)),
            "Room IDs must be non-empty and unique.");
        Add(checks, "corridors.ids",
            HaveUniqueNonBlankIds(plan.Corridors.Select(corridor => corridor.Id)),
            "Corridor IDs must be non-empty and unique.");
        Add(checks, "doors.ids", HaveUniqueNonBlankIds(plan.Doors.Select(door => door.Id)),
            "Door IDs must be non-empty and unique.");
        Add(checks, "rooms.bounds", RoomsAreInBounds(plan),
            "Rooms must be at least 3x3 and remain inside the map with a one-cell border.");
        Add(checks, "rooms.overlap", !RoomsOverlap(plan.Rooms),
            "Room rectangles may not overlap.");

        var roomIds = plan.Rooms
            .Select(room => room.Id)
            .ToHashSet(StringComparer.Ordinal);
        Add(checks, "corridors.references",
            plan.Corridors.All(corridor =>
                roomIds.Contains(corridor.FromRoomId) &&
                roomIds.Contains(corridor.ToRoomId) &&
                !string.Equals(
                    corridor.FromRoomId,
                    corridor.ToRoomId,
                    StringComparison.Ordinal) &&
                corridor.Width is >= 1 and <= 2),
            "Corridors must connect two different declared rooms and be one or two cells wide.");
        Add(checks, "doors.references",
            plan.Doors.All(door =>
                roomIds.Contains(door.RoomAId) &&
                roomIds.Contains(door.RoomBId) &&
                !string.Equals(door.RoomAId, door.RoomBId, StringComparison.Ordinal)),
            "Doors must reference two different declared rooms.");
        Add(checks, "markers.cardinality", HasRequiredMarkers(plan.Markers),
            "Exactly one entrance, objective, and exit marker is required.");
        Add(checks, "markers.references",
            plan.Markers.All(marker => roomIds.Contains(marker.RoomId)),
            "Every marker must reference a declared room.");

        var graph = BuildGraph(plan);
        var entranceRoom = FindMarkerRoom(plan, DungeonMarkerKind.Entrance);
        var objectiveRoom = FindMarkerRoom(plan, DungeonMarkerKind.Objective);
        var exitRoom = FindMarkerRoom(plan, DungeonMarkerKind.Exit);
        var reachable = entranceRoom is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : FindReachable(graph, entranceRoom);

        Add(checks, "graph.connected",
            entranceRoom is not null &&
            plan.Rooms.All(room => reachable.Contains(room.Id)),
            "Every room must be reachable from the entrance.");
        Add(checks, "objective.reachable",
            objectiveRoom is not null && reachable.Contains(objectiveRoom),
            "The objective must be reachable from the entrance.");
        Add(checks, "exit.reachable",
            exitRoom is not null && reachable.Contains(exitRoom),
            "The exit must be reachable from the entrance.");

        var score = Score(plan, graph, entranceRoom, objectiveRoom);
        return new DungeonVerificationReport(checks, score);
    }

    private static DungeonPlanEvaluation ParseFailure(string details) =>
        new(
            null,
            new DungeonVerificationReport(
                [new DungeonVerificationCheck("json.valid", false, details)],
                []));

    private static void Add(
        ICollection<DungeonVerificationCheck> checks,
        string id,
        bool passed,
        string details) =>
        checks.Add(new DungeonVerificationCheck(
            id,
            passed,
            passed ? "Passed." : details));

    private static bool HaveUniqueNonBlankIds(IEnumerable<string> ids)
    {
        var values = ids.ToArray();
        return values.All(id => !string.IsNullOrWhiteSpace(id)) &&
               values.Distinct(StringComparer.Ordinal).Count() == values.Length;
    }

    private static bool RoomsAreInBounds(DungeonPlan plan) =>
        plan.Rooms.All(room =>
            room.Width >= 3 &&
            room.Height >= 3 &&
            room.X >= 1 &&
            room.Y >= 1 &&
            room.X + room.Width < plan.Width &&
            room.Y + room.Height < plan.Height);

    private static bool RoomsOverlap(IReadOnlyList<DungeonRoom> rooms)
    {
        for (var first = 0; first < rooms.Count; first++)
        {
            for (var second = first + 1; second < rooms.Count; second++)
            {
                if (Overlaps(rooms[first], rooms[second]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Overlaps(DungeonRoom first, DungeonRoom second) =>
        first.X < second.X + second.Width &&
        first.X + first.Width > second.X &&
        first.Y < second.Y + second.Height &&
        first.Y + first.Height > second.Y;

    private static bool HasRequiredMarkers(IReadOnlyList<DungeonMarker> markers) =>
        Enum.GetValues<DungeonMarkerKind>().All(
            kind => markers.Count(marker => marker.Kind == kind) == 1) &&
        markers.Count == 3;

    private static Dictionary<string, HashSet<string>> BuildGraph(DungeonPlan plan)
    {
        var graph = plan.Rooms.ToDictionary(
            room => room.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var corridor in plan.Corridors)
        {
            if (!graph.ContainsKey(corridor.FromRoomId) ||
                !graph.ContainsKey(corridor.ToRoomId))
            {
                continue;
            }

            graph[corridor.FromRoomId].Add(corridor.ToRoomId);
            graph[corridor.ToRoomId].Add(corridor.FromRoomId);
        }

        return graph;
    }

    private static HashSet<string> FindReachable(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        string start)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        if (!graph.ContainsKey(start))
        {
            return visited;
        }

        var pending = new Queue<string>();
        pending.Enqueue(start);
        visited.Add(start);
        while (pending.TryDequeue(out var current))
        {
            foreach (var neighbor in graph[current])
            {
                if (visited.Add(neighbor))
                {
                    pending.Enqueue(neighbor);
                }
            }
        }

        return visited;
    }

    private static string? FindMarkerRoom(
        DungeonPlan plan,
        DungeonMarkerKind kind) =>
        plan.Markers
            .Where(marker => marker.Kind == kind)
            .Select(marker => marker.RoomId)
            .FirstOrDefault();

    private static IReadOnlyList<DungeonScoreComponent> Score(
        DungeonPlan plan,
        IReadOnlyDictionary<string, HashSet<string>> graph,
        string? entrance,
        string? objective)
    {
        var shortestPath = entrance is null || objective is null
            ? []
            : FindShortestPath(graph, entrance, objective);
        var pathRooms = shortestPath.ToHashSet(StringComparer.Ordinal);
        var optionalBranch = plan.Rooms.Any(room => !pathRooms.Contains(room.Id));
        var hasLoop = plan.Corridors.Count >= plan.Rooms.Count;
        var objectiveDistinct = entrance is not null &&
                                objective is not null &&
                                !string.Equals(entrance, objective, StringComparison.Ordinal);
        var separated = shortestPath.Count >= 4;
        var roomVariety = plan.Rooms
            .Select(room => room.Width * room.Height)
            .Distinct()
            .Count() >= 2;

        return
        [
            Component("branch.optional", optionalBranch,
                "At least one room lies outside the shortest objective route."),
            Component("graph.loop", hasLoop,
                "The room graph contains a cycle."),
            Component("objective.distinct", objectiveDistinct,
                "The objective occupies a room distinct from the entrance."),
            Component("objective.separation", separated,
                "The objective is at least three graph edges from the entrance."),
            Component("rooms.variety", roomVariety,
                "Room areas include at least two sizes.")
        ];
    }

    private static DungeonScoreComponent Component(
        string id,
        bool awarded,
        string details) =>
        new(id, awarded ? 1 : 0, awarded ? details : $"Not awarded: {details}");

    private static IReadOnlyList<string> FindShortestPath(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        string start,
        string target)
    {
        if (!graph.ContainsKey(start) || !graph.ContainsKey(target))
        {
            return [];
        }

        var pending = new Queue<string>();
        var previous = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [start] = null
        };
        pending.Enqueue(start);

        while (pending.TryDequeue(out var current))
        {
            if (string.Equals(current, target, StringComparison.Ordinal))
            {
                break;
            }

            foreach (var neighbor in graph[current])
            {
                if (previous.TryAdd(neighbor, current))
                {
                    pending.Enqueue(neighbor);
                }
            }
        }

        if (!previous.ContainsKey(target))
        {
            return [];
        }

        var path = new List<string>();
        for (string? current = target; current is not null; current = previous[current])
        {
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
}
