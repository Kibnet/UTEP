namespace UTEP.Cli.IO;

public sealed class RepoPaths
{
    public RepoPaths(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public string ConfigFile => Path.Combine(Root, "utep.config.json");

    public string ContextFile => Path.Combine(Root, ".utep", "context.json");

    public string GoalsDir => Path.Combine(Root, "goals");

    public string GoalDir(string goalId) => Path.Combine(GoalsDir, goalId);

    public string GoalFile(string goalId) => Path.Combine(GoalDir(goalId), "goal.json");

    public string TasksDir(string goalId) => Path.Combine(GoalDir(goalId), "tasks");

    public string LogsDir(string goalId) => Path.Combine(GoalDir(goalId), "logs");

    public string LogFile(string goalId) => Path.Combine(LogsDir(goalId), "utep.log.ndjson");

    public string ArtifactsDir(string goalId) => Path.Combine(GoalDir(goalId), "artifacts");

    public string IndexFile(string goalId, string indexFilename) =>
        Path.Combine(GoalDir(goalId), indexFilename);

    public string ReportFile(string goalId) =>
        Path.Combine(GoalDir(goalId), "report.md");
}
