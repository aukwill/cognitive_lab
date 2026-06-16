using System.Text.Json;
using CognitiveRuntime.Core.Artifacts;
using CognitiveRuntime.Core.Experiments.Dungeons;
using CognitiveRuntime.Core.Models;
using CognitiveRuntime.Core.Persistence;
using CognitiveRuntime.Core.Runtime;
using CognitiveRuntime.Core.Tracing;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Tests;

public sealed class DungeonExperimentTests
{
    private static DungeonPlan CreateBaselinePlan() => new(
        SchemaVersion: 1,
        Title: "The Ashen Reliquary",
        Width: 24,
        Height: 16,
        Rooms:
        [
            new DungeonRoom("entrance", 1, 6, 4, 4),
            new DungeonRoom("hall", 7, 6, 4, 4),
            new DungeonRoom("vault", 13, 6, 4, 4),
            new DungeonRoom("sanctum", 19, 6, 4, 4)
        ],
        Corridors:
        [
            new DungeonCorridor("corridor-entrance-hall", "entrance", "hall", 1),
            new DungeonCorridor("corridor-hall-vault", "hall", "vault", 1),
            new DungeonCorridor("corridor-vault-sanctum", "vault", "sanctum", 1)
        ],
        Doors:
        [
            new DungeonDoor("door-sanctum", "vault", "sanctum", DungeonDoorKind.Guarded)
        ],
        Markers:
        [
            new DungeonMarker(DungeonMarkerKind.Entrance, "entrance"),
            new DungeonMarker(DungeonMarkerKind.Objective, "sanctum"),
            new DungeonMarker(DungeonMarkerKind.Exit, "entrance")
        ]);

    [Fact]
    public void Verify_BaselinePlan_IsFeasible()
    {
        var verifier = new DungeonPlanVerifier();

        var report = verifier.Verify(CreateBaselinePlan());

        Assert.True(report.Feasible, string.Join(
            "; ",
            report.Checks.Where(check => !check.Passed).Select(check => check.Id)));
    }

    [Fact]
    public void Verify_DisconnectedObjective_FailsObjectiveReachableCheck()
    {
        var plan = CreateBaselinePlan() with
        {
            Corridors =
            [
                new DungeonCorridor("corridor-entrance-hall", "entrance", "hall", 1),
                new DungeonCorridor("corridor-hall-vault", "hall", "vault", 1)
            ]
        };

        var report = new DungeonPlanVerifier().Verify(plan);

        Assert.False(report.Feasible);
        AssertCheckFailed(report, "objective.reachable");
    }

    [Fact]
    public void Verify_OverlappingRooms_FailsRoomsOverlapCheck()
    {
        var plan = CreateBaselinePlan();
        var overlapping = plan with
        {
            Rooms =
            [
                .. plan.Rooms.Take(plan.Rooms.Count - 1),
                plan.Rooms[^1] with { X = plan.Rooms[0].X, Y = plan.Rooms[0].Y }
            ]
        };

        var report = new DungeonPlanVerifier().Verify(overlapping);

        Assert.False(report.Feasible);
        AssertCheckFailed(report, "rooms.overlap");
    }

    [Fact]
    public void Verify_CorridorReferencingMissingRoom_FailsCorridorReferencesCheck()
    {
        var plan = CreateBaselinePlan() with
        {
            Corridors =
            [
                new DungeonCorridor("corridor-entrance-hall", "entrance", "hall", 1),
                new DungeonCorridor("corridor-hall-vault", "hall", "vault", 1),
                new DungeonCorridor("corridor-vault-sanctum", "vault", "missing-room", 1)
            ]
        };

        var report = new DungeonPlanVerifier().Verify(plan);

        Assert.False(report.Feasible);
        AssertCheckFailed(report, "corridors.references");
    }

    [Fact]
    public void Verify_DuplicateEntranceMarker_FailsMarkerCardinalityCheck()
    {
        var plan = CreateBaselinePlan() with
        {
            Markers =
            [
                new DungeonMarker(DungeonMarkerKind.Entrance, "entrance"),
                new DungeonMarker(DungeonMarkerKind.Entrance, "hall"),
                new DungeonMarker(DungeonMarkerKind.Objective, "sanctum"),
                new DungeonMarker(DungeonMarkerKind.Exit, "entrance")
            ]
        };

        var report = new DungeonPlanVerifier().Verify(plan);

        Assert.False(report.Feasible);
        AssertCheckFailed(report, "markers.cardinality");
    }

