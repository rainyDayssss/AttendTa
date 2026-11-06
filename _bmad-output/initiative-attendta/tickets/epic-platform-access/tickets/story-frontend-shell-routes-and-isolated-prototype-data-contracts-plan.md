---
title: 'Frontend shell routes and isolated prototype data contracts'
type: 'feature'
ticket: '13'
created: '2026-11-06'
status: 'in-review'
baseline_revision: '5d4bfe9d9cf5621541dfd1e0ed0fff17cfafc383'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-attendta/ux-attendta/DESIGN.md'
  - '_bmad-output/initiative-attendta/delivery-queue.md'
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Eight demo screens do not exist: My Events with three tabs, the officer event dossier, the door scanner, the certificate ledger and viewer, the participation report, and the account and membership access dialogs. Without one shared shell, route and contract pass, every surface ticket would edit the same navigation, layout, global CSS and global JavaScript and collide on merge.

**Approach:** Add the routes, authorized actions, typed screen contracts and empty views for those screens; add the role-aware navigation links; create the My Events host with its three slot partials and the two access-action row slots; add the shared event selector; and split feature CSS/JS per surface behind one shell hook. No demo data and no business logic: each screen renders its real header and empty regions until its surface ticket fills them.

## Boundaries & Constraints

**Always:** Keep the Razor/Tailwind shell, `docs/DESIGN.md` tokens and existing page patterns. Authorize every new action with the existing `Member`, `Management` or `Administrator` policy so a wrong role is refused. Reuse an existing service where one already returns the data. Slot partials render nothing visible when empty and carry no placeholder copy. Contracts are viewmodels only.

