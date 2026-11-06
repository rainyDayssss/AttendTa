---
title: 'Registration-owned participation cancellation service (AD-6)'
type: 'feature'
ticket: '15'
created: '2026-11-06'
status: 'built'
baseline_revision: '12c9ea5325aeb960d382f2f58b71b0adcce18755'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 1
context:
  - 'AGENTS.md'
  - '_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Future event cancellation, registration review, and account deactivation need one atomic registration-owned cancellation path.

**Approach:** Provide the four AD-6 service operations and a read-only upcoming preview, preserving previous cancellations and emitting one safe audit entry per newly cancelled row.

## Boundaries & Constraints

**Always:** Mutations require an existing caller-owned transaction and authenticated actor. Callers hold owner user then event for individual cancellations, event for event-wide cancellation, and user for deactivation. For deactivation discover affected events, lock each event in ascending ID, then recheck registration, cutoff and valid attendance under locks using the injected clock and canonical facts. Validate required reasons inside the transaction. Save changes and audit atomically in the caller's transaction. Preview is advisory and read-only. Upcoming means strictly before start and no valid attendance for the registration.

**Never:** Promote waitlisted users, overwrite prior cancellations, log QR tokens, create UI or deactivation/event-cancellation commands, change the schema or bypass the registration owner. Individual and event callers own their business authorization and eligibility guards.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Open registration | Pending, Confirmed or Waitlisted | Cancel, clear QR, record actor/time/reason and one audit entry | Atomic with caller transaction |
| Repeated cancellation | Already Cancelled | Metadata and audit count unchanged | No-op |
| Upcoming preview/cascade | Future start and no valid attendance | Preview lists the affected registrations; cascade rechecks and cancels | At start/past or valid attendance excluded; invalidated attendance does not exclude |
| Reasons | Decline, administrative/event cancellation or deactivation | Require nonblank reason inside transaction | Reject without partial changes; member-requested cancellation does not require a user-provided reason |
| Missing transaction or invalid actor | Any mutation | No mutation or audit | Refuse |
| Concurrent mutation | Candidate changes before its event lock | Re-read after lock and use current time/facts | Never cancel a no-longer-eligible row |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` and `Interfaces/IRegistrationRepository.cs` currently offer insert, existence and confirmed-count operations; add tracked cancellation reads, advisory discovery and preview projections here.
- `ASI.Basecode.Data/Queries/ParticipationFacts.cs` supplies `ValidAttendances` and `StartCutoffPassed`; reuse rather than duplicating validity and cutoff rules. `Models/Registration.cs` has cancellation metadata; `Models/Lookups/LookupCodes.cs` defines statuses.
- `ASI.Basecode.Data/Repositories/EventRepository.cs` has transaction-required `LockEventAsync` with a primary-key SQL lock. `UnitOfWork.cs` rolls back and clears tracked changes, and saves only when explicitly requested. `MemberEventService.RegisterAsync` demonstrates user → event locks and clock capture after locks.
- `ASI.Basecode.Services/Services/ActivityLogWriter.cs` requires a transaction and actor, rejects secret-bearing payloads, and does not save. `ASI.Basecode.Resources/Constants/AuditActions.cs` and `Messages/AuditDescriptions.cs` supply allowed action vocabulary.
- `tests/ASI.Basecode.Services.Tests/ParticipationFactsTests.cs` and `MemberEventServiceTests.cs` show fake clocks and repository fakes. `tests/ASI.Basecode.IntegrationTests/EventDraftFlowTests.cs` records SQL locks; `MemberEventFlowTests.cs` demonstrates concurrent contexts and seeded SQL fixtures.

## Tasks & Acceptance

**Execution:**
- [ ] `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs`, `ASI.Basecode.Data/Repositories/RegistrationRepository.cs`, `ASI.Basecode.Data/Queries/ParticipationFacts.cs` — implement read/discovery and tracked rechecks for open registrations and upcoming preview using canonical attendance and start facts; return deterministic preview data and discover event IDs without relying on range locks.
- [ ] `ASI.Basecode.Services/Interfaces/IRegistrationCancellationService.cs`, `ASI.Basecode.Services/ServiceModels/`, `ASI.Basecode.Services/Services/RegistrationCancellationService.cs` — add AD-6 signatures, six-value cause enum and preview projection; enforce transaction, actor, cause/reason validation, ordered event locks, post-lock rechecks, clock, idempotent status/metadata changes, and per-row audit. Use caller-held locks as documented preconditions; do not open or commit transactions.
- [ ] `ASI.Basecode.Resources/Constants/AuditActions.cs`, `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — register the `registration.cancelled` action and description, constrain its entity type and avoid sensitive audit fields.
- [ ] `ASI.Basecode.WebApp/Startup.DI.cs` — register the new scoped service.
- [ ] `tests/ASI.Basecode.Services.Tests/`, `tests/ASI.Basecode.IntegrationTests/` — cover the matrix, update repository fakes, and add real SQL tests with reverse discovery order, command-parameter lock order, audit/QR/metadata/idempotence, rollback, and no promotion. Verify post-lock cutoff/attendance rechecks and safe audit payloads.

