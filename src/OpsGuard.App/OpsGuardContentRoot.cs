namespace OpsGuard.App;

public static class OpsGuardContentRoot
{
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OpsGuard.sln"))
                || File.Exists(Path.Combine(dir.FullName, "OpsGuard.slnx"))
                || Directory.Exists(Path.Combine(dir.FullName, "docs")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    public static string FindWebRoot(string contentRoot)
    {
        var devWebRoot = Path.Combine(contentRoot, "src/OpsGuard.Web/wwwroot");
        if (Directory.Exists(devWebRoot))
        {
            return devWebRoot;
        }

        var publishedWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        return publishedWebRoot;
    }

    public static void LoadEnvFiles()
    {
        EnvFileLoader.LoadFromDirectory(Find());
        EnvFileLoader.LoadFromDirectory(AppContext.BaseDirectory);
    }

    public static string ResolveTopologyPath(string[] args, string contentRoot)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--topology", StringComparison.OrdinalIgnoreCase))
            {
                return ResolvePath(args[i + 1], contentRoot);
            }
        }

        var configured = Environment.GetEnvironmentVariable("OPSGUARD_TOPOLOGY");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ResolvePath(configured, contentRoot);
        }

        return ResolvePath("docs/compose-topology.sample.json", contentRoot);
    }

    private static string ResolvePath(string path, string contentRoot)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(contentRoot, path));
    }
}
