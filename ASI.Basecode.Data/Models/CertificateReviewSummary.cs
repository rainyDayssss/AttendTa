namespace ASI.Basecode.Data.Models;

/// <summary>
/// Display-only identity for a certificate review that a bulk approval is about to decide.
/// Deliberately excludes the review state and every decision field: the result panel names people,
/// it never trusts a posted label.
/// </summary>
public sealed class CertificateReviewSummary
{
    public int RegistrationId { get; set; }
    public string MemberName { get; set; }
    public string StudentNumber { get; set; }
}
