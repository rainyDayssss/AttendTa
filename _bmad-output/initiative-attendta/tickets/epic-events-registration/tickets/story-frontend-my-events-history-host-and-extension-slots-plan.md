---
title: 'My Events host content and the Registrations tab'
type: 'feature'
ticket: '17'
created: '2026-11-07'
status: 'built'
baseline_revision: '39f95d1d0fe0b06aed4731010301ff5960e6a87a'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
  - 'docs/design.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The My Events host created by 1.13 renders three empty tabs, so a member cannot see their registrations, attendance or certificates. No cross-event owner-scoped registration read exists yet (3.13 builds it), so the demo must compose existing per-event reads.

**Approach:** Fill the Registrations tab from demo register row M-02: a demo `IMyEventsService` that composes `IRegistrationRepository.DiscoverOpenEventIdsForUserAsync` and `GetMemberRegistrationAsync` with `IEventRepository.GetPageAsync`, presents status through `RegistrationStatusDisplay` and pages at 25. The host gains per-tab paging, empty states and a guarded "View event" link. The attendance and certificate slot partials stay untouched.

## Boundaries & Constraints

**Always:** Keep the 1.13 host, routes, card contract and the three slot partials (`_PassPartial`, `_AttendanceHistoryPartial`, `_CertificateLinkPartial`) unchanged in behaviour. Reuse `RegistrationStatusDisplay`, `ManilaTimeFormatter`, `PageRequest`/`PagedResult`/`_Pager`/`_ResultCount`. Follow the 1.14 demo seam: contract in `Services/Interfaces`, demo provider in `WebApp/DemoData`, one surface registration file, and a switch-off provider registered when `DemoData:Enabled` is false. Nothing on screen may say the data is demo data. Record M-02 in `demo-mock-register.md` in this PR.

