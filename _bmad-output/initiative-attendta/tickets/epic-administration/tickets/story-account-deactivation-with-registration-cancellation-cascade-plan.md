---
title: 'Account deactivation with registration cancellation cascade and session refusal'
type: 'feature'
ticket: '4'
created: '2026-11-07'
status: 'in-review'
baseline_revision: 'e99a7b8b7bfc5caec1ac013147b9c5d293470c9d'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Administrator accounts console can preview a deactivation, but the confirm step only changes the demo store: the real account stays active, its upcoming registrations keep their seats, and a signed-in session survives.

**Approach:** Implement the real `IAccountAccessCommands.DeactivateAsync` behind the existing contract and MVC endpoint. One `ExecuteInTransactionAsync` takes the `attendta:last-admin` application lock before the `users` row lock when the target is an active Administrator, refuses the last active Administrator, calls `IRegistrationCancellationService.CancelUpcomingForUserAsync(AccountDeactivated)`, writes `users.is_active = 0` with `updated_by`/`updated_at`, and appends one `user.deactivated` Activity Log row. Session refusal on the next request stays in `OnValidatePrincipal`; this ticket only verifies it. Reactivation remains demo-only until 5.5.

## Boundaries & Constraints

**Always:** service-layer Administrator check from `ICurrentActor` plus the existing Administrator policy; trimmed reason of 1–1000 characters validated before state checks; app lock before the `users` row lock for an active Administrator; re-read after the row lock and refuse `Stale` when the target became an Administrator late or became inactive; count active Administrators after the lock; cancel through the existing `IRegistrationCancellationService` only; clear the cancel cascade's QR tokens through that service; one `user.deactivated` audit row with only the active flag; demo-on delegates deactivation to the real command and clears stale demo status; demo-off uses the real command with the standard unavailable reactivation.

**Never:** implement reactivation (5.5 owns it); change the dialog markup, the 5.3 preview, authentication, the schema, or the member Edit form; add a second cancellation rule; promote waitlisted members; write password, hash, token, path or whole-entity audit payloads; add a competing dialog.

</frozen-after-approval>

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output | Error Handling |
|---|---|---|---|
| Deactivate an officer | Active Officer with upcoming Pending/Confirmed registrations | `is_active = 0`, `updated_by`/`updated_at` set, registrations Cancelled with null QR token, seats released, one `user.deactivated` row | No error |
| Waitlist | Separate member Waitlisted on the same event | Still Waitlisted; no promotion | No error |
| Cancel audit | Each cancelled registration | One `registration.cancelled` row carrying `Cause=AccountDeactivated` | No error |
| Two Administrators, parallel | Both deactivated at once from separate connections | Exactly one commits; one active Administrator remains; one `user.deactivated` row | Loser gets `Stale` with "last active Administrator" |
| Last active Administrator | Only one active Administrator | No write, no audit row | `Stale` |
| Already inactive | Target `is_active = 0` | No write, no audit row | `Invalid` |
| Late Administrator | Target becomes an active Administrator after the pre-lock read | No late app lock, no write | `Stale` |
| Bad reason | Blank or over 1000 trimmed characters | No state check, no write | `Invalid` with field `reason` |
| Non-Administrator | Officer or anonymous caller | No write | `Forbidden` |
| Deactivated session | Signed-in target deactivated, next request | Login redirect | No error (existing `OnValidatePrincipal`) |
| Demo reactivation | Store entry says active, real row inactive, real deactivation posted | Real refusal; overlay cleared only after a successful real write | `Invalid` already inactive |

## Code Map

- `ASI.Basecode.Services/Services/AccountAccessService.cs` — new real command; copy the `AccountRoleService` transaction/lock/re-read/audit shape.
- `ASI.Basecode.Data/Repositories/AccountAdministrationRepository.cs`, `UserRepository.cs` — reuse `GetAccountAsync`, `AcquireLastAdministratorLockAsync`, `CountActiveAdministratorsAsync`, `LockUserAsync`.
- `ASI.Basecode.Services/Services/RegistrationCancellationService.cs` — reuse `CancelUpcomingForUserAsync`; do not duplicate its rules.
- `ASI.Basecode.Resources/Constants/AuditActions.cs`, `Messages/AuditDescriptions.cs`, `Messages/Audit.resx`, `Services/Services/ActivityLogWriter.cs` — add `user.deactivated` for `users`.
- `ASI.Basecode.WebApp/Startup.DI.cs` — register the concrete real service and `IAccountAccessCommands`.
- `ASI.Basecode.WebApp/DemoData/DemoAccountAccessCommands.cs` — delegate deactivation, keep demo reactivation.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs`, `DemoAccessRegistration.cs`, `DemoDataRegistration.cs`, `UnavailableAccessCommands.cs` — clear stale demo account status; register the demo wrapper last; drop the unavailable account registration when the switch is off.
- `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs`, `Views/AdminAccounts/_AccessDialog.cshtml` — unchanged; the existing POST and dialog already consume the contract.
- `tests/ASI.Basecode.IntegrationTests/AdminAccountDeactivationTests.cs` — new focused SQL/pipeline coverage.

## Tasks & Acceptance

**Execution:**
- [x] `AccountAccessService` with `DeactivateAsync` and the 5.5 placeholder `ReactivateAsync`.
- [x] `user.deactivated` action, description, resx template and entity-type guard.
- [x] `Startup.DI.cs` real registration.
- [x] Demo wrapper delegates deactivation, keeps demo reactivation, clears stale status; demo-off uses the real command.
- [x] Integration tests for the officer cascade, parallel last-administrator race, session refusal and refusals.
- [x] `demo-mock-register.md` M-14a set to removed; M-14b stays open.

**Acceptance Criteria:**
- Given an active Officer with upcoming registrations, when an Administrator deactivates the account with a reason, then the account is inactive, its upcoming registrations are cancelled with cleared tokens, waitlisted members are not promoted, and exactly one attributable `user.deactivated` row exists.
- Given two active Administrators deactivated in parallel, then exactly one succeeds, one remains active, and one `user.deactivated` row exists.
- Given a deactivated signed-in account, when it sends its next request, then it is redirected to sign in.
- Given a blank or overlong reason, an already-inactive account, the last active Administrator, or a non-Administrator caller, then nothing is written and no `user.deactivated` row exists.

## Verification

**Commands:**
- `dotnet build` — expected: 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass.
- With `ATTENDTA_TEST_SQL` set: `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter FullyQualifiedName~AdminAccountDeactivationTests` — expected: all pass.
- With `ATTENDTA_TEST_SQL` set: the full integration suite — expected: all pass.

**Manual checks:**
- With the demo switch on, deactivating an account now writes SQL and cancels its registrations; reactivating it still changes only the overlay until 5.5.

## Implementation Notes

Deactivation mirrors `AccountRoleService`: the pre-lock read only decides whether the application lock is needed, the `users` row lock is taken next, and the authoritative state and last-Administrator count are read after the lock. A late Administrator surfaces as `Stale` instead of taking the app lock out of order. The cancellation cascade is the existing `IRegistrationCancellationService`, so locks, QR clearing, waitlist behaviour and `registration.cancelled` audit rows are unchanged. The demo wrapper calls the real command first and clears the in-memory account status only after a successful write, so the overlay cannot hide the real inactive state. `ReactivateAsync` on the real service returns the standard unavailable refusal; the demo wrapper keeps the M-14b in-memory reactivation until 5.5.
