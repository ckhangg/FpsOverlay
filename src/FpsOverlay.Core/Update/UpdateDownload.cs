using System.Security.Cryptography;

namespace FpsOverlay.Core.Update;

/// <summary>
/// Phần thuần logic của việc tải bản cập nhật (không mạng, không UI): chỉ tải từ các host GitHub qua HTTPS, chép có giới
/// hạn kích thước và băm SHA-256 ngay trên dòng dữ liệu. App chạy với quyền Admin nên mọi kiểm tra ở đây đều "từ chối nếu nghi".
/// </summary>
public static class UpdateDownload
{
    /// <summary>Giới hạn exe tải về (bản hiện khoảng 160 MB); lớn hơn thì từ chối.</summary>
    public const long MaxExeBytes = 400L * 1024 * 1024;

    /// <summary>Giới hạn file <c>.sha256</c> (thật ra chỉ khoảng 80 byte).</summary>
    public const long MaxHashFileBytes = 4 * 1024;

    /// <summary>Số lần chuyển hướng tối đa (github.com → objects/release-assets là 1 lần).</summary>
    public const int MaxRedirects = 5;

    /// <summary>
    /// Host được tải: trang phát hành (github.com) và nơi GitHub chuyển hướng file asset tới. So khớp nguyên tên, không
    /// nhận tên miền con hay đuôi (github.com.evil.com).
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedHosts =
        ["github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"];

    /// <summary>URL tuyệt đối, HTTPS, cổng mặc định, không userinfo, host nằm trong <see cref="AllowedHosts"/>.</summary>
    public static bool IsAllowedUrl(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri) return false;
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0) return false;
        return AllowedHosts.Any(h => string.Equals(uri.Host, h, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Đích của một lần chuyển hướng (Location tương đối thì tính theo URL hiện tại); null nếu thiếu hay không được phép.</summary>
    public static Uri? NextHop(Uri current, Uri? location)
    {
        if (location is null) return null;
        var next = location.IsAbsoluteUri ? location : new Uri(current, location);
        return IsAllowedUrl(next) ? next : null;
    }

    /// <summary>
    /// Chép <paramref name="source"/> sang <paramref name="destination"/>, băm SHA-256 phần đã chép (hex chữ thường).
    /// Quá <paramref name="maxBytes"/> thì ném <see cref="InvalidDataException"/> trước khi ghi phần vượt.
    /// <paramref name="readTimeout"/>: một lần đọc chờ quá lâu (kết nối treo) thì ném <see cref="TimeoutException"/>.
    /// <paramref name="progress"/> nhận tổng số byte đã chép sau mỗi khối.
    /// </summary>
    public static async Task<(string Sha256, long Length)> CopyHashedAsync(Stream source, Stream destination, long maxBytes,
        Action<long>? progress, TimeSpan? readTimeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            if (readTimeout is { } t) stall.CancelAfter(t);   // đặt lại đồng hồ mỗi lần đọc
            int read;
            try
            {
                read = await source.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Download stalled");
            }
            if (read == 0) break;
            total += read;
            if (total > maxBytes) throw new InvalidDataException($"Download is larger than {maxBytes} bytes");
            hash.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            progress?.Invoke(total);
        }
        return (Convert.ToHexStringLower(hash.GetHashAndReset()), total);
    }

    /// <summary>Hai chuỗi hex SHA-256 bằng nhau (không phân biệt hoa thường); thiếu băm mong đợi thì là không khớp.</summary>
    public static bool HashMatches(string actual, string? expected) =>
        !string.IsNullOrEmpty(expected) && expected.Length == 64 &&
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
