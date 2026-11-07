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

    public static MyEventsViewModel FromRegistrations(PagedResult<MyEventRegistrationCardViewModel> page) => new()
    {
        ActiveTab = MyEventsTab.Registrations,
        Registrations = MyEventsRegistrationsTabViewModel.From(page)
    };

    public static MyEventsViewModel FromAttendance(PagedResult<MyEventAttendanceRowViewModel> page) => new()
    {
        ActiveTab = MyEventsTab.Attendance,
        Attendance = MyEventsAttendanceTabViewModel.From(page)
    };

    public static MyEventsViewModel FromCertificates(PagedResult<MyEventCertificateRowViewModel> page) => new()
    {
        ActiveTab = MyEventsTab.Certificates,
        Certificates = MyEventsCertificatesTabViewModel.From(page)
    };
}

/// <summary>
/// Shared paging metadata for one Participation History tab. Each tab is its own page of records,
/// so the host renders one result count and one pager from the active tab without knowing its row
/// type. A tab with no records reports page 1 of 0 pages, which keeps <c>_Pager</c> hidden.
/// </summary>
public abstract class MyEventsTabPage : IPagedResult
{
    protected MyEventsTabPage()
    {
        PageSize = PageRequest.FixedPageSize;
    }

    protected MyEventsTabPage(int pageNumber, int totalCount)
    {
        PageSize = PageRequest.FixedPageSize;
        TotalCount = Math.Max(0, totalCount);
        TotalPages = TotalCount == 0 ? 0 : (int)(((long)TotalCount + PageSize - 1) / PageSize);
        PageNumber = TotalPages == 0 ? 1 : Math.Clamp(pageNumber, 1, TotalPages);
    }

    public int PageNumber { get; } = 1;
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages { get; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int FirstItemNumber => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int LastItemNumber => TotalCount == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalCount);
}

/// <summary>My Registrations tab. Filled by the registrations surface ticket.</summary>
public sealed class MyEventsRegistrationsTabViewModel : MyEventsTabPage
{
    public MyEventsRegistrationsTabViewModel()
    {
    }

    private MyEventsRegistrationsTabViewModel(
        IReadOnlyList<MyEventRegistrationCardViewModel> cards,
        int pageNumber,
        int totalCount)
        : base(pageNumber, totalCount)
    {
        Cards = cards;
    }

    public IReadOnlyList<MyEventRegistrationCardViewModel> Cards { get; init; } = Array.Empty<MyEventRegistrationCardViewModel>();
    public int Count => Cards.Count;

    public static MyEventsRegistrationsTabViewModel Empty() => new();

    public static MyEventsRegistrationsTabViewModel From(PagedResult<MyEventRegistrationCardViewModel> page) =>
        page is null
            ? Empty()
            : new MyEventsRegistrationsTabViewModel(page.Items, page.PageNumber, page.TotalCount);
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

    /// <summary>
    /// True only when the member event detail page can serve the event. That page returns
    /// <c>404</c> for an event that is not published, so a completed or cancelled event gets no link.
    /// </summary>
    public bool CanViewEvent { get; init; }

    public PassViewModel Pass { get; init; } = PassViewModel.Empty();
}

/// <summary>
/// Pass card. The service fills the event metadata, holder identity and the links the server
/// renders; the QR image and its download are authorized GETs, so the token never reaches the view.
/// </summary>
public sealed class PassViewModel
{
    public int RegistrationId { get; init; }
    public int EventId { get; init; }
    public string EventTitle { get; init; }
    public string EventTypeName { get; init; }
    public string VenueName { get; init; }
    public DateTime StartsAt { get; init; }
    public string HolderName { get; init; }
    public string StudentNumber { get; init; }
    public string StatusLabel { get; init; }

    /// <summary>The visual event accent class for the card's top rule. Decorative only.</summary>
    public string AccentClass { get; init; }

    public string GateCode { get; init; }

    /// <summary>The authorized URL of the server-rendered QR PNG.</summary>
    public string QrImageUrl { get; init; }

    /// <summary>The authorized URL that streams the same PNG as a download.</summary>
    public string DownloadUrl { get; init; }

    /// <summary>True only when a scannable pass exists for the registration.</summary>
    public bool HasPass { get; init; }

    public static PassViewModel Empty() => new();
}

/// <summary>
/// The authorized result of one pass request. It carries the token the PNG encodes and the file
/// name the download uses; the WebApp renders the bytes, so Services never depends on an imaging
/// library and the token never reaches a view or a log.
/// </summary>
public sealed record PassDownload(string QrToken, string FileName);

/// <summary>My Attendance tab. Filled by the attendance-history surface ticket.</summary>
public sealed class MyEventsAttendanceTabViewModel : MyEventsTabPage
{
    public MyEventsAttendanceTabViewModel()
    {
    }

    private MyEventsAttendanceTabViewModel(
        IReadOnlyList<MyEventAttendanceRowViewModel> rows,
        int pageNumber,
        int totalCount)
        : base(pageNumber, totalCount)
    {
        Rows = rows;
    }

    public IReadOnlyList<MyEventAttendanceRowViewModel> Rows { get; init; } = Array.Empty<MyEventAttendanceRowViewModel>();
    public int Count => Rows.Count;

    public static MyEventsAttendanceTabViewModel Empty() => new();

    public static MyEventsAttendanceTabViewModel From(PagedResult<MyEventAttendanceRowViewModel> page) =>
        page is null
            ? Empty()
            : new MyEventsAttendanceTabViewModel(page.Items, page.PageNumber, page.TotalCount);
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
public sealed class MyEventsCertificatesTabViewModel : MyEventsTabPage
{
    public MyEventsCertificatesTabViewModel()
    {
    }

    private MyEventsCertificatesTabViewModel(
        IReadOnlyList<MyEventCertificateRowViewModel> rows,
        int pageNumber,
        int totalCount)
        : base(pageNumber, totalCount)
    {
        Rows = rows;
    }

    public IReadOnlyList<MyEventCertificateRowViewModel> Rows { get; init; } = Array.Empty<MyEventCertificateRowViewModel>();
    public int Count => Rows.Count;

    public static MyEventsCertificatesTabViewModel Empty() => new();

    public static MyEventsCertificatesTabViewModel From(PagedResult<MyEventCertificateRowViewModel> page) =>
        page is null
            ? Empty()
            : new MyEventsCertificatesTabViewModel(page.Items, page.PageNumber, page.TotalCount);
}

public enum MyCertificateState { Unavailable, Pending, Eligible, Declined, Issued }

/// <summary>One certificate review or issued document in the member history.</summary>
public sealed class MyEventCertificateRowViewModel
{
    public int CertificateId { get; init; }
    public int EventId { get; init; }
    public string EventTitle { get; init; }
    public DateTime StartsAt { get; init; }
    public MyCertificateState State { get; init; }
    public string Reason { get; init; }
    public bool HasDocument { get; init; }
    public string CertificateNumber { get; init; }
    public DateTime? IssuedAt { get; init; }
    public string DownloadUrl { get; init; }
}
