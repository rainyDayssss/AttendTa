---
title: 'Frontend per-event participation report'
type: 'feature'
ticket: '4'
created: '2026-11-07'
status: 'built'
baseline_revision: 'dc57cd0622821bce9cd6b6c00a7ef1552f87c1e9'
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

**Problem:** DESIGN screen 18 is an empty shell from 1.13. Officers cannot see per-event participation: status counts, Confirmed/Valid attendees, rate, completed-only no-shows, course/year breakdowns or participant rows. The report backend (7.1–7.3) does not exist yet.

**Approach:** Fill the 1.13 report contract with a demo-phase provider that composes real event/registration reads and the demo store's attendance and completion state (M-13a, M-13b, M-13c), then render screen 18 per `docs/DESIGN.md`. The controller stays thin; 7.1–7.3 replace the provider.

## Boundaries & Constraints

**Always:** Management policy; read-only; real reads first; demo data only behind `DemoData:Enabled`; nothing on screen names demo data; 25-row paging with full-dataset totals; N/A when Confirmed is zero; final rate and no-shows only for completed, non-cancelled events; breakdowns count valid attendees only, from signup snapshots; Manila time for check-in; keyboard, desktop and phone layouts.

**Never:** no schema change; no new Services/Data service implementation; no exports, print or cross-event metrics; do not drop pending/waitlisted/cancelled rows; do not edit another surface's demo registration file; do not commit demo keys or seed data; do not change the shared selector or pager contracts.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Completed event | Demo-completed with seeded check-ins | Counts, rate, no-shows, breakdowns and rows match those check-ins | No error expected |
| Published event | Confirmed > 0, not completed | Rate and no-shows show a not-final marker | No error expected |
| Cancelled event | Real Cancelled status | No no-show figure; counts and rows still render | No error expected |
| Zero Confirmed | Any status | Rate shows N/A | No error expected |
| Paging | Detail page 2 | Summary and breakdowns unchanged; next 25 rows | No error expected |
| No selection / no events | eventId null or empty list | Selector plus explanatory empty state | No error expected |
| Demo switch off | `DemoData:Enabled=false` | Empty report state; no demo data | No error expected |
| Invalidated attendance | Completed event, invalidated check-in | Row shows Invalidated; not a valid attendee | No error expected |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/ServiceModels/ParticipationReportViewModel.cs` — extend the 1.13 contract: status counts, event status, `IPagedResult`, row check-in time and attendance label/badge.
- `ASI.Basecode.Services/Interfaces/IParticipationReportService.cs` — new `GetReportAsync(int? eventId, int? page, CancellationToken)`.
- `ASI.Basecode.WebApp/DemoData/DemoParticipationReportService.cs` — new provider (M-13a/b/c) over `IEventRepository`, `IRegistrationRepository`, `DemoStore`.
- `ASI.Basecode.WebApp/DemoData/DemoReportsRegistration.cs` — report surface registration file.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` — call the report registration; empty provider when off.
- `ASI.Basecode.WebApp/DemoData/EmptyParticipationReportService.cs` — switch-off empty state.
- `ASI.Basecode.WebApp/Controllers/ReportsController.cs` — inject the contract; one call; pass `eventId`/`page`.
- `ASI.Basecode.WebApp/Views/Reports/Participation.cshtml` — selector, event header, summary, status counts, table + `_ResultCount`/`_Pager`, breakdowns.
- `ASI.Basecode.WebApp/Styles/features/reports.css` — summary/status/breakdown treatments.
- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs` + `Repositories/RegistrationRepository.cs` — project `CourseSnapshot`/`YearLevelSnapshot` onto `RegistrationReviewRow` (existing read).
- `_bmad-output/initiative-attendta/demo-mock-register.md` — record M-13a/b/c.

## Tasks & Acceptance

**Execution:**
- [x] `ParticipationReportViewModel.cs` — extend the model and implement `IPagedResult`.
- [x] `IParticipationReportService.cs` — add the read contract.
- [x] `IRegistrationRepository.cs`/`RegistrationRepository.cs` — project signup snapshots.
- [x] `DemoParticipationReportService.cs` — compute counts/rate/no-shows/breakdowns/rows.
- [x] `DemoReportsRegistration.cs` + `DemoDataRegistration.cs` + `EmptyParticipationReportService.cs` — register per switch.
- [x] `ReportsController.cs` — call the contract.
- [x] `Participation.cshtml` + `reports.css` — render screen 18.
- [x] `demo-mock-register.md` — fill M-13a/b/c detail.

**Acceptance Criteria:**
- Given a demo-completed event with seeded check-ins, when an Officer opens the report, then counts, rate, no-shows, breakdowns and rows agree with those check-ins.
- Given a published event, then rate and no-shows are marked not final.
- Given a cancelled event, then no no-show figure appears.
- Given zero Confirmed, then the rate reads N/A.
- Given page 2, then summary and breakdowns are identical to page 1.
- Given any rendered report, then no text names demo data and the layout works at desktop, keyboard and phone width.

## Implementation Notes

- Rate and no-show presentation is decided in the view from `AttendanceRate`, `IsFinal` and
  `EventStatusId`: zero Confirmed → **N/A**; cancelled → rate **N/A** and no no-show figure; any
  other unfinished event → **Not final** for both; completed non-cancelled → the percentage and the
  count. The service still returns the canonical `valid / confirmed` rate and the full no-show
  count, so 7.1/7.2 can change presentation without recomputing.
- The demo provider reads the existing `GetReviewQueueAsync` once per registration status instead
  of adding a repository method (7.3 owns the paged query), then orders by member name and
  registration id and slices 25 rows in memory. Totals and breakdowns use the full list.
- An invalidated check-in keeps the **Invalidated** row label (per the I/O matrix) while still
  counting as a no-show, because the canonical fact is "Confirmed without valid attendance".
- Breakdowns show each group's count plus its share of valid attendees as a proportional bar; an
  empty breakdown renders an explanatory line instead of an empty list.
- Verified with `dotnet build` (0 warnings), `npm run css:build`, the 271 service tests, the 323
  integration tests (two need the SDK on `PATH`; both pass once it is), and a manual walk of the
  I/O matrix against `AttendTaMVP_Test` with demo data on and off.
- Matrix Test Audit: this ticket's Done check and the delivery-queue demo-phase policy forbid new
  automated tests, so no covering test exists to run. Each I/O matrix row was verified by the manual
  walk above; 8.15 re-runs the full Verify line before the demo.

## Plan Change Log

## Review Triage Log

### Pass 1 — quick (2026-11-07)

Verdicts: high 0 · medium 0 · low 3 · false 2 · maybe-false 0.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | Valid attendance and no-shows read only from `DemoStore`, with no real-attendance read; the M-13 section omits the rule-12 hand-over | low | patch | The ticket and M-13 rows explicitly define attendance from the demo store, and no real per-registration attendance read exists, so the code is per contract. The missing hand-over note is real: 4.3/4.6 make check-ins real before 7.1–7.3 and rule 12 assigns the switch to the removing ticket, so the M-13 section must name it. |
| 2 | Completed overlay applied on a fourth event surface not named in rule 7 | false | — | Rule 7 governs overlays on the three existing event reads; the report is a new read whose completion source the ticket and M-13 rows explicitly define as `DemoStore.IsCompleted`, and the M-13 section records it. |
| 3 | Start-cutoff label never applied to pending/waitlisted rows | false | — | SPEC §9.5 governs registration processing (reject reviews, confirmations, promotions and cancellations; disable processing/check-in); the report is read-only and §15 asks for the stored registration status. The count strip and rows stay on the same stored-status vocabulary. |
| 4 | Rate not rounded to the canonical two places | low | patch | AD-5 rounds the fraction to two places for display; 7/8 currently renders 87.5% instead of 88%. Direct correction in the provider. |
| 5 | Breakdown panels mis-spaced by `.panel + .panel` and no top gap | low | patch | `.report-breakdowns` is a two-column grid whose second panel inherits `margin-top: 1rem`, offsetting the cards; the block also sits flush under the participants panel. |

## Design Notes

- Effective status = `DemoStore.IsCompleted(eventId) ? Completed : real EventStatusId`; a demo-completed event is still Published in SQL.
- Valid attendance = demo record present and not invalidated. No-show = Confirmed without valid attendance, counted only for completed non-cancelled events.
- Rate: Confirmed == 0 → N/A; not completed/cancelled → not-final marker; else valid/confirmed.
- Order rows by member name then registration id; compute totals before the page slice.
- Reuse `RegistrationStatusDisplay`/`EventStatusDisplay`, `ManilaTimeFormatter`, `_EventSelector`, `_Pager`, `_ResultCount`.

## Verification

**Commands:**
- `dotnet build` — expected: success, no new warnings.
- `npm run css:build` (in `ASI.Basecode.WebApp`) — expected: success, `app.css` regenerated.

**Manual checks:**
- With demo data on, open `/Reports/Participation` as an Officer and walk the I/O matrix.
