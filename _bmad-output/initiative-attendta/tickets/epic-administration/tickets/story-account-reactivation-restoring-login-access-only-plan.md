---
title: 'Account reactivation restoring login access only'
type: 'feature'
ticket: '5'
created: '2026-11-07'
status: 'built'
baseline_revision: 'b4394748a41f0f6695499f85ef0fb8a20cf4dbad'
route: 'full'
route_source: 'auto'
risk: 'low'
review: ''
review_source: ''
lenses_ran: ['quick']
review_loop_iteration: 0
followup_review_recommended: false
context:
  - 'AGENTS.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** The Administrator accounts console can preview and perform a real account deactivation, but the reactivate action still only changes the in-memory demo store (register M-14b): the real `users.is_active` row stays false, so the person cannot sign in again, and no `user.reactivated` audit row exists.

**Approach:** Implement the real `IAccountAccessCommands.ReactivateAsync` behind the existing contract and MVC endpoint. One `ExecuteInTransactionAsync` locks the `users` row, refuses an already-active account, sets `users.is_active = 1` with `updated_by`/`updated_at`, and appends one `user.reactivated` Activity Log row. It restores login access only — no registration, seat, QR token or certificate is touched — and delete the M-14b demo provider and its account-status overlay.

## Boundaries & Constraints

**Always:** service-layer Administrator check from `ICurrentActor` plus the existing Administrator policy; trimmed reason of 1–1000 characters validated before any state check; `users` row lock inside the transaction; audit the active-flag change only; leave cancelled Registrations, seats, QR tokens and issued Certificates untouched; keep the existing dialog and POST endpoint unchanged.

**Never:** re-cancel or restore registrations, promote waitlisted members, reissue certificates, take the last-administrator application lock (reactivation cannot remove an Administrator), change authentication, the schema, or the dialog markup; add a competing dialog; add broad automation beyond the one focused smoke (8.5 owns broad cases).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reactivate a deactivated account | `is_active = 0`, valid reason | `is_active = 1`, `updated_by`/`updated_at` set, one `user.reactivated` row with the trimmed reason and `IsActive` false→true | No error |
| Cancelled registrations | Target has Cancelled registrations with null QR tokens | Rows unchanged; no seat or token restored; waitlisted members not promoted | No error |
| Issued certificate | Target has a pre-existing issued certificate row | Row unchanged | No error |
| Already active | `is_active = 1` | No write, no audit row | `Invalid` "This account is already active." |
| Missing account | Unknown account id | No write | `NotFound` "Account not found." |
| Bad reason | Blank or over 1000 trimmed characters | No state check, no write | `Invalid` with field `reason` |
| Non-Administrator | Officer or anonymous caller | No write | `Forbidden` |
| Demo switch on | Real deactivation then reactivation posted | Real SQL write; no demo overlay masks the state | No error |

</intent-contract>

## Code Map

