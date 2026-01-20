using System.Text.Json;
using UTEP.Cli.Domain;
using UTEP.Cli.IO;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;

namespace UTEP.Cli.Services;

public sealed class LogWriter
{
    private readonly IClock _clock;

    public LogWriter(IClock clock)
    {
        _clock = clock;
    }

    public string AppendStatusChange(string logFile, string goalId, string taskId, TaskStatus from, TaskStatus to, string? note)
    {
        var eventId = $"evt-{Guid.NewGuid():N}".Substring(0, 12);
        var payload = new Dictionary<string, object?>
        {
            ["id"] = eventId,
            ["at"] = _clock.Now.ToString("o"),
            ["actor"] = "cli",
            ["event"] = "task.status_changed",
            ["goal_id"] = goalId,
            ["task_id"] = taskId,
            ["from"] = from.ToString(),
            ["to"] = to.ToString(),
            ["note"] = note
        };

        WriteNdjson(logFile, payload);
        return eventId;
    }

    public void AppendAttempt(string logFile, string goalId, string taskId, string note)
    {
        var payload = new Dictionary<string, object?>
        {
            ["at"] = _clock.Now.ToString("o"),
            ["actor"] = "cli",
            ["event"] = "task.attempt",
            ["goal_id"] = goalId,
            ["task_id"] = taskId,
            ["note"] = note
        };

        WriteNdjson(logFile, payload);
    }

    private static void WriteNdjson(string logFile, Dictionary<string, object?> payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonDefaults.Options);
        if (!json.EndsWith('\n'))
        {
            json += '\n';
        }

        var directory = Path.GetDirectoryName(logFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(logFile, json, JsonDefaults.Utf8NoBom);
    }
}
