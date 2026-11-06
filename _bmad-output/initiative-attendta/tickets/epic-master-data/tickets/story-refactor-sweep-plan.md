---
title: 'Refactor sweep'
type: 'refactor'
ticket: '9'
created: '2026-11-06'
status: 'built'
baseline_revision: 'f6b35428b4f59de181944a270d119ddffab2fea1'
route: 'full'
route_source: 'pinned'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: [quick]
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Master Data module (Member, Event Type, Venue) passed its feature tickets but still carries dead code, duplicated controller error handling, and Razor markup that drifted from the house pattern. The ticket's verify clause also names `actorUserId` as forbidden, and `EventTypeService` still uses that identifier.

**Approach:** Remove verified-dead code and duplication, align the three view sets and the member profile to `docs/DESIGN.md` §5–§6 using the existing house patterns, rename the remaining `actorUserId` identifiers, and close the rendered-critique findings the human approved: required markers, input contrast, form width and grouping, result counts, the Member email column, Profile grouping, the canonical screen names, and the login typeface. No search filters, sorting, page-number paging, or dialog conversion.

## Boundaries & Constraints

**Always:** Behavior stays identical for every existing endpoint. Shared CSS changes are limited to the rules the approved findings require. Every mutation stays POST + antiforgery. `ICurrentActor` remains the only service-layer actor source. `Data` repositories are untouched.

**Never:** Do not add search, status filters, sortable headers, page-number paging, or convert the editors to dialogs — those remain new scope. Do not remove `InvalidDataException` or `GetActorUserId` outside Master Data. Do not hard-delete records. Do not change the shared `_Pager.cshtml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| List renders | Officer opens `/Member`, `/EventType`, `/Venue` | Same rows and order; status is a `.badge`, numeric cells use `.numeric`, the canonical `h1` and a result count render, and Member shows Email | No error expected |
| Empty list | 0 rows, or a search with no match | The table and headers are retained; a full-width message row explains no-records versus no-match; no pager | No error expected |
| Toggle on a later page | `POST /EventType/ToggleActive` with `page=4` | Redirect returns to `/EventType?page=4` | No error expected |
| Venue save fails | Capacity below a linked event's limit | Field error redisplayed through the shared `HandleDomainException` | Field error, no redirect |
| Dead code removed | Build plus Master Data tests | Zero warnings, all tests green | n/a |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Services/EventTypeService.cs` — private `ActorUserId()` at 60 and locals at 109/125/137/157/168/181; the only `actorUserId` left in Master Data.
- `ASI.Basecode.WebApp/Controllers/VenueController.cs` — private `ReportError` at 109 duplicates `ControllerBase.HandleDomainException` (`Mvc/ControllerBase.cs:181`). The `using ASI.Basecode.Services.Services;` at 5 is **required**: it supplies the `OpenSafelyAsync` extension (`Services/Services/PrivateFileOperations.cs:17`).
- `ASI.Basecode.WebApp/Controllers/EventTypeController.cs` — `ToggleActive(int id)` at 99 drops the current page; `VenueController.ToggleActive(int id, int? page)` at 98 is the pattern to copy.
- `ASI.Basecode.WebApp/Startup.AutoMapper.cs` — `CreateMap<EventTypeViewModel, EventType>()` at 28 and `CreateMap<VenueViewModel, Venue>()` at 30 have no `Map<T>` caller; services map by hand.
- `ASI.Basecode.Services/ServiceModels/MemberViewModel.cs` — `UserId` at 9 is written at `MemberService.cs:275` and read nowhere.
- `ASI.Basecode.WebApp/Views/Member/Index.cshtml` — table always renders, then empty-state sits outside `.table-scroll` (54–57).
- `ASI.Basecode.WebApp/Views/EventType/Index.cshtml` — `aria-label="EventType table"` (17), `<td>` without `class="actions"` (34), `<table>` indented one level deeper than siblings (18).
- `ASI.Basecode.WebApp/Views/Venue/Index.cshtml` — `<table>` under-indented (18); `AvailabilityNotes` is free text.
- `ASI.Basecode.WebApp/Views/{Member/Edit,EventType/Create,EventType/Edit,Venue/Create,Venue/Edit}.cshtml` — stray double blank line between `<h2>` and `<form>` (lines 7–8 in each).
- `ASI.Basecode.WebApp/Views/Venue/Edit.cshtml` — line 12 claims a photo is stored but renders none; `Photo` action and `.venue-thumb` already exist.
- House pattern to copy: `Views/AccountRequest/Index.cshtml` (badges 126, `.row-actions` 130, `button-small` 132, `.numeric` 118–125).
- Tests that pin current markup: `MemberPagedListTests` (`data-member-id`, "No members match your search.", "inactive"), `EventTypeManagementFlowTests` (`data-table`), `VenuePagedListTests` (pager labels).
- `ASI.Basecode.Services/ServiceModels/IPagedResult.cs` and `PagedResult.cs` — gained `FirstItemNumber`/`LastItemNumber` so the shared result count and pager read one contract.
- `ASI.Basecode.WebApp/Views/Shared/_ResultCount.cshtml` — new shared partial rendering "Showing X–Y of N records"; renders nothing at zero because the empty row explains it.
- `ASI.Basecode.WebApp/Views/Profile/Index.cshtml` — split into Account / Academic details / Membership panels with a status badge.
- `ASI.Basecode.WebApp/Views/{Member,EventType,Venue}/_Form.cshtml` — fieldsets, legends, and required markers.
- `ASI.Basecode.WebApp/Styles/app.css` — `.required`, `.form-panel`, `.form-actions`, `.panel + .panel`, input border `#64748b`, and the login card returned to Outfit.

