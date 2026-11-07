---
title: 'Master Data lists: loading, submitting and error states'
type: 'feature'
ticket: '10'
created: '2026-11-07'
status: 'built'
baseline_revision: 'fb177217ccccb9ccfc9123c870233860035eca39'
route: 'full'
route_source: 'auto'
risk: 'medium'
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

**Problem:** The three Master Data lists already filter, count and page (#132/#134), but a slow filter, pager, search or toggle gives no feedback, a failed list read escapes to a bare 500 page, and the Venue list never receives its selected status so its tab highlight and no-match copy are dead.

**Approach:** One progressive-enhancement script marks the list busy and announces a status line on filter, pager, search and toggle activity; each `Index` action gains the house error path (`ViewData["ListError"]` plus an in-shell notice with Try again). No AJAX conversion.

## Boundaries & Constraints

**Always:** Keep the shipped tabs, counts, pager, empty copy and record-dialog triggers working. Lists stay GET; mutations stay POST with antiforgery. Preserve the shell, tabs and table headers while loading and after an error. Reuse `.notice`, `.notice-error`, `.muted`, `.filter-tabs`, `.button:disabled`, and the 1.13 seams (`Styles/features/*.css` + one import; `@section Scripts`). Announce loading with `role="status"`, errors with `role="alert"`; stay legible without motion.

**Never:** Rebuild the filters, counts or pager; convert navigation to AJAX/fetch; add search to Event Type or Venue; touch `_Layout.cshtml`, `site.js`, `_Pager.cshtml`, `_ResultCount.cshtml`, `_RecordDialog.cshtml`, services, repositories or schema; render an empty-state row on a failed load; show exception text; add automated tests (demo rescope).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Slow filter or pager | Click a tab or pager link | Busy label shown, table region `aria-busy="true"`, then the new page | No error expected |
| Toggle submit | Click Deactivate | Button reads "Deactivating…" and disables before the POST | Failure returns the shell notice, row unchanged |
| Back from a slow load | Page restored from bfcache | Busy flag and status line cleared | No stale "Loading…" |
| List read fails | Service throws | Shell, tabs and headers stay; notice with Try again repeating the same URL | No empty-state row, no exception text |
| Filtered empty | Status matches nothing | "No active venues." copy, distinct from no-records copy | No error expected |

</frozen-after-approval>

## Code Map

- `Views/{Member,EventType,Venue}/Index.cshtml` — the lists. Each sets `ViewData["Feature"]="record-dialog"` (single-valued), so the new script loads via `@section Scripts`, already rendered at `_Layout.cshtml:54`. Tabs at 24-28/13-17/13-17; toggle forms at EventType 70-73 and Venue 86-89; empty/populated branches at 30-68/19-47/19-51.
- `Controllers/MemberController.cs:58-64`, `EventTypeController.cs:50-55`, `VenueController.cs:54-55` — the `Index` reads. Venue never sets `ViewBag.Status`, so `Venue/Index.cshtml:5,14-16,38-45` highlights nothing and mis-words the empty row.
- `Controllers/CertificateLedgerController.cs:40-54` — house error shape: `DomainException` → its message; anything else → `LogWarning` + generic message; render with `ViewData[...Error]`.
- `PagedResult.cs` — error model is `new PagedResult<T>(Array.Empty<T>(), 1, 0)`.
- `wwwroot/js/site.js:38-54` — already disables the submit button and swaps `data-submitting-label`; only add the status line and `aria-busy`. `record-dialog.js:21-25,43-45` is the shipped busy/retry idiom.
- `Styles/app.css:4-15` — feature-import list; `wwwroot/css/app.css` is generated. `_Pager.cshtml:6-25` already rebuilds the query string, so `status`/`search` survive pagination.
- Pinned by `MemberPagedListTests`, `EventTypeManagementFlowTests`, `VenuePagedListTests`, `VenueToggleAuditTests`.

## Tasks & Acceptance

**Execution:**
- [x] `wwwroot/js/features/list-feedback.js` — new IIFE: find `[data-list-status]` in `#main-content`; on a `.filter-tab` or `a[rel="prev"|"next"]` click, or a `form[method="get"]` / `form.inline-form` submit, unhide the status line with the button's `data-submitting-label` or the paragraph's `data-list-busy-label` and set `aria-busy="true"` on every `[data-list-region]`; ignore modifier clicks; clear both on `pageshow`.
- [x] `Styles/features/list-feedback.css` + one import in `Styles/app.css` — busy dimming and status spacing; regenerate `wwwroot/css/app.css` via `npm run css:build`.
- [x] `Views/Member/Index.cshtml`, `EventType/Index.cshtml`, `Venue/Index.cshtml` — add the status `<p class="muted" data-list-status role="status" data-list-busy-label="Loading members…|event types…|venues…" hidden>` after the tabs, `data-list-region` on both `.table-scroll` divs, `data-submitting-label` on the toggle buttons, a `@section Scripts` loading the new script, and a `ViewData["ListError"]` branch rendering `.notice.notice-error[role="alert"]` with Try again to `Context.Request.Path` + `QueryString` ahead of the empty/populated branches.
- [x] `Controllers/MemberController.cs`, `EventTypeController.cs`, `VenueController.cs` — wrap the `Index` read like `CertificateLedgerController.Index`: `DomainException` → its message, others → `_logger.LogWarning` + "The member list could not be loaded. Please try again." (per list), set `ViewData["ListError"]`, return the empty `PagedResult<T>`; keep `ViewBag.Search`/`ViewBag.Status` set before the try.
- [x] `Controllers/VenueController.cs:54-55` — set `ViewBag.Status = status` — restores the shipped highlight and no-match copy.
- [x] `PRODUCT.md` — one line for list loading/error states.

**Acceptance Criteria:**
- Given a throttled connection, when the Officer uses a tab, pager link, Search or a toggle, then the status line and `aria-busy` appear before the new page and the toggle keeps a visible label while disabled.
- Given a failing list read, when the page renders, then the shell, tabs and headers remain, the notice names the failure, Try again repeats the same query string, and no empty-state row appears.
- Given `/Venue?status=active`, when it renders, then the Active tab carries `aria-current="page"` and a zero result reads "No active venues."
- Given a list with no records at all, when it renders, then its copy still differs from the filtered no-match copy.
- Given Back after a slow load, when the page restores, then no busy state or status text remains.

## Implementation Notes

- The `ListError` branch renders the notice and then the same `<thead>` as the populated table, with no `<tbody>` row, so the headers survive a failed read and no empty-state row appears. `_Pager` and `_ResultCount` stay outside the branch and render nothing for the empty `PagedResult<T>`.
- `list-feedback.js` records each list submit button's pre-submit label and disabled state at load, then restores both on `pageshow`. Without that, a bfcache restore would leave the toggle reading "Deactivating…" and disabled, because `site.js` disables the button on submit and is out of scope.
- Try again uses `Request.GetEncodedPathAndQuery()` (not decoded `Path` + `QueryString`) so a search term containing `&` or `#` repeats exactly.
- Verified with a temporary integration test (removed afterwards, since the ticket forbids new automated tests): the three lists expose the status line, `data-list-region` and the script; `/Venue?status=active` highlights Active and reads "No active venues."; a throwing `IVenueService` renders the alert, the generic message, a `Try again` link repeating `?status=active&page=2`, the tabs and the table headers, with no empty-state row and no exception text.
- Browser-only checks (DevTools throttling, 390px, keyboard-only, bfcache) were not run in this environment.
- Deviation from the task text: the status paragraph is no longer `hidden`. A live region that is `display:none` when its text is inserted is not reliably announced, which would defeat the plan's own "Announce loading with `role=\"status\"`" clause and `docs/DESIGN.md:251`. The region now stays in the accessibility tree while empty and `[data-list-status]:empty { margin: 0 }` removes the gap.
- Matrix audit: no automated test covers rows 1-5, because the ticket's demo rescope forbids new automated tests. Row 5's no-records copy is pinned by `VenuePagedListTests:70`; the filtered no-match copy and rows 1-4 are covered only by the manual checks below. Recorded rather than fixed, since adding the missing tests would break the ticket's explicit constraint.
- Correction to the note above: `_Pager` sits inside the populated `else` branch in all three views; only `_ResultCount` is outside. The rendered result is still right because `TotalPages == 0` suppresses the pager, but the error page has no pager by branch placement, not by the empty page model.
- Manual check (2026-11-07), headless Chromium against the running app with `DemoData__Enabled=true` on `AttendTa_Demo`: 21/21 interaction checks passed — the status line is empty and laid out when idle, a filter tab and a pager link set `aria-busy` and the busy label before the new page, Search keeps its label while disabled, a toggle reads "Activating…"/"Deactivating…" and disables, no document overflow at 390px on all three lists, and Tab reaches a filter tab with a 2px outline and Enter activates it. 8/8 pagination checks passed with 26 venues: `/Venue?status=active` keeps `status=active` in the pager, the count reads "Showing 1–25 of 26 records.", page 2 stays on Active and reads "Showing 26–26 of 26 records.". 8/8 error checks passed with the `venues` table renamed: the alert, the generic message, three tabs, eight headers, no empty-state row, a Try again link repeating `?status=active`, and recovery in place after the rename was undone.
- The demo database was left as found except that `officer@attendta.test` on the local `AttendTa_Demo` now has the password `Demo-Officer-2026!`; the original hash was overwritten to run the check and cannot be restored.

## Plan Change Log

## Review Triage Log

### 2026-11-07 — quick lens

- **medium / patch** — `list-feedback.js:35-53`: the busy state has no recovery when a navigation is aborted (Escape or Stop during a slow load). `pageshow` never fires, so the status line, `aria-busy` and the disabled toggle stay until a reload. Verified reachable: `setBusy` is the only writer and `pageshow` the only clearer. Fix: a bounded timeout that clears the indicator and says the interaction may not have completed, without re-enabling a toggle whose POST may still be in flight.
- **low / patch** — `list-feedback.js:53`: `clearBusy` runs on every `pageshow`, including the initial one, so a tab activated while slow thumbnails are still loading has its status wiped mid-navigation. Verified: the handler ignores `event.persisted`. Fix: clear only on a bfcache restore.
- **medium / patch** — `list-feedback.css:5`: `opacity: .6` on the busy region composites `.muted` `#64748b` to about `#a2acc0`, roughly 2.3:1 on white, below the 4.5:1 the project targets, and it dims the empty-state row inside the region. Verified against `Styles/app.css:76,106`. Fix: signal busy without changing text contrast.
- **low / patch** — `list-feedback.css:3-4`: `:empty { margin: 0 }` makes the status line zero-height when idle and a line plus margin when busy, so the table and pager jump at the moment of the click. Verified by the box model. Fix: reserve the line so nothing shifts.
- **medium / patch** — Verification gap: the ticket's Done check and Verify line require a look at the changed screen with `DemoData__Enabled=true` plus keyboard and phone-width checks, and the plan recorded that none ran. Fix: perform them against the built app. Done: 21/21 interaction checks and 8/8 pagination checks and 8/8 error-state checks passed in headless Chromium against the running app on `AttendTa_Demo` with `DemoData__Enabled=true`; see Implementation Notes.
- **low / rejected** — Plan Implementation Notes say `_Pager` and `_ResultCount` both sit outside the branch; `_Pager` is inside the populated `else` in all three views. The rendered result is correct (`TotalPages == 0` suppresses it). Rejected because the only fix is a plan edit, which the review rules exclude; the note is corrected below.

## Design Notes

A filter or pager click keeps the current document on screen until the response arrives, so a text status line plus `aria-busy` is truthful without a fetch. A failed read cannot preserve the shell client-side at all, so recovery is the controller's `ViewData["ListError"]` path, the shape `CertificateLedger` already ships.

Decision (2026-11-07): the error/retry state is server-rendered in the three `Index` actions. "Frontend only" is read as no service, repository or schema change; the WebApp controller edit is accepted because no non-AJAX alternative preserves the shell. The full plan is kept at ~2,300 tokens: one goal, with the overage in Code Map and verification detail.

Out of scope: the Member access dialog's POST drops `status`/`search`, and the Event Type/Venue toggles carry only `page`. Both are pre-existing navigation-context losses, not feedback gaps.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln --no-restore -warnaserror` — 0 warnings, 0 errors.
- `npm --prefix ASI.Basecode.WebApp run css:build` then `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` — no diff once the rebuilt sheet is committed.
- `node --test ASI.Basecode.WebApp/tests/site.test.js` — existing JS test green.
- With `ATTENDTA_TEST_SQL`: `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter "FullyQualifiedName~MemberPagedListTests|FullyQualifiedName~EventTypeManagementFlowTests|FullyQualifiedName~VenuePagedListTests|FullyQualifiedName~VenueToggleAuditTests"` — all pass, proving shipped markup is untouched.

**Manual checks:**
- `DemoData__Enabled=true`, Officer, DevTools throttled: use a tab, pager link, Search and a toggle on each list; confirm status line, `aria-busy`, disabled label, and that the new page matches the selected filter.
- Force a failing read (stop the database), confirm the shell, tabs and headers survive with a working Try again.
- 390px width and keyboard-only: tabs, pager, Search and toggle reachable, focus visible, no horizontal document scroll.


