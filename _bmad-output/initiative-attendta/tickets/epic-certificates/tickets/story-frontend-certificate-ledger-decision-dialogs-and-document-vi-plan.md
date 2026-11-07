---
title: 'Certificate ledger decision dialogs and document viewer'
type: 'feature'
ticket: '12'
created: '2026-11-07'
status: 'in-review'
baseline_revision: 'dc57cd0622821bce9cd6b6c00a7ef1552f87c1e9'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: []
review_loop_iteration: 0
context:
  - 'docs/DESIGN.md'
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
  - '_bmad-output/initiative-attendta/delivery-queue.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The officer certificate routes currently render empty shells. Officers need to review completed-event attendees, decide eligibility, issue an approved certificate, and view/download its PDF during the demonstration.

**Approach:** Fill the existing typed ledger and viewer using the shared event selector, real repository reads, existing in-memory store, fixed snapshot renderer and configured signatory. Supply missing review/decision/issuance/download contracts through switch-controlled certificate providers, with explicit unavailable behavior when disabled.

## Boundaries & Constraints

**Always:** Management authorization and antiforgery on mutations; current real-record checks on commands; one shared store; no visible indication of simulated data; PDF rendering through RenderSnapshotAsync only. Success follows a completed command. Keep registration, event and attendance identities coherent across screens. Keep 25-row server-side paging and readable phone-width presentation. Preserve the existing untracked Master Data plan. Changes remain uncommitted on feature/6.12-certificate-ledger-ui.

**Never:** SQL/schema changes, retained certificate files, Activity Log writes, bulk issuance, reopening declined reviews, new service implementations in Services/Data, edits to member-history partials, global CSS/JS redesign, or silent fallback after real-read failures. Do not force, stash, reset, commit or push. Per-ticket Impeccable critique/polish is deferred to 8.15/8.12 by the demo exception.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Ledger | Completed event with checked-in registrations | Shared selector, accurate tab counts and paged rows | Empty state if no matching rows |
| Approve | Pending review, valid attendance | Eligible state, then issue action | Refuse stale or invalid state |
| Decline | Pending review and nonblank reason | Final not-eligible state with reason | Missing reason leaves state unchanged |
| Issue | Approved review, completed event, valid attendance | Render real PDF and publish one issue in memory after success | Failure leaves review unissued; duplicate refused |
| Viewer | Issued registration and authorized actor | Landscape A4 PDF, configured signatory, view/download | Unknown/unissued refused; no supplied paths |
| Disabled | Demo switch false | Normal empty/unavailable surface | No demo-store dependency or fallback |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Controllers/CertificateLedgerController.cs` — Management-authorized Index/Viewer shells; replace model construction with provider calls and add thin POST/PDF actions.
- `ASI.Basecode.Services/ServiceModels/CertificateLedgerViewModels.cs` — existing row, tab and viewer contracts; extend only for decisions, reasons and paging.
- `ASI.Basecode.WebApp/Views/CertificateLedger/{Index,Viewer}.cshtml` — preserve current heading/tab composition and shared `_EventSelector`; fill actions, dialogs, empty states and viewer toolbar.
- `ASI.Basecode.Services/ServiceModels/EventSelectorViewModel.cs` and `Views/Shared/_EventSelector.cshtml` — reuse unchanged.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — singleton with attendance/review/issue dictionaries guarded by one gate; extend guarded transitions rather than creating a parallel store.
- `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs` — already seeds a completed event, pending/declined/issued reviews from real confirmed registrations; ensure remaining valid attendees receive reviews without overwriting decisions.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` — existing central switch; add one certificate registration call with enabled and disabled providers, avoiding Startup.DI/global source changes.
- `ASI.Basecode.Data/Interfaces/{IEventRepository,IRegistrationRepository}.cs` — reuse existing event and registration reads; no repository changes planned.
- `ASI.Basecode.Services/Interfaces/ICertificatePdfRenderer.cs` and `ServiceModels/CertificatePdfSnapshot.cs` — immutable snapshot rendering; reuse configured signatory in existing renderer, not the tracer's watermarked render.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Services/Interfaces/ICertificateLedgerService.cs` — define presentation query, decisions, issuance and PDF contracts for later real implementations; no implementation in Services.
- [x] `ASI.Basecode.Services/ServiceModels/CertificateLedgerViewModels.cs` — add required reason/paging/action metadata without breaking existing shells.
- [x] `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — guarded pending decisions and issue publication; retain issued snapshot state for repeat views and 6.13 consumption.
- [x] `ASI.Basecode.WebApp/DemoData/DemoCertificateLedgerService.cs` — compose real reads and shared state, enforce refusals, render snapshots in memory and page results; annotate M-10a/M-10b/M-11a/M-11b removal seams.
- [x] `ASI.Basecode.WebApp/DemoData/{DemoCertificatesRegistration,UnavailableCertificateLedgerService}.cs` and `DemoDataRegistration.cs` — register the isolated certificate surface for both switch states.
- [x] `ASI.Basecode.WebApp/Controllers/CertificateLedgerController.cs` — thin query/command/PDF endpoints with authorization, antiforgery, safe redirects and existing notification conventions.
- [x] `ASI.Basecode.WebApp/Views/CertificateLedger/{Index,Viewer}.cshtml` and certificate-scoped assets — accessible confirmation dialogs, required decline reason, submitting feedback, paging, responsive table and quiet PDF toolbar. Reused the existing compiled `Styles/features/certificate-ledger.css` seam and the `wwwroot/js/features` hook.
- [x] `tests/ASI.Basecode.IntegrationTests/{CertificateLedgerTests,CertificateLedgerFlowTests}.cs` — focused transition/render-failure regressions and real HTTP/browser flow alongside implementation, as required by higher-priority development instructions; retained existing tests.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` — document exact providers/contracts, seed behavior and removal owners for the four certificate rows.