## Tasks & Acceptance

**Execution:**
- [ ] `ASI.Basecode.Services/Services/EventTypeService.cs` — rename `ActorUserId()` to `RequireActorId()` and every `actorUserId` local to `actorId` — satisfies the verify clause literally.
- [ ] `ASI.Basecode.WebApp/Controllers/VenueController.cs` — replace `ReportError(ex); return View(model);` with `return HandleDomainException(ex, model);` and delete `ReportError`; keep the `Services.Services` using for `OpenSafelyAsync` — removes duplicated error mapping.
- [ ] `ASI.Basecode.WebApp/Controllers/EventTypeController.cs` — accept `int? page` on `ToggleActive` and redirect with it, mirroring `VenueController` — keeps the officer on their page.
- [ ] `ASI.Basecode.WebApp/Startup.AutoMapper.cs` — delete the two unused ViewModel→Entity maps — dead configuration.
- [ ] `ASI.Basecode.Services/ServiceModels/MemberViewModel.cs` and `ASI.Basecode.Services/Services/MemberService.cs` — remove `UserId` and its assignment — dead property.
- [ ] `ASI.Basecode.WebApp/Views/Member/Index.cshtml` — wrap table + pager in the `if (empty) / else` shape used by the other two lists; add `.numeric` to Student Number, Year, Mobile; render Status as `badge badge-active`/`badge badge-inactive` — consistency.
- [ ] `ASI.Basecode.WebApp/Views/EventType/Index.cshtml` — fix `aria-label` to "Event Type table", add `class="actions"`, wrap actions in `.row-actions` with `button-small`, badge the status, normalise table indentation — consistency.
- [ ] `ASI.Basecode.WebApp/Views/Venue/Index.cshtml` — `.numeric` on Capacity, `.row-actions` + `button-small`, badge the status, normalise table indentation — consistency.
- [ ] `ASI.Basecode.WebApp/Views/{Member/Edit,EventType/Create,EventType/Edit,Venue/Create,Venue/Edit}.cshtml` — delete the stray blank line; in `Venue/Edit.cshtml` render the current photo above the form via `Url.Action("Photo", new { id = Model.VenueId })` — removes the artifact and the false claim.
- [ ] Run `/impeccable polish` on the three view folders after the edits, then rebuild CSS only if a class change requires it.

**Acceptance Criteria:**
- Given `dotnet build ASI.Basecode.sln --no-restore -warnaserror`, when it runs, then it reports 0 warnings and 0 errors.
- Given a grep over Master Data controllers, services, view models, and views, when searching for `InvalidDataException`, `GetActorUserId`, `actorUserId`, `DateTime.UtcNow`, and `[Authorize(... Roles`, then there are no matches.
- Given the Services and Integration test projects, when run, then every Master Data test passes.
- Given an Officer opens each of the three lists, when rendered, then status is a badge, numeric cells are monospaced, row actions are small, and the pager markup is identical across the three.
- Given an Event Type is toggled from page 4, when the POST returns, then the redirect lands on `/EventType?page=4`.

## Implementation Notes

