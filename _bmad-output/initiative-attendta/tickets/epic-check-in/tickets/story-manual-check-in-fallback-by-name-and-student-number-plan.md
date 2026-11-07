---
title: 'Manual check-in fallback by name and student number'
type: 'feature'
ticket: '5'
created: '2026-11-07'
status: 'built'
baseline_revision: 'cf955f70ba2872599f6acea3d1aa0a19f50bcf5d'
route: 'full'
route_source: 'auto'
risk: 'medium'
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

**Problem:** The manual lookup and manual Check in still run in the M-06 demo provider: it reads up to 500 confirmed rows into memory, filters there, and records check-ins only in the demo store, so no real attendance or audit row is written.

**Approach:** Implement the real `ICheckInLookupService` with a registration-scoped, server-paged confirmed-participant search and delegate the explicit Check in to the shared `IAttendanceRecordingService` with `manual_search` or `student_number`; delete the M-06 demo and switch-off providers and register the real service in DI.

## Boundaries & Constraints

**Always:** Management policy and antiforgery stay on the controller; the actor comes from `ICurrentActor` inside the engine. Search is read-only. Search covers Confirmed registrations of the selected event, matches a name or student-number substring, orders by name then registration id, and pages at 25 through `PageRequest`. Check in uses the same engine validations, lock order and `attendance.recorded` audit as a scan; the method is `student_number` only when the trimmed query equals the row's student number, otherwise `manual_search`. The manual closed-window message stays "Check-in is closed for this event. Open it before checking in." With the demo switch off, the real service serves both states, as 4.3 did for scanning. Keep the controller, view and outcome shape unchanged. UTC time comes from `TimeProvider`; no schema or EF migration.

**Never:** No invalidation (4.6), camera (4.4) or member-history (4.7) work. No demo-store writes or new demo data. No registration-status change. No per-ticket Impeccable critique or polish (8.12). Do not touch the untracked 4.4 draft plan.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Search | Name fragment or student number on a published event | Paged confirmed matches with checked-in/invalidated state; no write | No error expected |
| Blank or no match | Empty query, or no confirmed match | Empty lookup state; `TotalCount` 0 | No error expected |
| Second page | More than 25 matches | Next 25 rows, correct `TotalPages` | No error expected |
| Check in | Open published event, confirmed eligible, no attendance | Success; attendance `student_number` or `manual_search`; one audit row | No error expected |
| Repeat | Existing valid attendance | Repeat with the original time; no insert or audit | No error expected |
| Closed window / unpublished | Closed, draft, completed or cancelled event | Closed; no write | Refusal message |
| Refused registration | Unconfirmed, cancelled, ineligible, invalidated or other event | Invalid; no write; no identity | Refusal message |
| Non-management | Member role | Forbidden before any write | `DomainException(Forbidden)` |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs` — add `ConfirmedLookupRow` and the count, page and per-registration student-number reads.
- `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` — a dedicated confirmed-lookup query (event + Confirmed, name/student-number `Contains`, left join to attendance, name-then-id order, `Skip/Take`); do not change `ReviewQueue`, which other screens order by `SubmittedAt`.
- `ASI.Basecode.Services/Interfaces/ICheckInLookupService.cs` — update the stale M-06 doc comment; contract unchanged.
- `ASI.Basecode.Services/Services/CheckInLookupService.cs` — new: `SearchAsync` + `CheckInAsync`; the latter pre-checks the published event and open window, reads the row's student number to choose the method, then delegates to `IAttendanceRecordingService`.
- `ASI.Basecode.Services/ServiceModels/CheckInScreenViewModel.cs` — add a shared `CheckInOutcomeViewModel.FromRecordingResult` mapper.
- `ASI.Basecode.Services/Services/CheckInService.cs` — use that mapper in place of the private `ToOutcome`; no behavior change, `CheckInFlowTests` must stay green.
- `ASI.Basecode.WebApp/Startup.DI.cs` — register `ICheckInLookupService` as `CheckInLookupService` next to `ICheckInService`. `AddDemoData` runs later and must not re-register this contract.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs`, `DemoCheckInRegistration.cs` — remove the M-06 lookup lines and stale comments; keep invalidation.
- Delete `ASI.Basecode.WebApp/DemoData/DemoCheckInLookupService.cs` and `UnavailableCheckInLookupService.cs`.
- `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs` — drop M-05/M-06 from the active-demo log line.
- `ASI.Basecode.WebApp/wwwroot/js/features/scanner-manual.js` — comment-only: drop "(register M-06)"; no behavior change.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — mark M-06 removed with the commit and update the provider/removal paragraphs that name `DemoCheckInLookupService`.
- `_bmad-output/initiative-attendta/demo-script.md`, `docs/demo-quickstart.md` — update the example demo-active log line.
- `tests/ASI.Basecode.IntegrationTests/CheckInLookupFlowTests.cs` — new focused SQL smoke.
- `tests/ASI.Basecode.IntegrationTests/DemoCheckInWindowFlagTests.cs` — build `AttendanceRecordingService` as `CheckInFlowTests.Services` does and pass it to `new CheckInLookupService(events, registrations, recording)`; keep the flag assertions and add lookup-resolution assertions for both switch states.

