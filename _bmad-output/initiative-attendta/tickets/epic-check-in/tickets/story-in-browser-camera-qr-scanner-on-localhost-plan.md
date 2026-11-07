---
title: 'In-browser camera QR scanner on localhost'
type: 'feature'
ticket: '4'
created: '2026-11-07'
status: 'built'
baseline_revision: 'cf955f70ba2872599f6acea3d1aa0a19f50bcf5d'
route: 'oneshot'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 4.11's camera adapter and local jsQR decoder already post to the real 4.3 scan engine, but the shared scan POST sends the antiforgery token in the body instead of the `RequestVerificationToken` header AD-8 requires, the repeat outcome serializes a SQL `datetime2` without a UTC marker so the browser shows a different time than the first scan, and no one has verified a real laptop camera against the real engine.

**Approach:** Send the layout meta token as the `RequestVerificationToken` header, mark the repeat time as UTC before the JSON response, show only "Scan next" after a camera scan, and verify one real scan, one repeat and the camera-denied fallback on localhost.

## Boundaries & Constraints

**Always:** Management policy; POST + antiforgery header from the layout meta tag; relative form action (no hard-coded localhost); local jsQR from wwwroot; no scan payload in logs; reuse 4.11's camera adapter and decoder; keep the AD-9 JSON outcome shape; UTC timestamps in JSON.

