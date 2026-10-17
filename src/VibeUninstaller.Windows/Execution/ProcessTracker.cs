using System.Diagnostics;
using System.Management;

namespace VibeUninstaller.Windows.Execution;

/// <summary>
/// Follows a process and everything it spawns. Uninstallers are known to copy
/// themselves to a temporary directory and exit, leaving the real work to a
/// child process, so tracking the whole tree is required for reliable waiting.
/// </summary>
internal sealed class ProcessTracker
{
    private readonly HashSet<int> _tracked = new();

    public ProcessTracker(int rootProcessId)
    {
        _tracked.Add(rootProcessId);
    }

    public int ProcessCount => _tracked.Count;

    public sealed record TrackedProcess(int ProcessId, string Name);

    public IReadOnlyList<TrackedProcess> Poll()
    {
        var snapshot = QueryProcesses();
        if (snapshot is null)
        {
            // WMI unavailable: fall back to the raw process list.
            var alive = _tracked.Where(IsAlive).ToArray();
            _tracked.Clear();
            foreach (var pid in alive)
            {
                _tracked.Add(pid);
            }

            return alive.Select(pid => new TrackedProcess(pid, SafeName(pid))).ToArray();
        }

        var (children, names) = snapshot.Value;
        var expanded = new HashSet<int>(_tracked);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (pid, parent) in children)
            {
                if (expanded.Contains(parent) && expanded.Add(pid))
                {
                    changed = true;
                }
            }
        }

        var result = new List<TrackedProcess>();
        _tracked.Clear();
        foreach (var pid in expanded)
        {
            if (names.TryGetValue(pid, out var name))
            {
                _tracked.Add(pid);
                result.Add(new TrackedProcess(pid, name));
            }
        }

        return result;
    }

    private static (Dictionary<int, int> Children, Dictionary<int, string> Names)? QueryProcesses()
    {
        try
        {
            var map = new Dictionary<int, int>();
            var names = new Dictionary<int, string>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, ParentProcessId, Name FROM Win32_Process");
            using var results = searcher.Get();
            foreach (var item in results)
            {
                using var process = (ManagementObject)item;
                if (process["ProcessId"] is not uint pid || process["ParentProcessId"] is not uint parent)
                {
                    continue;
                }

                map[(int)pid] = (int)parent;
                names[(int)pid] = process["Name"] as string ?? string.Empty;
            }

            return (map, names);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string SafeName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
