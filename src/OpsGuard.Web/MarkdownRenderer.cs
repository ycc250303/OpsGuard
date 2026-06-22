using System.Text.RegularExpressions;
using Markdig;

namespace OpsGuard.Web;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly Regex CollapsedTableRowRegex = new(
        @" \|\s+\|",
        RegexOptions.Compiled);

    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var normalized = NormalizeCollapsedTables(markdown);
        return Markdown.ToHtml(normalized, Pipeline);
    }

    internal static string NormalizeCollapsedTables(string markdown)
    {
        if (!markdown.Contains('|'))
        {
            return markdown;
        }

        return CollapsedTableRowRegex.Replace(markdown, " |\n|");
    }
}
