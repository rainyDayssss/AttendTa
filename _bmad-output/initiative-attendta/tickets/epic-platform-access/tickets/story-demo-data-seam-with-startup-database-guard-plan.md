---
title: 'Demo data seam with startup database guard'
type: 'feature'
ticket: '14'
created: '2026-11-06'
status: 'built'
baseline_revision: 'e5ab1eaf4f88d4681ac3fecc6db8542afd958e34'
route: 'full'
route_source: 'auto'
risk: 'medium'
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

**Problem:** The demo needs populated check-in, completion, attendance and certificate screens, but those backends do not exist yet, and demo data enabled by accident on a live host would fake check-ins and certificates.

**Approach:** One `DemoData:Enabled` switch that refuses to start against any database other than the demo one, one in-memory store keyed by real record ids, a start-up seed built from rows already in the connected database, and the two event-state commands with their real refusal rules plus the Completed/check-in overlay on the three existing event reads.

## Boundaries & Constraints

**Always:** `DemoData:Enabled` is `false` in every committed `appsettings*.json`; the guard runs before any demo code and refuses startup (or a missing `DemoData:TokenKey`) with a logged reason and nothing on screen. Demo commands change only the in-memory store: no SQL row, no file under `Common:SetupRoot`, no Activity Log entry. The overlay changes only the event status shown as Completed and the check-in open flag, and only for records a demo command changed. Real reads run first; a failing real read is never replaced by demo data. Nothing rendered may reveal demo data (register rule 3). All demo code sits in one WebApp folder with one registration method calling one registration file per surface. A demo command reloads the real record and refuses exactly as the real command will. A pass token is the HMAC-SHA256 of the registration id under `DemoData:TokenKey` and is stable across restarts, observed through an operator-only `--demo-pass-token <registrationId>` switch that prints the derived token and writes nothing; it is deleted with the seam in 8.13.

