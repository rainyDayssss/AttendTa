---
title: 'Signup deadline enforcement at exact local boundary'
type: 'feature'
ticket: ''
created: '2026-11-06'
status: 'built'
baseline_revision: '12c9ea5325aeb960d382f2f58b71b0adcce18755'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A stale signup must not create a registration at or after an event's deadline, and members need understandable closure information without losing earlier registrations.

**Approach:** Prove and, where necessary, complete the existing server-side deadline behavior using a controlled clock, including a stale browser submission and a Manila-labelled deadline.

## Boundaries & Constraints

**Always:** Evaluate signup against the injected TimeProvider after acquiring existing locks; use the canonical ParticipationFacts predicate and UTC storage. Display the deadline in Asia/Manila. Preserve existing registration rows and published event status. Test SQL datetime2(3) storage separately from tick-precision clock comparisons.

**Never:** Automatically change event status, cancel registrations, weaken authorization or antiforgery, alter unrelated event lifecycle behavior, or expose unpublished event details.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Before deadline | Eligible member, published event, clock one tick before deadline | Registration succeeds | No error |
| Exact and after | Stale form submitted at or after deadline | Explicit signup-closed refusal and no new registration | Existing registrations preserved |
| Browse after closure | Published event whose deadline has passed | Event remains in feed with closed state and labelled Manila deadline | No automatic status change |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Queries/ParticipationFacts.cs` — shared published and `now < RegistrationDeadline` signup predicate; do not duplicate it.
- `ASI.Basecode.Services/Services/MemberEventService.cs` — locks user then event, rechecks signup after locks, and returns `CutoffPassed`; retain transaction and guard ordering.
- `ASI.Basecode.WebApp/Controllers/MemberEventsController.cs`, `Views/MemberEvents/Index.cshtml`, `Views/MemberEvents/Details.cshtml` — stale POST redirects to feed with a closure message; feed and details show Manila-formatted deadlines. Adjust only if a requirement fails.
- `ASI.Basecode.WebApp/Formatting/ManilaTimeFormatter.cs` — existing UTC-to-Manila formatter; avoid unrelated global display changes.
- `tests/ASI.Basecode.Services.Tests/ParticipationFactsTests.cs`, `MemberEventServiceTests.cs` — existing fixed-clock and service harness tests.
- `tests/ASI.Basecode.IntegrationTests/MemberEventFlowTests.cs` — existing SQL and browser patterns; `AccountRequestSubmissionTests.cs` demonstrates fake clock injection. Existing SQL boundary test uses a one-millisecond difference despite its name.

## Tasks & Acceptance

**Execution:**
- [x] `tests/ASI.Basecode.Services.Tests/ParticipationFactsTests.cs` and `MemberEventServiceTests.cs` — explicitly test a fake-clock one-tick-before, exact, and after boundary, including signup success and refusal where supported by the service harness.
- [x] `tests/ASI.Basecode.IntegrationTests/MemberEventFlowTests.cs` — exercise a stale authenticated antiforgery form across the deadline with a controllable clock; verify persisted earlier registration, no new registration, published state, feed and Manila-labelled deadline. Use a database-representable deadline and clock ticks around it.
- [x] `ASI.Basecode.Services/Services/MemberEventService.cs`, `ASI.Basecode.WebApp/Controllers/MemberEventsController.cs`, `ASI.Basecode.WebApp/Views/MemberEvents/` — change only if tests expose missing required behavior, preserving existing safe redirect and transaction semantics.

**Acceptance Criteria:**
- Given an eligible participant and a published event, when the request clock is one tick before the stored deadline, then signup creates one pending registration.
- Given a stale form for an eligible participant, when submitted at or after that deadline, then the server explicitly refuses signup and creates no registration.
- Given a previous registration and a published event, when the deadline passes and a stale signup is refused, then the prior registration remains intact and the event remains published and visible in the feed with its Manila-labelled deadline.

## Implementation Notes

- Added service and HTTP/SQL boundary tests; no application behavior changed. Verification is blocked: no .NET SDK is installed and `ATTENDTA_TEST_SQL` is unset. Tests have not been compiled or run.
- The untracked plan disappeared unexpectedly and was restored from the approved copy with the user's permission.
- Located SDK 10.0.403 in `C:\Users\Administrator\.dotnet` and isolated this work in a separate worktree. Build passed; all 189 service tests and 212 SQL integration tests passed. The integration suite required the user-local SDK and existing Poppler tools on PATH; its initial run failed due to those PATH settings.

## Plan Change Log

## Review Triage Log

- Quick review: 0 high, 1 medium, 0 low, 0 false, 0 maybe-false.
- medium, defer — `ManilaTimeFormatter.Format` truncates seconds and milliseconds although `EventLifecycleService` accepts them, so an existing non-minute deadline may be shown earlier than its true boundary. This predates the test-only change; track separately.

## Verification

- `dotnet build` — succeeds with .NET 10 SDK.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — all tests pass.
- With `ATTENDTA_TEST_SQL` set to a local SQL Server master connection, `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — all tests pass, including stale HTTP form and persistence checks.
- Environment at planning: .NET SDK is not installed; verification requires an available .NET 10 SDK and local SQL Server.
