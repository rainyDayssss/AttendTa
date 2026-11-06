---
title: 'Management Account Request approval and rejection in caller transaction with audit'
type: 'feature'
ticket: '7'
created: '2026-11-06'
status: 'built'
baseline_revision: '08480597817960bcbbedd0d7a9c116f0b1206718'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
  - '_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Management has no way to finalize pending Account Requests, so applicants cannot become member accounts through the reviewed flow. Decisions must preserve the request's password hash, create a linked active Member profile on approval, and be atomic and auditable under concurrent decisions.

**Approach:** Add authorized approval and rejection operations that run inside `ExecuteInTransactionAsync`, lock and re-check the pending request, and append the decision audit in the same transaction. Approval creates the Member account and calls the existing member-creation service; rejection requires a reason and clears the stored hash.

## Boundaries & Constraints

**Always:** Take the actor from `ICurrentActor`; only Management may decide. Lock `account_requests` before `users`, re-check email and student-number uniqueness including inactive records, and let existing unique indexes remain the race guard. Approval transfers the stored hash unchanged to an active Member user and profile, then clears it from the finalized request. Rejection records its required reason and also clears the hash. Save the request, account, profile, and exactly one audit entry within the caller transaction; return `Stale` for a finalized or losing decision and roll back all effects. Audit payloads must never contain passwords or hashes.

**Decision:** Issue #18 adds the decision service and Management-only POST endpoints; issue #17 owns queue listing and form integration, which can call these endpoints once its UI lands.

