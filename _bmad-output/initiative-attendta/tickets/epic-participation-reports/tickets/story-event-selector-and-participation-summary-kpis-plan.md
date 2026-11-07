---
title: 'Event selector and participation summary KPIs'
type: 'feature'
ticket: '1'
created: '2026-11-07'
status: 'built'
baseline_revision: 'e99a7b8b7bfc5caec1ac013147b9c5d293470c9d'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
  - '_bmad-output/initiative-attendta/tickets/epic-participation-reports/tickets/story-event-selector-and-participation-summary-kpis.md'
  - 'ASI.Basecode.Services/Interfaces/IParticipationReportService.cs'
  - 'ASI.Basecode.Services/ServiceModels/ParticipationReportViewModel.cs'
  - 'ASI.Basecode.Data/Queries/ParticipationFacts.cs'
  - 'ASI.Basecode.Data/Repositories/RegistrationRepository.cs'
  - 'ASI.Basecode.WebApp/DemoData/DemoParticipationReportService.cs'
  - 'ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs'
  - 'ASI.Basecode.WebApp/Startup.DI.cs'
---

<frozen-after-approval reason="human-owned intent - do not modify unless human renegotiates">

## Intent

**Problem:** The 7.4 per-event report serves its summary KPIs (registration status counts, Confirmed and Valid attendees, attendance rate) from the demo store. Management needs those figures from the real database so the report matches recorded registrations and attendance.

**Approach:** Add a real `ParticipationReportService` and a Data-layer summary query built only on canonical `ParticipationFacts`. Keep the existing `IParticipationReportService` contract and Razor surface unchanged. `DemoParticipationReportService` becomes a post-read overlay that keeps the still-open M-13b/M-13c demo fields (and the still-open M-05/M-07 demo attendance source where the demo store holds attendance) until those rows are replaced.

## Boundaries & Constraints

**Always:** Reads only (AD-2). Use `ParticipationFacts.SeatHolders`, `ValidAttendances` and `AttendanceRates`; do not re-derive a canonical fact. Count the full event dataset before any paging. Attendance rate is Valid attendees divided by Confirmed, `null` at zero Confirmed, stored unrounded and rounded only by the view. `IsFinal` is true only for a Completed status, so Published, Draft and Cancelled report "Not final"/N/A through the existing view. Keep the shared event selector contract, view model fields, controller route and Razor output unchanged.

**Never:** Mutate participation records, add a second report screen, change layout, CSS or visible labels, add a PM override, implement 7.2 no-shows/breakdowns or 7.3 paged detail rows, or remove demo rows M-13b and M-13c.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Completed event | Event with registrations and valid/invalidated attendance | Status counts, Confirmed, Valid attendees and rate match the SQL dataset; rate unrounded | No error |
| Zero Confirmed | Event with no confirmed registration | `AttendanceRate` is null; view shows N/A | No divide-by-zero |
| Published / Draft / Cancelled | Non-completed event | `IsFinal` false; view shows Not final or N/A as today | No final rate |
| No selection | `eventId` null or unknown | Selector plus empty report, as today | No error |
| Demo host | Demo attendance present in `DemoStore` | Demo overlay keeps the demo figures until M-05/M-07 hand over | No visible screen change |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Queries/ParticipationFacts.cs` - reuse `SeatHolders`, `ValidAttendances` and `AttendanceRates`; do not change their definitions.
- New `ASI.Basecode.Data/Interfaces/IParticipationReportRepository.cs` - read contract plus a Data-owned `ParticipationReportSummaryFacts` record (Pending, Confirmed, Waitlisted, Cancelled, ValidAttendee counts and nullable canonical rate).
- New `ASI.Basecode.Data/Repositories/ParticipationReportRepository.cs` - `BaseRepository` with one `GetSummaryFactsAsync(eventId, ct)` composed from the facts; `AsNoTracking`, no writes.
- New `ASI.Basecode.Services/Services/ParticipationReportService.cs` - selector and event reads via `IEventRepository`; maps the facts into `ParticipationReportViewModel`; leaves Rows, breakdowns and NoShowCount empty for 7.2/7.3; `IsFinal = status == EventStatusCode.Completed`.
- `ASI.Basecode.WebApp/Startup.DI.cs` - register the repository and the real service as the default `IParticipationReportService`.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` - remove the `EmptyParticipationReportService` switch-off registration; the real service now serves that path.
- `ASI.Basecode.WebApp/DemoData/DemoParticipationReportService.cs` - delete the inline summary computation; call the real service, then overlay the still-open demo fields (rows, breakdowns, no-shows) and, only when `DemoStore.GetAttendanceForEvent(eventId)` is non-empty, the demo-attendance-derived valid count and rate (register rule 12). Effective Completed status still comes from the store until M-09 is removed.
- Delete `ASI.Basecode.WebApp/DemoData/EmptyParticipationReportService.cs`.
- New `tests/ASI.Basecode.Services.Tests/ParticipationReportServiceTests.cs` - focused fake-repository smoke for the summary mapping and edge cases.
- `_bmad-output/initiative-attendta/demo-mock-register.md` - set row M-13a to `removed` with a note that the demo attendance source remains under M-05/M-07.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/IParticipationReportRepository.cs`, `ASI.Basecode.Data/Repositories/ParticipationReportRepository.cs` - add the read contract and implementation using `ParticipationFacts`; register in `Startup.DI.cs`.
- [x] `ASI.Basecode.Services/Services/ParticipationReportService.cs` - implement selector, event header, summary counts, unrounded rate and completed-only `IsFinal`.
- [x] `ASI.Basecode.WebApp/Startup.DI.cs`, `DemoData/DemoDataRegistration.cs`, `DemoData/DemoReportsRegistration.cs`, `DemoData/DemoParticipationReportService.cs`, delete `DemoData/EmptyParticipationReportService.cs` - make the real service the default and convert the demo provider into a post-read overlay.
- [x] `tests/ASI.Basecode.Services.Tests/ParticipationReportServiceTests.cs` - prove the summary mapping, zero-denominator null, non-completed `IsFinal` and no-selection selector.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` - record M-13a removal.

