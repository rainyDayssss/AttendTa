---
title: 'Frontend account and membership access dialogs'
type: 'feature'
ticket: '11'
created: '2026-11-07'
status: 'built'
baseline_revision: 'dc57cd0622821bce9cd6b6c00a7ef1552f87c1e9'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
  - 'docs/DESIGN.md'
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Accounts and Members lists carry empty access-action slots, so an Administrator cannot deactivate/reactivate an account and an Officer cannot deactivate/reactivate a membership; the last-active-Administrator block has no on-screen explanation.

**Approach:** Fill the two 1.13 slot and dialog partials with reason-required dialogs. A new access-command contract is served by the 1.14 demo seam until 5.4–5.7 implement it, and a demo overlay flips the status in both lists. The account dialog reuses the real 5.3 preview.

## Boundaries & Constraints

**Always:** Use the real 5.3 preview and real account/member reads; keep account access separate from membership eligibility; require a non-empty reason on the server; refuse deactivating the last active Administrator with a visible explanation; change demo state only through `DemoStore` and its overlay; keep the existing list markup except the two `_AccessActions` partials; native `<dialog>` with Escape, close, and focus return; antiforgery on every POST; no visible demo wording.

**Never:** Implement the real 5.4–5.7 writes, activity logs, session refusal, or registration cancellation; change the schema, EF mappings, authentication, or the Member Edit form; add new automated tests or per-ticket Impeccable critique/polish; edit `wwwroot/css/app.css` by hand.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Account deactivate | Active, not last admin, reason entered | Dialog lists affected registrations; POST flips the badge to Inactive and toasts success | No error |
| Last admin | Active Administrator who is the only one | Dialog shows the blocked explanation and no deactivate form | No error |
| Account reactivate | Inactive, reason entered | Dialog explains access-only restore; POST flips the badge to Active | No error |
| Membership deactivate | Active membership, reason entered | Membership consequence and preview shown; Members badge Inactive, account badge unchanged | No error |
| Empty reason | Blank or whitespace | No state change | Inline required + error toast |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/ServiceModels/AccessDialogViewModels.cs` — extend both models with role, `IsLastActiveAdministrator`, `UserId`, `AffectedRegistrations`, and settable `IsActive`.
- `ASI.Basecode.Services/ServiceModels/AccountSummaryViewModel.cs` — make `IsActive` settable; `MemberViewModel.cs` + `Services/MemberService.cs` — add and map display-only `UserId`.
- `ASI.Basecode.Services/Interfaces/IRegistrationCancellationService.cs` — reuse `PreviewUpcomingForUserAsync`; do not change.
- `ASI.Basecode.Data/Interfaces/IAccountAdministrationRepository.cs` — `GetAccountAsync` and `CountActiveAdministratorsAsync` already exist; `IMemberRepository.GetMemberRecord(int)` carries `UserId` and `Status`.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — `SetAccountActive`, `SetMembershipStatus`, and the `TryGet*` reads; do not change its shape.
- `ASI.Basecode.WebApp/DemoData/DemoEventStateCommands.cs`, `IEventStateOverlay.cs`, `DemoDataRegistration.cs` — copy the command/overlay/registration pattern.
- `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs` — `Index` (line 42), `Access` (line 53); `MemberController.cs` — `Index` (line 31), `Access` (line 44).
- `ASI.Basecode.WebApp/Views/AdminAccounts/_AccessActions.cshtml`, `_AccessDialog.cshtml`; `Views/Member/_AccessActions.cshtml`, `_AccessDialog.cshtml` — the four stubs to fill; the slot includes at `AdminAccounts/Index.cshtml:50` and `Member/Index.cshtml:102` stay unchanged.
- `ASI.Basecode.WebApp/Views/Shared/_DeactivationPreview.cshtml`, `wwwroot/js/site.js:54-111`, `Styles/features/access-dialogs.css` — reuse the dialog, fetch, focus, and error patterns; `Views/AccountRequest/Index.cshtml:179-219` — required-reason dialog markup.
- `ASI.Basecode.WebApp/Startup.DI.cs` — register the new service; `DemoDataRegistration` already runs at line 84.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Services/Interfaces/IAccountAccessCommands.cs`, `IMembershipAccessCommands.cs` — add `DeactivateAsync`/`ReactivateAsync(int id, string reason, CancellationToken)` — the real 5.4–5.7 services will implement them.
- [x] `ASI.Basecode.Services/Interfaces/IAccessDialogService.cs`, `Services/AccessDialogService.cs` — compose the real read, the real preview, and the last-admin count into the dialog models — keeps controllers off repositories.
- [x] `ASI.Basecode.Services/ServiceModels/AccessDialogViewModels.cs` — extend both models; `AccountSummaryViewModel.cs` — settable `IsActive`; `MemberViewModel.cs` + `MemberService.cs` — map `UserId`.
- [x] `ASI.Basecode.WebApp/DemoData/IAccessStateOverlay.cs`, `DemoAccessStateOverlay.cs`, `NoOpAccessStateOverlay.cs` — apply store state to both list pages and both dialog models — register rule 7.
- [x] `ASI.Basecode.WebApp/DemoData/DemoAccountAccessCommands.cs`, `DemoMembershipAccessCommands.cs`, `UnavailableAccessCommands.cs` — reload real state, apply the last-admin refusal, write only the store; the unavailable pair serves switch-off — register rules 4 and 8.
- [x] `ASI.Basecode.WebApp/DemoData/DemoAccessRegistration.cs`, `DemoDataRegistration.cs` — one access surface registration file behind the switch — register rule 9.
- [x] `ASI.Basecode.WebApp/Startup.DI.cs` — register `IAccessDialogService`.
- [x] `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs` — overlay `Index`; populate `Access`; add `POST .../access/deactivate` and `.../reactivate` with `DomainException` to `TempData`; `MemberController.cs` — the same four changes.
- [x] `Views/AdminAccounts/_AccessActions.cshtml`, `Views/AdminAccounts/_AccessDialog.cshtml`, `Views/Member/_AccessActions.cshtml`, `Views/Member/_AccessDialog.cshtml` — render the trigger, state, consequence, preview list, required reason, blocked state, and POST forms.
- [x] `ASI.Basecode.WebApp/wwwroot/js/site.js` — generalize the preview fetch into one `wireFetchedDialog` for `data-deactivation-preview` and `data-access-dialog`, keeping the preview error id and focus return — one delegated handler keeps the JS test green.
- [x] `ASI.Basecode.WebApp/Styles/features/access-dialogs.css` — add consequence, blocked, and list styles; rebuild the compiled CSS.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` — fill rows M-14a/b and M-15a/b with the interface and class names — register rule 11.

**Acceptance Criteria:**
- Given an active account that is not the last Administrator, when an Administrator submits a reason, then the Accounts list shows Inactive and a success toast.
- Given the only active Administrator, when the dialog opens, then it shows the blocked explanation and no deactivate form, and a direct POST is refused.
- Given an inactive account, when an Administrator submits a reactivation reason, then the Accounts list shows Active.
- Given an active membership, when an Officer submits a deactivation reason, then the Members list shows Inactive while the linked account's status is unchanged.
- Given an empty or whitespace reason, when submitted, then no state changes and the refusal is shown.
- Given either dialog, when dismissed, then Escape and Close work and focus returns to the trigger.

## Implementation Notes

Implemented as planned on `feature/149-account-membership-access-dialogs` (commit `560eaea`). The two access-command contracts, `AccessDialogService`, the demo commands and overlay, the four partials, the generalized `site.js` fetched-dialog handler, and the rebuilt stylesheet are in place. No list markup changed outside the two `_AccessActions` partials.

Verification: `dotnet build ASI.Basecode.sln` 0 warnings/0 errors; Services tests 271/271; Integration tests 323/323 with `ATTENDTA_TEST_SQL`; `npm run test:js` 1/1; `npm run css:build` contains the access-dialog classes. Manual smoke with `DemoData__Enabled=true` covered both lists, both dialogs, the empty reason, the last-admin block, and a direct POST.

Known limit: the demo last-administrator refusal counts the real active Administrators. With two real Administrators, demo-deactivating one does not reduce that count, so the other can also be demo-deactivated. The block is reliable for the single-Administrator demo scenario; the real rule arrives with 5.4. This is recorded in `demo-mock-register.md`.

Review pass 1 patched the reason-check order in both demo commands, added `aria-describedby` to both dialogs, and added the browser-side whitespace-reason guard in `site.js`. The two demo overlay limits (status filter before the overlay, last-admin flag from the real row) are recorded in the register.

Matrix audit: the matrix rows are UI/demo behaviors and the demo-phase ticket forbids new automated tests, so they are verified by the manual smoke above rather than a new test. The existing preview JS test still covers the shared fetch-failure path.

## Plan Change Log

## Review Triage Log

**Pass 1 — quick lens (2026-11-07).** Verdicts: high 0, medium 0, low 6, false 0, maybe-false 0.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | `_AccessDialog` reason: a whitespace-only value passes `required`, the server refuses, and the redirect loses the dialog and the typed text | low | patch | `required` catches empty but not whitespace; `DemoStore` refuses it and the controller redirects with a toast. Fixed with a submit-time trim check in `site.js` so the common case stays in the dialog; a direct POST still gets the standard refusal. |
| 2 | Members status filter runs in SQL before the overlay, so a demo-deactivated member stays under Active | low | patch | `MemberService.GetPageAsync` filters `status` before `IAccessStateOverlay` runs; the unfiltered list (the demo path) is correct. Recorded as a known demo limit. |
| 3 | POST redirects drop `search`/`status`/`page` | low | reject | The demo script acts from the unfiltered list; preserving list context needs hidden fields and query plumbing beyond a direct correction, and 8.15/8.12 own the polish. |
| 4 | Demo commands check state before the reason, so a blank reason on the last admin reports the last-admin refusal | low | patch | `DemoAccountAccessCommands.DeactivateAsync` checks `IsActive`/last-admin before `DemoStore` checks the reason; the membership command does the same. Fixed by checking the reason first. |
| 5 | `IsLastActiveAdministrator` is computed from the real row while `IsActive` is overlaid; a demo-reactivated real-inactive admin can show a form the POST refuses | low | patch | The normal real-active single-admin path is correct; the narrow case is recorded as a known demo limit. |
| 6 | Dialogs set `aria-labelledby` but not `aria-describedby`; the blocked `role="alert"` is inserted before `showModal()` | low | patch | `docs/DESIGN.md` §5 requires associating the title and description. Added `aria-describedby` to both dialogs; the blocked text remains visible and is announced by the dialog description. |

## Design Notes

The dialog is fetched from the 1.13 GET action and returns a complete `<dialog>`, matching the 5.3 preview. The overlay runs in the controller after the real read, never as a repository decorator, because demo state is not a SQL row. The last-admin check lives in the demo command (reload, then count) so the refusal matches 5.4; the dialog also computes the flag for the blocked state. The membership dialog previews by the member's linked user id, so 5.6 can wire the same partial. With the switch off, the no-op overlay and unavailable commands keep the screens identical and return the standard refusal.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — expected: 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all existing tests pass.
- With `ATTENDTA_TEST_SQL` set: `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — expected: all existing tests pass.
- In `ASI.Basecode.WebApp`: `npm.cmd run css:build` and `npm.cmd run test:js` — expected: the access-dialog classes compile and the preview error test passes.

**Manual checks:**
- With `DemoData__Enabled=true`, `DemoData:TokenKey` set, and `DemoData:AllowDatabase` matching the local database, deactivate and reactivate an account on `/admin/accounts` and a membership on `/Member`; confirm the badges flip, the last-admin dialog is blocked, and both widths are usable.
