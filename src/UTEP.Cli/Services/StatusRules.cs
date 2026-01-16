using UTEP.Cli.Domain;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Cli.Services;

public static class StatusRules
{
    private static readonly Dictionary<TaskStatus, HashSet<TaskStatus>> AllowedTransitions = new()
    {
        [TaskStatus.Draft] = new HashSet<TaskStatus> { TaskStatus.Planned, TaskStatus.Cancelled },
        [TaskStatus.Planned] = new HashSet<TaskStatus>
        {
            TaskStatus.Ready, TaskStatus.Question, TaskStatus.Invalidated, TaskStatus.Cancelled
        },
        [TaskStatus.Ready] = new HashSet<TaskStatus>
        {
            TaskStatus.InProgress, TaskStatus.Question, TaskStatus.Invalidated, TaskStatus.Cancelled
        },
        [TaskStatus.InProgress] = new HashSet<TaskStatus>
        {
            TaskStatus.Completed, TaskStatus.Question, TaskStatus.Invalidated, TaskStatus.Cancelled
        },
        [TaskStatus.Question] = new HashSet<TaskStatus>
        {
            TaskStatus.Planned, TaskStatus.Ready, TaskStatus.Invalidated, TaskStatus.Cancelled
        },
        [TaskStatus.Completed] = new HashSet<TaskStatus>(),
        [TaskStatus.Cancelled] = new HashSet<TaskStatus>(),
        [TaskStatus.Invalidated] = new HashSet<TaskStatus>()
    };

    public static bool CanTransition(TaskStatus from, TaskStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}