**Acceptance Criteria:**
- Given an officer selects a completed event with C confirmed registrations and A valid attendances, when the report loads, then Pending/Confirmed/Waitlisted/Cancelled counts and Confirmed and Valid attendee totals equal the full SQL dataset and `AttendanceRate` equals A divided by C unrounded.
- Given an event with zero Confirmed registrations, when the report loads, then `AttendanceRate` is null and the existing view renders N/A.
- Given a Published, Draft or Cancelled event, when the report loads, then `IsFinal` is false and no final rate is presented.
- Given the same selected event requested with a different `page` value, when the report loads, then the summary values are unchanged.
- Given no usable `eventId`, when the report loads, then only the selector and empty report are returned.

## Implementation Notes

- 7.1 is backend-only; no Impeccable critique or polish. 7.2 and 7.3 keep their demo rows.
- The demo provider's attendance-derived summary remains a rule-12 handover from M-05/M-07, not new report logic; when those rows become real the overlay's demo branch stops applying.
- In this checkout M-05/M-07/M-09 are still demo, so the demo host keeps its store-seeded numbers; the real path reads SQL attendance.

## Plan Change Log

## Review Triage Log

- Quick review: 0 high, 1 medium, 1 low, 1 deferred.
- medium, patch — `DemoParticipationReportService.NoShowCount` subtracted the demo valid count (0 when the store held no attendance) while the summary displayed the SQL valid count, so a completed event with real attendance reported every confirmed registration as a no-show. Patched to subtract the displayed `validAttendeeCount`; covered by `RealValidCountDrivesNoShowsWhenDemoStoreHoldsNoAttendance`.
- low, defer — The real service shows summary counts while `Rows` and breakdowns stay empty until 7.2/7.3, so the unchanged view's existing empty-state copy reads "No registrations"/"No attendee data". The frozen intent excludes the 7.3 detail rows; 7.3 owns the detail content and closes this. Recorded in `deferred-work.md`.
- low, patch — `demo-mock-register.md`'s M-13a detail section still listed the deleted `EmptyParticipationReportService.cs` and described the demo summary. Updated to the real `ParticipationReportService`/`ParticipationReportRepository` and the demo overlay's M-13b/M-13c plus M-05/M-07 role.

## Verification

- `dotnet build` - solution builds.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` - focused summary tests pass and existing service tests stay green.
- Focused smoke: sign in as an officer against a local database containing one completed event with confirmed registrations and attendance, open `/Reports/Participation?eventId=<id>`, and confirm the KPIs match hand-counted rows; then request a different `page` and confirm the KPIs do not change.
