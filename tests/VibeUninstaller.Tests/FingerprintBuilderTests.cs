using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Tests;

public class FingerprintBuilderTests
{
    [Fact]
    public void Name_tokens_ignore_versions_and_editions()
    {
        var fingerprint = TestData.Fingerprint(TestData.SevenZip());

        Assert.Equal(new[] { "zip" }, fingerprint.NameTokens);
    }

    [Fact]
    public void Vendor_tokens_ignore_legal_suffixes()
    {
        var fingerprint = TestData.Fingerprint(TestData.MsiApp());

        Assert.Equal(new[] { "contoso" }, fingerprint.VendorTokens);
    }

    [Fact]
    public void Install_roots_come_from_the_install_location_and_the_uninstaller()
    {
        var fingerprint = TestData.Fingerprint(TestData.SevenZip());

        Assert.Contains(@"C:\Program Files\7-Zip", fingerprint.InstallRoots);
        Assert.Equal(@"C:\Program Files\7-Zip\Uninstall.exe", fingerprint.UninstallerPath);
        Assert.Contains("7-zip", fingerprint.KnownFolderNames);
    }

    [Fact]
    public void Root_candidates_cover_the_us_because_data_folders()
    {
        var fingerprint = TestData.Fingerprint(TestData.SevenZip());

        Assert.Contains(@"C:\ProgramData\7-zip", fingerprint.RootCandidates);
        Assert.Contains(@"C:\Users\tester\AppData\Local\7-zip", fingerprint.RootCandidates);
        Assert.Contains(@"C:\Users\tester\AppData\Roaming\7-zip", fingerprint.RootCandidates);
        Assert.Contains(@"C:\Program Files (x86)\7-zip", fingerprint.RootCandidates);
    }

    [Fact]
    public void Product_codes_are_normalized_and_packed()
    {
        var withBraces = TestData.Fingerprint(TestData.MsiApp());
        var withoutBraces = TestData.Fingerprint(TestData.MsiApp("367fef4f-3275-477e-8fa3-c8a61ce34ed5"));

        Assert.Equal("{367FEF4F-3275-477E-8FA3-C8A61CE34ED5}", withBraces.ProductCode);
        Assert.Equal("367FEF4F-3275-477E-8FA3-C8A61CE34ED5", withoutBraces.ProductCode);
        Assert.Equal("F4FEF7635723E774F83A8C6AC13EE45D", withoutBraces.PackedProductCode);
    }

    [Fact]
    public void Known_file_names_come_from_the_install_root_listing()
    {
        var fingerprint = TestData.Fingerprint(TestData.SevenZip(), new[] { "7zFM.exe", "7zG.exe" });

        Assert.Contains("7zFM.exe", fingerprint.KnownFileNames);
        Assert.Contains("Uninstall.exe", fingerprint.KnownFileNames);
    }

    [Theory]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files (x86)")]
    [InlineData(@"C:\ProgramData")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Users\tester\AppData\Local\Temp")]
    [InlineData("C:\\")]
    [InlineData("")]
    public void Shared_roots_are_never_used_as_an_install_root(string candidate)
    {
        Assert.False(FingerprintBuilder.IsPlausibleRoot(candidate, TestData.Environment()));
    }

    [Theory]
    [InlineData(@"C:\Program Files\7-Zip")]
    [InlineData(@"C:\Program Files (x86)\Vendor\App")]
    [InlineData(@"D:\Games\SomeGame")]
    public void Specific_folders_are_valid_install_roots(string candidate)
    {
        Assert.True(FingerprintBuilder.IsPlausibleRoot(candidate, TestData.Environment()));
    }

    [Fact]
    public void An_app_without_install_location_still_produces_name_evidence()
    {
        var app = new Core.Models.InstalledApp
        {
            Id = "reg|HKCU|Some App",
            DisplayName = "Some App",
            UninstallString = @"SomeApp.exe /remove",
            Scope = Core.Models.InstallScope.User,
        };

        var fingerprint = TestData.Fingerprint(app);

        Assert.Contains("some", fingerprint.NameTokens);
        Assert.Empty(fingerprint.InstallRoots);
    }
}
