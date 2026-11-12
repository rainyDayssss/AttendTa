using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// The single rule for the member-facing check-in method label. The method is not a status, so
/// there is no badge class: the attendance ledger shows the label as plain cell text.
/// </summary>
public static class CheckInMethodDisplay
{
    public static string Label(CheckInMethodCode method) => method switch
    {
        CheckInMethodCode.QrCode => "QR code",
        CheckInMethodCode.ManualSearch => "Manual search",
        CheckInMethodCode.StudentNumber => "Student number",
        _ => "Check-in"
    };
}
