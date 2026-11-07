---
title: 'Member attendance view on My Events'
type: 'feature'
ticket: '7'
created: '2026-11-07'
status: 'built'
baseline_revision: 'fd59677722910062b06f2cecb47bde0a87af4bfb'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: []
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
  - 'docs/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 4.12 delivered the Attendance tab UI, but 3.13 left `MyEventsService.GetAttendanceAsync`
returning an empty page, so a member can never see their own attendance and demo register row M-08
is still open.

**Approach:** Add one owner-scoped, 25-row-paged attendance read to the attendance repository and
map it in `MyEventsService.GetAttendanceAsync`. The existing partial, pager, tab model and
controller action stay unchanged.

## Boundaries & Constraints

**Always:** Resolve the actor only from `ICurrentActor.UserId` (the Member policy already guards the
route). Filter ownership inside SQL through `registration → members.user_id`. Order
`recorded_at DESC, attendance_id DESC`. Page with `PageRequest`/`PagedResult` at
`PageRequest.FixedPageSize` (25). Label the method through `CheckInMethodDisplay`. Keep invalidated
rows visible with their stored reason. Read-only: no write, transaction, audit entry or
`SaveChanges`. Update the M-08 row and its detail section in `demo-mock-register.md` in this PR.

**Never:** No schema, migration or SQL script change. No new route, controller action, view,
partial, view-model field or service class. No demo provider deletion (none exists) and no
`DemoData` change. No self-check-in, correction, edit or delete control. No broad automation
(8.5/8.4 own it) and no per-ticket Impeccable critique or polish.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid record | Member owns a valid attendance row | Event title, Manila recorded time, method label, `Valid` badge | No error expected |
| Invalidated record | `invalidated_at` set with a reason | Same row with `Invalidated` badge and the reason | No error expected |
| Foreign rows | Another member has attendance | Never returned | No error expected |
| No attendance | Zero owned rows | Empty state, no pager | Empty state |
| More than 25 | 26 owned rows | 25 on page 1, pager to page 2, page 99 clamps to the last page | No error expected |
| Anonymous | No cookie | Redirect to sign-in | Member policy |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Interfaces/IAttendanceRepository.cs` -- add `CountMyAttendanceAsync(int userId, ct)` and `GetMyAttendanceAsync(int userId, int skip, int take, ct)`, plus a `MyAttendanceRow` projection record mirroring `AttendanceLogRow` (:58-69). No owner-scoped read exists today.
- `ASI.Basecode.Data/Repositories/AttendanceRepository.cs` -- implement both with the join style of `GetRecentForEventAsync` (:53-69): attendance → registration → member, plus event for the title; `where member.UserId == userId`; `orderby attendance.RecordedAt descending, attendance.AttendanceId descending`; `Skip/Take`.
- `ASI.Basecode.Services/Services/MyEventsService.cs` -- add `IAttendanceRepository attendance` to the primary constructor; replace the stub at :65-72 with the `GetRegistrationsAsync` shape (:27-62): count → `PageRequest`/`NormalizePageNumber` → paged read → map each row with `CheckInMethodDisplay.Label(row.CheckInMethod)` and `IsInvalidated = row.InvalidatedAt is not null` → `new PagedResult<>(..., normalizedPage, totalCount)`. Update the class doc at :14-17.
- `ASI.Basecode.Services/Interfaces/IMyEventsService.cs` -- update the attendance XML doc (:7-11, :23-27) that says "empty page until 4.7".
- `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs` -- no change; `MyEventAttendanceRowViewModel` (:192-201) already carries every field the partial renders.
- `_bmad-output/initiative-attendta/demo-mock-register.md` -- set the M-08 row (:92) to `removed` and rewrite the M-08 detail section (:339-359) to describe the real query and the removal. Record the short sha in a follow-up docs commit, as 4.6 did for M-07 (`b83c5c6`).
- `tests/ASI.Basecode.IntegrationTests/MyEventsAttendanceQueryTests.cs` -- new focused smoke mirroring `MyEventsRegistrationQueryTests.cs`: seed 26 attendance rows for one member and one for another, sign in, assert owner scoping, order, paging, badges, empty state.
- Do not change: `Views/MyEvents/_AttendanceHistoryPartial.cshtml`, `Views/MyEvents/Index.cshtml`, `_Pager.cshtml`, `_ResultCount.cshtml`, `MyEventsController.cs`, `Startup.DI.cs` (`IAttendanceRepository` is already registered at :79), `CheckInMethodDisplay`, `ManilaTimeFormatter`, or anything under `DemoData/`.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/IAttendanceRepository.cs` -- add the count, paged read and projection record -- the Data layer owns the SQL.
- [x] `ASI.Basecode.Data/Repositories/AttendanceRepository.cs` -- implement both methods with the owner filter and stable order -- one query each.
- [x] `ASI.Basecode.Services/Services/MyEventsService.cs` -- inject the repository and implement `GetAttendanceAsync` -- replaces the 4.12 stub.
- [x] `ASI.Basecode.Services/Interfaces/IMyEventsService.cs` -- correct the contract doc -- it still promises an empty page.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` -- M-08 `removed`, detail rewrite, then the short sha in a follow-up docs commit -- register rules 11 and 12.
- [x] `tests/ASI.Basecode.IntegrationTests/MyEventsAttendanceQueryTests.cs` -- focused owner-scope/invalidated/paging smoke -- the ticket's one development check.

**Acceptance Criteria:**
- Given a member with a valid and an invalidated attendance row, when they open `/MyEvents/Attendance`, then only their rows render newest-first with the Manila time, method label, and `Valid`/`Invalidated` badge plus reason.
- Given 26 owned rows, when page 1 loads, then 25 rows render with "Showing 1–25 of 26 records." and "Page 1 of 2"; page 2 shows the 26th; `page=99` clamps to page 2.
- Given another member's attendance, when the tab renders, then none of their rows or event titles appear.
- Given no attendance, when the tab opens, then "No attendance to show yet." renders with no pager.
- Given the register, then M-08 reads `removed` with the implementation commit and no demo attendance provider remains.
- Given the change, then `dotnet build` is clean and the focused test passes.

## Implementation Notes

- **Files changed.** `IAttendanceRepository.cs` (+2 methods, +`MyAttendanceRow`), `AttendanceRepository.cs`
  (+2 queries), `MyEventsService.cs` (ctor + real `GetAttendanceAsync`), `IMyEventsService.cs` (doc),
  `demo-mock-register.md` (M-08 row + detail), new `MyEventsAttendanceQueryTests.cs`, and a two-line
  interface stub in the existing `DemoParticipationReportOverlayTests.FakeAttendance` fake.
- **Projection shape.** `MyAttendanceRow` carries `EventId`/`EventTitle` on top of the
  `AttendanceLogRow` shape because `MyEventAttendanceRowViewModel` renders the title; the record also
  exposes `IsValid` for symmetry with its siblings.
- **Surprise.** The integration project is xunit v3 on Microsoft.Testing.Platform, so
  `dotnet test --filter` reports "Zero tests ran". The plan's Verification now runs the test dll with
  `-class` as CI does.
- **Register sha.** M-08 reads `removed`; the short implementation sha is recorded in a follow-up
  docs commit, matching 4.6's M-07 precedent (`b83c5c6`).
- **Verification.** Build 0/0; focused test 1/1; Services.Tests 302/302; `css:build` produced no
  `app.css` drift.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens, 2026-11-07) — verdicts: high 0 / medium 0 / low 3 / false 1 / maybe-false 0.

| Finding | Verdict | Route | Evidence / action |
|---|---|---|---|
| `demo-mock-register.md:92` — M-08 reads `removed` without the implementation commit, so the AC and rule 11 are unmet | low | patch | Verified: the row has no sha and the detail defers it. The sha cannot exist before the implementation commit; patched by committing the change and recording the short sha in the follow-up docs commit, as 4.6 did for M-07 (`b83c5c6`). |
| `IAttendanceRepository.cs:87-94` — `MyAttendanceRow.InvalidationReason` is non-nullable while the column is nullable | false | — | Verified: `ASI.Basecode.Data` and `ASI.Basecode.Services` do not enable nullable reference types — adding `string?` produces CS8632 and breaks the plan's zero-warning gate. The record mirrors `Attendance.InvalidationReason` and both sibling records (`AttendanceLogRow`, `EventAttendanceRow`), and the only consumer (`_AttendanceHistoryPartial.cshtml:25`) guards with `IsNullOrWhiteSpace`. No runtime or developer harm at the cited location. |
| `DemoSeedService.cs:122-142` with `MyEventsService.cs:66-101` — seeded demo-store check-ins no longer appear in the member Attendance tab | low | defer | Verified: true by design. The approved intent replaces the demo source with the SQL query; a real 4.3 check-in does appear, and the store is removed by 8.13. Recorded in `deferred-work.md` so 8.15/8.12 do not expect seeded rows on this tab. |
| `demo-script.md:222`, `:238-240`, `:266` — the script still says the Attendance tab is empty until 4.7 | low | patch | Verified: three stale lines. Patched: M6 now describes the real history, the known-limit bullet states the tab is real since 4.7, and the 4.12 row passes. |
| `MyEventsController.cs:50-54` — the Attendance action lacks the `ResponseCache(NoStore)` attribute its sibling `Certificates` action carries | low | defer | Verified: `Certificates` sets `NoStore = true`; `Attendance` and `Registrations` do not, and no cache leak was demonstrated. The gap predates 4.7 and the frozen intent keeps the controller action unchanged, so it is recorded in `deferred-work.md` rather than patched. |

## Design Notes

- **No registration-status filter.** R7 says a member sees "their own Attendance Records"; attendance is retained after cancellation and invalidation, so the history returns every owned row. The 4.12 demo composed non-cancelled event ids only because that was the sole cross-event read available, and no demo case had a cancelled registration with attendance.
- **Repository-side paging.** Count plus `Skip/Take` keeps totals and the 25-row page correct without loading the member's whole history; it mirrors `GetMyRegistrationsAsync`.
- **Known limit.** Seeded `DemoStore` check-ins never appear here; only real SQL attendance does. A check-in made through the real engine (4.3) does appear, which is the integration intent.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` -- expected: 0 warnings, 0 errors.
- `$env:ATTENDTA_TEST_SQL="Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet tests/ASI.Basecode.IntegrationTests/bin/Debug/net10.0/ASI.Basecode.IntegrationTests.dll -class "*MyEventsAttendanceQueryTests" -parallelMode none` -- expected: 1 passed. (The project is xunit v3 on Microsoft.Testing.Platform, so `dotnet test --filter` runs zero tests; run the dll directly as CI does.)
- `dotnet test --project tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj --no-build` -- expected: all pass (302).
- `npm --prefix ASI.Basecode.WebApp run css:build` then `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` -- expected: no drift (no view change).

**Manual checks (if no CLI):**
- With `DemoData__Enabled=false`, sign in as a member whose registration has a real attendance row, open `/MyEvents/Attendance`, and confirm the row, its Manila time, the badge and the independent pager; no text mentions demo data.
