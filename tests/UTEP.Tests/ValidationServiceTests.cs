using System.Text.Json;
using System.Text.Json.Serialization;
using UTEP.Cli;
using UTEP.Cli.Domain;
using UTEP.Cli.IO;
using UTEP.Cli.Services;
using Xunit;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Tests;

public class ValidationServiceTests
{
    [Fact]
    public void ShouldDetectMissingDependency()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Ready, blockedBy: new[] { "T-404" });
        var snapshot = TestData.CreateSnapshot(task);
        var service = new ValidationService();

        var issues = service.Validate(snapshot, "c:/repo");

        Assert.Contains(issues, issue => issue.Code == "E001");
    }

    [Fact]
    public void ShouldDetectCompletedWithoutEvidence()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Completed);
        var snapshot = TestData.CreateSnapshot(task);
        var service = new ValidationService();

        var issues = service.Validate(snapshot, "c:/repo");

        Assert.Contains(issues, issue => issue.Code == "E004");
    }

    [Fact]
    public void ShouldDetectQuestionWithoutOpenQuestions()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Question);
        var snapshot = TestData.CreateSnapshot(task);
        var service = new ValidationService();

        var issues = service.Validate(snapshot, "c:/repo");

        Assert.Contains(issues, issue => issue.Code == "E008");
    }

    [Fact]
    public void ShouldDetectMissingSuccessCriteriaForActionableStatuses()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Ready);
        var snapshot = TestData.CreateSnapshot(task);
        var service = new ValidationService();

        var issues = service.Validate(snapshot, "c:/repo");

        var issue = Assert.Single(issues, item => item.Code == "E006");
        Assert.Contains("utep task set-status", issue.Message);
    }

    [Fact]
    public void ShouldDetectMissingBlockedByList()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Planned, includeDependencies: true, nullBlockedBy: true);
        var snapshot = TestData.CreateSnapshot(task);
        var service = new ValidationService();

        var issues = service.Validate(snapshot, "c:/repo");

        Assert.Contains(issues, issue => issue.Code == "E007");
    }

    [Fact]
    public void ShouldDetectDependencyCycle()
    {
        var taskA = TestData.CreateTask("T-001", TaskStatus.Ready, blockedBy: new[] { "T-002" });
        var taskB = TestData.CreateTask("T-002", TaskStatus.Ready, blockedBy: new[] { "T-001" });
        var snapshot = TestData.CreateSnapshot(taskA, taskB);
        var service = new ValidationService();

        var issues = service.Validate(snapshot, "c:/repo");

        Assert.Contains(issues, issue => issue.Code == "E003");
    }
}

public class CliQuestionAnswerTests
{
    [Fact]
    public void ShouldStoreAnswerWithoutChangingQuestionStatus()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var previous = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempRoot);

            Assert.Equal(0, Run("init"));
            Assert.Equal(0, Run("goal", "new", "--title", "Test Goal"));

            var goalDir = Directory.GetDirectories(Path.Combine(tempRoot, "goals")).Single();
            var goalId = Path.GetFileName(goalDir);
            Assert.False(string.IsNullOrWhiteSpace(goalId));

            Assert.Equal(0, Run("goal", "open", goalId!));
            Assert.Equal(0, Run("task", "new", "--id", "T-001", "--title", "Test Task", "--status", "Ready", "--success", "Done"));
            Assert.Equal(0, Run("task", "question", "T-001", "--kind", "decision", "--question", "Choose", "--requested-answer", "Pick one", "--option", "O-1:Yes"));
            Assert.Equal(0, Run("task", "answer", "T-001", "--option", "O-1"));

            var taskPath = Path.Combine(goalDir, "tasks", "T-001.task.json");
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
            var taskFile = JsonSerializer.Deserialize<TaskFile>(File.ReadAllText(taskPath), options);

            Assert.NotNull(taskFile);
            Assert.Equal(TaskStatus.Question, taskFile!.Task.Status);
            Assert.Single(taskFile.Task.OpenQuestions);
            Assert.Equal("O-1", taskFile.Task.OpenQuestions[0].Answer);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private static int Run(params string[] args)
    {
        return Program.Main(args).GetAwaiter().GetResult();
    }
}
