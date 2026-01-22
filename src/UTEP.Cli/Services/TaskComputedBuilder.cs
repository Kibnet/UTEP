using UTEP.Cli.Domain;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Cli.Services;

public sealed class TaskComputedBuilder
{
    public TaskComputed Build(TaskFile task, IReadOnlyDictionary<string, TaskInfo> tasks, Dictionary<string, int> blocksCount)
    {
        var dependencies = task.Task.Dependencies?.BlockedBy ?? new List<string>();
        var waiting = new List<string>();
        var needsReview = false;

        foreach (var blocker in dependencies)
        {
            if (!tasks.TryGetValue(blocker, out var blockerInfo))
            {
                waiting.Add(blocker);
                continue;
            }

            var status = blockerInfo.File.Task.Status;
            if (status is TaskStatus.Completed or TaskStatus.Cancelled or TaskStatus.Invalidated)
            {
                if (status != TaskStatus.Completed)
                {
                    needsReview = true;
                }
            }
            else
            {
                waiting.Add(blocker);
            }
        }

        var isUnblocked = waiting.Count == 0;
        var effectiveState = ResolveEffectiveState(task.Task.Status, isUnblocked, task.Task.OpenQuestions.Count);
        var count = blocksCount.TryGetValue(task.Task.Id, out var value) ? value : 0;

        return new TaskComputed
        {
            EffectiveState = effectiveState,
            IsUnblocked = isUnblocked,
            BlockedBy = waiting,
            NeedsReview = needsReview,
            BlocksCount = count
        };
    }

    private static EffectiveState ResolveEffectiveState(TaskStatus status, bool isUnblocked, int openQuestions)
    {
        return status switch
        {
            TaskStatus.Ready => isUnblocked ? EffectiveState.Execute : EffectiveState.Blocked,
            TaskStatus.InProgress => EffectiveState.Continue,
            TaskStatus.Question => EffectiveState.Clarify,
            TaskStatus.Completed or TaskStatus.Cancelled or TaskStatus.Invalidated => EffectiveState.Terminal,
            TaskStatus.Draft or TaskStatus.Planned => EffectiveState.Plan,
            _ => EffectiveState.Plan
        };
    }
}
