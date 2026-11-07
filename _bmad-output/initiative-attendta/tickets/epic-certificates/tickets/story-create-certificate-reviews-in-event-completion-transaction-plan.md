---
title: 'Create certificate reviews in event completion transaction'
type: 'feature'
ticket: '4'
created: '2026-11-07'
status: 'built'
baseline_revision: '4392e0935929ee89ec4c4054d65e69101c1117d9'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: [quick]
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Completing an event records no certificate eligibility. The demo ledger fabricates pending reviews in memory (M-10a), so real completed events never produce review rows and the real approval/issuance path has nothing to act on.

**Approach:** Implement the existing `IEventCompletionHandler` hook to insert one `pending_approval` Certificate per Registration with Valid Attendance through a new `CertificateRepository`, inside the completion transaction, then remove the demo review-creation path without changing the demo screen.

## Boundaries & Constraints

**Always:** Run inside the caller-owned completion transaction; use `ParticipationFacts.ValidAttendance`; set `created_by` to the completing officer and `created_at` to the completion time; skip registrations that already have a certificate row; keep the demo ledger visually unchanged.

**Never:** Add a second completion path or screen; call the renderer, PDF or private file store; write a second audit entry (R1 only); change frontend views; implement 6.5–6.9 decisions, issuance or downloads.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | Published past event, confirmed registration with valid attendance | One `pending_approval` row with officer and completion time | No error |
| Invalidated attendance | Attendance has `invalidated_at` | No row | No error |
| Absent attendee | Confirmed registration with no attendance | No row | No error |
| Existing review | Registration already has a certificate row | Existing row unchanged, no duplicate | Unique index not reached |
| Later handler fails | Real handler inserts rows, then another handler throws | Every inserted review rolls back with the event | Exception propagates |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Interfaces/IEventCompletionHandler.cs` — hook contract; do not change.
- `ASI.Basecode.Services/ServiceModels/EventCompletionContext.cs` — `EventId`, `CompletedBy`, `CompletedAt`.
- `ASI.Basecode.Services/Services/EventLifecycleService.cs:407-413` — invokes handlers after save, inside the transaction; do not add another call.
- `ASI.Basecode.Data/Models/Certificate.cs`, `Models/Lookups/LookupCodes.cs:46` — row shape and `PendingApproval`.
- `ASI.Basecode.Data/Queries/ParticipationFacts.cs:15` — `ValidAttendance` (`InvalidatedAt == null`).
- `ASI.Basecode.Data/Repositories/RegistrationRepository.cs:155-162` — valid-attendance join to copy.
- `ASI.Basecode.Data/Repositories/BaseRepository.cs`, `ASI.Basecode.Data/UnitOfWork.cs` — `GetDbSet`, `SaveChangesAsync`, transaction ownership.
- `ASI.Basecode.WebApp/Startup.DI.cs:35-71` — services block and repositories block.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs:76-87,188-198` — M-10a creation path to remove.
- `ASI.Basecode.WebApp/DemoData/DemoCertificateLedgerService.cs:33` — query-time creation call to remove.
- `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs:145` — demo seed keeps explicit review seeding.
- `ASI.Basecode.WebApp/DemoData/DemoCheckInService.cs:148` — demo check-ins write store-only attendance; the certificate demo event is seeded, so no review path is needed.
- `tests/ASI.Basecode.IntegrationTests/CertificateLedgerTests.cs:21,174-197` — `Setup` must seed reviews directly; rename the stale test.
- `tests/ASI.Basecode.IntegrationTests/CertificateLedgerFlowTests.cs:169` — full-app completion asserts no SQL rows because demo attendance is store-only.
- `tests/ASI.Basecode.IntegrationTests/EventDraftFlowTests.cs:548-577` — `Service` already accepts `completionHandlers`.
- `_bmad-output/initiative-attendta/demo-mock-register.md:424` — set M-10a to `removed` with the implementation commit.

## Tasks & Acceptance

