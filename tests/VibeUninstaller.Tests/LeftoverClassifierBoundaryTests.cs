using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

/// <summary>
/// Boundary and evidence-mix cases for the leftover scoring: inclusive tier
/// thresholds, publisher-only evidence, registry path shapes, pruning rules
/// and cancellation.
/// </summary>
public class LeftoverClassifierBoundaryTests
{
    private static readonly InstalledApp SevenZip = TestData.SevenZip();

    private static LeftoverClassifier WithOptions(LeftoverClassifierOptions options) =>
        new(TestData.Fingerprint(SevenZip), TestData.Rules(), TestData.Environment(), options);

    [Fact]
    public void Raising_the_safe_threshold_moves_the_same_finding_to_moderate()
    {
        const string path = @"C:\Program Files\7-Zip\blob.bin";

        var byDefault = TestData.Classifier(SevenZip).ClassifyOne(path, isRegistry: false, isDirectory: false);
        Assert.NotNull(byDefault);
        Assert.Equal(RiskTier.Safe, byDefault!.Tier);

        var strict = WithOptions(new LeftoverClassifierOptions { SafeThreshold = 100 });
        var strictItem = strict.ClassifyOne(path, isRegistry: false, isDirectory: false);

        Assert.NotNull(strictItem);
        Assert.Equal(RiskTier.Moderate, strictItem!.Tier);
    }

