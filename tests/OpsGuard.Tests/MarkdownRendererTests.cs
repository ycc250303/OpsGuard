using FluentAssertions;
using OpsGuard.Web;

namespace OpsGuard.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void ToHtml_RendersMarkdownTableTags()
    {
        const string markdown = """
            | serviceId | status |
            | --- | --- |
            | backend | running |
            """;

        var html = MarkdownRenderer.ToHtml(markdown);

        html.Should().Contain("<table");
        html.Should().Contain("<th");
        html.Should().Contain("<td");
    }

    [Fact]
    public void ToHtml_RendersCollapsedPipeTable()
    {
        const string markdown =
            "| serviceId | displayName | | --- | --- | | backend | 后端 API |";

        var html = MarkdownRenderer.ToHtml(markdown);

        html.Should().Contain("<table");
        html.Should().Contain("backend");
    }
}
