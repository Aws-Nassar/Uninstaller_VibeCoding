namespace VibeUninstaller.Windows.Execution;

/// <summary>Computes the size of leftover files and folders, with hard caps.</summary>
public static class SizeCalculator
{
    public static long GetSize(string path, int maxEntries = 250_000)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }

            if (!Directory.Exists(path))
            {
                return 0;
            }

            long total = 0;
            var count = 0;
            var stack = new Stack<string>();
            stack.Push(path);

            while (stack.Count > 0 && count < maxEntries)
            {
                var directory = stack.Pop();
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(directory);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch (Exception)
                    {
                        // Ignore unreadable files.
                    }

                    if (++count >= maxEntries)
                    {
                        break;
                    }
                }

                try
                {
                    foreach (var subDirectory in Directory.EnumerateDirectories(directory))
                    {
                        var attributes = File.GetAttributes(subDirectory);
                        if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            stack.Push(subDirectory);
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignore unreadable directories.
                }
            }

            return total;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
