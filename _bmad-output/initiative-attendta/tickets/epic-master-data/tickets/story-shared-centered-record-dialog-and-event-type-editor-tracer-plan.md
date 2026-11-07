---
title: 'Shared centered record dialog and Event Type editor tracer'
type: 'feature'
ticket: '12'
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
context:
  - 'docs/DESIGN.md'
  - 'PRODUCT.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Event Type creation and editing leave the list for full-page forms. Member and Venue follow-up tickets need a reusable centered editor contract with reliable validation and keyboard behavior.

**Approach:** Provide a shared native-dialog shell and reusable JavaScript interaction, with Event Type as the real-service tracer. Fetch server-rendered forms, submit with antiforgery, retain server validation and entered values inside the dialog, and return focus to the originating list control.

## Boundaries & Constraints

**Always:** Preserve required markers, Classification details grouping, list filters/counts/pager, Management authorization, antiforgery and existing service/audit behavior. Keep activation/deactivation a separate action. Follow the existing Razor/Tailwind identity and DESIGN's 680px maximum record-editor width. Preserve standalone form routes as progressive fallback. Publish the partial/JavaScript contract for 2.13/2.14, including multipart readiness for Venue.

**Never:** Rewrite service/repository/database rules, add fixtures or demo writes, convert Member/Venue editors, or include the local implementation prompt in version control. The ticket's newer demo-rescope instructions defer new automated tests, separate code review and per-ticket critique/polish to their integrated passes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Open editor | Officer selects New or Edit | Centered dialog loads blank or current real fields; focus enters it | Loading feedback, recoverable fetch error |
| Invalid save | Duplicate name or missing required field | Same dialog retains input and shows associated errors | Focus first invalid field or summary; allow correction |
| Valid save | Valid create/edit | Exactly one mutation; list reflects saved record; completion announced | Restore focus to equivalent originating control after refresh |
| Dismiss | Escape, Cancel or close before submitting | No mutation; dialog closes; focus returns to trigger | Closing never submits |
| Transport/session failure | Failed request, expired session or unexpected response | No success claim; entered values remain where available | Explain failure and offer retry or normal navigation without automatic duplicate writes |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Controllers/EventTypeController.cs`: existing Create/Edit GET and POST call real services; add partial-response handling while preserving standalone paths and domain-error mapping.
- `ASI.Basecode.WebApp/Views/EventType/{Index,Create,Edit,_Form}.cshtml`: list launch controls and existing shared fields; retain grouping, validation spans and fallback form routes.
- `ASI.Basecode.WebApp/wwwroot/js/site.js`: existing confirmation-dialog handlers; keep record-dialog selectors separate to prevent double handlers.
- `ASI.Basecode.WebApp/Styles/app.css`: existing task-dialog tokens and explicit feature imports.
- `ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml`: 1.13 supplies `ViewData["Feature"]` script loading; use it rather than broad shell changes.
- `ASI.Basecode.Services/ServiceModels/EventTypeViewModel.cs`: existing annotations; no business-contract change required.
- Prerequisite 2.9 shipped required markers/grouping and list conventions; 1.13 shipped feature asset seams. Preserve both.

## Tasks & Acceptance

**Execution:**
- [x] `Views/Shared/_RecordDialog.cshtml` and `wwwroot/js/features/record-dialog.js` under WebApp: implement reusable loading, server-form replacement, submission lock/feedback, error/retry and dismissal/focus lifecycle.
- [x] `Controllers/EventTypeController.cs` and `Views/EventType/_Editor.cshtml`: expose server-rendered editor responses and explicit save outcomes without changing service calls or normal route behavior.
- [x] `Views/EventType/Index.cshtml`, `_Form.cshtml` and standalone Create/Edit if needed: wire launch controls and shared form reuse; preserve independent status controls.
- [x] `Styles/features/record-dialog.css`, `Styles/app.css` and generated `wwwroot/css/app.css`: apply responsive record-editor geometry, internal scrolling and visible states.
- [x] `docs/record-dialog.md` and `PRODUCT.md`: document the reusable trigger/form/response contract and shipped editor capability.
- [x] Perform the matrix scenarios manually; record evidence and blockers in this plan. No new automated tests per ticket rescope.

**Acceptance Criteria:**
- Given an Officer on the list, when creating or editing, then the centered dialog uses real records and successful saves appear in the list.
- Given a duplicate name, when saving and correcting it, then values and field errors remain in the dialog until the correction succeeds.
- Given keyboard navigation, when opening, tabbing, cancelling or saving, then background content is inert, focus stays within the open dialog and returns to the trigger afterward.
- Given a 390px viewport, when opening either editor, then controls and actions remain reachable without horizontal overflow.

## Implementation Notes

- 2026-11-07: Added shared native dialog shell, isolated feature script and responsive stylesheet; Event Type partial GET/POST responses reuse existing services and domain errors. Published `docs/record-dialog.md` and updated PRODUCT.md. Standalone routes and status actions preserved.
- Implementation delegation was unavailable (configured model not found), so implementation proceeded directly.
- Verification passed: `dotnet build` (0 warnings/errors), CSS build, focused EventTypeManagementFlowTests (1/1), Services.Tests (271/271), IntegrationTests (323/323), and `git diff --check`. Full integration run used the existing portable Poppler directory on process PATH.
- BLOCKER: temporary external Playwright smoke harness at `C:/Users/vv/AppData/Local/Temp/opencode/record-smoke` could not complete sign-in because LocalDB stalled. Subsequent fixture initialization timed out, and independent `sqlcmd -S "(localdb)\MSSQLLocalDB" -d master -E -l 5 -Q "SELECT 1 AS reachable"` also timed out. No restart attempted; awaiting user permission. Rendered smoke and matrix acceptance remain unverified, so this plan remains in-progress.

## Plan Change Log

- 2026-11-07: User authorized continuing with the LocalDB restart. Normal stop/start restored connectivity. Temporary external browser smoke then passed all 11 checks: focus enters, Tab containment, create/save focus return, duplicate error/input retention and correction, real edit hydration/save, row focus restoration, 390px no-overflow fit, Escape, Cancel, failed GET recovery/retry and uncertain POST input retention. The run used Development demo mode with real Event Type services and a disposable fixture database. Phone screenshot inspected at `C:/Users/vv/AppData/Local/Temp/opencode/record-phone.png`.
- Browser smoke exposed native Tab cycling out of the dialog; added explicit Tab/Shift+Tab wrap and reran the complete focused smoke successfully. Final build remains 0 warnings/errors, CSS rebuild passed, existing JS test passed (1/1). Backend tests were already green and backend code did not change afterward.

## Review Triage Log

- Separate review and per-ticket critique/polish deferred under the root AGENTS.md demo-rescope exception and this story's current Done check; integrated passes and PR peer approval remain the planned gates. Final diff inspected for scope, antiforgery, authorization, existing service reuse and whitespace. No commit or push per user's explicit execution guidance.

## Design Notes

Use native `showModal()` for browser-managed inertness and focus containment, with explicit trigger restoration. Use server-rendered validation rather than duplicating business rules in JavaScript. Keep the reusable transport capable of `FormData` so Venue can adopt it without another interaction framework.

## Verification

- `dotnet build` after the first slice and at completion if code changed: successful build.
- `npm --prefix ASI.Basecode.WebApp run css:build`: successful generated stylesheet.
- Run existing focused Event Type integration coverage early if controller HTTP behavior changes; check LocalDB connectivity first, and run SQL-backed commands sequentially.
- Run required existing service/integration suites once after implementation, with existing Poppler installation on the process PATH if required; report environmental blockers precisely.
- `git diff --check`: no whitespace errors.
- With `DemoData__Enabled=true`, inspect the rendered Event Type screen using its real backend; smoke successful save, duplicate correction, keyboard cancel/save/focus return and phone fit. Demo mode must not supply fake Event Types.
