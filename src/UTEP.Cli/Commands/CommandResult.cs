using UTEP.Cli.Domain;

namespace UTEP.Cli.Commands;

public sealed class CommandResult<T>
{
    public string? Command { get; set; }

    public bool Ok { get; set; }

    public T? Result { get; set; }

    public List<ValidationIssue> Warnings { get; set; } = new();

    public List<ValidationIssue> Errors { get; set; } = new();

    public int ExitCode { get; set; }

    public string? GoalId { get; set; }
}
