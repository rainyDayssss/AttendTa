---
title: 'Check-in validation engine and attendance recording'
type: 'feature'
ticket: '3'
created: '2026-11-07'
status: 'in-review'
baseline_revision: '4392e0935929ee89ec4c4054d65e69101c1117d9'
route: 'full'
route_source: 'auto'
risk: 'high'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
  - '_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The door scanner still records check-ins in the in-memory demo store (M-05), so no real attendance or audit row is written, eligibility is not revalidated, and the duplicate-scan guarantee is unproven.

**Approach:** Add a real attendance repository with a row lock and one shared recording engine that validates and writes exactly one attendance row plus one `attendance.recorded` audit entry in a caller-owned transaction. Wire the scanner to it through the real `ICheckInService`, then delete the M-05 demo provider and point the M-10a demo reviews at real attendance.

## Boundaries & Constraints

**Always:** Management only, actor from `ICurrentActor`. One transaction with lock order owner's users → event → attendance. Recheck event match, Published, check-in open, Confirmed, Eligible Participant and Valid Attendance inside the transaction, before any repeat decision. One attendance row per registration (DB unique index); a repeat returns the original time and writes nothing. An invalidated attendance is refused. One `attendance.recorded` entry whose payload excludes `qr_token`. UTC time from `TimeProvider`; scan method `qr_code`. Refusals reveal no name, student number or time. Preserve the scanner outcome JSON and screen shape.

**Never:** No manual-lookup wiring (4.5), invalidation (4.6), report overlay (4.6) or UI change. No registration-status change. No schema or EF migration. Do not remove M-06/M-07 providers or the demo store. No per-ticket Impeccable critique or polish.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid scan | Published, check-in open, Confirmed + eligible, no attendance | Success with name and student number; one attendance (`qr_code`, actor, UTC) and one audit row | No error expected |
| Repeat scan | Existing valid attendance | Repeat with the original time; no insert, no audit | No error expected |
| Blank token | Empty or whitespace | Invalid; no write | Refusal message |
| Token normalization | Uppercase or padded valid token | Success; token lowercased and trimmed | No error expected |
| Invalidated | Existing attendance with `invalidated_at` | Invalid; no write | Refusal message |
| Closed window | Published, `is_check_in_open` false | Closed; no write | Refusal message |
| Unpublished / completed | Draft, cancelled or completed event | Closed; no write | Refusal message |
| Unknown / wrong event | Missing or foreign token | Invalid; no write | Same message either way |
| Unconfirmed / cancelled | Registration not Confirmed | Invalid; no write | Refusal message |
| Ineligible | Inactive user or inactive membership | Invalid; no write | Refusal message |
| Refusal payload | Any refusal | No name, student number or recorded time | No record leaked |
| Attended but now refused | Existing attendance, registration now cancelled or ineligible | Invalid; refusal wins over repeat | No record leaked |
| Non-management | No Management role | Forbidden before any write | `DomainException(Forbidden)` |
| Concurrent duplicate | Two scans for one registration | One attendance and one audit; the second returns Repeat | Event lock serializes; unique index is the backstop |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Resources/Constants/AuditActions.cs`, `Messages/AuditDescriptions.cs`, `Messages/Audit.resx` — add `attendance.recorded` first; `ActivityLogWriter` throws on an unknown action.
- `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — add the `attendance` entity-type guard for the new action.
- `ASI.Basecode.Data/Interfaces/IAttendanceRepository.cs` — new: `LockAttendanceAsync(attendanceId)` (PK seek), `GetByRegistrationIdAsync`, `AddAsync`, `GetRecentForEventAsync` (with per-registration issued-certificate flag), `GetValidAttendedRegistrationIdsAsync`; log row record.
- `ASI.Basecode.Data/Repositories/AttendanceRepository.cs` — new; mirror `EventRepository.LockEventAsync` (`UPDLOCK, HOLDLOCK, ROWLOCK`, tracked merge). Log: join registration → member → user, order `recorded_at DESC, attendance_id DESC`, take 25.
- `ASI.Basecode.Services/Interfaces/IAttendanceRecordingService.cs` — new shared engine contract: `RecordAsync(eventId, registrationId, method)`.
- `ASI.Basecode.Services/Services/AttendanceRecordingService.cs` — new engine: `RequireManagement`, discovery, transaction, owner `LockUserAsync` → `LockEventAsync` → `LockAttendanceAsync`, rechecks, repeat/invalid/insert, audit.
- `ASI.Basecode.Services/Services/CheckInService.cs` — new real `ICheckInService`: published-event screen + real recent log; `ScanAsync` normalizes the token, resolves it through `FindConfirmedRegistrationIdByTokenAsync`, then calls the engine with `qr_code`.
- `ASI.Basecode.WebApp/Startup.DI.cs` — register `IAttendanceRepository`, `IAttendanceRecordingService`, `ICheckInService`.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs`, `DemoCheckInRegistration.cs` — remove the `ICheckInService` lines; keep M-06/M-07. Must land with the DI registration because `AddDemoData` runs last and overrides.
- Delete `ASI.Basecode.WebApp/DemoData/DemoCheckInService.cs`, `UnavailableCheckInService.cs`; update stale comments in `ICheckInService.cs`, `DemoDataRegistration.cs`, `DemoCheckInRegistration.cs`.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs`, `DemoCertificateLedgerService.cs` — M-10a hand-over: overload `EnsureCertificateReviews(eventId, registrationIds, now)` and have the ledger pass real valid-attendance ids from `IAttendanceRepository`.
- `ASI.Basecode.WebApp/Controllers/CheckInController.cs` — no change; it already returns the AD-9 JSON outcome.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-05 to `removed` with the commit; note M-10a now reads real attendance.
- `tests/ASI.Basecode.IntegrationTests/CheckInFlowTests.cs` — new focused smoke over real SQL.
- `tests/ASI.Basecode.IntegrationTests/DemoCheckInWindowFlagTests.cs` — constructs the deleted demo service; rebuild it on the real `CheckInService`.
- `tests/ASI.Basecode.IntegrationTests/CertificateLedgerTests.cs` — add the new `IAttendanceRepository` argument to the demo-ledger constructor.