- The planned removal of `using ASI.Basecode.Services.Services;` from `VenueController` was wrong: that namespace supplies the `OpenSafelyAsync` extension (`Services/Services/PrivateFileOperations.cs:17`), and removing it fails the build with `CS1061`. The using stays; only `ReportError` was removed.
- `EventType/Index.cshtml` also gained the hidden `page` input on the toggle form. The controller signature alone was not enough to satisfy the page-preserving acceptance criterion.
- `EventTypeController` gained a named `EventTypeIndex` route on `Index`, and the paged toggle branch uses `RedirectToRoute` so the Location is exactly `/EventType?page=N`. `RedirectToAction("Index", new { page })` produced `/EventType/Index?page=N`, which still worked but did not match the frozen matrix.
- Coverage added for the matrix rows that had none: `EventTypeManagementFlowTests` now asserts the paged toggle Location, and `MemberPagedListTests` asserts the `.numeric` and `.badge` markup.
- `VenueToggleAuditTests` pinned the old `<td>Active</td>` markup. Its two assertions now check `class="badge badge-active">Active</span>` and the inactive counterpart, matching the frozen matrix's badge requirement and the `AdminAccountsListTests` style. The status text is still asserted.
- `Member/Index.cshtml` renders `Active`/`Inactive` rather than the raw lowercase lookup code, matching the Event Type and Venue badges. `MemberPagedListTests` asserts the text case-insensitively, so it still passes.
- `Styles/app.css` and the rebuilt `wwwroot/css/app.css` changed: `.required`, `.form-panel`, `.form-actions`, `.panel + .panel`, the input border moved to `#64748b`, and the login card returned to Outfit. The earlier note claiming no CSS change was wrong and is corrected here.
- Follow-up after the rendered review: all three lists now keep the table and its headers when empty, with the message in a full-width row, per `docs/DESIGN.md:224`. Member distinguishes "no members yet" from "no match"; Event Type and Venue say what to do next. `AuthenticationFlowTests` and `VenuePagedListTests` were updated to the new copy and now also assert `data-table` is retained.

## Plan Change Log

- 2026-11-06 — **Scope renegotiated by the human after the rendered critique.** The original "Never" clause forbade result counts, required markers, form panels, an `h1` promotion, and `app.css` edits. The human approved the markup/CSS-only critique set, the DESIGN §6 screen names, and restoring Outfit on the login card. Amended: the Approach, Always, Never and I/O matrix rows; the Code Map and Verification lists; and the false "no CSS classes were added" note. Known-bad state avoided: a reviewer reading the plan would have believed the shipped changes were out of scope. KEEP: the dead-code removals, the `ReportError` dedupe, the actor rename, and the page-preserving toggle — none of those were renegotiated.

## Review Triage Log

- medium / patch — `MemberPagedListTests`' new `class="numeric"` assertion also matched `_Pager.cshtml:31`'s `<span class="numeric">`, so it would pass with the member cells reverted; tightened to `<td class="numeric">` and `<td><span class="badge badge-`. Fixed.
- medium / patch — `EventTypeManagementFlowTests` posted `page=2` itself and never pinned the view's hidden page input, so removing it would go unnoticed; added a `name="page" value="2"` assertion on the page-2 response. Fixed.
- low / patch — The plan scheduled only polish and no critique artifact existed, though `AGENTS.md` asks for critique and polish; the critique was run (dual-agent, detector `[]`) and its snapshot is now persisted at `.impeccable/critique/2026-11-06T07-54-11Z__asi-basecode-webapp-views.md`. Fixed.
- low / patch — `Venue/Edit.cshtml`'s current-photo block sat inside the `<form>` rather than above it; moved above the form. Fixed.
- low / patch — `EventType/Index.cshtml`'s Actions `<th>` lacked `class="actions"` while `AccountRequest/Index.cshtml:111` has it; added. Fixed.

## Design Notes

The three lists currently disagree on empty-state shape, action-cell class, and status treatment. `AccountRequest/Index.cshtml` is the newest and most complete Operate screen, so it is the reference, not a new design.

Deliberately out of scope and worth a follow-up ticket: `.data-table th:nth-child(3)` in `Styles/app.css:81` pins the third column, which is now Email on Member, Status on Event Type, and Location on Venue — the identity column still scrolls away. Fixing it changes shared CSS and another module's table, so it is not part of this sweep.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln --no-restore -warnaserror` — expected: 0 warnings, 0 errors.
- `dotnet test --project tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass, including `PagingTests` for the new `FirstItemNumber`/`LastItemNumber` members.
- With `ATTENDTA_TEST_SQL` set to a local SQL Server `master` connection: `dotnet test --project tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — expected: all pass, including `MemberPagedListTests`, `EventTypeManagementFlowTests`, `VenuePagedListTests`, `VenueToggleAuditTests`, `VenuePhotoFlowTests`, `AuthenticationFlowTests`, `StaticAssetTests`, and `MasterDataTransactionTests`.
- `npm --prefix ASI.Basecode.WebApp run css:build` then `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` — expected: no diff once the rebuilt sheet is committed.

**Manual checks:**
- Sign in as an Officer and open `/Member`, `/EventType`, `/Venue`; confirm badges, numeric alignment, small row actions, and that the Event Type toggle keeps its page.

## Code Review

### 2026-11-06

**Code review complete.** 0 decision-needed, 15 patch, 0 defer, 18 rejected.

