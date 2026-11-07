---
title: 'Member attendance history in its slot'
type: 'feature'
ticket: '12'
created: '2026-11-07'
status: 'built'
baseline_revision: 'aaedda83a819ce44b6596d108c8904a47c47e84b'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
  - 'docs/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The My Events Attendance tab built by 1.13 renders `_AttendanceHistoryPartial` from an always-empty `MyEventsAttendanceTabViewModel`, so a member cannot see their own attendance. No owner-scoped attendance query exists yet; 4.7 builds it.

**Approach:** Fill the slot from demo register row M-08. Add `GetAttendanceAsync` to `IMyEventsService`, implement it in the demo provider by composing the actor's registrations, `DemoStore.TryGetAttendance` and one event page, and wire `MyEventsController.Attendance` to it. The partial, host model and the other two slots stay unchanged.

## Boundaries & Constraints

**Always:** Resolve the actor only from `ICurrentActor.UserId` (the member policy already guards the route). Reuse `ManilaTimeFormatter`, `PageRequest`/`PagedResult`/`MyEventsTabPage`, `_ResultCount` and `_Pager`. Follow the 1.14 demo seam: contract in `Services`, provider in `WebApp/DemoData`, a switch-off provider when the seam is off. Read-only history: no check-in, correction, edit or delete control. Nothing on screen may say the data is demo data. Record M-08's detail in `demo-mock-register.md` in this PR.

