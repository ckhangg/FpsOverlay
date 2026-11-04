using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using FpsOverlay.Core.Update;

namespace FpsOverlay;

/// <summary>
/// Hỏi GitHub Releases bản mới nhất của app (<c>releases/latest</c>, chỉ HTTPS) và tải bản đó (<see cref="DownloadAsync"/>).
/// Không quyết định có báo hay không: việc đó của <see cref="UpdatePolicy.ShouldOffer"/>. Gọi từ luồng UI được: phần mạng
/// chạy trên luồng nền.
/// </summary>
sealed class UpdateService
{
    const string LatestReleaseUrl = "https://api.github.com/repos/ckhangg/FpsOverlay/releases/latest";
    // JSON release thường vài KB (ghi chú phát hành dài cũng không tới mức này).
    const int MaxResponseBytes = 1024 * 1024;

    /// <summary>Phiên bản đang chạy (major.minor.patch) từ InformationalVersion của assembly; đọc không được thì từ AssemblyVersion.</summary>
    public static Version CurrentVersion { get; } = ReadCurrentVersion();

    // Một HttpClient cho cả app (không tạo mới mỗi lần kiểm). Chỉ theo chuyển hướng HTTPS (HttpClient không theo HTTPS → HTTP).
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 })
        {
            Timeout = TimeSpan.FromSeconds(10),
            MaxResponseContentBufferSize = MaxResponseBytes,
        };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FpsOverlay", CurrentVersion.ToString(3)));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    // Tải exe (khoảng 160 MB): client riêng, đọc dạng luồng (không đệm cả file), tự theo chuyển hướng từng bước để kiểm
    // host của mọi bước (GetFollowingRedirectsAsync). Cả lần tải tối đa DownloadTimeout; một lần đọc treo quá StallTimeout
    // thì dừng.
    static readonly HttpClient DownloadHttp = CreateDownloadClient();
    static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(20);
    static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    static HttpClient CreateDownloadClient()
    {
        var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan,   // đã có DownloadTimeout + StallTimeout
        };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FpsOverlay", CurrentVersion.ToString(3)));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return http;
    }

    static Version ReadCurrentVersion()
    {
        var assembly = typeof(UpdateService).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (UpdatePolicy.ParseVersion(informational) is { } version) return version;
        var v = assembly.GetName().Version ?? new Version(0, 0, 0);
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }

    /// <summary>
    /// Bản phát hành mới nhất; null nếu GitHub trả về release không dùng được (JSON hỏng, thiếu asset exe/sha256, URL không HTTPS).
    /// Lỗi mạng, quá 10 giây, HTTP lỗi (kể cả giới hạn lượt gọi, chưa có release) thì ném exception, để nút bấm tay báo lý do.
    /// </summary>
    public Task<ReleaseInfo?> CheckAsync(CancellationToken cancellationToken) => Task.Run(async () =>
    {
        using var response = await Http.GetAsync(LatestReleaseUrl, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub HTTP {(int)response.StatusCode}", null, response.StatusCode);
        if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
            throw new HttpRequestException("GitHub redirect to non-HTTPS");
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var release = ReleaseInfo.Parse(json);
        if (release is null) Trace.TraceWarning("Release mới nhất trên GitHub không dùng được (thiếu asset hoặc JSON lạ).");
        return release;
    }, cancellationToken);

    /// <summary>
    /// Tải bản <paramref name="release"/> vào <see cref="SecureTemp.PrepareDirectory"/> và trả về đường dẫn exe đã kiểm. Tải
    /// file <c>.sha256</c> trước, rồi exe (ghi <see cref="FileMode.CreateNew"/>, tên riêng mỗi lần, băm ngay lúc ghi). Chỉ
    /// tải từ host GitHub qua HTTPS (<see cref="UpdateDownload.IsAllowedUrl"/>, kiểm cả từng bước chuyển hướng), tối đa
    /// <see cref="UpdateDownload.MaxExeBytes"/>. Băm không khớp, thiếu dữ liệu, huỷ hay lỗi: xoá file rồi ném lỗi.
    /// <paramref name="progress"/> nhận tỉ lệ 0..1 (NaN khi máy chủ không báo kích thước); gọi từ luồng nền.
    /// </summary>
    public Task<string> DownloadAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            var exeUri = AllowedUri(release.ExeUrl);
            var hashUri = AllowedUri(release.HashUrl);
            using var overall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            overall.CancelAfter(DownloadTimeout);
            var token = overall.Token;
            try
            {
                var expected = await DownloadHashAsync(hashUri, token).ConfigureAwait(false);
                var directory = SecureTemp.PrepareDirectory();
                var path = Path.Combine(directory, $"{UpdateHandover.TempFilePrefix}{Guid.NewGuid():N}.exe");
                try
                {
                    await DownloadExeAsync(exeUri, path, expected, progress, token).ConfigureAwait(false);
                    return path;
                }
                catch
                {
                    TryDelete(path);
                    throw;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Download took too long");
            }
        }, cancellationToken);

    static Uri AllowedUri(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && UpdateDownload.IsAllowedUrl(uri)
            ? uri
            : throw new InvalidDataException($"Download URL not allowed: {url}");

    // File .sha256 (vài chục byte) đọc vào bộ nhớ, có giới hạn; không có chuỗi băm hợp lệ thì ném lỗi.
    static async Task<string> DownloadHashAsync(Uri uri, CancellationToken token)
    {
        using var response = await GetFollowingRedirectsAsync(uri, token).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength is > UpdateDownload.MaxHashFileBytes)
            throw new InvalidDataException("SHA-256 file is too large");
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await UpdateDownload.CopyHashedAsync(source, buffer, UpdateDownload.MaxHashFileBytes, null, StallTimeout, token)
            .ConfigureAwait(false);
        return UpdatePolicy.ParseSha256File(Encoding.UTF8.GetString(buffer.ToArray()))
               ?? throw new InvalidDataException("No SHA-256 in the .sha256 file");
    }

    static async Task DownloadExeAsync(Uri uri, string path, string expectedHash, IProgress<double>? progress, CancellationToken token)
    {
        using var response = await GetFollowingRedirectsAsync(uri, token).ConfigureAwait(false);
        var length = response.Content.Headers.ContentLength;
        if (length is > UpdateDownload.MaxExeBytes)
            throw new InvalidDataException($"Update is larger than {UpdateDownload.MaxExeBytes / (1024 * 1024)} MB");

        // Báo tiến trình khi đổi ít nhất 1% (khoảng trăm lần), không phải mỗi khối 80 KB.
        var lastPercent = -1;
        void Report(long done)
        {
            if (progress is null) return;
            if (length is not > 0)
            {
                if (lastPercent != int.MaxValue) progress.Report(double.NaN);   // không biết kích thước: thanh chạy vô định
                lastPercent = int.MaxValue;
                return;
            }
            var percent = (int)(done * 100 / length.Value);
            if (percent == lastPercent) return;
            lastPercent = percent;
            progress.Report(Math.Min(1.0, (double)done / length.Value));
        }

        string actual;
        long total;
        await using (var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
        await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                         FileOptions.Asynchronous))
        {
            (actual, total) = await UpdateDownload.CopyHashedAsync(source, target, UpdateDownload.MaxExeBytes, Report,
                StallTimeout, token).ConfigureAwait(false);
            await target.FlushAsync(token).ConfigureAwait(false);
        }
        if (total == 0 || (length is { } expectedLength && total != expectedLength))
            throw new InvalidDataException("Download is incomplete");
        if (!UpdateDownload.HashMatches(actual, expectedHash))
            throw new InvalidDataException("SHA-256 mismatch");
    }

    // Theo chuyển hướng bằng tay: mọi URL (kể cả từng bước chuyển hướng) phải là HTTPS tới host được phép. Trả về phản hồi
    // thành công (mới đọc phần đầu); HTTP lỗi thì ném lỗi.
    static async Task<HttpResponseMessage> GetFollowingRedirectsAsync(Uri uri, CancellationToken token)
    {
        for (var hop = 0; ; hop++)
        {
            if (!UpdateDownload.IsAllowedUrl(uri))
                throw new HttpRequestException($"Download host not allowed: {uri.Host}");
            var response = await DownloadHttp.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var next = UpdateDownload.NextHop(uri, response.Headers.Location);
                response.Dispose();
                if (hop >= UpdateDownload.MaxRedirects) throw new HttpRequestException("Too many redirects");
                uri = next ?? throw new HttpRequestException("Redirect to a host that isn't allowed");
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                throw new HttpRequestException($"HTTP {(int)status}", null, status);
            }
            return response;
        }
    }

    // Lỗi chỉ ghi vết: thư mục chỉ Admin ghi được, bản cài tự dọn exe cũ lúc khởi động (SecureTemp.DeleteStaleExecutables).
    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Không xoá được file tải dở: {ex.Message}");
        }
    }
}
