using System.Text.Json.Serialization;

namespace UTEP.Cli.Domain;

public sealed class InitResult
{
    [JsonPropertyOrder(1)]
    public List<string> Created { get; set; } = new();

    [JsonPropertyOrder(2)]
    public string RepoRoot { get; set; } = string.Empty;
}

public sealed class GoalNewResult
{
    [JsonPropertyOrder(1)]
    public GoalCreated Goal { get; set; } = new();
}

public sealed class GoalCreated
{
    [JsonPropertyOrder(1)]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public GoalStatus Status { get; set; }

    [JsonPropertyOrder(4)]
    public string File { get; set; } = string.Empty;

    [JsonPropertyOrder(5)]
    public string IndexFile { get; set; } = string.Empty;
}

public sealed class GoalOpenResult
{
    [JsonPropertyOrder(1)]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string ContextFile { get; set; } = string.Empty;
}

public sealed class GoalTreeResult
{
    [JsonPropertyOrder(1)]
    public GoalRef Goal { get; set; } = new();

    [JsonPropertyOrder(2)]
    public List<TreeNode> Nodes { get; set; } = new();

    [JsonPropertyOrder(3)]
    public List<string> Roots { get; set; } = new();
}

public sealed class GoalRef
{
    [JsonPropertyOrder(1)]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;
}

public sealed class TreeNode
{
    [JsonPropertyOrder(1)]
    public TaskRef Task { get; set; } = new();

    [JsonPropertyOrder(2)]
    public TaskComputed Computed { get; set; } = new();

    [JsonPropertyOrder(3)]
    public List<string> Children { get; set; } = new();
}

public sealed class TaskNewResult
{
    [JsonPropertyOrder(1)]
    public TaskCreated Task { get; set; } = new();
}

public sealed class TaskCreated
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public TaskStatus Status { get; set; }

    [JsonPropertyOrder(4)]
    public string? ParentId { get; set; }

    [JsonPropertyOrder(5)]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyOrder(6)]
    public string File { get; set; } = string.Empty;
}

public sealed class TaskShowResult
{
    [JsonPropertyOrder(1)]
    public TaskFile Task { get; set; } = new();

    [JsonPropertyOrder(2)]
    public TaskComputed Computed { get; set; } = new();

    [JsonPropertyOrder(3)]
    public TaskRelations Relations { get; set; } = new();
}

public sealed class TaskRelations
{
    [JsonPropertyOrder(1)]
    public List<string> Children { get; set; } = new();

    [JsonPropertyOrder(2)]
    public string? Parent { get; set; }

    [JsonPropertyOrder(3)]
    public List<string> Blocks { get; set; } = new();
}

public sealed class TaskSetStatusResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public TaskStatus From { get; set; }

    [JsonPropertyOrder(3)]
    public TaskStatus To { get; set; }

    [JsonPropertyOrder(4)]
    public string? Note { get; set; }

    [JsonPropertyOrder(5)]
    public string LogEventId { get; set; } = string.Empty;

    [JsonPropertyOrder(6)]
    public bool Rendered { get; set; }
}

public sealed class TaskStartResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public TaskStatus From { get; set; }

    [JsonPropertyOrder(3)]
    public TaskStatus To { get; set; }

    [JsonPropertyOrder(4)]
    public AttemptSession AttemptSession { get; set; } = new();

    [JsonPropertyOrder(5)]
    public bool Rendered { get; set; }
}

public sealed class AttemptSession
{
    [JsonPropertyOrder(1)]
    public string StartedAt { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public int AttemptsBefore { get; set; }
}

public sealed class TaskAttemptResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public AttemptCount Attempts { get; set; } = new();

    [JsonPropertyOrder(3)]
    public TimeSpent TimeSpentMinutes { get; set; } = new();

    [JsonPropertyOrder(4)]
    public List<Evidence> EvidenceAdded { get; set; } = new();

    [JsonPropertyOrder(5)]
    public bool Rendered { get; set; }
}

