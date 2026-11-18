namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// The outcome of one record inside a bulk approval. Every selected id produces exactly one row,
/// whether it was approved or refused, so the caller can report each person honestly.
/// </summary>
public sealed class AccountRequestApprovalOutcome
{
    public AccountRequestApprovalOutcome(int requestId, string fullName, string studentNumber, bool succeeded, string message)
    {
        RequestId = requestId;
        FullName = fullName;
        StudentNumber = studentNumber;
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>The account request this row describes.</summary>
    public int RequestId { get; }

    /// <summary>The server-resolved applicant name, never a client-supplied label.</summary>
    public string FullName { get; }

    /// <summary>
    /// The server-resolved student number, so two applicants who share a name stay distinguishable
    /// in the result panel. Null when the request could not be read.
    /// </summary>
    public string StudentNumber { get; }

    /// <summary>Whether the record was actually approved and committed.</summary>
    public bool Succeeded { get; }

    /// <summary>A short result message, or the refusal reason.</summary>
    public string Message { get; }
}
