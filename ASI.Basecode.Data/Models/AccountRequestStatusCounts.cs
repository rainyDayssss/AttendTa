namespace ASI.Basecode.Data.Models;

/// <summary>Per-status totals for the Management account-request queue filters.</summary>
public sealed record AccountRequestStatusCounts(int Pending, int Approved, int Rejected)
{
    public int Total => Pending + Approved + Rejected;
}
