using System.Text.Json;
using System.Text.Json.Serialization;
using UTEP.Cli;
using UTEP.Cli.Commands;
using UTEP.Cli.Domain;
using UTEP.Cli.IO;
using UTEP.Cli.Services;
using Xunit;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Tests;

public class ProtocolUpdatesTests
{
    [Fact]
    public void AttemptWithoutSessionShouldFail()
    {
        WithTempRepo((root, goalId) =>
        {
            Assert.Equal(0, Run("task", "new", "--id", "T-001", "--title", "Test Task", "--status", "Ready", "--success", "Done"));

            var exitCode = Run("task", "attempt", "T-001", "--evidence", "note");

            Assert.Equal(ExitCodes.ValidationError, exitCode);

            var task = ReadTask(Path.Combine(root, "goals", goalId, "tasks", "T-001.task.json"));
            Assert.Equal(0, task.Task.Attempts);
            Assert.Equal(0, task.Task.TimeSpentMinutes);
            Assert.Null(task.Task.ActiveAttemptStartedAt);
        });
    }

    [Fact]
    public void AttemptAfterStartShouldAutoAddTimeAndClearSession()
    {
        WithTempRepo((root, goalId) =>
        {
            Assert.Equal(0, Run("task", "new", "--id", "T-001", "--title", "Test Task", "--status", "Ready", "--success", "Done"));
            Assert.Equal(0, Run("task", "start", "T-001"));
            Assert.Equal(0, Run("task", "attempt", "T-001", "--evidence", "note"));

            var task = ReadTask(Path.Combine(root, "goals", goalId, "tasks", "T-001.task.json"));
            Assert.Equal(1, task.Task.Attempts);
            Assert.True(task.Task.TimeSpentMinutes >= 1);
            Assert.Null(task.Task.ActiveAttemptStartedAt);
        });
    }

    [Fact]
    public void CompleteWithMinutesShouldNotRequireSession()
    {
        WithTempRepo((root, goalId) =>
        {
            Assert.Equal(0, Run("task", "new", "--id", "T-001", "--title", "Test Task", "--status", "Ready", "--success", "Done"));
            Assert.Equal(0, Run("task", "set-status", "T-001", "InProgress", "--success", "Done"));

            var exitCode = Run("task", "complete", "T-001", "--evidence", "done", "--minutes", "12");

            Assert.Equal(ExitCodes.Success, exitCode);
            var task = ReadTask(Path.Combine(root, "goals", goalId, "tasks", "T-001.task.json"));
            Assert.Equal(12, task.Task.TimeSpentMinutes);
        });
    }

    [Fact]
    public void TaskNewShouldAutoRenderIndex()
    {
        WithTempRepo((root, goalId) =>
        {
            Assert.Equal(0, Run("task", "new", "--id", "T-001", "--title", "Test Task", "--status", "Ready", "--success", "Done"));

            var indexPath = Path.Combine(root, "goals", goalId, "index.md");
            var content = File.ReadAllText(indexPath);
            Assert.Contains("T-001", content);
        });
    }

    [Fact]
    public void BottlenecksShouldSkipTerminalTasks()
    {
        var terminal = TestData.CreateTask("T-001", TaskStatus.Completed);
        var blocked = TestData.CreateTask("T-002", TaskStatus.Planned, blockedBy: new[] { "T-001" });
        var snapshot = TestData.CreateSnapshot(terminal, blocked);

        var graph = new TaskGraphBuilder();
        var analyzer = new BottleneckAnalyzer();
        var depths = graph.BuildDepths(snapshot.Tasks);
        var blocksCount = graph.BuildBlocksCount(snapshot.Tasks);

        var items = analyzer.GetTop(snapshot.Tasks, depths, blocksCount, 5, new TaskComputedBuilder());

        Assert.DoesNotContain(items, item => item.Task.TaskId == "T-001");
    }

    [Fact]
    public void GoalStatusShouldBeComputedFromTasks()
    {
        var completed = TestData.CreateTask("T-001", TaskStatus.Completed);
        var cancelled = TestData.CreateTask("T-002", TaskStatus.Cancelled);
        var snapshot = TestData.CreateSnapshot(completed, cancelled);

        Assert.Equal(GoalStatus.Completed, StatusRules.ComputeGoalStatus(snapshot.Tasks));

        var inProgress = TestData.CreateTask("T-003", TaskStatus.InProgress);
        snapshot = TestData.CreateSnapshot(inProgress);

        Assert.Equal(GoalStatus.InProgress, StatusRules.ComputeGoalStatus(snapshot.Tasks));
    }

    private static void WithTempRepo(Action<string, string> action)
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

            action(tempRoot, goalId!);
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

    private static TaskFile ReadTask(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var taskFile = JsonSerializer.Deserialize<TaskFile>(File.ReadAllText(path), options);
        Assert.NotNull(taskFile);
        return taskFile!;
    }

    private static int Run(params string[] args)
    {
        return Program.Main(args).GetAwaiter().GetResult();
    }
}
