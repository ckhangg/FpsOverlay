using System.Xml;
using System.Xml.Linq;

namespace FpsOverlay.Core.Autostart;

/// <summary>
/// Nội dung tác vụ Task Scheduler bật app khi đăng nhập Windows. Dùng Task Scheduler (không dùng mục Startup)
/// vì app cần quyền Administrator: tác vụ "quyền cao nhất" chạy thẳng, không hiện hộp UAC mỗi lần bật máy.
/// </summary>
public static class AutostartTask
{
    public const string TaskName = "FpsOverlay";
    /// <summary>Tham số tác vụ truyền cho app: phân biệt khởi động cùng Windows với người dùng tự mở.</summary>
    public const string AutostartArgument = "--autostart";

    static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    /// <summary>XML cho <c>schtasks /Create /XML</c>: chạy <paramref name="exePath"/> khi <paramref name="userId"/> đăng nhập.</summary>
    public static string BuildXml(string exePath, string userId)
    {
        XElement E(string name, params object[] content) => new(Ns + name, content);

        var task = E("Task", new XAttribute("version", "1.2"),
            E("RegistrationInfo", E("Description", "EzFPS")),
            E("Triggers",
                E("LogonTrigger",
                    E("Enabled", "true"),
                    E("UserId", userId),
                    E("Delay", "PT5S"))),   // để Windows khởi động xong dịch vụ đo (ETW) trước PresentMon
            E("Principals",
                E("Principal", new XAttribute("id", "Author"),
                    E("UserId", userId),
                    E("LogonType", "InteractiveToken"),
                    E("RunLevel", "HighestAvailable"))),
            E("Settings",
                E("MultipleInstancesPolicy", "IgnoreNew"),
                E("DisallowStartIfOnBatteries", "false"),
                E("StopIfGoingOnBatteries", "false"),
                E("AllowHardTerminate", "true"),
                E("StartWhenAvailable", "false"),
                E("RunOnlyIfNetworkAvailable", "false"),
                E("IdleSettings", E("StopOnIdleEnd", "false"), E("RestartOnIdle", "false")),
                E("AllowStartOnDemand", "true"),
                E("Enabled", "true"),
                E("Hidden", "false"),
                E("RunOnlyIfIdle", "false"),
                E("WakeToRun", "false"),
                E("ExecutionTimeLimit", "PT0S"),   // mặc định 72 giờ thì Windows tắt app
                E("Priority", "4")),               // mặc định 7 (thấp hơn bình thường)
            E("Actions", new XAttribute("Context", "Author"),
                E("Exec",
                    E("Command", exePath),
                    E("Arguments", AutostartArgument),
                    E("WorkingDirectory", Path.GetDirectoryName(exePath) ?? ""))));

        return new XDeclaration("1.0", "UTF-16", null) + Environment.NewLine + task;
    }

    /// <summary>Đường dẫn exe trong XML của tác vụ (đầu ra <c>schtasks /Query /XML</c>), hoặc null nếu không đọc được.</summary>
    public static string? ReadCommand(string xml)
    {
        try
        {
            return XDocument.Parse(xml).Root?.Element(Ns + "Actions")?.Element(Ns + "Exec")?.Element(Ns + "Command")?.Value;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>Tham số trong XML của tác vụ, hoặc null nếu không có/không đọc được.</summary>
    public static string? ReadArguments(string xml)
    {
        try
        {
            return XDocument.Parse(xml).Root?.Element(Ns + "Actions")?.Element(Ns + "Exec")?.Element(Ns + "Arguments")?.Value;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>Tác vụ cần tạo lại: trỏ exe khác (không phân biệt hoa thường) hoặc thiếu/sai tham số <see cref="AutostartArgument"/>.</summary>
    public static bool NeedsRepair(string xml, string exePath) =>
        !string.Equals(ReadCommand(xml), exePath, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(ReadArguments(xml)?.Trim(), AutostartArgument, StringComparison.Ordinal);
}
