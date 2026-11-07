---
title: 'Frontend door scanner manual lookup and attendance dialogs'
type: 'feature'
ticket: '11'
created: '2026-11-07'
status: 'built'
baseline_revision: '0f71b770004f81ea6cf796aacb02defef83b6422'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'none'
review_source: 'pinned'
lenses_ran: []
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
  - 'docs/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Check-in route is an empty stub, so officers cannot select an event, scan a 4.10 pass, look up a confirmed participant, or invalidate a mistaken check-in.

**Approach:** Fill the scanner on the 1.13 shell with the 1.14 selector and check-in control, a locally served camera decoder, separate camera/manual adapters, a server-rendered lookup, and a reason-and-confirmation invalidation dialog. Demo providers behind one contract per register row (M-05/06/07) compose existing reads and the 1.14 store; no SQL, file or Activity Log write.

**Decision (2026-11-07):** Target is BMAD 4.11 / issue #138; 3.17 is merged (PR #163) and is not reopened.

## Boundaries & Constraints

**Always:** Management policy; POST + antiforgery for every mutation; local assets only; match 4.10's rule-10 token; reload the real registration status on each demo check-in (register rule 8); searching/viewing never records; invalidation needs a reason and is refused after issuance; no visible demo wording; keyboard and 390px usable.

**Never:** Add a real service in Services/Data; write SQL/files/Activity Log; change `MyEventsController` or the 1.13 host; add demo rows beyond M-05/06/07; add automated tests or run per-ticket critique/polish; show a demo banner/badge/watermark.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid scan | published event, open window, 4.10 pass | success with name, student number, time; one log row | No error |
| Repeat scan | same pass again | original time; no second row | No error |
| Unknown/wrong-event token | random or another event's token | invalid, no personal data | No error |
| Closed/completed window | closed or completed | closed; manual modes stay visible | No error |
| Cancelled registration | real status not Confirmed | invalid | No error |
| Camera denied/unavailable | refused or no device | camera-unavailable text; manual modes usable | No error |
| Manual lookup | name or student number | paged confirmed rows; explicit Check in | No error |
| Invalidate | valid row + reason | row marked; re-check-in refused | Refused when reason blank or certificate issued |
| Demo switch off | `DemoData:Enabled=false` | empty screen; commands refuse | No error |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Controllers/CheckInController.cs` — stub; add `[AutoValidateAntiforgeryToken]`, GET Index + POST Scan/CheckIn/Invalidate.
- `ASI.Basecode.Services/ServiceModels/CheckInScreenViewModel.cs` — screen, outcome, lookup and log models; log rows carry `RegistrationId`, `IsInvalidated`, `CanInvalidate`.
- `ASI.Basecode.Services/Interfaces/ICheckInService.cs` (M-05: `GetScreenAsync` + `ScanAsync`), `ICheckInLookupService.cs` (M-06), `IAttendanceInvalidationService.cs` (M-07) — three contracts, one per register row.
- `ASI.Basecode.WebApp/DemoData/DemoCheckInService.cs` (M-05), `DemoCheckInLookupService.cs` (M-06), `DemoAttendanceInvalidationService.cs` (M-07).
- `ASI.Basecode.WebApp/DemoData/UnavailableCheckInService.cs`, `UnavailableCheckInLookupService.cs`, `UnavailableAttendanceInvalidationService.cs` — switch-off.
- `ASI.Basecode.WebApp/DemoData/DemoCheckInRegistration.cs` + `DemoDataRegistration.cs` — per-surface registration file.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs` + `DemoSeedService.cs` — `GetAttendanceForEvent`, `MemberName`/`StudentNumber` on `DemoAttendance`, seed call.
- `ASI.Basecode.WebApp/Views/CheckIn/Index.cshtml` — gate on `Model.EventId`; empty state when no published events; selector, control, mode tabs, camera/pass-code/lookup, result, log, invalidation dialog.
- `ASI.Basecode.WebApp/Views/Shared/_CheckInControl.cshtml` + `EventController.cs` — `returnUrl` hidden field; `Url.IsLocalUrl` check with `EventIndexRoute` fallback; shared with 3.16/PR #164.
- `ASI.Basecode.WebApp/Styles/features/check-in.css` — scanner layout, result variants, 390px.
- `ASI.Basecode.WebApp/wwwroot/lib/jsqr/jsQR.js` + `LICENSE` — jsQR 1.4.0 (Apache-2.0).
- `ASI.Basecode.WebApp/wwwroot/js/features/scanner.js`, `scanner-camera.js`, `scanner-manual.js` — shared, camera and manual adapters; drop `ViewData["Feature"]` and load jsQR → `scanner.js` → adapters in `Scripts`.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — M-05/06/07 detail blocks.

