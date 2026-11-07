---
title: '141 officer dossier refusal and retry context follow-up'
type: 'bugfix'
ticket: 16
created: '2026-11-07'
status: 'built'
baseline_revision: 'e99a7b8b7bfc5caec1ac013147b9c5d293470c9d'
route: 'oneshot'
route_source: 'auto'
risk: 'medium'
review: 'none'
review_source: 'pinned'
lenses_ran: []
review_loop_iteration: 0
followup_review_recommended: false
context: [docs/DESIGN.md, PRODUCT.md]
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** PR #164 already implements BMAD 3.16, but dossier check-in/completion refusals are toast-only, failed roster decisions lose participant identity, and legacy review errors leave dossier retry state behind. These gaps affect the ticket's persistent blocked-action and required-reason experience.

**Approach:** Preserve lifecycle refusals inline on the dossier, restore an identifiable registration summary on failed decisions, and scope dossier retry state to dossier submissions. Reuse all existing real services and shared guarded demo commands/store. Keep the existing visual composition and dialog behavior.

</intent-contract>

## Implementation Notes

- Follow-up to the built original plan and merged PR #164, not a rebuild or separate review pass. User explicitly authorizes commit, push and PR, superseding the original plan's historical no-commit constraint.
- Oneshot: under 100 implementation lines across localized controller/view edits. No schema, backend integration, scanner, member certificate, shared store or global delivery-queue changes.
- Given a stale completion/check-in submission, when its existing command refuses it, the returned dossier must retain an inline alert after the toast disappears.
- Given a roster refusal, when its reason dialog reopens, the original reason and registration identity must be visible; a legacy queue refusal must not create a later dossier retry.
- Per-ticket critique/polish and separate review remain deferred by the demo rescope. No new repository automated tests.
- Implemented `RegistrationReviewController.Decide` dossier-origin gating and `Views/Event/Details.cshtml` server-derived retry identity, registration-ID fallback if the row leaves the page, and dialog description association. Updated `PRODUCT.md`.

## Plan Change Log

- Rendered smoke established that `_Layout.cshtml` already renders `ErrorMessage` as a persistent inline alert. Removed the redundant lifecycle controller additions after the browser exposed duplicate alerts. Existing lifecycle behavior is verified rather than rebuilt; only roster retry identity/accessibility and queue-state isolation need implementation.

## Review Triage Log

## Verification

- `dotnet build` — solution compiles.
- `npm run css:build` in `ASI.Basecode.WebApp` — generated Tailwind succeeds.
- Focused temporary browser smoke against this worktree with `DemoData:Enabled=true` and an isolated disposable `AttendTa_IT_<guid>` database: inspect desktop/phone dossier, persistent lifecycle refusals, reason retry identity/input, legacy queue isolation and successful lifecycle refresh. No shared database setup and no secrets in output.
- Run the Impeccable mechanical detector once over changed UI targets; no separate critique/polish pass.
- `git diff --check` — no whitespace errors.
- Record actual results and remaining human acceptance checks here before the PR.

## Auto Run Result

- Summary: follow-up to merged PR #164; restore participant/registration context on refused dossier decisions and prevent legacy queue failures from creating a later dossier retry.
- Changed files: `ASI.Basecode.WebApp/Controllers/RegistrationReviewController.cs` (scope retry TempData to dossier origin); `ASI.Basecode.WebApp/Views/Event/Details.cshtml` (retry summary and accessible description); `PRODUCT.md` (current behavior); this plan (follow-up decisions and actual evidence).
- Review: none; no lenses, critique, or polish launched under the demo exception.
- Build: plain `dotnet build` and early serial retries hit machine memory exhaustion. Final `dotnet build -m:1 -nr:false -p:UseSharedCompilation=false --no-restore` passed, zero warnings/errors, with process-only `DOTNET_GCHeapHardLimit=0x10000000`, `DOTNET_GCConserveMemory=9`, `DOTNET_PROCESSOR_COUNT=2`. It was rerun successfully after removing the redundant lifecycle additions.
- CSS: initial npm install hit memory pressure; `npm ci --ignore-scripts --no-audit --no-fund` and `npm run css:build` passed with process-only `NODE_OPTIONS=--max-old-space-size=128`. Generated CSS has no semantic diff.
- Impeccable mechanical detector: `detect --json ASI.Basecode.WebApp/Views/Event/Details.cshtml` returned `[]`.
- Focused browser smoke: temporary console outside the repository, project references and content root mapped exclusively to this worktree; Development, DemoData enabled, generated `AttendTa_IT_<guid>` LocalDB created/dropped by the existing fixture, ephemeral password/token key never printed. Default Playwright shell was missing; installed `msedge` channel worked. No retry of the denied external-directory read.
- Final browser run passed 13 checks: early completion refused inline; alert persists; cancelled event refuses open, close and complete; failed decline reopens; original participant identified; reason preserved; 390px dialog fits; no horizontal page overflow; Escape closes; legacy queue error leaves no later retry; started event completes via shared overlay. Temporary harness initially needed stale assembly refresh and an extra historical event because the shared demo initializer marks the first past event completed. No application changes were made to the provider/store.
- Visually inspected actual 1440px desktop refusal and 390px phone retry captures from the passing run. Focused smoke only; no broad test suite or rehearsal work.
- Residual checks: existing CI and one peer approval required before merge; full demo rehearsal/visual acceptance remains with its assigned ticket. Real completion/check-in integration remains with the backend owners. This follow-up does not close issue #141 prematurely.