**Never:** Add a real service or query to `ASI.Basecode.Services` or `ASI.Basecode.Data`, change the schema, add EF migrations, hard-delete, log tokens or secrets, add any demo marker to rendered output, change master-data rules, or add automated tests beyond the one startup-guard test.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Guard, wrong database | Switch on, `AllowDatabase` unset, connected name ≠ `AttendTa_Demo` | Process refuses to start | Reason logged to stderr; exception message names the database |
| Guard, allowed database | Switch on, `AllowDatabase` equals the connected name | App starts | — |
| Guard, demo database | Switch on, connected name is `AttendTa_Demo` | App starts | — |
| Guard, no token key | Switch on, `DemoData:TokenKey` blank | Process refuses to start | Reason logged; no store built |
| Switch off | Key absent or `false` | Normal screens, no store, existing suites green | — |
| Open/close check-in | Event not Published, or demo-completed | Refused | Inline/notice message, state unchanged |
| Complete event | Cancelled, or start time not passed, or already completed | Refused | Inline/notice message, state unchanged |
| Complete event | Published, started, not completed | Completed overlay on list, feed, detail; check-in closes | — |
| Pass token | Same registration id after restart | Identical token | — |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Program.cs` -- CLI switch parsing (L13-54) and host build (L56-80); add the pass-token switch beside them.
- `ASI.Basecode.WebApp/Startup.cs` -- `ConfigureServices` L59-107; connection-string refusal L65-72 is the pattern the guard follows.
- `ASI.Basecode.WebApp/Startup.DI.cs` -- `ConfigureOtherServices` L24-82; the demo registration method is called from here.
- `ASI.Basecode.WebApp/appsettings.json` -- committed settings (22 lines); add `DemoData:Enabled = false`.
- `ASI.Basecode.WebApp/Readiness/DemoReadiness.cs` L37-56 -- the existing `SqlConnection` + `SELECT DB_NAME()` pattern to reuse in the guard.
- `ASI.Basecode.Data/Repositories/EventRepository.cs` -- `GetPageAsync` L24 (all events, paged), `GetPublishedPageAsync` L61 and `GetPublishedEventAsync` L96 (both filter `Published`), `GetEventAsync` L54.
- `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` -- `GetReviewQueueAsync` L105 returns `RegistrationReviewRow(RegistrationId, EventId, EventTitle, MemberName, StudentNumber, ...)` for a status; the seed's confirmed-registration source.
- `ASI.Basecode.Services/ServiceModels/{EventViewModel,MemberEventListItemViewModel,MemberEventDetailViewModel}.cs` -- the three read models the overlay touches; none carries a check-in flag and the two member models lack `EventStatusId`.
- `ASI.Basecode.WebApp/Controllers/EventController.cs` L41-45 (`Index`), L174-188 (`Publish` refusal pattern); `Controllers/MemberEventsController.cs` L39-46.
- `ASI.Basecode.WebApp/Views/Event/Index.cshtml` L42 (plain `StatusLabel`), L43-58 (actions cell); `Views/MemberEvents/Index.cshtml` L32-46 (badges), `Views/MemberEvents/Details.cshtml` L51-54 (signup row).
- `ASI.Basecode.WebApp/Styles/app.css` L115-119, L145-146 -- badge classes; `wwwroot/css/app.css` is generated, never hand-edited.
- `tests/ASI.Basecode.IntegrationTests/{SqlServerDatabaseFixture,AttendTaWebApplicationFactory}.cs` -- per-class `AttendTa_IT_<guid>` database and `WithWebHostBuilder` settings; `CertificateTracerFlowTests.cs` L91-110 shows asserting a startup exception.
- `_bmad-output/initiative-attendta/demo-mock-register.md` -- rows M-01/M-04/M-09 and rules 2, 4, 5, 7, 8, 9, 10; the register update lands in this PR.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.WebApp/appsettings.json` -- add `DemoData:Enabled = false` -- committed switch stays off.
- [x] `ASI.Basecode.WebApp/DemoData/DemoDataStartupGuard.cs` -- read `Enabled`, require `TokenKey`, open the configured connection and compare `SELECT DB_NAME()` with `AttendTa_Demo`/`AllowDatabase`; log the reason and throw `InvalidOperationException` -- the live-host safety gate.
- [x] `ASI.Basecode.WebApp/Startup.cs` -- call the guard from `ConfigureServices` after the connection-string check -- runs before any demo registration.
- [x] `ASI.Basecode.WebApp/DemoData/DemoStore.cs` -- singleton in-memory store: check-in open and completed event ids, attendance by registration id, certificate review and issue state, account and membership status, `GetPassToken(registrationId)`; invalidation turns a pending/approved review not-eligible with the reason and is refused when a certificate is issued -- register rules 4, 5, 8, 10.
- [x] `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs` -- `IHostedService` that composes `IEventRepository.GetPageAsync` and `IRegistrationRepository.GetReviewQueueAsync` to seed a check-in-open published event, a started event ready to complete, and a completed event with check-ins (one invalidated) plus a pending, a declined and an issued certificate; logs active register ids and degrades when rows are missing -- register rule 5.
- [x] `ASI.Basecode.Services/Interfaces/IEventStateCommands.cs` -- contract `OpenCheckInAsync`, `CloseCheckInAsync`, `CompleteAsync` returning/raising the same `DomainException` outcomes the real tickets will -- one interface both the demo and 4.2/3.12 implement.
- [x] `ASI.Basecode.WebApp/DemoData/DemoEventStateCommands.cs` -- reload the real event, apply the refusal rules (Published only; start passed for completion; close check-in on completion), mutate only the store -- register rule 8.
- [x] `ASI.Basecode.WebApp/DemoData/DemoEventStateOverlay.cs` + no-op twin -- set `EventStatusId = Completed` and `IsCheckInOpen` on the three read models for store-marked ids, nothing else; the no-op is registered when the switch is off -- register rule 7.
- [x] `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` + `DemoEventsRegistration.cs` -- one registration method calling one file per surface; registered from `Startup.DI.cs` -- register rule 9.
- [x] `ASI.Basecode.Services/ServiceModels/CheckInControlViewModel.cs` -- event id, open flag, can-change -- model for the shared partial.
- [x] `ASI.Basecode.WebApp/Views/Shared/_CheckInControl.cshtml` -- compact POST form toggling check-in with antiforgery and an accessible label -- the partial 3.16 and 4.11 reuse.
- [x] `ASI.Basecode.WebApp/Controllers/EventController.cs` -- `OpenCheckIn`/`CloseCheckIn`/`Complete` POST actions calling the command contract and redirecting with `TempData` feedback; apply the overlay in `Index` -- the visible host for the demo.
- [x] `ASI.Basecode.WebApp/Controllers/MemberEventsController.cs` -- apply the overlay in `Index` and `Details` -- feed and detail show Completed/check-in.
- [x] `ASI.Basecode.Services/ServiceModels/{EventViewModel,MemberEventListItemViewModel,MemberEventDetailViewModel}.cs` -- add `IsCheckInOpen` and, on the two member models, `EventStatusId`/`StatusLabel` -- the named overlay fields.
- [x] `ASI.Basecode.WebApp/Views/{Event/Index,MemberEvents/Index,MemberEvents/Details}.cshtml` -- render the status badge, the check-in badge/row, the partial and the completion action -- DESIGN.md §5 badges, §6 screens 8/11/12.
- [x] `ASI.Basecode.WebApp/Styles/app.css` -- badge and compact-control classes; regenerate `wwwroot/css/app.css` -- design tokens.
- [x] `ASI.Basecode.WebApp/Program.cs` -- `--demo-pass-token <registrationId>` printing the derived token for the restart-stability check -- makes the Verify line runnable without logging tokens.
- [x] `tests/ASI.Basecode.IntegrationTests/DemoDataStartupGuardTests.cs` -- with `DemoData:Enabled=true`, `TokenKey` set, no `AllowDatabase` and the fixture database, creating the client throws with the database reason -- the one automated test.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md` -- record M-01/M-04/M-09 interface, classes and seeded scenarios, the seed's starting state for demo-script.md, and each screen's real/demo sources and file boundaries -- register rules 11 and the ticket's closing requirement.

**Acceptance Criteria:**
- Given the switch on, `AllowDatabase` unset and a non-demo database, when the app starts, then it refuses and the reason is logged; given `AllowDatabase` naming that database, or `AttendTa_Demo`, then it starts; a missing `TokenKey` also refuses.
- Given the switch off, when the existing suites run, then they pass and the screens show their normal state.
- Given the seeded data, when the commands run, then the officer list, member feed and detail show Completed and check-in state, and the three refusals leave state unchanged.
- Given a pass token before and after a restart, when it is derived again, then it is identical.
- Given any demo command, when it runs, then no SQL row, stored file or Activity Log entry is written.

## Implementation Notes

- The guard runs inside `Startup.ConfigureServices`, because Serilog only attaches in `ConfigureApp` (after `Build`); the reason therefore goes to `Console.Error` and is repeated in the `InvalidOperationException` message, the same channel the existing connection-string refusal uses.
- The overlay is applied at the three controller call sites rather than as repository decorators: the real reads filter on `Published`, and the demo-completed event is still a real Published row, so only the label and flag change and no query is touched.
- The seed composes `IEventRepository.CountAsync`, `GetPageAsync` and `IRegistrationRepository.GetReviewQueueAsync(Confirmed, null, 0, 500)`; it picks the started published event with the most confirmed registrations as the completed one, the next started one as ready to complete, and the next published one as check-in open.
- The seed also writes a declined review (not eligible with a review reason) beside the pending and issued ones, because the ticket's seed line requires all three; the invalidated check-in keeps its own not-eligible review with the invalidation reason.
- With the switch off the check-in control and Complete action stay rendered and `UnavailableEventStateCommands` returns the standard refusal, as register rule 2 requires, so the switch-off screens are not byte-identical to the previous build.
- Pre-existing baseline break fixed: `tests/ASI.Basecode.Services.Tests/RegistrationReviewTests.cs`'s `FakeMembers` still had the pre-2.9 `IMemberRepository` signatures, so the services test project did not compile at `e5ab1ea`. Only the two fake signatures changed; no test behaviour did.
- Matrix audit: the ticket permits exactly one automated test, so only the wrong-database row is covered by a test. The remaining rows are covered by the manual checks the ticket mandates (allowed database, missing token key, the three refusals, the completion overlay, and the pass token across restarts), and adding automated tests for them is forbidden by the ticket.

## Design Notes

`IEventStateCommands` lives in `Services/Interfaces` because register rule 9 requires the demo provider to implement the same contract 4.2 and 3.12 will; it is a contract only, with no implementation or data access in Services. The overlay is applied after the real read in the controller rather than as a repository decorator, because the real reads filter on `Published` and the demo-completed event is still a real Published row — the overlay only relabels it, so no query changes and the switch-off path is untouched. The seed composes existing repository reads only; it never writes, and it logs the register ids it activated.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln --configuration Release` -- solution builds with zero errors.
- `npm --prefix ASI.Basecode.WebApp ci` and `npm --prefix ASI.Basecode.WebApp run css:build` -- CSS compiles; `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` clean after regeneration.
- `dotnet test --project tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj --configuration Release` -- service tests pass.
- With `ATTENDTA_TEST_SQL` set: `dotnet test --project tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --configuration Release` -- existing integration tests and the new guard test pass.
- `dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build` with `DemoData__Enabled=true`, no `DemoData__AllowDatabase`, and a non-demo connection string -- exits non-zero with the database reason on stderr.

