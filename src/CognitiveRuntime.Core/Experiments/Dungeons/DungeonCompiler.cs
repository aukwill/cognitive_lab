using System.Text;
using System.Xml.Linq;

namespace CognitiveRuntime.Core.Experiments.Dungeons;

public sealed class DungeonCompiler
{
    public CompiledDungeon Compile(DungeonPlan plan)
    {
        var layout = DungeonLayoutBuilder.Build(plan);
        return new CompiledDungeon(
            RenderTmx(plan, layout),
            RenderFlareMap(plan, layout),
            RenderSpawnMap(plan, layout),
            """
            description=Verifier-guided dungeon builder output.
            requires=fantasycore
            game=flare-game
            """,
            """
            # Attribution

            This generated mod targets the Flare Engine and the `fantasycore`
            game data from the Flare Game project.

            Flare Engine: GPL-3.0-or-later

            Flare Game art and data: CC-BY-SA-3.0-or-later

            https://github.com/flareteam/flare-engine
            https://github.com/flareteam/flare-game
            """,
            RenderAscii(plan, layout));
    }

    private static string RenderTmx(DungeonPlan plan, DungeonLayout layout)
    {
        var map = new XElement(
            "map",
            new XAttribute("version", "1.10"),
            new XAttribute("tiledversion", "1.11.2"),
            new XAttribute("orientation", "isometric"),
            new XAttribute("renderorder", "right-down"),
            new XAttribute("width", plan.Width),
            new XAttribute("height", plan.Height),
            new XAttribute("tilewidth", 192),
            new XAttribute("tileheight", 96),
            new XAttribute("infinite", 0),
            new XElement(
                "properties",
                Property("title", plan.Title),
                Property("tileset", "tilesetdefs/tileset_dungeon.txt")),
            new XElement(
                "tileset",
                new XAttribute("firstgid", 1),
                new XAttribute("name", "collision"),
                new XAttribute("tilewidth", 192),
                new XAttribute("tileheight", 96),
                new XAttribute("tilecount", 15),
                new XAttribute("columns", 15),
                new XElement(
                    "image",
                    new XAttribute("source", "assets/tiled_collision.png"),
                    new XAttribute("width", 2880),
                    new XAttribute("height", 96))),
            new XElement(
                "tileset",
                new XAttribute("firstgid", 16),
                new XAttribute("name", "dungeon"),
                new XAttribute("tilewidth", 192),
                new XAttribute("tileheight", 384),
                new XAttribute("tilecount", 240),
                new XAttribute("columns", 16),
                new XElement(
                    "image",
                    new XAttribute("source", "assets/dungeon.png"),
                    new XAttribute("width", 3072),
                    new XAttribute("height", 5760))),
            TileLayer(
                "background",
                plan.Width,
                plan.Height,
                CreateLayerValues(
                    layout,
                    walkableValue: 16,
                    blockedValue: 0)),
            TileLayer(
                "object",
                plan.Width,
                plan.Height,
                Enumerable.Repeat(0, plan.Width * plan.Height)),
            TileLayer(
                "collision",
                plan.Width,
                plan.Height,
                CreateLayerValues(
                    layout,
                    walkableValue: 0,
                    blockedValue: 1)),
            MarkerObjectGroup(plan, layout));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            map);
        return document.ToString();
    }

    private static XElement Property(string name, string value) =>
        new(
            "property",
            new XAttribute("name", name),
            new XAttribute("value", value));

    private static XElement TileLayer(
        string name,
        int width,
        int height,
        IEnumerable<int> values) =>
        new(
            "layer",
            new XAttribute("name", name),
            new XAttribute("width", width),
            new XAttribute("height", height),
            new XElement(
                "data",
                new XAttribute("encoding", "csv"),
                Environment.NewLine +
                RenderCsv(values, width) +
                Environment.NewLine));

    private static XElement MarkerObjectGroup(
        DungeonPlan plan,
        DungeonLayout layout)
    {
        var group = new XElement("objectgroup", new XAttribute("name", "markers"));
        var objectId = 1;
        foreach (var marker in plan.Markers.OrderBy(marker => marker.Kind))
        {
            var point = layout.RoomCenters[marker.RoomId];
            group.Add(
                new XElement(
                    "object",
                    new XAttribute("id", objectId++),
                    new XAttribute("name", marker.Kind.ToString().ToLowerInvariant()),
                    new XAttribute("type", "marker"),
                    new XAttribute("x", point.X * 96),
                    new XAttribute("y", point.Y * 96),
                    new XAttribute("width", 96),
                    new XAttribute("height", 96),
                    new XElement(
                        "properties",
                        Property("kind", marker.Kind.ToString().ToLowerInvariant()),
                        Property("roomId", marker.RoomId))));
        }

        return group;
    }

    private static string RenderFlareMap(DungeonPlan plan, DungeonLayout layout)
    {
        var entrance = GetMarkerPoint(plan, layout, DungeonMarkerKind.Entrance);
        var objective = GetMarkerPoint(plan, layout, DungeonMarkerKind.Objective);
        var exit = GetMarkerPoint(plan, layout, DungeonMarkerKind.Exit);
        var builder = new StringBuilder()
            .AppendLine("[header]")
            .Append("width=").AppendLine(plan.Width.ToString())
            .Append("height=").AppendLine(plan.Height.ToString())
            .AppendLine("tilewidth=192")
            .AppendLine("tileheight=96")
            .AppendLine("orientation=isometric")
            .AppendLine("music=music/safe_room_theme.ogg")
            .AppendLine("tileset=tilesetdefs/tileset_dungeon.txt")
            .Append("title=").AppendLine(SanitizeValue(plan.Title))
            .AppendLine()
            .AppendLine("[layer]")
            .AppendLine("type=background")
            .AppendLine("data=")
            .AppendLine(RenderCsv(
                CreateLayerValues(layout, walkableValue: 16, blockedValue: 0),
                plan.Width))
            .AppendLine()
            .AppendLine("[layer]")
            .AppendLine("type=object")
            .AppendLine("data=")
            .AppendLine(RenderCsv(
                Enumerable.Repeat(0, plan.Width * plan.Height),
                plan.Width))
            .AppendLine()
            .AppendLine("[layer]")
            .AppendLine("type=collision")
            .AppendLine("data=")
            .AppendLine(RenderCsv(
                CreateLayerValues(layout, walkableValue: 0, blockedValue: 1),
                plan.Width))
            .AppendLine()
            .AppendLine("[event]")
            .AppendLine("# objective")
            .AppendLine("type=event")
            .Append("location=")
            .Append(objective.X).Append(',').Append(objective.Y)
            .AppendLine(",1,1")
            .AppendLine("activate=on_trigger")
            .AppendLine("cooldown=1s")
            .AppendLine("hotspot=location")
            .AppendLine("msg=Objective reached.")
            .AppendLine("repeat=true")
            .AppendLine("tooltip=Objective")
            .AppendLine()
            .AppendLine("[event]")
            .AppendLine("# exit")
            .AppendLine("type=event")
            .Append("location=")
            .Append(exit.X).Append(',').Append(exit.Y)
            .AppendLine(",1,1")
            .AppendLine("activate=on_trigger")
            .AppendLine("cooldown=1s")
            .AppendLine("hotspot=location")
            .AppendLine("msg=Exit reached.")
            .AppendLine("repeat=true")
            .AppendLine("tooltip=Exit")
            .AppendLine()
            .AppendLine("# entrance")
            .Append("# location=").Append(entrance.X).Append(',').AppendLine(entrance.Y.ToString());

        return builder.ToString();
    }

    private static string RenderSpawnMap(DungeonPlan plan, DungeonLayout layout)
    {
        var entrance = GetMarkerPoint(plan, layout, DungeonMarkerKind.Entrance);
        return $"""
            # Automatically loaded when a new game starts.

            [header]
            width=1
            height=1
            location=0,0,3

            [event]
            type=event
            location=0,0,1,1
            activate=on_load
            intermap=maps/dungeon.txt,{entrance.X},{entrance.Y}
            """;
    }

    private static DungeonPoint GetMarkerPoint(
        DungeonPlan plan,
        DungeonLayout layout,
        DungeonMarkerKind kind)
    {
        var roomId = plan.Markers.Single(marker => marker.Kind == kind).RoomId;
        return layout.RoomCenters[roomId];
    }

    private static IEnumerable<int> CreateLayerValues(
        DungeonLayout layout,
        int walkableValue,
        int blockedValue)
    {
        for (var y = 0; y < layout.Height; y++)
        {
            for (var x = 0; x < layout.Width; x++)
            {
                yield return layout.Walkable[x, y]
                    ? walkableValue
                    : blockedValue;
            }
        }
    }

    private static string RenderCsv(IEnumerable<int> values, int width)
    {
        var rows = values
            .Select((value, index) => new { value, index })
            .GroupBy(item => item.index / width)
            .Select(row => string.Join(',', row.Select(item => item.value)) + ",");
        return string.Join(Environment.NewLine, rows);
    }

    private static string RenderAscii(DungeonPlan plan, DungeonLayout layout)
    {
        var markerPoints = new Dictionary<DungeonPoint, DungeonMarkerKind>();
        foreach (var marker in plan.Markers.OrderBy(marker => marker.Kind))
        {
            markerPoints.TryAdd(layout.RoomCenters[marker.RoomId], marker.Kind);
        }

        var builder = new StringBuilder();
        for (var y = 0; y < layout.Height; y++)
        {
            for (var x = 0; x < layout.Width; x++)
            {
                var point = new DungeonPoint(x, y);
                if (markerPoints.TryGetValue(point, out var marker))
                {
                    builder.Append(marker switch
                    {
                        DungeonMarkerKind.Entrance => 'E',
                        DungeonMarkerKind.Objective => 'O',
                        DungeonMarkerKind.Exit => 'X',
                        _ => '?'
                    });
                }
                else
                {
                    builder.Append(layout.Walkable[x, y] ? '.' : '#');
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string SanitizeValue(string value) =>
        string.Join(
            " ",
            value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries));
}
