using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using FpsOverlay.Core.Update;

namespace FpsOverlay;

/// <summary>
/// Hỏi GitHub Releases bản mới nhất của app (<c>releases/latest</c>, chỉ HTTPS). Không quyết định có báo hay không:
/// việc đó của <see cref="UpdatePolicy.ShouldOffer"/>. Gọi từ luồng UI được: phần mạng chạy trên luồng nền.
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
}