**Execution:**
- [ ] `ASI.Basecode.Data/Interfaces/ICertificateRepository.cs` — add `Task<IReadOnlyList<int>> GetRegistrationsAwaitingReviewAsync(int eventId, CancellationToken ct = default)` and `Task AddRangeAsync(IReadOnlyList<Certificate> certificates, CancellationToken ct = default)` — the Data seam the story names.
- [ ] `ASI.Basecode.Data/Repositories/CertificateRepository.cs` — implement the query with `ParticipationFacts.ValidAttendance`, exclude registrations that already have a certificate row, and add rows without saving.
- [ ] `ASI.Basecode.Services/Services/CertificateReviewCompletionHandler.cs` — constructor `(ICertificateRepository, IUnitOfWork)`; query, add one `PendingApproval` row per registration with `CreatedBy`/`CreatedAt` from the context, then `SaveChangesAsync`; never call `ExecuteInTransactionAsync`.
- [ ] `ASI.Basecode.WebApp/Startup.DI.cs` — register `ICertificateRepository` in the repositories block and `IEventCompletionHandler` in the services block.
- [ ] `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — stop `MarkCompleted` creating reviews; keep `IsCompleted`; rename the helper to `SeedPendingReviewsForEvent`; update the stale M-10a comments.
- [ ] `ASI.Basecode.WebApp/DemoData/DemoCertificateLedgerService.cs` — remove the query-time review creation.
- [ ] `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs` — call the renamed seed helper.
- [ ] `tests/ASI.Basecode.IntegrationTests/CertificateLedgerTests.cs` — after `InvalidateAttendance`, call `SeedPendingReviewsForEvent` (it filters valid attendance) before `MarkCompleted`; rename `LedgerCreatesReviewsForEveryValidAttendeeAndPagesBothTabs`.
- [ ] `tests/ASI.Basecode.IntegrationTests/EventCompletionReviewTests.cs` — add focused tests: valid-only creation (assert `CreatedBy`, `CreatedAt` and exactly one `event.completed` audit row), rollback with `[certificateHandler, throwingHandler]` where the throwing handler records the uncommitted row count before throwing, no-duplicate, and a DI assertion that `CertificateReviewCompletionHandler` resolves.
- [ ] `_bmad-output/initiative-attendta/demo-mock-register.md` — mark M-10a removed with the implementation commit.

**Acceptance Criteria:**
- Given a published event with one valid, one invalidated and one absent attendee, when an officer completes it, then only the valid registration gets a `pending_approval` row with `created_by` = officer and `created_at` = completion time, and exactly one `event.completed` audit row exists.
- Given a completion where a later handler throws, when completion runs, then the throwing handler saw the inserted row, no certificate rows persist, and the event stays Published.
- Given a registration that already has a certificate row, when completion runs, then the row is unchanged and no duplicate is inserted.
- Given the application's DI container, when completion handlers are resolved, then `CertificateReviewCompletionHandler` is present.
- Given `DemoData:Enabled=true`, when the demo event is completed, then the certificate ledger still shows the same seeded review states.

## Implementation Notes

## Plan Change Log

- 2026-11-07 plan review:
  - High — ledger test seeding would over-count invalidated attendance; amended to call the filtered `SeedPendingReviewsForEvent` after invalidation.
  - Medium — rollback test could pass vacuously; amended to order `[certificateHandler, throwingHandler]` and prove the uncommitted row was visible before the throw.
  - Medium — handler must share the caller's `IUnitOfWork` and must not open a transaction; amended constructor and task.
  - Medium — added a DI registration guard and `CertificateLedgerFlowTests` to verification.
  - Medium — resolved the "no new automated tests" wording: the focused test is the story's required completion-to-review smoke; broad authoring stays 8.5.
  - Low — fixed the register line, DI block, repository signatures, assertions, stale comments and test name.

## Review Triage Log

| Finding | Verdict | Route | Evidence / action |
|---|---|---|---|
| Live demo check-ins on the certificate event disappear from the demo ledger after startup (`DemoCertificateLedgerService.cs:33`, `CertificateLedgerFlowTests.cs:169`) | low | rejected | The story's acceptance covers the seeded demo states, which are unchanged; the scripted demo checks in on Founders' Week, not the certificate event. 4.3's real attendance writes SQL and 6.6's real ledger reads it, closing the path. Re-adding read-time creation would restore M-10a. |
| `CertificateLedgerTests` no longer proves the ledger reflects completion | low | rejected | Real completion is proven by `EventCompletionReviewTests`; the demo ledger test now proves seeded paging, and test authoring is 8.5. |
| Stale M-10a text in `demo-mock-register.md:136,429` | low | rejected | The M-10a row and removal paragraph already record the removal; the remaining sentences are superseded and the file is concurrently edited by the 4.3 ticket. Noted as a follow-up. |
| Concurrent completions can hit the unique index | false | rejected | `LockEventAsync` uses UPDLOCK/HOLDLOCK; the second completion blocks, rechecks status and throws Stale/Invalid before the handler. |
| Handler does not verify an open transaction | false | rejected | The only caller wraps the handler loop in `ExecuteInTransactionAsync` and shares the scoped `IUnitOfWork`; no path invokes it without a transaction. |
| Audit assertion does not check `ActorUserId` | low | patched | Added `Assert.Equal(officerId, log.ActorUserId)` in `EventCompletionReviewTests`. |
| DI comment overstates the shared-unit-of-work guarantee | false | rejected | The comment describes the current scoped registration; no defect. |
| `SeedPendingReviewsForEvent` is a public demo API | low | rejected | `DemoStore` is the demo seam; the method is startup/test-only and documented. |
| `MarkCompleted` comment names removed M-13b | low | patched | Updated the comment to name only the participation-report overlay fixture. |
| `completionHandlerFactory` overlaps `completionHandlers` | low | rejected | No caller passes both; the factory is the only way to share the caller's unit of work in tests. |
| Repository has no event-status guard | false | rejected | The only caller runs after the event is saved as Completed; the precondition is documented in the plan. |
| Demo ledger comment implies it now reflects real reviews | low | rejected | Cosmetic; the comment says it no longer creates reviews, which is accurate. |
| `CertificateStatusId` duplicates the column default | false | rejected | Explicit status is clearer and matches the lookup enum; no defect. |
| Rollback test reads through a second context | low | rejected | The test passed; the second context sees the uncommitted row because the transaction is open, which is the fact under test. |
| Register paragraph conflates the ledger with the seed | low | rejected | Documentation imprecision; the removal paragraph is otherwise accurate. Follow-up with the 4.3 register edit. |
| Seed order makes the helper filter redundant | false | rejected | Both guards are correct; no defect. |
| Seed helper iterates under the lock | false | rejected | Startup/test-only, bounded by the demo store size; no request-path caller. |
| Added `using` in `EventDraftFlowTests` | false | rejected | Required for the new `IUnitOfWork` parameter. |
| `AddRangeAsync` ignores the token | false | rejected | Matches `RegistrationRepository.AddAsync`; the caller's `SaveChanges` honors the token. |
| Early return skips `SaveChanges` | false | rejected | Nothing is staged on that path; correct. |
| Ledger read-side mismatch for live check-ins | low | rejected | Same root cause as the first row; grouped. |

## Design Notes

- No registration-status filter: Valid Attendance is the canonical fact, and cancellation is refused while valid attendance exists. A cancelled row with valid attendance would be a data inconsistency, not a normal completion case.
- The actor comes from `EventCompletionContext.CompletedBy`, not `ICurrentActor`, because the completion use case already resolved and authorized it.
- The handler saves through the caller's `UnitOfWork` but never opens a transaction; a second `UnitOfWork` would see depth zero and could roll back the caller.
- Demo live check-ins are on a different event; the certificate demo event's reviews are seeded, so removing the query-time creation does not change the demo screen.

## Verification

**Commands:**
- `dotnet build` — expected: clean build.
- `$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter "FullyQualifiedName~EventCompletionReviewTests|FullyQualifiedName~EventCompletionServiceTests|FullyQualifiedName~CertificateLedgerTests|FullyQualifiedName~CertificateLedgerFlowTests"` — expected: all pass.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: green.

**Manual checks:**
- With `DemoData__Enabled=true`, complete the demo event and confirm the ledger still shows the same pending/declined/issued rows.