**Manual checks:**
- With the switch on and `AllowDatabase` naming the connected database, run the app: the officer event list, member feed and event detail show the seeded Completed and check-in state; opening check-in on an unpublished event, completing a cancelled event and completing before start are each refused with a message; no demo marker appears anywhere on screen.
- Run `--demo-pass-token <registrationId>`, restart, run again -- identical token.
- With the switch off, the three screens show their normal state with no overlay: the check-in control and the Complete action are still present but the command returns the standard refusal, as register rule 2 requires.

## Review Triage Log

Quick lens, iteration 0 (1 high / 0 medium / 4 low / 1 rejected).

- low / rejected (fix edits the plan): Views/Event/Index.cshtml L45/L62 hide the check-in control and Complete action for non-Published rows, so the plan's manual check cannot post them from the screen. Verified: the UI correctly withholds impossible actions (DESIGN.md §6 screen 8), and DemoEventStateCommands.RequirePublishedAsync still refuses a direct POST, which is what the ticket's Verify line checks. The only fix is plan wording, so it is rejected per the triage rule.
- medium / patch: five demo classes carry no register id comment, so the register's search-by-id sweep for 8.13 misses them. Verified by grep over ASI.Basecode.WebApp/DemoData/*.cs: DemoDataOptions, DemoDataStartupGuard, IEventStateOverlay, NoOpEventStateOverlay and UnavailableEventStateCommands have no M- marker.
- low / patch: Styles/app.css L5 enumerates adge-draft badge-published badge-cancelled, which EventStatusDisplay.BadgeClass never emits, and omits the four names it does emit. No rendering defect today because the rules sit in @layer components, but the enumeration is dead and misleads the next dynamic class.
- low / patch: DemoStore.InvalidateAttendance tests the issued-certificate guard before the attendance lookup, so a registration with an issued certificate and no attendance row returns the certificate message instead of not-found.
- low / patch: Program.cs --demo-pass-token reads only DemoData__TokenKey while the guard and store read the whole configuration stack, so a key supplied by appsettings or user secrets enables demo data but breaks the restart check.
- low / patch: DemoEventStateOverlay assigns IsCheckInOpen for every record, forcing alse on ids the store does not know, contrary to "only for records a demo command changed". Benign today because no overlaid read projects the field; it would mask real state once one does.