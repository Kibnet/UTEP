using UTEP.Cli.Domain;

namespace UTEP.Cli.Services;

public sealed class TaskSnapshot
{
    public TaskSnapshot(GoalFile goal, Dictionary<string, TaskInfo> tasks)
    {
        Goal = goal;
        Tasks = tasks;
    }

    public GoalFile Goal { get; }

    public Dictionary<string, TaskInfo> Tasks { get; }
}

public sealed class TaskInfo
{
    public TaskInfo(TaskFile file, string path)
    {
        File = file;
        Path = path;
    }

    public TaskFile File { get; }

    public string Path { get; }
}
