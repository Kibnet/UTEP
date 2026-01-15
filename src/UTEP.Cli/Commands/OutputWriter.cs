using System.Text.Json;
using Spectre.Console;
using UTEP.Cli.Domain;
using UTEP.Cli.IO;

namespace UTEP.Cli.Commands;

public sealed class OutputWriter
{
    public void WriteJson<T>(Envelope<T> envelope)
    {
        var json = JsonSerializer.Serialize(envelope, JsonDefaults.Options);
        Console.Out.WriteLine(json);
    }

    public void WriteHuman(string title, IEnumerable<string> lines)
    {
        AnsiConsole.MarkupLine($"[bold]{title}[/]");
        foreach (var line in lines)
        {
            AnsiConsole.MarkupLine(line);
        }
    }
}
