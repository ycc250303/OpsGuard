using FluentAssertions;
using OpsGuard.App.Services;

namespace OpsGuard.Tests;

public class StreamingMarkdownTests
{
    [Fact]
    public void BuildStreamingMarkdown_ShowsRunningThenCompletedStages()
    {
        var chunks = new List<DiagnosticChunk>
        {
            new("Collector", DiagnosticChunkPhase.Started, null),
            new("Collector", DiagnosticChunkPhase.Completed, "CPU 12%"),
            new("Analyzer", DiagnosticChunkPhase.Started, null)
        };

        var markdown = OpsGuardOrchestrator.BuildStreamingMarkdown(chunks);

        markdown.Should().Contain("## Collector");
        markdown.Should().Contain("CPU 12%");
        markdown.Should().Contain("## Analyzer");
        markdown.Should().Contain("*运行中…*");
        markdown.Should().NotContain("CPU 12%*运行中");
    }
}
