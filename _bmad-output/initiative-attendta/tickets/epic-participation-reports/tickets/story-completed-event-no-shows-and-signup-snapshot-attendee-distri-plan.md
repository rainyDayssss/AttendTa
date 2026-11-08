---
title: 'Completed-event no-shows and signup snapshot attendee distributions'
type: 'feature'
ticket: '2'
created: '2026-11-07'
status: 'built'
baseline_revision: 'bfed8110210fc25d4823fa0fb8774fceb6d69a4b'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
followup_review_recommended: false
review_loop_iteration: 0
context:
  - 'AGENTS.md'
  - '_bmad-output/initiative-attendta/tickets/epic-participation-reports/tickets/story-completed-event-no-shows-and-signup-snapshot-attendee-distri.md'
  - '_bmad-output/initiative-attendta/tickets/epic-participation-reports/epic-participation-reports.md'
  - 'ASI.Basecode.Data/Queries/ParticipationFacts.cs'
---

<frozen-after-approval reason="human-owned intent - do not modify unless human renegotiates">

## Intent

**Problem:** The report's No-shows figure and course/year breakdowns still come from the demo overlay (M-13b). Management needs both from canonical SQL: a completed event must count Confirmed registrations without valid attendance as no-shows, and actual attendees must be grouped by the course/year captured at signup, including members who are now inactive.

**Approach:** Add read-only no-show and distribution queries to `ParticipationReportRepository`, built on `ParticipationFacts.NoShows`, `SeatHolders` and `ValidAttendances`, and map them in `ParticipationReportService`. The demo overlay keeps only the M-13c rows and the M-05/M-07 attendance handover, passing through the real no-shows and breakdowns whenever the demo store holds no attendance for the event.

## Boundaries & Constraints

**Always:** Reads only (AD-2). No-show = Confirmed + event Completed + no Valid Attendance (AD-5); pending, waitlisted and cancelled rows are excluded. Distributions group valid attendees by `registrations.course_snapshot` and `year_level_snapshot`; never join users/members with an active filter, so inactive history stays visible. Percent is the whole-percent share of valid attendees. Keep the `IParticipationReportService` contract, route, view model and Razor output unchanged.

