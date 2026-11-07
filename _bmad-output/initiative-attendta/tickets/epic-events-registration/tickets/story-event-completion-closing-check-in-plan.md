---
title: 'Event completion closing check-in'
type: 'feature'
ticket: 12
created: '2026-11-07'
status: 'built'
baseline_revision: '0a61d4e35f71c9c2ec15f538b876bfbee0bba1a2'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: [quick]
review_loop_iteration: 0
context:
  - _bmad-output/initiative-attendta/demo-mock-register.md
  - _bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The officer completion control still posts to the demo `IEventStateCommands.CompleteAsync`, which only marks an in-memory store. Nothing completes a real Event, closes check-in, records `completed_by/at`, writes `event.completed`, or runs a completion hook.

**Approach:** Add `CompleteAsync` to `IEventLifecycleService`: at or after start, under the event lock and in one transaction, set status Completed, close check-in, record completion metadata, append `event.completed`, then invoke every registered `IEventCompletionHandler` inside the same transaction. Point the controller at the real service, remove the M-09 demo command and Completed read overlay, and make the member feed/detail read Completed events from SQL.

## Boundaries & Constraints

**Always:** Management actor only; Published only; refuse before start with `ParticipationFacts.StartCutoffPassed(now)` and the same `now` persisted as `completed_at`; take only the event lock (AD-4); one caller-owned transaction (AD-3); an empty handler list is valid; every handler runs once inside the transaction and its exception rolls the whole completion back; audit stores a field subset with no tokens or paths (AD-7); keep the existing dossier/list UI.

