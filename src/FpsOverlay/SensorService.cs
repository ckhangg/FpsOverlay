using System.Diagnostics;
using FpsOverlay.Core;
using LibreHardwareMonitor.Hardware;
using SensorValue = FpsOverlay.Core.SensorValue;   // trùng tên với kiểu cùng tên của LibreHardwareMonitor

namespace FpsOverlay;

/// <summary>
/// Đọc tải/nhiệt độ GPU, CPU và RAM mỗi giây trên một luồng nền riêng.
/// LibreHardwareMonitor chỉ bật CPU + GPU; thư viện không tự cài driver nào (nhiệt độ CPU AMD/Intel cần
/// driver PawnIO do app cài riêng, rồi gọi <see cref="Reinitialize"/>). Mọi ngoại lệ từ thư viện đều bị nuốt, giá trị thành null.
/// </summary>
sealed class SensorService : IDisposable
{
    const int IntervalMs = 1000;

    /// <summary>Hộp bất biến: <see cref="SensorSnapshot"/> có nhiều double? nên gán thẳng có thể bị xé, gán tham chiếu thì nguyên tử.</summary>
    sealed class Box
    {
        public readonly SensorSnapshot Value;
        public Box(SensorSnapshot value) => Value = value;
    }

    readonly object _gate = new();          // bảo vệ _thread / _stop / _computer khi Start/Stop/Dispose
    Thread? _thread;
    CancellationTokenSource? _stop;
    Computer? _computer;
    volatile Box _latest = new(SensorSnapshot.Empty);
    volatile bool _reopen;
    volatile bool _cpuTempAvailable;
    readonly HashSet<string> _gpuSensorsLogged = [];   // mỗi card chỉ ghi danh sách cảm biến một lần (chỉ đụng từ luồng đọc)
    string? _previousGpuId;                 // chỉ đụng từ luồng đọc
    bool _disposed;

    /// <summary>Số đọc mới nhất; đọc không khoá từ luồng UI.</summary>
    public SensorSnapshot Latest => _latest.Value;

