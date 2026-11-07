---
title: 'Master Data forms: centered record dialogs and required markers'
type: 'feature'
ticket: '11'
created: '2026-11-07'
status: 'built'
baseline_revision: 'f5392a4f1b8e242dae86aff498832b2376a16e3e'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'docs/DESIGN.md'
  - 'PRODUCT.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Member, Event Type and Venue editors already open in the shared centered dialog, but the combined #128 acceptance walkthrough finds gaps against `docs/DESIGN.md`: clicking the dialog's own padding dismisses it and loses input, Member focus lands on the read-only email, the Venue editor loses its current photo after a failed save and never previews a newly selected file, and the Member editor lacks DESIGN's identity summary and future-registration notice.

**Approach:** Fix only those findings in the shared dialog shell, the three editors and the Venue controller's presentation response. Keep the real services, validation, authorization and list behavior unchanged. Verify all three editors rendered, then run the Impeccable critique and polish pass.

## Boundaries & Constraints

**Always:** Keep the native `<dialog>` shell, 680px record width, 48px viewport clearance, internal scrolling, required markers, fieldsets, server validation, antiforgery, Management authorization, and the standalone form routes. Keep status/activation actions in the list row, separate from Cancel/Save, per `docs/record-dialog.md`. Reuse the existing services and CSS build.

