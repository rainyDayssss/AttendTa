---
title: 'Important automated acceptance cases and SPEC coverage mapping'
type: 'chore'
ticket: '5'
created: '2026-11-11'
status: 'built'
baseline_revision: '57e628b8427e3c7391b86928806da0845c5d0164'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: ['_bmad-output/initiative-attendta/finale-test-cases.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 8.5 has no coverage map, and most of its target modules are still demo or no-op seams: `CheckInController` returns an empty model, `CertificateLedgerController` has no service implementation, `ReportsController` returns an empty report, and `DemoDataRegistration` registers no-op providers when the switch is off. Only auth/requests, master data, events, registration/allocation/cutoff/cancellation, last-admin, audit and the PDF tracer are real, so only part of the missing important automation can be authored now.

**Approach:** Write the SPEC §20 / finale-case coverage matrix as the ticket's primary artifact, marking every scenario that cannot be automated today as deferred to 8.13 with a concrete 8.6 manual case; add the one family of missing important automation that real code supports — real-SQL `ParticipationFacts` aggregate proofs at 26+ rows. Add nothing against the demo seam.

## Boundaries & Constraints

**Always:** Two existing test projects only; test-only change plus `_bmad-output` docs; every SPEC §20 bullet and every finale case appears in the matrix; deferred rows name their owning entry; assertions prove full-dataset behaviour.

**Never:** Reference `ASI.Basecode.WebApp/DemoData/*` or the no-op providers; add a test project, harness or prototype-only suite; change product, schema or CI; duplicate a passing case or skip/weaken a test to manufacture a pass.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| MATRIX_GAP | Any §20 bullet with no automated test | Row names a concrete 8.6 manual case or `deferred: 8.13` | Unmapped row fails review |
| RATE_26 | 26 confirmed, mixed valid/invalidated attendance | Exact fraction over all 26; invalidated excluded | No error expected |
| RATE_ZERO | Event with zero confirmed | `null` (N/A) | No error expected |
| INACTIVE_HISTORY | Inactive member with confirmed + valid attendance | Still counted in totals | No error expected |
| NO_SHOWS | Completed event only | Confirmed with no valid attendance; other statuses excluded | No error expected |

</frozen-after-approval>

## Code Map

- `docs/AttendTa-System-Specification.md` §20 (lines 377–393) — nine acceptance bullets, the authoritative scenario list.
- `_bmad-output/initiative-attendta/finale-test-cases.md` — case ids AUTH-01…MOCK-01, UI-01…OPS-02, and the functional-ID table.
- `_bmad-output/initiative-attendta/tickets/epic-release-acceptance/tickets/story-recorded-manual-spec-20-acceptance-checklist-verification.md` — 8.6 consumer of the manual remainder.
- `ASI.Basecode.Data/Queries/ParticipationFacts.cs` — `AttendanceRates` returns null when confirmed = 0 (line 128); `NoShows` filters Completed only (line 108); `ValidAttendances` is `InvalidatedAt == null`; `SeatHolders` is Confirmed.
- `tests/ASI.Basecode.IntegrationTests/SqlServerDatabaseFixture.cs` — `IClassFixture`, `ConnectionString`, schema boot from `database/attendta-mvp-schema.sql`.
- `tests/ASI.Basecode.IntegrationTests/EventDraftFlowTests.cs` — `EventDraftTestSupport.Context(...)` and `SeedUsersAsync(...)`.
- `tests/ASI.Basecode.IntegrationTests/EventPublicationFlowTests.cs` lines 115–173 — copy the fact-seeding shape (User/Member/Event/Registration snapshots/Attendance).
- `tests/ASI.Basecode.Services.Tests/ParticipationFactsTests.cs` — existing expression cases; do not repeat them.
- Do not touch: `ASI.Basecode.WebApp/DemoData/*`, `CheckInController`, `CertificateLedgerController`, `ReportsController`, `database/*`.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/initiative-attendta/acceptance-matrix.md` -- create the matrix with one row per expanded §20 scenario, each finale case, and each functional ID; columns: id, source, automated coverage (`Class.Method` or `none`), manual case/steps or `deferred (8.13)`, status -- it is the ticket deliverable and the 8.6 input.
- [x] `tests/ASI.Basecode.IntegrationTests/ParticipationReportFactsTests.cs` -- add the real-SQL aggregate test with 26 confirmed registrations, valid and invalidated attendance, an inactive historical member, and a zero-confirmed event -- proves REP-01 at full-dataset scale using only real code.
- [x] `_bmad-output/initiative-attendta/finale-test-cases.md` -- add one line pointing to `acceptance-matrix.md` -- keeps the finale entry set pointing at the map.

**Acceptance Criteria:**
- Given any SPEC §20 bullet or finale case, when read in the matrix, then it names an existing automated test or a concrete manual 8.6 case, or is labelled deferred with its owning entry.
- Given a local SQL Server and 26 confirmed rows, when the new test runs, then the rate, no-show and inactive-history assertions pass exactly.
- Given the diff, when searched for `DemoData`, `DemoCertificateLedgerService`, `UnavailablePassService` or `EmptyMyEventsService`, then nothing is added against them.

## Implementation Notes

- `acceptance-matrix.md` expands the nine §20 bullets into 39 scenario rows and also lists all 19
  finale cases and all 26 functional IDs plus FR-35/FR-36. Real automated coverage is named as
  `Class.Method`; every check-in, pass, certificate-ledger, report-surface, account-access command
  and demo-seam row is labelled `deferred` or `partial` with its `demo-mock-register.md` row and
  removing ticket.
- `ParticipationReportFactsTests` seeds one 26-confirmed completed event (13 valid, 5 invalidated,
  8 unattended), one inactive historical user/member with a valid attendance, and one completed
  event with only pending/waitlisted/cancelled rows. It asserts the exact 0.5 rate, 13 no-shows,
  the null zero-confirmed rate, and that the inactive member is excluded from current eligibility
  but retained in the seat and attendee totals. Existing `ParticipationFactsTests` cases are not
  duplicated.
- No demo-seam file, product code, schema or CI workflow was changed.

## Plan Change Log

## Review Triage Log

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | No-show "other statuses excluded" assertion was vacuous | medium | patch | Verified: the no-show query was filtered to the completed event while the non-confirmed rows were seeded on the zero-confirmed event, so their ids could never appear. Patched by seeding three distinct non-confirmed registrations on the completed event; 26 confirmed, 0.5 rate and 13 no-shows unchanged. |
| 2 | New-automation note stated 8 no-shows | medium | patch | Verified: the test asserts 13 (8 unattended + 5 invalidated). Note corrected to 13. |
| 3 | S20-04c and §20 bullet 4 marked `covered` without a deactivation no-promotion proof | medium | patch | Verified: the cascade tests cancel without a competing waitlisted peer on the same event. S20-04c set `partial` with deferred M-14a/M-14b (5.4/5.5) and an 8.6 ADM-01 step; the bullet-4 summary is `partial`. |
| 4 | FR-36 deferred cell omitted two owning entries | medium | patch | Verified: the pass list (M-03, 4.1) and member certificate history (M-12, 6.9) were missing. Both added. |
| 5 | `partial` legend contradicted rows using it for a manual remainder | low | patch | Verified at rows S20-02a, OPS-01, MD-04, MD-05. Legend reworded to cover a manual remainder, a deferred seam, or both. |
| 6 | S20-03c citations did not prove publication opens registration | medium | patch | Verified: the cited tests only publish and list. Added `MemberEventRegistrationFlowTests.MemberRegistersOnceWithSnapshotAndDuplicateIsRefused` (published event, registration accepted). |
| 7 | S20-08e cited a test that never touches reports | medium | patch | Verified: `MemberIsRedirectedToAccessDenied` requests only `/Member` and `/Member/Edit/{id}`. Automated cell set to `none`; 8.6 REP-01 step and M-13a owner retained. |
| 8 | S20-02f ignored existing read-only profile coverage | low | patch | Verified: `AssertReadOnlyProfile` asserts no input/select/textarea and no Save/Edit control. Row now cites `AuthenticationFlowTests.MemberProfileShowsOnlyTheAuthenticatedOwnersReadOnlyDetails` and is `partial`, with the route/write absence left manual. |

## Verification

**Commands:**
- `dotnet build` -- expected: success.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` -- expected: all pass.
- `$env:ATTENDTA_TEST_SQL="Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter FullyQualifiedName~ParticipationReportFactsTests` -- expected: all pass.

**Results (2026-11-11):**
- `dotnet build "ASI.Basecode.sln"` -- succeeded, 0 warnings, 0 errors.
- Services tests -- 271 passed, 0 failed.
- `ParticipationReportFactsTests` -- 1 passed, 0 failed (real LocalDB SQL Server via `ATTENDTA_TEST_SQL`).
