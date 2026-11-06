---
title: 'Member self-cancellation and re-registration refusal'
type: 'feature'
ticket: '9'
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

**Problem:** A member with a pending, confirmed or waitlisted registration has no way to withdraw it, so the seat stays held and the member cannot free themselves before the event starts.

**Approach:** Add a member-owned cancel command on the published event detail page. It locks the owner's `users` row then the event, rechecks ownership, status, publication, the Event Start Cutoff and valid attendance, then delegates to `IRegistrationCancellationService.CancelRegistrationAsync` with `MemberRequested`. The existing unique key keeps re-registration refused.

## Boundaries & Constraints

**Always:** Authenticated owner only; POST with antiforgery. Discover the actor's registration, then lock `users` → `events` and recheck everything inside one caller-owned transaction. Use `ParticipationFacts.StartCutoffPassed` against the injected `TimeProvider`; capture time after both locks. Allow only Pending, Confirmed or Waitlisted rows on a Published event, strictly before start, with no valid attendance. Let `IRegistrationCancellationService` remain the only writer that cancels a registration, clears the QR token and writes `registration.cancelled`. Show the warning and outcome through the existing TempData notices.

**Never:** Require a cancellation reason from the member, require an active membership to cancel, promote a waitlisted row, cancel another member's registration, cancel after start or with valid attendance, change the schema, add My Events (3.13) or the "Not confirmed before cutoff" badge (3.10), or log QR tokens.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Cancel | Owner, Pending/Confirmed/Waitlisted, Published, one tick before start, no valid attendance | Row Cancelled, QR cleared, actor/time/reason recorded, one `registration.cancelled` audit | Success notice |
| At or after start | Same, `now >= starts_at` | No mutation | `CutoffPassed` |
| Valid attendance | Attendance row not invalidated | No mutation | `Invalid` |
| Not owner / no row | Actor has no registration for the event | No mutation | `NotFound` |
| Stale | Event no longer Published or registration changed before the lock | No mutation | `Stale` |
| Already cancelled | Row Cancelled | No mutation | `Stale` |
| Anonymous / forged | No cookie or no antiforgery token | No mutation | Sign-in redirect or 400 |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs` and `Repositories/RegistrationRepository.cs` — add `GetMemberRegistrationAsync(eventId, userId)` returning `MemberRegistrationRow(RegistrationId, MemberId, EventId, Status)` or null; join registrations → members on `m.UserId == userId`. Reuse `HasValidAttendanceAsync` and `GetForCancellationAsync`; keep `ExistsAsync` for submission.
- `ASI.Basecode.Services/Interfaces/IMemberEventService.cs` and `Services/MemberEventService.cs` — add `CancelRegistrationAsync(int eventId, CancellationToken)`; follow `RegisterAsync` for `RequireAuthenticatedActor`, `ExecuteInTransactionAsync`, `_users.LockUserAsync` → `_members.GetMemberByUserIdAsync` → `_events.LockEventAsync`, clock capture after locks, and `DomainException` outcomes. Inject `IRegistrationCancellationService` (already registered in `Startup.DI.cs`). Populate the detail registration fields in `GetDetailAsync` in place of the `ExistsAsync` boolean.
- `ASI.Basecode.Services/ServiceModels/MemberEventDetailViewModel.cs` — add `RegistrationId`, `RegistrationStatus`, `CanCancel`. `CanCancel` mirrors the server guards so the button is not offered when the POST would fail.
- `ASI.Basecode.WebApp/Controllers/MemberEventsController.cs` — add `[HttpPost] Cancel(int id, CancellationToken)` beside `Register`; map `DomainException` to `TempData["ErrorMessage"]` and redirect to `Details`, or to the feed for `NotFound`/`Stale`.
- `ASI.Basecode.WebApp/Views/MemberEvents/Details.cshtml` — show the registration status badge and, when `CanCancel`, a native `<dialog>` warning that cancellation is final with a POST form and a "Keep my registration" close button; add the small open/close script in the page's `Scripts` section. Show the cancelled state without a re-registration form.
- `ASI.Basecode.WebApp/Styles/app.css` — add `.button-danger` and `.confirm-dialog` component styles; rebuild `wwwroot/css/app.css` with `npm run css:build`.
- `tests/ASI.Basecode.Services.Tests/MemberEventServiceTests.cs` — add a fake `IRegistrationCancellationService` and `GetMemberRegistrationAsync` to the harness; cover the matrix, lock order, clock-after-locks, and detail `CanCancel`.
- `tests/ASI.Basecode.Services.Tests/CertificateTracerServiceTests.cs`, `tests/ASI.Basecode.IntegrationTests/MemberEventFlowTests.cs` (`StaleExistenceRegistrationRepository`, `MemberEventTestSupport.Service`), and `tests/ASI.Basecode.IntegrationTests/RegistrationCancellationTests.cs` (`ReverseDiscoveryRepository`) — update the `IRegistrationRepository` fakes/wrappers and the `MemberEventService` constructor.
- `tests/ASI.Basecode.IntegrationTests/MemberEventFlowTests.cs` or a new `MemberSelfCancellationTests.cs` — SQL-backed HTTP and service coverage; reuse `MemberEventTestSupport`, `SqlServerDatabaseFixture`, `AttendTaWebApplicationFactory`, `FixedClock`, and the antiforgery/sign-in helpers.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs`, `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` — add the owner-scoped registration read — the command must discover the actor's row before locking.
- [x] `ASI.Basecode.Services/Interfaces/IMemberEventService.cs`, `ASI.Basecode.Services/Services/MemberEventService.cs`, `ASI.Basecode.Services/ServiceModels/MemberEventDetailViewModel.cs` — add the cancel command with locks, guards and cancellation delegation; populate the detail fields — reuse the existing registration and cancellation patterns.
- [x] `ASI.Basecode.WebApp/Controllers/MemberEventsController.cs`, `ASI.Basecode.WebApp/Views/MemberEvents/Details.cshtml`, `ASI.Basecode.WebApp/Styles/app.css` — expose the status and confirmation dialog, POST action and danger styles — the member needs an explicit final warning.
- [x] `tests/ASI.Basecode.Services.Tests/MemberEventServiceTests.cs`, `tests/ASI.Basecode.Services.Tests/CertificateTracerServiceTests.cs`, `tests/ASI.Basecode.IntegrationTests/MemberEventFlowTests.cs`, `tests/ASI.Basecode.IntegrationTests/RegistrationCancellationTests.cs` — update fakes and cover the matrix, boundary, ownership, audit and re-registration refusal — prove the server guards, not only the UI.
- [x] Rebuild the compiled Tailwind stylesheet and run build plus both test projects — confirm no regressions.

