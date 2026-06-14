using System.Net;
using System.Text;
using CognitiveRuntime.Core.Abstractions;
using CognitiveRuntime.Core.IO;
using CognitiveRuntime.Core.Persistence;
using CognitiveRuntime.Core.Views;

namespace CognitiveRuntime.Core.Artifacts;

public sealed class HtmlRunViewWriter : IRunViewWriter
{
    private readonly IArtifactStore _artifactStore;
    private readonly TimeProvider _timeProvider;

    public HtmlRunViewWriter(
        IArtifactStore? artifactStore = null,
        TimeProvider? timeProvider = null)
    {
        _artifactStore = artifactStore ?? new NullArtifactStore();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<string> WriteAsync(
        RunViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var outputPath = Path.Combine(
            viewModel.Run.OutputDirectory,
            "index.html");
        ArtifactWriter.EnsureWithinRunDirectory(
            viewModel.Run.OutputDirectory,
            outputPath);

        var content = Render(viewModel);
        await FilePersistence.WriteAllTextAtomicAsync(
            outputPath,
            content,
            cancellationToken);
        await _artifactStore.PutAsync(
            StoredRunArtifactFactory.CreateText(
                viewModel.Run.RunId,
                viewModel.Run.OutputDirectory,
                outputPath,
                "text/html; charset=utf-8",
                content,
                _timeProvider.GetUtcNow()),
            cancellationToken);

        return outputPath;
    }

    internal static string Render(RunViewModel viewModel)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"utf-8\">");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("  <title>")
            .Append(Encode(viewModel.Run.ModeName))
            .AppendLine(" run inspection</title>");
        html.AppendLine("  <style>");
        html.AppendLine(Css);
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <main>");
        html.AppendLine("    <header class=\"hero\">");
        html.AppendLine("      <p class=\"eyebrow\">Cognitive Runtime Lab</p>");
        html.Append("      <h1>")
            .Append(Encode(viewModel.Run.ModeName))
            .AppendLine(" run inspection</h1>");
        html.AppendLine("      <p>Read-only runtime artifact. This page cannot control or rerun the loop.</p>");
        html.Append("      <span class=\"status ")
            .Append(viewModel.Run.Status == "PASS" ? "pass" : "fail")
            .Append("\">")
            .Append(Encode(viewModel.Run.Status))
            .AppendLine("</span>");
        html.AppendLine("    </header>");

        AppendRunSection(html, viewModel.Run);
        AppendArtifactsSection(html, viewModel.Artifacts);
        AppendPatternSection(html, viewModel.Pattern);
        AppendModeSection(html, viewModel.Mode);
        AppendPhasesSection(html, viewModel.Phases);
        AppendToolPolicySection(html, viewModel.ToolPolicyDecisions);
        AppendEvalsSection(html, viewModel.Eval);
        AppendTraceSection(html, viewModel.Trace);

