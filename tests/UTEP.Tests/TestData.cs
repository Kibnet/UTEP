using UTEP.Cli.Domain;
using UTEP.Cli.Services;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Tests;

public static class TestData
{
    public static TaskInfo CreateTask(
        string id,
        TaskStatus status,
        string? parentId = null,
        IEnumerable<string>? blockedBy = null,
        bool includeDependencies = true,
        bool nullBlockedBy = false,
        int openQuestions = 0)
    {
        TaskDependencies? dependencies = null;
        if (includeDependencies)
        {
            dependencies = new TaskDependencies
            {
                BlockedBy = nullBlockedBy ? null : (blockedBy?.ToList() ?? new List<string>())
            };
        }

        var task = new TaskFile
        {
            Version = 1,
            Task = new TaskData
            {
                Id = id,
                GoalId = "G-2026-001",
                ParentId = parentId,
                Title = $"Task {id}",
                Status = status,
                Priority = 2,
                Dependencies = dependencies,
                OpenQuestions = Enumerable.Range(1, openQuestions)
                    .Select(index => new OpenQuestion
                    {
                        Id = $"Q-{index:00}",
                        Kind = "test",
                        Question = "Test question",
                        RequestedAnswer = "Answer",
                        CreatedAt = "2026-01-01T00:00:00Z"
                    })
                    .ToList()
            },
            Links = new TaskLinks()
        };

        return new TaskInfo(task, $"goals/G-2026-001/tasks/{id}.task.json");
    }

    public static TaskSnapshot CreateSnapshot(params TaskInfo[] tasks)
    {
        var goal = new GoalFile
        {
            Version = 1,
            Goal = new GoalData { Id = "G-2026-001", Title = "Test" },
            Meta = new GoalMeta()
        };

        var map = tasks.ToDictionary(info => info.File.Task.Id, info => info);
        return new TaskSnapshot(goal, map);
    }
}
