---
title: 'Registration review and transactional seat allocation'
type: 'feature'
ticket: '7'
created: '2026-11-06'
status: 'built'
baseline_revision: 'f53ae5580941a06a530cf64af9e6e6b0c0237161'
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

**Problem:** Officers cannot review registrations or allocate limited seats fairly and safely under concurrent requests.

**Approach:** Add a management review queue and transactional approval, decline, and administrative cancellation, using the existing shared facts, locks, cancellation service and audit writer.

## Boundaries & Constraints

**Always:** Authorize management and require antiforgery on POSTs. Discover the owner and event, lock the owner's user row then the event, and recheck the registration and applicable event, eligibility, attendance, reason and cutoff rules inside the transaction. Capture injected UTC time after locks. Approval requires a Published event and an eligible owner, strictly before start; signup deadline does not close review. Serialize seat and eligible-waitlist decisions on the event lock. Use canonical participation facts, fixed 25-row paging, deterministic ordering, and atomic caller audit. Never expose QR tokens in audit or diagnostics.

**Never:** Automatically promote waitlisted registrations, implement promotion itself, duplicate cancellation state changes, allow cancellation with valid attendance, hard-delete records, or expand into unrelated modules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Approve | Pending, eligible, published, before start; seat free, no eligible waitlist priority | Confirm with unique 32-random-byte lowercase 64-hex QR; stamp reviewer and confirmation metadata; audit | Atomic |
| Approve without fair seat | Full or earlier eligible waitlist exists | Waitlist and stamp review metadata; audit | No QR |
| Stale review | Changed owner, status, eligibility or event; at/after start | Refuse without mutation | Refreshable error |
| Decline | Pending, nonblank reason | Stamp review; cancel through Declined; caller and cancellation audits | Missing reason refuses atomically |
| Administrative cancellation | Confirmed or Waitlisted, no valid attendance, nonblank reason | Cancel through AdminCancelled; clear QR; caller and cancellation audits | Invalid state, attendance or missing reason refuses atomically |
| Concurrent last seat | Two independent pending approvals, one remaining seat | Exactly one confirmed, other waitlisted | No overbooking |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Services/MemberEventService.cs` and `ASI.Basecode.Data/Repositories/{UserRepository,EventRepository}.cs` demonstrate caller-owned transactions and user → event row locks; rediscover and recheck after locking.
- `ASI.Basecode.Data/Queries/ParticipationFacts.cs` defines eligibility, valid attendance, seat, waitlist ordering and cutoff; `RegistrationRepository.cs` provides seat counts and cancellation reads. Extend its interface and test fakes.
- `ASI.Basecode.Services/Services/RegistrationCancellationService.cs` requires the caller's locks and transaction, writes `registration.cancelled`, and preserves caller review metadata. The caller performs business guards and its separate audit.
- `ASI.Basecode.Services/Services/ActivityLogWriter.cs`, `ASI.Basecode.Resources/Constants/AuditActions.cs`, and `ASI.Basecode.Resources/Messages/AuditDescriptions.cs` constrain action vocabulary and safe payloads.
- `ASI.Basecode.WebApp/Controllers/AccountRequestController.cs`, `Views/AccountRequest/Index.cshtml`, `Views/Shared/_Pager.cshtml`, and `ServiceModels/PageRequest.cs` show authenticated review, feedback and paging patterns. Link the review queue from management event navigation.
- `tests/ASI.Basecode.IntegrationTests/MemberEventFlowTests.cs` supplies SQL concurrency and independent-context fixtures; `RegistrationCancellationTests.cs` and `AccountRequestQueueTests.cs` show cancellation and browser checks.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs` and `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` — add discovery, tracked rechecks, eligible waitlist and paged queue queries using canonical facts and stable ordering.
- [x] `ASI.Basecode.Services/Interfaces/`, `ASI.Basecode.Services/Services/`, `ASI.Basecode.Services/ServiceModels/` — implement review commands and queue, locking and guards, cryptographic token generation, existing cancellation calls and atomic audit; update affected repository fakes.
- [x] `ASI.Basecode.Resources/Constants/AuditActions.cs`, `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — register safe approval, decline and admin-cancellation audit actions.
- [x] `ASI.Basecode.WebApp/Startup.DI.cs`, `ASI.Basecode.WebApp/Controllers/`, `ASI.Basecode.WebApp/Views/` — wire management-only queue, paging, forms and navigation with server-side validation and antiforgery.
- [x] `tests/ASI.Basecode.Services.Tests/` and `tests/ASI.Basecode.IntegrationTests/` — cover the matrix, boundary and stale races, fair waitlist eligibility, paging, security, rollback, audit safety and real SQL concurrent final-seat approvals.

**Acceptance Criteria:**
- Given an officer reviewing pending registrations, when approval is submitted, then confirmation or waitlisting follows capacity and eligible waitlist priority, with correct timestamps and audit.
- Given an officer declining or administratively cancelling an eligible registration, when a valid reason is submitted, then both caller and cancellation audit records persist with the cancellation; invalid requests leave no changes.
- Given concurrent SQL Server approvals for the final seat, when both requests complete, then at most one seat is allocated and both outcomes and audit records agree with persisted state.
- Given a management user with more than 25 review records, when navigating pages, then records are ordered consistently with filters retained; unauthorized and antiforgery-invalid mutations are rejected.

## Implementation Notes

- Implemented the queue and locked transaction commands; extended cutoff enforcement to all review and administrative cancellation operations before review. SQL-backed tests cover last-seat contention, cancellation, audits, eligibility, stale requests, paging, authorization and antiforgery.

## Plan Change Log

## Review Triage Log

- Quick review: 0 high, 0 medium, 0 low, 1 false, 0 maybe-false. The reviewer claimed reasons longer than the form's 500-character limit are invalid server-side. The approved requirements require a nonblank reason, not a 500-character business limit; the database columns for review notes, cancellation reasons and audit reasons are `nvarchar(max)`. The UI limit does not establish a server-side domain invariant, so accepting longer nonblank reasons is not a demonstrated correctness failure.

## Verification

- `dotnet build` — succeeds using the installed .NET 10 SDK.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — all tests pass.
- Set `ATTENDTA_TEST_SQL` to local SQL Server master, then `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — all tests pass, including SQL concurrency and authenticated browser-flow coverage.
- Verified with .NET SDK 10.0.403 and LocalDB: build passed; 196 service tests and 267 integration tests passed, none skipped. Existing certificate tests used Poppler from the OpenCode temporary directory.
