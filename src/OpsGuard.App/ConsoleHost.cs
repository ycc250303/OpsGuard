using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using OpsGuard.App.DependencyInjection;
using OpsGuard.App.Services;
using OpsGuard.App.Services.Conversations;
using OpsGuard.Core.Agents;
using OpsGuard.Core.Configuration;

namespace OpsGuard.App;

internal static class ConsoleHost
{
    public static async Task RunAsync(string[] args)
    {
        var contentRoot = OpsGuardContentRoot.Find();
        OpsGuardContentRoot.LoadEnvFiles();

        var topologyPath = OpsGuardContentRoot.ResolveTopologyPath(args, contentRoot);
        var query = ParseQuery(args);
        var configuration = BuildConfiguration(contentRoot);
        var services = new ServiceCollection()
            .AddOpsGuard(configuration, topologyPath)
            .BuildServiceProvider();

        var orchestratorFactory = services.GetRequiredService<OpsGuardOrchestratorFactory>();
        var modelSelection = services.GetRequiredService<IUserModelSelection>();
        var session = new DiagnosticSessionService(
            orchestratorFactory,
            modelSelection,
            services.GetRequiredService<IConversationStore>(),
            services.GetRequiredService<IOptions<AgentOptions>>());

        PrintWelcome(topologyPath);

        if (args.Any(a => a.Equals("--smoke", StringComparison.OrdinalIgnoreCase)))
        {
            var code = await AgentSmokeTest.RunAsync(services);
            Environment.ExitCode = code;
            return;
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            await session.AskAsync(query);
            Console.WriteLine();
            Console.WriteLine("=== 诊断报告 ===");
            Console.WriteLine(session.Messages.Last().Content);
            return;
        }

        while (true)
        {
            Console.Write("ops> ");
            var input = Console.ReadLine();
            if (input is null)
            {
                break;
            }

            input = input.Trim();
            if (input.Length == 0)
            {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)
                || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            Console.WriteLine();
            Console.WriteLine("正在诊断，请稍候...");
            Console.WriteLine();
            var report = await session.AskAsync(input);
            Console.WriteLine();
            Console.WriteLine("=== 诊断报告 ===");
            Console.WriteLine(report);
        }
    }

    private static IConfiguration BuildConfiguration(string contentRoot)
    {
        return new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(contentRoot, "src/OpsGuard.App/appsettings.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string? ParseQuery(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--query", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void PrintWelcome(string topologyPath)
    {
        Console.WriteLine("OpsGuard — Linux Host + Docker Compose 运维诊断 Agent");
        Console.WriteLine($"拓扑文件: {topologyPath}");
        Console.WriteLine("输入自然语言问题开始诊断，输入 exit/quit 退出。");
        Console.WriteLine();
    }
}
