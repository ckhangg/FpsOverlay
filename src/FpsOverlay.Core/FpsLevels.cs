namespace FpsOverlay.Core;

public enum FpsLevel { None, Good, Medium, Bad }

public static class FpsLevels
{
    /// <summary>Phân loại FPS theo giá trị đã làm tròn (khớp số hiển thị). <c>null</c> → <see cref="FpsLevel.None"/>.</summary>
    public static FpsLevel Classify(double? fps, int goodThreshold, int badThreshold)
    {
        if (fps is null) return FpsLevel.None;
        var rounded = Math.Round(fps.Value, MidpointRounding.AwayFromZero);
        if (rounded >= goodThreshold) return FpsLevel.Good;
        if (rounded < badThreshold) return FpsLevel.Bad;
        return FpsLevel.Medium;
    }
}