- `ASI.Basecode.Services/Services/AccountAccessService.cs` — real command; mirror `DeactivateAsync` minus the app lock and cancellation cascade; reuse `LockUserAsync`, `RequiredReason`, `RequireAdministratorActor`, `ActivationAuditValues`.
- `ASI.Basecode.Resources/Constants/AuditActions.cs` — add `UserReactivated = "user.reactivated"`.
- `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `Messages/Audit.resx` — map the new action to a template (`User account reactivated.`).
- `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — guard `user.reactivated` to entity type `users`, as `user.deactivated` does.
- `ASI.Basecode.Services/Interfaces/IAccountAccessCommands.cs` — doc comment now describes both commands as real.
- `ASI.Basecode.WebApp/DemoData/DemoAccountAccessCommands.cs` — delete (M-14b only).
- `ASI.Basecode.WebApp/DemoData/DemoAccessRegistration.cs` — drop the `IAccountAccessCommands` line; membership overlay registration stays.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — delete `TryGetAccountStatus`, `SetAccountActive`, `ClearAccountStatus`, `_accounts`, `DemoAccountStatus`.
- `ASI.Basecode.WebApp/DemoData/IAccessStateOverlay.cs`, `DemoAccessStateOverlay.cs`, `NoOpAccessStateOverlay.cs` — delete the account overloads; membership overloads stay.
- `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs` — stop applying the overlay to the account list and account dialog; remove the now-unused dependency.
- `ASI.Basecode.WebApp/DemoData/UnavailableAccessCommands.cs` — drop `IAccountAccessCommands` (the real service serves account access when the switch is off).
- `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-14b to `removed`.
- `tests/ASI.Basecode.IntegrationTests/AdminAccountReactivationTests.cs` — new focused SQL/pipeline coverage, following `AdminAccountDeactivationTests`.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Resources/Constants/AuditActions.cs` — add `UserReactivated` — the audit action the ticket names.
- [x] `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `Messages/Audit.resx` — add the `UserReactivated` template — `ActivityLogWriter` requires a description for every action.
- [x] `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — guard `user.reactivated` to `users` — mirrors `user.deactivated`.
- [x] `ASI.Basecode.Services/Services/AccountAccessService.cs` — implement `ReactivateAsync` in one transaction — the real command.
- [x] `ASI.Basecode.Services/Interfaces/IAccountAccessCommands.cs` — update the contract comment — remove the 5.5 placeholder wording.
- [x] `ASI.Basecode.WebApp/DemoData/DemoAccountAccessCommands.cs` — delete the file — M-14b removal.
- [x] `ASI.Basecode.WebApp/DemoData/DemoAccessRegistration.cs` — drop the account command registration — the real DI registration must win.
- [x] `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — delete the account-status store — no writer remains after M-14b.
- [x] `ASI.Basecode.WebApp/DemoData/IAccessStateOverlay.cs`, `DemoAccessStateOverlay.cs`, `NoOpAccessStateOverlay.cs` — delete account overloads — membership overlay stays for M-15a/b.
- [x] `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs` — remove account overlay calls and dependency — the real row is authoritative.
- [x] `ASI.Basecode.WebApp/DemoData/UnavailableAccessCommands.cs` — drop `IAccountAccessCommands` — account access always uses the real service.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-14b to `removed` — register rule 11.
- [x] `tests/ASI.Basecode.IntegrationTests/AdminAccountReactivationTests.cs` — add the focused reactivate/login smoke — development check the ticket names.

**Acceptance Criteria:**
- Given a deactivated account with a cancelled registration and a seeded issued certificate, when an Administrator posts a reactivation with a reason, then the account is active with `updated_by`/`updated_at` set, the cancelled registration stays Cancelled with a null QR token, the certificate row is unchanged, and exactly one `user.reactivated` row carries the trimmed reason and the false→true active flag.
- Given the reactivated account, when it signs in, then it reaches the authenticated member surface instead of the sign-in redirect.
- Given a blank or overlong reason, an already-active account, an unknown account, or a non-Administrator caller, then nothing is written and no `user.reactivated` row exists.
- Given the demo switch on, when an account is reactivated, then the real service writes SQL and no demo overlay masks the new state.

## Implementation Notes

`ReactivateAsync` mirrors `DeactivateAsync` minus the parts that only guard removal: the reason is
validated before the transaction, then one `ExecuteInTransactionAsync` locks the `users` row with the
existing `IUserRepository.LockUserAsync`, refuses an already-active account, writes `is_active = 1`
with `updated_by`/`updated_at`, and appends one `user.reactivated` row using the existing
`ActivationAuditValues` record (`false` → `true`). No application lock, no registration read and no
certificate write, so cancelled registrations, seats, QR tokens and issued certificates are untouched
by construction. The audit description lives in `Audit.resx` and `ActivityLogWriter` now guards the
new action to entity type `users`, matching `user.deactivated`.

M-14b is gone: `DemoAccountAccessCommands` is deleted, `DemoAccessRegistration` registers only the
membership provider, `DemoStore` no longer holds account status, and `IAccessStateOverlay` /
`DemoAccessStateOverlay` / `NoOpAccessStateOverlay` keep only the membership overloads.
`AdminAccountsController` no longer injects the overlay, so the accounts list and access dialog read
the authoritative `users` row in both switch positions. `UnavailableAccessCommands` now implements
`IMembershipAccessCommands` only. The demo register marks M-14b `removed` and its detail section
describes the real account commands.

`AdminAccountReactivationTests` covers the four acceptance paths: reactivation with a cancelled
registration and a seeded issued certificate (both unchanged), sign-in after reactivation reaching
`/MemberEvents`, the blank/overlong/already-active/unknown/non-Administrator refusals, and the demo
switch on (asserts `IAccountAccessCommands` resolves to `AccountAccessService` and the accounts page
renders the real active row).

## Plan Change Log

## Review Triage Log

### 2026-11-07 — Review pass
- verdicts: 3 findings — high 0, medium 0, low 3, false 0, maybe-false 0
- findings:
  - `[low]` `[patch]` demo-mock-register "Known limits of the demo" still said demo reactivation does not restore sign-in and changes only the overlay — verified at demo-mock-register.md:459–464; both bullets rewritten to the real command behaviour, membership-overlay limit kept.
  - `[low]` `[patch]` stale account-overlay comments on the two account view models — verified at AccessDialogViewModels.cs:26 and AccountSummaryViewModel.cs:12; both now describe the real `users.is_active` state, membership comment untouched.
  - `[low]` `[patch]` DemoDataRegistration class comment said only the real account deactivation stays in place — verified at DemoDataRegistration.cs:10–11; updated to both account commands real and membership-only demo providers.

## Design Notes

Reactivation is deactivation's inverse without the parts that only guard removal: no `attendta:last-admin` application lock, no cancellation cascade, no registration reads. The reason is validated before the row is read, so a bad reason never locks or writes. The audit payload reuses the existing `ActivationAuditValues(bool)` record, so the Activity Log shows only the active flag and never a password, token or path. The demo overlay is removed for accounts because, once the real command writes SQL, an overlay entry could only mask the authoritative row.

## Verification

**Commands:**
- `dotnet build` — expected: 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass.
- With `ATTENDTA_TEST_SQL` set: `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter FullyQualifiedName~AdminAccountReactivationTests` — expected: all pass.

## Auto Run Result

**Summary:** `IAccountAccessCommands.ReactivateAsync` is now the real transactional command: Administrator check, trimmed reason validation before any state read, one `ExecuteInTransactionAsync` that locks the `users` row, refuses an already-active account, sets `is_active = 1` with `updated_by`/`updated_at`, and appends one `user.reactivated` Activity Log row. It restores login access only. The M-14b demo provider, its account-status store and the account overlay were removed; membership demo rows remain.

**Files changed:**
- `ASI.Basecode.Services/Services/AccountAccessService.cs` — real `ReactivateAsync`.
- `ASI.Basecode.Services/Interfaces/IAccountAccessCommands.cs` — contract comment updated.
- `ASI.Basecode.Resources/Constants/AuditActions.cs` — `user.reactivated` action.
- `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `Messages/Audit.resx` — action template.
- `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — entity-type guard for the new action.
- `ASI.Basecode.Services/ServiceModels/AccessDialogViewModels.cs`, `AccountSummaryViewModel.cs` — stale overlay comments corrected.
- `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs` — account overlay removed; real row is authoritative.
- `ASI.Basecode.WebApp/DemoData/DemoAccountAccessCommands.cs` — deleted.
- `ASI.Basecode.WebApp/DemoData/DemoAccessRegistration.cs`, `DemoDataRegistration.cs`, `DemoStore.cs`, `IAccessStateOverlay.cs`, `DemoAccessStateOverlay.cs`, `NoOpAccessStateOverlay.cs`, `UnavailableAccessCommands.cs` — M-14b demo seam removed.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — M-14b `removed`; stale limits and detail sections corrected.
- `tests/ASI.Basecode.IntegrationTests/AdminAccountReactivationTests.cs` — focused reactivate/login, refusal and demo-switch coverage.

**Review findings breakdown:** 3 findings, all `low` and patched (stale demo-register limits, two stale account view-model comments, stale demo-registration class comment). 0 deferred, 0 rejected.

**Follow-up review recommendation:** false — first pass patched no `high` and fewer than two `medium` entries.

**Verification performed:**
- `dotnet build` — 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — 281/281 passed.
- With `ATTENDTA_TEST_SQL` set: `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter FullyQualifiedName~AdminAccountReactivationTests` — 4/4 passed.

**Residual risks:** none identified; the change adds no schema, no view and no new public surface.
