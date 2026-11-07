---
title: 'My Events personal registrations list'
type: 'feature'
ticket: '13'
created: '2026-11-07'
status: 'built'
baseline_revision: '13f56864b0f53e3cb620828b013944b4cb6a09ec'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
  - '_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** My Events' Registrations tab still reads demo row M-02 from a WebApp provider that composes per-event reads; it cannot show Cancelled rows and has no authoritative cross-event owner query, so the status and "Not confirmed before cutoff" labels rest on demo composition rather than one real read.

**Approach:** Add one owner-scoped, SQL-paged cross-event registration read behind `IMyEventsService`, joining registrations to members, events, event types and venues; resolve labels with `RegistrationStatusDisplay` and the canonical cutoff fact; register the real service; delete the M-02 demo classes; keep `GetAttendanceAsync` as an empty page for M-08 until 4.7.

## Boundaries & Constraints

**Always:** The actor comes only from `ICurrentActor.UserId`; ownership is resolved through `members.user_id` inside SQL; the member policy guards the route. Count and page after the owner filter, fixed 25-row page, ordering `starts_at DESC, registration_id DESC`. Labels come from `RegistrationStatusDisplay`; the cutoff flag from `ParticipationFacts.StartCutoffPassed` against the injected `TimeProvider`. Include every stored status, including Cancelled. `CanViewEvent` is true only for a Published event. Read-only: no transaction, write or audit.

**Never:** Add a second controller, view, card model or partial; edit `MyEventsController`, the three My Events partials, `Index.cshtml`, `_RegistrationsTab.cshtml` or any CSS. No schema, migration or SQL write. Do not implement attendance or certificate data (M-08 stays an empty page; 4.7 replaces it). Do not leave demo classes, registration lines or register ids referencing M-02. No per-ticket Impeccable critique or polish.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Own rows only | Actor and another member each have registrations | Only the actor's registrations render; the other member's registration id never appears | No error expected |
| Paging | Actor has 26 registrations | Page 1 shows 25, page 2 shows the 26th; count says 1–25 of 26 | No error expected |
| Status labels | Pending, Confirmed, Waitlisted, Cancelled across events | Each card shows the matching `RegistrationStatusDisplay` label and badge | No error expected |
| Cutoff label | Pending or Waitlisted on an event whose start passed | "Not confirmed before cutoff" with `badge-closed`; stored status unchanged | No error expected |
| Cancelled row | Cancelled registration | Included with "Cancelled"/`badge-inactive` | No error expected |
| Non-published event | Registration on a Draft, Completed or Cancelled event | Card renders without a "View event" link | No error expected |
| Empty | Actor has no registrations | Empty page, page 1 of 0, pager hidden, empty state shows | No error expected |
| Page out of range | `?page=99` | Clamped to the last page | No error expected |
| Anonymous | No cookie | Member policy redirects to sign-in; no rows are read | Sign-in redirect |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs` — add `CountMyRegistrationsAsync(int userId, ct)` and `GetMyRegistrationsAsync(int userId, int skip, int take, ct)`, both owner-filtered by `members.user_id`; add record `MyEventRegistrationRow(RegistrationId, EventId, EventTitle, TypeName, VenueName, StartsAt, EventStatusId, Status)`. Keep `GetMemberRegistrationAsync` and `DiscoverOpenEventIdsForUserAsync` unchanged for their other callers.
- `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` — implement both with the existing `GetDbSet<T>().AsNoTracking()` query style: join `Registration` → `Member` (`member.UserId == userId`) → `Event` → `EventType` (`TypeName`) → `Venue` (`RoomName`); `orderby event.StartsAt descending, registration.RegistrationId descending`; `Skip(skip).Take(take)` (AD-13).
- `ASI.Basecode.Services/Services/MyEventsService.cs` (new) — `MyEventsService : IMyEventsService`; injects `IRegistrationRepository`, `ICurrentActor`, `TimeProvider`. `GetRegistrationsAsync`: actor id, count, `PageRequest` normalize/clamp, page read, map each row to `MyEventRegistrationCardViewModel` with `RegistrationStatusDisplay.Label/BadgeClass`, `startCutoffPassed` from `ParticipationFacts.StartCutoffPassed(now).Compile()(new Event { StartsAt = row.StartsAt })`, `CanViewEvent = row.EventStatusId == EventStatusCode.Published`. `GetAttendanceAsync`: empty `PagedResult<MyEventAttendanceRowViewModel>` (M-08 hand-over; 4.7 replaces the method).
- `ASI.Basecode.WebApp/Startup.DI.cs` — register `services.AddScoped<IMyEventsService, MyEventsService>();` beside the other Services.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` — remove the `EmptyMyEventsService` line in the disabled branch and the `AddDemoMyEvents` call in the enabled branch.
- Delete `ASI.Basecode.WebApp/DemoData/DemoMyEventsService.cs`, `EmptyMyEventsService.cs`, `DemoMyEventsRegistration.cs`.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — M-02 row state → `removed` with this commit; update the M-08 detail table and hand-over note to point at `MyEventsService.GetAttendanceAsync` (empty) until 4.7.
- `tests/ASI.Basecode.IntegrationTests/MyEventsRegistrationQueryTests.cs` (new) — one SQL-backed smoke using `SqlServerDatabaseFixture`, `MemberEventTestSupport`, `AttendTaWebApplicationFactory`: two members, 26 events/registrations for the actor across all statuses (one started Pending), one registration for the other member; assert owner scope, 25/1 paging, status and cutoff labels, Cancelled visibility, and the guarded "View event" link.

