using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

/// <summary>Safe account-request fields for queue display. Deliberately excludes password hashes.</summary>
public sealed class AccountRequestQueueItem
{
    public int RequestId { get; set; }
    public string StudentNumber { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Course { get; set; }
    public int YearLevel { get; set; }
    public string Section { get; set; }
    public string MobileNumber { get; set; }
    public AccountRequestStatusCode Status { get; set; }
    public DateTime RequestedAt { get; set; }
}