**Never:** Rebuild the scanner screen; choose another decoder; add a runtime CDN; wire manual lookup (4.5) or invalidation (4.6); change the schema or the validation rules; add per-ticket critique/polish; change CI.

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/wwwroot/js/features/scanner.js` — `postScan` sends the token in the body today; move it to the `RequestVerificationToken` header and keep `eventId`/`token` in the body.
- `ASI.Basecode.Services/Services/AttendanceRecordingService.cs` — both repeat returns pass `current.RecordedAt` (SQL `datetime2`, `Kind=Unspecified`) into the result; wrap with `DateTime.SpecifyKind(..., DateTimeKind.Utc)` so `Json()` emits `Z`.
- `ASI.Basecode.WebApp/wwwroot/js/features/scanner-camera.js` — after a scan, `stop()` unhides "Start camera" and the callback unhides "Scan next"; hide "Start camera" when "Scan next" is shown.
- `ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml` — already renders the meta token; do not change.
- `ASI.Basecode.WebApp/Startup.Auth.cs` — already sets `AntiforgeryOptions.HeaderName = "RequestVerificationToken"`; do not change.
- `ASI.Basecode.WebApp/Views/CheckIn/Index.cshtml` — already loads local jsQR, `scanner.js` and the adapters; do not change.
- `ASI.Basecode.WebApp/Controllers/CheckInController.cs` + `ASI.Basecode.Services/Services/CheckInService.cs` — real `Scan` endpoint and engine from 4.3; do not change.
- `ASI.Basecode.WebApp/tests/site.test.js` — existing `test:js` entry; add one scan-POST test with its own fakes (the shared `createElement` lacks `querySelectorAll`/`getAttribute`).
- `tests/ASI.Basecode.IntegrationTests/CheckInFlowTests.cs` — extend the existing repeat test with a `DateTimeKind.Utc` assertion.

## Tasks & Acceptance

**Execution:**
- [ ] `ASI.Basecode.WebApp/wwwroot/js/features/scanner.js` — send `RequestVerificationToken` as a header, not a body field — AD-8.
- [ ] `ASI.Basecode.Services/Services/AttendanceRecordingService.cs` — return the repeat time with `DateTimeKind.Utc` — keeps first and repeat JSON times equal.
- [ ] `ASI.Basecode.WebApp/wwwroot/js/features/scanner-camera.js` — hide "Start camera" while "Scan next" is shown — one clear resume control.
- [ ] `ASI.Basecode.WebApp/tests/site.test.js` — VM test loading `scanner.js`: asserts the header, `fetch` called with the form's own action, and no token in the body.
- [ ] `tests/ASI.Basecode.IntegrationTests/CheckInFlowTests.cs` — assert the repeat `RecordedAt` has `DateTimeKind.Utc`.

**Acceptance Criteria:**
- Given the CheckIn page, when a pass is scanned or a pass code is submitted with JavaScript, then the POST carries the `RequestVerificationToken` header from the layout meta and the body carries only `eventId` and `token`.
- Given a successful camera scan on localhost, then the member name and student number show and one log row appears.
- Given the same pass again, then the original check-in time shows unchanged and no second attendance row is added.
- Given a camera scan completes, then only "Scan next" is offered, not "Start camera" as well.
- Given camera permission is denied, then the camera-unavailable outcome shows and the pass-code and manual modes stay usable.
- Given the browser network log, then no request goes to a host other than the app origin; given the server log, then no scan token appears.

## Implementation Notes

- Reused 4.11's camera adapter, local jsQR decoder and the 4.3 `Scan` endpoint; the screen, controller and engine contract are unchanged.
- `scanner.js` now sends the layout meta token as the `RequestVerificationToken` header; the AJAX body carries only `eventId` and `token`. The no-JS form path is untouched.
- `AttendanceRecordingService` marks both repeat returns as `DateTimeKind.Utc`. SQL `datetime2` materializes as `Unspecified`, so without this the AJAX JSON omitted `Z` and the browser read the repeat time as local time, showing a different clock time than the first scan.
- `scanner-camera.js` now routes the action row through `showStart()`/`showNext()`: a completed scan offers only "Scan next", switching modes restores "Start camera", and the permission prompt keeps a visible disabled "Start camera" control.
- Added a VM-based scan-POST test to the existing `tests/site.test.js` (no `package.json` or CI change) and a `DateTimeKind.Utc` assertion to the existing repeat test.
- The repeat test now uses a fresh `DbContext`, as a real second request does. With the shared context it passed even without the fix; with a fresh context it failed `Expected: Utc / Actual: Unspecified`, proving the fix and the guard.
- The shared repository worktree was switched to `feature/48` by a concurrent 4.5 session mid-run. 4.4 was moved to an isolated worktree on `feature/47/In-browser-camera-QR-scanner-on-localhost`; the 4.5 working changes were left untouched.
- Verification: `dotnet build` 0 warnings / 0 errors; `npm.cmd run test:js` 2/2; `CheckInFlowTests` 10/10 on LocalDB.
- The real laptop-camera scan, repeat and camera-denied fallback remain the human check; this environment has no webcam.

## Plan Change Log

## Review Triage Log

- **HITL acceptance unverified** — low, not fixable here: the story is `hitl: true` and the plan keeps the real laptop-camera scan, repeat, camera-denied fallback, same-origin network check and no-token-in-log check as the human acceptance step.
- **"Start camera" reappears next to "Scan next" after a mode switch** — medium, patched: `stop()` now calls `showStart()`, and a completed scan calls `showNext()`, so exactly one resume control is visible.
- **Empty action row while "Scan next" waits for permission** — medium, patched: `start()` keeps a visible disabled "Start camera" control until the stream resolves or fails.
- **Scan-POST test action assertion could not fail** — medium, patched: the fake form action is now distinct (`/CheckIn/Scan?eventId=12`) and the test also asserts no absolute origin.
- **Kind assertion did not exercise the SQL materialization** — high, patched: the repeat now runs on a fresh context; it failed `Expected: Utc / Actual: Unspecified` before the fix and passes after.

## Verification

**Commands:**
- `dotnet build` — expected: full solution builds.
- `npm.cmd --prefix ASI.Basecode.WebApp run test:js` — expected: existing test plus the new scan test pass. (Local only; CI does not run `test:js`.)
- `$env:ATTENDTA_TEST_SQL="Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter "FullyQualifiedName~CheckInFlowTests"` — expected: all pass.

**Manual checks (if no CLI):**
- Seed or reuse a disposable local database with an active Officer, a published event with check-in open, and one confirmed registration with a QR pass (download it from `/Passes` as the member).
- Run the app on localhost, sign in as the Officer, open `/CheckIn`, start the camera and scan the pass; expect name, student number and one recorded row.
- Scan the same pass again; expect the same original time and no second row.
- Deny camera permission; expect the camera-unavailable message and usable pass-code/manual modes.
- In DevTools Network, confirm every request is same-origin; in the server log, confirm no QR token appears.
