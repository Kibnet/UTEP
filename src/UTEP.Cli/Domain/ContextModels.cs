using System.Text.Json.Serialization;

namespace UTEP.Cli.Domain;

public sealed class ContextFile
{
    [JsonPropertyOrder(1)]
    public string? CurrentGoalId { get; set; }
}