**Never:** Mutate participation records; change the view, CSS or visible labels; re-derive a canonical fact; add a second report screen; implement 7.3 paged detail rows; remove demo row M-13c; add a PM override.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Completed event | Confirmed rows, some with valid attendance, some invalidated or never checked in | NoShowCount = Confirmed without valid attendance; pending/waitlisted/cancelled excluded | No error |
| Cancelled event | Cancelled event with confirmed rows | NoShowCount = 0; view keeps showing "—" and N/A rate | No error |
| Distributions | Valid attendees with different signup snapshots | Course and year counts/percent reflect snapshots, not current member data | No error |
| Inactive history | Valid attendee whose user/member is now inactive | Still counted in the distributions and totals | No error |
| Zero attendees | No valid attendance | Empty breakdowns; percent never divides by zero | No error |
| Demo host | Demo store holds attendance for the event | Overlay keeps attendance-derived figures (rule 12); otherwise real values stand | No visible change |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Interfaces/IParticipationReportRepository.cs` — add `GetNoShowCountAsync(eventId, ct)` and `GetAttendeeDistributionAsync(eventId, ct)` plus Data-owned records (`ParticipationReportDistributionFacts`, `CourseDistributionRow`, `YearDistributionRow`). Keep the existing summary method.
- `ASI.Basecode.Data/Repositories/ParticipationReportRepository.cs` — `GetNoShowCountAsync` counts `ParticipationFacts.NoShows` for the event; `GetAttendeeDistributionAsync` groups `SeatHolders` joined to `ValidAttendances` by `CourseSnapshot` and `YearLevelSnapshot`. `AsNoTracking`, no writes.
- `ASI.Basecode.Services/Services/ParticipationReportService.cs` — read the two new facts; map `NoShowCount`, `CourseBreakdown`, `YearBreakdown`; format labels (`Unspecified`, `Year N`), order and whole-percent share as the demo overlay does today.
- `ASI.Basecode.WebApp/DemoData/DemoParticipationReportService.cs` — drop the unconditional demo no-show/breakdown computation; keep M-13c rows and the M-05/M-07 handover. When `GetAttendanceForEvent` is empty, pass through the real `NoShowCount` and breakdowns; when it holds attendance, keep the attendance-derived values so the screen does not change.
- `ASI.Basecode.WebApp/DemoData/DemoReportsRegistration.cs` — comment now names M-13c only.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-13b to `removed; demo attendance source remains under M-05/M-07`; update the M-13 detail, piece table, handover and removal paragraphs.
- `tests/ASI.Basecode.Services.Tests/ParticipationReportServiceTests.cs` — extend the fake repository; assert no-show mapping and breakdown labels/counts/percents/order.
- `tests/ASI.Basecode.IntegrationTests/DemoParticipationReportOverlayTests.cs` — extend the fake repository; prove the overlay uses real no-shows/breakdowns when the store holds no attendance.
- New `tests/ASI.Basecode.IntegrationTests/ParticipationReportRepositoryTests.cs` — focused SQL smoke: completed no-shows, cancelled zero, snapshot distribution with an inactive member.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/IParticipationReportRepository.cs` — add the two read methods and distribution records.
- [x] `ASI.Basecode.Data/Repositories/ParticipationReportRepository.cs` — implement both queries from canonical facts; no user/member active filter.
- [x] `ASI.Basecode.Services/Services/ParticipationReportService.cs` — map no-shows and breakdowns; keep selector/summary behavior unchanged.
- [x] `ASI.Basecode.WebApp/DemoData/DemoParticipationReportService.cs`, `DemoReportsRegistration.cs` — remove the M-13b computation, keep M-13c and the rule-12 handover.
- [x] `tests/ASI.Basecode.Services.Tests/ParticipationReportServiceTests.cs` — update the fake and add mapping/edge tests.
- [x] `tests/ASI.Basecode.IntegrationTests/DemoParticipationReportOverlayTests.cs` — update the fake and add the real-figures pass-through test.
- [x] `tests/ASI.Basecode.IntegrationTests/ParticipationReportRepositoryTests.cs` — add the focused SQL smoke.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` — record M-13b removal.

**Acceptance Criteria:**
- Given a completed event with C confirmed registrations and V valid attendances, when the report loads, then NoShowCount = C − V and includes invalidated or never-checked-in confirmed rows only.
- Given a cancelled event, when the report loads, then NoShowCount = 0 and the view still renders "—" and N/A.
- Given valid attendees with different signup snapshots, when the report loads, then course and year breakdowns match those snapshots, ordered and percentaged as today.
- Given a valid attendee whose account/member is inactive, when the report loads, then that attendee is still counted.
- Given the demo host with no demo attendance, when the report loads, then the real no-shows and breakdowns are shown unchanged.

## Implementation Notes

- The repository reuses one `ValidAttendeeIds` helper for the summary and distribution reads, so both count the same valid-attendee set.
- The service computes percentages from the distribution's own attendee total, which equals the canonical `ValidAttendeeCount`; this keeps the panel self-consistent if the two reads ever race.
- `DemoStore.cs`'s `MarkCompleted` comment dropped the removed M-13b fixture reference; behavior unchanged.
- Verification: `dotnet build` 0 warnings/0 errors; Services tests 288/288; report integration tests 3/3; full integration suite 357/361, with the 4 failures confined to `CertificateTracerFlowTests` because Poppler's `pdftotext` is not installed (pre-existing environment issue, unrelated to this change).

## Plan Change Log

## Review Triage Log

- Quick review: 0 high, 0 medium, 5 low, 0 false, 0 maybe-false.
- low, patch — `demo-mock-register.md` Hand-over paragraph said the summary, no-shows and breakdowns "always come from the real service", contradicting the rule-12 demo override. Reworded to say they come from the real service unless the demo store holds attendance.
- low, patch — The M-01 row and `DemoStore.MarkCompleted` comment said the completed-state fixture serves "only the M-10a tests", but `DemoParticipationReportOverlayTests` also calls it. Named the M-10a and participation-report overlay tests.
- low, patch — The register said `DemoStore.IsCompleted` remains only for the M-10a fixture, but `DemoParticipationReportService` still calls it. Named the report overlay test fixture too.
- low, patch — `RealNoShowsAndBreakdownsPassThroughWhenDemoStoreHoldsNoAttendance` asserted a no-show value the removed demo formula also produced, so it did not prove pass-through. Set the fake to 0 (the Published event's real value) and asserted 0 against a `Confirmed - Valid` of 1.
- low, patch — `IParticipationReportService` doc comment still described 7.1–7.3 as future and named M-13a/M-13b. Updated to say 7.1 and 7.2 are real and only M-13c remains with the demo provider.

### 2026-11-08 — Review pass

- verdicts: 5 findings — high 0, medium 0, low 5, false 0, maybe-false 0
- findings:
  - `[low]` `[reject]` The report assembles its figures from six independent reads with no shared snapshot, so a concurrent check-in can make Confirmed − Valid disagree with No-shows — real only while a write lands mid-request; the fix is transaction/snapshot plumbing across the whole read path, more than a direct correction, and the plan scopes the surface as read-only (AD-2).
  - `[low]` `[patch]` `GetAttendeeDistributionAsync` ran the valid-attendee set twice, once per breakdown, so the course and year panels could cover different attendees — merged into one grouped query and split in memory.
  - `[low]` `[patch]` `demo-mock-register.md` M-13b said `removed` without the removal commit, breaking register rule 11 — recorded `(975c08b)`.
  - `[low]` `[patch]` The year `Unspecified` label was unreachable because `chk_registrations_positive_year_level` forbids `year_level_snapshot = 0` — removed the fallback and the test row that asserted the impossible state.
  - `[low]` `[patch]` `ZeroAttendeesProducesEmptyBreakdownsWithoutDividingByZero` never reached `Share`'s zero guard, because a non-empty distribution always sums to at least one — renamed to `ZeroAttendeesProducesEmptyBreakdowns`; the guard stays as a defensive no-cost branch.

## Design Notes

- 7.2 keeps `DemoParticipationReportService` because it still owns M-13c (7.3) and the M-05/M-07 attendance handover. The ticket note's "delete the provider classes" applies when the last demo row leaves; here only the M-13b computation goes.
- The distribution query deliberately does not join `users` or `members`: SPEC §15 requires inactive history to remain visible, and the snapshot columns already hold the historical course/year.
- Percent uses the distribution's own attendee total as the denominator; it is the same valid-attendee set as `ValidAttendeeCount`, so the panel stays self-consistent and matches the demo's share-of-attendees rule.

## Verification

**Commands:**
- `dotnet build` — expected: success.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass.
- `$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — expected: new report and overlay tests pass.

