---
title: 'Verify real demo-host readiness and close provisioning evidence gaps'
type: 'feature'
ticket: ''
created: '2026-11-06'
status: 'built'
baseline_revision: '04c6d9ec794478450da8b9b68a9edb1c297106a3'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 8.1 shipped provisioning and readiness tooling, but no real demo-host run proves it, and the validator misses four gaps: it checks fixed server roles only, not direct or custom-role privileges; it checks table-level but not column-level `activity_logs` UPDATE; it probes `logs/` instead of `logs/<yyyyMM>`; and it launches Chromium without proving PDF rendering.

**Approach:** Close those gaps with tests, align the guide, and produce a dated checklist that records evidence or names each missing host input as a blocker. Never claim real-host or offline readiness from an online run.

## Boundaries & Constraints

**Always:** Keep the runtime identity least-privilege (SELECT/INSERT only on `activity_logs`, no database roles, no extra server rights); preserve the append-only trigger; validate with the actual login and OS identity; use disposable test databases; redact secrets; keep 8.1 history untouched.

**Never:** reset or overwrite a database; touch shared or production systems; disable the trigger; grant broad roles; implement HTTPS, phone scanning, or backup/restore (8.2/8.3); claim offline or host readiness without a disconnected host run.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Elevated login | Direct or custom-role server privilege (e.g. ALTER ANY LOGIN) | SQL check FAILs before the audit probe | Nonzero exit; no secrets printed |
| Column grant | `GRANT UPDATE (col) ON dbo.activity_logs` | Audit check FAILs before the transaction | No insert attempted |
| Log dir | `logs/` exists, `logs/<yyyyMM>` absent or unwritable | Month dir is created and probed, else FAIL | Missing `logs/` fails without auto-creating it |
| Offline browser | Staged Chromium, host disconnected | Chromium launches and renders a non-empty PDF | Nonzero exit |
| No real host | Host, identity, or secrets unavailable | Checklist row is BLOCKED with the missing input named | No PASS claimed |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Readiness/DemoReadiness.cs` — add server-permission, column-level, month-log, and PDF checks.
- `ASI.Basecode.WebApp/Startup.Logger.cs` — writes `logs/<yyyyMM>`; do not change.
- `database/provision-demo-runtime.sql` — add the column-level guard inside the existing impersonated check; keep rollback.
- `database/attendta-mvp-schema.sql` — `activity_logs` columns and append-only trigger; do not change.
- `tests/ASI.Basecode.IntegrationTests/DemoReadinessSqlTests.cs` — server and column cases.
- `tests/ASI.Basecode.IntegrationTests/DemoReadinessStorageTests.cs` — month-log cases.
- `tests/ASI.Basecode.IntegrationTests/DemoReadinessCoordinatorTests.cs` — check-name expectations.
- `tests/ASI.Basecode.IntegrationTests/SqlServerDatabaseFixture.cs` — `MasterConnectionString` for temporary logins.
- `docs/demo-host-setup.md` — align operator steps and expected PASS checks.
- `docs/demo-host-readiness-evidence.md` — new dated checklist.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.WebApp/Readiness/DemoReadiness.cs` — add `CheckServerPermissionsAsync` rejecting any effective server permission outside `CONNECT SQL`/`VIEW ANY DATABASE` (`sys.fn_my_permissions(NULL,'SERVER')`, which covers direct and custom-role grants); add column-level UPDATE rejection for every `activity_logs` column; require `logs/`, then create and probe `logs/<yyyyMM>`; render a minimal non-empty PDF in `CheckBrowserAsync`.
- [x] `database/provision-demo-runtime.sql` — add the same column-level rejection to the `EXECUTE AS USER` verification.
- [x] `tests/ASI.Basecode.IntegrationTests/DemoReadinessSqlTests.cs` — a temporary clean login passes; direct `ALTER ANY LOGIN` and a custom role granting it fail via `EXECUTE AS LOGIN` (not SQL-auth connections, which LocalDB may reject); column-level `UPDATE (description)` fails before a transaction. Drop login/role in `finally`.
- [x] `tests/ASI.Basecode.IntegrationTests/DemoReadinessStorageTests.cs` — month dir is created/probed; missing `logs/` fails without being created; no probes remain.
- [x] `tests/ASI.Basecode.IntegrationTests/DemoReadinessCoordinatorTests.cs` — update check-name expectations if the browser/PDF label changes; keep the no-false-offline assertion.
- [x] `docs/demo-host-setup.md` — document the server/column/month/PDF checks; link the checklist.
- [x] `docs/demo-host-readiness-evidence.md` — dated checklist with host/runtime fields; rows for DB connect, server denial, table/column audit denial, rollback probe, SetupRoot/logs, offline Chromium/PDF, localhost login, Argon2 benchmark, runtime/secrets, and the host/address/certificate decision owner (phone scanning stays 8.2); unverified rows BLOCKED with the missing input; closed #74 is not proof.

