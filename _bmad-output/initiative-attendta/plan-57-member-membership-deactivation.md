---
title: 'Member membership deactivation with preview, reason and cancellation cascade'
type: 'feature'
ticket: '6'
created: '2026-11-07'
status: 'built'
baseline_revision: '0a61d4e35f71c9c2ec15f538b876bfbee0bba1a2'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
followup_review_recommended: false
context:
  - 'AGENTS.md'
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** The Members list membership dialog still posts to the demo command (register M-15a): the real `members.membership_status_id` row never changes, the member's upcoming registrations are never cancelled, and no `member.membership_deactivated` audit row exists.

**Approach:** Implement the real `IMembershipAccessCommands.DeactivateAsync` behind the existing contract and `MemberController` POST. One `ExecuteInTransactionAsync` resolves the member's linked user, locks the `users` row, sets `members.membership_status_id` to inactive, calls `IRegistrationCancellationService.CancelUpcomingForUserAsync(userId, MembershipDeactivated, reason)`, and appends one `member.membership_deactivated` Activity Log row. No app lock, no `users.is_active` change, no dialog markup change. Delete the M-15a demo path while M-15b (reactivation) stays demo.

## Boundaries & Constraints

**Always:** Management-policy caller checked in the service from `ICurrentActor` (officer or admin); trimmed reason of 1–1000 characters validated before any state read; `users` row lock inside the transaction before the member row is read for the write; cancellation through the existing `IRegistrationCancellationService` (never a new query); one audit row with old/new membership status; keep the existing dialog, preview partial and POST endpoints unchanged; leave `users.is_active` untouched.

**Never:** take the `attendta:last-admin` application lock; set `users.is_active`; cancel or restore registrations outside the shared service; promote waitlisted members; reissue or touch certificates; change the schema, EF mappings, authentication or Razor markup; add a competing dialog or a second preview query; implement membership reactivation (5.7); add broad automation beyond the focused smoke (8.5 owns broad cases).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Deactivate active membership | Active member, valid reason, two upcoming registrations | Status inactive, both registrations Cancelled with null QR tokens, one audit row, `users.is_active` still true, sign-in still works | No error |
| Preview | Management opens the dialog on an active member | Dialog lists that member's upcoming registrations from the real 5.3 preview | No error |
| Blank or overlong reason | `"   "` or 1001 trimmed characters | No write, no audit row | `Invalid` with field `reason` |
| Already inactive | `membership_status_id = inactive` | No write, no audit row | `Invalid` "This membership is already inactive." |
| Unknown member | Unknown member id | No write | `NotFound` "Member not found." |
| Non-Management caller | Member role or anonymous | No write | `Forbidden` "Access denied." |
| Demo switch on | Real deactivation posted | Real SQL write, no overlay masks the inactive status | No error |
| Demo reactivation then real deactivation | Store says active, real row active | Real deactivation wins; store entry cleared | No error |

</intent-contract>

## Code Map

