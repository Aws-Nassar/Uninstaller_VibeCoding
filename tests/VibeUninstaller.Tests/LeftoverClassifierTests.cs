using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

public class LeftoverClassifierTests
{
    private static readonly InstalledApp SevenZip = TestData.SevenZip();

    [Fact]
    public void Files_inside_the_install_folder_are_safe()
    {
        var item = TestData.Classifier(SevenZip).ClassifyOne(@"C:\Program Files\7-Zip\7zFM.exe", isRegistry: false, isDirectory: false);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Safe, item!.Tier);
        Assert.Contains("install folder", item.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Folders_named_after_the_app_under_data_roots_are_safe()
    {
        var item = TestData.Classifier(SevenZip).ClassifyOne(@"C:\ProgramData\7-Zip", isRegistry: false, isDirectory: true);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Safe, item!.Tier);
    }

    [Fact]
    public void Files_inside_a_folder_named_after_the_app_are_found_too()
    {
        var item = TestData.Classifier(SevenZip)
            .ClassifyOne(@"C:\Users\tester\AppData\Roaming\7-Zip\settings.ini", isRegistry: false, isDirectory: false);

        Assert.NotNull(item);
        Assert.True(item!.Tier <= RiskTier.Moderate);
    }

    [Fact]
    public void Registry_keys_named_after_the_app_are_safe()
    {
        var item = TestData.Classifier(SevenZip).ClassifyOne(@"HKLM\SOFTWARE\7-Zip", isRegistry: true, isDirectory: true);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Safe, item!.Tier);
        Assert.Equal(SystemItemKind.RegistryKey, item.Kind);
    }

    [Fact]
    public void Product_code_keys_are_safe_even_without_a_name_match()
    {
        var app = TestData.MsiApp();
        var item = TestData.Classifier(app)
            .ClassifyOne(@"HKLM\SOFTWARE\Classes\Installer\Products\F4FEF7635723E774F83A8C6AC13EE45D", isRegistry: true, isDirectory: true);

        // The whole Installer branch is protected, so nothing from it may be proposed.
        Assert.Null(item);
    }

    [Fact]
    public void Product_code_registry_entries_are_matched()
    {
        var app = TestData.MsiApp();
        var classifier = TestData.Classifier(app);
        var item = classifier.ClassifyOne(
            @"HKLM\SOFTWARE\Contoso\{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}",
            isRegistry: true,
            isDirectory: true);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Safe, item!.Tier);
        Assert.Contains("Product code", item.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Nothing_inside_a_denied_system_folder_is_ever_proposed()
    {
        var classifier = TestData.Classifier(SevenZip);

        Assert.Null(classifier.ClassifyOne(@"C:\Windows\System32\7zip.dll", isRegistry: false, isDirectory: false));
        Assert.Null(classifier.ClassifyOne(@"C:\Windows\7-Zip", isRegistry: false, isDirectory: true));
        Assert.Null(classifier.ClassifyOne(@"C:\Users\tester\AppData\Local\Temp\7-Zip\7z.tmp", isRegistry: false, isDirectory: false));
        Assert.Null(classifier.ClassifyOne(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", isRegistry: true, isDirectory: true));
    }

    [Fact]
    public void The_shared_program_files_root_is_never_proposed()
    {
        var classifier = TestData.Classifier(SevenZip);

        Assert.Null(classifier.ClassifyOne(@"C:\Program Files", isRegistry: false, isDirectory: true));
        Assert.Null(classifier.ClassifyOne(@"C:\ProgramData\Microsoft", isRegistry: false, isDirectory: true));
    }

    [Fact]
    public void Unrelated_software_is_not_proposed()
    {
        var classifier = TestData.Classifier(SevenZip);

        Assert.Null(classifier.ClassifyOne(@"C:\Program Files\Notepad++\notepad++.exe", isRegistry: false, isDirectory: false));
        Assert.Null(classifier.ClassifyOne(@"HKLM\SOFTWARE\OtherVendor", isRegistry: true, isDirectory: true));
    }

    [Fact]
    public void Weak_evidence_only_reaches_the_advanced_tier()
    {
        var item = TestData.Classifier(SevenZip)
            .ClassifyOne(@"C:\Users\tester\AppData\Local\unrelated\zip\notes.dat", isRegistry: false, isDirectory: false);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Advanced, item!.Tier);
    }

    [Fact]
    public void Created_during_the_uninstall_raises_the_confidence()
    {
        var classifier = TestData.Classifier(SevenZip);
        var baseline = classifier.ClassifyOne(@"C:\Users\tester\AppData\Local\unrelated\zip\notes.dat", isRegistry: false, isDirectory: false, createdDuringUninstall: false);
        var created = classifier.ClassifyOne(@"C:\Users\tester\AppData\Local\unrelated\zip\notes.dat", isRegistry: false, isDirectory: false, createdDuringUninstall: true);

        Assert.NotNull(baseline);
        Assert.NotNull(created);
        Assert.True(created!.Score > baseline!.Score);
        Assert.Contains("Created while", created.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Advanced_items_can_be_switched_off()
    {
        var classifier = new LeftoverClassifier(
            TestData.Fingerprint(SevenZip),
            TestData.Rules(),
            TestData.Environment(),
            new LeftoverClassifierOptions { IncludeAdvanced = false });

        Assert.Null(classifier.ClassifyOne(@"C:\Users\tester\AppData\Local\unrelated\zip\notes.dat", isRegistry: false, isDirectory: false));
    }

    [Fact]
    public void A_whole_scan_prunes_children_of_selected_containers()
    {
        var diff = new SnapshotDiff
        {
            RegistryRemaining = new[] { @"HKLM\SOFTWARE\7-Zip", @"HKLM\SOFTWARE\7-Zip\Extraction" },
            RegistryCreated = Array.Empty<string>(),
            DirectoriesRemaining = new[] { @"C:\ProgramData\7-Zip", @"C:\ProgramData\7-Zip\Lang" },
            DirectoriesCreated = Array.Empty<string>(),
            FilesRemaining = new[] { @"C:\ProgramData\7-Zip\settings.ini" },
            FilesCreated = Array.Empty<string>(),
            RegistryVanishedCount = 12,
            FilesVanishedCount = 340,
        };

        var result = TestData.Classifier(SevenZip).Classify(diff);

        Assert.Contains(result.Items, item => item.Path == @"C:\ProgramData\7-Zip");
        Assert.DoesNotContain(result.Items, item => item.Path == @"C:\ProgramData\7-Zip\Lang");
        Assert.Contains(result.Items, item => item.Path == @"HKLM\SOFTWARE\7-Zip");
        Assert.Equal(12, result.RegistryVanishedCount);
        Assert.Equal(340, result.FilesVanishedCount);
        Assert.True(result.SafeCount >= 2);
    }

    [Fact]
    public void Every_item_carries_a_reason_and_a_score()
    {
        var diff = new SnapshotDiff
        {
            RegistryRemaining = Array.Empty<string>(),
            RegistryCreated = Array.Empty<string>(),
            DirectoriesRemaining = new[] { @"C:\Program Files\7-Zip", @"C:\ProgramData\7-Zip" },
            DirectoriesCreated = Array.Empty<string>(),
            FilesRemaining = new[] { @"C:\Program Files\7-Zip\7z.exe" },
            FilesCreated = Array.Empty<string>(),
        };

        var result = TestData.Classifier(SevenZip).Classify(diff);

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Reason));
            Assert.InRange(item.Score, 0.0, 1.0);
        });
    }
}
