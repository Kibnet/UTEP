using System.Text.Json;

namespace UTEP.Cli.IO;

public sealed class JsonFileStore
{
    public T? ReadFile<T>(string path, out string? error)
    {
        error = null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return default;
        }
    }

    public void WriteFileAtomic<T>(string path, T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonDefaults.Options);
        if (!json.EndsWith('\n'))
        {
            json += '\n';
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, true);
    }
}
