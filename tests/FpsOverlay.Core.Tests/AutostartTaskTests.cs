using System.Xml.Linq;
using FpsOverlay.Core.Autostart;

namespace FpsOverlay.Core.Tests;

public class AutostartTaskTests
{
    static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    static XElement Settings(string xml) => XDocument.Parse(xml).Root!.Element(Ns + "Settings")!;

    [Fact]
    public void Runs_exe_at_logon_of_user_with_highest_privileges()
    {
        var xml = AutostartTask.BuildXml(@"C:\Games\FPS\FpsOverlay.exe", @"PC\khang");
        var root = XDocument.Parse(xml).Root!;

        var trigger = root.Element(Ns + "Triggers")!.Element(Ns + "LogonTrigger")!;
        Assert.Equal(@"PC\khang", trigger.Element(Ns + "UserId")!.Value);

        var principal = root.Element(Ns + "Principals")!.Element(Ns + "Principal")!;
        Assert.Equal("HighestAvailable", principal.Element(Ns + "RunLevel")!.Value);
        Assert.Equal("InteractiveToken", principal.Element(Ns + "LogonType")!.Value);

        var exec = root.Element(Ns + "Actions")!.Element(Ns + "Exec")!;
        Assert.Equal(@"C:\Games\FPS\FpsOverlay.exe", exec.Element(Ns + "Command")!.Value);
        Assert.Equal(@"C:\Games\FPS", exec.Element(Ns + "WorkingDirectory")!.Value);
    }

    [Fact]
    public void Is_not_stopped_on_battery_or_after_time_limit()
    {
        // Mặc định Task Scheduler dừng tác vụ khi rút sạc và sau 72 giờ, và chạy ở độ ưu tiên thấp.
        var settings = Settings(AutostartTask.BuildXml(@"C:\FpsOverlay.exe", "u"));
        Assert.Equal("false", settings.Element(Ns + "DisallowStartIfOnBatteries")!.Value);
        Assert.Equal("false", settings.Element(Ns + "StopIfGoingOnBatteries")!.Value);
        Assert.Equal("PT0S", settings.Element(Ns + "ExecutionTimeLimit")!.Value);
        Assert.Equal("4", settings.Element(Ns + "Priority")!.Value);
    }

    [Fact]
    public void Read_command_round_trips_paths_with_special_characters()
    {
        const string path = @"C:\Users\A & B\Tải về\<FPS>\FpsOverlay.exe";
        Assert.Equal(path, AutostartTask.ReadCommand(AutostartTask.BuildXml(path, "u")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not xml")]
    [InlineData("<Task xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\" />")]
    public void Read_command_of_unusable_xml_is_null(string xml) => Assert.Null(AutostartTask.ReadCommand(xml));
}