**Acceptance Criteria:**
- Given caller-held locks and an open transaction, when any cancellation operation changes open registrations, then each newly cancelled row has its QR cleared, actor/time/reason recorded and exactly one matching audit entry, while prior cancellations and waitlisted rows not selected remain unchanged.
- Given upcoming and past registrations with valid or invalidated attendance, when preview or deactivation cancellation runs, then only open registrations strictly before start without valid attendance qualify, and changes are rechecked after ascending event locks.
- Given a missing required reason, failed audit, or caller rollback, when cancellation is attempted, then no partial registration or audit changes persist.
- Given a call without an active transaction, when a mutating operation is invoked, then it refuses without writing.

## Implementation Notes

- Reimplemented with tracked-field-preserving rechecks and in-memory exact UTC cutoff comparisons. The build passed; 183 service tests and 220 LocalDB integration tests passed without skips. Issue #34 work remains in the preserved mixed stash, not the feature diff.

## Plan Change Log

- Review loop 1: unconditional EF reload could erase the decline caller's unsaved review metadata. Recheck locked database state while preserving caller modifications to tracked registration fields, and test that behavior. Add SQL-backed clock boundary tests one tick before, at, and after a datetime2(3) start. KEEP ordered locking, cancellation idempotence, safe per-row audit, rollback, QR clearing and no promotion. Do not restore unrelated issue #34 changes into this feature.

## Review Triage Log

- Quick review — medium, bad_plan: `RegistrationRepository` reloads tracked registration entities, potentially discarding unsaved review metadata written by a caller in the same transaction. The service is intended for the future decline caller, which must be able to record its review without losing it.
- Quick review — maybe-false, unverified: the upcoming SQL query may round an injected time just before the start because the mapped column is `datetime2(3)`; a SQL-level parameter-precision test is needed to establish the behavior.
- Quick review (second pass) — false: mismatched `MemberRequested` causes can bypass required reasons in bulk methods, but the specified future event and deactivation callers use `EventCancelled` and deactivation causes respectively; no reachable caller supplies that mismatch. Validation for those required causes rejects blank reasons.
- Quick review (second pass) — false: a caller catching audit failure and committing can persist partial changes, but the caller-owned unit-of-work contract requires exceptions to propagate and roll back; no caller swallowing the exception was demonstrated. Integration tests verify rollback via that contract.

## Design Notes

Deactivation preview can become stale; the mutation must independently discover and recheck. The user lock serializes signup/deactivation for that owner, while event locks serialize competing attendance and registration changes. No automatic promotion follows a released seat.

## Verification

- `dotnet build` — succeeds.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — succeeds, including fake-clock preview and cutoff cases.
- With `ATTENDTA_TEST_SQL` set to an approved local SQL Server master connection, `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — succeeds, including real locking, rollback and audit assertions.
