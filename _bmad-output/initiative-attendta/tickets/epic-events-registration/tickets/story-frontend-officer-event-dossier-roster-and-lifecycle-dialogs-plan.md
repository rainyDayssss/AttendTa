---
title: 'Officer event dossier, roster and lifecycle dialogs'
type: 'feature'
ticket: 16
created: '2026-11-07'
status: 'built'
baseline_revision: 'c1d4cc6561edec46286754c76d54d111ff6c9ba3'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'none'
review_source: 'pinned'
lenses_ran: []
review_loop_iteration: 0
context:
  - docs/DESIGN.md
  - _bmad-output/initiative-attendta/demo-mock-register.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Officers currently manage events and registrations on separate screens, with a numeric Event ID filter and immediate lifecycle submissions. There is no contextual event dossier combining metadata, capacity, roster and lifecycle decisions.

**Approach:** Add an event dossier reached from Events, composing existing real event and paged review reads. Reuse real review, promotion and cancellation services and the shared check-in/completion command seam, with centered confirmation/reason dialogs and contextual navigation after decisions.

## Boundaries & Constraints

**Always:** Match DESIGN screens 8–10 and the existing Razor/Tailwind shell. Keep Pending Review, Confirmed and Waitlisted counts visible, use Asia/Manila-labelled times, preserve paging, enforce Management authorization and antiforgery, and let existing services enforce mutation guards. Apply the existing event-state overlay to dossier metadata. Display actionable, persistent validation/refusal feedback; preserve reason input on failure. Respect the demo-phase exception: build, CSS build and focused rendered smoke; no new automated tests or separate per-ticket critique, polish or review pass.

