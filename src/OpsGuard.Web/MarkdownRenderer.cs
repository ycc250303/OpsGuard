using System.Text.RegularExpressions;
using Markdig;

namespace OpsGuard.Web;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline FullPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>流式输出用轻量管线（无表格/脚注等扩展），降低 Markdig 解析开销。</summary>
    private static readonly MarkdownPipeline StreamingPipeline = new MarkdownPipelineBuilder()
        .Build();

    private static readonly Regex CollapsedTableRowRegex = new(
        @" \|\s+\|",
        RegexOptions.Compiled);

    public static string ToHtml(string markdown, bool streaming = false)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var normalized = NormalizeCollapsedTables(markdown);
        var pipeline = streaming ? StreamingPipeline : FullPipeline;
        return Markdown.ToHtml(normalized, pipeline);
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
