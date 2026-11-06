---
title: 'Member event browsing and registration submission with academic snapshot'
type: 'feature'
ticket: '5'
created: '2026-11-06'
status: 'built'
baseline_revision: '74bbd29400d2e88b7078635d7562905b7e86424a'
route: 'full'
route_source: 'auto'
risk: 'medium'
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

**Problem:** Published events cannot yet be browsed or joined by eligible participants.

**Approach:** Provide a paged published-event feed and details, and submit one pending registration with server-captured academic snapshots and clear capacity and cutoff feedback.

## Boundaries & Constraints

**Always:** Authenticate requests; permit eligible members, officers and administrators equally, checking active account and membership rather than role. Use canonical ParticipationFacts, UTC and injected TimeProvider; lock the owner's users row then the event in one transaction before rechecking eligibility, signup, duplicate and seat count. Use database uniqueness as the final duplicate guard. Require antiforgery on POST; display Asia/Manila times; keep draft events and private file paths hidden. Page in SQL at 25 with deterministic ordering.

**Never:** Allocate seats, create QR tokens, review, cancel, promote or implement My Events. Do not block signup merely because confirmed seats are full, change schema, expose image paths, or add speculative submission audit events.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Browse | Published including deadline-passed; draft or other states | Only published appear; closed signup indicated; count and paging follow filtering | Empty state if none |
| Register | Eligible participant; published; before deadline | One Pending row with current course, year and section snapshots; no seat or QR | Success feedback |
| Full | Confirmed count at or above limit | Pending row still created; warning says seat is not guaranteed | No capacity refusal |
| Duplicate | Existing registration of any status, including Cancelled | No second row | Duplicate, including unique-key race |
| Closed or stale | SignupOpen is false, including exact deadline or changed state | No row created | CutoffPassed without exposing draft details |
| Ineligible or invalid | Missing/inactive user or inactive/missing member, anonymous or invalid antiforgery | No row created | Forbidden/authentication or antiforgery response |
| Concurrent changes | Membership, event state, deadline or duplicate changes before locks | Recheck locked state and serialize registration writes | Refusal leaves no partial write |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Queries/ParticipationFacts.cs` provides `EligibleParticipant`, `SignupOpen` and `SeatHeld`/`SeatHolders`; use those definitions rather than duplicating rules.
- `ASI.Basecode.Data/Repositories/UserRepository.cs` and `EventRepository.cs` provide transaction-required `LockUserAsync` and `LockEventAsync`; `UnitOfWork.cs` owns transactions and translates SQL 2601/2627 to Duplicate. `MemberRepository.cs` supplies linked member data; `User.Id` is numeric whereas `User.UserId` is email.
- `EventRepository.cs` management queries include drafts; `RegistrationRepository.cs` only checks confirmed status. The schema uniquely indexes `(event_id, member_id)` across all statuses.
- `ASI.Basecode.Services/ServiceModels/PageRequest.cs` and `PagedResult.cs`, `Views/Shared/_Pager.cshtml`, `Formatting/ManilaTimeFormatter.cs`, and `EventCoverController.cs` provide existing paging, display and private-cover access. `EventController` is management-only; do not reuse its list unfiltered.
- `tests/ASI.Basecode.Services.Tests/ParticipationFactsTests.cs` and `tests/ASI.Basecode.IntegrationTests/EventDraftFlowTests.cs` show clock, SQL fixture, lock, sign-in and antiforgery patterns. Update affected repository fakes.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/`, `ASI.Basecode.Data/Repositories/` — add published feed/detail projections and registration existence, confirmed-count and insert operations; apply filters before count and order by start then event ID. Reuse canonical seat fact and avoid returning image paths to views.
- [x] `ASI.Basecode.Services/Interfaces/IMemberEventService.cs`, `ASI.Basecode.Services/Services/MemberEventService.cs`, `ASI.Basecode.Services/ServiceModels/` — implement feed, detail and transactional signup with server-side academic snapshots, actor, lock order, canonical facts, duplicate handling and full warning; capture time after locks.
- [x] `ASI.Basecode.WebApp/Startup.DI.cs`, `ASI.Basecode.WebApp/Controllers/MemberEventsController.cs` — wire authenticated feed/details and antiforgery-protected POST with PRG and safe outcome feedback for all eligible roles.
- [x] `ASI.Basecode.WebApp/Views/MemberEvents/`, `ASI.Basecode.WebApp/Views/Shared/_Header.cshtml`, `ASI.Basecode.WebApp/Styles/app.css`, `ASI.Basecode.WebApp/wwwroot/css/app.css` — add responsive feed/details, accessible signup/full/closed feedback, authorized cover links, pager and navigation for all roles; rebuild compiled CSS if changed.
- [x] `tests/ASI.Basecode.Services.Tests/`, `tests/ASI.Basecode.IntegrationTests/` — cover the matrix and all acceptance criteria, including required interface fake updates, SQL uniqueness and lock order.

