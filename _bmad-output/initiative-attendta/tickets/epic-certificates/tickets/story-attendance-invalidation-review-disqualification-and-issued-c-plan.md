---
title: 'Attendance invalidation review disqualification and issued-certificate lock'
type: 'feature'
ticket: '5'
created: '2026-11-07'
status: 'built'
baseline_revision: 'afb60e6f2f5fd4fcff657486e3d09ae976f1fb09'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: []
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 4.6 invalidates attendance but nothing disqualifies the certificate review or blocks invalidation once a certificate is issued, so a corrected attendance can still be approved and issued.

**Approach:** Implement the `IAttendanceInvalidationHandler` hook 4.6 declares: inside the invalidation transaction, flip a `pending_approval` or `eligible` review to `not_eligible` with the correction reason, and throw `Forbidden` when the certificate is already issued. Register it and retire the demo store's duplicate rule.

## Boundaries & Constraints

**Always:** Run inside the caller's invalidation transaction and share its unit of work; rely on the event lock it already holds for serialization against issuance; leave the review untouched when absent or already `not_eligible`; commit review change, attendance stamp and audit together.

**Never:** Open a transaction; add a second invalidation screen; change presentation; write a second audit entry; implement issuance or download (6.7/6.8); overwrite an existing `not_eligible` reason; add a certificate lock AD-4 does not require.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Pending review | `pending_approval` | `not_eligible`, `review_notes` = reason; attendance stamped; one audit | No error |
| Eligible review | `eligible` | Same as pending | No error |
| Issued certificate | `issued` | Refused; attendance valid; no audit | `Forbidden` |
| No review row | No certificate | Commits; no row created | No error |
| Already not eligible | `not_eligible` with reason | Unchanged; commits | No error |
| Later handler throws | Any handler throws | Whole invalidation rolls back | Propagates |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Interfaces/IAttendanceInvalidationHandler.cs:14-17` — hook contract; do not change.
- `ASI.Basecode.Services/Services/AttendanceInvalidationService.cs:54-56,68-77,107-115` — caller transaction, event lock, handler loop before the stamp; do not edit.
- `ASI.Basecode.Services/Services/CertificateReviewCompletionHandler.cs` — sibling hook: share `IUnitOfWork`, save, never open a transaction.
- `ASI.Basecode.Data/Models/Certificate.cs:10-25`; `Models/Lookups/LookupCodes.cs:46-52` — status ids and the `ReviewNotes`/`UpdatedBy/At` fields; `CanonicalSchemaMappings.cs:252-281` maps `ReviewNotes` to the only free-text column, `review_notes`.
- `ASI.Basecode.Data/Interfaces/ICertificateRepository.cs`, `Repositories/CertificateRepository.cs` — add the tracked read; existing methods stay.
- `ASI.Basecode.WebApp/Startup.DI.cs:58,65-67,81` — service, hook and repository blocks.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs:141-183` — the 1.14 store rule to retire; `DemoSeedService.cs:135-142` already seeds the `not_eligible` review.
- `tests/ASI.Basecode.IntegrationTests/AttendanceInvalidationFlowTests.cs:244-307` — fixture, `Service(...)` and `SeedAsync` to extend; `EventCompletionReviewTests.cs:150-157` — DI assertion pattern.
- `_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md:111-136` — AD-4: certificate rows are written only under their event lock.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/ICertificateRepository.cs` — add `Task<Certificate> GetByRegistrationIdAsync(int registrationId, CancellationToken ct = default)` returning the tracked row or `null`; document that callers mutate it inside their transaction.
- [x] `ASI.Basecode.Data/Repositories/CertificateRepository.cs` — implement the tracked single-or-default read; no `AsNoTracking`, no save.
- [x] `ASI.Basecode.Services/Services/CertificateReviewInvalidationHandler.cs` — new handler: load the row; throw `Forbidden` ("A certificate has already been issued for this attendance.") when `Issued`; return when absent or not `PendingApproval`/`Eligible`; else set `NotEligible`, `ReviewNotes = context.Reason`, `UpdatedBy/UpdatedAt`, save.
- [x] `ASI.Basecode.WebApp/Startup.DI.cs` — register `IAttendanceInvalidationHandler → CertificateReviewInvalidationHandler` beside the completion hook.
- [x] `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — delete the review-flip from `InvalidateAttendance` (the 1.14 rule 6.5 replaces); keep the issued gate and update the comment.
- [x] `tests/ASI.Basecode.IntegrationTests/AttendanceInvalidationFlowTests.cs` — add a `Func<IUnitOfWork, IEnumerable<IAttendanceInvalidationHandler>>` factory to `Service(...)` and seven facts: pending, eligible, issued, no row, already `not_eligible`, later-handler rollback, DI registration.

**Acceptance Criteria:**
- Given a `pending_approval` or `eligible` review, when invalidation commits, then it is `not_eligible` with the reason and actor/time, the attendance is stamped and one `attendance.invalidated` audit row exists.
- Given an `issued` certificate, when invalidation runs, then it throws `Forbidden` and neither the attendance row nor the audit changes.
- Given the DI container, when `IAttendanceInvalidationHandler` is resolved, then `CertificateReviewInvalidationHandler` is present.

## Implementation Notes

- Matrix audit: the "later handler throws" row needed a test where the certificate write itself rolls back, so `LaterHandlerFailureRollsBackTheReviewChange` runs the certificate handler first and a throwing handler second, then asserts the review is still `pending_approval`.

## Plan Change Log

## Review Triage Log

**Pass 1 (quick)** — high 0, medium 0, low 4, false 1, maybe-false 0.

| Finding | Verdict | Route | Evidence / action |
|---|---|---|---|
| `demo-mock-register.md:136-137,304-305` still says `InvalidateAttendance` flips the review | low | patch | Confirmed: the diff removed the flip, so the register now contradicts `DemoStore.cs`. Correct the two sentences to name `CertificateReviewInvalidationHandler`. |
| Handler does not throw when no transaction is open (AD-3) | false | rejected | The handler is registered only as `IAttendanceInvalidationHandler`; its sole caller `AttendanceInvalidationService.InvalidateAsync` opens `ExecuteInTransactionAsync` before the handler loop. No path invokes it outside a transaction, so the described standalone commit cannot occur. |
| New facts count audit rows without asserting `attendance.invalidated` | low | patch | Confirmed: the AC names the action; the new tests filter by entity only. Add `Action == AuditActions.AttendanceInvalidated` to the predicates. |
| Eligible fixture never sets `ReviewedBy`/`ReviewedAt`, so preservation is unproven | low | patch | Confirmed against `docs/implementation-plans/CERT-01_Independent_Backend_And_Integration_Plan.md:191`. Seed an original reviewer and assert it survives the flip; the issued fixture's null issuance fields are schema-permitted and irrelevant to the status check. |
| `Startup.DI.cs:69-71` says "every registration" | low | patch | Confirmed: the service iterates registered handlers. Change to "every registered handler". |
| `ICertificateRepository.cs:8-11` summary scopes the interface to the completion hook | low | patch | Confirmed: the interface now serves both hooks. Reword the summary. |


## Verification

**Commands:**
- `dotnet build` — expected: clean build.
- `$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter "FullyQualifiedName~AttendanceInvalidationFlowTests|FullyQualifiedName~CertificateLedgerTests"` — expected: all pass.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: green.

**Manual checks:**
- With `DemoData__Enabled=true`, confirm the ledger still shows its seeded pending/declined/issued rows.
