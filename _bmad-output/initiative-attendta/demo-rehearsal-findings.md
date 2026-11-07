# Demo rehearsal findings — 8.15

Evidence for the 7 November 2026 demo rehearsal. The presenter steps are in
`demo-script.md`. This file records what was walked, what was fixed, what remains for 8.12,
and each demo ticket's Verify result.

## 1. Scope and method

- Build: `ASPNETCORE_ENVIRONMENT=Production`, `DemoData__Enabled=true`,
  `DemoData__AllowDatabase=AttendTaMVP_Test`, a local `DemoData__TokenKey`, and the
  disposable LocalDB database `AttendTaMVP_Test`.
- Starting state: `database/seed-demo-scenario.sql` after the schema, admin seed, and
  event-type seed. The database held 2 published events, 1 cancelled event, 9 members,
  10 users, 15 registrations, and 1 pending account request.
- Post-merge adaptation: PR #187 (3.12 event completion) merged after the first walk. The
  seed now leaves Leadership Summit ready to complete and the presenter completes it live
  through the real service; Community Outreach is cancelled. The affected beats were
  re-checked against the merged build.
- Roles: Administrator (`maria.dizon@uc.edu.ph`), Officer
  (`rafael.santos@uc.edu.ph`), Member (`liza.fernandez@uc.edu.ph`).
- Widths: 1440×900 and 390×844. The in-app browser was used for interactive beats; a
  Playwright walker captured every route at both widths (102 page records, screenshots and
  `report.json` under `%TEMP%\attendta-walk-output`).
- The walker checked HTTP status, final URL, document overflow, broken images, console
  errors, and visible text for `demo`, `mock`, `sample`, `fixture`, `prototype`, `simulated`,
  and `test` as whole words.
- The demo store was restarted between destructive beats so the final database state stayed
  the clean scenario.

## 2. Result summary

| Check | Result |
|---|---|
| Routes returning 200 | 100 of 102 |
| Routes returning 404 | 2 — both `/CertificateTracer` (intentional, Production) |
| Document horizontal overflow at 1440 or 390 | 0 |
| Visible demo/mock/sample/fixture/prototype/simulated/test words | 0 |
| Broken images after load | 0 (the first pass reported lazy-loaded covers before they loaded; a forced eager load returned natural widths) |
| Dead ends or error pages in the walked beats | 0 after the fixes below |
| `/CertificateTracer` GET (authenticated Administrator) | 404 |
| `/CertificateTracer/Trace` POST (authenticated Administrator, with antiforgery token) | 404 |
| Console errors | Only the intentional tracer 404 |

## 3. Fixed findings

### F1 — Home page was a one-line dead end

- **Screen:** `/Home/Index`, all roles.
- **Observed:** "Welcome to AttendTa / Use the navigation to access the pages available to
  your account." No next step and no role-specific entry points.
- **Expected:** a presenter or member can start from Home.
- **Fix:** role-aware shortcut cards for management (operations and records) and members
  (events, passes, history, profile), with the admin-only Accounts and Activity log cards.
  `Views/Home/Index.cshtml`, `Styles/features/home.css`, `Styles/app.css`.
- **Re-check:** 1440 and 390 show the cards, one column at 390, no overflow.

### F2 — Create-event copy said publishing was not implemented

- **Screen:** `/Event/Create`.
- **Observed:** "Publishing arrives in a later story." Publishing is implemented from the
  Events list.
- **Fix:** "Save the draft, add a cover image, then publish it from the Events list."
  `Views/Event/Create.cshtml`.
- **Re-check:** the copy matches the walked create → cover → publish flow.

### F3 — Event list used the internal label "Lifecycle controls"

- **Screen:** `/Event`.
- **Observed:** a published row showed both "View dossier" and "Lifecycle controls", which
  open the same dossier. The second label is internal jargon.
- **Fix:** renamed to "Manage event". `Views/Event/Index.cshtml`.

### F4 — Login heading did not match DESIGN.md

- **Screen:** `/Account/Login`.
- **Observed:** "Sign in" with "Sign in to your AttendTa account."
- **Expected:** DESIGN.md screen 1 uses "Sign in to your account" and the D-01 helper.
- **Fix:** heading "Sign in to your account" and "Enter the email address registered to your
  account." `Views/Account/Login.cshtml`.

