using UTEP.Cli.Domain;
using UTEP.Cli.Services;
using Xunit;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Tests;

public class NextSelectorTests
{
    [Fact]
    public void ShouldReturnAllNonTerminalTasks()
    {
        var ready = TestData.CreateTask("T-001", TaskStatus.Ready);
        var blocked = TestData.CreateTask("T-002", TaskStatus.Ready, blockedBy: new[] { "T-006" });
        var blocker = TestData.CreateTask("T-006", TaskStatus.Planned);
        var planned = TestData.CreateTask("T-003", TaskStatus.Planned);
        var question = TestData.CreateTask("T-004", TaskStatus.Question, openQuestions: 1);
        var inProgress = TestData.CreateTask("T-005", TaskStatus.InProgress);
        var completed = TestData.CreateTask("T-007", TaskStatus.Completed);

        var snapshot = TestData.CreateSnapshot(ready, blocked, blocker, planned, question, inProgress, completed);
        var selector = new NextSelector();
        var graph = new TaskGraphBuilder();
        var depths = graph.BuildDepths(snapshot.Tasks);
        var blocksCount = graph.BuildBlocksCount(snapshot.Tasks);

        var actionable = selector.SelectActionable(snapshot.Tasks, depths, blocksCount, 10, new TaskComputedBuilder());

        Assert.Equal(6, actionable.Count);
        Assert.DoesNotContain(actionable, item => item.Task.TaskId == "T-007");
    }

    [Fact]
    public void ShouldSortByEffectiveStateThenDepthAndBlocksCount()
    {
        var inProgress = TestData.CreateTask("T-001", TaskStatus.InProgress);
        var ready = TestData.CreateTask("T-002", TaskStatus.Ready);
        var question = TestData.CreateTask("T-003", TaskStatus.Question, openQuestions: 1);
        var planned = TestData.CreateTask("T-004", TaskStatus.Planned);
        var blocked = TestData.CreateTask("T-005", TaskStatus.Ready, blockedBy: new[] { "T-006" });
        var blocker = TestData.CreateTask("T-006", TaskStatus.Planned);

        var snapshot = TestData.CreateSnapshot(inProgress, ready, question, planned, blocked, blocker);
        var selector = new NextSelector();
        var graph = new TaskGraphBuilder();
        var depths = graph.BuildDepths(snapshot.Tasks);
        var blocksCount = graph.BuildBlocksCount(snapshot.Tasks);

        var actionable = selector.SelectActionable(snapshot.Tasks, depths, blocksCount, 10, new TaskComputedBuilder());

        Assert.Equal("T-001", actionable[0].Task.TaskId);
        Assert.Equal("T-002", actionable[1].Task.TaskId);
        Assert.Equal("T-003", actionable[2].Task.TaskId);
        Assert.Equal("T-006", actionable[3].Task.TaskId);
        Assert.Equal("T-004", actionable[4].Task.TaskId);
        Assert.Equal("T-005", actionable[5].Task.TaskId);
    }

    [Fact]
    public void ShouldReturnNoneReasonWhenOnlyTerminalTasksExist()
    {
        var completed = TestData.CreateTask("T-001", TaskStatus.Completed);
        var cancelled = TestData.CreateTask("T-002", TaskStatus.Cancelled);
        var snapshot = TestData.CreateSnapshot(completed, cancelled);
        var selector = new NextSelector();
        var graph = new TaskGraphBuilder();
        var blocksCount = graph.BuildBlocksCount(snapshot.Tasks);

        var reason = selector.ResolveNoActionableReason(snapshot.Tasks, blocksCount, new TaskComputedBuilder());

        Assert.Equal("none", reason);
    }
}
