using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using FpsOverlay.Core.PawnIo;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace FpsOverlay;

/// <summary>
/// Tải, kiểm chữ ký và cài ngầm PawnIO (driver để LibreHardwareMonitor đọc nhiệt độ CPU). Chỉ chạy khi người dùng
/// chọn (ô tick trong hộp thoại cài, hoặc nút trong cài đặt); không bao giờ chạy lúc app tự khởi động cùng Windows.
/// Chuỗi an toàn: tải qua HTTPS vào thư mục chỉ Admin ghi được → mở file chỉ cho chia sẻ đọc (không ai ghi/xoá/tráo được
/// tới lúc chạy xong) → WinVerifyTrust trên chính handle đó + người ký khớp <see cref="PawnIoSigner"/> → chạy → xoá.
/// </summary>
static class PawnIoSetup
{
    public const string DownloadUrl = "https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe";

    /// <summary>
    /// Bản setup truyền cho bản đã cài nó khởi chạy khi đã hỏi người dùng về PawnIO (tick hay không): bản đã cài (chủ của
    /// file cài đặt) đặt <c>PawnIoOffered = true</c>. Bản setup không bao giờ tự ghi file cài đặt.
    /// </summary>
    public const string OfferedArgument = "--pawnio-offered";

    const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO";
    const string ServiceKeyPath = @"SYSTEM\CurrentControlSet\Services\PawnIO";
    const string FilePrefix = "PawnIO_setup.";
    const long MaxDownloadBytes = 20L * 1024 * 1024;
    static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(60);
    static readonly TimeSpan VerifyTimeout = TimeSpan.FromSeconds(45);
    static readonly TimeSpan InstallTimeout = TimeSpan.FromSeconds(120);

