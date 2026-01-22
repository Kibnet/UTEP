using UTEP.Cli.Domain;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;
namespace UTEP.Cli.Services;

public sealed class NextSelector
{
    private static readonly IReadOnlyDictionary<EffectiveState, int> EffectiveStateOrder = new Dictionary<EffectiveState, int>
    {
        [EffectiveState.Continue] = 0,
        [EffectiveState.Execute] = 1,
        [EffectiveState.Clarify] = 2,
        [EffectiveState.Plan] = 3,
        [EffectiveState.Blocked] = 4
    };

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
            if (computed.EffectiveState == EffectiveState.Terminal)
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
                    Rule = "effective_state, depth, blocks_count, priority, task_id"
                }
            });
        }

        var ordered = items
            .OrderBy(item => ResolveEffectiveStateOrder(item.Computed.EffectiveState))
            .ThenBy(item => item.Task.Depth)
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
        var hasNonTerminal = tasks.Values.Any(info =>
            info.File.Task.Status is not TaskStatus.Completed
                and not TaskStatus.Cancelled
                and not TaskStatus.Invalidated);
        if (hasNonTerminal)
        {
            return "none";
        }

        return "none";
    }

    private static int ResolveEffectiveStateOrder(EffectiveState effectiveState)
    {
        return EffectiveStateOrder.TryGetValue(effectiveState, out var order) ? order : int.MaxValue;
    }
}