## Tasks & Acceptance

**Execution:**
- [ ] Contracts + models: `ICheckInService` (screen + scan), `ICheckInLookupService`, `IAttendanceInvalidationService`; log rows carry `RegistrationId`, `IsInvalidated`, `CanInvalidate`; `Empty()` returns no options.
- [ ] Store + seed: `GetAttendanceForEvent`, `MemberName`/`StudentNumber` on `DemoAttendance`, seed call.
- [ ] Demo providers:
  - scan: match the rule-10 token, reload `IsConfirmedAsync`, return `repeat` only for a valid existing attendance, return `invalid` for an invalidated attendance, `closed` for a closed/completed window, `invalid` for unknown/wrong-event/cancelled.
  - lookup: bounded read of all confirmed rows (500), filter by name/student number in memory, then page 25; method is `StudentNumber` when the query equals the student number, else `ManualSearch`.
  - invalidation: reason required, issued certificate refused, event match checked.
  - screen: published options, default to the first open event, selected state, recent log.
- [ ] Registration file; `DemoDataRegistration` calls it.
- [ ] Controller: `[AutoValidateAntiforgeryToken]`; Index composes screen + lookup; Scan returns JSON for AJAX and redirects for forms; CheckIn/Invalidate redirect with TempData.
- [ ] View: empty state when `Model.EventId` is null; GET lookup form, POST Check in, shared `data-dialog-*` invalidation dialog.
- [ ] JS: vendor jsQR; drop `ViewData["Feature"]`; load jsQR → `scanner.js` → adapters in `Scripts`.
- [ ] CSS + regenerated `app.css`; `returnUrl` with `Url.IsLocalUrl` + `EventIndexRoute` fallback; register doc.
- [ ] Verify: build, both test projects, `test:js`, CSS idempotence, focused demo smoke.

**Acceptance Criteria:**
- Given an officer on `/CheckIn` with a published event and the window open, when a valid 4.10 pass is scanned, then name/student number/time show and one log row appears.
- Given the same pass again, then the original time shows and no duplicate row is added.
- Given an unknown or wrong-event token, then the invalid outcome shows and no attendance is recorded.
- Given a closed window, a completed event or a denied camera, then the matching outcome shows and manual modes remain usable.
- Given a cancelled registration, then the invalid outcome shows.
- Given a name/student-number lookup, then paged confirmed rows show and Check in records the manual method; searching or viewing alone records nothing.
- Given a reason, invalidation marks the row and re-check-in is refused; given an issued certificate, invalidation is refused.
- Given `DemoData:Enabled=false`, then the empty state shows and commands refuse.
- Given desktop, 390px and keyboard-only, then no horizontal overflow and every control is reachable.
- Given build, `css:build`, both test projects and `test:js`, then all pass.

## Design Notes

- Three contracts, one per register row, so 4.3/4.5/4.6 can delete one provider and its registration line each.
- Lookup is server-rendered for keyboard/no-JS access; camera and pass-code adapters post form-encoded AJAX and receive JSON.
- jsQR 1.4.0 is Apache-2.0, single-file UMD, vendored with its license.
- The result persists until the next scan; the camera stops after an outcome and Scan next resumes it.
- Outcome vocabulary is `success`, `repeat`, `invalid`, `closed`; `camera-unavailable` is client-side only.

## Implementation Notes

- Contracts are `ICheckInService` (`GetScreenAsync`, `ScanAsync`), `ICheckInLookupService`
  (`SearchAsync`, `CheckInAsync`) and `IAttendanceInvalidationService`; the screen, outcome,
  lookup and log models live in `CheckInScreenViewModel.cs`. The log row carries
  `RegistrationId`, `IsInvalidated` and `CanInvalidate`.
- `DemoStore` now stores `MemberName`/`StudentNumber` on each attendance and exposes
  `GetAttendanceForEvent`. The seed passes the confirmed row's identity into `RecordAttendance`.
