# Walkthrough: 8.15 demo rehearsal

Target: commit `bc12082` on `feature/135-demo-rehearsal` — "feat(demo): [BMAD 8.15] rehearse the demo surface and record findings".
Amended after review: commit `a040393` records the demo-polish Verify lines.
Ticket: [story-demo-rehearsal-with-impeccable-critique-and-polish.md](../tickets/epic-release-acceptance/tickets/story-demo-rehearsal-with-impeccable-critique-and-polish.md)

## Blocks

- [x] 1. Intent
- [ ] 2. Broad strokes — current
- [ ] 3. The repeatable starting state
- [ ] 4. The Home page and shell polish
- [ ] 5. Copy fixes on four screens
- [ ] 6. The presenter script and the evidence
- [ ] 7. Periphery

## 1. Intent

A person runs the demo script end to end on the demo build with `DemoData__Enabled=true`, in the Administrator, Officer and Member roles at desktop and 390px width: account request and approval, master data, event create and publish, signup and review, My Events and the pass, the door scan, completion, certificate review, issue and download, the report, access changes and the Activity Log. Run Impeccable critique on the demo-critical screens, then fix dead ends, broken links, misleading copy, any visible sign of demo data and the highest-value findings with Impeccable polish. Findings not fixed go to 8.12. This is the one critique-and-polish pass of the demo phase; it adds no new screens or backend. The demo script and its starting state are written to `_bmad-output/initiative-attendta/demo-script.md` for the presenter. Confirm that unlinked development-only pages, such as the certificate tracer and its watermarked sample, are not reachable on the demo machine. It also checks each demo-phase ticket's full Verify line once; a failing line goes back to its ticket before the demo. The script leaves out signing in as a deactivated account, because the demo does not block that login until 5.4.

Source: the ticket's Description, pasted verbatim.

## 2. Broad strokes

The change makes the demo repeatable and then proves it. A guarded SQL seed builds a known three-event starting state; the in-app demo seam reads that state at startup. A presenter script walks the three roles; the rehearsal evidence records what was seen and what was fixed. The fixes are concentrated on the Home page, which was a one-line dead end, plus four copy corrections.

Start here:
- [database/seed-demo-scenario.sql](../../../database/seed-demo-scenario.sql) — the guarded starting state.
- [demo-script.md](../demo-script.md) — the presenter script and pre-flight.
- [Views/Home/Index.cshtml](../../../ASI.Basecode.WebApp/Views/Home/Index.cshtml) — the new role-aware landing page.
- [demo-rehearsal-findings.md](../demo-rehearsal-findings.md) — the evidence and per-ticket Verify results.
- [.impeccable/critique/2026-11-07T03-32-53Z__asi-basecode-webapp-views.md](../../../.impeccable/critique/2026-11-07T03-32-53Z__asi-basecode-webapp-views.md) — the critique snapshot.

## 3. The repeatable starting state

The seed is guarded to `AttendTaMVP_Test`, `AttendTa_Demo` and `AttendTa_IT_<guid>`, takes officer/member Argon2id hashes as sqlcmd variables, and is idempotent. It creates one officer and eight member accounts, two venues, three event types, three published events (one started with eight confirmed registrations, one started, one upcoming), the registrations, one pending account request, and one audit row. The event covers use 32-character GUID keys that the cover service accepts; the pre-flight copies the images into `Common__SetupRoot`.

- [database/seed-demo-scenario.sql](../../../database/seed-demo-scenario.sql) — guard, accounts, events, registrations, idempotency.
- [DemoSeedService.cs](../../../ASI.Basecode.WebApp/DemoData/DemoSeedService.cs) — unchanged; it reads the three events and seeds the in-memory store.

## 4. The Home page and shell polish

Home was a single sentence pointing at the navigation. It now renders role-aware shortcut cards: management gets operations and records, an administrator also gets Accounts and Activity log, and a member gets events, passes, history and profile. The cards are a new `home.css` feature file imported by `Styles/app.css`; the built `app.css` is regenerated.

- [Views/Home/Index.cshtml](../../../ASI.Basecode.WebApp/Views/Home/Index.cshtml) — role-aware cards.
- [Styles/features/home.css](../../../ASI.Basecode.WebApp/Styles/features/home.css) — card grid and breakpoints.
- [Styles/app.css](../../../ASI.Basecode.WebApp/Styles/app.css) — imports the feature file.
- [wwwroot/css/app.css](../../../ASI.Basecode.WebApp/wwwroot/css/app.css) — regenerated output.

## 5. Copy fixes on four screens

Four small corrections remove stale or misleading text: the login heading and helper now match DESIGN.md screen 1; the create-event page no longer says publishing is not implemented; the event list's "Lifecycle controls" label becomes "Manage event"; and the report empty state names the Show action.

- [Views/Account/Login.cshtml](../../../ASI.Basecode.WebApp/Views/Account/Login.cshtml)
- [Views/Event/Create.cshtml](../../../ASI.Basecode.WebApp/Views/Event/Create.cshtml)
- [Views/Event/Index.cshtml](../../../ASI.Basecode.WebApp/Views/Event/Index.cshtml)
- [Views/Reports/Participation.cshtml](../../../ASI.Basecode.WebApp/Views/Reports/Participation.cshtml)

## 6. The presenter script and the evidence

The script is the deliverable a presenter follows: pre-flight, starting state, Administrator/Officer/Member beats, 390px checks, known limits and a coverage map. The findings file records 102 page records at 1440 and 390 across the three roles, the fixed findings, the open findings for 8.12, and each demo ticket's Verify result. The one partial is 4.12: the member attendance tab is empty until 4.7.

- [demo-script.md](../demo-script.md)
- [demo-rehearsal-findings.md](../demo-rehearsal-findings.md)

## 7. Periphery

- [.impeccable/critique/2026-11-07T03-32-53Z__asi-basecode-webapp-views.md](../../../.impeccable/critique/2026-11-07T03-32-53Z__asi-basecode-webapp-views.md) — critique snapshot; degraded single-context, detector clean, 28/40.
- [DemoDataStartupGuardTests.cs](../../../tests/ASI.Basecode.IntegrationTests/DemoDataStartupGuardTests.cs) — added the missing-`TokenKey` refusal test.
- [story-demo-rehearsal-with-impeccable-critique-and-polish-plan.md](../tickets/epic-release-acceptance/tickets/story-demo-rehearsal-with-impeccable-critique-and-polish-plan.md) — the plan, now `built`.
