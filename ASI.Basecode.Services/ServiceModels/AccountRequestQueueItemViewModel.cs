using System;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Display-only account-request data for the Management queue.</summary>
public sealed class AccountRequestQueueItemViewModel
{
    public int RequestId { get; init; }
    public string StudentNumber { get; init; }
    public string FullName { get; init; }
    public string Email { get; init; }
    public string Course { get; init; }
    public int YearLevel { get; init; }
    public string Section { get; init; }
    public string MobileNumber { get; init; }
    public string Status { get; init; }
    public DateTime RequestedAt { get; init; }
}
