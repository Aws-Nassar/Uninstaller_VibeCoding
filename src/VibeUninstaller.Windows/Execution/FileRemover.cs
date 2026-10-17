using System.Runtime.InteropServices;

namespace VibeUninstaller.Windows.Execution;

public sealed record FileRemovalResult(int Attempted, int Failed, IReadOnlyList<string> Errors);

/// <summary>
/// Deletes files through the Windows shell so that everything lands in the
/// Recycle Bin and can be restored by the user.
/// </summary>
public sealed class FileRemover
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMMKDIR = 0x0200;
    private const ushort FOF_NOERRORUI = 0x0400;

    public FileRemovalResult MoveToRecycleBin(IReadOnlyList<string> paths, IProgress<string>? progress = null)
    {
        var existing = new List<string>(paths.Count);
        foreach (var path in paths)
        {
            if (Directory.Exists(path) || File.Exists(path))
            {
                existing.Add(path);
            }
        }

        if (existing.Count == 0)
        {
            return new FileRemovalResult(0, 0, Array.Empty<string>());
        }

        progress?.Report($"Moving {existing.Count} item{(existing.Count == 1 ? string.Empty : "s")} to the Recycle Bin...");

        var shellError = DeleteOnce(existing);
        if (shellError == 0)
        {
            return new FileRemovalResult(existing.Count, 0, Array.Empty<string>());
        }

        // Isolate failures so one stubborn item cannot block the rest.
        var errors = new List<string>();
        var failed = 0;
        foreach (var path in existing)
        {
            var singleError = DeleteOnce(new[] { path });
            if (singleError != 0)
            {
                failed++;
                var reason = path.Length > 240 ? "path is too long for the Recycle Bin" : $"shell error {singleError}";
                errors.Add($"{path}: {reason}");
            }
        }

        return new FileRemovalResult(existing.Count, failed, errors);
    }

    private static int DeleteOnce(IReadOnlyList<string> paths)
    {
        var from = string.Join('\0', paths) + "\0\0";
        var operation = new ShFileOpStructW
        {
            wFunc = FO_DELETE,
            pFrom = from,
            pTo = string.Empty,
            fFlags = FOF_SILENT | FOF_NOCONFIRMATION | FOF_ALLOWUNDO | FOF_NOCONFIRMMKDIR | FOF_NOERRORUI,
        };

        return SHFileOperationW(ref operation);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStructW
    {
        public IntPtr hwnd;

        public uint wFunc;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string pTo;

        public ushort fFlags;

        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;

        public IntPtr hNameMappings;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperationW(ref ShFileOpStructW fileOperation);
}
