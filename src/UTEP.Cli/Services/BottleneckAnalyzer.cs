using UTEP.Cli.Domain;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Cli.Services;

public sealed class BottleneckAnalyzer
{
    public IReadOnlyList<BottleneckItem> GetTop(
        IReadOnlyDictionary<string, TaskInfo> tasks,
        Dictionary<string, int> depths,
        Dictionary<string, int> blocksCount,
        int top,
        TaskComputedBuilder computedBuilder)
    {
        var items = new List<BottleneckItem>();
        foreach (var (taskId, info) in tasks)
        {
            if (info.File.Task.Status is TaskStatus.Completed or TaskStatus.Cancelled or TaskStatus.Invalidated)
            {
                continue;
            }

            if (!blocksCount.TryGetValue(taskId, out var count) || count == 0)
            {
                continue;
            }

            var depth = depths.TryGetValue(taskId, out var depthValue) ? depthValue : 0;
            var computed = computedBuilder.Build(info.File, tasks, blocksCount);
            var taskRef = new TaskRef
            {
                TaskId = taskId,
                Title = info.File.Task.Title,
                Status = info.File.Task.Status,
                Priority = info.File.Task.Priority,
                Depth = depth,
                File = info.Path.Replace('\\', '/')
            };

            items.Add(new BottleneckItem
            {
                Task = taskRef,
                BlocksCount = count,
                BlockedTasksSample = GetBlockedSample(taskId, tasks, 3)
            });
        }

        return items
            .OrderByDescending(item => item.BlocksCount)
            .ThenBy(item => item.Task.Depth)
            .ThenBy(item => item.Task.TaskId, StringComparer.OrdinalIgnoreCase)
            .Take(top)
            .ToList();
    }

    private static List<string> GetBlockedSample(string taskId, IReadOnlyDictionary<string, TaskInfo> tasks, int max)
    {
        var blocked = new List<string>();
        foreach (var (candidateId, info) in tasks)
        {
            var blockedBy = info.File.Task.Dependencies?.BlockedBy ?? new List<string>();
            if (blockedBy.Contains(taskId, StringComparer.OrdinalIgnoreCase))
            {
                blocked.Add(candidateId);
                if (blocked.Count >= max)
                {
                    break;
                }
            }
        }

        return blocked;
    }
}