**Never:** Add a repository query, schema change, migration or SQL write. Do not implement 4.12/6.13 attendance or certificate data. Do not add tab counts (inactive tabs are not loaded). Do not edit the three slot partials. No new automated tests and no per-ticket Impeccable critique/polish.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Member with registrations | `DemoData:Enabled=true`, rows in SQL | Cards newest start first, 25 per page, status label + badge, pager | No error expected |
| Pending/Waitlisted after start | Event `StartsAt <= now` | Label "Not confirmed before cutoff", `badge-closed` | No error expected |
| No registrations | Member with none | Empty state with a link to the Events feed | No error expected |
| Attendance/Certificates tab | No data yet | Slot partial renders nothing; host shows its empty state | No error expected |
| Event not published | Registration on a non-published event | Card renders without the "View event" link | No error expected |
| Demo switch off | `DemoData:Enabled=false` | Empty registrations page, no error | Empty state |
| Page out of range | `?page=99` | Clamped to the last page | No error expected |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Interfaces/IMyEventsService.cs` -- new contract `GetRegistrationsAsync(int? pageNumber, CancellationToken)` returning `PagedResult<MyEventRegistrationCardViewModel>`; 3.13 implements the real query behind it.
- `ASI.Basecode.WebApp/DemoData/DemoMyEventsService.cs` -- demo M-02 provider. Actor from `ICurrentActor.UserId`. Compose `IRegistrationRepository.DiscoverOpenEventIdsForUserAsync`, `GetMemberRegistrationAsync`, and `IEventRepository.CountAsync` + `GetPageAsync(0, total)` (dictionary by event id for `Title`, `TypeName`, `VenueName`, `StartsAt`, `EventStatusId`). `startCutoffPassed = now >= StartsAt` from `TimeProvider`. Order by `StartsAt` desc then `RegistrationId` desc; page in memory with `PageRequest`/`PagedResult`.
- `ASI.Basecode.WebApp/DemoData/EmptyMyEventsService.cs` -- switch-off provider returning an empty `PagedResult`.
- `ASI.Basecode.WebApp/DemoData/DemoMyEventsRegistration.cs` -- `AddDemoMyEvents(IServiceCollection)`; called from `DemoDataRegistration.AddDemoData`.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` -- register `EmptyMyEventsService` in the disabled branch, call the new surface file in the enabled branch, and add M-02 to `DemoSeedService`'s active log line.
- `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs` -- add a shared `MyEventsTabPage : IPagedResult` base (page number, total count, computed page metadata); derive the three tab models from it; add `From(PagedResult<T>)` factories; add `bool CanViewEvent` to the card; add `MyEventsViewModel.FromRegistrations/FromAttendance/FromCertificates` factories (properties stay `init`).
- `ASI.Basecode.WebApp/Controllers/MyEventsController.cs` -- inject `IMyEventsService`; `Registrations(int? page, CancellationToken)` loads the tab; `Attendance`/`Certificates` accept `int? page` and keep returning empty tab models.
- `ASI.Basecode.WebApp/Views/MyEvents/Index.cshtml` -- after the active partial render the tab's empty state, `_ResultCount` and `_Pager`; keep the tab nav and `aria-current`.
- `ASI.Basecode.WebApp/Views/MyEvents/_RegistrationsTab.cshtml` -- render "View event" only when `card.CanViewEvent`.
- `ASI.Basecode.WebApp/Styles/features/my-events.css` -- spacing for tab content, empty state and pager inside the panel.
- `_bmad-output/initiative-attendta/demo-mock-register.md` -- M-02 row: added by 3.17, classes named, state `open`.
- Do not touch: `Views/MyEvents/_PassPartial.cshtml`, `_AttendanceHistoryPartial.cshtml`, `_CertificateLinkPartial.cshtml`, `Views/Shared/_Pager.cshtml`, `_ResultCount.cshtml`, `Views/Shared/_NavLinks.cshtml` (My Events is already linked).

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Services/Interfaces/IMyEventsService.cs` -- add the contract -- the real 3.13 query will implement it.
- [x] `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs` -- paged tab base, factories, `CanViewEvent` -- host and demo provider share one shape.
- [x] `ASI.Basecode.WebApp/DemoData/DemoMyEventsService.cs`, `EmptyMyEventsService.cs`, `DemoMyEventsRegistration.cs`, `DemoDataRegistration.cs` -- demo M-02 provider and registration -- switch-off keeps the empty page.
- [x] `ASI.Basecode.WebApp/Controllers/MyEventsController.cs` -- load the Registrations tab and pass `page` on every tab -- one action per tab stays isolated.
- [x] `ASI.Basecode.WebApp/Views/MyEvents/Index.cshtml`, `_RegistrationsTab.cshtml` -- empty states, result count, pager, guarded link -- no dead ends.
- [x] `ASI.Basecode.WebApp/Styles/features/my-events.css` -- panel spacing -- keep DESIGN.md density.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` -- record M-02 -- register rule 11.

**Acceptance Criteria:**
- Given a member with registrations, when they open My Events, then cards show event, type, venue, Manila start time and the registration status label/badge, newest start first.
- Given more than 25 registrations, when the member pages, then each tab keeps its own `page` value and the pager shows the correct range.
- Given a Pending or Waitlisted registration on a started event, when the card renders, then it reads "Not confirmed before cutoff".
- Given no registrations, when the Registrations tab opens, then an empty state with a link to Events appears.
- Given `DemoData:Enabled=false`, when My Events opens, then it renders the empty state and no error.
- Given a registration on a non-published event, when the card renders, then no "View event" link appears.

## Implementation Notes