- [x] [Review][Patch] The plan is stale and self-contradictory — frozen "Never" clause forbids the shipped result counts, required markers, form panels, `h1` promotion and `app.css` edits; Implementation Notes claim "no CSS classes were added"; I/O matrix rows are inverted; Code Map omits half the diff; Verification omits the edited test files. [story-refactor-sweep-plan.md]
- [x] [Review][Patch] Empty-state copy names buttons that no longer exist — "Add Event Type" / "Add Venue" vs the "New Event Type" / "New Venue" buttons. [ASI.Basecode.WebApp/Views/EventType/Index.cshtml:25, ASI.Basecode.WebApp/Views/Venue/Index.cshtml:29]
- [x] [Review][Patch] `.page-heading h1` shrinks `h1` on non-Master-Data pages — AccountRequest, MemberEvents/Index and MemberEvents/Details drop from 1.8rem to 1.5rem. [ASI.Basecode.WebApp/Styles/app.css:66]
- [x] [Review][Patch] `AssertReadOnlyProfile` now checks only the first profile panel, so a control added to Academic or Membership would pass. [tests/ASI.Basecode.IntegrationTests/AuthenticationFlowTests.cs:616]
- [x] [Review][Patch] Result-count arithmetic untested — no assertion for `FirstItemNumber`/`LastItemNumber` on first, last, or empty pages. [tests/ASI.Basecode.Services.Tests/PagingTests.cs]
- [x] [Review][Patch] Event Type empty state unpinned while the Member and Venue equivalents are pinned. [tests/ASI.Basecode.IntegrationTests/EventTypeManagementFlowTests.cs]
- [x] [Review][Patch] Venue Edit current-photo block unpinned. [tests/ASI.Basecode.IntegrationTests/VenuePhotoFlowTests.cs:165]
- [x] [Review][Patch] Event Type status badge unpinned while the Member and Venue badges are pinned. [tests/ASI.Basecode.IntegrationTests/EventTypeManagementFlowTests.cs]
- [x] [Review][Patch] Venue Edit renders a broken image when the stored photo is missing — the `<img>` is unconditional. [ASI.Basecode.WebApp/Views/Venue/Edit.cshtml:11]
- [x] [Review][Patch] Orphaned Plus Jakarta `@font-face` and asset remain after the login card stopped using them. [ASI.Basecode.WebApp/Styles/app.css:22]
- [x] [Review][Patch] `.required` reuses the error red, conflating "required" with "invalid". [ASI.Basecode.WebApp/Styles/app.css:60]
- [x] [Review][Patch] `aria-live` on a static result count announces nothing and repeats the `_Pager` defect. [ASI.Basecode.WebApp/Views/Shared/_ResultCount.cshtml:5]
- [x] [Review][Patch] Create/Edit headings still say "Add Event Type" / "Add Venue" while the lists say "New ...". [ASI.Basecode.WebApp/Views/EventType/Create.cshtml:6, ASI.Basecode.WebApp/Views/Venue/Create.cshtml:6]
- [x] [Review][Patch] The two critique snapshots contradict each other with no supersession note. [.impeccable/critique/2026-11-06T07-54-11Z__asi-basecode-webapp-views.md]
- [x] [Review][Patch] No coverage for `_ResultCount`, Profile panels, or required markers beyond the gaps above. [tests/ASI.Basecode.IntegrationTests]

#### Rejected

- R6 — false — the empty-state row already states the zero case and distinguishes no-records from no-match; a "Showing 0 of 0" line would duplicate it.
- R7 — false — `PagedResult` clamps `PageNumber` to `[1, TotalPages]` at construction, so the inverted range is unreachable; `PageSize` is the constant 25.
- R8 — false — the only other implementer, `AccountRequestQueueViewModel`, already declares both members.
- R9 — false — the page-preserving return sits after the `try/catch` and runs on both paths.
- R10 — false — `ToggleActive` catches `DomainException`, not bare `Exception`.
- R11 — false — `HandleDomainException` sets `TempData` and returns `View(model)` for the field-less case, identical to the deleted `ReportError`.
- R13 — low — table duplication is real but extracting a partial is larger than a direct correction.
- R17 — false — every marked field has `[Required]` or `[Range]`; optional fields are unmarked.
- R18 — low — pre-existing, and changing it alters error display app-wide.
- R19 — false — the assertions were tightened to the cell form and the hidden `page` input is asserted.
- R20 — low — the double toggle is self-contained and deterministic.
- R23 — low — the duplicated tail is a reviewer artifact; the committed files are correct.
- R26 — false — the DB link and `MemberRecord.UserId` remain; only an unused view-model property was removed.
- R28 — low — cosmetic field grouping.
- R29 — low — debatable taxonomy.
- R32 — low — the lookup code is stable; a shared constant would need exposing.
- R40 — low — the narrow reading of "Master Data" holds.
- R41 — low — intended behavior, verified in the browser.
