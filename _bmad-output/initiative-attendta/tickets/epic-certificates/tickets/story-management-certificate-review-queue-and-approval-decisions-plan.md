---
title: 'Management certificate review queue and approval decisions'
type: 'feature'
ticket: '6'
created: '2026-11-07'
status: 'built'
baseline_revision: 'a53fd765b63be841889e593f97274f0cc3314762'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: [quick]
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md'
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The certificate ledger's review queue and its approve/decline decisions are in-memory demo state (M-10b). A real completed event therefore has no way to approve or decline a review, nothing is audited, and a decision cannot be serialized against invalidation.

**Approach:** Implement the queue and both decisions against SQL behind the existing 6.12 ledger contract: a 25-row paged queue, approve to `eligible`, decline to `not_eligible` with a required reason, each under the AD-4 locks with a `Stale` refusal and a `certificate.approved`/`certificate.declined` audit entry; retire M-10b.

**Decision (demo hand-over, 2026-11-07):** Seed the demo event's attendance and pending/declined reviews into SQL. The demo provider delegates the queue and decisions to the real service and overlays the still-demo issued state until 6.7, so the screen stays unchanged while M-10b's decision and review-composition code is deleted.

## Boundaries & Constraints

**Always:** Reuse `ICertificateLedgerService`, `CertificateLedgerController` and the existing views; Management authorization; one caller-owned transaction; lock the owner `users` row then the event (AD-4); recheck `pending_approval` and Valid Attendance inside the transaction; append the audit entry through `IActivityLogWriter` before the save; page with `PageRequest`/`_Pager`; keep the demo screen populated and unchanged.