**Never:** Add demo data, demo providers, `DemoData:*` keys or the startup guard (1.14). Add business rules, queries or mutations. Change existing screen behaviour, list columns, or the certificate template path `~/Views/Certificates/CertificateTemplate.cshtml`. Add automated tests or run per-ticket Impeccable critique/polish (8.15/8.12 own that).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Member opens My Events | Authenticated `member` | Host renders three tabs, Registrations active, slots empty | No error expected |
| Wrong role opens a screen | `officer` on My Events, or `member` on a management screen | Refused by policy | Redirect to AccessDenied |
| Dossier of a missing event | `/Event/{id}` with unknown id | No dossier rendered | `HandleDomainException` redirects to the event list with an error notice |
| Selector has no options | Empty `Options` | Selector renders nothing visible | No error expected |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Views/Shared/_Header.cshtml` -- duplicated mobile/desktop nav; extract links to `_NavLinks.cshtml` and add the new role links.
- `ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml` -- add the `ViewData["Feature"]` script hook after `site.js`; change nothing else.
- `ASI.Basecode.WebApp/Styles/app.css` -- add `@import "./features/*.css";` at the top; leave existing rules alone.
- `ASI.Basecode.WebApp/Controllers/EventController.cs` -- already `Management`; add `Details(int id)` at `Event/{id:int}`.
- `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs`, `MemberController.cs` -- add `Access(int id)` GET partials; leave existing actions untouched.
- `ASI.Basecode.Services/ServiceModels/` -- new contracts; follow `AccountSummaryViewModel.cs` style.
- `ASI.Basecode.WebApp/Views/AdminAccounts/Index.cshtml`, `Views/Member/Index.cshtml` -- append the access slot inside the existing actions cell.
- `tests/ASI.Basecode.Services.Tests/RegistrationReviewTests.cs` -- baseline repair committed at `5d4bfe9` (member fake signatures).

## Tasks & Acceptance

**Execution:**
- [ ] `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs` -- host, three tab models, card, attendance row, certificate row, pass -- one type per surface so 3.17/4.10/4.12/6.13 edit different files.
- [ ] `ASI.Basecode.Services/ServiceModels/EventSelectorViewModel.cs` -- selector plus option record with a `HasOptions` guard -- shared by scanner, ledger and report.
- [ ] `ASI.Basecode.Services/ServiceModels/EventDossierViewModel.cs`, `CheckInScreenViewModel.cs`, `CertificateLedgerViewModels.cs`, `ParticipationReportViewModel.cs`, `AccessDialogViewModels.cs` -- remaining contracts with `Empty` factories -- surface tickets fill only their own.
- [ ] `ASI.Basecode.WebApp/Controllers/MyEventsController.cs` -- new `Member` controller at `MyEvents` with `Index` (redirect), `Registrations`, `Attendance`, `Certificates` -- one action per tab isolates model loading.
- [ ] `ASI.Basecode.WebApp/Controllers/CheckInController.cs`, `CertificateLedgerController.cs`, `ReportsController.cs` -- new `Management` controllers at `CheckIn`, `Certificates`, `Reports/Participation` returning empty contracts.
- [ ] `ASI.Basecode.WebApp/Controllers/EventController.cs` -- `Details` loads `GetEventAsync` into the dossier with an empty roster -- real header, empty roster slot.
- [ ] `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs`, `MemberController.cs` -- `Access` GET partial actions -- 5.11 fills only these bodies.
- [ ] `ASI.Basecode.WebApp/Views/MyEvents/Index.cshtml`, `_RegistrationsTab.cshtml`, `_PassPartial.cshtml`, `_AttendanceHistoryPartial.cshtml`, `_CertificateLinkPartial.cshtml` -- host with tab nav and three empty slots; the registrations loop already calls `_PassPartial`.
- [ ] `ASI.Basecode.WebApp/Views/Event/Details.cshtml`, `CheckIn/Index.cshtml`, `CertificateLedger/Index.cshtml`, `CertificateLedger/Viewer.cshtml`, `Reports/Participation.cshtml` -- empty views with real headings and stable empty regions.
- [ ] `ASI.Basecode.WebApp/Views/Shared/_EventSelector.cshtml` -- labelled GET selector rendered only when options exist -- invisible until a surface supplies options.
- [ ] `ASI.Basecode.WebApp/Views/AdminAccounts/_AccessActions.cshtml`, `Views/Member/_AccessActions.cshtml`, `Views/AdminAccounts/_AccessDialog.cshtml`, `Views/Member/_AccessDialog.cshtml` -- empty slot and dialog partials -- 5.11 fills them; 2.13 leaves the slot alone.
- [ ] `ASI.Basecode.WebApp/Views/Shared/_NavLinks.cshtml`, `_Header.cshtml` -- shared role-aware links including My Events, Check-in, Certificates and Reports.
- [ ] `ASI.Basecode.WebApp/Styles/features/*.css`, `Styles/app.css` -- per-surface stylesheets imported once -- later tickets edit only their own file.
- [ ] `ASI.Basecode.WebApp/wwwroot/js/features/scanner.js`, `_Layout.cshtml` -- feature script folder plus the `ViewData["Feature"]` hook -- later tickets add a file without touching the shell.
- [ ] `ASI.Basecode.WebApp/Views/AdminAccounts/Index.cshtml`, `Views/Member/Index.cshtml` -- append the access slot to each row.

**Acceptance Criteria:**
- Given an authenticated account, when it opens every link in its role's navigation, then each resolves to a 200 screen, never a 404 or error page.
- Given a role that is not permitted, when it requests a new screen, then policy refuses it and no screen content is returned.
- Given a member on My Events, when each tab opens, then the tab is selected, its slot renders nothing visible and no placeholder copy appears.
- Given the Accounts and Members lists, when they render, then each row contains the empty access slot and existing columns and actions are unchanged.
- Given the repository, when the build, both test projects, `css:build` and `test:js` run, then all pass and the regenerated `wwwroot/css/app.css` is committed.

## Design Notes

- **Contracts in `Services/ServiceModels`.** AGENTS.md puts viewmodels there, and the integration phase replaces each demo provider with "the real service behind the same contract", so a service must return the same type. Only POCOs are added.
- **One action per My Events tab.** The delivery queue says each surface "changes only their own action's model loading"; a single `Index(tab)` would make 3.17, 4.12 and 6.13 edit one method.
- **Feature asset seam.** `Styles/features/<surface>.css` is imported once; `_Layout` loads `~/js/features/<surface>.js` when a view sets `ViewData["Feature"]`. A surface adds its file and sets one key.
- **Selector stays empty in 1.13.** Each surface needs a different status filter, so baking one in now would be wrong for two of the three.
- **Demo-data look deferred.** 1.13 adds no demo data, so its screens render identically with `DemoData__Enabled` on or off; the switch does not exist until 1.14 merges. The real-database look is this ticket's smoke, and 8.15 repeats it with demo data.

## Implementation Notes

- Baseline repair (separate commit `5d4bfe9`): `RegistrationReviewTests.FakeMembers` did not implement the `status` parameters PR #134 added to `IMemberRepository`; `origin/dev` did not compile until this was fixed.
- Contracts are POCOs in `Services/ServiceModels` with `Empty`/`FromEvent` factories; no service, interface, repository or schema change.
- `MyEventsController` uses one action per tab (`Registrations`, `Attendance`, `Certificates`); `Index` redirects to `Registrations`.
- `EventController.Details` loads `GetEventAsync` (returns null for a missing id) and routes a missing id through `HandleDomainException` to the event list.
- Deviation: Tailwind 4.3.4 does not resolve glob or directory `@import`s, so `Styles/app.css` lists the ten feature files explicitly. The seam holds: a surface edits only its own file; a brand-new file needs one import line.
- `_Layout` loads `~/js/features/<name>.js` when a view sets `ViewData["Feature"]`; `CheckIn/Index.cshtml` sets `scanner`.
- Copy fix after reading the diff: the member registration card action reads "View event" rather than "View dossier".
- Verification evidence (2026-11-06): `dotnet build` 0 warnings/0 errors; Services.Tests 271/271; IntegrationTests 312 total, 308 pass, 4 fail — exactly the `CertificateTracerFlowTests` PDF-inspection cases that need Poppler, identical to the pre-change baseline; `test:js` 1/1; `css:build` idempotent with no drift after staging.
- Manual smoke against `AttendTaMVP_Test` with temporary local admin/officer/member rows and one event: all 28 rendered navigation links per role resolve to 200; admin/officer on `/MyEvents` and member on every management route redirect to `AccessDenied`; `/Event/1` returns 200 with the real header and an empty roster; `/Event/999999` redirects to `/Event/Index`; the selector renders no markup on scanner, ledger and report; My Events shows the three tabs with no placeholder copy; Accounts and Members rows contain no access-dialog markup. The smoke rows were added to the disposable local database only.
- Matrix rows are covered by the manual smoke above; the ticket explicitly forbids new automated tests, so no automated covering test was added.
- Review patches: removed the My Events tab count badges (inactive-tab counts were structurally wrong), added `EventSelectorViewModel.HiddenValues` so the ledger keeps `tab` across a selector submit and passed `eventId` on the ledger tab links, and added a `Dossier` link to each event row so the new dossier route is reachable.
- Upstream merge (2026-11-07): `upstream/dev` brought 1.14 (demo data seam, M-01/M-04/M-09) and 8.14. The only conflict was the generated `wwwroot/css/app.css`, resolved by regenerating from the merged `Styles/app.css`; `Styles/app.css` auto-merged with both the feature imports and 1.14's classes. `EventController.Details` now passes the real read through `IEventStateOverlay.Apply` so the dossier header shows the same demo-completed status and check-in flag as the event list (register rule 7).

## Review Triage Log

Pass 1 (quick lens, 2026-11-07):

| Finding | Verdict | Route | Evidence / action |
|---|---|---|---|
| My Events tab counts are always 0 for the two inactive tabs because each action builds only its own tab model | medium | patch | Verified: `MyEventsViewModel.Empty(tab)` leaves the other tab models empty, so the host's count badges misreport. Removed the count badges from the tab links; the ticket requires tabs, not counts, and 3.17 can add counts when it loads them. |
| The shared event selector cannot carry the ledger's `tab` value, and the ledger tab links drop `eventId` | medium | patch | Verified: the selector rendered one field and the tab links passed only `tab`. Added `EventSelectorViewModel.HiddenValues`, rendered them as hidden inputs, set `tab` from the ledger action, and passed `eventId` on both tab links. |
| The dossier route has no entry point; `Event/Index` links only Cover, Review, Edit, Cancel and Publish | medium | patch | Verified by reading `Views/Event/Index.cshtml`. Added a `Dossier` link to `Details` in each event row so the screen is reachable without typing a URL. |

All three were patched and re-verified: build 0 warnings, dossier link present and `/Event/1` returns 200, ledger `tab=issued` keeps its selected tab, My Events tabs render without count badges.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` -- expected: 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` -- expected: all pass (baseline 271).
- `$env:ATTENDTA_TEST_SQL='Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True'; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` -- expected: 308 pass; the 4 `CertificateTracerFlowTests` PDF-inspection cases need Poppler (`pdftotext`/`pdfinfo`), which CI installs, so install Poppler locally or record them as environment-only failures.
- `npm --prefix ASI.Basecode.WebApp run css:build` then `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` -- expected: no drift after the regenerated file is committed.
- `npm --prefix ASI.Basecode.WebApp run test:js` -- expected: 1 test passes.

**Manual checks (if no CLI):**
- Run against `AttendTaMVP_Test`, sign in as each role, open every navigation link for that role; expect 200 and the right screen, and a policy refusal for a wrong role.
- Open My Events, switch all three tabs, then Accounts and Members; expect empty slots, no placeholder copy and unchanged columns.
- Check the changed screens at 390px and 1440px with keyboard focus; expect no horizontal overflow.

