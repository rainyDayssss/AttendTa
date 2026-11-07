---
title: 'Demo rehearsal with Impeccable critique and polish'
type: 'feature'
ticket: '15'
created: '2026-11-07'
status: 'built'
baseline_revision: 'db93b2b9be23499f6579cc55a4d2b5cac5265f74'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'docs/design.md'
  - 'PRODUCT.md'
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
  - '_bmad-output/initiative-attendta/delivery-queue.md'
  - 'docs/demo-host-setup.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The demo-critical surfaces are merged but have never been walked together; there is no presenter script or repeatable starting state, and a dead end, error page or visible demo-data sign in the client demo would undermine the MVP.

**Approach:** Prepare a repeatable demo starting state and a presenter script, run the one Impeccable critique/polish pass on the demo surface, walk every demo-critical screen in all three roles at desktop and 390px, fix dead ends/copy/demo-data leaks, and record each demo ticket's Verify result.

## Boundaries & Constraints

**Always:** Demo data stays in the in-memory store; no demo command writes SQL, files or Activity Log. No visible demo/mock/sample/fixture/prototype/simulated/test sign in UI, PDFs, filenames, console or CSS. Real services first; demo providers only where no real method exists. `docs/DESIGN.md` is the visual authority. Run with `DemoData__Enabled=true` and an allowed database; the demo build runs outside Development so the tracer 404s. Every mutation stays POST + antiforgery with role checks.

**Never:** Do not build 6.13's feature here. Do not change schema, real service behavior or demo register rows. Do not claim a human walk or project-owner sign-off. Do not commit `DemoData:TokenKey`, passwords or hashes. Do not add new screens or backend.

## Decisions