    [Fact]
    public void Verify_RoomOutsideMapBounds_FailsRoomsBoundsCheck()
    {
        var plan = CreateBaselinePlan();
        var outOfBounds = plan with
        {
            Rooms =
            [
                .. plan.Rooms.Take(plan.Rooms.Count - 1),
                plan.Rooms[^1] with { X = plan.Width - 1 }
            ]
        };

        var report = new DungeonPlanVerifier().Verify(outOfBounds);

        Assert.False(report.Feasible);
        AssertCheckFailed(report, "rooms.bounds");
    }

    [Fact]
    public void Evaluate_InvalidJson_ProducesParseFailure()
    {
        var evaluation = new DungeonPlanVerifier().Evaluate("not json");

        Assert.Null(evaluation.Plan);
        Assert.False(evaluation.Report.Feasible);
        AssertCheckFailed(evaluation.Report, "json.valid");
    }

    [Fact]
    public void Compile_BaselinePlan_ProducesLayersWithExactCellCounts()
    {
        var plan = CreateBaselinePlan();

        var compiled = new DungeonCompiler().Compile(plan);

        var expectedCells = plan.Width * plan.Height;
        foreach (var csv in ExtractTmxLayerCsv(compiled.Tmx))
        {
            var values = csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Equal(expectedCells, values.Length);
        }

        Assert.Contains("[header]", compiled.FlareMap);
        Assert.Contains($"width={plan.Width}", compiled.FlareMap);
        Assert.Contains($"height={plan.Height}", compiled.FlareMap);
    }

    private static IEnumerable<string> ExtractTmxLayerCsv(string tmx)
    {
        var document = System.Xml.Linq.XDocument.Parse(tmx);
        foreach (var data in document.Descendants("data"))
        {
            yield return data.Value;
        }
    }

    private static void AssertCheckFailed(DungeonVerificationReport report, string checkId) =>
        Assert.Contains(
            report.Checks,
            check => check.Id == checkId && !check.Passed);

    [Fact]
    public async Task RunAsync_WithMockProvider_ProducesFeasibleWinnerAndArtifacts()
    {
        using var workspace = new TestWorkspace();
        var experimentsRoot = Path.Combine(workspace.Root, "experiments");
        var promptsDirectory = Path.Combine(experimentsRoot, "dungeon-builder", "prompts");
        Directory.CreateDirectory(promptsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(promptsDirectory, "propose.md"),
            "Propose one DungeonPlan JSON object.");
        await File.WriteAllTextAsync(
            Path.Combine(promptsDirectory, "revise.md"),
            "Revise the DungeonPlan JSON using the verifier report.");

        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero));
        var artifactStore = new NullArtifactStore();
        var runner = new DungeonExperimentRunner(
            new ModelClientFactory([new MockModelClient()]),
            new ArtifactWriter(timeProvider, artifactStore),
            new DungeonArtifactWriter(artifactStore, timeProvider),
            new JsonTraceSessionFactory(timeProvider, artifactStore),
            new FixedRunIdGenerator("dungeon-run"),
            new DungeonExperimentDefinitionLoader(),
            new DungeonPlanVerifier(),
            new DungeonCompiler(),
            timeProvider);

        var result = await runner.RunAsync(
            new DungeonExperimentRequest(
                "Build a compact crypt with an entrance, three to six rooms, " +
                "one optional branch, one locked or guarded objective room, " +
                "and a reachable exit.",
                "mock",
                workspace.OutputRoot,
                experimentsRoot));

        Assert.Equal(RunOutcome.Success, result.Run.Outcome);
        Assert.Equal(1, result.WinnerIndex);
        Assert.NotNull(result.TmxPath);
        Assert.True(File.Exists(result.TmxPath));
        Assert.NotNull(result.FlareMapPath);
        Assert.True(File.Exists(result.FlareMapPath));
        Assert.NotNull(result.FlareModPath);
        Assert.True(File.Exists(Path.Combine(result.FlareModPath!, "settings.txt")));

        var winnerVerificationPath = Path.Combine(
            result.Run.OutputDirectory, "winner", "verification.json");
        using var verificationDocument = JsonDocument.Parse(
            await File.ReadAllTextAsync(winnerVerificationPath));
        var checks = verificationDocument.RootElement.GetProperty("checks");
        Assert.All(
            checks.EnumerateArray(),
            check => Assert.True(check.GetProperty("passed").GetBoolean()));
    }
}
