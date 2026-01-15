using UTEP.Cli.Domain;

namespace UTEP.Cli.Services;

public static class IssueBuilder
{
    public static ValidationIssue NotFound(string kind, string id, string path)
    {
        return new ValidationIssue
        {
            Code = "E440",
            Severity = "error",
            Message = $"{kind} not found",
            Locations = new List<IssueLocation>
            {
                new IssueLocation { Kind = kind, Id = id, Path = path }
            }
        };
    }

    public static ValidationIssue ParseError(string path, string? error)
    {
        return new ValidationIssue
        {
            Code = "E430",
            Severity = "error",
            Message = "Failed to parse JSON",
            Details = new Dictionary<string, object> { ["error"] = error ?? "Unknown error" },
            Locations = new List<IssueLocation>
            {
                new IssueLocation { Kind = "file", Path = path }
            }
        };
    }
}
