# ADM-01 Phase 1 handoff

## Integration quick reference

| Question | Summary |
| --- | --- |
| Status | Implemented foundation on the ADM feature branch; historical test run, not a live integrated endpoint. |
| Provides | Service interface, safe DTOs, pure rules and isolated tests; no concrete service or DI binding. |
| Depends on | Canonical Data, trusted current actor, shared cancellation and audit in one transaction. |
| Connect or adapt later | Keep tested rules and fixtures; add real loaders/mapping and orchestration. Reconcile the cancellation predicate with its owner. Replace feature-local constants/paging only with compatible shared equivalents. |
| Completion evidence | Re-run unit tests on the chosen commit; add real SQL and HTTP/session tests. The recorded 38 passes do not prove integration. |

Shared references: [coding standards](../CODING_STANDARDS.md), [integration tutorial and handoff checklist](../INTEGRATION_GUIDE.md), [active plan index](README.md). These summaries do not authorize deferred implementation or supersede explicit scope decisions.

Status: independent contracts and business rules implemented; persistence and web integration pending.

## What exists

- Services/Interfaces/IAccountAdministrationService: proposed use cases only; deliberately no implementation/DI binding.
- Services/ServiceModels/AccountAdministration: explicit safe request/response contracts, required-state booleans and fixed 25-row paging normalization.
- Services/AccountAdministration: pure decisions for authorization eligibility, role/status validation, stale edits, no-ops, last-admin protection and future-registration cancellation eligibility.
- tests/ASI.Basecode.AdminRoles.UnitTests: isolated test project. No solution/project changes to existing shared files.

All service paths are under ASI.Basecode.Services. Snapshots are decision inputs, not duplicate EF entities. The future caller must load trusted current snapshots inside its transaction. The caller must compute anotherActiveAdminExists excluding the target, using active admins only. These pure functions cannot authenticate a caller or provide database locks.

## Validation

On 2026-11-02, the test command built the Services project and dependencies and passed 38 tests, with zero failures/skips. One test additionally covers 192 registration/event/time/attendance combinations. Tests use fixed times; invalidated attendance maps to HasValidAttendance=false while its historical row must remain in storage.

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/ASI.Basecode.AdminRoles.UnitTests/ASI.Basecode.AdminRoles.UnitTests.csproj
```

Existing restore warning: AutoMapper 14.0.0 reports NU1903. No dependency upgrade was made in this isolated slice. Initial disk-space restore failure was resolved on retry after space became available.

## Integration requirements

1. Reuse the schema teammate's v2.1 entities and seeded lookup mappings. Do not assume these string role/status codes are numeric FK values.
2. Implement the use-case service with trusted current account reads, coordinated locking, expected-state checks, and one transaction for account/cancellation/audit changes.
3. Reconcile RegistrationCancellationRules with the registration owner's shared operation; do not ship two cancellation engines. Its Changed result means eligible for cancellation, not that any write occurred.
4. Preserve valid attendance; invalidated attendance does not prevent an otherwise eligible future cancellation, but its row/history remains intact.
5. Stamp actor/update fields and append safe audit entries exactly once. NormalizedReason is returned for successful account decisions; invalid reasons are rejected before writing.
6. Complete signed-cookie/current-role enforcement, HTTP authorization and antiforgery in Phase 3. No endpoint or fake persistence is registered now.
7. Add SQL rollback/concurrency and HTTP/session tests. Current tests do not prove SQL lock safety, QR invalidation, or session rejection.

## Small differences from the planning inventory

Test inputs are local named values rather than a separate AccountRuleSamples fixture because no reusable fixture is needed yet. Page input is a string so malformed HTTP values can later normalize without automatic integer-binding rejection; last-page normalization requires the authorized filtered total count. Reason limit is 1,000 raw characters, then trimmed for use.

No shared data, authentication, DI, UnitOfWork, solution or existing project file was modified. Nothing has been committed or pushed as part of this handoff.