**Never:** Change seat allocation, locking, cancellation transactions, schema or audit semantics; introduce replacement mocks for real services; silently fall back after real failures; expose demo markers; implement My Events or real completion/check-in backends. Preserve unrelated local files and changes. Do not commit, push or open a PR.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Dossier read | Existing event, status and page | Real metadata, whole-event counts, paged status roster | Missing event returns a clear not-found result |
| Registration decision | Pending approve/decline; confirmed/waitlisted cancel | Existing real command, refreshed dossier and counts | Required reason; domain refusal remains visible |
| Promotion | Published event before cutoff | Explicit promote-next command respects real queue priority | No seat/eligible candidate shows existing refusal |
| Lifecycle | Published event; shared check-in toggle or complete | Confirmation then command; refreshed overlaid state | Completion before start or on cancelled event refused |
| Terminal/cutoff | Completed/cancelled event or review cutoff reached | Read-only terminal surface; explain blocked review actions | Stale submissions still face service guards |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Controllers/EventController.cs`: event list/read, real cancel/publish/edit, shared state commands and list overlay; add dossier read and contextual redirects.
- `ASI.Basecode.WebApp/Controllers/RegistrationReviewController.cs`: real approve/decline/cancel/promote POSTs and paged queue; preserve legacy queue while routing event-context decisions back to the dossier.
- `ASI.Basecode.Services/Interfaces/IRegistrationReviewService.cs` and `Services/RegistrationReviewService.cs`: `GetPageAsync` plus guarded mutations; reuse paging totals for whole-event status counts without modifying repository queries.
- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs`: existing count/page contracts and `RegistrationReviewRow` identity/status/submission fields. No new data writes or schema work required.
- `ASI.Basecode.Services/ServiceModels/EventViewModel.cs`: existing metadata and lifecycle fields; add a dossier composite model alongside it.
- `ASI.Basecode.WebApp/Views/Event/Index.cshtml`, `Views/RegistrationReview/Index.cshtml`: current entry points and roster; replace numeric-filter discovery with event-context navigation.
- `ASI.Basecode.WebApp/Views/Shared/_CheckInControl.cshtml`, `wwwroot/js/site.js`: shared control and native dialog open/close/value/reopen conventions.
- `ASI.Basecode.WebApp/DemoData/IEventStateOverlay.cs`, `DemoEventStateCommands.cs`: existing M-04/M-09 seam, including completion and check-in refusals; reuse without adding demo rows.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Services/ServiceModels/EventDossierViewModel.cs` — define composite metadata, selected roster, status counts and cutoff state. Compose existing service reads in the web layer; no new mutation contracts.
- [x] `ASI.Basecode.WebApp/Controllers/EventController.cs` — add dossier loading, normalize status, obtain whole-event totals through existing paged reads sequentially, apply overlay and use injected TimeProvider for presentation guards. Add safe contextual returns for lifecycle POSTs and validation rerendering.
- [x] `ASI.Basecode.WebApp/Controllers/RegistrationReviewController.cs` — return contextual decisions to dossier; retain global queue compatibility and preserve failed reason/decision context. Existing services remain authoritative.
- [x] `ASI.Basecode.WebApp/Views/Event/Details.cshtml` — implement breadcrumb, metadata, capacity, counted tabs, paged roster, explicit empty/cutoff/terminal states, and accessible decision/lifecycle dialogs using existing shared conventions.
- [x] `ASI.Basecode.WebApp/Views/Shared/_CheckInControl.cshtml` and its service model — support confirmation and dossier return context while preserving existing consumers.
- [x] `ASI.Basecode.WebApp/Views/Event/Index.cshtml` and `Views/RegistrationReview/Index.cshtml` — link event identities to dossiers and remove numeric Event ID discovery from the presentation.
- [x] `PRODUCT.md`, scoped `Styles/app.css` only if needed, and generated `wwwroot/css/app.css` — document the surface and compile responsive styling.
- [x] Verify the edge-case matrix with focused interaction smoke and existing checks; inspect final scope and whitespace.

**Acceptance Criteria:**
- Given an authenticated Officer on Events, when opening an event, then its dossier shows metadata, capacity and a paged roster with visible pending/confirmed/waitlisted totals.
- Given an actionable registration before cutoff, when the Officer confirms an allowed decision, then the real service runs and the refreshed dossier reflects its result, including approval-to-waitlist behavior.
- Given a required-reason decision, when its reason is blank or the command refuses it, then clear feedback is shown and the Officer can correct the decision without losing event context.
- Given a published event, when check-in is opened/closed or an eligible started event is completed, then confirmation precedes submission and the resulting state appears on the dossier for the running session.
- Given a cancelled event or an event before start, when completion is attempted, then it is refused; given terminal state, when viewing the dossier, then mutation controls are unavailable.
- Given desktop, phone or keyboard-only use, when navigating tabs and dialogs, then controls remain reachable, dialogs dismiss correctly, required fields are labelled, and the roster stays usable.

## Implementation Notes

- Implemented the dossier composite, existing-read composition, contextual roster decisions, required-reason retries, shared check-in confirmation, completion/event-cancel dialogs, responsive roster and contextual Events links. No data-layer or mutation-service changes.
- Preserved the existing filtered review route and legacy redirects for existing consumers; dossier forms opt into contextual redirects. Updated the existing promotion browser assertion to expect the new dossier link. No automated tests added.
- The implementation subagent could not start because its configured model was unavailable; implementation proceeded in the main session. The tool exposes no model/reasoning override.
- Verification: `dotnet build` passes with zero warnings/errors; CSS compilation passes; targeted service tests 21/21, full service suite 271/271, targeted registration-review/promotion integration classes 63/63, existing JS smoke 1/1.
- Final full integration run: 319/323 pass. Four CertificateTracerFlowTests fail solely because `pdfinfo` is unavailable. Existing Git installation supplies `pdftotext`; its directory was added only to the current process PATH. No tools installed and no machine configuration changed.
- A temporary browser-smoke console outside the repository used an isolated generated integration database, real authentication/review services, and DemoData enabled with a guarded database and ephemeral key. Successful run checked dossier metadata, real approval/capacity, real decline, required reason, check-in open/close overlay, phone dialog width, Escape dismissal, completion overlay and cancelled-event control suppression. Desktop/phone screenshots were inspected under the approved focused smoke, without a separate critique/polish pass.
- The rendered check exposed cramped count labels and inappropriate inherited sticky roster columns; these were corrected using existing filter-tab components and scoped roster CSS. Build, CSS and full integration checks ran after those edits. A final browser-smoke repeat then encountered LocalDB request and CREATE DATABASE timeouts, so rendered re-verification of those last visual adjustments remains pending. LocalDB was not restarted.
- Existing local `.gitignore` change and `IMPLEMENTATION-FAST-PROMPT.md` were preserved. No commit, push or PR was created.
- Continuation: user authorized proceeding after the environmental blockers. A normal stop/start of MSSQLLocalDB restored isolated database creation. The final browser smoke then passed all 14 checks, including real cancellation releasing a seat, phone roster scroll-region bounds, and missing-event 404. Latest desktop and phone screenshots were inspected.
- Downloaded portable Poppler 26.09.0 from the upstream poppler-windows release into the approved temporary directory, with its bin directory prepended only to the test process PATH. A focused CertificateTracerFlowTests run passed 8/8; the subsequent complete integration suite passed 323/323, no skips. No application code changed during this continuation.
- Review disposition: the approved demo-phase ticket explicitly defers separate per-ticket critique, polish and code-review passes to the designated rehearsal/integration tickets. Final diff scope and whitespace checks were performed; peer approval remains part of the eventual PR workflow. No extra review pass was launched.

## Plan Change Log

## Review Triage Log

## Design Notes

Counts come from whole-event read totals, never the visible roster page. Read composition is sequential because services share an EF context. Presentation guards improve clarity but never substitute for server mutation guards. Existing demo completion overlays change only status/check-in fields; real registration reads and commands remain real.

## Verification

**Commands:**
- `dotnet build` — solution compiles after the first implementation slice and at completion if code changes afterward.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj --filter FullyQualifiedName~RegistrationReview` — relevant existing service checks pass early.
- `npm run css:build` in `ASI.Basecode.WebApp` — compiled Tailwind output succeeds.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — full existing service suite passes once implementation is complete.
- `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` with local master `ATTENDTA_TEST_SQL` — existing integration suite passes; first check LocalDB reachability and run sequentially.
- `git diff --check` — no whitespace errors.

**Manual checks:**
- Start with `DemoData__Enabled=true`, approved local database guard and an environment-only token key. Inspect dossier at desktop and phone widths; exercise keyboard dialog focus/dismissal, reason validation, roster paging and lifecycle state refresh. Record unavailable runtime credentials/data as blockers rather than inventing verification evidence.
