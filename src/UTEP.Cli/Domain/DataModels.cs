using System.Text.Json.Serialization;

namespace UTEP.Cli.Domain;

public sealed class UtepConfig
{
    [JsonPropertyOrder(1)]
    public int Version { get; set; }

    [JsonPropertyOrder(2)]
    public LimitsConfig Limits { get; set; } = new();

    [JsonPropertyOrder(3)]
    public ThresholdsConfig Thresholds { get; set; } = new();

    [JsonPropertyOrder(4)]
    public RenderConfig Render { get; set; } = new();

    [JsonPropertyOrder(5)]
    public OutputConfig Output { get; set; } = new();
}

public sealed class LimitsConfig
{
    [JsonPropertyOrder(1)]
    public int AttemptLimit { get; set; }

    [JsonPropertyOrder(2)]
    public int TimeLimitMinutes { get; set; }

    [JsonPropertyOrder(3)]
    public int LargeTaskMinutes { get; set; }
}

public sealed class ThresholdsConfig
{
    [JsonPropertyOrder(1)]
    public double ConfidenceMin { get; set; }
}

public sealed class RenderConfig
{
    [JsonPropertyOrder(1)]
    public bool Index { get; set; }

    [JsonPropertyOrder(2)]
    public string IndexFilename { get; set; } = "index.md";
}

public sealed class OutputConfig
{
    [JsonPropertyOrder(1)]
    public string Default { get; set; } = "human";
}

public sealed class GoalFile
{
    [JsonPropertyOrder(1)]
    public int Version { get; set; }

    [JsonPropertyOrder(2)]
    public GoalData Goal { get; set; } = new();

    [JsonPropertyOrder(3)]
    public GoalMeta Meta { get; set; } = new();
}

public sealed class GoalData
{
    [JsonPropertyOrder(1)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public GoalStatus Status { get; set; }

    [JsonPropertyOrder(4)]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyOrder(5)]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyOrder(6)]
    public List<string> SuccessCriteria { get; set; } = new();

    [JsonPropertyOrder(7)]
    public string? NextTaskId { get; set; }
}

public sealed class GoalMeta
{
    [JsonPropertyOrder(1)]
    public string Owner { get; set; } = "human";

    [JsonPropertyOrder(2)]
    public List<string> Tags { get; set; } = new();
}

public sealed class TaskFile
{
    [JsonPropertyOrder(1)]
    public int Version { get; set; }

    [JsonPropertyOrder(2)]
    public TaskData Task { get; set; } = new();

    [JsonPropertyOrder(3)]
    public TaskLinks Links { get; set; } = new();
}

public sealed class TaskData
{
    [JsonPropertyOrder(1)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public string? ParentId { get; set; }

    [JsonPropertyOrder(4)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyOrder(5)]
    public TaskStatus Status { get; set; }

    [JsonPropertyOrder(6)]
    public int Priority { get; set; } = 3;

    [JsonPropertyOrder(7)]
    public string Risk { get; set; } = "Med";

    [JsonPropertyOrder(8)]
    public int CostEstimateMinutes { get; set; } = 0;

    [JsonPropertyOrder(9)]
    public List<string> SuccessCriteria { get; set; } = new();

    [JsonPropertyOrder(10)]
    public double Confidence { get; set; } = 0.5;

    [JsonPropertyOrder(11)]
    public TaskDependencies? Dependencies { get; set; }

    [JsonPropertyOrder(12)]
    public List<Assumption> Assumptions { get; set; } = new();

    [JsonPropertyOrder(13)]
    public List<OpenQuestion> OpenQuestions { get; set; } = new();

    [JsonPropertyOrder(14)]
    public int Attempts { get; set; }

    [JsonPropertyOrder(15)]
    public int TimeSpentMinutes { get; set; }

    [JsonPropertyOrder(16)]
    public List<Evidence> Evidence { get; set; } = new();
}

public sealed class TaskDependencies
{
    [JsonPropertyOrder(1)]
    public List<string>? BlockedBy { get; set; }
}

public sealed class Assumption
{
    [JsonPropertyOrder(1)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Text { get; set; } = string.Empty;
}

public sealed class OpenQuestion
{
    [JsonPropertyOrder(1)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyOrder(4)]
    public List<QuestionOption> Options { get; set; } = new();

    [JsonPropertyOrder(5)]
    public string? Recommendation { get; set; }

    [JsonPropertyOrder(6)]
    public string RequestedAnswer { get; set; } = string.Empty;

    [JsonPropertyOrder(7)]
    public string? Answer { get; set; }

    [JsonPropertyOrder(8)]
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class QuestionOption
{
    [JsonPropertyOrder(1)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public List<string> Pros { get; set; } = new();

    [JsonPropertyOrder(4)]
    public List<string> Cons { get; set; } = new();

    [JsonPropertyOrder(5)]
    public List<string> Risks { get; set; } = new();
}

public sealed class Evidence
{
    [JsonPropertyOrder(1)]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public string At { get; set; } = string.Empty;
}

public sealed class TaskLinks
{
    [JsonPropertyOrder(1)]
    public string ArtifactsDir { get; set; } = "../artifacts/";
}

public enum TaskStatus
{
    Draft,
    Planned,
    Ready,
    InProgress,
    Question,
    Completed,
    Cancelled,
    Invalidated
}

public enum GoalStatus
{
    Draft,
    Planned,
    Ready,
    InProgress,
    Question,
    Completed,
    Cancelled,
    Invalidated
}
