using UTEP.Cli.Domain;
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
    public void ShouldDetectBlockedWithoutQuestion()
    {
        var task = TestData.CreateTask("T-001", TaskStatus.Blocked);
        var snapshot = TestData.CreateSnapshot(task);
        var service = new ValidationService();

        var issues = service.Validate(snapshot, "c:/repo");

        Assert.Contains(issues, issue => issue.Code == "E005");
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
