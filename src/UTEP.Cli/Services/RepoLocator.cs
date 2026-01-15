namespace UTEP.Cli.Services;

public sealed class RepoLocator
{
    public string? FindRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current != null)
        {
            var configPath = Path.Combine(current.FullName, "utep.config.json");
            if (File.Exists(configPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
