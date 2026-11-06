using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

public sealed class AccountRequestQueueService(IAccountRequestRepository accountRequestRepository) : IAccountRequestQueueService
{
    public async Task<AccountRequestQueueViewModel> GetPageAsync(
        string status,
        int? pageNumber,
        string search = null,
        string sort = null,
        CancellationToken cancellationToken = default)
    {
        var statusCode = ParseStatus(status);
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var oldestFirst = string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase);
        var counts = await accountRequestRepository.GetStatusCountsAsync(normalizedSearch, cancellationToken);
        var totalCount = statusCode switch
        {
            AccountRequestStatusCode.Approved => counts.Approved,
            AccountRequestStatusCode.Rejected => counts.Rejected,
            AccountRequestStatusCode.Pending => counts.Pending,
            _ => counts.Total
        };

        var pageRequest = new PageRequest(pageNumber);
        var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
        var records = await accountRequestRepository.GetPageByStatusAsync(
            statusCode,
            normalizedSearch,
            oldestFirst,
            pageRequest.GetOffset(normalizedPage),
            PageRequest.FixedPageSize,
            cancellationToken);

        var items = records.Select(record => new AccountRequestQueueItemViewModel
        {
            RequestId = record.RequestId,
            StudentNumber = record.StudentNumber,
            FullName = record.FullName,
            Email = record.Email,
            Course = record.Course,
            YearLevel = record.YearLevel,
            Section = record.Section,
            MobileNumber = record.MobileNumber,
            Status = StatusText(record.Status),
            RequestedAt = record.RequestedAt
        }).ToList();

        return new AccountRequestQueueViewModel(
            new PagedResult<AccountRequestQueueItemViewModel>(items, normalizedPage, totalCount),
            StatusCodeText(statusCode),
            counts.Pending,
            counts.Approved,
            counts.Rejected,
            normalizedSearch,
            oldestFirst ? "oldest" : "newest");
    }

    private static AccountRequestStatusCode? ParseStatus(string status) => status?.Trim().ToLowerInvariant() switch
    {
        "approved" => AccountRequestStatusCode.Approved,
        "rejected" => AccountRequestStatusCode.Rejected,
        "all" => null,
        _ => AccountRequestStatusCode.Pending
    };

    private static string StatusCodeText(AccountRequestStatusCode? status) => status switch
    {
        AccountRequestStatusCode.Approved => "approved",
        AccountRequestStatusCode.Rejected => "rejected",
        AccountRequestStatusCode.Pending => "pending",
        _ => "all"
    };

    private static string StatusText(AccountRequestStatusCode status) => status switch
    {
        AccountRequestStatusCode.Approved => "Approved",
        AccountRequestStatusCode.Rejected => "Rejected",
        _ => "Pending"
    };
}
