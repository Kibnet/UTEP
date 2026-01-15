using UTEP.Cli.Domain;
using UTEP.Cli.IO;

namespace UTEP.Cli.Services;

public sealed class RepositoryLoader
{
    private readonly JsonFileStore _store;

    public RepositoryLoader(JsonFileStore store)
    {
        _store = store;
    }

    public TaskSnapshot Load(RepoPaths paths, string goalId, List<ValidationIssue> issues)
    {
        var goalPath = paths.GoalFile(goalId);
        if (!File.Exists(goalPath))
        {
            issues.Add(IssueBuilder.NotFound("goal", goalId, goalPath));
            return new TaskSnapshot(new GoalFile { Goal = new GoalData { Id = goalId } }, new Dictionary<string, TaskInfo>());
        }

        var goalFile = _store.ReadFile<GoalFile>(goalPath, out var goalError);
        if (goalFile == null)
        {
            issues.Add(IssueBuilder.ParseError(goalPath, goalError));
            return new TaskSnapshot(new GoalFile { Goal = new GoalData { Id = goalId } }, new Dictionary<string, TaskInfo>());
        }

        var taskInfos = new Dictionary<string, TaskInfo>(StringComparer.OrdinalIgnoreCase);
        var tasksDir = paths.TasksDir(goalId);
        if (Directory.Exists(tasksDir))
        {
            foreach (var file in Directory.GetFiles(tasksDir, "*.task.json", SearchOption.TopDirectoryOnly))
            {
                var taskFile = _store.ReadFile<TaskFile>(file, out var error);
                if (taskFile == null)
                {
                    issues.Add(IssueBuilder.ParseError(file, error));
                    continue;
                }

                var taskId = taskFile.Task.Id;
                if (!string.IsNullOrWhiteSpace(taskId))
                {
                    taskInfos[taskId] = new TaskInfo(taskFile, file);
                }
            }
        }

        return new TaskSnapshot(goalFile, taskInfos);
    }
}
