---
title: 'Event cancellation with atomic registration cancellation and check-in closure'
type: 'feature'
ticket: '11'
created: '2026-11-06'
status: 'built'
baseline_revision: 'a16daa77560519dc3315ed665e71d5d63b67502e'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - 'AGENTS.md'
  - '_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Management cannot cancel an event, so a cancelled event's registrations, QR passes and check-in stay live.

**Approach:** Add a management-only cancellation command on `IEventLifecycleService` that locks the event, refuses when valid attendance or an issued certificate exists, cancels every open registration through `IRegistrationCancellationService`, closes check-in, and records cancellation metadata plus one `event.cancelled` audit entry in the same transaction, with a confirmation screen.

## Boundaries & Constraints

**Always:** Require management authorization and antiforgery. Run one caller-owned transaction and take only the event lock, in AD-4 order. Recheck the event status after locking. Use `ParticipationFacts.ValidAttendance` and a Data-layer certificate read. Validate a nonblank reason inside the transaction. Call `IRegistrationCancellationService.CancelAllForEventAsync(EventCancelled)` so it remains the only code that cancels registrations and clears QR tokens. Set `is_check_in_open = 0`, closing `check_in_closed_by/at` only when it was open. Record `cancelled_by`, `cancelled_at` and `cancellation_reason`, and write exactly one `event.cancelled` entry. Capture injected UTC time after locks. No promotion.