- `ASI.Basecode.Services/Services/AccountAccessService.cs` — the deactivation/reason/audit/transaction pattern to mirror; drop the app lock and the `users.is_active` write.
- `ASI.Basecode.Services/Services/MemberService.cs:129-195` — the member write pattern: `GetMemberRecord` → `LockUserAsync(memberRecord.UserId)` → `GetMember` → mutate `UpdatedBy`/`UpdatedAt` → `UpdateMemberAsync` → save → audit → save.
- `ASI.Basecode.Services/Services/MemberService.cs:197-222` — `RequireManagementActor` (officer or admin) to copy for the service check.
- `ASI.Basecode.Services/Services/RegistrationCancellationService.cs:36-52` — `CancelUpcomingForUserAsync`; already requires an open transaction and an authenticated actor. Do not change.
- `ASI.Basecode.Services/ServiceModels/CancellationCause.cs` — `MembershipDeactivated` already exists.
- `ASI.Basecode.Data/Interfaces/IMemberRepository.cs` — `GetMemberRecord(int)` (carries `UserId`), `GetMember(int)` (tracked), `UpdateMemberAsync`.
- `ASI.Basecode.Data/Interfaces/IUserRepository.cs:22` — `LockUserAsync(int userId)`.
- `ASI.Basecode.Resources/Constants/AuditActions.cs` — add `MemberMembershipDeactivated = "member.membership_deactivated"`.
- `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `Messages/Audit.resx` — action template; `ActivityLogWriter` throws without one.
- `ASI.Basecode.Services/Services/ActivityLogWriter.cs:33` — extend the `members` entity-type guard with the new action.
- `ASI.Basecode.WebApp/Startup.DI.cs:44-46` — register `MembershipAccessService` and `IMembershipAccessCommands` beside `AccountAccessService`.
- `ASI.Basecode.WebApp/DemoData/DemoMembershipAccessCommands.cs` — becomes the M-15b wrapper: delegate `DeactivateAsync` to the real service, then clear the store entry; keep demo `ReactivateAsync`.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs:328-352` — add `ClearMembershipStatus(int memberId)`; keep `TryGetMembershipStatus`/`SetMembershipStatus`.
- `ASI.Basecode.WebApp/DemoData/UnavailableAccessCommands.cs` — delete; it implements only `IMembershipAccessCommands` and the real service now serves the switch-off path.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs:24` — drop the `IMembershipAccessCommands` switch-off registration; update the class comment.
- `ASI.Basecode.WebApp/DemoData/DemoAccessRegistration.cs`, `DemoAccessStateOverlay.cs`, `IAccessStateOverlay.cs` — comment-only updates (membership deactivation is real, M-15b stays demo).
- `ASI.Basecode.WebApp/Controllers/MemberController.cs:100-132` — unchanged; both POSTs already call `IMembershipAccessCommands`.
- `ASI.Basecode.WebApp/Views/Member/_AccessDialog.cshtml`, `_AccessActions.cshtml` — unchanged; the dialog already shows the 5.3 preview and the required reason.
- `tests/ASI.Basecode.IntegrationTests/AdminAccountDeactivationTests.cs`, `AdminAccountReactivationTests.cs` — the endpoint/antiforgery/service-construction and demo-switch patterns to follow.
- `tests/ASI.Basecode.IntegrationTests/MemberEventFlowTests.cs:955-1022` — `MemberEventTestSupport` seed and sign-in helpers.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Resources/Constants/AuditActions.cs` — add `MemberMembershipDeactivated` — the action the ticket names.
- [x] `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `Messages/Audit.resx` — add the `MemberMembershipDeactivated` template — `ActivityLogWriter` requires a description for every action.
- [x] `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — guard `member.membership_deactivated` to entity type `members` — mirrors the other member actions.
- [x] `ASI.Basecode.Services/Services/MembershipAccessService.cs` — new real command: Management check, reason validation, one transaction with the `users` row lock, inactive status, cancellation cascade, one audit row; `ReactivateAsync` returns the standard "not available on this deployment" refusal until 5.7.
- [x] `ASI.Basecode.Services/Interfaces/IMembershipAccessCommands.cs` — update the contract comment — deactivation is real, reactivation stays demo.
- [x] `ASI.Basecode.WebApp/Startup.DI.cs` — register `MembershipAccessService` and `IMembershipAccessCommands` — the real command must win over any demo provider.
- [x] `ASI.Basecode.WebApp/DemoData/DemoMembershipAccessCommands.cs` — delegate `DeactivateAsync` to the real service and clear the store entry; keep the demo `ReactivateAsync` — M-15b removal is 5.7.
- [x] `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — add `ClearMembershipStatus` — no demo state may mask the real inactive row.
- [x] `ASI.Basecode.WebApp/DemoData/UnavailableAccessCommands.cs` — delete the file; `DemoDataRegistration.cs` — drop its registration line and update the comment — the real service serves the switch-off path.
- [x] `ASI.Basecode.WebApp/DemoData/DemoAccessRegistration.cs`, `DemoAccessStateOverlay.cs`, `IAccessStateOverlay.cs` — correct the M-15a/M-15b comments — the register must match the code.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-15a to `removed`, update the detail table, source/store paragraph and known limits — register rule 11.
- [x] `tests/ASI.Basecode.IntegrationTests/AdminMembershipDeactivationTests.cs` — add the focused preview/reason/cascade smoke, refusals and demo-switch case — the development check the ticket names.

**Acceptance Criteria:**
- Given an active member with two upcoming registrations and a waitlisted peer, when Management opens the dialog, then it lists exactly those two upcoming registrations from the real preview.
- Given the same member, when Management submits a deactivation with a reason, then `members.membership_status_id` is inactive with `updated_by`/`updated_at` set, both registrations are Cancelled with null QR tokens and the trimmed reason, the waitlisted peer is untouched, `users.is_active` stays true, and exactly one `member.membership_deactivated` row carries the reason and the active→inactive status.
- Given a deactivated membership, when the member signs in, then they reach `/MemberEvents` instead of a sign-in redirect.
- Given a blank or overlong reason, an already-inactive membership, an unknown member, or a non-Management caller, then nothing is written and no `member.membership_deactivated` row exists.
- Given the demo switch on, when a membership is deactivated, then the real service writes SQL, the store entry is cleared, and the Members list renders the inactive badge.

## Implementation Notes

`MembershipAccessService.DeactivateAsync` mirrors `AccountAccessService.DeactivateAsync` minus the
parts that only guard removal: the actor check and the trimmed 1–1000 character reason are validated
before any read, then one `ExecuteInTransactionAsync` reads the member record for its `UserId`, locks
that `users` row with the existing `IUserRepository.LockUserAsync`, reads the tracked member, refuses
an already-inactive membership, cascades through `IRegistrationCancellationService
.CancelUpcomingForUserAsync(..., CancellationCause.MembershipDeactivated, reason)`, writes
`members.membership_status_id = inactive` with `updated_by`/`updated_at`, saves, and appends one
`member.membership_deactivated` row (entity type `members`) carrying the reason and a
`MembershipStatusAuditValues` payload of `Active` → `Inactive`. No `attendta:last-admin` lock is taken
and `users.is_active` is never written, so the linked account keeps signing in.

`ReactivateAsync` returns the same `Invalid` "This action is not available on this deployment."
refusal the deleted `UnavailableAccessCommands` produced, until 5.7 implements it.

The demo split follows 5.4's account precedent: `Startup.DI` registers the real
`MembershipAccessService` (concrete and as `IMembershipAccessCommands`), and with the switch on
`DemoAccessRegistration` registers `DemoMembershipAccessCommands` last, so it wins. That wrapper
injects the concrete real service, delegates `DeactivateAsync` to it, then calls
`DemoStore.ClearMembershipStatus(memberId)` so `DemoAccessStateOverlay` cannot mask the SQL row; its
`ReactivateAsync` keeps the demo store and refusal rules. With the switch off nothing overrides the
real registration, so `UnavailableAccessCommands` was deleted and its line dropped from
`DemoDataRegistration`.

`AdminMembershipDeactivationTests` covers the five acceptance paths: the real 5.3 preview listing
exactly the member's upcoming registrations, the endpoint cascade (inactive row, both registrations
Cancelled with null QR tokens and the trimmed reason, waitlisted peer untouched, `users.is_active`
still true, one audit row with `Active` → `Inactive`), sign-in reaching `/MemberEvents` after
deactivation, the blank/overlong/already-inactive/unknown/non-Management/anonymous refusals writing
nothing, and the demo switch on (resolves `DemoMembershipAccessCommands`, writes SQL, clears the
store entry, renders the inactive badge).

## Plan Change Log

- Also corrected the register rule 8 refusal table row from "Deactivate or reactivate a membership"
  to "Reactivate a membership", because deactivation is no longer a demo command.

## Review Triage Log

### 2026-11-07 — Review pass
- verdicts: 4 findings — high 0, medium 0, low 3, false 1, maybe-false 0
- findings:
  - `[low]` `[reject]` `DemoMembershipAccessCommands.cs:39-43` — a store-Active plus SQL-Inactive member (left by a demo reactivation) is refused as already inactive and the store entry is never cleared, so the badge stays Active; the matrix row and the test only cover store-Active plus real-Active. The matrix row's stated state (`Store says active, real row active`) is reached and asserted by `DemoSwitchOnWritesSqlClearsTheStoreAndShowsTheInactiveBadge`. The reachable variant needs the reverse order (reactivate then deactivate) on the demo switch only, is recorded in the register's known limits, and its smallest fix is a new guard rather than a direct correction, so it is not worth the added branch before 5.7 makes reactivation real.
  - `[low]` `[patch]` `demo-mock-register.md:47` — register rule 7 still named M-15a as a row whose demo state changes the membership status, although M-15a is now `removed`. Rule 7 now reads "(M-15b)".
  - `[false]` `[reject]` `plan-57-member-membership-deactivation.md:4` — `ticket: '6'` is claimed to misidentify the change as ticket 6 instead of 57. The plan template defines `ticket` as the entry id from the ticket tree, and `tickets.toml` gives this entry `id = 6`; the sibling plans use `ticket: '3'`, `'5'` and `'11'` the same way. 57 is the GitHub tracker id, not this field.
  - `[low]` `[reject]` `AdminMembershipDeactivationTests.cs:269-329` — the demo case asserts store clearing and the inactive badge only on the success path, so a store entry surviving a refused deactivation is untested. Same root cause as the first finding: the refusal path is the documented demo-only limit, and the success path the matrix names is covered.

## Design Notes

Deactivation is account deactivation's sibling minus the parts that only guard removal: no `attendta:last-admin` application lock (a membership change cannot remove an Administrator) and no `users.is_active` write (membership eligibility is separate from login access). The `users` row lock is still taken because AD-4 makes it the serialization point that also protects the `members` row, and the cancellation service already runs inside the caller's transaction. The audit payload carries only the membership status, never a token, path or personal field.

The demo split follows 5.4's account precedent: the still-demo command stays in the demo provider behind the switch, the real command is registered in DI, and the wrapper delegates the real half and clears the store so `DemoAccessStateOverlay` cannot mask the authoritative SQL row. Deleting `UnavailableAccessCommands` is safe because the real service now supplies the switch-off refusal for the one command it does not implement.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — expected: 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all existing tests pass.
- With `ATTENDTA_TEST_SQL` set to a local SQL Server master connection: `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter FullyQualifiedName~AdminMembershipDeactivationTests` — expected: all pass.

**Manual checks:**
- With `DemoData__Enabled=true`, `DemoData:TokenKey` set and `DemoData:AllowDatabase` matching the local database, open `/Member`, deactivate a member with a reason, and confirm the badge flips to Inactive while the linked account still signs in.

## Auto Run Result

**Summary:** `IMembershipAccessCommands.DeactivateAsync` is now the real transactional command: Management-actor check, trimmed 1–1000 character reason validated before any read, one `ExecuteInTransactionAsync` that locks the member's `users` row, refuses an already-inactive membership, cascades through `IRegistrationCancellationService.CancelUpcomingForUserAsync(..., MembershipDeactivated, reason)`, sets `members.membership_status_id = inactive` with `updated_by`/`updated_at`, and appends one `member.membership_deactivated` Activity Log row. No application lock and no `users.is_active` write, so the linked account keeps signing in. The M-15a demo seam is removed; M-15b reactivation stays demo behind the switch.

**Files changed:**
- `ASI.Basecode.Services/Services/MembershipAccessService.cs` — new real membership deactivation command; reactivation refuses until 5.7.
- `ASI.Basecode.Services/Interfaces/IMembershipAccessCommands.cs` — contract comment updated.
- `ASI.Basecode.Resources/Constants/AuditActions.cs`, `Messages/AuditDescriptions.cs`, `Messages/Audit.resx` — `member.membership_deactivated` action and template.
- `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — entity-type guard for the new action.
- `ASI.Basecode.WebApp/Startup.DI.cs` — real service registered concretely and as `IMembershipAccessCommands`.
- `ASI.Basecode.WebApp/DemoData/DemoMembershipAccessCommands.cs` — delegates deactivation to the real service and clears the store; demo reactivation kept.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — `ClearMembershipStatus`.
- `ASI.Basecode.WebApp/DemoData/UnavailableAccessCommands.cs` — deleted; `DemoDataRegistration.cs` registration line and comment updated.
- `ASI.Basecode.WebApp/DemoData/DemoAccessRegistration.cs`, `DemoAccessStateOverlay.cs`, `IAccessStateOverlay.cs` — M-15a/M-15b comments corrected.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — M-15a `removed`; rule 7, rule 8, detail, source/store and known-limits sections updated.
- `tests/ASI.Basecode.IntegrationTests/AdminMembershipDeactivationTests.cs` — focused preview, cascade, sign-in, refusals and demo-switch coverage.

**Review findings breakdown:** 4 findings — 1 patched (`low`: register rule 7 still named M-15a), 3 rejected (2 `low`: documented demo-only store/SQL divergence and its missing refusal-path test, both needing a new guard for a reverse-order demo flow; 1 `false`: `ticket: '6'` is the entry id the template asks for). 0 deferred.

**Follow-up review recommendation:** false — one `low` entry patched, no `high` and fewer than two `medium` entries.

**Verification performed:**
- `dotnet build ASI.Basecode.sln` — 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — 281/281 passed.
- With `ATTENDTA_TEST_SQL` set: `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter FullyQualifiedName~AdminMembershipDeactivationTests` — 5/5 passed.

**Residual risks:** the demo switch still overlays a demo reactivation over an inactive SQL row, so a following real deactivation of that member is refused as already inactive until 5.7 makes reactivation real; this is recorded in the demo register's known limits. The plan's manual browser check was not run; its two assertions are covered by the integration tests.