**Acceptance Criteria:**
- Given a runtime login with a direct or custom-role server privilege, when the SQL check runs, then it fails before any audit insert.
- Given a column-level UPDATE grant on `activity_logs`, when the audit check runs, then it fails before a transaction starts.
- Given writable `logs/` but no `logs/<yyyyMM>`, when storage is validated, then the month dir is created and probed with no probe left; when `logs/` is absent, validation fails without creating it.
- Given a staged browser on a disconnected host, when the browser check runs, then Chromium renders a non-empty PDF.
- Given no real host, when the checklist is produced, then every unverified row is BLOCKED with the missing input named and no PASS recorded.

## Implementation Notes

- Added effective server-permission rejection (`sys.fn_my_permissions`), column-level `activity_logs` UPDATE rejection, required `logs/` plus `logs/<yyyyMM>` creation/probe, and headless Chromium PDF rendering to the readiness checks; aligned the provisioning script and docs.
- Exposed `DemoReadiness.CheckBrowserAsync` to the internals-visible test assembly and added `StagedChromiumLaunchesAndRendersNonEmptyPdf`, which fails if Chromium cannot launch or render a non-empty PDF.
- Copied `docs/demo-host-readiness-evidence.md` into the integration test output and added `ChecklistLeavesUnverifiedRowsBlockedWithoutPassOrIssueProof` to guard the no-real-host matrix row: every current unverified check row must be `BLOCKED` with a non-empty missing-input note, the document must state that `PASS` requires recorded host evidence, and closed issues are not proof.

## Plan Change Log

## Review Triage Log

- Quick: 0 high, 4 medium, 2 low, 0 false, 0 maybe-false.
  - medium / patch — `docs/demo-host-readiness-evidence.md:22` — the Application revision row records the planned baseline (`04c6d9e`) as if it were host evidence and is not `BLOCKED`; it does not identify the deployed revision. Fix: mark the deployed revision `BLOCKED` with the missing host input named, keeping the planned baseline as context.
  - medium / patch — `tests/ASI.Basecode.IntegrationTests/DemoReadinessCoordinatorTests.cs:129-136` — `Assert.DoesNotContain("| PASS |", text)` will fail as soon as the checklist legitimately records a host PASS, and `Assert.Contains("BLOCKED", text)` is satisfied by the prose. Fix: assert every current unverified check row is `BLOCKED` with a non-empty missing-input note and that the document states the PASS rule, without forbidding future PASS rows.
  - medium / patch — `database/provision-demo-runtime.sql:35-38` — the new column-level guard is never executed by a test; the only script test stops at the 51100 database-name guard. Fix: add a test that runs a database-name-substituted copy against a disposable database with a column-level UPDATE grant and asserts THROW 51104 with no permission changes.
  - low / rejected — `tests/ASI.Basecode.IntegrationTests/DemoReadinessSqlTests.cs:107,128,150,244-259` — `CreateTemporaryLoginAsync` creates the login before the test's `try`; a failure while creating the user/grants would leak the login. Rejected: the failure is unlikely in everyday use and the fix adds cleanup complexity, not a direct correction.
  - medium / patch — `ASI.Basecode.WebApp/Readiness/DemoReadiness.cs:59-68` — the identity check rejects only `db_owner`, `db_ddladmin`, `db_securityadmin`, and `db_accessadmin`; a `db_backupoperator` or custom-role member passes, contradicting the plan's "no database roles" boundary and the guide's "absence of fixed ... database roles" claim. Fix: reject any database role membership (matching `database/provision-demo-runtime.sql`) and test a `db_backupoperator` member.
  - low / rejected — `ASI.Basecode.WebApp/Readiness/DemoReadiness.cs:130-133` — `ValidateStorage` probes `logs/<current month>` while the app writes to the month directory created at startup; a month rollover can probe the wrong directory. Rejected: the rollover is unlikely during a demo and the fix adds directory enumeration, not a direct correction.

## Design Notes

Effective permissions catch grants that `IS_SRVROLEMEMBER` misses. Column-level UPDATE leaves object-level UPDATE at 0. `Directory.CreateDirectory` would silently create a missing `logs/`, so check the parent first. PDF output works only in headless Chromium, matching the existing launch.

## Verification

**Commands:**
- `dotnet build` — succeeds.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — passes.
- `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` with `ATTENDTA_TEST_SQL` set to a local master connection — passes.

**Manual checks:**
- The checklist shows no PASS for any row without host evidence; issue #150 is claimed on GitHub by a human before merge.