## Tasks & Acceptance

**Execution:**
- [x] `IRegistrationRepository` + `RegistrationRepository` — add the confirmed lookup reads with server-side filter and paging.
- [x] `CheckInLookupService` + shared outcome mapper — real search and manual check-in through the engine; update `CheckInService`.
- [x] DI and demo removal — register the real service, delete both M-06 providers and their lines, fix the stale M-06 log/doc/JS comments.
- [x] `CheckInLookupFlowTests` — seed 30 confirmed rows and assert: search by name and by number, blank/no match, page 1 = 25 rows and page 2 = 5 with `TotalPages` 2, only this event's Confirmed rows, attendance and audit counts unchanged after search, `manual_search` vs `student_number` with one attendance + one audit row, repeat keeps the original time, closed/unpublished → Closed, refused registrations → Invalid, member role → Forbidden.
- [x] `DemoCheckInWindowFlagTests` — real lookup composition, open/closed flag assertions, and `ICheckInLookupService` resolves to `CheckInLookupService` with `DemoData:Enabled` on and off.
- [x] `demo-mock-register.md` — set M-06 to removed with the commit.

**Acceptance Criteria:**
- Given a published event with confirmed participants, when an Officer searches a name fragment or student number, then only that event's confirmed rows show, 25 per page, and no attendance or audit row is written.
- Given a search result, when the Officer clicks Check in, then one attendance row carries `manual_search` or `student_number`, one `attendance.recorded` audit row exists, and the outcome shows name, student number and time.
- Given that recorded attendance, when the same row is checked in again, then the outcome is Repeat with the original time and no new rows.
- Given a closed window or unpublished event, when Check in is posted, then the outcome is Closed and nothing is written.
- Given `DemoData:Enabled` off, when the scanner page loads, then the real lookup service is resolved and search still works.

## Implementation Notes

- The lookup query is one `ConfirmedLookup` shape in `RegistrationRepository`: registration → member →
  user, left joined to `attendance`, filtered on event + Confirmed + `Contains`, ordered by `User.Name`
  then `RegistrationId`, then `Skip/Take`. EF Core could not translate ordering over a record or a
  `Contains` inside a projection, so the ordering runs on the joined entities and the projection to
  `ConfirmedLookupRow` happens after `Skip/Take`.
- `GetConfirmedStudentNumberAsync` doubles as the event-match and status check before delegating, so a
  cancelled, wrong-event or unknown registration is refused with the manual copy without touching the
  engine.
- The demo register row M-06 records commit `ff06123`; the plan's own commit is separate.

## Plan Change Log

## Review Triage Log

## Design Notes

The manual path reuses the 4.3 engine, so validation, lock and audit rules cannot drift between scan and manual check-in. The service pre-checks the published event and open window before delegating, mirroring `CheckInService.ScanAsync`, so a closed window answers Closed even for an unknown registration id; it keeps the manual copy "…Open it before checking in." while the engine's scan message is unchanged. Search does not require an open window: it is read-only, and the operator may still need to see who is confirmed while the window is closed.

A dedicated lookup query is used instead of extending `GetReviewQueueAsync`: the review queue orders by `SubmittedAt` and has no attendance projection, and other screens depend on that shape. `Contains` is the house search pattern (`MemberRepository`, `AccountAdministrationRepository`) and relies on the SQL Server case-insensitive collation, matching the old in-memory `OrdinalIgnoreCase` behaviour.

Deleting `UnavailableCheckInLookupService` intentionally changes the switch-off state to the real service, matching 4.3's precedent for `ICheckInService`; the demo register's switch-off wording is updated with the M-06 row.

## Verification

**Commands:**
- `dotnet build` — expected: full solution builds.
- `$env:ATTENDTA_TEST_SQL="Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter "FullyQualifiedName~CheckInLookupFlowTests|FullyQualifiedName~DemoCheckInWindowFlagTests|FullyQualifiedName~CheckInFlowTests"` — expected: all pass.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass.

**Manual checks (if no CLI):**
- With a seeded open event, search a name, click Check in, and see the success panel; search again and see the row marked Checked in; confirm a search alone added no attendance row.