- `DemoCheckInService` composes the published-event read and the store: it selects the first open
  event, matches the rule-10 token of the event's confirmed rows, reloads `IsConfirmedAsync`, and
  returns `success`, `repeat`, `invalid` or `closed`. `DemoCheckInLookupService` reads the event's
  confirmed rows (bounded at 500), filters in memory, pages at 25, and records
  `student_number`/`manual_search` only on the explicit Check in. `DemoAttendanceInvalidationService`
  requires a reason, checks the event match and refuses an issued certificate.
- `CheckInController` has `[AutoValidateAntiforgeryToken]`; `Index` composes the screen and the
  lookup, `Scan` returns JSON for AJAX and redirects for forms, and `CheckIn`/`Invalidate` redirect
  with TempData. `_CheckInControl` posts an optional local `returnUrl`; `EventController` follows it
  only through `Url.IsLocalUrl` and otherwise falls back to `EventIndexRoute`.
- The view renders the empty state when there is no published event, the shared selector and
  check-in control, the three mode tabs, the server-rendered lookup, the log and the shared
  `data-dialog-*` invalidation dialog. The panels render visible so the pass-code and lookup forms
  work without JavaScript; `scanner.js` hides the inactive panels and selects the manual tab when a
  query is present. A successful scan appends its log row without a reload.
- `scanner.js` owns the tabs, the pass-code AJAX post, the outcome renderer and the log row;
  `scanner-camera.js` owns `getUserMedia` and the local jsQR decode loop; `scanner-manual.js` keeps
  the lookup explicit and prevents double submission. jsQR 1.4.0 (Apache-2.0) is vendored at
  `wwwroot/lib/jsqr/`. The CheckIn view loads its scripts in `Scripts`; the layout's `ViewData["Feature"]` hook is
  left for other surfaces.
- The demo register's M-05/M-06/M-07 detail block records the contracts, classes, composition and
  removal tickets.

**Verification (2026-11-07):**
- `dotnet build ASI.Basecode.sln` — 0 warnings, 0 errors.
- Services tests — 271/271 via `dotnet test`.
- Integration tests — 323 total, 4 failed only in `CertificateTracerFlowTests` because Poppler's
  `pdftotext` is not on PATH, as the plan anticipates.
- `css:build` — idempotent (same SHA-256 before and after a second run); `test:js` — 1/1.
- Server smoke against `AttendTaMVP_Test` with `DemoData__Enabled=true`: valid scan, repeat with the
  original time, unknown token, wrong-event token, manual lookup by name and student number, manual
  check-in, blank-reason refusal, invalidation and re-check-in refusal, issued-certificate refusal,
  completed-event and closed-window outcomes, close/open through the shared control with the
  scanner return URL, and a non-local `returnUrl` fallback. With `DemoData__Enabled=false` the
  screen shows its empty state and the scan returns the standard refusal JSON.
- Playwright smoke at 390px and 1440px: no horizontal overflow, tab clicks and arrow keys switch
  panels, the pass-code scan shows name/student number/time, the log row appears without a reload,
  a repeat does not add a second row, the invalidation dialog opens with focus in the reason field
  and Escape closes it, the camera-unavailable outcome appears while manual mode stays usable, and
  the manual lookup returns the confirmed participant.
- Smoke-only rows were added to the disposable `AttendTaMVP_Test` database (a second confirmed
  attendee and registration 6); no committed file contains a password or hash.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — all pass.
- `$env:ATTENDTA_TEST_SQL='Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True'; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — all pass (PDF cases need Poppler).
- `npm --prefix ASI.Basecode.WebApp run css:build` then `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` — no drift after commit.
- `npm --prefix ASI.Basecode.WebApp run test:js` — 1 test passes.

**Manual checks (if no CLI):**
- Start with `DemoData__Enabled=true`, `DemoData__AllowDatabase=AttendTaMVP_Test` and `DemoData__TokenKey=<local secret>`; run `dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"` and sign in as an officer.
- Open `/CheckIn`, select the open event, scan a 4.10 pass from `/Passes`; expect name/student number and a log row; rescan and expect the original time.
- Try an unknown token, a wrong-event token, a closed window, a denied camera and a cancelled registration; expect the matching outcomes and no new attendance.
- Look up by name and student number; confirm searching alone records nothing; check in; invalidate with a reason; confirm re-check-in and issued-certificate refusals.
- Check 390px/1440px keyboard use and no visible demo wording.

