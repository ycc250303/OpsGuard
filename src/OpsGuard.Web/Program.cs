using OpsGuard.App;
using OpsGuard.App.DependencyInjection;
using OpsGuard.App.Services;
using OpsGuard.App.Services.Conversations;
using OpsGuard.Core.Configuration;
using OpsGuard.Web;
using OpsGuard.Web.Components;
using OpsGuard.Web.Services;

var contentRoot = OpsGuardContentRoot.Find();
OpsGuardContentRoot.LoadEnvFiles();
var webRoot = OpsGuardContentRoot.FindWebRoot(contentRoot);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot,
    WebRootPath = webRoot
});

builder.Configuration
    .AddJsonFile(Path.Combine(contentRoot, "src/OpsGuard.App/appsettings.json"), optional: true)
    .AddEnvironmentVariables();

var topologyPath = OpsGuardContentRoot.ResolveTopologyPath(args, contentRoot);
var conversationDir = ResolveConversationDirectory(builder.Configuration, contentRoot);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOpsGuard(builder.Configuration, topologyPath);
builder.Services.Configure<ConversationStoreOptions>(options => options.Directory = conversationDir);
builder.Services.AddSingleton<IConversationStore, JsonConversationStore>();
builder.Services.AddScoped<IUserModelSelection, BrowserUserModelSelection>();
builder.Services.AddScoped<DiagnosticSessionService>();
builder.Services.AddSingleton(new WebAppInfo(topologyPath));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string ResolveConversationDirectory(IConfiguration configuration, string contentRoot)
{
    var envDir = Environment.GetEnvironmentVariable("OPSGUARD_CONVERSATIONS_DIR");
    if (!string.IsNullOrWhiteSpace(envDir))
    {
        return Path.GetFullPath(envDir);
    }

    var configured = configuration[$"{ConversationStoreOptions.SectionName}:Directory"];
    var relative = string.IsNullOrWhiteSpace(configured) ? "data/conversations" : configured;
    return Path.IsPathRooted(relative)
        ? Path.GetFullPath(relative)
        : Path.GetFullPath(Path.Combine(contentRoot, relative));
}
