using System.Diagnostics;
using Microsoft.SemanticKernel;

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

        Console.WriteLine($"[Tool] {pluginName}.{functionName} 调用中...");

        await next(context);

        sw.Stop();
        var resultPreview = context.Result?.ToString();
        if (!string.IsNullOrEmpty(resultPreview) && resultPreview.Length > 120)
        {
            resultPreview = resultPreview[..120] + "...";
        }

        Console.WriteLine($"[Tool] {pluginName}.{functionName} 完成 ({sw.ElapsedMilliseconds}ms)");
        if (!string.IsNullOrWhiteSpace(resultPreview))
        {
            Console.WriteLine($"[Tool] 结果预览: {resultPreview}");
        }
    }
}