**Acceptance Criteria:**
- Given a member with a confirmed registration and a published event one tick before start, when they confirm the warning, then the registration is Cancelled, the QR token is cleared, the actor/time/reason are recorded, one `registration.cancelled` entry exists, and a second registration attempt is refused with `Duplicate`.
- Given the same state at or after start, or with valid attendance, or with no registration, when cancellation is posted, then no row or audit changes and the matching `CutoffPassed`, `Invalid` or `NotFound` outcome is returned.
- Given a non-owner, anonymous request or a POST without antiforgery, when cancellation is attempted, then the other member's registration is untouched and the request is refused.
- Given the detail page, when the registration is Pending, Confirmed or Waitlisted before start without valid attendance, then the status badge and Cancel action are shown; otherwise the action is absent.

## Implementation Notes

- `RegistrationRepository.GetMemberRegistrationAsync` joins `registrations` to `members` on `member.user_id`, so the row is owner-scoped in SQL; `ExistsAsync` stays the submission guard.
- `MemberEventService.CancelRegistrationAsync` runs one caller-owned transaction: discover the owner's row, lock `users`, resolve the member, lock the event, capture the clock, then recheck publication, the start cutoff, the row identity/status and valid attendance before delegating to `IRegistrationCancellationService.CancelRegistrationAsync(..., MemberRequested, null, ct)`. The blank reason lets the cancellation service store its canonical "Member requested cancellation" text, so the member is never asked for one.
- `GetDetailAsync` now projects `RegistrationId`, `RegistrationStatus`, `CanCancel` from the owner read instead of the `ExistsAsync` boolean; `CanCancel` mirrors every server guard and deliberately does not require an active membership.
- The detail page keeps the existing "You already have a registration for this event." notice, adds a status badge, and renders a native `<dialog>` with a POST form and a "Keep my registration" close button only when `CanCancel` holds. A cancelled row shows its state and no re-registration form.
- New SQL-backed coverage lives in `tests/ASI.Basecode.IntegrationTests/MemberSelfCancellationTests.cs` (HTTP happy path, cutoff boundary, valid attendance, non-owner, stale event, anonymous/forged POST, deactivated membership, and lock order plus audit). Unit coverage in `MemberEventServiceTests` covers the same matrix with a fake cancellation service.

## Plan Change Log

## Review Triage Log

## Design Notes

The detail page is the host because My Events (3.13) does not exist yet; 3.13 will reuse the same service command. The confirmation is a native `<dialog>` so focus enters it and Escape closes it without a JS library. `GetDetailAsync` must not require an active membership: a deactivated member can still withdraw an existing registration; the account-deactivation cascade belongs to epic-administration.

## Verification

**Commands:**
- `dotnet build` — expected: succeeds with no errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all tests pass.
- `$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — expected: all tests pass, including the new SQL-backed cancellation coverage.

**Result (2026-11-06):**
- `dotnet build` — succeeded, 0 warnings, 0 errors.
- `npm run css:build` — rebuilt `wwwroot/css/app.css` with `.button-danger`, `.confirm-dialog`, `.confirm-dialog::backdrop` and `.confirm-dialog-actions`.
- Service tests — 216 passed, 0 failed (run as `tests/ASI.Basecode.Services.Tests/bin/Debug/net10.0/ASI.Basecode.Services.Tests.exe -noColor`).
- Integration tests — 276 total, 272 passed, 4 failed. All 4 failures are `CertificateTracerFlowTests`, which require Poppler's `pdftotext` on `PATH` (or `ATTENDTA_PDFTOTEXT`); the tool is not installed in this environment and the failures are unrelated to this change.
- `dotnet test` reports "Zero tests ran" (exit code 5) for both test projects in this environment, including at baseline revision `a16daa7`; the xUnit v3 in-process runner executables were used instead.