**Never:** Change backend business rules, services, repositories, schema, audit behavior, or list filters/paging. Do not add member creation, membership-status mutation, or demo fixtures. Do not rewrite the dialog into a custom modal or add a JavaScript framework.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Interior click | Click dialog padding or header | Dialog stays open, input retained | No close |
| Backdrop click | Click outside the panel | Dialog closes, focus returns to trigger | No save |
| Open Member editor | Officer selects Edit | Focus enters first editable field (name), not read-only email | — |
| Failed Venue save | Invalid capacity or duplicate room | Current photo still shown, entered values retained, field error | Reselect guidance |
| New Venue photo | Choose JPEG/PNG | Selected image previews before save | Bad type/size refused by server |
| Escape during save | Save in flight | Dialog stays open | Close blocked until response |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/wwwroot/js/site.js` -- document click handler closes any dialog when `event.target` is the dialog; a click on the dialog's own padding hits this and discards input. Fix by closing only when the click is outside `getBoundingClientRect()`.
- `ASI.Basecode.WebApp/wwwroot/js/features/record-dialog.js` -- shared open/load/save/focus lifecycle; `focusForm()` picks `input:not([type=hidden])`, so Member focuses the readonly email; no image preview. Add `:not([readonly])` and a generic file preview.
- `ASI.Basecode.WebApp/Views/Shared/_RecordDialog.cshtml` -- shell; close button uses `button button-secondary` (44px) instead of DESIGN's 32px close; add `record-dialog-close` and an optional subtitle element.
- `ASI.Basecode.WebApp/Styles/features/record-dialog.css` -- add close-button geometry, header title sizing and ≤600px clearance; keep 680px/48px.
- `ASI.Basecode.WebApp/Controllers/VenueController.cs` -- `Edit` POST returns `Editor(model)` with `ImagePath` null after a validation or domain failure; rehydrate from `GetVenue`.
- `ASI.Basecode.WebApp/Views/Venue/_Form.cshtml` -- add the preview image element and reselect guidance.
- `ASI.Basecode.WebApp/Views/Member/_Editor.cshtml` -- add DESIGN's future-registration notice; the identity summary comes from the shell subtitle.
- `ASI.Basecode.WebApp/Views/Member/Index.cshtml` -- pass the persisted identity as the Edit trigger subtitle.
- `PRODUCT.md`, `docs/record-dialog.md` -- record the three-editor capability and the preview/backdrop rules.

## Tasks & Acceptance

**Execution:**
- [x] `site.js` -- backdrop-only close with a rect check -- interior clicks must not discard input.
- [x] `record-dialog.js` -- skip readonly fields on focus; preview the selected image and revoke URLs -- focus and photo preview.
- [x] `_RecordDialog.cshtml` + `record-dialog.css` -- 32px close button, subtitle slot, phone clearance -- DESIGN geometry.
- [x] `VenueController.cs` -- rehydrate `ImagePath` on invalid or failed Edit -- keep the current photo.
- [x] `Venue/_Form.cshtml` -- preview element and reselect guidance -- selected-photo feedback.
- [x] `Member/_Editor.cshtml` + `Member/Index.cshtml` -- future-registration notice and identity subtitle -- DESIGN §5.
- [x] `PRODUCT.md`, `docs/record-dialog.md` -- update capability and contract.
- [x] Run Impeccable critique then polish on the three editors; fix only findings.

**Acceptance Criteria:**
- Given an Officer on any of the three lists, when they open the editor, then it is centered, fits the viewport, and focus enters the first editable control.
- Given an open editor, when Tab/Shift+Tab, Escape or Cancel is used, then focus stays inside and returns to the trigger, and no mutation occurs.
- Given a failed save, when the response returns, then entered values, field errors and (Venue) the current photo remain visible.
- Given a Venue editor, when a JPEG/PNG is selected, then it previews before save.
- Given the Member editor, when opened, then the identity summary and future-registration notice are visible and email is read-only.
- Given the list rows, when inspecting actions, then status/activation stays separate from the dialog's Cancel/Save.

## Implementation Notes

- 2026-11-07: Implemented the six findings only. Shared shell: 32px close control, optional subtitle, 18px title, ≤600px clearance. JS: readonly/disabled inputs skipped on initial focus, subtitle hydration, selected-image preview with URL revocation, and backdrop detection by panel rectangle so interior padding clicks keep the dialog. Venue: `ImagePath` rehydrated on invalid/domain-failed Edit, reselect guidance when a file was posted. Member: persisted identity subtitle and future-registration notice. Docs and PRODUCT.md updated.
- Verification: `dotnet build` 0 warnings/0 errors; CSS build reproducible (SHA-256 `1DCF40B5…`); `npm run test:js` 1/1; Services 286/286; Integration 356/360, the 4 failures all `CertificateTracerFlowTests` requiring Poppler `pdftotext` (missing in this environment, unrelated to this change); `git diff --check` clean. `dotnet test` reports "Zero tests ran" for the xunit v3 MTP projects here, so the suites ran through `dotnet run --project`.
- Rendered harness (external, not committed): 40/40 checks against the disposable `AttendTaMVP_Test` database at 1440×900 and 390×844 — centering, 680px width, first-editable focus, Tab containment, interior vs backdrop click, Escape, focus restore, footer-only Cancel/Save, Member subtitle/notice/read-only email, Venue current photo, selected preview, failed-save retention and reselect guidance, phone fit. Script and screenshots: `C:\Users\johnp\AppData\Local\Temp\opencode\record-smoke-128\`.
- Impeccable: detector clean on the Shared, Member, EventType and Venue view folders. Design review fixed two polish findings (preview enlarged to 160×106, description styled as a muted hint). The review was single-context because this model cannot view images; DOM/geometry evidence replaced visual inspection.

## Review Triage Log

- 2026-11-07 quick lens, 6 findings, all verified as real and routed to patch:
  - `site.js` backdrop uses click release coordinates — medium — a selection that starts inside and releases on the backdrop would close and discard input; patched to decide from the pointerdown origin.
  - `.record-dialog-close` had no disabled/pressed state — low — `setBusy` disables it while looking enabled; patched with `:disabled`/`:active` styling.
  - Venue reselect hint missing from `aria-describedby` — low — the hint had no id and was not announced; patched with a conditional id and describedby entry.
  - Current-photo alt used the posted RoomName — low — an empty failed save rendered `alt="Current photo of "`; patched to a stable alt, also in standalone `Venue/Edit.cshtml`.
  - `RestoreCurrentPhoto` rehydrated legacy paths — low — the Photo action serves only `uploads/venues/`; patched to rehydrate only servable paths.
  - Future-registration notice missing on standalone Member Edit — low — the notice lived only in `_Editor`; moved to `_Form` so both entry points render it.
- All six patches applied and re-verified: build 0/0, CSS reproducible, JS 1/1, Services 286/286, Integration 356/360 (4 Poppler-only), rendered smoke 40/40.

## Design Notes

The subtitle carries the persisted identity so the summary does not duplicate editable fields or show an unsaved invalid name. The preview uses `URL.createObjectURL` and is revoked on change and close. Backdrop detection uses the panel rect because a native dialog's own padding is a valid interior click target.

## Verification

**Commands:**
- `dotnet build` -- expected: 0 warnings and 0 errors.
- `npm --prefix ASI.Basecode.WebApp run css:build` -- expected: succeeds; a second run is byte-identical.
- `npm --prefix ASI.Basecode.WebApp run test:js` -- expected: existing tests pass.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` -- expected: all pass.
- `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` with `ATTENDTA_TEST_SQL` -- expected: all pass.
- `git diff --check` -- expected: clean.

**Manual checks:**
- With the local database and an Officer or Admin sign-in, open each editor: centered, focus on the first editable field, Tab contained, Escape and Cancel restore focus, an interior padding click keeps the dialog, a backdrop click closes it.
- Venue: current photo on edit, failed save keeps it, selected photo previews. Member: summary and notice visible. At 390px width the dialog and footer have no horizontal overflow.
