using System.Security.Cryptography.X509Certificates;
using FpsOverlay.Core.Localization;
using FpsOverlay.Core.PawnIo;
using FpsOverlay.Core.Settings;

namespace FpsOverlay.Core.Tests;

public class PawnIoTests
{
    // Subject thật của bộ cài PawnIO 2.2.0.0 (đọc bằng Get-AuthenticodeSignature).
    const string RealSubject = "E=admin@namazso.eu, CN=namazso.eu, O=namazso, L=Debrecen, C=HU";

    [Fact]
    public void Real_signer_subject_matches()
    {
        Assert.True(PawnIoSigner.Matches(new X500DistinguishedName(RealSubject)));
    }

    [Fact]
    public void Minimal_subject_with_cn_and_o_matches()
    {
        Assert.True(PawnIoSigner.Matches(new X500DistinguishedName("CN=namazso.eu, O=namazso")));
    }

    [Theory]
    [InlineData("CN=namazso.eu.evil.com, O=namazso")]            // CN chỉ chứa chuỗi
    [InlineData("CN=evil.namazso.eu, O=namazso")]
    [InlineData("CN=namazso.eu, O=namazso evil")]                  // O chỉ chứa chuỗi
    [InlineData("CN=namazso.eu, O=Namazso")]                       // khác hoa thường: không nhận
    [InlineData("CN=NAMAZSO.EU, O=namazso")]
    [InlineData("CN=namazso.eu")]                                  // thiếu O
    [InlineData("O=namazso")]                                      // thiếu CN
    [InlineData("CN=evil, O=evil, OU=\"CN=namazso.eu, O=namazso\"")] // chuỗi khớp nằm trong giá trị khác
    [InlineData("CN=namazso.eu, CN=evil, O=namazso")]              // hai CN
    [InlineData("CN=namazso.eu, O=namazso, O=evil")]               // hai O
    [InlineData("CN=namazso.eu + OU=x, O=namazso")]                // RDN nhiều giá trị: không nhận
    [InlineData("E=admin@namazso.eu, CN=evil, O=evil")]
    public void Other_subjects_do_not_match(string subject)
    {
        Assert.False(PawnIoSigner.Matches(new X500DistinguishedName(subject)));
    }

    [Fact]
    public void Empty_subject_does_not_match()
    {
        Assert.False(PawnIoSigner.Matches(new X500DistinguishedName("")));
    }

    [Theory]
    [InlineData(0, PawnIoResult.Installed)]
    [InlineData(3010, PawnIoResult.InstalledRebootRequired)]
    [InlineData(1, PawnIoResult.InstallFailed)]
    [InlineData(-1, PawnIoResult.InstallFailed)]
    [InlineData(1602, PawnIoResult.InstallFailed)]
    [InlineData(1641, PawnIoResult.InstallFailed)]
    public void Exit_code_maps_to_result(int exitCode, PawnIoResult expected)
    {
        Assert.Equal(expected, PawnIoResults.FromExitCode(exitCode));
    }

    [Fact]
    public void Only_installed_results_count_as_success()
    {
        Assert.True(PawnIoResults.IsSuccess(PawnIoResult.Installed));
        Assert.True(PawnIoResults.IsSuccess(PawnIoResult.InstalledRebootRequired));
        Assert.False(PawnIoResults.IsSuccess(PawnIoResult.DownloadFailed));
        Assert.False(PawnIoResults.IsSuccess(PawnIoResult.SignatureInvalid));
        Assert.False(PawnIoResults.IsSuccess(PawnIoResult.InstallFailed));
    }

    [Fact]
    public void Every_result_has_a_message_in_both_languages()
    {
        foreach (var result in Enum.GetValues<PawnIoResult>())
        {
            var key = PawnIoResults.MessageKey(result);
            Assert.False(string.IsNullOrWhiteSpace(Loc.Get(key, AppLanguage.Vietnamese)));
            Assert.False(string.IsNullOrWhiteSpace(Loc.Get(key, AppLanguage.English)));
        }
        Assert.Equal(5, Enum.GetValues<PawnIoResult>().Select(PawnIoResults.MessageKey).Distinct().Count());
    }

    [Fact]
    public void Reboot_message_says_to_restart()
    {
        var key = PawnIoResults.MessageKey(PawnIoResult.InstalledRebootRequired);
        Assert.Contains("Khởi động lại", Loc.Get(key, AppLanguage.Vietnamese));
        Assert.Contains("Restart", Loc.Get(key, AppLanguage.English));
    }
}
