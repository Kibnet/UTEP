using System.Text;
using UTEP.Cli.Domain;
using UTEP.Cli.IO;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Cli.Services;

public sealed class RenderService
{
    private readonly TaskGraphBuilder _graphBuilder;
    private readonly TaskComputedBuilder _computedBuilder;
    private readonly BottleneckAnalyzer _bottleneckAnalyzer;

    public RenderService(TaskGraphBuilder graphBuilder, TaskComputedBuilder computedBuilder, BottleneckAnalyzer bottleneckAnalyzer)
    {
        _graphBuilder = graphBuilder;
        _computedBuilder = computedBuilder;
        _bottleneckAnalyzer = bottleneckAnalyzer;
    }

    public RenderResult Render(TaskSnapshot snapshot, RepoPaths paths, string indexFilename)
    {
        var tasks = snapshot.Tasks;
        var childrenMap = _graphBuilder.BuildChildrenMap(tasks);
        var depths = _graphBuilder.BuildDepths(tasks);
        var blocksCount = _graphBuilder.BuildBlocksCount(tasks);

        var roots = tasks.Values
            .Where(info => string.IsNullOrWhiteSpace(info.File.Task.ParentId) || !tasks.ContainsKey(info.File.Task.ParentId))
            .Select(info => info.File.Task.Id)
            .OrderBy(id => depths.TryGetValue(id, out var depth) ? depth : 0)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine($"# {snapshot.Goal.Goal.Title}");
        builder.AppendLine();
        builder.AppendLine("## Дерево задач");
        builder.AppendLine();

        foreach (var root in roots)
        {
            RenderNode(builder, root, childrenMap, tasks, depths, blocksCount, 0);
        }

        builder.AppendLine();
        builder.AppendLine("## Bottlenecks");
        builder.AppendLine();
        var bottlenecks = _bottleneckAnalyzer.GetTop(tasks, depths, blocksCount, 5, _computedBuilder);
        if (bottlenecks.Count == 0)
        {
            builder.AppendLine("- Нет блокеров.");
        }
        else
        {
            foreach (var item in bottlenecks)
            {
                builder.AppendLine($"- {item.Task.TaskId} ({item.BlocksCount})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Blocked");
        builder.AppendLine();

        var blockedExamples = tasks.Values
            .Select(info => new
            {
                info.File.Task.Id,
                Computed = _computedBuilder.Build(info.File, tasks, blocksCount)
            })
            .Where(item => item.Computed.EffectiveState == "Blocked")
            .Take(5)
            .ToList();

        if (blockedExamples.Count == 0)
        {
            builder.AppendLine("- Нет заблокированных задач.");
        }
        else
        {
            foreach (var item in blockedExamples)
            {
                builder.AppendLine($"- {item.Id} -> {string.Join(", ", item.Computed.BlockedBy)}");
            }
        }

        var indexPath = paths.IndexFile(snapshot.Goal.Goal.Id, indexFilename);
        File.WriteAllText(indexPath, builder.ToString());

        return new RenderResult
        {
            Rendered = true,
            Files = new List<string> { indexPath.Replace('\\', '/') }
        };
    }

    private void RenderNode(
        StringBuilder builder,
        string taskId,
        Dictionary<string, List<string>> childrenMap,
        IReadOnlyDictionary<string, TaskInfo> tasks,
        Dictionary<string, int> depths,
        Dictionary<string, int> blocksCount,
        int indent)
    {
        if (!tasks.TryGetValue(taskId, out var info))
        {
            return;
        }

        var computed = _computedBuilder.Build(info.File, tasks, blocksCount);
        var marker = BuildMarker(computed, info.File.Task);
        var prefix = new string(' ', indent * 2);
        builder.AppendLine($"{prefix}- {info.File.Task.Id} [{info.File.Task.Status}] {info.File.Task.Title}{marker}");

        if (childrenMap.TryGetValue(taskId, out var children))
        {
            foreach (var child in children.OrderBy(id => depths.TryGetValue(id, out var depth) ? depth : 0).ThenBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                RenderNode(builder, child, childrenMap, tasks, depths, blocksCount, indent + 1);
            }
        }
    }

    private static string BuildMarker(TaskComputed computed, TaskData task)
    {
        if (computed.EffectiveState == "Blocked")
        {
            return $" ⛔ blocked: {string.Join(",", computed.BlockedBy)}";
        }

        if (computed.NeedsReview)
        {
            return " ⚠ review";
        }

        if (task.Status == TaskStatus.Question && task.OpenQuestions.Count > 0)
        {
            return " 🟥 Question";
        }

        return string.Empty;
    }
}
