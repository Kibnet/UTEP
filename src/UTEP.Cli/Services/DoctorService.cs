using UTEP.Cli.Domain;
using UTEP.Cli.IO;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Cli.Services;

public sealed class DoctorService
{
    private readonly JsonFileStore _store;
    private readonly IClock _clock;

    public DoctorService(JsonFileStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public DoctorResult ApplyFixes(TaskSnapshot snapshot, RepoPaths paths, List<ValidationIssue> issues, bool fix)
    {
        var actions = new List<DoctorAction>();
        var fixedCount = 0;

        foreach (var issue in issues)
        {
            if (!fix)
            {
                continue;
            }

            switch (issue.Code)
            {
                case "E001":
                    fixedCount += FixMissingTask(snapshot, paths, actions, issue);
                    break;
                case "E002":
                    fixedCount += FixOrphanParent(snapshot, paths, actions, issue);
                    break;
                case "E004":
                    fixedCount += FixMissingEvidence(snapshot, paths, actions, issue);
                    break;
                case "E005":
                    fixedCount += FixMissingQuestion(snapshot, paths, actions, issue);
                    break;
            }
        }

        var remainingIssues = fix ? new ValidationService().Validate(snapshot, paths.Root) : issues;
        var errorCount = remainingIssues.Count(issue => issue.Severity == "error");

        return new DoctorResult
        {
            Summary = new DoctorSummary
            {
                ErrorsBefore = issues.Count(issue => issue.Severity == "error"),
                ErrorsAfter = errorCount,
                Fixed = fixedCount,
                RequiresManual = errorCount
            },
            Actions = actions,
            RemainingIssues = remainingIssues
        };
    }

    private int FixMissingTask(TaskSnapshot snapshot, RepoPaths paths, List<DoctorAction> actions, ValidationIssue issue)
    {
        if (issue.Details == null || !issue.Details.TryGetValue("missing_task_id", out var idObj))
        {
            return 0;
        }

        var taskId = idObj?.ToString();
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return 0;
        }

        if (snapshot.Tasks.ContainsKey(taskId))
        {
            return 0;
        }

        var goalId = snapshot.Goal.Goal.Id;
        var taskFile = new TaskFile
        {
            Version = 1,
            Task = new TaskData
            {
                Id = taskId,
                GoalId = goalId,
                Title = $"Placeholder для {taskId}",
                Status = TaskStatus.Draft,
                SuccessCriteria = new List<string>()
            },
            Links = new TaskLinks()
        };

        var filePath = Path.Combine(paths.TasksDir(goalId), $"{taskId}.task.json");
        _store.WriteFileAtomic(filePath, taskFile);
        snapshot.Tasks[taskId] = new TaskInfo(taskFile, filePath);

        actions.Add(new DoctorAction
        {
            IssueCode = "E001",
            RemedyId = "R2",
            Applied = true,
            CommandsExecuted = new List<string> { $"utep task new --id {taskId} --title \"Placeholder\"" }
        });

        return 1;
    }

    private int FixOrphanParent(TaskSnapshot snapshot, RepoPaths paths, List<DoctorAction> actions, ValidationIssue issue)
    {
        var location = issue.Locations.FirstOrDefault();
        if (location?.Id == null || !snapshot.Tasks.TryGetValue(location.Id, out var info))
        {
            return 0;
        }

        info.File.Task.ParentId = null;
        _store.WriteFileAtomic(info.Path, info.File);

        actions.Add(new DoctorAction
        {
            IssueCode = "E002",
            RemedyId = "R1",
            Applied = true,
            CommandsExecuted = new List<string> { $"utep task set-status {location.Id} {info.File.Task.Status}" }
        });

        return 1;
    }

    private int FixMissingEvidence(TaskSnapshot snapshot, RepoPaths paths, List<DoctorAction> actions, ValidationIssue issue)
    {
        var location = issue.Locations.FirstOrDefault();
        if (location?.Id == null || !snapshot.Tasks.TryGetValue(location.Id, out var info))
        {
            return 0;
        }

        info.File.Task.Evidence.Add(new Evidence
        {
            Kind = "note",
            Text = "TODO: добавить evidence",
            At = _clock.Now.ToString("o")
        });
        _store.WriteFileAtomic(info.Path, info.File);

        actions.Add(new DoctorAction
        {
            IssueCode = "E004",
            RemedyId = "R1",
            Applied = true,
            CommandsExecuted = new List<string> { $"utep task attempt {location.Id} --note \"Add evidence\"" }
        });

        return 1;
    }

    private int FixMissingQuestion(TaskSnapshot snapshot, RepoPaths paths, List<DoctorAction> actions, ValidationIssue issue)
    {
        var location = issue.Locations.FirstOrDefault();
        if (location?.Id == null || !snapshot.Tasks.TryGetValue(location.Id, out var info))
        {
            return 0;
        }

        info.File.Task.OpenQuestions.Add(new OpenQuestion
        {
            Id = "Q-01",
            Kind = "process",
            Question = "Нужна дополнительная информация",
            Options = new List<QuestionOption>(),
            Recommendation = null,
            RequestedAnswer = "Предоставьте ответ",
            CreatedAt = _clock.Now.ToString("o")
        });
        _store.WriteFileAtomic(info.Path, info.File);

        actions.Add(new DoctorAction
        {
            IssueCode = "E005",
            RemedyId = "R1",
            Applied = true,
            CommandsExecuted = new List<string> { $"utep task block {location.Id} --question-file questions/{location.Id}.question.json" }
        });

        return 1;
    }
}
