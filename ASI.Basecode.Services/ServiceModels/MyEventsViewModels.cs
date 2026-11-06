using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>The three sections of the member Participation History screen.</summary>
public enum MyEventsTab
{
    Registrations,
    Attendance,
    Certificates
}

/// <summary>
/// Host contract for the member Participation History screen. Each tab owns its own model so a
/// surface ticket changes only the tab it owns.
/// </summary>
public sealed class MyEventsViewModel
{
    public MyEventsTab ActiveTab { get; init; } = MyEventsTab.Registrations;
    public MyEventsRegistrationsTabViewModel Registrations { get; init; } = MyEventsRegistrationsTabViewModel.Empty();
    public MyEventsAttendanceTabViewModel Attendance { get; init; } = MyEventsAttendanceTabViewModel.Empty();
    public MyEventsCertificatesTabViewModel Certificates { get; init; } = MyEventsCertificatesTabViewModel.Empty();

    public static MyEventsViewModel Empty(MyEventsTab tab = MyEventsTab.Registrations) => new() { ActiveTab = tab };
}

/// <summary>My Registrations tab. Filled by the registrations surface ticket.</summary>
public sealed class MyEventsRegistrationsTabViewModel
{
    public IReadOnlyList<MyEventRegistrationCardViewModel> Cards { get; init; } = Array.Empty<MyEventRegistrationCardViewModel>();
    public int Count => Cards.Count;

    public static MyEventsRegistrationsTabViewModel Empty() => new();
}

/// <summary>One registration card. The pass slot is filled by the pass surface ticket.</summary>
public sealed class MyEventRegistrationCardViewModel
{
    public int RegistrationId { get; init; }
    public int EventId { get; init; }
    public string EventTitle { get; init; }
    public string TypeName { get; init; }
    public string VenueName { get; init; }
    public DateTime StartsAt { get; init; }
    public string StatusLabel { get; init; }
    public string StatusBadgeClass { get; init; }
    public PassViewModel Pass { get; init; } = PassViewModel.Empty();
}

/// <summary>Pass card slot. Renders nothing until the pass surface ticket supplies a pass.</summary>
public sealed class PassViewModel
{
    public int RegistrationId { get; init; }
    public string EventTitle { get; init; }
    public string HolderName { get; init; }
    public string StudentNumber { get; init; }
    public string GateCode { get; init; }
    public string QrImageUrl { get; init; }
    public string DownloadUrl { get; init; }

    /// <summary>True only when a scannable pass exists for the registration.</summary>
    public bool HasPass { get; init; }

    public static PassViewModel Empty() => new();
}

/// <summary>My Attendance tab. Filled by the attendance-history surface ticket.</summary>
public sealed class MyEventsAttendanceTabViewModel
{
    public IReadOnlyList<MyEventAttendanceRowViewModel> Rows { get; init; } = Array.Empty<MyEventAttendanceRowViewModel>();
    public int Count => Rows.Count;

    public static MyEventsAttendanceTabViewModel Empty() => new();
}

/// <summary>One attendance record in the member history.</summary>
public sealed class MyEventAttendanceRowViewModel
{
    public int AttendanceId { get; init; }
    public int EventId { get; init; }
    public string EventTitle { get; init; }
    public DateTime RecordedAt { get; init; }
    public string CheckInMethodLabel { get; init; }
    public bool IsInvalidated { get; init; }
    public string InvalidationReason { get; init; }
}

/// <summary>My Certificates tab. Filled by the certificate-history surface ticket.</summary>
public sealed class MyEventsCertificatesTabViewModel
{
    public IReadOnlyList<MyEventCertificateRowViewModel> Rows { get; init; } = Array.Empty<MyEventCertificateRowViewModel>();
    public int Count => Rows.Count;

    public static MyEventsCertificatesTabViewModel Empty() => new();
}

/// <summary>One issued certificate in the member history.</summary>
public sealed class MyEventCertificateRowViewModel
{
    public int CertificateId { get; init; }
    public int EventId { get; init; }
    public string EventTitle { get; init; }
    public string CertificateNumber { get; init; }
    public DateTime IssuedAt { get; init; }
    public string DownloadUrl { get; init; }
}