- `MyEventsTabPage` owns the paging metadata for all three tabs, so the host renders one `_ResultCount` and one `_Pager` from whichever tab is active. `Empty()` reports page 1 of 0 pages, which keeps `_Pager` hidden; the `From(PagedResult<T>)` factories clamp an out-of-range page to the last page.
- `DemoMyEventsService` composes the reads named in the plan and never writes. `CanViewEvent` mirrors `MemberEvents/Details`, which only serves a published event, so a draft or cancelled event renders without the link.
- The demo service reads the actor only from `ICurrentActor.UserId`; the member policy guards the route. Status text and badge class come from `RegistrationStatusDisplay`, and `startCutoffPassed` is `now >= StartsAt` from `TimeProvider`, matching `ParticipationFacts.StartCutoffPassed`.
- Verification: `dotnet build ASI.Basecode.sln` clean (0 warnings, 0 errors); `npm run css:build` regenerated `app.css` with only the new `.my-events-panel` rules added; `npm run test:js` passes; 271 service tests pass.
- A temporary end-to-end check (deleted before hand-off, because the ticket forbids new tests) ran the real pipeline with `DemoData:Enabled=true` against a disposable LocalDB `AttendTa_IT_<guid>`: 25 cards on page 1 and 1 on page 2, newest start first, "Not confirmed before cutoff" with `badge-closed`, no "View event" on a draft-event card, the empty state with the Events link for a member with none, and the switch-off provider's empty page. No rendered text named demo data.
- Known limit carried from the plan: `DiscoverOpenEventIdsForUserAsync` excludes Cancelled rows, so a cancelled registration is absent until 3.13's real query.
- Matrix audit: the cutoff row is covered by `RegistrationStatusDisplayTests` and the out-of-range row by `PagingTests`; both ran green. The remaining rows have no persistent automated test because the frozen intent forbids new tests, so they are covered by the temporary end-to-end check above and the manual smoke. Recorded as a deliberate deviation, not an omission.
- Runner note: `dotnet test` on the Services test project reports "Zero tests ran" (MTP exit 5) in this environment; running the built `ASI.Basecode.Services.Tests.exe` directly reports Total: 271, Failed: 0.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens, 2026-11-07) — verdicts: high 0 / medium 0 / low 3 / false 0 / maybe-false 0.

| Finding | Verdict | Route | Evidence / action |
|---|---|---|---|
| `MyEventsViewModels.cs:51-74` — `MyEventsTabPage` duplicates `PagedResult<T>` metadata and its clamp is dead | low | reject | Verified: the duplication is real, but the protected constructor is only reached from `From(PagedResult<T>)`, which already passes a clamped `PageNumber`, so no user or developer meets a wrong page today. The fix is a delegation refactor, not a direct correction. |
| `Views/MyEvents/Index.cshtml:18-23,56-65` — empty-state messages assert "no records" for states the page never checked (cancelled-only member; attendance/certificate tabs with no data source) | low | patch | Verified: `DiscoverOpenEventIdsForUserAsync` excludes Cancelled and the Attendance/Certificates actions load nothing, so the copy can be false. Smallest fix is wording that describes what the page shows; the empty state, the Events link and the approved matrix stay. |
| `DemoMyEventsService.cs:45-65` — the 25-row page bounds only the in-memory slice; one full event read plus one read per registered event | low | reject | Verified and already prescribed by the plan's Code Map. Removing it needs a new cross-event repository query, which the frozen intent forbids; the provider is demo-only and 3.13 replaces it. |

## Design Notes

- **Why a demo provider and not a real service.** No cross-event owner read exists; M-02 is the register row 3.13 removes. Keeping the contract in `Services` lets 3.13 swap the provider without touching the controller or views.
- **Why compose `GetPageAsync`.** One read supplies title, type, venue, start and status for every registered event, matching `DemoSeedService`'s existing composition and avoiding per-row lookup repositories.
- **Known limit (documented, not hidden).** `DiscoverOpenEventIdsForUserAsync` excludes Cancelled rows, so a cancelled registration disappears until 3.13's real query; this ticket's Verify line does not require Cancelled.
- **Why `CanViewEvent`.** `MemberEvents/Details` only serves published events, so linking a completed or cancelled event would 404.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` -- expected: 0 warnings, 0 errors.
- `npm --prefix ASI.Basecode.WebApp run css:build` then `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` -- expected: no drift after the regenerated file is committed.
- `npm --prefix ASI.Basecode.WebApp run test:js` -- expected: passes.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` -- expected: all pass (no new tests).

**Manual checks (if no CLI):**
- With `DemoData__Enabled=true`, `DemoData__AllowDatabase=AttendTaMVP_Test` and `DemoData__TokenKey` set, sign in as a member: `/MyEvents` redirects to Registrations; a just-made registration appears; all three tabs switch; a member with none sees the empty state; no page text mentions demo data.
- Check the page at 390px and 1440px with keyboard focus; expect no horizontal overflow and reachable tabs and pager.