**Never:** A second screen, controller or contract; schema or migration changes; issuance or download (6.7/6.8); reopening a declined review; bulk decisions; frontend presentation changes; hard-coded actor ids; audit outside the transaction; deleting the demo provider that still serves M-11a/M-11b.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Approve | `pending_approval`, valid attendance | `eligible`, reviewer + time, one `certificate.approved` audit | No error |
| Decline | `pending_approval`, non-blank reason | `not_eligible`, `review_notes` = trimmed reason, one `certificate.declined` audit | No error |
| Blank reason | whitespace-only reason | State unchanged | `Invalid`, field `reason` |
| Already decided | `eligible`/`not_eligible`/`issued` | State unchanged | `Stale` |
| Invalidated attendance | `invalidated_at` set | State unchanged | `Stale` |
| Unknown review | no certificate row | State unchanged | `NotFound` |
| Non-management caller | member role | Refused | `Forbidden` |
| Page past the end | `page` > total pages | Clamped to last page | No error |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Interfaces/ICertificateRepository.cs` — add `CertificateReviewQueueRow` and `GetReviewQueueAsync(eventId)`; keep `GetByRegistrationIdAsync` tracked.
- `ASI.Basecode.Data/Repositories/CertificateRepository.cs` — queue query: confirmed registrations → member/user, left-join attendance and certificate.
- `ASI.Basecode.Data/Queries/ParticipationFacts.cs:15` — `ValidAttendance` (`InvalidatedAt == null`).
- `ASI.Basecode.Data/Repositories/UserRepository.cs:65`, `EventRepository.cs:212` — `LockUserAsync` then `LockEventAsync`.
- `ASI.Basecode.Data/UnitOfWork.cs:48-86` — `ExecuteInTransactionAsync`; nested calls join the outer transaction.
- `ASI.Basecode.Services/Interfaces/ICertificateLedgerService.cs:7-14` — the 6.12 contract; do not change.
- `ASI.Basecode.Services/Services/CertificateLedgerService.cs` — new real implementation.
- `ASI.Basecode.Services/Services/CertificateReviewInvalidationHandler.cs` — mutation idiom to copy.
- `ASI.Basecode.Services/Services/AttendanceInvalidationService.cs:48-124` — discover → transaction → locks → recheck → mutate → audit.
- `ASI.Basecode.Resources/Constants/AuditActions.cs`, `Services/ActivityLogWriter.cs:24-34` — new action constants and the allow-list guard.
- `ASI.Basecode.WebApp/Controllers/CertificateLedgerController.cs:32-105` — unchanged; it already consumes the contract.
- `ASI.Basecode.WebApp/Startup.DI.cs:35-63,74-86,108-110` — service, repository and demo registration points.
- `ASI.Basecode.WebApp/DemoData/DemoCertificateLedgerService.cs:22-90`, `DemoCertificatesRegistration.cs:10-23`, `DemoStore.cs:176-197`, `DemoSeedService.cs:111-173` — M-10b removal and the M-11a/M-11b hand-over.
- `tests/ASI.Basecode.IntegrationTests/AttendanceInvalidationFlowTests.cs:416-510` — `Service(...)`/`SeedAsync` pattern to copy.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Resources/Constants/AuditActions.cs` — add `CertificateApproved`/`CertificateDeclined`; `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — allow both for entity `certificates`.
- [x] `ASI.Basecode.Data/Interfaces/ICertificateRepository.cs` + `Repositories/CertificateRepository.cs` — add the queue projection and query.
- [x] `ASI.Basecode.Services/Services/CertificateLedgerService.cs` — new: real `GetPageAsync` and `DecideAsync`; `IssueAsync`/`GetViewerAsync`/`GetPdfAsync` throw unavailable.
- [x] `ASI.Basecode.WebApp/Startup.DI.cs` — register the real service before `AddDemoData`.
- [x] `ASI.Basecode.WebApp/DemoData/DemoCertificateLedgerService.cs` — delegate queue/decisions, overlay issued state, keep issuance/viewer.
- [x] `ASI.Basecode.WebApp/DemoData/DemoCertificatesRegistration.cs` + `UnavailableCertificateLedgerService.cs` — demo override when enabled; retire the unavailable stub.
- [x] `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs` + `DemoStore.cs` — seed SQL attendance and reviews; delete `DecideCertificate`.
- [x] `tests/ASI.Basecode.IntegrationTests/CertificateReviewDecisionTests.cs` — approve, decline, blank reason, stale non-pending, stale invalidated, unknown, forbidden, paging, DI.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-10b `removed (<commit>)`.

**Acceptance Criteria:**
- Given a pending review with valid attendance, when an Officer approves it, then it is `eligible` with reviewer and time and one `certificate.approved` audit row.
- Given a pending review, when an Officer declines it with a reason, then it is `not_eligible` with that reason and one `certificate.declined` audit row.
- Given a blank reason, when decline runs, then the review is unchanged and the error names the reason field.
- Given a review that is not pending or whose attendance is invalid, when a decision runs, then it is refused as `Stale` with no mutation and no audit.
- Given a member caller, when a decision runs, then it is refused as `Forbidden`.
- Given more than 25 reviews, when the queue renders, then it shows 25 rows and the pager reaches the rest.
- Given `DemoData:Enabled=true`, when the ledger opens, then the same pending/declined/issued rows appear as before.

## Implementation Notes

- The demo provider needs the selected event's full review set to overlay the still-demo issued state before tab filtering, so `CertificateLedgerService` exposes `ReadAsync` and a static `BuildPage`. `ICertificateLedgerService` is unchanged; the demo registration replaces only that interface.
- `DemoMyCertificatesService` (M-12) was handed over to the real SQL reviews in the same change (register rule 12). It overlays only the store issue, so member history keeps its pending/declined/issued states.
- `DemoStore` now holds only pass tokens and issued certificate documents. The attendance, review and completed-event fixtures were removed with the seed hand-over; the seed writes attendance and reviews through the repositories inside one unit-of-work transaction and never overwrites an existing row.
- `CertificateLedgerTests.cs` was deleted because it tested the removed demo review/decision composition. `CertificateLedgerFlowTests.cs` now covers the seeded SQL rows, both decision audits and the demo issuance/viewer end to end.
- The xUnit v3 MTP test projects do not discover tests through `dotnet test` (it reports "Zero tests ran"). The equivalent runs used `dotnet run --project <test project> --no-build -- -class <class>`.
- The M-10b register row is `removed`; add the commit hash when the change is committed.
- Correction (2026-11-07): the xUnit v3 MTP projects do discover tests through `dotnet test --filter` on this machine. The earlier "Zero tests ran" note was wrong; the plan's verification commands ran as written — 11 focused decision tests, 12 with the ledger flow, and the full 410-test suite all passed with exit code 0.
- `CertificateLedgerTests.cs` deletion removes the unit-level demo issuance failure and concurrency cases. The end-to-end demo issuance and viewer path stays covered by `CertificateLedgerFlowTests`; SQL-backed issuance unit cases belong to 6.7.
- Review pass 1 patches: the seed now derives `valid` from the real attendance rows and never approves an invalidated attendee; the pending-state check precedes the decline-reason check; demo issuance and the seed mark the SQL review `Issued` so the 6.5 invalidation guard refuses; issued rows use the retained event-title snapshot; the member history reads one certificate instead of the whole queue; the M-11a register text matches the code.

## Plan Change Log

## Review Triage Log

**Pass 1 (quick)** — high 0, medium 2, low 4, false 1, maybe-false 0.

| Finding | Verdict | Route | Evidence / action |
|---|---|---|---|
| `DemoSeedService.cs:175,201-241` — `valid` only excludes the seed's own invalidated row, so an attendee whose existing attendance is already invalidated can be staged and set `Eligible` | medium | patch | Verified: `valid` is `recorded` minus the one id the seed invalidates; it never reads `InvalidatedAt` on existing rows, so a pre-existing invalidated attendance with no certificate row gets an approved review and the issued overlay. Fix: derive `valid` from the real attendance rows and never approve an invalidated attendee. |
| `CertificateLedgerService.cs:193-212` — the blank-reason check runs before the pending-state check, so a decline with a blank reason on a decided review returns `Invalid` instead of the matrix's `Stale` | low | patch | Verified: reason validation precedes the status guard; `AlreadyDecidedReviewIsStaleAndUnchanged` only exercises approve. Fix: check `PendingApproval` first, then validate the reason. |
| `CertificateLedgerService.cs:174-217` — the decision does not recheck confirmed registration or a completed event | false | rejected | `MemberEventService.CancelAsync` and `RegistrationReviewService` both refuse cancellation while valid attendance exists, and reviews exist only after completion, so the described candidate is not reachable through a normal flow. The intent names pending + Valid Attendance as the rechecks. |
| `DemoStore.cs:68-79` — `PublishCertificate` no longer rechecks invalidation or review state inside the store gate | low | patch | Verified: the store no longer owns attendance or review state, so the gate cannot recheck it; the register's M-11a row still claims it does. Fix: correct the register text to describe the caller recheck and the single-publication guarantee. |
| `DemoCertificateLedgerService.cs:48-72` — demo issuance leaves the SQL review `Eligible`, so an issued certificate can still be invalidated | medium | patch | Verified: `CertificateReviewInvalidationHandler` refuses only on SQL `Issued`; the demo issue and the seed leave the row `Eligible`, so invalidation flips it while the overlay still shows Issued. Fix: mark the SQL review `Issued` after a successful publish and seed the issued attendee as `Issued`. |
| `CertificateLedgerService.cs:244-269` — issued rows show the current event title instead of the retained snapshot | low | patch | Verified: `Map` uses the selected event title and `CertificateReviewQueueRow` lacks `EventTitleSnapshot`. Fix: carry the snapshot and prefer it for issued rows. |
| `DemoMyCertificatesService.cs:30-35` — the member history reads the full review queue for every event and keeps one row | low | patch | Verified: `GetReviewQueueAsync` runs inside the event loop. Fix: read the actor's certificate with `GetByRegistrationIdAsync` instead of the whole queue. |

## Design Notes

- Decisions lock the owner `users` row then the event (AD-4) so they serialize against invalidation and issuance; the certificate row has no lock method and is written only under the event lock.
- The queue reads one event's confirmed registrations and pages in memory: the row set is bounded by event participation and the tab counts need the full set, matching the demo provider.
- `Stale` is the single refusal for a changed candidate; `NotFound` is reserved for a missing review and `Invalid` for a missing reason.

## Verification

**Commands:**
- `dotnet build` — expected: clean.
- `$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter "FullyQualifiedName~CertificateReviewDecisionTests|FullyQualifiedName~CertificateLedger"` — expected: all pass.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: green.

**Manual checks:**
- With `DemoData__Enabled=true`, open `/Certificates`, approve one review and decline another, and confirm the same rows and states as before.