        html.AppendLine("  </main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static void AppendRunSection(StringBuilder html, RunViewRun run)
    {
        html.AppendLine("    <section>");
        html.AppendLine("      <h2>Run</h2>");
        html.AppendLine("      <dl class=\"facts\">");
        AppendFact(html, "Run ID", run.RunId);
        AppendFact(html, "Mode name", run.ModeName);
        AppendFact(html, "Input file", run.InputSource);
        AppendFact(html, "Run mode", run.ModelProvider);
        AppendFact(html, "Status", run.Status);
        AppendFact(html, "Start time", FormatTime(run.StartedAt));
        AppendFact(html, "End time", FormatTime(run.EndedAt));
        AppendFact(html, "Output directory", run.OutputDirectory);
        html.AppendLine("      </dl>");
        html.AppendLine("    </section>");
    }

    private static void AppendArtifactsSection(
        StringBuilder html,
        IReadOnlyList<RunViewArtifact> artifacts)
    {
        html.AppendLine("    <section>");
        html.AppendLine("      <h2>Artifacts</h2>");
        html.AppendLine("      <ul class=\"links\">");
        foreach (var artifact in artifacts)
        {
            html.Append("        <li><a href=\"")
                .Append(Encode(artifact.RelativePath))
                .Append("\">")
                .Append(Encode(artifact.Name))
                .AppendLine("</a></li>");
        }

        if (artifacts.Count == 0)
        {
            html.AppendLine("        <li>No artifacts were available.</li>");
        }

        html.AppendLine("      </ul>");
        html.AppendLine("    </section>");
    }

    private static void AppendPatternSection(
        StringBuilder html,
        RunViewPattern pattern)
    {
        html.AppendLine("    <section>");
        html.AppendLine("      <h2>Pattern</h2>");
        html.Append("      <h3>")
            .Append(Encode(pattern.Name))
            .AppendLine("</h3>");
        html.Append("      <p>")
            .Append(pattern.Nodes.Count)
            .Append(' ')
            .Append(Encode(pattern.UnitName))
            .Append(pattern.Nodes.Count == 1 ? string.Empty : "s")
            .AppendLine(" executed in runtime-defined order.</p>");
        html.Append("      <figure class=\"pattern-graph\" aria-label=\"")
            .Append(Encode($"{pattern.Name} pattern graph"))
            .AppendLine("\">");
        html.AppendLine("        <div class=\"pattern-flow\" role=\"list\">");

        for (var index = 0; index < pattern.Nodes.Count; index++)
        {
            var node = pattern.Nodes[index];
            if (index > 0)
            {
                html.AppendLine("          <span class=\"flow-arrow\" aria-hidden=\"true\">&rarr;</span>");
            }

            html.Append("          <article class=\"pattern-node\" role=\"listitem\" id=\"")
                .Append(Encode(node.Id))
                .AppendLine("\">");
            html.Append("            <p class=\"node-kind\">")
                .Append(Encode(node.Kind))
                .AppendLine("</p>");
            html.Append("            <h4>")
                .Append(Encode(node.Label))
                .AppendLine("</h4>");
            html.Append("            <p>")
                .Append(Encode(node.Detail))
                .AppendLine("</p>");
            html.AppendLine("          </article>");
        }

        html.AppendLine("        </div>");
        html.AppendLine("        <figcaption>Arrows show execution order. Directed relationships are listed below.</figcaption>");
        html.AppendLine("      </figure>");
        html.AppendLine("      <h3>Relationships</h3>");

        if (pattern.Edges.Count == 0)
        {
            html.Append("      <p>This pattern has no relationships between ")
                .Append(Encode(pattern.UnitName))
                .AppendLine("s.</p>");
        }
        else
        {
            var nodesById = pattern.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            html.AppendLine("      <ul class=\"pattern-relations\">");
            foreach (var edge in pattern.Edges)
            {
                html.Append("        <li><strong>")
                    .Append(Encode(nodesById[edge.FromNodeId].Label))
                    .Append("</strong> &rarr; <strong>")
                    .Append(Encode(nodesById[edge.ToNodeId].Label))
                    .Append("</strong>: ")
                    .Append(Encode(edge.Label))
                    .AppendLine("</li>");
            }

            html.AppendLine("      </ul>");
        }

        html.AppendLine("    </section>");
    }

    private static void AppendModeSection(StringBuilder html, RunViewMode mode)
    {
        html.AppendLine("    <section>");
        html.AppendLine("      <h2>Mode</h2>");
        html.Append("      <h3>")
            .Append(Encode(mode.Name))
            .AppendLine("</h3>");
        html.Append("      <p>")
            .Append(Encode(mode.Description))
            .AppendLine("</p>");
        html.Append("      <h3>")
            .Append(Encode(mode.SequenceLabel))
            .AppendLine("</h3>");
        html.AppendLine("      <ol>");
        foreach (var sequenceName in mode.SequenceNames)
        {
            html.Append("        <li>")
                .Append(Encode(sequenceName))
                .AppendLine("</li>");
        }

        html.AppendLine("      </ol>");
        html.Append("      <p><strong>Completion rule:</strong> ")
            .Append(Encode(mode.CompletionRule))
            .AppendLine("</p>");
        html.AppendLine("    </section>");
    }

    private static void AppendPhasesSection(
        StringBuilder html,
        IReadOnlyList<RunViewPhase> phases)
    {
        html.AppendLine("    <section>");
        html.AppendLine("      <h2>Phases</h2>");
        html.AppendLine("      <div class=\"cards\">");
        foreach (var phase in phases)
        {
            html.AppendLine("        <article class=\"card\">");
            html.Append("          <h3>")
                .Append(Encode(phase.Name))
                .AppendLine("</h3>");
            html.AppendLine("          <dl>");
            AppendFact(html, "Status", phase.Status, 10);
            AppendFact(html, "Model provider", phase.ModelProvider, 10);
            AppendFact(html, "Role", phase.Role, 10);
            AppendFact(html, "Tool calls requested", phase.ToolCallsRequested.ToString(), 10);
            AppendFact(html, "Tool calls executed", phase.ToolCallsExecuted.ToString(), 10);
            html.AppendLine("          </dl>");
            html.Append("          <p class=\"output-summary\">")
                .Append(Encode(phase.OutputSummary))
                .AppendLine("</p>");
            html.AppendLine("        </article>");
        }

        html.AppendLine("      </div>");
        html.AppendLine("    </section>");
    }

    private static void AppendToolPolicySection(
        StringBuilder html,
        IReadOnlyList<RunViewToolPolicyDecision> decisions)
    {
        html.AppendLine("    <section>");
        html.AppendLine("      <h2>Tool Policy</h2>");
        if (decisions.Count == 0)
        {
            html.AppendLine("      <p>No tool policy decisions were recorded for this run.</p>");
            html.AppendLine("    </section>");
            return;
        }

        html.AppendLine("      <div class=\"table-wrap\">");
        html.AppendLine("        <table>");
        html.AppendLine("          <thead><tr><th>Tool name</th><th>Requested action</th><th>Decision</th><th>Reason</th><th>Phase</th></tr></thead>");
        html.AppendLine("          <tbody>");
        foreach (var decision in decisions)
        {
            html.AppendLine("            <tr>");
            AppendCell(html, decision.ToolName);
            AppendCell(html, decision.RequestedAction);
            AppendCell(html, decision.Decision);
            AppendCell(html, decision.Reason);
            AppendCell(html, decision.Phase);
            html.AppendLine("            </tr>");
        }

        html.AppendLine("          </tbody>");
        html.AppendLine("        </table>");
        html.AppendLine("      </div>");
        html.AppendLine("    </section>");
    }

    private static void AppendEvalsSection(StringBuilder html, RunViewEval eval)
    {
        html.AppendLine("    <section>");
        html.AppendLine("      <h2>Evals</h2>");
        html.Append("      <p><strong>Overall:</strong> ")
            .Append(eval.Passed ? "PASS" : "FAIL")
            .AppendLine("</p>");
        html.AppendLine("      <ul class=\"checks\">");
        foreach (var check in eval.Checks)
        {
            html.Append("        <li><span class=\"check ")
                .Append(check.Passed ? "pass" : "fail")
                .Append("\">")
                .Append(check.Passed ? "PASS" : "FAIL")
                .Append("</span> <strong>")
                .Append(Encode(check.Name))
                .Append("</strong>: ")
                .Append(Encode(check.Details))
                .AppendLine("</li>");
        }

        html.AppendLine("      </ul>");
        html.AppendLine("    </section>");
    }

    private static void AppendTraceSection(StringBuilder html, RunViewTrace trace)
    {
        html.AppendLine("    <section>");
        html.AppendLine("      <h2>Trace</h2>");
        html.Append("      <p><a href=\"")
            .Append(Encode(trace.RelativePath))
            .Append("\">Open trace.json</a></p>")
            .AppendLine();
        html.Append("      <p>")
            .Append(trace.EventCount)
            .AppendLine(" runtime events recorded.</p>");
        html.AppendLine("    </section>");
    }

    private static void AppendFact(
        StringBuilder html,
        string label,
        string value,
        int indentation = 8)
    {
        var spaces = new string(' ', indentation);
        html.Append(spaces)
            .Append("<div><dt>")
            .Append(Encode(label))
            .Append("</dt><dd>")
            .Append(Encode(value))
            .AppendLine("</dd></div>");
    }

    private static void AppendCell(StringBuilder html, string value)
    {
        html.Append("              <td>")
            .Append(Encode(value))
            .AppendLine("</td>");
    }

    private static string FormatTime(DateTimeOffset? value) =>
        value?.ToString("O") ?? "Not available";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private const string Css = """
        :root {
          color-scheme: light;
          font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
          color: #1f2933;
          background: #eef2f5;
        }
        * { box-sizing: border-box; }
        body { margin: 0; line-height: 1.55; }
        main { width: min(1100px, calc(100% - 32px)); margin: 32px auto 64px; }
        .hero, section {
          background: #ffffff;
          border: 1px solid #d9e2ec;
          border-radius: 12px;
          box-shadow: 0 8px 24px rgba(31, 41, 51, 0.06);
        }
        .hero { padding: 32px; margin-bottom: 20px; }
        section { padding: 24px; margin-top: 20px; }
        h1, h2, h3 { color: #102a43; line-height: 1.2; }
        h1 { margin: 4px 0 8px; }
        h2 { margin-top: 0; border-bottom: 1px solid #e6edf3; padding-bottom: 10px; }
        .eyebrow { margin: 0; color: #486581; font-size: 0.8rem; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; }
        .status, .check { display: inline-block; border-radius: 999px; font-size: 0.75rem; font-weight: 800; padding: 4px 10px; }
        .pass { color: #0b6b3a; background: #d9fbe8; }
        .fail { color: #9b1c1c; background: #fee2e2; }
        .facts { margin: 0; }
        .facts > div, article dl > div { display: grid; gap: 16px; padding: 8px 0; border-bottom: 1px solid #edf2f7; }
        .facts > div { grid-template-columns: minmax(140px, 220px) 1fr; }
        article dl > div { grid-template-columns: minmax(0, 1fr) minmax(100px, 1fr); }
        dt { color: #52616b; font-weight: 700; }
        dd { margin: 0; overflow-wrap: anywhere; }
        a { color: #0b69a3; }
        .links, .checks { padding-left: 22px; }
        .links li, .checks li { margin: 8px 0; }
        .pattern-graph { margin: 20px 0; }
        .pattern-flow { display: flex; align-items: stretch; gap: 12px; overflow-x: auto; padding: 4px 2px 12px; }
        .pattern-node { flex: 1 0 210px; max-width: 300px; border: 2px solid #829ab1; border-radius: 10px; padding: 16px; background: #f8fbfd; }
        .pattern-node h4 { margin: 4px 0 8px; color: #102a43; font-size: 1.05rem; }
        .pattern-node p { margin: 0; }
        .node-kind { color: #486581; font-size: 0.75rem; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
        .flow-arrow { align-self: center; color: #486581; font-size: 1.75rem; font-weight: 800; }
        figcaption { color: #52616b; font-size: 0.9rem; }
        .pattern-relations { padding-left: 22px; }
        .pattern-relations li { margin: 8px 0; }
        .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; }
        .card { border: 1px solid #d9e2ec; border-radius: 10px; padding: 18px; }
        .card h3 { margin-top: 0; }
        .output-summary { background: #f5f7fa; border-radius: 8px; padding: 12px; overflow-wrap: anywhere; }
        .table-wrap { overflow-x: auto; }
        table { width: 100%; border-collapse: collapse; }
        th, td { border-bottom: 1px solid #d9e2ec; padding: 10px; text-align: left; vertical-align: top; }
        th { color: #334e68; background: #f5f7fa; }
        @media (max-width: 640px) {
          main { width: min(100% - 20px, 1100px); margin-top: 10px; }
          .hero, section { padding: 18px; }
          .facts > div, article dl > div { grid-template-columns: 1fr; gap: 2px; }
          .pattern-flow { flex-direction: column; }
          .pattern-node { flex-basis: auto; max-width: none; }
          .flow-arrow { transform: rotate(90deg); }
        }
        """;
}
