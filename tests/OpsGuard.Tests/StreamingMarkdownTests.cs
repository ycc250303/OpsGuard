using FluentAssertions;
using OpsGuard.App.Services;
using OpsGuard.Core.Streaming;

namespace OpsGuard.Tests;

public class StreamingMarkdownTests
{
    [Fact]
    public void BuildMarkdown_StreamsDeltasAndTools()
    {
        var builder = new DiagnosticStreamBuilder();

        builder.Apply(new DiagnosticChunk("Collector", DiagnosticChunkPhase.Started, null));
        builder.Apply(new DiagnosticChunk("Collector", DiagnosticChunkPhase.ToolInvoking, null, "Host.GetHostMetrics"));
        builder.Apply(new DiagnosticChunk("Collector", DiagnosticChunkPhase.ToolCompleted, null, "Host.GetHostMetrics"));
        builder.Apply(new DiagnosticChunk("Collector", DiagnosticChunkPhase.Delta, "CPU "));
        builder.Apply(new DiagnosticChunk("Collector", DiagnosticChunkPhase.Delta, "12%"));
        builder.Apply(new DiagnosticChunk("Collector", DiagnosticChunkPhase.Completed, "CPU 12%"));
        builder.Apply(new DiagnosticChunk("Analyzer", DiagnosticChunkPhase.Started, null));
        builder.Apply(new DiagnosticChunk("Analyzer", DiagnosticChunkPhase.Delta, "初步"));

        var markdown = builder.BuildMarkdown(streaming: true);

        markdown.Should().Contain("## Collector");
        markdown.Should().Contain("CPU 12%");
        markdown.Should().Contain("Host.GetHostMetrics");
        markdown.Should().Contain("## Analyzer");
        markdown.Should().Contain("初步");
    }

    [Fact]
    public void BuildMarkdown_ReusesCachedSnapshotUntilContentChanges()
    {
        var builder = new DiagnosticStreamBuilder();

        builder.Apply(new DiagnosticChunk("Collector", DiagnosticChunkPhase.Started, null));
        builder.Apply(new DiagnosticChunk("Collector", DiagnosticChunkPhase.Delta, "CPU 12%"));

        var first = builder.BuildMarkdown(streaming: true);
        var second = builder.BuildMarkdown(streaming: true);

        first.Should().Be(second);
        ReferenceEquals(first, second).Should().BeTrue();
    }
}
