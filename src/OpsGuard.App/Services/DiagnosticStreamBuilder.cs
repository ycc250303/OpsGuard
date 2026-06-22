using System.Text;
using OpsGuard.Core.Streaming;

namespace OpsGuard.App.Services;

internal sealed class DiagnosticStreamBuilder
{
    private static readonly string[] StageOrder = ["Collector", "Analyzer", "Advisor"];

    private readonly Dictionary<string, StringBuilder> _stageContent = new(StringComparer.Ordinal);
    private readonly HashSet<string> _startedStages = new(StringComparer.Ordinal);

    public void Apply(DiagnosticChunk chunk)
    {
        if (!_stageContent.TryGetValue(chunk.Stage, out var buffer))
        {
            buffer = new StringBuilder();
            _stageContent[chunk.Stage] = buffer;
        }

        switch (chunk.Phase)
        {
            case DiagnosticChunkPhase.Started:
                _startedStages.Add(chunk.Stage);
                break;

            case DiagnosticChunkPhase.Delta when !string.IsNullOrEmpty(chunk.Content):
                buffer.Append(chunk.Content);
                break;

            case DiagnosticChunkPhase.ToolInvoking when !string.IsNullOrWhiteSpace(chunk.ToolName):
                buffer.AppendLine();
                buffer.AppendLine($"> 🔧 调用 `{chunk.ToolName}` …");
                break;

            case DiagnosticChunkPhase.ToolCompleted when !string.IsNullOrWhiteSpace(chunk.ToolName):
                buffer.AppendLine($"> ✅ `{chunk.ToolName}` 完成");
                buffer.AppendLine();
                break;

            case DiagnosticChunkPhase.Completed when !string.IsNullOrWhiteSpace(chunk.Content):
                if (buffer.Length == 0)
                {
                    buffer.Append(chunk.Content.Trim());
                }

                break;
        }
    }

    public string BuildMarkdown(bool streaming)
    {
        var sb = new StringBuilder();

        foreach (var stage in StageOrder)
        {
            if (!_startedStages.Contains(stage))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine($"## {stage}");
            sb.AppendLine();

            if (_stageContent.TryGetValue(stage, out var content) && content.Length > 0)
            {
                sb.Append(content);
            }
            else if (streaming)
            {
                sb.Append("*等待输出…*");
            }
            else
            {
                sb.Append("_（无输出）_");
            }
        }

        return sb.ToString().Trim();
    }
}
