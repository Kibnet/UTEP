using UTEP.Cli.Domain;
using UTEP.Cli.Services;
using Xunit;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Tests;

public class TaskComputedTests
{
    [Fact]
    public void ShouldReturnWaitingDependenciesWhenBlocked()
    {
        var blocker = TestData.CreateTask("T-001", TaskStatus.Planned);
        var task = TestData.CreateTask("T-002", TaskStatus.Ready, blockedBy: new[] { "T-001" });
        var snapshot = TestData.CreateSnapshot(blocker, task);

        var builder = new TaskComputedBuilder();
        var graph = new TaskGraphBuilder();
        var computed = builder.Build(task.File, snapshot.Tasks, graph.BuildBlocksCount(snapshot.Tasks));

        Assert.Equal("WaitingDependencies", computed.EffectiveState);
        Assert.False(computed.IsUnblocked);
        Assert.Contains("T-001", computed.WaitingDependencies);
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
