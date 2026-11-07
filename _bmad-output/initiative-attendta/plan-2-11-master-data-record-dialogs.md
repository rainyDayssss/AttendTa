---
title: 'Master Data forms: centered record dialogs and required markers'
type: 'feature'
ticket: 'story-master-data-forms-centered-record-dialogs-and-required-marke'
created: '2026-11-07'
status: 'built'
baseline_revision: '8872eeab46734da68e26b692cf10dc12319038bd'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
followup_review_recommended: false
context: ['docs/DESIGN.md']
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** GitHub #128 is the finale acceptance umbrella for the three Master Data record editors shipped in 2.12–2.14 (Event Type, Member, Venue). A read of the current `dev` code against `docs/DESIGN.md` finds real gaps: the Venue editor never previews a newly selected photo and loses the current-photo block after a failed save (the posted viewmodel carries no `ImagePath`), the shared record dialog keeps 24px clearance and 24px padding at phone widths where DESIGN §4 requires 12px/20px, the Member editor opens focus on its read-only email field, the dialog close control is 44px rather than the 32px DESIGN §5 reserves, and the Member editor has no future-registration notice (DESIGN §6 screen 8).

**Approach:** Fix only those findings inside the existing real-backend surfaces: add a delegated selected-photo preview to the Venue upload control, restore the persisted photo on a failed Venue save, add the phone-width dialog rules and a 32px close control, make dialog focus land on the first editable control, and add the one-line registration-snapshot notice backed by epic requirement R2. No backend contract, schema, or copy beyond that notice changes.

</intent-contract>

## Implementation Notes

Oneshot route: the change is roughly 70 lines across seven presentation files plus a rebuilt stylesheet, all mechanical and localized to the three editors. No new service, repository, route, or test surface is introduced.

- Files touched: `Views/Venue/_Form.cshtml` (preview markup and caption), `Views/Venue/{Index,Create,Edit}.cshtml` (script include), `wwwroot/js/features/venue-photo.js` (new delegated preview), `wwwroot/js/features/record-dialog.js` (focus target), `Styles/features/record-dialog.css` (phone rules, 32px close), `Styles/app.css` (preview styles), `Controllers/VenueController.cs` (restore persisted `ImagePath` on a failed save), `Views/Member/_Form.cshtml` (registration-snapshot notice), rebuilt `wwwroot/css/app.css`.
- Surprise: the `<dialog>` user-agent `max-width: calc(100% - 6px - 2em)` capped the phone width at 18px clearance even with the media query, so `max-width: none` was added.
- Surprise: the Venue photo is not posted back, so every failed save lost the current-photo block; restoring it in `Editor()` covers both the ModelState and `DomainException` paths with one guard.
- The local SDK is `C:\Users\Administrator\.dotnet\dotnet.exe` (10.0.403); the `dotnet` on PATH has no SDK. Rendered checks ran against `AttendTaMVP_Test` on LocalDB with a temporary admin password reset and one extra "Critique Hall" venue created through the UI.
- No .NET SDK was on PATH, so `dotnet build` used the user-local SDK. No automated test covers these presentation files; the change was verified by build, CSS rebuild, existing JS tests, and a scripted Chromium walkthrough.

## Verification

**Commands:**
- `dotnet build "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"` — expected: build succeeds with no warnings introduced by these files.
- `npm run css:build` (in `ASI.Basecode.WebApp`) — expected: `wwwroot/css/app.css` regenerates and contains the new dialog rules.
- `node --test tests/site.test.js` (in `ASI.Basecode.WebApp`) — expected: existing JS tests pass.

**Manual checks:**
- With `DemoData__Enabled=true` against the disposable local database, open Members → Edit, Event Types → Edit and Venues → Edit: each opens a centered dialog, focus lands on the first editable field, Tab stays inside, Escape returns focus to the row Edit link, and the status/access action stays outside the dialog.
- In the Venue dialog choose a JPEG/PNG: the selected photo previews before save; force a capacity error and confirm the current photo and entered values remain visible.
- At a 390px viewport the dialog keeps 12px outer clearance and 20px body padding, and the footer actions stack without clipping.

