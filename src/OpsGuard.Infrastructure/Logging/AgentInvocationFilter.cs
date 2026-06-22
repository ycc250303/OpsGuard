using System.Diagnostics;
using Microsoft.SemanticKernel;
using OpsGuard.Infrastructure.Streaming;

namespace OpsGuard.Infrastructure.Logging;

public sealed class AgentInvocationFilter : IAutoFunctionInvocationFilter
{
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var sw = Stopwatch.StartNew();
        var pluginName = context.Function.PluginName ?? "unknown";
        var functionName = context.Function.Name ?? "unknown";
        var toolName = $"{pluginName}.{functionName}";

        Console.WriteLine($"[Tool] {toolName} 调用中...");
        DiagnosticStreamContext.PublishToolInvoking(toolName);

        await next(context);

        sw.Stop();
        DiagnosticStreamContext.PublishToolCompleted(toolName);

        var resultPreview = context.Result?.ToString();
        if (!string.IsNullOrEmpty(resultPreview) && resultPreview.Length > 120)
        {
            resultPreview = resultPreview[..120] + "...";
        }

        Console.WriteLine($"[Tool] {toolName} 完成 ({sw.ElapsedMilliseconds}ms)");
        if (!string.IsNullOrWhiteSpace(resultPreview))
        {
            Console.WriteLine($"[Tool] 结果预览: {resultPreview}");
        }
    }
}