**Acceptance Criteria:**
- Given the seeded completed event, when an Officer selects it, then each valid checked-in attendee has a review and the two tabs show accurate paged state.
- Given pending reviews, when the Officer approves one and declines another with a reason, then only the approved review can be issued and repeat decisions are refused.
- Given an approved review, when issuance succeeds, then its registry entry and landscape A4 PDF are available with the configured signatory and no visible data-source marker.
- Given a render failure or invalid attendance, when issuance is attempted, then no issued state is published.
- Given desktop, phone width and keyboard input, when tabs/dialogs/viewer are used, then controls remain reachable, focus is managed and decline consequences are explicit.

## Implementation Notes

- Approved by the user on 2026-11-07; implemented directly because no subagent capability is exposed. Frozen intent was not changed. No commits, pushes, staging, stashing, resets or force operations were performed.
- Reused the existing compiled feature CSS import rather than adding a second stylesheet. Updated PRODUCT.md and the certificate register; no shared shell, selector, pager, member-history partial, Services implementation, Data or schema changes.
- Tests were written first: the initial focused run failed to compile for the missing provider, then passed after implementation. Browser checks caught and fixed decline focus targeting. The test-host relay resolves redirects internally because TestServer has no listening socket; actual HTTP redirect status is asserted separately.
- The end-to-end check used DemoData:Enabled=true, the startup guard's exact isolated database allowlist, real authentication, real repositories, the existing seed and real snapshot renderer. Browser checks ran at 1440×900 and 390×900, exercised approve/decline/issue forms, checked focus/escape/required reason and no document overflow, and produced screenshots in the system temp directory. Inspected ledger desktop/phone, phone decline, desktop issue and phone viewer screenshots. The embedded viewer is blank in headless Chromium; its Open/Download fallbacks and application/pdf bytes were verified, not a physical phone PDF plugin.
- `F:/AttendTa` is unavailable on this machine; appearance followed docs/DESIGN.md screens 16–17 and the incumbent shell/template. Impeccable per-ticket critique/polish remains deferred by the demo exception.
- GitHub claim/status was not mutated: step-03 explicitly forbids remote operations. The earlier live check found issue #139 open, assigned to wigglebop25, with no claim comments; a human must reconcile the remote claim/status and eventual peer approval.
- Verification: final `dotnet build` passed with 0 warnings/errors; services 271/271 passed; complete LocalDB integration suite 332/332 passed, including all 9 new ledger tests. Existing JS test 1/1 passed. CSS rebuilt byte-identically. Changed-file `dotnet format whitespace ... --verify-no-changes --no-restore` and `git diff --check` passed. AFT reported no diagnostics but cannot supply C# LSP validation because that server is not installed; the actual compiler is the authority.
- Matrix audit: Ledger is covered by LedgerCreatesReviewsForEveryValidAttendeeAndPagesBothTabs and the browser flow; Approve/Decline by DecisionsRequirePendingValidAttendanceAndDeclineReason and browser-submitted forms; Issue by render-failure, concurrent-issue and snapshot tests; Viewer by the HTTP/browser flow plus the existing official A4/signatory renderer tests; Disabled by DisabledProviderIsEmptyAndRefusesCommandsAndDocuments and existing switch-off integration coverage. All covering tests ran in the passing full suites.

## Plan Change Log

## Review Triage Log

- Quick lens not launched: the installed step-04 requires a context-free reviewer subagent, but this session has no subagent tool. A standalone `-plan-quick.md` packet is generated beside this plan as required by the fallback. No independent review findings or approval are claimed. Stop here pending a separate-session review and returned findings.

## Design Notes

The fetched baseline already contains 1.13 and 1.14. Extend their contracts/store instead of rebuilding navigation or event-state infrastructure. Preserve snapshot values once an issue is published; no private file writes are authorized in this slice. The live issue includes superseded workflow wording; the local story and demo queue explicitly replace that wording. Subagent tooling is unavailable in this session, so investigation and subsequent implementation must run directly.

## Verification

- `dotnet build` — solution compiles.
- `npm --prefix ASI.Basecode.WebApp run css:build` — regenerate CSS when Razor/styles change; inspect generated diff, never hand-edit output.
- `npm --prefix ASI.Basecode.WebApp run test:js` — existing frontend checks pass.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — existing and focused tests pass using actual project runner syntax.
- `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` with a local master connection in ATTENDTA_TEST_SQL — integration checks pass or report actual environment blocker.
- Run the repository-supported formatter check after confirming its configuration; do not invent a lint script.
- Run `aft_inspect` after edits and inspect `git diff --check` and final status.
- Start with DemoData__Enabled=true and the guarded allowed database; inspect ledger/viewer at desktop/phone width and exercise approve/decline/issue/refusal paths. Record unavailable browser/database/renderer prerequisites as blockers rather than claiming execution.
- Recheck issue claims before coding, claim this session through the queue protocol, and leave merge/peer approval to the authorized human workflow.
