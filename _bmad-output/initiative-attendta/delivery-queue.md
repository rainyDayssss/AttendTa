# AttendTa demo-first shared delivery queue

Decision: 2026-11-06 — demo rescope, approved by the project owner: the client demo is close,
so the remaining work runs as a demo phase (frontend only, demo data where the backend is
missing), then the integration phase (real backend replaces each mock), then the finale
(hardening, mock removal proof, polish, broad tests, acceptance). R1–R36 and the final MVP
acceptance goals are unchanged. This supersedes the earlier frontend-first ordering below where
they differ; the earlier claim protocol and source boundaries still apply.
Five interchangeable builders self-claim the next unblocked ticket; no fixed person/module lanes.

**Live GitHub queue:** [AttendTa frontend-first shared delivery](https://github.com/rainyDayssss/AttendTa/milestone/1).
The GitHub issues were **not** updated for the demo rescope; until someone syncs them, this file
and the `tickets.toml` entries on branch `docs/demo-frontend-replan` are the current order.
The eight capability epics stay local; do not create duplicate epic issues.

Current dependency graphs: [dependency-graphs-demo-first.md](dependency-graphs-demo-first.md).
[delivery-waves.md](delivery-waves.md) and the superseded
[dependency-graphs-frontend-first.md](dependency-graphs-frontend-first.md) predate the demo
rescope; use them for background only.

**First handoff:** [#136 — 1.13 shared shell and routes](https://github.com/rainyDayssss/AttendTa/issues/136)
and 1.14 (demo data seam; no issue yet) start now, in parallel. After both, 3.16, 3.17, 4.10,
6.12, 7.4, 4.12, 6.13 and 5.11 are independent demo-critical surfaces. Writing this plan did not
start any of them.

Demo data rules and the list of every mock with its removing ticket:
[demo-mock-register.md](demo-mock-register.md).

## Baseline and authority

- Work from integrated `dev`, including PR #134 (Master Data filters/profile) and PR #133
  (start cutoff), merge `997f290`. Do not rebuild these features or reopen completed history.
- GitHub issue state and merged evidence must be reconciled before dispatch. Local plan-file
  `built`/`in-review` text is historical build evidence, not a fresh claim or GitHub closure.
- The `tickets.toml` entries and pulled leaf files are the requirements. `after` contains real
  prerequisites; `phase`, `area` and numeric `queue` order the remaining shared work.
- `docs/DESIGN.md` remains the visual authority. The separate `F:/AttendTa` prototype is a
  read-only visual source, not a second app or a source of production business rules.
- The architecture's one writer per table/module still applies to code. It does **not** assign
  a permanent person to that module. Historical WBS owners remain attribution/context only.
- Old dependency waves, `bmad-progress.*` and older ownership tables are historical snapshots,
  not today's dispatch queue. Always check the live issue before starting.

## Phases

| Phase | What runs | Ends when |
|---|---|---|
| Demo | 1.13 and 1.14, then the demo-critical surfaces, then the demo-polish surfaces; frontend only, real backend where it exists, demo data where it is missing. 8.14 host readiness may run alongside. | 8.15 rehearsal walks the demo script and every demo-phase Verify line without a dead end; demo-polish tickets not merged by then are shown as they are. |
| Integration | Backend tickets in the order below; each replaces its register rows with no visible change to the screen. | Every register row except M-01 is `removed`. |
| Finale | Sweeps, 8.13 hardening and removal of the demo seam (M-01), 8.11 verification that no demo data remains, 2.11 and 8.12 frontend acceptance and polish, 8.10 cleanup, 8.5/8.4 tests, 8.2/8.3 device and storage, 8.6–8.8 acceptance, 8.9 release. | Initiative Done when. |

Ranks are tie-breaks among unblocked work, not a demand to wait for every earlier number.
If time slips, record the actual remaining work; never call a demo-data screen integrated.

## Claim and status protocol

1. Check GitHub for an open, unclaimed issue with satisfied native blockers. Select the lowest
   numeric queue rank in the earliest phase with available work; prefer frontend first.
2. Comment `Claim: <person/session>`, assign yourself where possible, and change `backlog` to
   `in-progress` **before editing**. Re-read the issue: an earlier claim wins; a second builder
   backs off. Shared-account sessions must use distinct session names in their claim comments.
3. Keep one active implementation per builder. A blocked ticket says exactly what it awaits;
   mark it `blocked`, keep its blocker links current and take another unblocked ticket only
   after releasing/parking the claim explicitly. Clear the block on resolution.
4. Move to `review` with PR, checks and handoff evidence; another available builder can review.
   Close only after the ticket's own acceptance is true. A demo-phase slice can finish while
   its linked functional issue stays open. Never close an epic merely because children close.
5. Update the GitHub issue and BMAD mirror together using the configured tracker workflow.
   Read the current remote body before editing; do not overwrite another builder's new scope.

No permanent assignments are created by this replan. Historical assignees on unstarted backlog
items are cleared for the shared pool; active claims/reviews are not silently reassigned.

## Demo phase queue

All rows are frontend only: no new controller logic beyond calling existing services or the
demo providers, no schema change, no new service in Services or Data. Keep the incumbent visual
identity and follow `docs/DESIGN.md`; builders may use Impeccable's craft guidance while
building. A ticket is done on `dotnet build`, `npm run css:build` when views or styles change,
and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests (1.14's
startup guard test is the one exception); existing CI stays green; each pull request keeps one
peer approval and there is no separate code-review pass. Verify lines use seeded data only.
The 8.15 rehearsal runs Impeccable critique and polish once and checks every demo-phase
ticket's full Verify line once; a failing line goes back to its ticket before the demo.

**Demo-critical** (the demo script needs them):

| Rank | Ref | Outcome / handoff | Register rows | Prerequisite |
|---:|---|---|---|---|
| 10 | 1.13 / #136 | Role navigation, routes and authorized actions with typed contracts for every demo screen, the My Events host with its slot partials, the Accounts and Members access-action slots, the shared event selector, feature asset seams. | — | — |
| 15 | 1.14 | Demo data seam: switch with startup database guard, demo store with stable pass tokens, seed, open/close check-in and complete event commands with real refusals, the shared check-in control, the event-state overlay. | M-01, M-04, M-09 | — |
| 20 | 3.16 | Officer event dossier with contextual roster, real review/promote/cancel, completion and check-in window controls. | uses M-04, M-09 | 1.13, 1.14 |
| 30 | 3.17 | My Events host content and the Registrations tab. | M-02 | 1.13, 1.14 |
| 40 | 4.10 | Pass cards with a scannable QR and PNG download in the `_PassPartial` slot. | M-03 | 1.13, 1.14 |
| 50 | 6.12 | Certificate ledger, decisions, issuance and PDF viewer on the real template. | M-10a, M-10b, M-11a, M-11b | 1.13, 1.14 |
| 60 | 7.4 | Per-event participation report. | M-13a, M-13b, M-13c | 1.13, 1.14 |
| 70 | 4.11 | Door scanner with live camera decoding, outcomes, manual lookup and invalidation. | M-05, M-06, M-07 (uses M-04) | 1.13, 1.14, 4.10 |
| 80 | 4.12 | Member attendance history in its slot. | M-08 | 1.13, 1.14 |
| 90 | 6.13 | Member certificate history and download links in its slot. | M-12 | 1.13, 1.14 |
| 100 | 5.11 | Account and membership access dialogs in the 1.13 row slots, on the real preview. | M-14a, M-14b, M-15a, M-15b | 1.13, 1.14 |
| 110 | 8.15 / #135 | Demo rehearsal (was 1.12): walk the demo script and every demo-phase Verify line, Impeccable critique and polish of the demo surface, fix dead ends. Needs a person. | — | 1.13, 1.14 and every demo-critical row |

**Demo-polish** (cosmetic or residual; real backend already exists, no demo data):

| Rank | Ref | Outcome / handoff | Prerequisite |
|---:|---|---|---|
| 120 | 3.18 | Member feed/detail visual alignment; keep the real signup, cancellation and cutoff. | 1.13 |
| 130 | 4.13 / #129 | Profile action to My Passes. | 4.10 |
| 140 | 2.12 | Shared centered record dialog and Event Type editor. | 1.13 |
| 150 | 2.13 | Member editor in the shared dialog; leaves the access-action slot alone. | 2.12 |
| 160 | 2.14 | Venue editor in the shared dialog. | 2.12 |
| 170 | 2.10 / #127 | Residual list loading/error/retry states. | 1.13, 2.12, 2.13, 2.14 |

With five builders the demo-critical set takes about four rounds: 1.13 and 1.14; then 3.16,
3.17, 4.10, 6.12, 7.4, 4.12, 6.13 and 5.11 as builders free up; then 4.11; then 8.15. These are
queue options, **not assignments to five named builders**. 2.11 (#128) is an acceptance check
of the three editors and runs in the finale.

### Source boundaries that make parallel work safe

- 1.13 owns shared shell/navigation, routes, the controller actions and typed contracts for the
  demo screens, global JS and primitive asset setup, the My Events host with its three slot
  partials, the Accounts and Members access-action slot partials and the event selector. Its
  handoff must name actual files and contracts before consumers start.
- 1.14 owns the demo data folder, the registration method, the demo store, the seed, the
  event-state commands, the check-in control partial and the event-state overlay.
- 3.17 owns the My Events host content and the Registrations tab. 4.10, 4.12 and 6.13 fill only
  their slot partials and providers and change only their own action's model loading.
- 4.11 owns the scanner page, the locally served browser QR decoder and separate camera and manual
  adapters that post to the check-in contract (served by demo data until 4.3 and 4.5). 4.4
  verifies the camera against the real engine; manual fallback does not wait for the camera.
- 2.12 owns the dialog primitive and Event Type tracer; 2.13/2.14 consume it in separate editors.
  5.11 fills only the two access-action slot partials 1.13 placed in the Accounts and Members
  rows; 2.13 changes only the Member Edit trigger and form. They touch different files.
- 1.14's registration method calls one registration file per surface. Each surface adds its
  providers to its own registration file and its own register rows; a slice does not edit
  another slice's providers or registration file.
- Backend tickets extend the existing owner service/hook, not a duplicate rule implementation.
  Access mutations serialize their shared administration changes; certificate handlers, queries
  and issuance use separate agreed files where they run in parallel.
- Declare a new shared-file dependency before changing another ticket's source boundary.
  `Startup.DI.cs` and global source CSS/JS changes require a single integration claim. Generated
  `wwwroot/css/app.css` is regenerated from current sources after merges, never hand-merged.

## Integration phase queue

Each row removes the register rows it names, and exactly one ticket removes each row: it
implements the real service behind the same contract, deletes the demo provider classes and
their line in the surface's registration file, and sets the rows to `removed` in
[demo-mock-register.md](demo-mock-register.md). The screen does not change visibly. When another
open row still reads the data it made real, the remaining demo provider reads the real source
from that commit on (register rule 12). No per-ticket Impeccable critique or polish (that is
8.12). Existing full business guards and final acceptance remain.

| Rank | Ref / existing issue | Real delivery | Removes |
|---:|---|---|---|
| 190 | 8.14 / #150 | Actual demo-host evidence/fixes following tooling-only #74; prepare HTTPS inputs early. May run during the demo phase. | — |
| 200 | 3.13 / #42 | Real owner-scoped registrations query in the existing 3.17 host. | M-02 |
| 210 | 3.12 / #41 | Completion transaction, close check-in, completion hook. | M-09 |
| 220 | 4.1 / #44 | Real secure QR PNG and authorized download in the 4.10 presentation. | M-03 |
| 230 | 4.2 / #45 | Real audited check-in window command, control and open flag. | M-04 |
| 240 | 4.3 / #46 | Shared validated, transactional, idempotent attendance engine; waits on 4.1 so passes carry real tokens first. | M-05 |
| 250 | 5.4 / #55 | Real account deactivation, cascade and session refusal. | M-14a |
| 260 | 5.5 / #56 | Real account reactivation. | M-14b |
| 270 | 5.6 / #57 | Real membership deactivation and cascade. | M-15a |
| 280 | 5.7 / #58 | Real membership reactivation. | M-15b |
| 290 | 6.4 / #64 | Create reviews through the completion hook and canonical attendance facts. | M-10a |
| 300 | 4.4 / #47 | Verify 4.11's camera adapter against the real engine. | — |
| 310 | 4.5 / #48 | Real manual search and check-in; no search side effects. | M-06 |
| 320 | 4.6 / #49 | Audited attendance invalidation and hook declaration. | M-07 |
| 330 | 4.7 / #50 | Real owner-scoped attendance source in the existing history partial. | M-08 |
| 340 | 6.5 / #65 | Invalidation/issued guard; replaces the store's invalidation-to-review rule. | — |
| 350 | 6.6 / #66 | Real review queue and decisions. | M-10b |
| 360 | 6.7 / #67 | Retained issuance. | M-11a |
| 370 | 6.8 / #68 | Authorized PDF download. | M-11b |
| 380 | 6.9 / #69 | Real member certificate links. | M-12 |
| 390 | 7.1 / #71 | Real report summary. | M-13a |
| 400 | 7.2 / #72 | Real no-shows and snapshot distributions. | M-13b |
| 410 | 7.3 / #73 | Real paged detail rows. | M-13c |

Ranks are tie-breaks among **unblocked** work, not a demand to wait for every earlier number.
Mapped attendance/facts let review creation and report query implementation start without waiting
for the whole scanner UI. Final real-data proof still waits for all required integrations.

## Final project phase

| Rank | Ref / issue | Acceptance responsibility |
|---:|---|---|
| 450–480 | 3.14 / #43, 4.9 / #51, 5.10 / #60, 6.11 / #70 | Focused backend module cleanup from concrete findings; no repeated whole-app UI review. |
| 500 | 8.13 / #151 | Delete the demo seam (M-01) once every other row is removed, then cross-module backend hardening; preserve transactions, locks, facts, auth, hooks and storage. |
| 510 | 8.11 / #152 | Verify only: every register row is `removed`; the published build starts normally with the switch on, no token key and a non-demo database (the guard refuses this while the seam exists); no register id, demo folder or `DemoData` key in source or published output; real integrations end to end. Findings return to the owning ticket. |
| 515 | 2.11 / #128 | Accept the three centered editors against `docs/DESIGN.md`; fixes only. |
| 520 | 8.12 / #153 | Final frontend walkthrough, targeted refactor, Impeccable implementation/critique/polish. |
| 530 | 8.10 / #83 | Release cleanup **before** acceptance tests and release. |
| 540 | 8.5 / #78 | Add/reuse only important automated cases; map every other SPEC scenario to manual verification. |
| 550 | 8.4 / #77 | One combined existing/new automated run with environment and commit evidence. |
| 560–570 | 8.2 / #75, 8.3 / #76 | Actual trusted-HTTPS phone scanning and backup/restore/redeploy proof. |
| 580 | 8.6 / #79 | Complete manual SPEC checklist; reuse automated/UI/device evidence. |
| 590 | 8.7 / #80 | Full 26-function plus FR-35/FR-36 demo continuity and evidence. |
| 600 | 8.8 / #81 | Human Tier 3 acceptance; retest only missing/affected cases. |
| 610 | 8.9 / #82 | Release PR and human merge **last**, with peer review and green CI. |

The test cases and release blockers are in [finale-test-cases.md](finale-test-cases.md).
No implementation, review, execution evidence or human sign-off is claimed by writing this plan.

## Demo data register and removal rule

The rules and the full list live in [demo-mock-register.md](demo-mock-register.md). In short:
one `DemoData:Enabled` switch, refused at startup unless the connected database is
`AttendTa_Demo` or `DemoData:AllowDatabase` names it; no visible sign in the frontend; no SQL,
file or Activity Log writes; the real refusal rules on every demo command; one coherent
in-memory store keyed by real records; and every mock has a row naming the one ticket that
removes it. 8.13 removes the seam; 8.11 verifies that no row is open.

## Development verification policy

- Demo phase: `dotnet build`, `npm run css:build` when views or styles change, one look at the
  changed screen with demo data on. No new automated tests except 1.14's startup guard test, and
  no per-ticket Impeccable critique or polish. 8.15 runs critique and polish once for the demo
  surface and checks every demo-phase ticket's full Verify line once.
- Integration phase: build, one primary/guard check and a focused regression when changing a live
  high-risk mutation. Keep existing protections/tests; do not introduce broad per-ticket quotas.
- Finale: 8.5 fills important coverage gaps, 8.4 executes the complete existing suite, 8.12 runs
  the whole-app Impeccable pass, and 8.6–8.8 provide full scenario/manual/demo acceptance.
- Existing required PR CI (build, both test projects, CSS drift check) and one peer approval stay
  on every pull request in every phase.
- Do not invent lint/typecheck scripts: the web package has `css:build` and `test:js`, not a
  separate lint/typecheck command. Use the repository's actual .NET/CI commands.

## Decisions and unresolved inputs

- Q1 signatory setup shipped in #63/PR #102; reuse it. Official final PDF acceptance still runs.
- Q2 needs a real host address/certificate decision and phone verification; preparation can
  start early, but #75 cannot pass on a localhost screenshot alone.
- #74 closed the provisioning tooling; 8.14 owns the still-unproven host/SQL/log/offline checks.
- Report wording follows the existing SPEC-over-PRD precedence and `docs/DESIGN.md`; do not
  invent a new PM override or extra cross-event metrics while implementing the report.
- All final MVP goals remain required. Cosmetic extras can be reduced only as recorded scope
  decisions; remaining mocks, unsafe mutations or unverified major tests block final completion.