public sealed class AttemptCount
{
    [JsonPropertyOrder(1)]
    public int Before { get; set; }

    [JsonPropertyOrder(2)]
    public int After { get; set; }
}

public sealed class TimeSpent
{
    [JsonPropertyOrder(1)]
    public int Before { get; set; }

    [JsonPropertyOrder(2)]
    public int After { get; set; }
}

public sealed class TaskCompleteResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public TaskStatus From { get; set; }

    [JsonPropertyOrder(3)]
    public TaskStatus To { get; set; }

    [JsonPropertyOrder(4)]
    public List<Evidence> EvidenceAdded { get; set; } = new();

    [JsonPropertyOrder(5)]
    public ParentCheck ParentCheck { get; set; } = new();

    [JsonPropertyOrder(6)]
    public bool Rendered { get; set; }
}

public sealed class ParentCheck
{
    [JsonPropertyOrder(1)]
    public bool Ran { get; set; }

    [JsonPropertyOrder(2)]
    public List<ParentStatusChange> AffectedTasks { get; set; } = new();
}

public sealed class ParentStatusChange
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public TaskStatus From { get; set; }

    [JsonPropertyOrder(3)]
    public TaskStatus To { get; set; }

    [JsonPropertyOrder(4)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class TaskInvalidateResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public TaskStatus From { get; set; }

    [JsonPropertyOrder(3)]
    public TaskStatus To { get; set; }

    [JsonPropertyOrder(4)]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyOrder(5)]
    public bool Rendered { get; set; }
}

public sealed class TaskBlockResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public TaskStatus From { get; set; }

    [JsonPropertyOrder(3)]
    public TaskStatus To { get; set; }

    [JsonPropertyOrder(4)]
    public QuestionImported QuestionImported { get; set; } = new();

    [JsonPropertyOrder(5)]
    public bool Rendered { get; set; }
}

public sealed class TaskQuestionResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public TaskStatus From { get; set; }

    [JsonPropertyOrder(3)]
    public TaskStatus To { get; set; }

    [JsonPropertyOrder(4)]
    public string OpenQuestionId { get; set; } = string.Empty;

    [JsonPropertyOrder(5)]
    public bool Rendered { get; set; }
}

public sealed class TaskAnswerResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string OpenQuestionId { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public string Answer { get; set; } = string.Empty;

    [JsonPropertyOrder(4)]
    public bool Rendered { get; set; }
}

public sealed class QuestionImported
{
    [JsonPropertyOrder(1)]
    public string File { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string OpenQuestionId { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public string Kind { get; set; } = string.Empty;
}

public sealed class TaskDepChangeResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Change { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public List<string> BlockedBy { get; set; } = new();

    [JsonPropertyOrder(4)]
    public bool Rendered { get; set; }
}

public sealed class TaskDepsResult
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public List<string> BlockedBy { get; set; } = new();

    [JsonPropertyOrder(3)]
    public List<string> Blocks { get; set; } = new();
}

public sealed class NextResult
{
    [JsonPropertyOrder(1)]
    public List<ActionableItem> Actionable { get; set; } = new();

    [JsonPropertyOrder(2)]
    public string? Reason { get; set; }

    [JsonPropertyOrder(3)]
    public NextBlocking? Blocking { get; set; }
}

public sealed class ActionableItem
{
    [JsonPropertyOrder(1)]
    public TaskRef Task { get; set; } = new();

    [JsonPropertyOrder(2)]
    public TaskComputed Computed { get; set; } = new();

    [JsonPropertyOrder(3)]
    public SelectionReason SelectionReason { get; set; } = new();
}

public sealed class SelectionReason
{
    [JsonPropertyOrder(1)]
    public int Depth { get; set; }

    [JsonPropertyOrder(2)]
    public int BlocksCount { get; set; }

    [JsonPropertyOrder(3)]
    public int Priority { get; set; }