## Review Triage Log

### 2026-11-07 — Review pass
- verdicts: 6 findings — high 0, medium 0, low 5, false 1, maybe-false 0
- findings:
  - `[low]` `[patch]` Venue preview caption says "replace the current one" on the create surface, where no current photo exists — `_Form.cshtml` is shared by Create and Edit; patched by deriving the caption from `hasCurrentPhoto`, verified as "add it to the venue" on create and "replace the current one" on edit.
  - `[low]` `[patch]` `width: calc(100vw - 24px)` includes the classic scrollbar gutter, so 12px clearance is not guaranteed with a non-overlay scrollbar — measured 12px in headless Chromium (overlay scrollbars, gutter 0), so the bad outcome is not reproducible here, but the `vw` pitfall is real; patched to `inset-inline: 12px; width: auto`, re-measured 12px clearance and 20px padding.
  - `[low]` `[reject]` Plan mis-cites the notice as "DESIGN §6 screen 8" — the citation is wrong (Members is screen 5), but the fix is to edit this build's plan, which triage rejects; the notice itself is present in `Member/_Form.cshtml` and states the epic R2 snapshot rule.
  - `[low]` `[reject]` Plan's "no copy beyond that notice" is exceeded by the preview caption and alt text — fix is to edit this build's plan, which triage rejects; the caption is the same copy corrected under the first finding.
  - `[false]` `[reject]` Blob URL leaked once per open/select/close cycle — disproved: `venue-photo.js` revokes the previous URL before each new `createObjectURL`, so at most one URL is retained until the next selection or page unload.
  - `[low]` `[patch]` Preview accepted any `image/*` while the server accepts only JPEG/PNG — patched the gate to `image/jpeg`, `image/jpg` and `image/png`; verified a PNG previews and a GIF does not.

## Auto Run Result

- Summary: closed the #128 acceptance findings on the three Master Data record editors without changing the backend contract. The Venue editor now previews a selected JPEG/PNG in both the dialog and the full-page fallback and keeps the persisted current photo after a failed save; the shared dialog reserves a 32px close control, keeps 12px/20px phone geometry, and focuses the first editable field instead of a read-only one; the Member editor states that profile edits do not change registration details or issued certificates.
- Files changed: `VenueController.cs` (restore `ImagePath` for failed saves), `Styles/app.css` and `Styles/features/record-dialog.css` (preview styles, phone rules, close size), `wwwroot/js/features/record-dialog.js` (focus target), `wwwroot/js/features/venue-photo.js` (new preview script), `Views/Venue/{_Form,Index,Create,Edit}.cshtml` (preview markup and script include), `Views/Member/_Form.cshtml` (notice), rebuilt `wwwroot/css/app.css`, and this plan.
- Review findings: 3 patches applied (create-surface caption, `vw` clearance, preview format gate), 0 deferred, 3 rejected — 2 because their fix is to edit this build's plan, 1 `false` because the blob URL is revoked before each new selection.
- Follow-up review recommendation: `false` — no patched entry was `high`, and no `medium` entry was patched (patched counts: high 0, medium 0, low 3).
- Verification performed: `dotnet build` succeeded with 0 warnings and 0 errors; `npm run css:build` regenerated `wwwroot/css/app.css`; `node --test tests/site.test.js` passed 1/1; the impeccable detector returned no findings on the six changed UI files; a scripted Chromium walkthrough confirmed centered dialogs at 1280px, 12px/20px phone geometry at 390px with no horizontal overflow or footer clipping, focus on the first editable control, Tab containment, Escape restoring the row trigger, required markers matching required inputs, 19.43:1 title contrast and 7.58:1 hint contrast, PNG preview present and GIF rejected, and the current-photo block plus entered values retained after a server validation failure.
- Residual risks: the rendered walkthrough ran in headless Chromium only (overlay scrollbars), so the classic-scrollbar clearance is argued from the CSS change, not observed; the local `AttendTaMVP_Test` database now has a temporary admin password and one extra "Critique Hall" venue from the smoke test; no automated test covers these presentation files, so regressions are caught by build plus manual review.

