using System.Reflection;
using CognitiveRuntime.Core.Contracts;

namespace CognitiveRuntime.Tests;

public sealed class TraceEventNamesTests
{
    [Fact]
    public void CoreEventNames_AreUniqueAndMatchTheSerializedContract()
    {
        var fields = typeof(TraceEventNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .ToArray();
        var values = fields
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expected = new[]
        {
            "artifact.reserved",
            "artifact.written",
            "budget.exceeded",
            "critic.completed",
            "critic.started",
            "eval.completed",
            "eval.started",
            "mode.loaded",
            "model.called",
            "model.completed",
            "model.failed",
            "node.cancelled",
            "node.completed",
            "node.failed",
            "node.started",
            "pattern.completed",
            "pattern.started",
            "phase.completed",
            "phase.started",
            "revision.completed",
            "revision.started",
            "run.cancelled",
            "run.completed",
            "run.failed",
            "run.finalized",
            "run.started",
            "stage.completed",
            "stage.started",
            "tool.called",
            "tool.completed",
            "tool.policy_evaluated"
        };

        Assert.Equal(expected, values);
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
    }
}
