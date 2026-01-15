using UTEP.Cli.Domain;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Cli.Services;

public sealed class ValidationService
{
    public List<ValidationIssue> Validate(TaskSnapshot snapshot, string repoRoot)
    {
        var issues = new List<ValidationIssue>();
        var tasks = snapshot.Tasks;
        var taskIds = new HashSet<string>(tasks.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var (taskId, info) in tasks)
        {
            var task = info.File.Task;
            foreach (var blocker in task.Dependencies.BlockedBy)
            {
                if (!taskIds.Contains(blocker))
                {
                    issues.Add(new ValidationIssue
                    {
                        Code = "E001",
                        Severity = "error",
                        Message = "Missing task file",
                        Details = new Dictionary<string, object> { ["missing_task_id"] = blocker },
                        Locations = new List<IssueLocation>
                        {
                            new IssueLocation
                            {
                                Kind = "task",
                                Id = taskId,
                                Path = ToRepoPath(repoRoot, info.Path),
                                JsonPointer = "/task/dependencies/blocked_by"
                            }
                        },
                        Remedies = new List<Remedy>
                        {
                            new Remedy
                            {
                                Id = "R1",
                                Title = $"Remove dependency {blocker} from {taskId}",
                                Commands = new List<string>
                                {
                                    $"utep task dep rm {taskId} --blocked-by {blocker}"
                                }
                            },
                            new Remedy
                            {
                                Id = "R2",
                                Title = $"Create placeholder task {blocker}",
                                Commands = new List<string>
                                {
                                    $"utep task new --id {blocker} --title \"Placeholder\""
                                }
                            }
                        }
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(task.ParentId) && !taskIds.Contains(task.ParentId))
            {
                issues.Add(new ValidationIssue
                {
                    Code = "E002",
                    Severity = "error",
                    Message = "Orphan parent",
                    Details = new Dictionary<string, object> { ["missing_parent_id"] = task.ParentId! },
                    Locations = new List<IssueLocation>
                    {
                        new IssueLocation
                        {
                            Kind = "task",
                            Id = taskId,
                            Path = ToRepoPath(repoRoot, info.Path),
                            JsonPointer = "/task/parent_id"
                        }
                    },
                    Remedies = new List<Remedy>
                    {
                        new Remedy
                        {
                            Id = "R1",
                            Title = "Set parent_id to null",
                            Commands = new List<string> { $"utep task set-status {taskId} {task.Status}" }
                        }
                    }
                });
            }

            if (task.Status == TaskStatus.Completed && task.Evidence.Count == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Code = "E004",
                    Severity = "error",
                    Message = "Completed task without evidence",
                    Locations = new List<IssueLocation>
                    {
                        new IssueLocation
                        {
                            Kind = "task",
                            Id = taskId,
                            Path = ToRepoPath(repoRoot, info.Path),
                            JsonPointer = "/task/evidence"
                        }
                    },
                    Remedies = new List<Remedy>
                    {
                        new Remedy
                        {
                            Id = "R1",
                            Title = "Add evidence note",
                            Commands = new List<string>
                            {
                                $"utep task attempt {taskId} --note \"Add evidence\""
                            }
                        }
                    }
                });
            }

            if (task.Status == TaskStatus.Blocked && task.OpenQuestions.Count == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Code = "E005",
                    Severity = "error",
                    Message = "Blocked task without question",
                    Locations = new List<IssueLocation>
                    {
                        new IssueLocation
                        {
                            Kind = "task",
                            Id = taskId,
                            Path = ToRepoPath(repoRoot, info.Path),
                            JsonPointer = "/task/open_questions"
                        }
                    },
                    Remedies = new List<Remedy>
                    {
                        new Remedy
                        {
                            Id = "R1",
                            Title = "Add placeholder question",
                            Commands = new List<string>
                            {
                                $"utep task block {taskId} --question-file questions/{taskId}.question.json"
                            }
                        }
                    }
                });
            }
        }

        issues.AddRange(FindDependencyCycles(snapshot, repoRoot));
        return issues;
    }

    private static IEnumerable<ValidationIssue> FindDependencyCycles(TaskSnapshot snapshot, string repoRoot)
    {
        var tasks = snapshot.Tasks;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var taskId in tasks.Keys)
        {
            foreach (var issue in DetectCycle(taskId, tasks, visited, stack, new Stack<string>(), repoRoot))
            {
                yield return issue;
            }
        }
    }

    private static IEnumerable<ValidationIssue> DetectCycle(
        string taskId,
        IReadOnlyDictionary<string, TaskInfo> tasks,
        HashSet<string> visited,
        HashSet<string> stack,
        Stack<string> path,
        string repoRoot)
    {
        if (stack.Contains(taskId))
        {
            var cycle = path.Reverse().Concat(new[] { taskId }).ToList();
            yield return new ValidationIssue
            {
                Code = "E003",
                Severity = "error",
                Message = "Dependency cycle detected",
                Details = new Dictionary<string, object> { ["cycle"] = cycle },
                Locations = new List<IssueLocation>
                {
                    new IssueLocation { Kind = "task", Id = taskId, Path = ToRepoPath(repoRoot, tasks[taskId].Path) }
                },
                Remedies = new List<Remedy>
                {
                    new Remedy
                    {
                        Id = "R1",
                        Title = "Remove one dependency from the cycle",
                        Commands = new List<string> { "utep task dep rm <task_id> --blocked-by <id>" }
                    }
                }
            };
            yield break;
        }

        if (!visited.Add(taskId))
        {
            yield break;
        }

        stack.Add(taskId);
        path.Push(taskId);

        if (tasks.TryGetValue(taskId, out var info))
        {
            foreach (var blocker in info.File.Task.Dependencies.BlockedBy)
            {
                if (tasks.ContainsKey(blocker))
                {
                    foreach (var issue in DetectCycle(blocker, tasks, visited, stack, path, repoRoot))
                    {
                        yield return issue;
                    }
                }
            }
        }

        path.Pop();
        stack.Remove(taskId);
    }

    private static string ToRepoPath(string repoRoot, string path)
    {
        return path.Replace(repoRoot, string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimStart(Path.DirectorySeparatorChar)
            .Replace('\\', '/');
    }
}