    [JsonPropertyOrder(4)]
    public string Rule { get; set; } = string.Empty;
}

public sealed class NextBlocking
{
    [JsonPropertyOrder(1)]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public QuestionTaskInfo? QuestionTask { get; set; }

    [JsonPropertyOrder(3)]
    public OpenQuestionInfo? Question { get; set; }

    [JsonPropertyOrder(4)]
    public RecommendedBlocker? RecommendedBlocker { get; set; }

    [JsonPropertyOrder(5)]
    public List<BlockedExample> BlockedExamples { get; set; } = new();
}

public sealed class QuestionTaskInfo
{
    [JsonPropertyOrder(1)]
    public TaskRef Task { get; set; } = new();

    [JsonPropertyOrder(2)]
    public TaskComputed Computed { get; set; } = new();
}

public sealed class OpenQuestionInfo
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string OpenQuestionId { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyOrder(4)]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyOrder(5)]
    public List<QuestionOptionShort> Options { get; set; } = new();

    [JsonPropertyOrder(6)]
    public string? Recommendation { get; set; }

    [JsonPropertyOrder(7)]
    public string RequestedAnswer { get; set; } = string.Empty;
}

public sealed class QuestionOptionShort
{
    [JsonPropertyOrder(1)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string Title { get; set; } = string.Empty;
}

public sealed class RecommendedBlocker
{
    [JsonPropertyOrder(1)]
    public TaskRef Task { get; set; } = new();

    [JsonPropertyOrder(2)]
    public TaskComputed Computed { get; set; } = new();

    [JsonPropertyOrder(3)]
    public int BlocksCount { get; set; }
}

public sealed class BlockedExample
{
    [JsonPropertyOrder(1)]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public List<string> BlockedOn { get; set; } = new();
}

public sealed class BottlenecksResult
{
    [JsonPropertyOrder(1)]
    public List<BottleneckItem> Top { get; set; } = new();
}

public sealed class BottleneckItem
{
    [JsonPropertyOrder(1)]
    public TaskRef Task { get; set; } = new();

    [JsonPropertyOrder(2)]
    public int BlocksCount { get; set; }

    [JsonPropertyOrder(3)]
    public List<string> BlockedTasksSample { get; set; } = new();
}

public sealed class ValidateResult
{
    [JsonPropertyOrder(1)]
    public ValidateSummary Summary { get; set; } = new();

    [JsonPropertyOrder(2)]
    public List<ValidationIssue> Issues { get; set; } = new();
}

public sealed class ValidateSummary
{
    [JsonPropertyOrder(1)]
    public int Errors { get; set; }

    [JsonPropertyOrder(2)]
    public int Warnings { get; set; }
}

public sealed class DoctorResult
{
    [JsonPropertyOrder(1)]
    public DoctorSummary Summary { get; set; } = new();

    [JsonPropertyOrder(2)]
    public List<DoctorAction> Actions { get; set; } = new();

    [JsonPropertyOrder(3)]
    public List<ValidationIssue> RemainingIssues { get; set; } = new();
}

public sealed class DoctorSummary
{
    [JsonPropertyOrder(1)]
    public int ErrorsBefore { get; set; }

    [JsonPropertyOrder(2)]
    public int ErrorsAfter { get; set; }

    [JsonPropertyOrder(3)]
    public int Fixed { get; set; }

    [JsonPropertyOrder(4)]
    public int RequiresManual { get; set; }
}

public sealed class DoctorAction
{
    [JsonPropertyOrder(1)]
    public string IssueCode { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public string RemedyId { get; set; } = string.Empty;

    [JsonPropertyOrder(3)]
    public bool Applied { get; set; }

    [JsonPropertyOrder(4)]
    public List<string> CommandsExecuted { get; set; } = new();
}

public sealed class RenderResult
{
    [JsonPropertyOrder(1)]
    public bool Rendered { get; set; }

    [JsonPropertyOrder(2)]
    public List<string> Files { get; set; } = new();
}
