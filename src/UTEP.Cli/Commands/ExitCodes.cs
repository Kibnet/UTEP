namespace UTEP.Cli.Commands;

public static class ExitCodes
{
    public const int Success = 0;
    public const int ValidationError = 2;
    public const int NotFound = 3;
    public const int InvalidTransition = 4;
    public const int NoActionable = 5;
    public const int InvalidCommand = 1;
}
