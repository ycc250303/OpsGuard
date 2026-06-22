using OpsGuard.App;
using OpsGuard.App.DependencyInjection;
using OpsGuard.App.Services;
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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOpsGuard(builder.Configuration, topologyPath);
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
