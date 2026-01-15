using UTEP.Cli.Domain;

namespace UTEP.Cli.Services;

public sealed class TaskGraphBuilder
{
    public Dictionary<string, List<string>> BuildChildrenMap(IReadOnlyDictionary<string, TaskInfo> tasks)
    {
        var map = tasks.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var (taskId, info) in tasks)
        {
            var parentId = info.File.Task.ParentId;
            if (string.IsNullOrWhiteSpace(parentId))
            {
                continue;
            }

            if (!map.TryGetValue(parentId, out var children))
            {
                children = new List<string>();
                map[parentId] = children;
            }

            children.Add(taskId);
        }

        return map;
    }

    public Dictionary<string, int> BuildBlocksCount(IReadOnlyDictionary<string, TaskInfo> tasks)
    {
        var counts = tasks.Keys.ToDictionary(id => id, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var (taskId, info) in tasks)
        {
            foreach (var blocker in info.File.Task.Dependencies.BlockedBy)
            {
                if (counts.ContainsKey(blocker))
                {
                    counts[blocker] += 1;
                }
            }
        }

        return counts;
    }

    public Dictionary<string, int> BuildDepths(IReadOnlyDictionary<string, TaskInfo> tasks)
    {
        var depths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var taskId in tasks.Keys)
        {
            depths[taskId] = GetDepth(taskId, tasks, depths, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        return depths;
    }

    private static int GetDepth(
        string taskId,
        IReadOnlyDictionary<string, TaskInfo> tasks,
        Dictionary<string, int> cache,
        HashSet<string> visiting)
    {
        if (cache.TryGetValue(taskId, out var depth))
        {
            return depth;
        }

        if (!tasks.TryGetValue(taskId, out var info))
        {
            return 0;
        }

        var parentId = info.File.Task.ParentId;
        if (string.IsNullOrWhiteSpace(parentId) || !tasks.ContainsKey(parentId))
        {
            cache[taskId] = 0;
            return 0;
        }

        if (!visiting.Add(taskId))
        {
            cache[taskId] = 0;
            return 0;
        }

        var parentDepth = GetDepth(parentId, tasks, cache, visiting);
        visiting.Remove(taskId);
        cache[taskId] = parentDepth + 1;
        return cache[taskId];
    }
}
