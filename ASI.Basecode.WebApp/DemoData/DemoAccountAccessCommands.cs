using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Demo account access commands (register M-14b). Deactivation is already the real command (M-14a was
/// removed by 5.4): this wrapper delegates to <see cref="AccountAccessService"/> and clears any demo
/// status entry an earlier demo reactivation left behind. Reactivation remains in-memory until 5.5
/// replaces it (register rules 4 and 8).
/// </summary>
public sealed class DemoAccountAccessCommands : IAccountAccessCommands
{
    private readonly AccountAccessService _accountAccessService;
    private readonly IAccountAdministrationRepository _accounts;
    private readonly DemoStore _store;
    private readonly TimeProvider _timeProvider;

    public DemoAccountAccessCommands(
        AccountAccessService accountAccessService,
        IAccountAdministrationRepository accounts,
        DemoStore store,
        TimeProvider timeProvider)
    {
        _accountAccessService = accountAccessService;
        _accounts = accounts;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task DeactivateAsync(int accountId, string reason, CancellationToken cancellationToken = default)
    {
        await _accountAccessService.DeactivateAsync(accountId, reason, cancellationToken);
        _store.ClearAccountStatus(accountId);
    }

    public async Task ReactivateAsync(int accountId, string reason, CancellationToken cancellationToken = default)
    {
        RequireReason(reason);
        var account = await RequireAccountAsync(accountId, cancellationToken);
        if (IsActive(account))
        {
            throw new DomainException(OutcomeCode.Invalid, "This account is already active.");
        }

        _store.SetAccountActive(account.AccountId, true, reason, _timeProvider.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Refuses a blank reason before any state check, so the reason refusal wins over the
    /// already-active and already-inactive refusals.
    /// </summary>
    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(OutcomeCode.Invalid, "A reason is required.", "reason");
        }
    }

    /// <summary>
    /// The state the screen shows: the real row, overlaid by the store when a demo command already
    /// changed it. The store never writes SQL, so the real row alone would refuse the second step.
    /// </summary>
    private bool IsActive(AccountListItem account) =>
        _store.TryGetAccountStatus(account.AccountId, out var status) ? status.IsActive : account.IsActive;

    private async Task<AccountListItem> RequireAccountAsync(int accountId, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            throw new DomainException(OutcomeCode.NotFound, "Account not found.");
        }

        return account;
    }
}
