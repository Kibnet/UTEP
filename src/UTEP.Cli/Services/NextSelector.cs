using UTEP.Cli.Domain;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Cli.Services;

public sealed class NextSelector
{
    public IReadOnlyList<ActionableItem> SelectActionable(
        IReadOnlyDictionary<string, TaskInfo> tasks,
        Dictionary<string, int> depths,
        Dictionary<string, int> blocksCount,
        int count,
        TaskComputedBuilder computedBuilder)
    {
        var items = new List<ActionableItem>();
        foreach (var (taskId, info) in tasks)
        {
            var computed = computedBuilder.Build(info.File, tasks, blocksCount);
            if (computed.EffectiveState != "Actionable")
            {
                continue;
            }

            var depth = depths.TryGetValue(taskId, out var depthValue) ? depthValue : 0;
            var taskRef = new TaskRef
            {
                TaskId = taskId,
                Title = info.File.Task.Title,
                Status = info.File.Task.Status,
                Priority = info.File.Task.Priority,
                Depth = depth,
                File = info.Path.Replace('\\', '/')
            };

            items.Add(new ActionableItem
            {
                Task = taskRef,
                Computed = computed,
                SelectionReason = new SelectionReason
                {
                    Depth = depth,
                    BlocksCount = computed.BlocksCount,
                    Priority = taskRef.Priority,
                    Rule = "depth, blocks_count, priority, created_at"
                }
            });
        }

        var ordered = items
            .OrderBy(item => item.Task.Depth)
            .ThenByDescending(item => item.Computed.BlocksCount)
            .ThenBy(item => item.Task.Priority)
            .ThenBy(item => item.Task.TaskId, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToList();

        return ordered;
    }

    public string ResolveNoActionableReason(
        IReadOnlyDictionary<string, TaskInfo> tasks,
        Dictionary<string, int> blocksCount,
        TaskComputedBuilder computedBuilder)
    {
        var hasQuestion = tasks.Values.Any(info =>
            info.File.Task.Status == TaskStatus.Question && info.File.Task.OpenQuestions.Count > 0);
        if (hasQuestion)
        {
            return "question";
        }

        var hasBlocked = tasks.Values.Any(info =>
            computedBuilder.Build(info.File, tasks, blocksCount).EffectiveState == "Blocked");
        return hasBlocked ? "blocked" : "none";
    }
}
