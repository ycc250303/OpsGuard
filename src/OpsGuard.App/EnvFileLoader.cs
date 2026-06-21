namespace OpsGuard.App;

internal static class EnvFileLoader
{
    public static void LoadFromDirectory(string directory)
    {
        var envPath = Path.Combine(directory, ".env");
        if (!File.Exists(envPath))
        {
            return;
        }

        foreach (var rawLine in File.ReadLines(envPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            if (key.Length == 0)
            {
                continue;
            }

            // 不覆盖已存在的环境变量（便于 CI/Shell 注入）
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