**Never:** Cancel Completed or Cancelled events; hard-delete; write registrations directly; promote waitlisted rows; change the schema; log QR tokens or file paths; implement completion (3.12), the check-in toggle (4.2) or other modules' screens.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Published or draft cancel | Management actor, nonblank reason, no valid attendance, no issued certificate | Event Cancelled; all pending, confirmed and waitlisted registrations Cancelled with QR cleared; check-in closed; one `event.cancelled` plus one `registration.cancelled` per newly cancelled row | Success notification |
| Valid attendance | Any registration of the event has a non-invalidated attendance row | No changes | `Invalid` refusal |
| Issued certificate | Any registration of the event has an issued certificate | No changes | `Invalid` refusal |
| Terminal or missing | Completed, Cancelled or unknown event | No changes | `Invalid` or `NotFound` |
| Invalid request | Blank reason, non-management actor, anonymous, missing antiforgery | No changes | `Invalid`, `Forbidden`, sign-in or 400 |
| Stale state | Status or references change before the lock | No changes | `Stale` refusal |
| Persistence failure | Audit or save fails | Event, registrations and audit roll back together | Existing failure handling |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Interfaces/IEventLifecycleService.cs` — add `CancelAsync(int eventId, string reason, CancellationToken)`.
- `ASI.Basecode.Services/Services/EventLifecycleService.cs` — reuse `RequireManagementActor`, `ExecuteInTransactionAsync`, `LockEventAsync`, `UpdateEventAsync`, `_audit.Append` and `SaveChangesAsync`; inject `IRegistrationRepository` and `IRegistrationCancellationService`; add a private `EventCancellationAuditValues` record.
- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs`, `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` — add `HasValidAttendanceForEventAsync(eventId)` and `HasIssuedCertificateForEventAsync(eventId)`, reusing `ParticipationFacts.ValidAttendances` and `CertificateStatusCode.Issued`. Reads only.
- `ASI.Basecode.Resources/Constants/AuditActions.cs`, `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `ASI.Basecode.Resources/Messages/Audit.resx`, `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — register `event.cancelled`, its description and the `events` entity-type constraint.
- `ASI.Basecode.Services/ServiceModels/EventCancelViewModel.cs` — new confirmation model: event id, title, status, start, required reason.
- `ASI.Basecode.WebApp/Controllers/EventController.cs` — GET/POST `Cancel`; the controller already has the Management policy and antiforgery. Redirect with `TempData` and map `DomainException` like `Publish`.
- `ASI.Basecode.WebApp/Views/Event/Cancel.cshtml`, `Views/Event/Index.cshtml` — confirmation form and a Cancel action for Draft/Published rows only.
- `tests/ASI.Basecode.Services.Tests/EventLifecycleServiceTests.cs`, `EventCoverServiceTests.cs` — extend the harnesses with a fake registration repository and cancellation service and update the `EventLifecycleService` constructor; cover the matrix, lock order, audit payloads and rollback.
- `tests/ASI.Basecode.Services.Tests/{MemberEventServiceTests,CertificateTracerServiceTests}.cs`, `tests/ASI.Basecode.IntegrationTests/{MemberEventFlowTests,RegistrationCancellationTests}.cs` — update `IRegistrationRepository` fakes and wrappers for the two new reads.
- `tests/ASI.Basecode.IntegrationTests/EventCancellationFlowTests.cs` — new SQL-backed service and authenticated HTTP coverage.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs`, `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` — add the event-wide valid-attendance and issued-certificate reads — the service needs canonical facts without writing another module's tables.
- [x] `ASI.Basecode.Services/Interfaces/IEventLifecycleService.cs`, `ASI.Basecode.Services/Services/EventLifecycleService.cs` — implement `CancelAsync` with the event lock, guards, cancellation-service call, check-in closure, metadata and audit — the atomic core of the story.
- [x] `ASI.Basecode.Resources/Constants/AuditActions.cs`, `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `ASI.Basecode.Resources/Messages/Audit.resx`, `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — add the `event.cancelled` vocabulary and constraint — keeps the audit vocabulary closed.
- [x] `ASI.Basecode.Services/ServiceModels/EventCancelViewModel.cs`, `ASI.Basecode.WebApp/Controllers/EventController.cs`, `ASI.Basecode.WebApp/Views/Event/Cancel.cshtml`, `ASI.Basecode.WebApp/Views/Event/Index.cshtml` — add the confirmation screen and list action — gives officers the required reason entry point.
- [x] `tests/ASI.Basecode.Services.Tests/`, `tests/ASI.Basecode.IntegrationTests/` — cover the matrix, fakes, rollback, audit privacy and the HTTP flow — proves the behavior without relying on the UI.

**Acceptance Criteria:**
- Given a Published event with pending, confirmed and waitlisted registrations and open check-in, when an officer cancels with a reason, then the event is Cancelled, all open registrations are Cancelled with QR tokens cleared, check-in is closed with actor and time, cancellation metadata is recorded, and one `event.cancelled` plus one `registration.cancelled` per newly cancelled registration are logged.
- Given a Draft event, when cancelled, then it becomes Cancelled with the same audit and no other event changes.
- Given valid attendance or an issued certificate, when cancellation is attempted, then it is refused as `Invalid` and nothing changes.
- Given a Completed or Cancelled event, or a blank reason, when cancellation is attempted, then it is refused and nothing changes.
- Given a non-management or anonymous request, when cancellation is attempted, then it is forbidden or sent to sign-in and nothing changes.
- Given a persistence failure, when cancellation is attempted, then the event, registrations and audit roll back together.
- Given the management event list, when an event is Draft or Published, then a Cancel action is shown; terminal events show none.

## Implementation Notes

- `CancelAsync` discovers the event outside the transaction for `NotFound`/terminal-state refusals, then runs one `ExecuteInTransactionAsync` that takes only `LockEventAsync`, rechecks the status (`Stale` on a change), validates the trimmed reason, reads the canonical attendance and certificate facts, calls `IRegistrationCancellationService.CancelAllForEventAsync(EventCancelled)`, and only then writes the event row and the single `event.cancelled` audit entry. `CancelAllForEventAsync` saves its own rows inside the same transaction, so a later failure rolls back registrations, event and audit together.
- `HasValidAttendanceForEventAsync` composes `ParticipationFacts.ValidAttendances`; `HasIssuedCertificateForEventAsync` reads `certificates` for `Issued` rows through the registration join. Both are `AsNoTracking` reads and neither writes another module's tables.
- `EventCancellationAuditValues` records status, `IsCheckInOpen`, `CancelledBy`, `CancelledAt` and `CancellationReason` only; the `Reason` field carries the trimmed reason. No QR token, file path or image path enters the payload, and `ActivityLogWriter` now constrains `event.cancelled` to the `events` entity type.
- `is_check_in_open` is always cleared. `check_in_closed_by/at` are written only when the flag was open, so an earlier closure is preserved.
- The confirmation screen posts `EventId` plus the required `Reason`; blank reasons are re-rendered with a field error, while service refusals follow `Publish`'s `TempData["ErrorMessage"]` + redirect pattern.
- Verification on 2026-11-06: `dotnet build` passed with 0 warnings; Services tests passed 214/214; Integration tests passed 287/292. The 5 failures are the pre-existing `CertificateTracerFlowTests`, which need the Playwright Chromium executable (`pwsh bin/Debug/netX/playwright.ps1 install`) and are unrelated to this change. All new `EventCancellationServiceTests` and `EventCancellationFlowTests` passed.
- Final verification after installing Playwright Chromium: build 0 warnings; Services 214/214; Integration 288/292. The 4 remaining `CertificateTracerFlowTests` failures need Poppler's `pdftotext` (`ATTENDTA_PDFTOTEXT`) and are unrelated to this change. All 284 non-certificate integration tests passed, including the 11 cancellation tests.
- The workspace also contained uncommitted waitlist-promotion work from a parallel session; this change is additive and did not modify or revert that work.

## Plan Change Log

## Review Triage Log

## Design Notes

Event cancellation is an event-only lock per AD-4, so the attendance and certificate reads are protected because those rows are written only while holding the event lock. The cancellation service saves its own rows inside the caller's transaction; the event update and `event.cancelled` audit are saved after it, and any failure rolls the whole transaction back. `is_check_in_open` is always cleared, but `check_in_closed_by/at` are written only when the flag was open, preserving an earlier closure. The reason is trimmed and required; no new server-side length rule is invented, matching the existing review-reason behavior.

## Verification

**Commands:**
- `dotnet build` — expected: succeeds with no warnings.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass.
- `$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — expected: all pass, including the new SQL and HTTP cancellation tests.
