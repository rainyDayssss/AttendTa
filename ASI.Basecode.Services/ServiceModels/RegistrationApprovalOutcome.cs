namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// What actually happened to one registration inside a bulk approval. A successful approval is not
/// a promise of a seat, so Confirmed and Waitlisted are separate results rather than one success.
/// </summary>
public enum RegistrationApprovalResult
{
    /// <summary>The registration was approved and holds a confirmed seat.</summary>
    Confirmed,

    /// <summary>The registration was approved but joined the waitlist.</summary>
    Waitlisted,

    /// <summary>The registration was refused and left unchanged.</summary>
    Refused
}

/// <summary>
/// The outcome of one registration inside a bulk approval. Every distinct posted id produces
/// exactly one row, whether it was confirmed, waitlisted or refused, so the caller can report each
/// participant honestly. The name, student number and event title are resolved on the server.
/// </summary>
public sealed class RegistrationApprovalOutcome
{
    public RegistrationApprovalOutcome(int registrationId, string memberName, string studentNumber, string eventTitle,
        RegistrationApprovalResult result, string message)
    {
        RegistrationId = registrationId;
        MemberName = memberName;
        StudentNumber = studentNumber;
        EventTitle = eventTitle;
        Result = result;
        Message = message;
    }

    /// <summary>The registration this row describes.</summary>
    public int RegistrationId { get; }

    /// <summary>The server-resolved participant name, never a client-supplied label.</summary>
    public string MemberName { get; }

    /// <summary>
    /// The server-resolved student number, so two participants who share a name stay
    /// distinguishable in the result panel. Null when the registration could not be read.
    /// </summary>
    public string StudentNumber { get; }

    /// <summary>The server-resolved event title, so a batch that spans events stays legible.</summary>
    public string EventTitle { get; }

    /// <summary>Confirmed, Waitlisted or Refused.</summary>
    public RegistrationApprovalResult Result { get; }

    /// <summary>A short result message, or the refusal reason.</summary>
    public string Message { get; }
}