### F5 — Report empty state did not name the next action

- **Screen:** `/Reports/Participation` with no `eventId`.
- **Observed:** the selector already showed an event, but the panel said "Select an event to
  view its participation report." The presenter had to infer the Show button.
- **Fix:** "Select an event above and choose Show to load its participation report."
  `Views/Reports/Participation.cshtml`.
- **Re-check:** the Show button loads the selected event; no service behavior changed.

### F6 — Account requests queue opened empty

- **Screen:** `/AccountRequest`.
- **Observed:** 0 pending, so the approval beat had no starting record.
- **Fix:** the scenario seed now inserts one pending request for Camille Torres
  (`UC-2026-0110`) only when that email has no account and no pending request.
  `database/seed-demo-scenario.sql`.
- **Re-check:** the queue shows 1 pending; approving creates the member account; the Activity
  Log records `account_request.approved` and `member.created`.

### F7 — Member mobile numbers were blank

- **Screen:** `/Member`, `/Profile`.
- **Observed:** the Mobile column and the profile's "Mobile number" field were empty for the
  seeded participants.
- **Fix:** the seed fills `0917-000-0102` … `0917-000-0109` only where the member's mobile
  number is still empty. `database/seed-demo-scenario.sql`.

### F8 — Seeded event covers used non-GUID file names

- **Screen:** `/MemberEvents`, event detail hero.
- **Observed:** the feed rendered cover images, but `/EventCover/Image?id=…` returned 404
  because the event cover service requires an `uploads/events/<32-hex-guid>.jpg` key.
- **Fix:** the seed now stores GUID file names and the pre-flight copies the tracked prototype
  images to those names. `database/seed-demo-scenario.sql`, `demo-script.md` §2.3.
- **Re-check:** the three covers load at 1440 and 390.

### F9 — Scenario seed variable carry-over

- **Observed during authoring:** `SELECT @var = …` left the previous iteration's value when a
  row was missing, so the participant loop inserted one member and skipped the rest.
- **Fix:** reset the participant and registration variables before each lookup; the seed now
  produces 8 confirmed registrations on the completed event, 2 on the ready event, and 3
  confirmed + 1 pending on the upcoming event. `database/seed-demo-scenario.sql`.

## 4. Impeccable critique and polish

- **Snapshot:** `.impeccable/critique/2026-11-07T03-32-53Z__asi-basecode-webapp-views.md`.
- **Method:** degraded single-context. The harness blocks nested subagents (depth limit 1), so
  the two isolated assessments could not run; the critique used the bundled detector plus the
  full-role 1440/390 walker evidence. The snapshot carries the required degraded banner.
- **Detector:** `impeccable detect --json ASI.Basecode.WebApp/Views` returned `[]` (clean,
  exit 0).
- **Score:** 28/40 (Good). The surface is coherent and product-specific; the weakest area is
  the create-event flow.
- **Polish applied in this pass:** the Home dead end, the stale create-event copy, the
  "Lifecycle controls" label, the login heading/helper, the report empty-state copy, the
  pending account request, the member mobile numbers, and the GUID event-cover keys. These
  were the demo blockers and the highest-value fixes.
- **Polish deferred to 8.12:** the create-event Publish action and cover step, the redundant
  "View dossier"/"Manage event" pair, the member attendance empty state (4.7), the report
  default Show click, and the generic publish refusal message. These are the critique's P2
  issues and are listed in §5.

## 5. Open findings for 8.12

These are recorded, not fixed here. None blocks the demo script.

1. **Member attendance history is empty until 4.7 (M-08).** The demo store records the door
   check-in, and the scanner and report show it, but `MyEventsService.GetAttendanceAsync` is
   still the real empty read. The 4.12 Verify line is partial for this reason. Owner: 4.12 /
   4.7. The demo script tells the presenter to use the scanner log or report for attendance.
2. **The Activity Log omits demo-store actions.** Check-ins, completion, certificate
   decisions, and issuance do not write SQL or the Activity Log (register rule 4). The
   account-request approval and member creation are real and do appear. This is expected;
   document it in the presenter notes.