    [Fact]
    public void The_advanced_threshold_is_inclusive_for_a_single_point_of_evidence()
    {
        // Two strong words in a folder, only one of them the publisher: +1 point.
        const string path = @"C:\ProgramData\unrelated\igor-quux\blob.dat";

        var byDefault = TestData.Classifier(SevenZip).ClassifyOne(path, isRegistry: false, isDirectory: false);
        Assert.Null(byDefault); // one point is below the default 1.5

        var lenient = WithOptions(new LeftoverClassifierOptions { AdvancedThreshold = 1.0 });
        var item = lenient.ClassifyOne(path, isRegistry: false, isDirectory: false);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Advanced, item!.Tier);
    }

    [Fact]
    public void Publisher_only_evidence_never_reaches_moderate()
    {
        // A whole segment that is exactly the publisher scores 3 points, but
        // without name or container evidence the tier stays Advanced on purpose.
        const string path = @"C:\ProgramData\unrelated\igor\blob.dat";

        var item = TestData.Classifier(SevenZip).ClassifyOne(path, isRegistry: false, isDirectory: false);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Advanced, item!.Tier);
        Assert.Equal(0.5, item.Score);
        Assert.Contains("Publisher", item.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repeated_publisher_segments_without_a_container_stay_advanced()
    {
        // Two "igor" segments give 3 + 1 points (only the first is leading), but
        // with no container evidence the tier still stays Advanced on purpose.
        const string path = @"C:\ProgramData\unrelated\igor\igor\blob.dat";

        var item = TestData.Classifier(SevenZip).ClassifyOne(path, isRegistry: false, isDirectory: false);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Advanced, item!.Tier);
        Assert.Equal(4.0 / 6, item.Score, 10);
    }

    [Fact]
    public void A_publisher_named_folder_under_a_data_root_reaches_moderate()
    {
        // The vendor folder itself is a root candidate (container evidence), so
        // container + whole-segment publisher match is enough for Moderate.
        const string path = @"C:\ProgramData\igor\blob.dat";

        var item = TestData.Classifier(SevenZip).ClassifyOne(path, isRegistry: false, isDirectory: false);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Moderate, item!.Tier);
        Assert.Equal(1.0, item.Score);
    }

    [Fact]
    public void A_shallow_segment_budget_misses_deeper_name_matches()
    {
        const string path = @"C:\ProgramData\unrelated\7-Zip\blob.dat";

        var byDefault = TestData.Classifier(SevenZip).ClassifyOne(path, isRegistry: false, isDirectory: false);
        Assert.NotNull(byDefault);
        Assert.Equal(RiskTier.Safe, byDefault!.Tier);

        var shallow = WithOptions(new LeftoverClassifierOptions { MaxSegmentsToInspect = 1 });
        Assert.Null(shallow.ClassifyOne(path, isRegistry: false, isDirectory: false));
    }

    [Fact]
    public void Wow6432Node_is_skipped_before_the_name_match()
    {
        var item = TestData.Classifier(SevenZip)
            .ClassifyOne(@"HKLM\SOFTWARE\WOW6432Node\7-Zip", isRegistry: true, isDirectory: true);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Safe, item!.Tier);
        Assert.Equal(SystemItemKind.RegistryKey, item.Kind);
    }

    [Fact]
    public void Unc_paths_matching_starts_after_server_and_share()
    {
        var item = TestData.Classifier(SevenZip)
            .ClassifyOne(@"\\server\share\7-Zip\blob.dll", isRegistry: false, isDirectory: false);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Safe, item!.Tier);
    }

    [Fact]
    public void Ancestors_of_the_standard_roots_are_protected()
    {
        var classifier = TestData.Classifier(SevenZip);

        Assert.Null(classifier.ClassifyOne(@"C:\Users\tester", isRegistry: false, isDirectory: true));
        Assert.Null(classifier.ClassifyOne(@"C:\Users\tester\AppData", isRegistry: false, isDirectory: true));
        Assert.Null(classifier.ClassifyOne(@"C:\Users\tester\AppData\Local", isRegistry: false, isDirectory: true));
    }

    [Fact]
    public void A_substring_hit_in_the_prefilter_is_not_scoring_evidence()
    {
        // "unzipper" contains "zip", so the cheap prefilter passes, but the
        // scored token comparison must still reject it.
        var item = TestData.Classifier(SevenZip)
            .ClassifyOne(@"C:\ProgramData\unrelated\unzipper\blob.dat", isRegistry: false, isDirectory: false);

        Assert.Null(item);
    }

    [Fact]
    public void Product_codes_match_with_and_without_braces_and_in_packed_form()
    {
        var classifier = TestData.Classifier(TestData.MsiApp());

        var unbraced = classifier.ClassifyOne(
            @"HKLM\SOFTWARE\Contoso\367FEF4F-3275-477E-8FA3-C8A61CE34ED5", isRegistry: true, isDirectory: true);
        Assert.NotNull(unbraced);
        Assert.Equal(RiskTier.Safe, unbraced!.Tier);

        var packed = classifier.ClassifyOne(
            @"HKLM\SOFTWARE\Contoso\F4FEF7635723E774F83A8C6AC13EE45D", isRegistry: true, isDirectory: true);
        Assert.NotNull(packed);
        Assert.Equal(RiskTier.Safe, packed!.Tier);

        // Equality, not containment: a longer segment that merely embeds the
        // code must not score.
        var embedded = classifier.ClassifyOne(
            @"HKLM\SOFTWARE\367FEF4F-3275-477E-8FA3-C8A61CE34ED5-x", isRegistry: true, isDirectory: true);
        Assert.Null(embedded);
    }

    [Fact]
    public void The_apps_own_uninstaller_is_recognized_and_scores_full()
    {
        var item = TestData.Classifier(SevenZip)
            .ClassifyOne(@"C:\Program Files\7-Zip\Uninstall.exe", isRegistry: false, isDirectory: false);

        Assert.NotNull(item);
        Assert.Equal(RiskTier.Safe, item!.Tier);
        Assert.Equal(1.0, item.Score);
        Assert.Contains("uninstaller", item.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Files_inside_a_selected_directory_survive_pruning()
    {
        var diff = new SnapshotDiff
        {
            RegistryRemaining = Array.Empty<string>(),
            RegistryCreated = Array.Empty<string>(),
            DirectoriesRemaining = new[] { @"C:\ProgramData\7-Zip" },
            DirectoriesCreated = Array.Empty<string>(),
            FilesRemaining = new[] { @"C:\ProgramData\7-Zip\settings.ini" },
            FilesCreated = Array.Empty<string>(),
        };

        var result = TestData.Classifier(SevenZip).Classify(diff);

        Assert.Contains(result.Items, item => item.Path == @"C:\ProgramData\7-Zip");
        Assert.Contains(result.Items, item => item.Path == @"C:\ProgramData\7-Zip\settings.ini");
    }

    [Fact]
    public void Results_are_grouped_by_kind()
    {
        var diff = new SnapshotDiff
        {
            RegistryRemaining = new[] { @"HKLM\SOFTWARE\7-Zip" },
            RegistryCreated = Array.Empty<string>(),
            DirectoriesRemaining = new[] { @"C:\ProgramData\7-Zip" },
            DirectoriesCreated = Array.Empty<string>(),
            FilesRemaining = new[] { @"C:\Program Files\7-Zip\7z.dll" },
            FilesCreated = Array.Empty<string>(),
        };

        var result = TestData.Classifier(SevenZip).Classify(diff);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(
            new[] { SystemItemKind.RegistryKey, SystemItemKind.File, SystemItemKind.Directory },
            result.Items.Select(item => item.Kind).ToArray());
    }

    [Fact]
    public void A_cancelled_scan_throws_instead_of_returning_partial_results()
    {
        var diff = new SnapshotDiff
        {
            RegistryRemaining = new[] { @"HKLM\SOFTWARE\7-Zip" },
            RegistryCreated = Array.Empty<string>(),
            DirectoriesRemaining = Array.Empty<string>(),
            DirectoriesCreated = Array.Empty<string>(),
            FilesRemaining = Array.Empty<string>(),
            FilesCreated = Array.Empty<string>(),
        };

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => TestData.Classifier(SevenZip).Classify(diff, cancellation.Token));
    }
}
