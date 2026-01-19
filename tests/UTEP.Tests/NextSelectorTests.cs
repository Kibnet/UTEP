using UTEP.Cli.Domain;
using UTEP.Cli.Services;
using Xunit;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Tests;

public class NextSelectorTests
{
    [Fact]
    public void ShouldReturnOnlyActionableTasks()
    {
        var ready = TestData.CreateTask("T-001", TaskStatus.Ready);
        var blocked = TestData.CreateTask("T-002", TaskStatus.Ready, blockedBy: new[] { "T-003" });
        var blocker = TestData.CreateTask("T-003", TaskStatus.Planned);

        var snapshot = TestData.CreateSnapshot(ready, blocked, blocker);
        var selector = new NextSelector();
        var graph = new TaskGraphBuilder();
        var depths = graph.BuildDepths(snapshot.Tasks);
        var blocksCount = graph.BuildBlocksCount(snapshot.Tasks);

        var actionable = selector.SelectActionable(snapshot.Tasks, depths, blocksCount, 10, new TaskComputedBuilder());

        Assert.Single(actionable);
        Assert.Equal("T-001", actionable[0].Task.TaskId);
    }

    [Fact]
    public void ShouldSortByDepthAndBlocksCount()
    {
        var root = TestData.CreateTask("T-001", TaskStatus.Ready);
        var child = TestData.CreateTask("T-002", TaskStatus.Ready, parentId: "T-001");
        var blockedA = TestData.CreateTask("T-003", TaskStatus.Planned, blockedBy: new[] { "T-001" });
        var blockedB = TestData.CreateTask("T-004", TaskStatus.Planned, blockedBy: new[] { "T-001" });

        var snapshot = TestData.CreateSnapshot(root, child, blockedA, blockedB);
        var selector = new NextSelector();
        var graph = new TaskGraphBuilder();
        var depths = graph.BuildDepths(snapshot.Tasks);
        var blocksCount = graph.BuildBlocksCount(snapshot.Tasks);

        var actionable = selector.SelectActionable(snapshot.Tasks, depths, blocksCount, 10, new TaskComputedBuilder());

        Assert.Equal("T-001", actionable[0].Task.TaskId);
    }

    [Fact]
    public void ShouldReturnQuestionReasonWhenOpenQuestionsExist()
    {
        var questionTask = TestData.CreateTask("T-001", TaskStatus.Question, openQuestions: 1);
        var snapshot = TestData.CreateSnapshot(questionTask);
        var selector = new NextSelector();
        var graph = new TaskGraphBuilder();
        var blocksCount = graph.BuildBlocksCount(snapshot.Tasks);

        var reason = selector.ResolveNoActionableReason(snapshot.Tasks, blocksCount, new TaskComputedBuilder());

        Assert.Equal("question", reason);
    }

    [Fact]
    public void ShouldReturnBlockedReasonWhenBlockedTasksExist()
    {
        var blocked = TestData.CreateTask("T-001", TaskStatus.Ready, blockedBy: new[] { "T-002" });
        var blocker = TestData.CreateTask("T-002", TaskStatus.Planned);
        var snapshot = TestData.CreateSnapshot(blocked, blocker);
        var selector = new NextSelector();
        var graph = new TaskGraphBuilder();
        var blocksCount = graph.BuildBlocksCount(snapshot.Tasks);

        var reason = selector.ResolveNoActionableReason(snapshot.Tasks, blocksCount, new TaskComputedBuilder());

        Assert.Equal("blocked", reason);
    }

    [Fact]
    public void ShouldReturnNoneReasonWhenNoQuestionsOrBlockedTasksExist()
    {
        var planned = TestData.CreateTask("T-001", TaskStatus.Planned);
        var snapshot = TestData.CreateSnapshot(planned);
        var selector = new NextSelector();
        var graph = new TaskGraphBuilder();
        var blocksCount = graph.BuildBlocksCount(snapshot.Tasks);

        var reason = selector.ResolveNoActionableReason(snapshot.Tasks, blocksCount, new TaskComputedBuilder());

        Assert.Equal("none", reason);
    }
}
