namespace FpsOverlay.Core;

public enum HeatState { Normal, Hot, Alarming }

/// <summary>
/// Theo dõi một đợt nóng: nóng liên tục đủ số giây giữ thì báo động một lần, và đợt chỉ kết thúc
/// khi nhiệt hạ xuống dưới ngưỡng một khoảng đệm (tránh báo lại liên tục khi nhiệt dao động quanh ngưỡng).
/// </summary>
public sealed class HeatAlarm
{
    /// <summary>Số độ phải hạ dưới ngưỡng thì đợt báo động mới kết thúc.</summary>
    public const int ReleaseMargin = 3;

    private double? _hotSince;

    public HeatState State { get; private set; } = HeatState.Normal;

    /// <summary>Cập nhật theo nhiệt độ mới; trả <c>true</c> đúng một lần khi đợt nóng bắt đầu báo động.</summary>
    public bool Update(double? temp, int threshold, int holdSeconds, double nowSeconds)
    {
        if (temp is not { } t || !double.IsFinite(t))
        {
            Reset();
            return false;
        }

        if (State == HeatState.Alarming)
        {
            // So trên giá trị đã làm tròn như hiển thị.
            if (Math.Round(t, MidpointRounding.AwayFromZero) < threshold - ReleaseMargin) Reset();
            return false;
        }

        if (!HeatLevels.IsHot(t, threshold))
        {
            Reset();
            return false;
        }

        _hotSince ??= nowSeconds;
        if (nowSeconds - _hotSince.Value >= holdSeconds)
        {
            State = HeatState.Alarming;
            return true;
        }
        State = HeatState.Hot;
        return false;
    }

    public void Reset()
    {
        _hotSince = null;
        State = HeatState.Normal;
    }
}