**Never:** Change the schema, hash a second time, create a standalone member, bypass `MemberService.CreateMemberFromApprovedAccountRequestAsync`, write audit outside the decision transaction, or weaken uniqueness constraints.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Approval | Authenticated officer/admin; pending request; unique email and student number | Active Member account and profile; original password works; request approved and hash cleared; one audit row | None |
| Rejection | Pending request and nonblank reason | Request rejected with reviewer/time/reason; hash cleared; no account/profile; one audit row | None |
| Invalid rejection | Missing/blank reason | No state change or audit | `Invalid` |
| Missing/finalized request | Request absent or already decided, including a competing decision | No account/profile/audit from this attempt | `NotFound` / `Stale` |
| Uniqueness race | Email or student number becomes occupied during approval | No partial account, profile, request update, or audit | `Duplicate` |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Services/AccountRequestService.cs` and `Interfaces/IAccountRequestService.cs` currently only submit requests; add decision orchestration here.
- `ASI.Basecode.Data/Repositories/AccountRequestRepository.cs` and `IAccountRequestRepository.cs` already lock the request row and query pending uniqueness; `UserRepository.LockUserAsync` provides the next lock. `IUserRepository`/`UserRepository` need a save-free user insert operation.
- `ASI.Basecode.Data/UnitOfWork.cs` joins nested calls, rolls failures back, clears tracking, and translates SQL 2601/2627 to `Duplicate`; the caller must save explicitly.
- `ASI.Basecode.Services/Services/MemberService.cs` implements `CreateMemberFromApprovedAccountRequestAsync` and joins the caller transaction. Build its `MemberViewModel` from request profile fields; it sets active status and actor/time.
- `ASI.Basecode.Services/Services/ActivityLogWriter.cs`, `IActivityLogWriter.cs`, `AuditActions.cs`, `AuditDescriptions.cs`, and `Messages/Audit.resx` support authenticated transactional audit and approval today; add rejection to the closed action catalog and entity validation.
- `ASI.Basecode.WebApp/Controllers/AccountRequestController.cs` is `[AllowAnonymous]` for public submission. Management decision POSTs need a separately authorized controller/action boundary using `Policies.Management`, antiforgery, `GetActorUserId()`, and `RequestAborted`.
- `tests/ASI.Basecode.Services.Tests/AccountRequestServiceTests.cs` covers submission with repository fakes; update it for new dependencies and add decision rules. `tests/ASI.Basecode.IntegrationTests/SqlServerDatabaseFixture.cs`, `TransactionVerificationTests.cs`, and `MasterDataTransactionTests.cs` provide isolated SQL Server, observer-context, rollback, and lock-test patterns.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/IUserRepository.cs` and `Repositories/UserRepository.cs` — add a cancellable save-free user insertion method so approval stages the account through the repository.
- [x] `ASI.Basecode.Services/Interfaces/IAccountRequestService.cs` and `Services/AccountRequestService.cs` — add approve/reject operations; validate actor and reason; execute transaction, lock request then existing user, re-check uniqueness, create user and linked profile, finalize request/hash, append audit, and save.
- [x] `ASI.Basecode.Resources/Constants/AuditActions.cs`, `Messages/AuditDescriptions.cs`, `Messages/Audit.resx`, and `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — add a supported rejection action/template for `account_requests`.
- [x] `ASI.Basecode.WebApp/Controllers/ManagementAccountRequestController.cs` — expose Management-only antiforgery POST decisions, map domain failures through existing controller handling, and pass the authenticated actor and cancellation token.
- [x] `tests/ASI.Basecode.Services.Tests/AccountRequestServiceTests.cs` — cover authorization, invalid reason, missing/stale requests, uniqueness rejection, and staged member/audit outcomes using fakes.
- [x] `tests/ASI.Basecode.IntegrationTests/AccountRequestDecisionTests.cs` — prove approval/login, rejection/resubmission, same-request parallel decisions, email/student-number races, and full rollback/no audit for losers on independent SQL Server contexts.

**Acceptance Criteria:**
- Given a Management officer and a unique pending request, when approved, then one active Member account/profile exists, the original password authenticates, the request is approved with its hash cleared, and one audit row is committed.
- Given a pending request and a nonblank rejection reason, when rejected, then the request records the reason and reviewer, clears its hash, creates no account/profile, and has one audit row; a rejected applicant can submit again.
- Given two decisions for one request, when they run concurrently, then exactly one commits and the losing call returns `Stale` with no second account/profile or audit row.
- Given an email or student number conflict during concurrent approval, when the decisions finish, then no duplicate account/profile persists and the failed attempt leaves no audit row.
- Given an anonymous/member caller, blank rejection reason, missing request, or finalized request, when a decision is attempted, then authorization/validation/not-found/stale behavior is returned without unintended writes.

## Implementation Notes

Implemented approval/rejection orchestration, Management-only POST endpoints, and transactional audit support. Verified on the .NET 10 SDK: Release build succeeded with 0 warnings/errors; Services tests passed 51/51; AccountRequestDecisionTests passed 7/7, including two concurrent approvals. The full IntegrationTests run passed 120/121; `CertificateTracerFlowTests.TracerRequiresConfirmedRegistrationAndManagementInDevelopmentAndDeletesStreamedPdf` returned HTTP 500. The same isolated failure was reproduced at baseline revision `08480597817960bcbbedd0d7a9c116f0b1206718`, so it predates this change. Quick review required no code patches; its findings and triage are recorded below. Queue display and form integration remain with issue #17.

Follow-up (2026-11-06): queue integration landed. The queue posts to the browser decision endpoints on `AccountRequestController`, which preserve the active status filter and re-render the queue with the failure. Approve and Decline use centered dialogs; Approve states the account-creation consequence and both disable their submit button while posting. The decline dialog preserves the typed reason and associates its error with the textarea through `aria-describedby` and `aria-invalid`. `ManagementAccountRequestController` remains the JSON/API command endpoint.

## Plan Change Log

## Review Triage Log

| # | Location | Verdict | Evidence | Route |
|---|---|---|---|---|
| 1 | `ASI.Basecode.Services/Services/AccountRequestService.cs:257` | low | The supplied rejection reason is stored as `AuditEntry.Reason` and `review_notes`; a reason deliberately containing a credential would be persisted. The field is required human-authored rationale, distinct from the validated old/new audit payloads, and arbitrary-text filtering cannot reliably identify credentials without corrupting valid reasons. This is not an everyday input; no change. | reject |
| 2 | `AGENTS.md:101-103,139-141` | false | The general assignment note limits default work to Master Data, but the user explicitly selected Platform Access story 1.7 and directed its implementation; the ticket's R2 scope explicitly creates accounts and calls MemberService. The targeted issue is the controlling task intent. | reject |

## Design Notes

The locked `AccountRequest` is tracked and refreshed by its repository. Create and save the user inside the transaction to obtain its generated ID, call `MemberService` (which joins the transaction), then finalize the request, append the audit, and save again. A uniqueness insert race is translated by `UnitOfWork`; the transaction rollback must remove prior user/member writes. For existing emails, acquire `LockUserAsync` only after the request lock. Do not include any hash-bearing property in audit snapshots.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln --configuration Release` — solution builds without errors.
- `dotnet test --project tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj --configuration Release` — service tests pass.
- With `ATTENDTA_TEST_SQL` set to a local SQL Server `master` connection, `dotnet test --project tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --configuration Release` — isolated integration tests pass, including decision and concurrency coverage.
