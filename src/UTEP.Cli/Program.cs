using System.CommandLine;
using System.CommandLine.Invocation;
using Spectre.Console;
using UTEP.Cli.Commands;
using UTEP.Cli.IO;
using UTEP.Cli.Services;

namespace UTEP.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var jsonOption = new Option<bool>(
            name: "--json",
            description: "Выводить ответ в JSON формате.");

        var goalOption = new Option<string?>(
            name: "--goal",
            description: "Идентификатор цели.");

        var root = new RootCommand("UTEP CLI");
        root.AddGlobalOption(jsonOption);

        var commandFactory = new CommandFactory(jsonOption, goalOption);
        foreach (var command in commandFactory.BuildAll())
        {
            root.AddCommand(command);
        }

        root.SetHandler(context =>
        {
            var json = context.ParseResult.GetValueForOption(jsonOption);
            if (!json)
            {
                AnsiConsole.MarkupLine("[red]Неизвестная команда.[/]");
            }

            context.ExitCode = ExitCodes.InvalidCommand;
        });

        return await root.InvokeAsync(args);
    }
}
