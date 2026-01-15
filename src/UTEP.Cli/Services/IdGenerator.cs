namespace UTEP.Cli.Services;

public sealed class IdGenerator
{
    public string NextGoalId(string goalsDir, DateTimeOffset now)
    {
        var year = now.Year;
        var max = 0;
        if (Directory.Exists(goalsDir))
        {
            foreach (var dir in Directory.GetDirectories(goalsDir, "G-*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(dir);
                var parts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3 && int.TryParse(parts[1], out var goalYear) && goalYear == year &&
                    int.TryParse(parts[2], out var number))
                {
                    max = Math.Max(max, number);
                }
            }
        }

        return $"G-{year}-{(max + 1):000}";
    }

    public string NextTaskId(string tasksDir)
    {
        var max = 0;
        if (Directory.Exists(tasksDir))
        {
            foreach (var file in Directory.GetFiles(tasksDir, "T-*.task.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name is null)
                {
                    continue;
                }

                var idPart = name.Replace(".task", string.Empty, StringComparison.OrdinalIgnoreCase);
                var parts = idPart.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[1], out var number))
                {
                    max = Math.Max(max, number);
                }
            }
        }

        return $"T-{(max + 1):000}";
    }
}
