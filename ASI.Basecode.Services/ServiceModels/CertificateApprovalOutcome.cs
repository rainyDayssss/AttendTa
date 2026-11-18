namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// The outcome of one certificate review inside a bulk approval. Every selected id produces exactly
/// one row, whether it was approved or refused, so the caller can report each member honestly.
/// </summary>
public sealed class CertificateApprovalOutcome
{
    public CertificateApprovalOutcome(int registrationId, string memberName, string studentNumber, bool succeeded, string message)
    {
        RegistrationId = registrationId;
        MemberName = memberName;
        StudentNumber = studentNumber;
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>The registration whose certificate review this row describes.</summary>
    public int RegistrationId { get; }

    /// <summary>The server-resolved member name, never a client-supplied label.</summary>
    public string MemberName { get; }

    /// <summary>
    /// The server-resolved student number, so two members who share a name stay distinguishable in
    /// the result panel. Null when the registration could not be read.
    /// </summary>
    public string StudentNumber { get; }

    /// <summary>Whether the review was actually approved and committed.</summary>
    public bool Succeeded { get; }

    /// <summary>A short result message, or the refusal reason.</summary>
    public string Message { get; }
}