**Manual checks:**
- Sign in as an officer against a local database with a completed event; open `/Reports/Participation?eventId=<id>` and confirm no-shows and breakdowns match hand-counted rows, including an inactive member.

## Auto Run Result

**Summary:** Follow-up review pass on the already-merged 7.2 change. The feature itself landed on `dev` as PR #189 (`975c08b`); this pass reviewed that diff against the current tree and applied four small corrections.

**Files changed:**
- `ASI.Basecode.Data/Repositories/ParticipationReportRepository.cs` — one grouped query for the attendee distribution instead of two scans of the same valid-attendee set.
- `ASI.Basecode.Services/Services/ParticipationReportService.cs` — the year label is always `Year N`; the schema forbids a zero year.
- `tests/ASI.Basecode.Services.Tests/ParticipationReportServiceTests.cs` — dropped the impossible year-0 row, updated counts and percents, renamed the zero-attendee test.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — M-13b records the removal commit `975c08b`.

**Review findings:** 5 findings, all low. 4 patched, 1 rejected (see the 2026-11-08 triage entry). 0 deferred.

**Follow-up review recommended:** false — no high or medium entry was patched.

**Verification:**
- `dotnet build` — succeeded, 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — 315/315 passed.
- `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter FullyQualifiedName~ParticipationReport` — 2/2 passed against SQL Server LocalDB.

**Residual risks:** The report still reads its summary, no-shows, distribution and detail rows in separate statements, so a write landing mid-request can briefly disagree across panels (the rejected low finding).
