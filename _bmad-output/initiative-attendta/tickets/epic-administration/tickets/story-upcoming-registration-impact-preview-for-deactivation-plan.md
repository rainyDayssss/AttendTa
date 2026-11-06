---
title: 'Upcoming registration impact preview for deactivation'
type: 'feature'
ticket: '3'
created: '2026-11-06'
status: 'built'
baseline_revision: 'a16daa77560519dc3315ed665e71d5d63b67502e'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 1
context:
  - 'AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Before an Administrator deactivates an account, there is no way to see which upcoming registrations the deactivation will cancel, so the consequence is invisible until after the fact.

**Approach:** Add a read-only Management GET endpoint that returns a dialog listing the target user's upcoming, not-yet-started registrations without valid attendance, and open it from a "Preview deactivation" action on `/admin/accounts`. The endpoint calls the existing `IRegistrationCancellationService.PreviewUpcomingForUserAsync`; it takes no lock and writes nothing.

## Boundaries & Constraints

**Always:** Require the Management policy; call `PreviewUpcomingForUserAsync` and reuse its filtering and order; render Asia/Manila start times; use a native `<dialog>` with Escape, close, and focus return; show an empty state when nothing will be cancelled; keep the existing accounts list and role action unchanged.

**Never:** Add the deactivation command (5.4); change account or membership status; cancel registrations; add a second cancellation or preview query; change the schema, EF mappings, or authentication; add a custom role or permission editor; log personal data, passwords, or tokens.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Two affected | User has two upcoming un-attended registrations | Dialog lists both, ordered by start time | No error |
| Attended | User has an upcoming registration with valid attendance | That registration is not listed | No error |
| Past or cancelled | User has a past event or a Cancelled registration | Not listed | No error |
| Empty | User has no affected registrations | Dialog shows "No upcoming registrations will be cancelled." | No error |
| Access | Anonymous / Member / Officer / Administrator | Login redirect / access denied / dialog / dialog | Policy handles it |
| Fetch failure | Endpoint returns a non-200 response | Page shows an error notice and no dialog | Error notice |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Interfaces/IRegistrationCancellationService.cs` — reuse `PreviewUpcomingForUserAsync`; do not change it.
- `ASI.Basecode.Services/Services/RegistrationCancellationService.cs:54-58` — the read-only preview; no lock, no writes.
- `ASI.Basecode.Services/ServiceModels/UpcomingRegistrationPreview.cs` — row shape: `RegistrationId`, `EventId`, `EventTitle`, `StartsAt`.
- `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs` — existing Administrator-only list and role actions; leave the controller unchanged.
- `ASI.Basecode.WebApp/Views/AdminAccounts/Index.cshtml` — add the "Preview deactivation" trigger beside "Change role".
- `ASI.Basecode.WebApp/Controllers/DeactivationPreviewController.cs` — new Management GET controller; call the service and return the partial.
- `ASI.Basecode.WebApp/Views/Shared/_DeactivationPreview.cshtml` — new dialog partial.
- `ASI.Basecode.WebApp/wwwroot/js/site.js` — add the reusable delegated fetch/showModal handler.
- `ASI.Basecode.WebApp/Styles/app.css` and `wwwroot/css/app.css` — add dialog styles and rebuild.
- `ASI.Basecode.WebApp/Formatting/ManilaTimeFormatter.cs` — format `StartsAt` for display.
- `ASI.Basecode.Resources/Constants/AuthConstants.cs` — `Policies.Management`.
- `ASI.Basecode.WebApp/Startup.DI.cs:47` — `IRegistrationCancellationService` is already registered; no DI change.
- `tests/ASI.Basecode.IntegrationTests/MemberEventFlowTests.cs:884-1196` — `MemberEventTestSupport` seed and sign-in helpers to reuse.
- `tests/ASI.Basecode.IntegrationTests/AdminAccountsListTests.cs` — authenticated HTTP and antiforgery patterns.
- `tests/ASI.Basecode.IntegrationTests/RegistrationCancellationTests.cs:56-100` — proof that valid attendance excludes a row from the preview.
- `tests/ASI.Basecode.IntegrationTests/SqlServerDatabaseFixture.cs` — isolated database per test class.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.WebApp/Controllers/DeactivationPreviewController.cs` — add `[Authorize(Policy = Policies.Management)]`, `[Route("DeactivationPreview")]`, and `GET /DeactivationPreview/{userId:int}`; call `PreviewUpcomingForUserAsync` and return `PartialView("_DeactivationPreview", rows)`.
- [x] `ASI.Basecode.WebApp/Views/Shared/_DeactivationPreview.cshtml` — render `<dialog class="app-dialog" aria-labelledby="deactivation-preview-title">` with a title, the event title and Manila start time per row, an empty state, and a `<form method="dialog">` Close button; add `data-registration-id` to each row for the test.
- [x] `ASI.Basecode.WebApp/Views/AdminAccounts/Index.cshtml` — add a `button type="button"` "Preview deactivation" with `data-deactivation-preview="@Url.Action("Index", "DeactivationPreview", new { userId = account.AccountId })"` in the Actions cell.
- [x] `ASI.Basecode.WebApp/wwwroot/js/site.js` — add a delegated click handler: fetch the URL, inject the partial into a host, call `showModal()`, clear the host on close, return focus to the trigger, and show an error notice on failure.
- [x] `ASI.Basecode.WebApp/Styles/app.css` — add `.app-dialog` and `.app-dialog::backdrop` styles; rebuild `wwwroot/css/app.css` with `npm.cmd run css:build`.
- [x] `tests/ASI.Basecode.IntegrationTests/DeactivationPreviewTests.cs` — seed an admin, a member, and three upcoming events (two un-attended, one with valid attendance); request the preview; assert only the two are listed and nothing was written; add access and empty-state tests.

