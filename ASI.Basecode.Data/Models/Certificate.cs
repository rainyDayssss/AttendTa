using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

public class Certificate
{
    public int CertificateId { get; set; }
    public int RegistrationId { get; set; }
    public CertificateStatusCode CertificateStatusId { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string ReviewNotes { get; set; }
    public string CertificateNumber { get; set; }
    public string RecipientNameSnapshot { get; set; }
    public string EventTitleSnapshot { get; set; }
    public DateTime? EventStartsAtSnapshot { get; set; }
    public string IssuerNameSnapshot { get; set; }
    public string FilePath { get; set; }
    public int? IssuedBy { get; set; }
    public DateTime? IssuedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
