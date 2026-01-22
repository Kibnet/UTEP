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
            .Where(item => item.Computed.EffectiveState == EffectiveState.Blocked)
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
        File.WriteAllText(indexPath, builder.ToString(), JsonDefaults.Utf8NoBom);

        return new RenderResult
        {
            Rendered = true,
            Files = new List<string> { indexPath.Replace('\\', '/') }
        };
    }

    public RenderResult RenderReport(TaskSnapshot snapshot, RepoPaths paths)
    {
        var tasks = snapshot.Tasks;
        var childrenMap = _graphBuilder.BuildChildrenMap(tasks);
        var depths = _graphBuilder.BuildDepths(tasks);
        var blocksCount = _graphBuilder.BuildBlocksCount(tasks);

        var computedStatus = StatusRules.ComputeGoalStatus(tasks);
        var counts = tasks.Values
            .GroupBy(info => info.File.Task.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (TaskStatus statusValue in Enum.GetValues<TaskStatus>())
        {
            if (!counts.ContainsKey(statusValue))
            {
                counts[statusValue] = 0;
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine($"# Отчет выполнения: {snapshot.Goal.Goal.Title}");
        builder.AppendLine();
        builder.AppendLine($"- Цель: {snapshot.Goal.Goal.Id}");
        builder.AppendLine($"- Статус (goal.json): {snapshot.Goal.Goal.Status}");
        builder.AppendLine($"- Вычисленный статус: {computedStatus}");
        builder.AppendLine();
        builder.AppendLine("## Статусы задач");
        builder.AppendLine();
        foreach (var statusValue in Enum.GetValues<TaskStatus>())
        {
            builder.AppendLine($"- {statusValue}: {counts[statusValue]}");
        }

        builder.AppendLine();
        builder.AppendLine("## Критерии успеха цели");
        builder.AppendLine();
        if (snapshot.Goal.Goal.SuccessCriteria.Count == 0)
        {
            builder.AppendLine("- Нет критериев.");
        }
        else
        {
            foreach (var item in snapshot.Goal.Goal.SuccessCriteria)
            {
                builder.AppendLine($"- {item}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Дерево задач");
        builder.AppendLine();
        var roots = tasks.Values
            .Where(info => string.IsNullOrWhiteSpace(info.File.Task.ParentId) || !tasks.ContainsKey(info.File.Task.ParentId))
            .Select(info => info.File.Task.Id)
            .OrderBy(id => depths.TryGetValue(id, out var depth) ? depth : 0)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var root in roots)
        {
            RenderNode(builder, root, childrenMap, tasks, depths, blocksCount, 0);
        }

        builder.AppendLine();
        builder.AppendLine("## Таблица задач");
        builder.AppendLine();
        builder.AppendLine("| ID | Статус | Попытки | Время (мин) | Заголовок | Последний evidence |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var info in tasks.Values.OrderBy(info => info.File.Task.Id, StringComparer.OrdinalIgnoreCase))
        {
            var task = info.File.Task;
            var lastEvidence = task.Evidence.LastOrDefault()?.Text ?? string.Empty;
            builder.AppendLine($"| {task.Id} | {task.Status} | {task.Attempts} | {task.TimeSpentMinutes} | {EscapePipe(task.Title)} | {EscapePipe(lastEvidence)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Вопросы и ответы");
        builder.AppendLine();
        var questions = tasks.Values
            .SelectMany(info => info.File.Task.OpenQuestions.Select(question => (info.File.Task.Id, question)))
            .ToList();

        if (questions.Count == 0)
        {
            builder.AppendLine("- Нет открытых вопросов.");
        }
        else
        {
            foreach (var (taskId, question) in questions)
            {
                builder.AppendLine($"- {taskId} ({question.Id}): {question.Question}");
                if (!string.IsNullOrWhiteSpace(question.Answer))
                {
                    builder.AppendLine($"  - Ответ: {question.Answer}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Зависимости и блокировки");
        builder.AppendLine();
        var dependencies = tasks.Values
            .Select(info => new
            {
                info.File.Task.Id,
                BlockedBy = info.File.Task.Dependencies?.BlockedBy ?? new List<string>()
            })
            .Where(item => item.BlockedBy.Count > 0)
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (dependencies.Count == 0)
        {
            builder.AppendLine("- Нет зависимостей.");
        }
        else
        {
            foreach (var item in dependencies)
            {
                builder.AppendLine($"- {item.Id} -> {string.Join(", ", item.BlockedBy)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Лента логов (последние 20 событий)");
        builder.AppendLine();
        var logPath = paths.LogFile(snapshot.Goal.Goal.Id);
        if (!File.Exists(logPath))
        {
            builder.AppendLine("- Лог отсутствует.");
        }
        else
        {
            var lines = File.ReadAllLines(logPath);
            var tail = lines.Skip(Math.Max(0, lines.Length - 20)).ToList();
            if (tail.Count == 0)
            {
                builder.AppendLine("- Лог пуст.");
            }
            else
            {
                builder.AppendLine("```json");
                foreach (var line in tail)
                {
                    builder.AppendLine(line);
                }
                builder.AppendLine("```");
            }
        }

        var reportPath = paths.ReportFile(snapshot.Goal.Goal.Id);
        File.WriteAllText(reportPath, builder.ToString(), JsonDefaults.Utf8NoBom);

        return new RenderResult
        {
            Rendered = true,
            Files = new List<string> { reportPath.Replace('\\', '/') }
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
        if (computed.EffectiveState == EffectiveState.Blocked)
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

    private static string EscapePipe(string value)
    {
        return value.Replace("|", "\\|");
    }
}
