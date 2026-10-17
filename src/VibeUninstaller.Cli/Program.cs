using System.Text;
using VibeUninstaller.Cli.Commands;

namespace VibeUninstaller.Cli;

public static class Program
{
    private const string Version = "0.1.0";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch (Exception)
        {
            // Console redirection may not support encoding changes.
        }

        var noColor = args.Any(arg => arg.Equals("--no-color", StringComparison.OrdinalIgnoreCase));
        var json = args.Any(arg => arg.Equals("--json", StringComparison.OrdinalIgnoreCase));
        var ui = ConsoleUi.Create(noColor, json);

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
            Console.Error.WriteLine();
            Console.Error.WriteLine("Canceling...");
        };

        try
        {
            var command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "help";

            return command switch
            {
                "list" or "ls" => await ListCommand.RunAsync(args, ui, cancellation.Token).ConfigureAwait(false),
                "info" or "show" => await InfoCommand.RunAsync(args, ui, cancellation.Token).ConfigureAwait(false),
                "scan" => await ScanCommand.RunAsync(args, ui, cancellation.Token).ConfigureAwait(false),
                "uninstall" or "remove" => await UninstallCommand.RunAsync(args, ui, cancellation.Token).ConfigureAwait(false),
                "version" or "--version" or "-v" => PrintVersion(),
                "help" or "--help" or "-h" or "-?" or "" => PrintHelp(ui),
                _ => Unknown(command, ui),
            };
        }
        catch (CliUsageException exception)
        {
            ui.Error(exception.Message);
            return ExitCode.Error;
        }
        catch (OperationCanceledException)
        {
            ui.Error("Canceled.");
            return ExitCode.Error;
        }
        catch (Exception exception)
        {
            ui.Error($"Unexpected error: {exception.Message}");
            if (args.Any(arg => arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine(exception);
            }

            return ExitCode.Error;
        }
    }

    private static int PrintVersion()
    {
        Console.Out.WriteLine($"vibe {Version}");
        Console.Out.WriteLine("VibeUninstaller - free and open source Windows uninstaller");
        return ExitCode.Ok;
    }

    private static int Unknown(string command, ConsoleUi ui)
    {
        ui.Error($"Unknown command '{command}'.");
        PrintHelp(ui);
        return ExitCode.Error;
    }

    private static int PrintHelp(ConsoleUi ui)
    {
        ui.Line("vibe - VibeUninstaller command line");
        ui.Line();
        ui.Line("USAGE");
        ui.Line("  vibe <command> [arguments] [options]");
        ui.Line();
        ui.Line("COMMANDS");
        ui.Line("  list [options]              List installed applications");
        ui.Line("  info <name>                 Show details and the leftover fingerprint of one app");
        ui.Line("  scan <name>                 Preview leftovers without changing anything");
        ui.Line("  uninstall <name> [options]  Run the app's own uninstaller, then scan for leftovers");
        ui.Line("  version                     Show version information");
        ui.Line("  help                        Show this help");
        ui.Line();
        ui.Line("COMMON OPTIONS");
        ui.Line("  --json                      Machine readable output on stdout");
        ui.Line("  --no-color                  Disable colored output");
        ui.Line("  --include-system            Include system components and updates");
        ui.Line("  --verbose                   Print exception details on failure");
        ui.Line("  -h, --help                  Show this help");
        ui.Line();
        ui.Line("list OPTIONS");
        ui.Line("  --search <text>             Filter by name or publisher");
        ui.Line("  --source <registry|store|steam|all>");
        ui.Line("  --scope <machine|user|all>");
        ui.Line("  --sort <name|size|date|publisher>");
        ui.Line("  --limit <n>                 Show at most n applications");
        ui.Line("  --min-size <megabytes>      Only apps whose reported size is at least this");
        ui.Line("  --no-store, --no-steam      Skip those sources");
        ui.Line();
        ui.Line("scan OPTIONS");
        ui.Line("  --min-tier <safe|moderate|advanced>   Lowest confidence to show (default: advanced)");
        ui.Line();
        ui.Line("uninstall OPTIONS");
        ui.Line("  --silent                    Use unattended switches for known installers");
        ui.Line("  --no-scan                   Skip the leftover scan");
        ui.Line("  --clean                     Remove the leftovers that were found");
        ui.Line("  --min-tier <safe|moderate|advanced>   How aggressive --clean is (default: safe)");
        ui.Line("  --yes                       Do not ask for confirmation (required in scripts)");
        ui.Line("  --dry-run                   Show the command and the leftovers, change nothing");
        ui.Line("  --timeout <minutes>         Give up on the uninstaller after n minutes (default: 30)");
        ui.Line();
        ui.Line("EXIT CODES");
        ui.Line("  0 success   1 error   2 not found   3 ambiguous   4 needs elevation");
        ui.Line("  5 uninstall failed   6 removed with errors");
        ui.Line();
        ui.Line("EXAMPLES");
        ui.Line("  vibe list --json > apps.json");
        ui.Line("  vibe scan 7-Zip");
        ui.Line("  vibe uninstall \"Notepad++\" --silent --clean --yes");
        ui.Line();

        return ExitCode.Ok;
    }
}