    static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>
    /// PawnIO đã cài: có khoá gỡ cài đặt <c>HKLM\...\Uninstall\PawnIO</c> (bộ cài PawnIO ghi, LibreHardwareMonitor cũng đọc
    /// khoá này để biết phiên bản) hoặc có dịch vụ driver <c>PawnIO</c>. Đọc registry 64-bit; lỗi đọc thì coi là chưa cài.
    /// </summary>
    public static bool IsInstalled()
    {
        try
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var uninstall = hklm.OpenSubKey(UninstallKeyPath);
            if (uninstall is not null) return true;
            using var service = hklm.OpenSubKey(ServiceKeyPath);
            return service is not null;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Không đọc được trạng thái PawnIO: {ex.Message}");
            return false;
        }
    }

    /// <summary>Tải, kiểm chữ ký, chạy <c>-install -silent</c>, xoá file tải. Không ném lỗi (trừ khi huỷ qua token).</summary>
    public static async Task<PawnIoResult> InstallAsync(CancellationToken cancellationToken)
    {
        string directory;
        try
        {
            directory = await Task.Run(PrepareTempDirectory, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceWarning($"Không chuẩn bị được thư mục tạm cho PawnIO: {ex.Message}");
            return PawnIoResult.DownloadFailed;
        }

        // Tên riêng mỗi lần: không đụng file cũ còn sót (nếu có) hay một lần chạy khác.
        var path = Path.Combine(directory, $"{FilePrefix}{Guid.NewGuid():N}.exe");
        try
        {
            if (!await DownloadAsync(path, cancellationToken))
                return PawnIoResult.DownloadFailed;

            // Mở lại chỉ cho chia sẻ đọc và giữ tới khi bộ cài chạy xong: không ai ghi, xoá hay đổi tên được file giữa
            // lúc kiểm chữ ký và lúc chạy. (Từ lúc đóng handle ghi tới lúc mở handle này, chỉ Admin ghi được thư mục.)
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            var verify = Task.Run(() => VerifySignature(handle, path), cancellationToken);
            bool trusted;
            try
            {
                trusted = await verify.WaitAsync(VerifyTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                Trace.TraceWarning("Kiểm chữ ký PawnIO quá lâu (mạng kiểm thu hồi chứng chỉ?)");
                trusted = false;
            }
            if (!trusted)
                return PawnIoResult.SignatureInvalid;

            return await RunInstallerAsync(path, directory, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Trace.TraceWarning($"Cài PawnIO lỗi: {ex.Message}");
            return File.Exists(path) ? PawnIoResult.InstallFailed : PawnIoResult.DownloadFailed;
        }
        finally
        {
            await TryDeleteAsync(path);
        }
    }

    // ---------- Thư mục tạm chỉ Admin ghi được ----------

    /// <summary>
    /// Có bản cài (bản setup vừa cài xong, hoặc bản đã cài đang chạy): <c>&lt;thư mục cài&gt;\Temp</c>, thừa hưởng quyền của
    /// Program Files. Không có (<c>--no-install</c>): <c>%ProgramData%\FpsOverlay\Temp</c>, đặt ACL riêng chỉ Administrators
    /// và SYSTEM. Thư mục là liên kết (junction/symlink) thì từ chối.
    /// </summary>
    static string PrepareTempDirectory()
    {
        string directory;
        if (Installer.IsInstallPathValid && File.Exists(Installer.InstalledExePath))
        {
            RejectReparsePoint(Installer.InstallDirectory);
            directory = Path.Combine(Installer.InstallDirectory, "Temp");
            Directory.CreateDirectory(directory);
            RejectReparsePoint(directory);
        }
        else
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrEmpty(programData) || !Path.IsPathFullyQualified(programData))
                throw new InvalidOperationException($"ProgramData folder is not an absolute path: \"{programData}\"");
            var root = Path.Combine(programData, Core.Install.InstallPaths.DirectoryName);
            directory = Path.Combine(root, "Temp");
            SecureDirectory(root);
            SecureDirectory(directory);
        }
        DeleteLeftovers(directory);
        return directory;
    }

    // Tạo (hoặc đặt lại) thư mục với ACL: chủ sở hữu Administrators, tắt thừa hưởng, chỉ Administrators + SYSTEM toàn quyền.
    // Đặt xong đọc lại để kiểm; không đúng thì ném lỗi.
    static void SecureDirectory(string path)
    {
        var security = new DirectorySecurity();
        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inherit,
            PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit,
            PropagationFlags.None, AccessControlType.Allow));

        var info = new DirectoryInfo(path);
        if (!info.Exists)
            info.Create(security);   // tạo luôn với ACL; có ai tạo trước giữa chừng thì ném lỗi, lần sau đi nhánh dưới
        else
        {
            RejectReparsePoint(path);
            info.SetAccessControl(security);
        }

        info.Refresh();
        RejectReparsePoint(path);
        var actual = info.GetAccessControl();
        var owner = actual.GetOwner(typeof(SecurityIdentifier));
        if (owner is null || (!owner.Equals(Administrators) && !owner.Equals(LocalSystem)))
            throw new InvalidOperationException($"Unexpected owner on {path}: {owner}");
        if (!actual.AreAccessRulesProtected)
            throw new InvalidOperationException($"Inherited permissions on {path}");
        foreach (FileSystemAccessRule rule in actual.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (!rule.IdentityReference.Equals(Administrators) && !rule.IdentityReference.Equals(LocalSystem))
                throw new InvalidOperationException($"Unexpected permission on {path}: {rule.IdentityReference}");
    }

    static void RejectReparsePoint(string path)
    {
        if (new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException($"Refusing to use a link: {path}");
    }

    // File của lần trước bị dừng giữa chừng (app bị tắt lúc đang tải/cài): xoá được thì xoá, không thì thôi.
    static void DeleteLeftovers(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, FilePrefix + "*.exe"))
        {
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    // ---------- Tải ----------

    // Chỉ HTTPS (HttpClient không theo chuyển hướng HTTPS → HTTP), giới hạn thời gian cả lần tải và kích thước.
    static async Task<bool> DownloadAsync(string path, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);
        try
        {
            using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 })
            {
                Timeout = DownloadTimeout,
            };
            using var response = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                Trace.TraceWarning($"Tải PawnIO lỗi HTTP {(int)response.StatusCode}");
                return false;
            }
            if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                return false;
            if (response.Content.Headers.ContentLength is > MaxDownloadBytes)
                return false;

            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
            {
                total += read;
                if (total > MaxDownloadBytes) return false;
                await target.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
            return total > 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException ||
                                   (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            Trace.TraceWarning($"Tải PawnIO lỗi: {ex.Message}");
            return false;
        }
    }

    // ---------- Chữ ký ----------

    /// <summary>
    /// Chữ ký Authenticode hợp lệ (WinVerifyTrust, WINTRUST_ACTION_GENERIC_VERIFY_V2, kiểm thu hồi cả chuỗi; không kiểm
    /// được thu hồi thì coi là không hợp lệ) và người ký khớp <see cref="PawnIoSigner"/>. Chứng chỉ người ký lấy từ chính
    /// trạng thái của lần kiểm đó (WTHelperProvDataFromStateData → WTHelperGetProvSignerFromChain), không đọc file lần nữa
    /// và không dùng X509Certificate.CreateFromSignedFile (đã lỗi thời).
    /// </summary>
    static bool VerifySignature(SafeFileHandle handle, string path)
    {
        var added = false;
        var pathPtr = IntPtr.Zero;
        var filePtr = IntPtr.Zero;
        var action = NativeMethods.WINTRUST_ACTION_GENERIC_VERIFY_V2;
        var data = new NativeMethods.WINTRUST_DATA();
        var stateOpen = false;
        try
        {
            handle.DangerousAddRef(ref added);   // handle không bị đóng giữa chừng kể cả khi bên gọi hết thời gian chờ
            pathPtr = Marshal.StringToHGlobalUni(path);
            var file = new NativeMethods.WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<NativeMethods.WINTRUST_FILE_INFO>(),
                pcwszFilePath = pathPtr,
                hFile = handle.DangerousGetHandle(),
            };
            filePtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(file, filePtr, false);

            data.cbStruct = (uint)Marshal.SizeOf<NativeMethods.WINTRUST_DATA>();
            data.dwUIChoice = NativeMethods.WTD_UI_NONE;
            data.fdwRevocationChecks = NativeMethods.WTD_REVOKE_WHOLECHAIN;
            data.dwUnionChoice = NativeMethods.WTD_CHOICE_FILE;
            data.pFile = filePtr;
            data.dwStateAction = NativeMethods.WTD_STATEACTION_VERIFY;
            data.dwProvFlags = NativeMethods.WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT | NativeMethods.WTD_DISABLE_MD2_MD4;

            var status = NativeMethods.WinVerifyTrust(new IntPtr(-1), ref action, ref data);   // -1: không có giao diện
            stateOpen = data.hWVTStateData != IntPtr.Zero;
            if (status != 0)
            {
                Trace.TraceWarning($"Chữ ký PawnIO không hợp lệ: 0x{status:X8}");
                return false;
            }

            using var signer = GetSignerCertificate(data.hWVTStateData);
            if (signer is null || !PawnIoSigner.Matches(signer.SubjectName))
            {
                Trace.TraceWarning($"Người ký PawnIO không khớp: {signer?.Subject}");
                return false;
            }
            return true;
        }
        finally
        {
            if (stateOpen)
            {
                data.dwStateAction = NativeMethods.WTD_STATEACTION_CLOSE;
                NativeMethods.WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            }
            if (filePtr != IntPtr.Zero) Marshal.FreeHGlobal(filePtr);
            if (pathPtr != IntPtr.Zero) Marshal.FreeHGlobal(pathPtr);
            if (added) handle.DangerousRelease();
        }
    }

    // Chứng chỉ của người ký chính (phần tử 0 của chuỗi). X509Certificate2(IntPtr) chép lại context nên vẫn dùng được
    // sau khi đóng trạng thái WinVerifyTrust.
    static X509Certificate2? GetSignerCertificate(IntPtr stateData)
    {
        var provData = NativeMethods.WTHelperProvDataFromStateData(stateData);
        if (provData == IntPtr.Zero) return null;
        var signerPtr = NativeMethods.WTHelperGetProvSignerFromChain(provData, 0, false, 0);
        if (signerPtr == IntPtr.Zero) return null;
        var signer = Marshal.PtrToStructure<NativeMethods.CRYPT_PROVIDER_SGNR_HEAD>(signerPtr);
        if (signer.csCertChain == 0 || signer.pasCertChain == IntPtr.Zero) return null;
        // CRYPT_PROVIDER_CERT { DWORD cbStruct; PCCERT_CONTEXT pCert; ... }: pCert nằm sau cbStruct, căn theo cỡ con trỏ.
        var certContext = Marshal.ReadIntPtr(signer.pasCertChain, IntPtr.Size);
        return certContext == IntPtr.Zero ? null : new X509Certificate2(certContext);
    }

    // ---------- Chạy bộ cài ----------

    static async Task<PawnIoResult> RunInstallerAsync(string path, string directory, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory,
        };
        start.ArgumentList.Add("-install");
        start.ArgumentList.Add("-silent");

        using var process = Process.Start(start);
        if (process is null) return PawnIoResult.InstallFailed;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(InstallTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Trace.TraceWarning("Bộ cài PawnIO chạy quá lâu, đã dừng.");
            try { process.Kill(entireProcessTree: true); }
            catch (Exception ex) { Trace.TraceWarning($"Không dừng được bộ cài PawnIO: {ex.Message}"); }
            return PawnIoResult.InstallFailed;
        }
        if (process.ExitCode is not (0 or 3010))
            Trace.TraceWarning($"Bộ cài PawnIO thoát với mã {process.ExitCode}");
        return PawnIoResults.FromExitCode(process.ExitCode);
    }

    // Xoá file tải trong mọi trường hợp; bị giữ tạm (trình diệt virus đang quét, bộ cài vừa bị dừng) thì thử lại vài lần.
    static async Task TryDeleteAsync(string path)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(300);
            }
        }
        Trace.TraceWarning($"Không xoá được file tải PawnIO: {path}");
    }
}