## Tasks & Acceptance

**Execution:**
- [x] Audit action, description, resx and writer guard — land before the engine so `Append` resolves.
- [x] `IAttendanceRepository` + `AttendanceRepository` — lock, read, insert, recent log, valid-attendance ids.
- [x] `IAttendanceRecordingService` + `AttendanceRecordingService` — implement the engine; save attendance to obtain the identity, append the audit with that id, save again; re-read after the attendance lock.
- [x] `CheckInService` — real screen and scan; `ScanAsync` delegates to the engine with `qr_code`.
- [x] DI registration; delete the M-05 demo and unavailable providers; update the stale comments.
- [x] M-10a hand-over — after merging `upstream/dev`, 6.4's real completion hook owns review creation from real attendance; the demo ledger no longer creates reviews and the store helper only seeds the demo fixture.
- [x] `CheckInFlowTests`; rebuild `DemoCheckInWindowFlagTests`; `CertificateLedgerTests` taken from 6.4 after the merge.
- [x] Demo register M-05 row.

**Acceptance Criteria:**
- Given a published event with check-in open and a Confirmed eligible registration, when Management scans its token, then one attendance row (`qr_code`, actor, UTC) and one `attendance.recorded` audit row are committed and the outcome carries name and student number.
- Given that recorded attendance, when the same token is scanned again, then the outcome is Repeat with the original time and the attendance and audit counts do not change.
- Given a blank, unknown, wrong-event, unconfirmed, ineligible, invalidated, closed-window or unpublished request, when scanned, then the outcome is Closed or Invalid, no attendance or audit row is written, and no name, student number or time is returned.
- Given a registration that already attended but is now cancelled or ineligible, when scanned, then the refusal wins and no repeat identity is returned.
- Given the real service, when the scanner screen loads, then the selector and recent log read SQL, and the log orders newest first and caps at 25.
- Given the demo switch on, when the scanner scans a pass, then it writes real SQL and the M-05 provider is gone.
- Given a completed event with real attendance, when the event completes, then 6.4's real completion hook creates the pending review from real attendance (the M-10a hand-over).

