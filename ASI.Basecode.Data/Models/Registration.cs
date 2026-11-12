using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

public class Registration
{
    public int RegistrationId { get; set; }
    public int MemberId { get; set; }
    public int EventId { get; set; }
    public RegistrationStatusCode RegistrationStatusId { get; set; }
    public string CourseSnapshot { get; set; }
    public int YearLevelSnapshot { get; set; }
    public string SectionSnapshot { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string ReviewNotes { get; set; }
    public int? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string QrToken { get; set; }
    public int? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string CancellationReason { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
