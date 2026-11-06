---
title: 'Management account-request review'
type: 'feature'
ticket: ''
created: '2026-11-06'
status: 'built'
baseline_revision: 'e9b703a161bbcd90d9f7274b3ea2539c10f75b0f'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Management can see pending account requests but has no browser controls to approve or reject them, preventing the Epic 1 sign-up flow from being completed through the UI. The current filter is visually awkward.

**Approach:** Add a clear, accessible management review experience to the existing queue using the established transactional decision service; improve the status filter and provide honest feedback for success and errors. Keep account-request submission improvements separate.

## Boundaries & Constraints

**Always:** Keep management authorization and antiforgery on every mutation; use the existing decision service, current authenticated actor, transaction, audit, uniqueness, and concurrency checks. Show actions only for pending requests. Follow the existing Tailwind design tokens, responsive layout, visible focus and labeled form controls. Never display credentials, hashes, or promise email notifications.

**Never:** Change account-request submission, password rules, course fields, schema, decision business rules, or existing JSON endpoint contracts. Do not add standalone member creation. Do not silently accept a failed, stale, or duplicate decision.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Approve | Management submits a pending request | Account and member created by existing service; success displayed and queue refreshed | Service error visible; no false success |
| Reject | Management supplies a nonblank reason for a pending request | Request rejected with existing audit; success displayed and queue refreshed | Empty or invalid reason is explained; preserve entered reason for correction |
| Stale/conflict | Another reviewer has already decided or identity conflicts | No duplicate decision; understandable error and a path to refresh | Existing service remains source of truth |
| Unauthorized | Anonymous/member or missing antiforgery token | No decision or audit mutation | Reject request server-side |
| Filter/empty | Pending, approved or rejected status, including no results | Clear selected filter; responsive empty state; finalized entries have no decision controls | Existing paging and query preservation remain |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Views/AccountRequest/Index.cshtml` renders status selector, table, and pager, but no decisions; `AccountRequestController.cs` owns management-protected queue rendering.
- `ASI.Basecode.WebApp/Controllers/ManagementAccountRequestController.cs` exposes protected JSON POST decisions; preserve its contract. `ASI.Basecode.Services/Services/AccountRequestService.cs` implements locking, authorization, decision, and auditing.
- `ASI.Basecode.WebApp/Styles/app.css` is the CSS source; rebuild `wwwroot/css/app.css` with the existing npm `css:build` script. Reuse layout notifications, `_Pager`, and existing components.
- `tests/ASI.Basecode.IntegrationTests/AccountRequestQueueTests.cs` covers queue rendering and paging; `AccountRequestDecisionTests.cs` covers decision transactions, authentication and antiforgery. Extend rather than replace these tests.
- `_bmad-output/initiative-attendta/ux-attendta/DESIGN.md` and `EXPERIENCE.md` define the visual and behavioral direction: white ledger, slate surfaces, restrained amber, accessible state and feedback.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.WebApp/Controllers/AccountRequestController.cs` — implement management-only antiforgery-protected browser decision posts with success/error handling using the existing decision service, preserving JSON endpoint behavior.
- [x] `ASI.Basecode.WebApp/Views/AccountRequest/Index.cshtml` and any focused partial/view model — add pending-only approval and rejection forms, a required labeled reason, clear inline feedback, and accessible status filters while retaining paging and safe encoding.
- [x] `ASI.Basecode.WebApp/Styles/app.css` and `ASI.Basecode.WebApp/wwwroot/css/app.css` — polish queue and decision controls, including mobile, keyboard focus, error and empty states; rebuild the generated CSS.
- [x] `tests/ASI.Basecode.IntegrationTests/AccountRequestQueueTests.cs` and `AccountRequestDecisionTests.cs` — cover rendered controls, browser decision flows, rejection validation, finalized/stale/conflict outcomes, authorization and antiforgery without regressing existing JSON clients.

**Acceptance Criteria:**
- Given an officer or admin and a pending request, when they approve it using the queue, then the service creates one member account and profile, records the audit, and the UI confirms the outcome.
- Given a pending request, when Management rejects it with a reason, then the decision is recorded and feedback is shown; when the reason is blank, no decision is made and the form explains the problem.
- Given a finalized or concurrently decided request, when a stale decision is submitted, then no duplicate account or audit is created and the user sees a useful error.
- Given an anonymous user, member, or missing antiforgery token, when a decision is posted, then access is refused and no decision is made.
- Given a selected status, when Management navigates filters or pages, then the selected status, correct rows, accessible controls, and appropriate empty state remain clear on desktop and mobile.
- Given existing API callers, when they use the management decision endpoints, then their JSON outcomes and status codes remain unchanged.

## Implementation Notes

- Release build passed with zero warnings; 162 service tests and 192 SQL integration tests passed. CSS was rebuilt. Local Poppler tools were installed in the OpenCode temporary directory for PDF tests.
- After the review fix, the Release build again passed with zero warnings, all 162 service tests passed, and all 192 integration tests passed. Manual keyboard and narrow-viewport inspection remains for human verification.

## Plan Change Log

## Review Triage Log

- Medium, patch: if a pending request moves off the current page between display and a failed rejection, the view loses the entered reason because it only renders it on the request's row. Preserve the submitted reason and recovery controls even when that row is absent.

## Verification

- `npm --prefix ASI.Basecode.WebApp ci` and `npm --prefix ASI.Basecode.WebApp run css:build` — generated CSS is up to date.
- `dotnet build ASI.Basecode.sln --configuration Release` — succeeds without warnings.
- `dotnet test --project tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj --configuration Release` — passes.
- With `ATTENDTA_TEST_SQL` set to local SQL Server master, run the integration executable and the queue and decision tests; report any environment limitations accurately.
- Exercise approval, rejection, stale responses, keyboard focus and a narrow viewport against the local demo without exposing credentials.