**Never:** No automatic completion, no scheduled job, no second completion screen or service, no schema change, no UI redesign, no broad test authoring (8.5 owns that).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| HAPPY_PATH | Officer, Published, start <= now | Completed, check-in closed with `check_in_closed_by/at`, `completed_by/at`, one `event.completed`, handlers run once | Success message, redirect |
| BEFORE_START | Published, start > now | Refused, nothing changes | `Invalid`, error message |
| WRONG_STATE | Draft, Completed or Cancelled | Refused, nothing changes | `Invalid` |
| STALE | Status changes between read and lock | Refused, nothing changes | `Stale` |
| HANDLER_THROWS | Registered handler throws | Event, check-in closure and audit roll back | Exception surfaces |
| NO_HANDLERS | Empty `IEnumerable` | Completion still succeeds | No error |
| NOT_MANAGEMENT | Member or anonymous | Forbidden / sign-in | `Forbidden` / redirect |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Interfaces/IEventLifecycleService.cs` — add `CompleteAsync(int eventId, CancellationToken)`.
- `ASI.Basecode.Services/Interfaces/IEventCompletionHandler.cs` — new hook: `Task HandleAsync(EventCompletionContext, CancellationToken)`.
- `ASI.Basecode.Services/ServiceModels/EventCompletionContext.cs` — `(EventId, CompletedBy, CompletedAt)` record.
- `ASI.Basecode.Services/Services/EventLifecycleService.cs` — inject `IEnumerable<IEventCompletionHandler>`; mirror `CancelAsync` (discover outside, lock inside, recheck, `now` after lock, audit, save, then handlers).
- `ASI.Basecode.Resources/Constants/AuditActions.cs`, `Messages/AuditDescriptions.cs`, `Messages/Audit.resx`, `Services/Services/ActivityLogWriter.cs` — add `event.completed` and the `events` entity constraint.
- `ASI.Basecode.WebApp/Controllers/EventController.cs` — `Complete` posts to `_eventService.CompleteAsync`; keep `StateReturn`.
- `ASI.Basecode.Services/Interfaces/IEventStateCommands.cs`, `WebApp/DemoData/DemoEventStateCommands.cs`, `WebApp/DemoData/UnavailableEventStateCommands.cs` — remove `CompleteAsync`; keep open/close check-in (M-04).
- `ASI.Basecode.WebApp/DemoData/DemoEventStateOverlay.cs` — remove the Completed branch; keep the check-in-open overlay (M-04).
- `ASI.Basecode.WebApp/DemoData/DemoCheckInService.cs`, `DemoCheckInLookupService.cs` — drop the now-dead `_store.IsCompleted` guards; the published read already excludes Completed.
- `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs` — stop marking a Published row completed; seed attendance/certificate demo data on the "ready to complete" event so the demo completes it live. Keep `DemoStore.MarkCompleted` only as the M-10a/M-13b test fixture.
- `ASI.Basecode.Data/Interfaces/IEventRepository.cs`, `Repositories/EventRepository.cs` — add `CountMemberVisibleAsync`, `GetMemberVisiblePageAsync`, `GetMemberVisibleEventAsync` filtering `Published or Completed`; leave the published-only methods for check-in.
- `ASI.Basecode.Services/Services/MemberEventService.cs` — use the member-visible reads; registration/cancellation guards stay Published-only.
- `tests/ASI.Basecode.Services.Tests/EventLifecycleServiceTests.cs`, `EventCoverServiceTests.cs` — harness handler list and the new constructor argument.
- `tests/ASI.Basecode.IntegrationTests/EventDraftFlowTests.cs` — `EventDraftTestSupport.Service` accepts handlers.
- `tests/ASI.Basecode.IntegrationTests/EventCompletionFlowTests.cs` — new SQL-backed completion, before-start, audit, member-feed Completed and throwing-handler rollback tests.
- `tests/ASI.Basecode.IntegrationTests/MemberEventServiceTests.cs` and the other `IEventRepository` fakes — add the three member-visible methods.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-09 to `removed`, update the M-01/M-04/M-09 seam detail (command, overlay, seed, Complete POST).

## Tasks & Acceptance

**Execution:**
- [x] `IEventCompletionHandler` + `EventCompletionContext` — declare the hook contract — lets epic-certificates plug in without a cycle.
- [x] `IEventLifecycleService` + `EventLifecycleService` — implement the guarded, locked, audited completion and invoke handlers — the story's core.
- [x] Audit vocabulary files — register `event.completed` — keeps the closed vocabulary.
- [x] `EventController` — call the real service — makes the delivered control real.
- [x] Demo command/overlay/seed/check-in files — remove M-09 — no demo provider hides the real completion.
- [x] Member-visible repository reads + `MemberEventService` — show Completed events on the feed/detail — replaces the removed overlay.
- [x] Tests + `demo-mock-register.md` — focused primary/guard smoke and row removal — proves the acceptance rules.

**Acceptance Criteria:**
- Given a Published event at or after start, when an officer completes it, then status is Completed, check-in is closed with actor/time, `completed_by/at` are set, and one `event.completed` is logged.
- Given a Published event before start, when completion is attempted, then it is refused and nothing changes.
- Given a registered test-double handler, when completion commits, then the handler ran exactly once inside the transaction; when it throws, the event stays Published with no audit row.
- Given a completed event, when a member opens the feed/detail, then it still appears with the Completed label and cannot be registered for.

## Implementation Notes

- `CompleteAsync` discovers the event outside the transaction for `NotFound`/terminal refusals, then runs one `ExecuteInTransactionAsync` that takes only `LockEventAsync`, rechecks the status (`Stale` on a change), captures `now` after the lock, checks `ParticipationFacts.StartCutoffPassed(now)`, writes the event row and one `event.completed` audit entry, saves, and only then invokes every registered `IEventCompletionHandler` with the same `now`. A handler throw rolls the event, check-in closure and audit back together.
- `EventCompletionAuditValues` records status, `IsCheckInOpen`, `CompletedBy` and `CompletedAt` only. No QR token, file path or image path enters the payload, and `ActivityLogWriter` now constrains `event.completed` to the `events` entity type.
- `is_check_in_open` is always cleared. `check_in_closed_by/at` are written only when the flag was open, matching cancellation and preserving an earlier closure.
- The member feed/detail use `CountMemberVisibleAsync`, `GetMemberVisiblePageAsync` and `GetMemberVisibleEventAsync`, which filter Published or Completed. The published-only reads stay untouched for the check-in screen. Registration and cancellation still require Published through `ParticipationFacts.SignupOpen` and the explicit status guards.
- M-09 removal: `IEventStateCommands` and both providers lost `CompleteAsync`; `DemoEventStateOverlay` only overlays the check-in flag; the demo seed keeps the ready-to-complete event Published in SQL and seeds its attendance/certificate data so the demo completes it live; the scanner's dead `IsCompleted` guards are gone. `DemoStore.MarkCompleted` remains only as the M-10a/M-13b test fixture.
- `CertificateLedgerFlowTests` now completes the seeded ready-to-complete event through `POST /Event/Complete` before reading the ledger, and asserts the one `event.completed` audit row plus the SQL Completed status. `MemberEventBrowsingFlowTests` now expects Completed events on page 2 with the Completed label.
- Verification on 2026-11-07: `dotnet build` passed with 0 warnings; Services tests 281/281; Integration tests 350/354. The 4 failures are the pre-existing `CertificateTracerFlowTests`, which need Poppler's `pdftotext` (`ATTENDTA_PDFTOTEXT`) and are unrelated to this change. All 8 new `EventCompletionServiceTests` passed, including the before-start, wrong-state, audit, no-handler, throwing-handler rollback and member-feed cases.
- The workspace also contained uncommitted pass-QR work from a parallel session; this change is additive and did not modify or revert that work.
- Added two focused unit tests to `EventLifecycleServiceTests` for the STALE and NOT_MANAGEMENT matrix rows (the integration file covers the rest). Re-verified on 2026-11-07: build 0 warnings; Services 285/285; Integration 350/354, with the same four pre-existing `CertificateTracerFlowTests` failures caused by missing Poppler `pdftotext`.
- Review patches applied on 2026-11-07: members can open a Completed event cover; the demo overlay no longer shows check-in open on a Completed model; My Events shows "View event" for Completed; and the stale register/model/controller docs were corrected. Added `MemberCanOpenACompletedEventCoverAndStillCannotOpenADraftCover`. Final verification: build 0 warnings; Services 286/286; Integration 350/354 with the same four Poppler-only failures.
- Rebased onto `upstream/dev` `c642554` (after PR #185 merged the pass work) so the branch is current; the only conflict was the demo register's M-15a/M-15b wording, resolved by keeping the upstream M-15b-only text and the M-09 removal. Re-verified: build 0 warnings; Services 286/286; Integration 355/359 with the same four Poppler-only failures.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-11-07. Verdicts: high 0, medium 2, low 9, false 15, maybe-false 0.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | Completed event cover returns 404 for members | medium | patch | `EventLifecycleService.OpenCoverAsync` still restricts members to Published; the feed/detail now render Completed covers. |
| 2 | `RegisterAsync` relies on `SignupOpen` for the status guard | false | — | `ParticipationFacts.SignupOpen` requires `EventStatusId == Published`; a Completed row can never match. |
| 3 | `IsSignupOpen` is status-agnostic | false | — | Same predicate: Published is a conjunct, so a Completed event is never signup-open. |
| 4 | STALE is unreachable for a real concurrent change | false | — | The discover read precedes the lock; any commit between them changes the locked status, so the recheck fires. |
| 5 | Handler exception escapes the controller as 500 | false | — | The matrix says "Exception surfaces"; the service must let it propagate so the transaction rolls back. |
| 6 | Overlay can show check-in open on a Completed event | low | patch | The store's open id is not cleared by the real completion; guard the overlay on the model status. |
| 7 | Scanner `IsCompleted` branch is dead and `CanChangeWindow` unconditional | low | reject | The published-only selector never returns a Completed event; 4.2/4.3 replace the screen. No user harm. |
| 8 | Audit `occurred_at` is a second clock read | false | — | The payload uses the captured `now`; the writer's row timestamp is existing behavior shared with cancellation. |
| 9 | Register says real Completed comes from SQL but the report keeps the store fallback | low | reject | The app never sets the store completed state, so the report reads the real status; the fallback is test-only. |
| 10 | Register says "completed event seeded by M-01" | low | patch | `DemoSeedService` now seeds the ready-to-complete event; the paragraph is stale. |
| 11 | M-10a says "seeded or real completed events" | low | patch | The seed no longer marks completion; only real completed events remain. |
| 12 | Certificate ledger test comment and seed coupling | low | patch | The test seeds its own event; the comment should say so. The class database has one started event, so the seed picks it. |
| 13 | AD-4 assertion is weak | false | — | `Assert.Single` plus the "events" substring excludes type/venue locks ("event_types" does not contain "events"). |
| 14 | Audit payload uses substring matching | low | reject | The record defines the exact field subset; the negative token/path checks cover AD-7. |
| 15 | No `IEventCompletionHandler` registration point | low | reject | DI resolves an empty `IEnumerable`; the interface doc explains the seam. No runtime defect. |
| 16 | `PublishedEventDetail`/`PublishedEventListItem` docs say published-only | low | patch | They are reused by the member-visible reads for Completed events. |
| 17 | `MemberEventDetailViewModel` doc says published | low | patch | It now covers Published or Completed. |
| 18 | `MemberEventsController` doc says published | low | patch | It now covers Published or Completed. |
| 19 | `DemoParticipationReportService` doc names M-09 | low | patch | M-09 is removed; the real status comes from SQL. |
| 20 | `EventLifecycleService` class doc says draft-only | low | patch | The class also publishes, cancels, completes and manages covers. |
| 21 | My Events hides "View event" for Completed | medium | patch | The feed/detail now allow Completed, so the My Events link should too; one-line status check. |
| 22 | Officer list loses "Lifecycle controls" after completion | false | — | "View dossier" remains on every row. |
| 23 | `disabled="@(!Model.HasStarted)"` may block completion | false | — | Razor omits a false conditional attribute; the button is enabled at/after start. |
| 24 | `HasStarted` `<=` may differ from `StartCutoffPassed` | false | — | `StartCutoffPassed` is `now >= StartsAt`; the two are equivalent. |
| 25 | Seed "not used above" doc drift | false | — | Only one event is excluded, so the sentence is still accurate. |
| 26 | `DemoEventStateCommands` keeps dead `IsCompleted` guards | low | reject | Dead only because the store is no longer set in the app; consistent with rule 8 and removed with M-04/4.2. |
| 27 | `Complete` lacks `returnUrl` | false | — | The dossier posts `dossier=true`; no completion caller passes a return URL. |
| 28 | Member-visible reads duplicate published reads | low | reject | Intentional split so check-in keeps published-only semantics; no current divergence. |
| 29 | `readyToComplete` may have no confirmed registrations | false | — | The register says fewer rows degrade gracefully; the log still names the event. |
| 30 | Completed event shows "Signup closed" beside the Completed badge | low | reject | The Completed badge is present; the wording is cosmetic and belongs to 8.12 polish. |

## Design Notes

Completion mirrors cancellation: discover outside the transaction for `NotFound`/terminal refusals, then take only `LockEventAsync`, recheck status (`Stale` on change), capture `now` after the lock, write the event row and audit, save, then invoke handlers. Handlers run after the completion row is saved but still inside the caller's transaction, so they see Completed and a throw rolls everything back. `check_in_closed_by/at` are written only when the flag was open, matching cancellation. The handler contract carries only ids and the completion time; certificate review creation belongs to 6.4. The member-visible reads add Completed without touching the published-only check-in reads, so the demo seam keeps its existing refusal behavior.

## Verification

**Commands:**
- `dotnet build` — expected: succeeds.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass, including the new completion cases.
- `$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — expected: all pass, including the new completion/rollback tests.
