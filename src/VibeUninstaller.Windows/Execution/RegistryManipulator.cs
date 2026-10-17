using System.Diagnostics;
using Microsoft.Win32;
using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Windows.Execution;

/// <summary>
/// Exports registry keys to .reg backups before deleting them. A key is never
/// deleted when its backup could not be written.
/// </summary>
public sealed class RegistryManipulator
{
    public bool ExportKey(string canonicalPath, string targetFile, out string? error)
    {
        var directory = Path.GetDirectoryName(targetFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            var startInfo = new ProcessStartInfo("reg.exe")
            {
                Arguments = $"export \"{canonicalPath}\" \"{targetFile}\" /y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                error = "reg.exe could not be started";
                return false;
            }

            if (!process.WaitForExit(30_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // Ignore.
                }

                error = "reg.exe timed out";
                return false;
            }

            if (process.ExitCode != 0)
            {
                error = process.StandardError.ReadToEnd().Trim();
                if (error.Length == 0)
                {
                    error = $"reg.exe exited with {process.ExitCode}";
                }

                return false;
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public bool DeleteKey(string canonicalPath, out string? error)
    {
        var split = PathTools.SplitRegistryPath(canonicalPath);
        if (split is null || split.Value.SubPath.Length == 0)
        {
            error = "Unrecognized registry path";
            return false;
        }

        var hive = split.Value.Hive switch
        {
            "HKLM" => RegistryHive.LocalMachine,
            "HKCU" => RegistryHive.CurrentUser,
            "HKCR" => RegistryHive.ClassesRoot,
            "HKU" => RegistryHive.Users,
            _ => (RegistryHive?)null,
        };

        if (hive is null)
        {
            error = "Unsupported registry hive";
            return false;
        }

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive.Value, RegistryView.Registry64);
            baseKey.DeleteSubKeyTree(split.Value.SubPath, throwOnMissingSubKey: false);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }
}