3. **Publish refusal message is generic when the cover is missing.** The service returns
   "This event no longer meets the publication requirements." The demo script adds the cover
   step; a more specific message is a copy improvement for 8.12.
4. **The create-event form has no Publish action.** DESIGN.md screen 9 lists Cancel, Save
   Draft, and Publish/Open Signup; the current form saves a draft and the Events list
   publishes after the cover upload. Adding a create-and-publish action is a controller
   change outside this rehearsal; record it for 8.12.
5. **The demo store resets on restart.** Check-ins, reviews, and issued documents last only
   for the process lifetime. Passes keep scanning because the token is the HMAC of the
   registration id under `DemoData__TokenKey`. Expected.
6. **The demo pass token is the rule-10 HMAC**, not `registrations.qr_token`. Known limit.
7. **`--demo-readiness` is not provisioned on this machine.** The command reported FAIL for
   the runtime SQL identity/audit check and FAIL for the localhost login check (the app was
   stopped), PASS for private storage, PASS for Playwright Chromium PDF rendering, and the
   INFO line that offline status is not verified; exit 1. This is expected on an
   unprovisioned host; 8.14 owns host provisioning and dated evidence.
8. **The report's zero-confirmed N/A path was reviewed, not exercised.** The seeded state
   has no published event with zero confirmed registrations. The view renders N/A when the
   rate is null; a fresh zero-confirmed event would confirm it in 8.12.

## 6. Demo ticket Verify results

| Ticket | Verify result | Evidence / note |
|---|---|---|
| 1.13 / #136 | **Pass** | Every navigation link opened an authorized screen. A Member was sent to `/Account/AccessDenied` for Event, Check-in, Certificates, Reports, Members, Accounts, and Activity log; an Officer was refused Accounts and Activity log. My Events tabs, Accounts rows, and Members rows rendered with no placeholder copy. Existing suites pass. |
| 1.14 | **Pass** | Both startup guard integration tests pass (wrong database and missing `TokenKey`). With the app running, the seeded list/feed/detail show the ready-to-complete and check-in-open state. Completing before start returned "An event cannot be completed before it starts." Opening check-in on a draft and completing a cancelled event returned "This action is only available for a published event." `--demo-pass-token 12` returned the same token twice and matched the pass page. SQL `attendance` and `certificates` stayed at 0 rows; the Activity Log held only the two seed rows. Switch-off suites pass. |
| 3.16 | **Pass** | The dossier showed metadata, capacity, and the status-aware roster. Approve/decline/cancel controls were present; check-in open/close worked; Leadership Summit completed with the dialog; completing the cancelled Community Outreach and completing before start were refused. 1440 and 390 had no overflow. |
| 3.17 | **Pass** | My Events opened on Registrations, listed confirmed and cancelled records, and the three tabs paged independently. No demo sign. |
| 4.10 | **Pass** | Each confirmed registration had a pass card with a 180×180 QR canvas and an enabled Download PNG (`door-pass-12.png`, 2,782 bytes). The cancelled registration had no usable pass. One column at 390, two at 1440. |
| 4.11 | **Pass** | Pass-code scan returned "Liza Fernandez checked in." with identity, student number, and Manila time; a repeat returned the original time; manual lookup found Liza and showed "Checked in"; the invalidation dialog named the participant and required a reason. No demo sign. |
| 4.12 | **Partial** | The Attendance tab opened and showed its empty state at both widths, with no self-check-in or correction control. The seeded check-in is only in the demo store, so the member history is empty until 4.7 (M-08). Record against 4.12 / 4.7. |
| 5.11 | **Pass** | The account preview listed affected registrations; the Deactivate dialog showed the consequence and required a reason; the last active Administrator showed the blocked explanation; the membership dialog kept account and membership states distinct. Empty reasons are refused by the shared dialog. 1440 and 390. |
| 6.12 | **Pass** | After completing Leadership Summit in O3, the ledger showed a review per valid check-in; approving moved the row to Approved; issuing produced `AT-2026-000002` and the PDF viewer returned 200; declining Beatrice Lim with a reason moved the row to Not eligible with the reason and "Decision final". No demo sign in the page or PDF. 1440 and 390. |
| 6.13 | **Pass** | The member saw the issued Leadership Summit certificate with View/Download PDF and the Founders' Week Unavailable state. The download produced `AT-2026-000001.pdf` (445,686 bytes, `application/pdf`). No demo sign. 390 fits. |
| 7.4 | **Pass** | Leadership Summit showed 8 confirmed, 7 valid, 87.5%, 1 no-show, and course/year breakdowns that agreed with the seeded check-ins. Community Outreach (cancelled) showed no final rate and no no-shows. Founders' Week showed the not-final markers. Summary totals did not depend on the detail page. 1440 and 390. The zero-confirmed N/A path was reviewed, not exercised (open finding 10). |
| 8.15 | **Pass with recorded limits** | The script was walked in all three roles at 1440 and 390; the demo-data scan found no visible sign; the tracer is 404; the fixes above are in place. After the 3.12 merge the adapted starting state was re-checked: check-in open event 3, ready to complete event 1, and the affected beats re-walked. The limits in §5 are recorded for 8.12. |

