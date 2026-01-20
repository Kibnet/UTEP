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

    public static GoalStatus ComputeGoalStatus(IReadOnlyDictionary<string, TaskInfo> tasks)
    {
        if (tasks.Count == 0)
        {
            return GoalStatus.Planned;
        }

        var statuses = tasks.Values.Select(info => info.File.Task.Status).ToList();
        if (statuses.All(status => status is TaskStatus.Completed or TaskStatus.Cancelled or TaskStatus.Invalidated))
        {
            return GoalStatus.Completed;
        }

        if (statuses.Contains(TaskStatus.InProgress))
        {
            return GoalStatus.InProgress;
        }

        if (statuses.Contains(TaskStatus.Question))
        {
            return GoalStatus.Question;
        }

        if (statuses.Contains(TaskStatus.Ready))
        {
            return GoalStatus.Ready;
        }

        return GoalStatus.Planned;
    }
}
