using UTEP.Cli.Domain;
using UTEP.Cli.Services;
using Xunit;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Tests;

public class TaskComputedTests
{
    [Fact]
    public void ShouldReturnBlockedWhenWaitingOnDependencies()
    {
        var blocker = TestData.CreateTask("T-001", TaskStatus.Planned);
        var task = TestData.CreateTask("T-002", TaskStatus.Ready, blockedBy: new[] { "T-001" });
        var snapshot = TestData.CreateSnapshot(blocker, task);

        var builder = new TaskComputedBuilder();
        var graph = new TaskGraphBuilder();
        var computed = builder.Build(task.File, snapshot.Tasks, graph.BuildBlocksCount(snapshot.Tasks));

        Assert.Equal(EffectiveState.Blocked, computed.EffectiveState);
        Assert.False(computed.IsUnblocked);
        Assert.Contains("T-001", computed.BlockedBy);
    }

    [Fact]
    public void ShouldReturnExecuteWhenReadyAndUnblocked()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Ready);
        var snapshot = TestData.CreateSnapshot(task);

        var builder = new TaskComputedBuilder();
        var graph = new TaskGraphBuilder();
        var computed = builder.Build(task.File, snapshot.Tasks, graph.BuildBlocksCount(snapshot.Tasks));

        Assert.Equal(EffectiveState.Execute, computed.EffectiveState);
    }

    [Fact]
    public void ShouldReturnContinueWhenInProgress()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.InProgress);
        var snapshot = TestData.CreateSnapshot(task);

        var builder = new TaskComputedBuilder();
        var graph = new TaskGraphBuilder();
        var computed = builder.Build(task.File, snapshot.Tasks, graph.BuildBlocksCount(snapshot.Tasks));

        Assert.Equal(EffectiveState.Continue, computed.EffectiveState);
    }

    [Fact]
    public void ShouldReturnClarifyWhenQuestion()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Question, openQuestions: 1);
        var snapshot = TestData.CreateSnapshot(task);

        var builder = new TaskComputedBuilder();
        var graph = new TaskGraphBuilder();
        var computed = builder.Build(task.File, snapshot.Tasks, graph.BuildBlocksCount(snapshot.Tasks));

        Assert.Equal(EffectiveState.Clarify, computed.EffectiveState);
    }

    [Fact]
    public void ShouldReturnPlanWhenDraft()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Draft);
        var snapshot = TestData.CreateSnapshot(task);

        var builder = new TaskComputedBuilder();
        var graph = new TaskGraphBuilder();
        var computed = builder.Build(task.File, snapshot.Tasks, graph.BuildBlocksCount(snapshot.Tasks));

        Assert.Equal(EffectiveState.Plan, computed.EffectiveState);
    }

    [Fact]
    public void ShouldReturnPlanWhenPlanned()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Planned);
        var snapshot = TestData.CreateSnapshot(task);

        var builder = new TaskComputedBuilder();
        var graph = new TaskGraphBuilder();
        var computed = builder.Build(task.File, snapshot.Tasks, graph.BuildBlocksCount(snapshot.Tasks));

        Assert.Equal(EffectiveState.Plan, computed.EffectiveState);
    }

    [Fact]
    public void ShouldReturnTerminalWhenCompleted()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Completed);
        var snapshot = TestData.CreateSnapshot(task);

        var builder = new TaskComputedBuilder();
        var graph = new TaskGraphBuilder();
        var computed = builder.Build(task.File, snapshot.Tasks, graph.BuildBlocksCount(snapshot.Tasks));

        Assert.Equal(EffectiveState.Terminal, computed.EffectiveState);
    }

    [Fact]
    public void ShouldFlagNeedsReviewWhenDependencyCancelled()
    {
        var blocker = TestData.CreateTask("T-001", TaskStatus.Cancelled);
        var task = TestData.CreateTask("T-002", TaskStatus.Ready, blockedBy: new[] { "T-001" });
        var snapshot = TestData.CreateSnapshot(blocker, task);

        var builder = new TaskComputedBuilder();
        var graph = new TaskGraphBuilder();
        var computed = builder.Build(task.File, snapshot.Tasks, graph.BuildBlocksCount(snapshot.Tasks));

        Assert.True(computed.NeedsReview);
    }
}