**Acceptance Criteria:**
- Given published and unpublished events across multiple pages, when an authenticated user browses, then only published events appear in stable order, including closed events, with safe covers and Manila-labelled dates.
- Given an eligible participant of any application role, when they register before the deadline, then exactly one pending registration stores their current academic snapshot and a full event gives a no-seat-guaranteed warning without blocking submission.
- Given a duplicate including a cancelled registration, closed signup at or after the deadline, an ineligible participant or a forged POST, when registration is attempted, then no new row is persisted and the appropriate refusal is returned.
- Given competing or stale registration attempts, when SQL transactions run, then user and event locks are acquired in order, guards use locked current state and duplicate submissions cannot create multiple rows.

## Implementation Notes

- Coordinator verification: `dotnet build` passed with zero warnings; 170 service and 204 SQL integration tests passed with no skips. `npm ci` and CSS build passed. Matrix coverage includes empty feed, SQL unique-key fallback, stale forms, and locked membership/event changes.
- Data: `PublishedEventListItem`/`PublishedEventDetail` project `HasCover` instead of `image_path`; confirmed seats come from `ParticipationFacts.SeatHolders`. `GetPublishedPageAsync` filters published rows before `Skip/Take` and orders by `starts_at, event_id`; `GetPublishedEventAsync` returns only published rows. `RegistrationRepository` adds any-status existence, canonical confirmed count and insert; `MemberRepository` adds the linked `Member` read used for snapshots.
- Services: `MemberEventService` authenticates through `ICurrentActor`, then in one `ExecuteInTransactionAsync` locks `users` and `events` in AD-4 order. Eligibility is rechecked from the locked user and its member row; signup, duplicate and confirmed count are rechecked under the event lock; time is captured after both locks. The pending row stores course/year/section snapshots and no seat or QR. A full event still inserts and returns `SeatNotGuaranteed`. A duplicate pre-check and the `(event_id, member_id)` unique key both map to `Duplicate`.
- Web: `MemberEventsController` exposes `/Events` (feed), `/MemberEvents/Details` and an antiforgery-protected `/MemberEvents/Register` POST with PRG. Success goes to the detail page; `Duplicate` returns to the detail; `CutoffPassed`, `Forbidden` and `NotFound` return to the feed so draft details are never revealed. The header links members to Events and management to Browse events.
- UI: card feed (1/2/3 columns at base/640/1280) and a detail page with cover, Manila-labelled times, confirmed count and accessible open/closed/full/already-registered feedback. Covers are rendered only through the authorized `EventCover/Image` action.

## Plan Change Log

## Review Triage Log

- Quick review: 0 high, 0 medium, 1 low, 3 false, 0 maybe-false.
- false — `MemberEventsController.Index`/`Details` malformed-actor 500: `Startup.Auth.ValidatePrincipalAsync` rejects missing/non-numeric identifiers, and `AccountService.GetActiveUserAsync` rejects nonpositive IDs before `[Authorize]` invokes either action.
- low, rejected — `MemberEvents/Index` alias absent: `/Events` and `/MemberEvents` already provide the feed, all new navigation generates its route, and there is no prior or specified `/MemberEvents/Index` contract; adding a public route for an unlikely manually entered URL is not needed.
- false — repeated `SignupOpen` compilation and projected `Event` allocation: these use the single canonical predicate, and the finding establishes no measurable user or developer harm from processing at most 25 cards.
- false — feed says signup open for an already registered member: `SignupOpen` is the event-wide Published/deadline fact, not a personalized eligibility claim; details accurately suppress a second submission and the server enforces uniqueness.

## Design Notes

The full warning counts Confirmed registrations only, not Pending or Waitlisted. Hide drafts in browsing and details; signup refusals must not reveal their content. Snapshot fields come only from the locked owner's current member record. No submission audit is required by this story's audit vocabulary.

## Verification

- `dotnet build` — succeeds.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — all pass, including clock boundaries and eligibility.
- With `ATTENDTA_TEST_SQL` set to local SQL Server master, `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — all pass, including HTTP, SQL uniqueness and concurrency.
- If styles change, `npm --prefix ASI.Basecode.WebApp ci` and `npm --prefix ASI.Basecode.WebApp run css:build` — succeed; generated CSS is committed with source and regenerates without further changes.
