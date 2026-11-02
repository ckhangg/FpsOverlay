using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FpsOverlay;

/// <summary>Cửa sổ trong suốt hiện số FPS: luôn trên cùng, chuột bấm xuyên qua, không chiếm focus.</summary>
public partial class OverlayWindow : Window
{
    readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    IntPtr _hwnd;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        _topmostTimer.Tick += (_, _) => KeepOnTop();
    }

    public void SetText(string text)
    {
        if (FpsText.Text != text)
            FpsText.Text = text;
    }

    void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED |
                 NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
        _topmostTimer.Start();
    }

    // Game borderless đôi khi đè lên cửa sổ topmost; đặt lại định kỳ.
    void KeepOnTop()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible)
            return;
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnClosed(EventArgs e)
    {
        _topmostTimer.Stop();
        base.OnClosed(e);
    }
}
