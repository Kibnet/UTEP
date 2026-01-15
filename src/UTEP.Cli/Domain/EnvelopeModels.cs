using System.Text.Json.Serialization;

namespace UTEP.Cli.Domain;

public sealed class Envelope<T>
{
    [JsonPropertyOrder(1)]
    public string UtepVersion { get; set; } = "1.1";

    [JsonPropertyOrder(2)]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public string RepoRoot { get; set; } = string.Empty;

    [JsonPropertyOrder(4)]
    public string? GoalId { get; set; }

    [JsonPropertyOrder(5)]
    public bool Ok { get; set; }

    [JsonPropertyOrder(6)]
    public T? Result { get; set; }

    [JsonPropertyOrder(7)]
    public List<ValidationIssue> Warnings { get; set; } = new();

    [JsonPropertyOrder(8)]
    public List<ValidationIssue> Errors { get; set; } = new();

    [JsonPropertyOrder(9)]
    public EnvelopeMeta Meta { get; set; } = new();
}

public sealed class EnvelopeMeta
{
    [JsonPropertyOrder(1)]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public int DurationMs { get; set; }
}

public sealed class ValidationIssue
{
    [JsonPropertyOrder(1)]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyOrder(4)]
    public Dictionary<string, object>? Details { get; set; }

    [JsonPropertyOrder(5)]
    public List<IssueLocation> Locations { get; set; } = new();

    [JsonPropertyOrder(6)]
    public List<Remedy> Remedies { get; set; } = new();
}

public sealed class IssueLocation
{
    [JsonPropertyOrder(1)]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string? Id { get; set; }

    [JsonPropertyOrder(3)]
    public string? Path { get; set; }

    [JsonPropertyOrder(4)]
    public string? JsonPointer { get; set; }
}

public sealed class Remedy
{
    [JsonPropertyOrder(1)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public List<string> Commands { get; set; } = new();
}
