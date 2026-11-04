using System.Text;
using FpsOverlay.Core.Update;

namespace FpsOverlay.Core.Tests;

public class UpdateDownloadTests
{
    [Theory]
    [InlineData("https://github.com/ckhangg/FpsOverlay/releases/download/v1.5.0/FpsOverlay.exe")]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/1?x=y")]
    [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/1")]
    [InlineData("https://GitHub.com/a")]
    public void Allows_github_download_hosts_over_https(string url)
    {
        Assert.True(UpdateDownload.IsAllowedUrl(new Uri(url)));
    }

    [Theory]
    [InlineData("http://github.com/a")]                          // không HTTPS
    [InlineData("https://example.com/FpsOverlay.exe")]           // host lạ
    [InlineData("https://github.com.evil.com/a")]                // giả dạng bằng đuôi
    [InlineData("https://evilgithub.com/a")]
    [InlineData("https://api.github.com/a")]                     // host con không nằm trong danh sách
    [InlineData("https://github.com@evil.com/a")]                // userinfo: host thật là evil.com
    [InlineData("https://user@github.com/a")]                    // userinfo trên host đúng cũng từ chối
    [InlineData("https://github.com:8443/a")]                    // cổng lạ
    [InlineData("ftp://github.com/a")]
    [InlineData("file:///C:/x.exe")]
    public void Rejects_other_urls(string url)
    {
        Assert.False(UpdateDownload.IsAllowedUrl(new Uri(url)));
    }

    [Fact]
    public void Rejects_null_and_relative()
    {
        Assert.False(UpdateDownload.IsAllowedUrl(null));
        Assert.False(UpdateDownload.IsAllowedUrl(new Uri("/a/b", UriKind.Relative)));
    }

    [Fact]
    public void Next_hop_resolves_relative_location_and_checks_it()
    {
        var current = new Uri("https://github.com/ckhangg/FpsOverlay/releases/download/v1.5.0/FpsOverlay.exe");
        Assert.Equal(new Uri("https://github.com/other/path"), UpdateDownload.NextHop(current, new Uri("/other/path", UriKind.Relative)));
        Assert.Equal(new Uri("https://objects.githubusercontent.com/x"),
            UpdateDownload.NextHop(current, new Uri("https://objects.githubusercontent.com/x")));
        Assert.Null(UpdateDownload.NextHop(current, new Uri("http://objects.githubusercontent.com/x")));
        Assert.Null(UpdateDownload.NextHop(current, new Uri("https://evil.example/x")));
        Assert.Null(UpdateDownload.NextHop(current, null));
    }

    [Fact]
    public async Task Copy_hashes_copies_and_reports_progress()
    {
        var data = Encoding.ASCII.GetBytes("abc");
        using var source = new MemoryStream(data);
        using var target = new MemoryStream();
        var seen = new List<long>();

        var (hash, length) = await UpdateDownload.CopyHashedAsync(source, target, 3, seen.Add, null, CancellationToken.None);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
        Assert.Equal(3, length);
        Assert.Equal(data, target.ToArray());
        Assert.Equal(3, seen[^1]);
    }

    [Fact]
    public async Task Copy_of_large_stream_hashes_all_chunks()
    {
        var data = new byte[1_000_003];
        new Random(42).NextBytes(data);
        using var source = new MemoryStream(data);
        using var target = new MemoryStream();

        var (hash, length) = await UpdateDownload.CopyHashedAsync(source, target, data.Length, null, null, CancellationToken.None);

        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data)), hash);
        Assert.Equal(data.Length, length);
        Assert.Equal(data, target.ToArray());
    }

    [Fact]
    public async Task Copy_over_limit_throws()
    {
        using var source = new MemoryStream(new byte[101]);
        using var target = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            UpdateDownload.CopyHashedAsync(source, target, 100, null, null, CancellationToken.None));
        Assert.True(target.Length <= 100);   // không ghi phần vượt giới hạn
    }

    [Fact]
    public async Task Copy_cancelled_throws_operation_cancelled()
    {
        using var source = new MemoryStream(new byte[10]);
        using var target = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            UpdateDownload.CopyHashedAsync(source, target, 100, null, null, cts.Token));
    }

    [Fact]
    public async Task Copy_stalled_read_times_out()
    {
        using var source = new StallingStream();
        using var target = new MemoryStream();
        await Assert.ThrowsAsync<TimeoutException>(() =>
            UpdateDownload.CopyHashedAsync(source, target, 100, null, TimeSpan.FromMilliseconds(100), CancellationToken.None));
    }

    [Fact]
    public void Hash_comparison_ignores_case_and_rejects_missing()
    {
        var h = new string('a', 64);
        Assert.True(UpdateDownload.HashMatches(h, h.ToUpperInvariant()));
        Assert.False(UpdateDownload.HashMatches(h, new string('b', 64)));
        Assert.False(UpdateDownload.HashMatches(h, null));
        Assert.False(UpdateDownload.HashMatches(h, ""));
    }

    // Đọc không bao giờ trả về (chờ tới khi bị huỷ), như kết nối treo.
    sealed class StallingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