**Acceptance Criteria:**
- Given a user with two upcoming un-attended registrations and one upcoming registration with valid attendance, when an Administrator requests the preview, then the dialog lists exactly the two un-attended events and not the attended one.
- Given the preview request, when it completes, then no registration status, QR token, or activity log row changed.
- Given a user with no affected registrations, when the preview is requested, then the dialog shows the empty message and no rows.
- Given an anonymous or Member caller, when they request the preview, then the request is refused by the policy; an Officer or Administrator receives the dialog.
- Given the dialog is open, when Escape or Close is used, then it closes and focus returns to the trigger.

## Implementation Notes

Implemented as planned. The preview endpoint reuses `PreviewUpcomingForUserAsync`; the partial returns a complete native `<dialog>`; `site.js` injects it and calls `showModal()`. Added a Node test for the JS fetch-failure notice because the matrix's fetch-failure row is client-side only. The 3.8/3.11 leftovers were stashed before starting, so the diff contains only this story.

## Plan Change Log

## Review Triage Log

**Pass 1 — quick lens (2026-11-06).** Verdicts: high 0, medium 1, low 2, false 2, maybe-false 0.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | `_DeactivationPreview.cshtml:7-11` — the empty state always renders "Upcoming registrations that will be cancelled" and "Deactivating this account cancels these registrations." before "No upcoming registrations will be cancelled." | low | patch | The copy is outside the `@if`; in the empty branch the dialog claims both that registrations will be cancelled and that none will be. The test only asserts the final sentence. |
| 2 | `site.js:26-55` — two quick clicks can let an older fetch overwrite the newer account's dialog. | medium | patch | Both `.then` handlers assign to the same `previewHost`; no request token or `AbortController` exists, so the first response can arrive last and show the wrong account's preview. |
| 3 | `site.js:27` — `event.target.closest` is called without an Element guard. | false | — | A user-initiated click targets an Element (`body`, `html`, a button, or the dialog), all of which have `closest`; the app never dispatches a synthetic click on `document`, so the `TypeError` is not reachable in normal use. |
| 4 | `DeactivationPreviewController.cs:41-44` — unknown user ids return 200 with the empty-state dialog instead of a not-found error. | false | — | The endpoint is linked only from `/admin/accounts` for existing accounts; an unknown id returns an empty preview by design, and the plan's "no query of its own" excludes an account-existence check. The empty state is the correct response for a user with no affected registrations. |
| 5 | `AdminAccounts/Index.cshtml:46` — the preview button is rendered for inactive accounts. | low | patch | The button is unconditional; `account.IsActive` is available but unused. An inactive account cannot be deactivated, so the preview presents a consequence that cannot occur. The self-account case is valid: 5.4 allows self-deactivation when another active Administrator remains. |

## Design Notes

The endpoint is deliberately on a separate Management controller, not on `AdminAccountsController` (Administrator-only), because story 5.6 reuses it from the Management member screen. The preview is read-only: `PreviewUpcomingForUserAsync` already filters to Pending, Confirmed, or Waitlisted registrations on not-yet-started events without valid attendance, and orders by start time. The partial returns a complete `<dialog>` so the integration test can assert the modal without a browser; `site.js` injects it and calls `showModal()`, which gives Escape, focus trapping, and focus restore natively. The Administration module adds no query of its own.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — expected: 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass.
- With `ATTENDTA_TEST_SQL` set to a local SQL Server master connection: `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter FullyQualifiedName~DeactivationPreviewTests` — expected: all pass.
- In `ASI.Basecode.WebApp`: `npm.cmd run css:build` — expected: the compiled stylesheet includes `.app-dialog`.
- In `ASI.Basecode.WebApp`: `npm.cmd run test:js` — expected: the fetch-failure notice test passes.

**Manual checks:**
- Sign in as an Administrator, open `/admin/accounts`, click "Preview deactivation" on an account with upcoming registrations, and confirm the dialog lists them, Escape closes it, and focus returns to the button. Repeat as an Officer against the endpoint URL to confirm Management access.
