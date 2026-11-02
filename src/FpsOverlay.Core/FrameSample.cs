namespace FpsOverlay.Core;

/// <summary>Một khung hình mà PresentMon ghi nhận: tiến trình nào vẽ, vào thời điểm nào (giây).</summary>
public readonly record struct FrameSample(int ProcessId, double TimeSeconds);