## Implementation Notes

- The engine returns `AttendanceRecordingResult` instead of the scanner's `CheckInOutcomeViewModel`, so 4.5 can call the same validation and write path without depending on the screen contract. `CheckInService` maps the result onto the existing outcome JSON and keeps the controller unchanged.
- The transaction locks the participant's `users` row, then the event, then (when present) the attendance row. It re-reads the attendance by registration id after the event lock, so a concurrent first scan is seen before the repeat decision. The unique `registration_id` index remains the backstop: a `DomainException(Duplicate)` is caught outside the transaction and answered as Repeat or Invalid.
- The audit payload is `EventId`, `RegistrationId`, `CheckInMethodId` and `RecordedAt`; it never carries the QR token.
- `GetRecentForEventAsync` joins attendance → registration → member → user, orders by `recorded_at DESC, attendance_id DESC`, caps at 25 and carries the issued-certificate flag for `CanInvalidate`.
- M-10a hand-over: after merging `upstream/dev` (6.4, PR #194), the real completion hook creates pending reviews from real attendance, so the demo ledger no longer creates reviews. The merge took 6.4's `DemoCertificateLedgerService` and `DemoStore.SeedPendingReviewsForEvent`; the 4.3 M-10a test was removed because 6.4 owns that behavior and its `EventCompletionReviewTests` cover it.

## Plan Change Log

- 2026-11-07 implementation: the shared worktree was switched to the concurrent 6.4 branch mid-run. The 4.3 changes were stashed and re-applied on `feature/46/Check-in-validation-engine-and-attendance-recording` at `4392e09`, so this branch contained no 6.4 commits.
- 2026-11-07 conflict resolution: `upstream/dev` advanced to 6.4 (PR #194). Merging it resolved `DemoCertificateLedgerService` and `DemoStore` in 6.4's favour, took 6.4's `CertificateLedgerTests`, and removed the 4.3 M-10a test. The check-in engine, scanner replacement and M-05 removal are unchanged.

## Review Triage Log

## Design Notes

The engine is separate so 4.5 can call the same validation and write path from the manual adapter without depending on the scanner screen contract. It locks the participant's `users` row (not the recorder's), then the event, then the attendance row. `LockAttendanceAsync` takes the attendance primary key, matching AD-4's primary-key seek; the event lock serializes first inserts for one event, and the unique `registration_id` index is the final guard. If that guard still raises `DomainException(Duplicate)`, the engine re-reads and returns Repeat or Invalid. It reuses the existing `GetForCancellationAsync` as the fresh tracked registration read, so no shared interface changes and no fake updates are needed. `GetScreenAsync` keeps the published-only selector, `IsCompleted = false`, and the existing outcome JSON; the controller stays unchanged, and the engine's `Forbidden` is defence in depth behind the policy.

Known temporary limitations, owned by later tickets: M-06 shows real check-ins as not checked in until 4.5; M-07 cannot invalidate a real row until 4.6; the M-13c report overlay still reads the store until 4.6. M-10a is removed by 6.4's real completion hook.

## Verification

**Commands:**
- `dotnet build` — expected: full solution builds.
- `$env:ATTENDTA_TEST_SQL="Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter "FullyQualifiedName~CheckInFlowTests|FullyQualifiedName~DemoCheckInWindowFlagTests|FullyQualifiedName~CertificateLedgerTests"` — expected: all pass.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass.

**Matrix coverage:** `CheckInFlowTests` proves the valid, repeat, blank, normalization, invalidated, closed-window, unpublished, unknown, unconfirmed, ineligible, refusal-payload, attended-but-refused and Forbidden rows, plus the audit row and the screen read. `DemoCheckInWindowFlagTests` proves the scanner and manual lookup read the real flag and that the demo switch still resolves the real `ICheckInService`. The true parallel duplicate race is 8.5 (#78); this story proves the sequential repeat and the lock path.

**Manual checks (if no CLI):**
- With a seeded published event and open window, scan a confirmed pass, see the name and student number, scan again and see the original time; close the window and confirm the refusal writes nothing.
