---
title: 'Preliminary shared-dialog frontend polish'
type: 'refactor'
ticket: 12
created: '2026-11-07'
status: 'in-review'
baseline_revision: 'c1d4cc6561edec46286754c76d54d111ff6c9ba3'
route: 'oneshot'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: []
review_loop_iteration: 0
context: ['docs/DESIGN.md', 'PRODUCT.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Issue #153 / BMAD 8.12 requires the final integrated frontend walkthrough, but #152/8.11 and #135/8.15 are incomplete. The user explicitly authorizes a preliminary frontend pass now, without representing the final acceptance as complete. Shared native dialogs currently lack explicit centering after Tailwind v4 resets margins, and the shared backdrop-click handler dismisses clicks on the dialog's own interior padding.

**Approach:** Preserve the approved Razor/Tailwind visual identity and backend behavior. Restore shared dialog centering and viewport-bounded scrolling, and dismiss only actual outside clicks. Add focused regression tests and perform the available Impeccable critique/polish and browser checks. Never remove demo providers, implement missing integrations, modify business behavior, commit, push, or disturb the unrelated untracked quick plan. Final integrated acceptance remains blocked pending the real integration gate and rehearsal findings.

</frozen-after-approval>

## Implementation Notes

- One-shot route: two tightly scoped shared-dialog defects, estimated below 100 added/modified code lines including Node regression coverage. Medium risk because the styles and handler serve multiple record decisions.
- Tracking: investigation complete; regression coverage pending; implementation pending; verification pending; independent review pending.
- `wwwroot/js/site.js` owns delegated open/close behavior; retain native Escape, focus trapping and restoration.
- `Styles/app.css` defines confirm/app/task dialog classes; Tailwind v4 documentation confirms `dialog { margin: auto; }` restores native centering. Bound scroll height and wrap long content without restyling unrelated components.
- `tests/site.test.js` uses Node's test runner with a lightweight DOM stub; extend this harness rather than adding a new test framework.
- No subagent tool is exposed. Impeccable critique must disclose single-context assessment. BMad independent review requires a handoff prompt and a stop if that capability remains unavailable.
- Implementation complete for the authorized preliminary scope: restored native centering, bounded all three dialog variants to dynamic viewport height, wrapped long strings and task actions, and restricted shared backdrop dismissal to outside coordinates. No Razor/business/backend mutations.
- TDD: the new interior-padding and boundary regressions failed before the handler change; all eight Node tests passed afterward.
- Final tracking: investigation, regression coverage, implementation, rendered component critique/polish and verification complete. Independent quick review pending; no reviewer was launched and no independent review is claimed. The plan is in-review, not built/done.
- Verification: npm ci audited 66 packages with zero vulnerabilities; css:build passed; dotnet build passed with zero warnings/errors; Services.Tests 271/271 passed. IntegrationTests initially failed without ATTENDTA_TEST_SQL; rerun with the documented local-master connection passed 323/323 using isolated databases.
- `dotnet format --verify-no-changes --no-restore` failed on existing whitespace in untouched service/integration test files (including EventCoverServiceTests, EventLifecycleServiceTests, AccountRequestDecisionTests, EventDraftFlowTests, EventPublicationFlowTests, PublishedEventEditFlowTests, RegistrationPromotionTests and RegistrationReviewTests). No unrelated formatting edits made.
- AFT diagnostics: zero reported errors/warnings, but TypeScript language server is not installed, so diagnostics are incomplete. Node regression execution and the actual solution build are the authoritative checks.
- Browser: reused installed Playwright 1.64.1 Chromium, temporary loopback server and source-derived dialog snippets with Razor server attributes removed; no authentication/database needed. All three variants passed at 1440x900, 1024x768, 768x1024, 390x844 and 720x450: geometry, interior/backdrop clicks, native keyboard/background exclusion, Escape/focus restore, long unbroken content scrolling, reachable actions, no page errors. Inspected normal/long/action screenshots. Native Tab may visit browser chrome/body; no custom focus-trap certification claimed. Server/browser closed in finally.
- Evidence is local/transient: `C:/Users/JEHRON/AppData/Local/Temp/attendta-153-dialog-check.cjs` and `C:/Users/JEHRON/AppData/Local/Temp/attendta-153-dialog-evidence/`. Re-run the script from the repository root after a build to reproduce the component smoke. No broad screenshot test suite added.
- Impeccable source detector ran once on Views and returned `[]`; no overlay was injected. Single-context critique and polish archived at `.impeccable/critique/2026-11-06T16-52-04Z__p-views-shared-deactivationpreview-cshtml-c689773a.md`; component score 29/40, two corrected priorities, zero remaining scoped P0/P1. The component snapshot is closed, not the ticket.
- Final release acceptance remains blocked pending #152/#135. 720x450 is only a half-size viewport proxy; actual 200% browser zoom, integrated 20-screen role journeys, PDF/QR, error/retry and rehearsal findings are unverified. This work must not close #153.
- No commits, pushes, issue claims/status updates or configuration changes. Preserved unrelated untracked Master Data quick plan and external backup.

## Plan Change Log

- 2026-11-07 follow-up: User authorized a normal merge of origin/dev, conflict resolution, verification, push and draft PR. Merged dev at dc57cd0622821bce9cd6b6c00a7ef1552f87c1e9; only generated wwwroot/css/app.css conflicted. Regenerated it from the cleanly merged source, preserving upstream feature imports and all preliminary dialog fixes. Repeated CSS generation was byte-identical. Post-merge checks: 8/8 Node tests, clean build (zero warnings/errors), 271/271 service tests, 323/323 disposable LocalDB integration tests. Formatter still fails on untouched test whitespace. Merge-wide whitespace check also reports an upstream plan's extra EOF blank line; the scoped diff against origin/dev passes. AFT reports zero diagnostics with TypeScript server unavailable. No new rendered-browser verification or independent approval is claimed; final acceptance gates remain unchanged.

- 2026-11-07: User superseded the earlier no-commit/no-push restriction and authorized a scoped commit, push and draft PR only after a conflict-free check against freshly fetched origin/dev. This does not authorize final acceptance or closing #153. Independent review is not approved: the prior review attempt lacked command tooling/workflow rendering. The earlier handoff remains historical evidence, not approval.

## Review Triage Log

- Independent review unavailable: no Task/subagent tool is exposed. Quick-review handoff is written beside this plan; wait for findings before finalizing the BMad build. Thorough/adversarial lenses were not run on this one-shot route.

## Verification

- `npm run test:js` in `ASI.Basecode.WebApp`: existing preview failure test plus interior, child and outside dialog click regressions pass.
- `npm ci` then `npm run css:build` in `ASI.Basecode.WebApp`: regenerate tracked CSS with pinned dependencies; no package changes.
- `git diff --check`: no whitespace errors.
- `dotnet format --verify-no-changes --no-restore`: report existing failures separately; do not reformat unrelated code.
- `dotnet build` and Services.Tests: compile and existing service regressions pass.
- IntegrationTests: run if local SQL tooling permits; report skips and prerequisite failures accurately.
- `aft_inspect` after edits, before build/tests.
- Run Impeccable detector once on shared view markup after fixes; source-only results do not certify rendered quality.
- Attempt focused browser checks at desktop/1024/768/390 widths, keyboard focus/Escape and 200% zoom. Distinguish isolated shared-component checks from authenticated integrated routes. Never invent screenshots or results.
- Final acceptance remains blocked: all 20 real integrated screens, role navigation, PDF/QR presentation and rehearsal findings require completed #152/#135. This preliminary pass does not close issue #153.