## Tasks & Acceptance

**Execution:**
- [ ] `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs`, `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` — add the owner-scoped count and page query — one SQL read replaces the composition.
- [ ] `ASI.Basecode.Services/Services/MyEventsService.cs` — implement the contract with the real registrations query and the empty attendance page — the surface keeps its shape.
- [ ] `ASI.Basecode.WebApp/Startup.DI.cs` — register the real service — production resolves it with the demo switch on or off.
- [ ] `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` and the three M-02 demo files — remove the seam — no M-02 code or registration line remains.
- [ ] `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-02 `removed` and update M-08's provider reference — register rule 11 and rule 12.
- [ ] `tests/ASI.Basecode.IntegrationTests/MyEventsRegistrationQueryTests.cs` — prove owner scope, paging, statuses and cutoff through the rendered tab — the ticket's focused smoke.
- [ ] `dotnet build` and the two focused test runs — confirm no regressions.

**Acceptance Criteria:**
- Given two members with registrations, when the first opens My Events, then only their own registrations render, newest start first, 25 per page with the correct count and pager.
- Given registrations in every stored status, when the tab renders, then each label and badge comes from `RegistrationStatusDisplay`, and a Pending or Waitlisted row on a started event reads "Not confirmed before cutoff" without changing its stored status.
- Given a Cancelled registration, when the tab renders, then it appears as "Cancelled" (the old composition dropped it).
- Given a registration on a non-published event, when the card renders, then no "View event" link appears.
- Given the demo switch on or off, when My Events opens, then the real service answers and no demo provider is registered.

## Implementation Notes

## Plan Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---------|---------|-------|----------|
| `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs:98-103` — the demo startup log still lists M-02 (and M-08) as active after those providers were removed | medium | patch | Verified: this log line is the only remaining M-02 reference in code; register rule 3 treats the startup log as the active-id list and 8.11 searches the source for every register id, so the stale id would fail that gate. Remove M-02 and M-08 from the logged list. |
| `_bmad-output/initiative-attendta/demo-mock-register.md:299-300` — the M-08 detail still says the providers are in `DemoData` although the real provider is `MyEventsService` | low | patch | Verified: the table immediately below points at `ASI.Basecode.Services/Services/MyEventsService.cs` and no `IMyEventsService` implementation remains under `DemoData`; 4.7 would search the wrong folder. Reword the sentence to name the Services provider and the empty attendance page. |

## Design Notes

One repository read supplies everything the card needs, so the service never composes per-event reads. Paging and ordering happen in SQL (AD-13) and the owner filter precedes `Count`. `CanViewEvent` mirrors `MemberEvents/Details`, which serves only a published event, so linking anything else would 404. `GetAttendanceAsync` stays an empty page: M-08 shares the old provider class, and register rule 12 hands 4.7 the method to replace rather than a class to delete.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — expected: 0 warnings, 0 errors.
- `.\tests\ASI.Basecode.Services.Tests\bin\Debug\net10.0\ASI.Basecode.Services.Tests.exe -noColor` — expected: all pass (no new service tests).
- `$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; .\tests\ASI.Basecode.IntegrationTests\bin\Debug\net10.0\ASI.Basecode.IntegrationTests.exe -class ASI.Basecode.IntegrationTests.MyEventsRegistrationQueryTests -noColor` — expected: the focused smoke passes.

**Manual checks (if no CLI):**
- As a member with mixed-status registrations, open `/MyEvents/Registrations`: own rows only, newest start first, status badges match the detail page, the pager appears at 26 rows, and a Cancelled row is visible.