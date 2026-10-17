namespace VibeUninstaller.Cli;

/// <summary>Process exit codes. Stable contract for scripts.</summary>
public static class ExitCode
{
    public const int Ok = 0;

    /// <summary>Bad usage, unexpected failure.</summary>
    public const int Error = 1;

    /// <summary>No application matched the query.</summary>
    public const int NotFound = 2;

    /// <summary>The query matched several applications.</summary>
    public const int Ambiguous = 3;

    /// <summary>The operation needs administrator rights.</summary>
    public const int ElevationRequired = 4;

    /// <summary>The application's own uninstaller failed.</summary>
    public const int UninstallFailed = 5;

    /// <summary>Something was removed but not everything worked.</summary>
    public const int PartialFailure = 6;
}

public sealed class CliUsageException : Exception
{
    public CliUsageException(string message)
        : base(message)
    {
    }
}
