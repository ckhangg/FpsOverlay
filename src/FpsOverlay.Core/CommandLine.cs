using System.Globalization;
using FpsOverlay.Core.Autostart;

namespace FpsOverlay.Core;

/// <summary>Tham số dòng lệnh của app (không phân biệt hoa thường).</summary>
public sealed record CommandLine(bool Uninstall, bool NoInstall, bool Autostart, int? UpdateAfterPid, bool PawnIoOffered)
{
    public const string UninstallArgument = "--uninstall";
    public const string NoInstallArgument = "--no-install";
    public const string UpdateAfterArgument = "--update-after";
    /// <summary>Bản setup đã hỏi người dùng về PawnIO: bản cài nhận cờ này và nhớ lại.</summary>
    public const string PawnIoOfferedArgument = "--pawnio-offered";

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        bool Has(string name) => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        int? pid = null;
        for (var i = 0; i < args.Count - 1; i++)
            if (string.Equals(args[i], UpdateAfterArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0)
                    pid = n;
                break;
            }

        return new CommandLine(Has(UninstallArgument), Has(NoInstallArgument), Has(AutostartTask.AutostartArgument),
            pid, Has(PawnIoOfferedArgument));
    }
}
