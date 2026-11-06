using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>The two sections of the officer certificate ledger.</summary>
public enum CertificateLedgerTab
{
    Eligibility,
    Issued
}

/// <summary>Officer certificate ledger: shared selector, tab counts and the active tab's rows.</summary>
public sealed class CertificateLedgerViewModel
{
    public EventSelectorViewModel Selector { get; init; } = EventSelectorViewModel.Empty();
    public CertificateLedgerTab ActiveTab { get; init; } = CertificateLedgerTab.Eligibility;
    public int EligibilityCount { get; init; }
    public int IssuedCount { get; init; }
    public IReadOnlyList<CertificateLedgerRowViewModel> Rows { get; init; } = Array.Empty<CertificateLedgerRowViewModel>();

    public static CertificateLedgerViewModel Empty() => new();
}

/// <summary>One certificate eligibility or issued-registry row.</summary>
public sealed class CertificateLedgerRowViewModel
{
    public int RegistrationId { get; init; }
    public int MemberId { get; init; }
    public string MemberName { get; init; }
    public string StudentNumber { get; init; }
    public string EventTitle { get; init; }
    public DateTime? AttendedAt { get; init; }
    public CertificateStatusCode? Status { get; init; }
    public string StatusLabel { get; init; }
    public string StatusBadgeClass { get; init; }
    public string CertificateNumber { get; init; }
    public DateTime? IssuedAt { get; init; }
    public string ViewerUrl { get; init; }
}

/// <summary>Certificate viewer shell. The PDF link stays empty until the ledger ticket supplies it.</summary>
public sealed class CertificateViewerViewModel
{
    public int CertificateId { get; init; }
    public string EventTitle { get; init; }
    public string MemberName { get; init; }
    public string CertificateNumber { get; init; }
    public string PdfUrl { get; init; }

    public static CertificateViewerViewModel Empty(int certificateId = 0) => new() { CertificateId = certificateId };
}