- 2026-11-07 — 6.13 is built first in a separate session; 8.15 pauses until it is merged.
- 2026-11-07 — 6.13 merged as `b4a2491` (PR #177); the rehearsal walks the merged certificate history and records any gap against 6.13.
- 2026-11-07 — The demo starting state comes from a tracked `database/seed-demo-scenario.sql` guarded to the disposable demo databases.
- 2026-11-07 — The agent runs the pre-flight browser walkthrough and records findings; the human does the final judged walk with the project owner.
- 2026-11-07 — The demo build runs with `ASPNETCORE_ENVIRONMENT=Production` so the tracer and watermarked sample are not reachable.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Demo off | `DemoData__Enabled` unset | normal empty states; no demo classes | normal errors |
| Wrong DB | `AllowDatabase` mismatch | startup refuses with a clear log | no screen |
| Missing key | no `TokenKey` | startup refuses | no screen |
| Tracer on demo build | `GET /CertificateTracer` outside Development | 404 | no page |
| Empty demo DB | no events | seed logs "seeded nothing"; script states it | pre-flight check |
| No issued certificate | member with no issued certificate | empty state, no download | recorded against 6.13 |

</frozen-after-approval>

## Code Map

- `_bmad-output/initiative-attendta/demo-script.md` — new presenter script: pre-flight, starting state, per-role beats, 390px checks, known limits, coverage map.
- `_bmad-output/initiative-attendta/demo-rehearsal-findings.md` — new evidence: Verify results, dead ends, demo-data leaks, unfixed items for 8.12.
- `database/seed-demo-scenario.sql` — new guarded starting state; follow `seed-local-admin.sql`'s `AttendTaMVP_Test`/`AttendTa_Demo` guard; hashes via sqlcmd variables, never plaintext.
- `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs` — read-only seed; needs one started published event with confirmed registrations (completes it, ≤8 check-ins, one invalidation, pending/declined/issued reviews), a second started event and a third published event. Do not change.
- `ASI.Basecode.WebApp/DemoData/` — `DemoStore.cs` (in-memory state), `DemoDataStartupGuard.cs`, `DemoDataOptions.cs`, `DemoDataRegistration.cs` (switch, guard, wiring); read-only unless a demo command is wrong.
- `ASI.Basecode.WebApp/Program.cs` — `--demo-readiness`, `--demo-pass-token <registrationId>`.
- `ASI.Basecode.WebApp/Controllers/CertificateTracerController.cs` — 404 unless Development.
- `ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml`, `_Header.cshtml`, `_NavLinks.cshtml` — shell/nav critique surface.
- `ASI.Basecode.WebApp/Views/` — demo screens: `MyEvents/` (Index, `_RegistrationsTab`, `_AttendanceHistoryPartial`, `_CertificateLinkPartial`, `_PassPartial`), `Passes/`, `CheckIn/`, `CertificateLedger/`, `MyCertificates/`, `Reports/Participation`, `AdminAccounts/`, `Member/`, `Event/`, `EventType/`, `Venue/`, `RegistrationReview/`, `ActivityLog/`, `AccountRequest/`.
- `ASI.Basecode.WebApp/Styles/features/*.css` + `wwwroot/css/app.css` — regenerate after view/style edits.
- `.impeccable/critique/` — critique snapshots; `.opencode/commands/impeccable.md` — critique/polish commands.
- `docs/design.md` (DESIGN.md), `PRODUCT.md` — visual authority.

## Tasks & Acceptance

**Execution:**
- [x] `database/seed-demo-scenario.sql` — add a guarded, idempotent starting state: three published events (one started with confirmed registrations, one started, one upcoming), confirmed registrations, and officer/member accounts from sqlcmd hash variables — makes the rehearsal repeatable and feeds `DemoSeedService`.
- [x] `_bmad-output/initiative-attendta/demo-script.md` — write pre-flight, starting state, per-role beats (account request/approval, master data, event create/publish, signup/review, My Events/pass, door scan, completion, certificate review/issue/download, report, access changes, Activity Log), 390px checks, known limits and coverage map.
- [x] Run the demo build (`ASPNETCORE_ENVIRONMENT=Production`, `DemoData__Enabled=true`, `DemoData__AllowDatabase=AttendTaMVP_Test`, local `DemoData__TokenKey`) and walk each beat in Administrator, Officer and Member at 1440px and 390px; record every dead end, error page, broken link, misleading copy and demo-data sign.
- [x] `.impeccable/critique/` — run `/impeccable critique` on the demo surface, then `/impeccable polish` on the highest-value findings.
- [x] `ASI.Basecode.WebApp/Views/**` and `Styles/features/*.css` — fix the findings; regenerate `wwwroot/css/app.css`.
- [x] `_bmad-output/initiative-attendta/demo-rehearsal-findings.md` — record each demo ticket's Verify result; failures name the ticket; unfixed findings go to 8.12.
- [x] Confirm `/CertificateTracer` and `/CertificateTracer/Trace` return 404 on the demo build; record the check.

**Acceptance Criteria:**
- Given the demo build with `DemoData__Enabled=true`, when the script is walked in each role at desktop and 390px, then no dead end, error page or visible demo-data sign appears.
- Given the script, when a presenter follows it, then the starting state and every step are explicit and reproducible.
- Given each demo ticket's Verify line, when checked once, then the result is recorded and every failure names its ticket.
- Given the demo build, when `/CertificateTracer` is requested, then it is not reachable.
- Given the critique, when polish is applied, then the highest-value findings are fixed or listed for 8.12.

## Implementation Notes

- Built from `db93b2b`. The scenario seed is guarded to `AttendTaMVP_Test`, `AttendTa_Demo` and `AttendTa_IT_<guid>`, takes officer/member Argon2id hashes as sqlcmd variables, and is idempotent (re-runs keep counts and re-anchor event dates).
- `DemoSeedService` needs one started published event with confirmed registrations, a second started event and a third published event; the seed supplies exactly that and the rehearsal log showed `completed event 1; ready to complete event 2; check-in open event 3`.
- Rehearsal evidence: 102 page records at 1440×900 and 390×844 across Administrator, Officer and Member; 100 × 200, two intentional `/CertificateTracer` 404s; no overflow, no demo/mock/sample/fixture/prototype/simulated/test text, no broken images. Interactive beats and the per-ticket Verify results are in `demo-rehearsal-findings.md`.
- Fixed during polish: Home role-aware shortcuts (replacing a one-line dead end), login heading/helper, stale create-event copy, "Lifecycle controls" → "Manage event", report empty-state copy, plus seed fixes (pending request, mobile numbers, GUID cover keys, loop variable resets).
- Critique ran degraded: the harness blocks nested subagents (depth limit 1), so the snapshot carries the required degraded banner; the bundled detector returned `[]` and the score is 28/40. The P2 findings are recorded for 8.12, not fixed here.
- Matrix audit: rows 2 and 4 are covered by `DemoDataStartupGuardTests` and `CertificateTracerFlowTests` (both ran and passed). Row 3 gained a missing-TokenKey guard test in the same 1.14 test class (342 integration tests pass). Rows 1, 5 and 6 are covered by the demo-off suites and the rehearsal's manual checks; the demo-phase rule adds no automated tests beyond 1.14's guard test.
- Left open and recorded: 4.12 is partial because the member attendance read is empty until 4.7 (M-08); the create-event Publish action, the redundant event-row links, the report Show step and the generic publish refusal go to 8.12. None blocks the demo script.
- Post-merge adaptation (PR #187, 3.12): the merged `DemoSeedService` no longer marks an event completed at startup. The seed now leaves Leadership Summit ready to complete (completed live in O3) and marks Community Outreach cancelled; the script and findings were updated, and the merged build re-verified (286 services + 361 integration tests).
- Verify-line audit: the six demo-polish tickets (3.18, 4.13, 2.12, 2.13, 2.14, 2.10) now have their own rows in the findings and the coverage map. Each is Partial because the rehearsal opened the surface but did not run the full Verify line; each is recorded against its ticket for 8.12, and 8.15's result names them.

## Review Triage Log

- 2026-11-07 — quick lens, review pass 1. Verdicts: high 0 / medium 0 / low 0 / false 0 / maybe-false 0. No unmet acceptance criteria, broken rules or bugs found. Checked: the Home shortcut targets are authorized for their roles (`MemberEvents` is `[Authorize]`; `Passes`/`MyEvents` are Member-only and only shown to members; Accounts/Activity log are admin-gated); `page-heading` is an existing shared class; `ClaimTypes.Name` carries the full name; the pre-flight asset paths exist; the seed's columns and lookup ids match the schema and the seed ran idempotently. The critique snapshot's degraded banner is disclosed and accepted: the harness blocks nested subagents, and 8.12 owns the whole-app critique/polish.

## Design Notes

- The script is a presenter document: numbered beats, role, route, expected outcome, 390px check.
- Findings are evidence: screen, role, width, observed vs expected.
- Polish only the demo surface; 8.12 owns the whole-app pass.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — expected: success.
- `cd ASI.Basecode.WebApp; npm run css:build` — expected: success, no CSS drift.
- `npm run test:js` — expected: pass.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: pass.
- `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` with `ATTENDTA_TEST_SQL` — expected: pass.
- Start the demo build; `Invoke-WebRequest <app-url>/CertificateTracer` — expected: 404.
- `dotnet run --project ASI.Basecode.WebApp -- --demo-readiness` — expected: exit 0 on a provisioned host (record, do not block the rehearsal).

**Manual checks:**
- Walk the script in each role at 1440px and 390px; record in the findings file.
