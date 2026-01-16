using System.Text.Json.Serialization;

namespace UTEP.Cli.Domain;

public sealed class TaskComputed
{
    [JsonPropertyOrder(1)]
    public string EffectiveState { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public bool IsUnblocked { get; set; }

    [JsonPropertyOrder(3)]
    public List<string> BlockedBy { get; set; } = new();

    [JsonPropertyOrder(4)]
    public bool NeedsReview { get; set; }

    [JsonPropertyOrder(5)]
    public int BlocksCount { get; set; }
}

public sealed class TaskRef
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public TaskStatus Status { get; set; }

    [JsonPropertyOrder(4)]
    public int Priority { get; set; }

    [JsonPropertyOrder(5)]
    public int Depth { get; set; }

    [JsonPropertyOrder(6)]
    public string File { get; set; } = string.Empty;
}

public sealed class GoalSummary
{
    [JsonPropertyOrder(1)]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public GoalStatus Status { get; set; }

    [JsonPropertyOrder(4)]
    public Dictionary<string, int> Counts { get; set; } = new();

    [JsonPropertyOrder(5)]
    public string? NextTaskId { get; set; }

    [JsonPropertyOrder(6)]
    public string RepoPath { get; set; } = string.Empty;
}