## Auto Run Result

### #138 follow-up — 2026-11-07

**Starting revision:** `e99a7b8b7bfc5caec1ac013147b9c5d293470c9d`.
The initial implementation is already merged by PR #170, merge revision
`4f5720948b01ccd72364c20e8c0c187c92723b35`. This pass reused it and fixed three observed gaps.
Claim `demo-138-gpt-agent-20261107` was posted to #138, assigned to the authenticated GitHub user,
and re-read; there was no earlier competing claim.

**Implemented / files changed:**
- `Controllers/CheckInController.cs`: carry an event-scoped serialized outcome through
  POST/redirect/GET for form-post scans and manual check-ins, including domain refusals.
- `Views/CheckIn/Index.cshtml`: render that outcome with name, student number, original Manila
  time and message, using the existing outcome styles. It remains readable on the page without
  a timer and works without JavaScript.
- `wwwroot/js/features/scanner-camera.js`: gate repeated starts while permission is pending;
  invalidate late continuations on mode change/page exit and stop every superseded stream track.
- `Styles/features/check-in.css` and compiled `wwwroot/css/app.css`: fix unreadable inherited
  white participant text on the lookup table's white rows.
- `PRODUCT.md` and the M-05/M-06/M-07 register detail: document the scanner interaction and
  unchanged provider/integration boundaries.

**Review:** none; no review lenses or subagents launched. Per-ticket critique/polish remain
deferred by the demo rescope. This follow-up uses the owner's current minimal verification
instructions instead of the original plan's broad service/integration test commands above;
no automated test suite was added.

**Verification actually performed:**
- `dotnet build -m:1 -nr:false -p:UseSharedCompilation=false`: succeeded, 0 warnings / 0 errors.
- `npm --prefix ASI.Basecode.WebApp ci`: succeeded, 0 vulnerabilities.
- `npm --prefix ASI.Basecode.WebApp run css:build`: succeeded.
- `npm --prefix ASI.Basecode.WebApp run test:js`: existing test 1/1 passed.
- Impeccable mechanical detector on the changed view/styles: `[]` findings.
- `git diff --check`: passed.
- Focused Playwright smoke used the installed `msedge` channel with `DemoData__Enabled=true`,
  an actual Officer cookie and a newly created isolated LocalDB database
  `AttendTa_IT_cdb13800e85b420d9fc372e88e900138` from the canonical schema. It was dropped in
  cleanup; no shared database was modified. Password and token secrets were generated in process
  and never committed.
- Passed browser interactions: delayed camera permission resolved after switching modes stops
  its track and never attaches the stream; denied camera shows the unavailable outcome; keyboard
  arrow navigation switches tabs; valid pass-code check-in shows identity; repeat retains the
  original time with one log row; unknown code refuses; manual search alone records nothing;
  explicit manual Check in shows a persistent identity/student-number/time panel after 5.5 seconds.
- Passed existing guards through the HTTP surface: blank invalidation reason refused; issued
  certificate refused; invalidated attendance refuses re-check-in; cancellation through the real
  member self-cancellation endpoint makes the original pass invalid; shared close control makes
  subsequent scans return closed. No-JavaScript form-post scan renders a persistent closed panel.
- Rendered desktop (1440px) and phone (390px) screenshots were inspected: participant identity is
  readable, result panel remains visible, no document-level horizontal overflow. Invalidation
  dialog focuses the reason field and Escape closes it. No browser page errors were observed.
- Local smoke scripts/screenshots/logs are ignored under `artifacts/scanner-138/`, not part of
  the deliverable. The initial smoke found the white-on-white table and a smoke-only URL typo;
  both were corrected before the successful run.

**Remaining human checks / limits:** a real laptop camera reading a displayed or printed 4.10 QR
pass was not exercised (the permission lifecycle was controlled in-browser). The complete Verify
line, full keyboard-only journey, rehearsal critique/polish and integrated backend verification
remain scheduled acceptance work. Peer approval and current PR CI are merge gates; this run does
not close #138 or merge the follow-up PR. Existing in-memory demo persistence and the documented
500-row candidate limit are unchanged.