**Never:** No repository query, schema change, migration, or SQL/file/Activity-Log write. No new route. Do not edit `_AttendanceHistoryPartial.cshtml`, `_PassPartial.cshtml`, `_CertificateLinkPartial.cshtml`, `_RegistrationsTab.cshtml`, the `MyEventsViewModel` host class, `_Pager`, `_ResultCount`, or `DemoStore`'s write semantics. No new automated tests and no per-ticket Impeccable critique or polish (demo phase).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid check-in | Seeded completed event, `DemoData:Enabled=true` | Row shows event title, Manila recorded time, method label, `Valid` badge | No error expected |
| Invalidated check-in | Last seeded check-in invalidated | Same row with an `Invalidated` badge and the reason | No error expected |
| No attendance | Member with none | Slot renders nothing; host shows "No attendance to show yet." | No error expected |
| More than 25 records | 26+ demo attendance rows | Page 1 shows 25, pager links page 2 on `/MyEvents/Attendance` | Out-of-range page clamps to the last page |
| Demo switch off | `DemoData:Enabled=false` | Empty page and empty state | Empty state |
| Another member's records | A different signed-in member | Only that actor's rows; ownership through `members.user_id` | No error expected |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Interfaces/IMyEventsService.cs` -- add `Task<PagedResult<MyEventAttendanceRowViewModel>> GetAttendanceAsync(int? pageNumber, CancellationToken cancellationToken = default)`; 4.7 implements the real owner-scoped query behind it.
- `ASI.Basecode.Services/ServiceModels/CheckInMethodDisplay.cs` -- new static label map for `CheckInMethodCode` (`QrCode` → "QR code", `ManualSearch` → "Manual search", `StudentNumber` → "Student number", default "Check-in"), mirroring `RegistrationStatusDisplay`. No badge class: the method is not a status.
- `ASI.Basecode.WebApp/DemoData/DemoMyEventsService.cs` -- implement `GetAttendanceAsync`: `userId = _actor.UserId`; `IRegistrationRepository.DiscoverOpenEventIdsForUserAsync` (it returns every non-cancelled registration's event id, so completed events are included); per event `GetMemberRegistrationAsync` → `DemoStore.TryGetAttendance(registrationId)`; one `IEventRepository.CountAsync` + `GetPageAsync(0, total)` for titles; order `RecordedAt` desc then `RegistrationId` desc; page in memory with `PageRequest`/`PagedResult`. Set `AttendanceId = RegistrationId` because the store keys one attendance per registration.
- `ASI.Basecode.WebApp/DemoData/EmptyMyEventsService.cs` -- return an empty `PagedResult<MyEventAttendanceRowViewModel>`.
- `ASI.Basecode.WebApp/Controllers/MyEventsController.cs` -- `Attendance(int? page, CancellationToken)` becomes async and returns `MyEventsViewModel.FromAttendance(await _myEvents.GetAttendanceAsync(page, cancellationToken))`; drop the "until 4.12" clause from the comment.
- `_bmad-output/initiative-attendta/demo-mock-register.md` -- add an "M-08 — demo seam detail (4.12)" section (contract, provider, switch-off provider, what it composes); the M-08 row stays `open`.
- Do not touch: `Views/MyEvents/_AttendanceHistoryPartial.cshtml` (already renders Event / Recorded / Method / State with `ManilaTimeFormatter` and the valid and invalidated badges), `MyEventsViewModels.cs`, `DemoDataRegistration.cs` (DI already resolves `IMyEventsService`), `DemoStore.cs`.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Services/Interfaces/IMyEventsService.cs` -- add the attendance read contract -- 4.7 replaces the implementation, not the shape.
- [x] `ASI.Basecode.Services/ServiceModels/CheckInMethodDisplay.cs` -- one label rule for the method column -- the real query reuses it.
- [x] `ASI.Basecode.WebApp/DemoData/DemoMyEventsService.cs` -- compose the M-08 provider -- actor-scoped, read-only, paged at 25.
- [x] `ASI.Basecode.WebApp/DemoData/EmptyMyEventsService.cs` -- switch-off empty page -- the tab keeps working with the seam off.
- [x] `ASI.Basecode.WebApp/Controllers/MyEventsController.cs` -- load the Attendance tab -- one action per tab stays isolated.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` -- record the M-08 detail -- register rule 11.

**Acceptance Criteria:**
- Given a member with a seeded valid check-in, when they open My Attendance, then they see that event, its recorded time labelled Asia/Manila, the method label and a `Valid` badge.
- Given an invalidated check-in, when the tab renders, then the row shows an `Invalidated` badge and the reason, and no correction control exists.
- Given more than 25 records, when the member pages, then only the Attendance tab's `page` changes and the Registrations tab keeps its own page.
- Given no attendance, when the tab opens, then the empty state shows and no pager renders.
- Given `DemoData:Enabled=false`, when the tab opens, then it renders the empty state with no error.
- Given any rendered state, then no text names demo data.

## Implementation Notes

- `IMyEventsService` now carries both reads; `DemoMyEventsService` and `EmptyMyEventsService` implement the new member, so DI needs no change. `DemoMyEventsService` gained a `DemoStore` dependency (singleton, registered before the surface file); the switch-off provider does not touch the store, so `DemoData:Enabled=false` still resolves.
- `GetAttendanceAsync` composes the same reads as the registrations provider: `DiscoverOpenEventIdsForUserAsync` (non-cancelled, so completed events are included), `GetMemberRegistrationAsync` per event, `IEventRepository.CountAsync` + `GetPageAsync(0, total)` for titles, then `DemoStore.TryGetAttendance`. Rows order by `RecordedAt` desc then `AttendanceId` desc and page in memory at 25.
- `AttendanceId` carries the registration id because the store keys one attendance per registration; 4.7 returns the real `attendance_id` behind the same contract.
- Verification: `dotnet build ASI.Basecode.sln` clean (0 warnings, 0 errors); 271 service tests pass; `npm run css:build` produced no `app.css` drift (`git diff --exit-code` = 0 — the CSS build rewrites the file with LF while the index holds CRLF, so compare with `git diff`, not `git status`, and restore the file afterwards).
- Matrix audit: a temporary integration check (deleted before hand-off, because the frozen intent forbids new tests) ran the real pipeline with the demo provider against a disposable LocalDB `AttendTa_IT_<guid>` and covered every matrix row: 26 records paged 25 + 1 with the pager pointing at `/MyEvents/Attendance?page=2`, a valid row with `badge-active`/"Valid"/"QR code"/"Asia/Manila", the invalidated row with `badge-inactive`/"Invalidated" and its reason and no correction control, `?page=999` clamped to the last page, another member seeing only their own row, and the switch-off factory showing "No attendance to show yet." with no pager. No rendered text contained "demo". Both temporary tests passed (2/2). Recorded as a deliberate deviation from the test-per-row rule, not an omission.
- Known limit carried from the plan: the provider still makes one `GetMemberRegistrationAsync` call per registered event (same pattern as the registrations read); 4.7's real owner-scoped query removes it.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens, 2026-11-07) — verdicts: high 0 / medium 0 / low 2 / false 0 / maybe-false 0.

| Finding | Verdict | Route | Evidence / action |
|---|---|---|---|
| `DemoSeedService.cs:22,131` with `DemoMyEventsService.cs:104-168` and `_Pager.cshtml:28` — the "More than 25 records" row and the independent-paging criterion cannot be reached with demo data, so the pager is not demonstrable | low | defer | Verified: `MaxSeededCheckIns = 8` seeds the single completed event only, and a member has one registration per event, so a member's history holds one row and `_Pager` stays hidden. The product behavior is correct and was proven for 26 rows by the temporary check; the limit is the pre-existing 1.14 seed, not this change. Recorded in `deferred-work.md` for 4.11/4.7. |
| `DemoSeedService.cs:98` — the "Demo data active (M-01, M-02, M-04, M-09)" log line omits M-08 although the M-08 provider is now active and this service writes the attendance it reads | low | patch | Verified: register rule 3 allows the startup log to note active register ids, and 3.17 added M-02 to this same line. Patched to `(M-01, M-02, M-04, M-08, M-09)`; build and 271 service tests re-ran green. |

Pass 2 (quick lens, 2026-11-07) — verdicts: high 0 / medium 1 / low 1 / false 1 / maybe-false 0.

| Finding | Verdict | Route | Evidence / action |
|---|---|---|---|
| `MyEventsController.cs:46` — the comment "The certificate tab keeps its empty model until 6.13 fills it." sits above the Attendance action, which no longer has an empty model, while the Certificates action it describes has none | low | patch | Verified at lines 46-55: the comment is attached to `Attendance`. Moved directly above `Certificates`; build and 271 service tests re-ran green. |
| `DemoMyEventsService.cs:104-169` with `EmptyMyEventsService.cs:23-29` and the registration file — M-08 has no provider class or registration line of its own, so 3.13's M-02 removal deletes the class that also serves M-08 before 4.7 runs, and 4.7 then has nothing to delete | medium | patch | Verified: 3.13 and 4.7 both instruct "delete the provider classes and their registration", and 4.7's `after` includes `3.13`. The code is correct for the demo phase; the smallest fix is the register hand-over note (rule 12) telling 3.13 to keep `GetAttendanceAsync` as an empty page and 4.7 to replace that method. Added to the M-08 section. |
| `DemoSeedService.cs:22,131` with `DemoMyEventsService.cs:104-169` and `_Pager.cshtml:28` — the "More than 25 records" criterion and the independent-paging verify line cannot be reached on the delivered surface | low | defer | carried — same claim and location as the Pass 1 row; already recorded in `deferred-work.md` and not re-deferred. |
| Plan frontmatter `status`/`baseline_revision` are uncommitted, and the baseline move has no Plan Change Log entry | false | — | The uncommitted state is the expected mid-workflow state: step-04 rewrites the frontmatter and step-05 commits it. The baseline move is content-safe (the only delta between `426e926` and `aaedda8` is the fork-only `docsss/**` and `docs/bmad-source/AGENTS.md` trees), and `## Plan Change Log` records bad_plan loopbacks, not metadata edits. |



## Design Notes

- **Why a demo provider.** No owner-scoped attendance query exists; M-08 is the register row 4.7 removes. Keeping the contract in `Services` lets 4.7 swap the provider without touching the controller or the view.
- **Why `AttendanceId = RegistrationId`.** `DemoStore` keys attendance by registration id (one record per registration), so the registration id is the stable row identity until 4.7 returns the real `attendance_id`.
- **Why the partial is untouched.** 1.13 already renders the ledger with Manila times and the valid/invalidated badges; 4.12's gap is the data path only.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` -- expected: 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` -- expected: all pass (no new tests).
- `npm --prefix ASI.Basecode.WebApp run css:build` then `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` -- expected: no drift (no view or style change).

**Manual checks (if no CLI):**
- With `DemoData__Enabled=true`, `DemoData__AllowDatabase=AttendTaMVP_Test` and `DemoData__TokenKey` set, sign in as a member with a check-in on the completed event: `/MyEvents/Attendance` lists the record with the Manila time, the invalidated record is marked with its reason, the pager is independent of Registrations, and no page text mentions demo data.
- Check the tab at 390px and 1440px with keyboard focus; expect no horizontal overflow and a reachable table scroll region.
