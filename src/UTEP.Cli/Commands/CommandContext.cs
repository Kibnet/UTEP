using UTEP.Cli.Domain;
using UTEP.Cli.IO;
using UTEP.Cli.Services;

namespace UTEP.Cli.Commands;

public sealed class CommandContext
{
    public CommandContext(string command, bool json, string? repoRoot)
    {
        Command = command;
        Json = json;
        RepoRoot = repoRoot;
    }

    public string Command { get; }

    public bool Json { get; }

    public string? RepoRoot { get; }
}