Demo-polish rows (3.18, 4.13, 2.12, 2.13, 2.14, 2.10) were not separate demo beats. The
shared record dialogs (Event Type, Venue, Member Edit) loaded their fields and actions; the
member feed and profile-to-passes action were exercised in the member beats; the list
loading/error/retry surfaces were not re-triggered beyond the normal reads.

## 7. Verification commands

| Command | Result |
|---|---|
| `dotnet build ASI.Basecode.sln` | Success, 0 warnings, 0 errors |
| `cd ASI.Basecode.WebApp; npm run css:build` | Success; `wwwroot/css/app.css` regenerated after the view/style changes |
| `npm run test:js` | 1 passed, 0 failed |
| `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` | 286 passed, 0 failed (after the 3.12 merge) |
| `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` with `ATTENDTA_TEST_SQL` | 361 passed, 0 failed (the 1.14 guard class gained a missing-TokenKey refusal test; the 3.12 merge added its completion suite) |
| `/CertificateTracer` (GET, authenticated Administrator) | 404 |
| `/CertificateTracer/Trace` (POST, authenticated Administrator with antiforgery token) | 404 |
| `dotnet run --project ASI.Basecode.WebApp -- --demo-readiness` | FAIL runtime SQL identity/audit; PASS private storage; PASS Playwright Chromium PDF; INFO offline not verified; FAIL localhost login (app stopped); exit 1 — expected on an unprovisioned host, recorded not blocking |

## 8. Files changed by this rehearsal

- `database/seed-demo-scenario.sql` — new guarded, idempotent starting state; pending request;
  mobile numbers; GUID event-cover keys; loop variable resets.
- `_bmad-output/initiative-attendta/demo-script.md` — new presenter script.
- `_bmad-output/initiative-attendta/demo-rehearsal-findings.md` — this evidence file.
- `.impeccable/critique/2026-11-07T03-32-53Z__asi-basecode-webapp-views.md` — the critique snapshot.
- `ASI.Basecode.WebApp/Views/Home/Index.cshtml` — role-aware shortcuts.
- `ASI.Basecode.WebApp/Styles/features/home.css` — home shortcut styles.
- `ASI.Basecode.WebApp/Styles/app.css` — imports the home feature styles.
- `ASI.Basecode.WebApp/Views/Account/Login.cshtml` — heading and helper copy.
- `ASI.Basecode.WebApp/Views/Event/Create.cshtml` — draft/cover/publish copy.
- `ASI.Basecode.WebApp/Views/Event/Index.cshtml` — "Manage event" label.
- `ASI.Basecode.WebApp/Views/Reports/Participation.cshtml` — empty-state copy.
- `ASI.Basecode.WebApp/wwwroot/css/app.css` — regenerated.
- `tests/ASI.Basecode.IntegrationTests/DemoDataStartupGuardTests.cs` — added the missing-`TokenKey` refusal test (the 1.14 guard-test exception).

After PR #187 (3.12) merged, the scenario seed and this script were adapted: Leadership Summit is
ready to complete and is completed live in O3, and Community Outreach is a cancelled event. The
seed, `demo-script.md` and this file carry those changes; no demo provider or register row changed.

No schema, service behavior, demo register row, or real backend was changed. The demo store
and its providers were not modified.