    /// <summary>Lần đọc gần nhất có cảm biến nhiệt độ CPU thực sự cho giá trị hay không.</summary>
    public bool CpuTemperatureAvailable => _cpuTempAvailable;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _thread is not null) return;
            _stop = new CancellationTokenSource();
            var token = _stop.Token;
            _thread = new Thread(() => Loop(token)) { IsBackground = true, Name = "SensorService" };
            _thread.Start();
        }
    }

    /// <summary>Dừng vòng đọc (chờ luồng thoát); không giải phóng thư viện, có thể <see cref="Start"/> lại.</summary>
    public void Stop()
    {
        Thread? t;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            t = _thread;
            cts = _stop;
            _thread = null;
            _stop = null;
        }
        if (t is null) return;
        cts!.Cancel();
        if (t != Thread.CurrentThread) t.Join(TimeSpan.FromSeconds(5));
        cts.Dispose();
    }

    /// <summary>Đóng rồi mở lại <c>Computer</c> ở vòng đọc kế tiếp (dùng sau khi cài PawnIO để thấy nhiệt độ CPU).</summary>
    public void Reinitialize() => _reopen = true;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        Stop();
        CloseComputer();
    }

    void Loop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_computer is null || _reopen)
                {
                    _reopen = false;
                    OpenComputer();
                }

                Publish(ReadAll());
                // Chờ 1 giây nhưng thoát ngay khi Stop
                token.WaitHandle.WaitOne(IntervalMs);
            }
        }
        catch (Exception ex)
        {
            // Không để ngoại lệ ngoài ý muốn làm sập cả app
            Debug.WriteLine($"SensorService loop: {ex}");
        }
    }

    void Publish(SensorSnapshot s) => _latest = new Box(s);

    void OpenComputer()
    {
        CloseComputer();
        try
        {
            var c = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            c.Open();
            _computer = c;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SensorService open: {ex.Message}");
            _computer = null;
        }
    }

    void CloseComputer()
    {
        var c = _computer;
        _computer = null;
        if (c is null) return;
        try { c.Close(); } catch (Exception ex) { Debug.WriteLine($"SensorService close: {ex.Message}"); }
    }

    SensorSnapshot ReadAll()
    {
        double? cpuLoad = null, cpuTemp = null, gpuLoad = null, gpuTemp = null;
        double? gpuMem = null, gpuClock = null, gpuPower = null, gpuFan = null;

        var c = _computer;
        if (c is not null)
        {
            try
            {
                var gpus = new List<GpuReading>();
                foreach (var hw in c.Hardware)
                {
                    try
                    {
                        hw.Update();
                        switch (hw.HardwareType)
                        {
                            case HardwareType.Cpu:
                                cpuLoad = Pick(hw, SensorType.Load, "CPU Total");
                                cpuTemp = PickCpuTemp(hw);
                                break;
                            case HardwareType.GpuNvidia:
                            case HardwareType.GpuAmd:
                            case HardwareType.GpuIntel:
                                var values = new List<SensorValue>();
                                foreach (var s in hw.Sensors)
                                    values.Add(new SensorValue(s.SensorType.ToString(), s.Name, s.Value));
                                if (!_gpuSensorsLogged.Contains(hw.Identifier.ToString()))
                                {
                                    _gpuSensorsLogged.Add(hw.Identifier.ToString());
                                    Trace.TraceInformation("SensorService GPU {0}: {1}", hw.Name,
                                        string.Join("; ", values.Select(v => $"{v.Type}/{v.Name}")));
                                }
                                gpus.Add(GpuSensorPick.Read(hw.Identifier.ToString(), values));
                                break;
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"SensorService update {hw.Name}: {ex.Message}"); }
                }

                var busiest = SensorMath.BusiestGpu(gpus, _previousGpuId);
                if (busiest is { } g)
                {
                    _previousGpuId = g.Id;
                    gpuLoad = g.Load;
                    gpuTemp = g.Temp;
                    gpuMem = g.MemoryUsedGb;
                    gpuClock = g.ClockMhz;
                    gpuPower = g.PowerW;
                    gpuFan = g.FanRpm;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"SensorService read: {ex.Message}"); }
        }

        _cpuTempAvailable = cpuTemp is not null;
        return new SensorSnapshot(gpuLoad, gpuTemp, cpuLoad, cpuTemp, ReadRamUsedGb(), gpuMem, gpuClock, gpuPower, gpuFan);
    }

    static double? Pick(IHardware hw, SensorType type, string name)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == type && s.Name == name)
                return s.Value is float v && float.IsFinite(v) ? v : null;
        }
        return null;
    }

    /// <summary>
    /// Nhiệt độ CPU: ưu tiên "Core (Tctl/Tdie)" (AMD), "CPU Package" (Intel), không có thì cảm biến nóng nhất.
    /// Khi chưa có driver PawnIO, CPU AMD vẫn liệt kê cảm biến nhưng trả 0 (đo thực tế) nên giá trị &lt;= 0 coi là không có.
    /// </summary>
    static double? PickCpuTemp(IHardware hw)
    {
        double? tctl = null, package = null, max = null;
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType != SensorType.Temperature || s.Value is not float v || !float.IsFinite(v) || v <= 0) continue;
            if (s.Name == "Core (Tctl/Tdie)") tctl = v;
            else if (s.Name == "CPU Package") package = v;
            if (max is null || v > max) max = v;
        }
        return tctl ?? package ?? max;
    }

    static double? ReadRamUsedGb()
    {
        try
        {
            var m = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
            if (!NativeMethods.GlobalMemoryStatusEx(ref m)) return null;
            return (m.ullTotalPhys - m.ullAvailPhys) / (1024.0 * 1024 * 1024);
        }
        catch { return null; }
    }
}
