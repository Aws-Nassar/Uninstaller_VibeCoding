using VibeUninstaller.Snapshot;

namespace VibeUninstaller.Tests;

public class SnapshotDifferTests
{
    private static RegistrySnapshot Registry(params string[] keys) => new(new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase));

    private static FileSnapshot Files(string[] directories, string[] files) => new(
        new HashSet<string>(directories, StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(files, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void Keys_that_survive_the_uninstall_are_reported_as_remaining()
    {
        var before = Registry(@"HKLM\SOFTWARE\7-Zip", @"HKLM\SOFTWARE\Other");
        var after = Registry(@"HKLM\SOFTWARE\7-Zip");

        var diff = SnapshotDiffer.Diff(before, after, Files(Array.Empty<string>(), Array.Empty<string>()), Files(Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(new[] { @"HKLM\SOFTWARE\7-Zip" }, diff.RegistryRemaining);
        Assert.Empty(diff.RegistryCreated);
        Assert.Equal(1, diff.RegistryVanishedCount);
    }

    [Fact]
    public void Keys_created_by_the_uninstaller_are_reported_as_created()
    {
        var before = Registry(@"HKLM\SOFTWARE\7-Zip");
        var after = Registry(@"HKLM\SOFTWARE\7-Zip", @"HKLM\SOFTWARE\7-Zip\InstallDate");

        var diff = SnapshotDiffer.Diff(before, after, Files(Array.Empty<string>(), Array.Empty<string>()), Files(Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(new[] { @"HKLM\SOFTWARE\7-Zip\InstallDate" }, diff.RegistryCreated);
        Assert.Equal(0, diff.RegistryVanishedCount);
    }

    [Fact]
    public void Files_and_folders_are_split_the_same_way()
    {
        var before = Files(
            new[] { @"C:\ProgramData\7-Zip", @"C:\ProgramData\Gone" },
            new[] { @"C:\ProgramData\7-Zip\settings.ini" });

        var after = Files(
            new[] { @"C:\ProgramData\7-Zip", @"C:\ProgramData\New" },
            new[] { @"C:\ProgramData\7-Zip\settings.ini", @"C:\ProgramData\7-Zip\fresh.log" });

        var diff = SnapshotDiffer.Diff(
            Registry(),
            Registry(),
            before,
            after);

        Assert.Equal(new[] { @"C:\ProgramData\7-Zip" }, diff.DirectoriesRemaining);
        Assert.Equal(new[] { @"C:\ProgramData\New" }, diff.DirectoriesCreated);
        Assert.Equal(new[] { @"C:\ProgramData\7-Zip\settings.ini" }, diff.FilesRemaining);
        Assert.Equal(new[] { @"C:\ProgramData\7-Zip\fresh.log" }, diff.FilesCreated);
        Assert.Equal(1, diff.FilesVanishedCount);
    }

    [Fact]
    public void An_identical_snapshot_produces_no_vanished_entries()
    {
        var registry = Registry(@"HKLM\SOFTWARE\App");
        var files = Files(new[] { @"C:\ProgramData\App" }, new[] { @"C:\ProgramData\App\a.ini" });

        var diff = SnapshotDiffer.Diff(registry, registry, files, files);

        Assert.Equal(3, diff.CandidateCount);
        Assert.Equal(0, diff.RegistryVanishedCount);
        Assert.Equal(0, diff.FilesVanishedCount);
    }

    [Fact]
    public void Candidate_count_sums_every_set()
    {
        var diff = SnapshotDiffer.Diff(
            Registry(@"HKLM\SOFTWARE\App"),
            Registry(@"HKLM\SOFTWARE\App", @"HKLM\SOFTWARE\App\New"),
            Files(new[] { @"C:\ProgramData\App" }, Array.Empty<string>()),
            Files(new[] { @"C:\ProgramData\App", @"C:\ProgramData\App\Data" }, new[] { @"C:\ProgramData\App\x.dat" }));

        // 2 registry + 2 directories + 1 file
        Assert.Equal(5, diff.CandidateCount);
    }
}
