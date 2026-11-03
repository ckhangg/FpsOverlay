using System.Security.Cryptography.X509Certificates;
using FpsOverlay.Core.Localization;

namespace FpsOverlay.Core.PawnIo;

/// <summary>Kết quả cài PawnIO (driver đọc nhiệt độ CPU).</summary>
public enum PawnIoResult { Installed, InstalledRebootRequired, DownloadFailed, SignatureInvalid, InstallFailed }

public static class PawnIoResults
{
    /// <summary>Mã thoát của bộ cài: 0 = xong, 3010 = xong nhưng cần khởi động lại máy, còn lại = lỗi.</summary>
    public static PawnIoResult FromExitCode(int exitCode) => exitCode switch
    {
        0 => PawnIoResult.Installed,
        3010 => PawnIoResult.InstalledRebootRequired,
        _ => PawnIoResult.InstallFailed,
    };

    public static bool IsSuccess(PawnIoResult result) =>
        result is PawnIoResult.Installed or PawnIoResult.InstalledRebootRequired;

    /// <summary>Câu báo kết quả cho người dùng.</summary>
    public static StringKey MessageKey(PawnIoResult result) => result switch
    {
        PawnIoResult.Installed => StringKey.PawnIoInstalled,
        PawnIoResult.InstalledRebootRequired => StringKey.PawnIoRebootRequired,
        PawnIoResult.DownloadFailed => StringKey.PawnIoDownloadFailed,
        PawnIoResult.SignatureInvalid => StringKey.PawnIoSignatureInvalid,
        _ => StringKey.PawnIoInstallFailed,
    };
}

/// <summary>
/// Người ký Authenticode được chấp nhận cho bộ cài PawnIO. Ghi cứng theo bộ cài thật (PawnIO 2.2.0.0, đọc bằng
/// Get-AuthenticodeSignature, chứng chỉ do "GLOBALTRUST 2015 CODESIGNING 1" cấp). Chỉ ghim CN và O: E, L, C có thể đổi
/// khi gia hạn chứng chỉ mà vẫn là cùng người ký.
/// </summary>
public static class PawnIoSigner
{
    /// <summary>Subject đầy đủ đã thấy trên bộ cài thật (để tham khảo; việc so khớp chỉ dùng CN và O bên dưới).</summary>
    public const string ExpectedSigner = "E=admin@namazso.eu, CN=namazso.eu, O=namazso, L=Debrecen, C=HU";
    public const string ExpectedCommonName = "namazso.eu";
    public const string ExpectedOrganization = "namazso";

    const string CommonNameOid = "2.5.4.3";
    const string OrganizationOid = "2.5.4.10";

    /// <summary>
    /// Subject có đúng một CN và đúng một O, bằng chính xác (phân biệt hoa thường) giá trị ghim. Đọc từng RDN theo OID
    /// từ dữ liệu ASN.1, không so chuỗi con trên cả subject (một giá trị khác chứa "CN=namazso.eu" không lừa được).
    /// RDN nhiều giá trị (A=1 + B=2) thì không nhận.
    /// </summary>
    public static bool Matches(X500DistinguishedName subject)
    {
        string? cn = null, o = null;
        int cnCount = 0, oCount = 0;
        try
        {
            foreach (var rdn in subject.EnumerateRelativeDistinguishedNames())
            {
                if (rdn.HasMultipleElements) return false;
                switch (rdn.GetSingleElementType().Value)
                {
                    case CommonNameOid:
                        cnCount++;
                        cn = rdn.GetSingleElementValue();
                        break;
                    case OrganizationOid:
                        oCount++;
                        o = rdn.GetSingleElementValue();
                        break;
                }
            }
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;   // subject hỏng: không nhận
        }
        return cnCount == 1 && oCount == 1 &&
               string.Equals(cn, ExpectedCommonName, StringComparison.Ordinal) &&
               string.Equals(o, ExpectedOrganization, StringComparison.Ordinal);
    }
}
