# ADM-01: Independent Backend Work and Shared-Data Integration Plan

## Integration quick reference

| Question | Summary |
| --- | --- |
| Status | Active execution plan; independent Phase 1 exists on its feature branch. |
| Provides | IAccountAdministrationService, safe account DTOs, account rules and a cancellation-eligibility reference. |
| Depends on | Canonical Data, current Auth, append-only audit and Registration-owned cancellation. |
| Connect or adapt later | Keep rules/DTOs; map canonical entities to inputs, add the real service/repository, reuse shared cancellation in one transaction, then connect controller/DI. Do not build a second cancellation engine. |
| Completion evidence | Unit tests cover supplied facts; SQL tests must prove last-admin protection and rollback, HTTP tests current access/CSRF. Detailed integration: sections 8-10. |

Shared references: [coding standards](../CODING_STANDARDS.md), [integration tutorial and handoff checklist](../INTEGRATION_GUIDE.md), [active plan index](README.md). These summaries do not authorize deferred implementation or supersede explicit scope decisions.

> **Active ADM-01 execution plan.** Phase 1 has since been implemented on its feature branch; persistence and HTTP integration remain pending. Planning-time inventory/status statements below are historical. See [the plan index and review](README.md) and the Phase 1 handoff for evidence limits. This plan supersedes the earlier full-scope plan wherever they differ, not merely its immediate execution order.

- Branch: `feature/admin-manage-roles`
- Owner: Pinca, Member 5
- Date: 2026-11-02
- Status: Proposed implementation plan; documentation only.
- Baseline: Reduced WBS, revised MVP v2.1 ERD and reread v2.1 Markdown requirements.
- Revision: 1.1 - reconciled with the workspace Markdown updated on 2026-11-02.
- Immediate implementation boundary: Phase 1, independent contracts/rules/tests.

## 1. Purpose and relationship to the earlier plan

A teammate is preparing the SQL scripts and shared data models but has not pushed that work yet. This plan allows meaningful ADM-01 progress without competing with those changes. It changes implementation order and ownership boundaries, not the final feature requirements.

This document supersedes the execution sequence and file-ownership schedule in `ADM-01_Admin_Manage_Roles_Backend_Plan.md`. The earlier document remains the full feature reference. When they differ about what to implement now, follow this phased plan. Do not begin with the earlier plan's SQL changes, User/DbContext edits or authentication changes.

Phase 1 produces independently tested contracts and business decisions. It does not produce working production account-management endpoints. A service interface can be defined now; the complete transaction-executing service follows when persistence is available. No implementation is performed by creating this document.

## 2. Context, sources and scope

Repository: `F:\AttendTaBakcend\AttendTa`. Requirements are outside that repository, under `F:\AttendTaBakcend\docs`.

Use these sources in order:

1. User instructions and `WBS_Account_Name_Application_Name_yyyymmdd.pdf`, the reduced WBS.
2. Revised MVP v2.1 ERD: `C:\Users\cruiz\.codex\attachments\80a59153-8dec-4d7c-9b16-5807145bce3e\Pasted text.txt`.
3. Compatible details in the system specification (especially section 5), user stories and sequence diagrams. Their older archive, automatic-promotion or registration-opening behavior does not override the WBS.
4. `git_qa_guidelines.pdf` for delivery and review.

Attachment paths are local provenance, not build dependencies for teammates. The team has agreed to the ERD; do not request it again. Physical lookup names, mappings and transaction conventions are still implementation details to inspect when the teammate pushes.

### Final ADM-01 scope

- Active-admin-only account list, fixed at 25 rows per page.
- Assign exactly one fixed role: admin, officer or member.
- Preview deactivation impact; deactivate/reactivate with a reason.
- Enforce current role and account active state, including existing sessions.
- Protect the last active administrator under simultaneous changes.
- Cancel applicable future registrations and invalidate their QR credentials.
- Preserve account identity, optional member profiles, attendance and historical participation.
- Reactivation does not restore registrations, seats, QR credentials or priority.
- Record actor/time metadata and append-only audit entries atomically with changes.
- Validation, authorization, CSRF and meaningful tests.

No frontend, authentication rewrite, account approval, archive/restore, hard deletion, profile images, custom permission system, report exports, certificate implementation, automatic promotion or reapplication after cancellation. No required rowversion/auth-version field, session table, new Domain project or architecture migration.

### Verified starting point

At planning time the repository still has starter User/EF/authentication code and no shared participation models. The earlier implementation attempt was reverted. The existing plan is the only untracked file before this new plan. No actual database schema has been verified. A diagram is not evidence that SQL tables exist.

Projects target net9.0. PATH previously selected the older D: dotnet installation; use `C:\Program Files\dotnet\dotnet.exe` and do not downgrade targets.

## 3. Architecture and dependency direction

| Familiar layer | Existing project | Responsibility |
| --- | --- | --- |
| Web | WebApp | HTTP requests/responses, authorization integration, eventual Razor views. |
| Application | Services | Use cases, DTOs, business decisions and orchestration. |
| Domain | No separate project | Rules live primarily in Services; entities are database-oriented Data models. |
| Infrastructure | Data and existing auth components | EF Core, repositories, SQL Server, transactions and authentication mechanisms. |
| Shared resources | Resources | Shared constants and text; not entity definitions. |

Services currently references Data and Resources. Do not add Data -> Services references: that creates a circular dependency. Phase 1 code will avoid Data entities even though the project already references Data.

Pure decision inputs are small immutable C# snapshots. They are not duplicate User/Registration entities: no passwords, persistence attributes, EF navigation properties or DbSets. The later service maps canonical entities to those inputs.

Do not prematurely create application-layer repository ports requiring a redesign of the existing dependency graph. Document required database operations now; reconcile repository interfaces in Data during Phase 2, following existing conventions.

## 4. Phases and edit boundaries

| Phase | Work allowed | Deferred |
| --- | --- | --- |
| 1: Independent foundation | New feature contracts, pure rules and isolated unit tests. | Shared production files, SQL, entities, DbContext, repositories, UnitOfWork, authentication, DI, controllers. |
| 2: Persistence integration | Concrete service, feature repositories and coordinated transaction support using teammate data work. | Claims of working sessions/QR until real integration is verified. |
| 3: Web/security integration | Controller, DI, current-account checks, coordinated auth hooks and HTTP/session tests. | Frontend and other feature modules. |

Pushing a phase is valid, but label it partial work. Do not describe tested rules as a completed ADM-01 feature.

## 5. Phase 1 planned file structure

Paths are relative to the actual repository. Only the planning document is created now; all C# and test paths below are proposed additions.

```text
AttendTa/
|-- docs/implementation-plans/
|   |-- ADM-01_Admin_Manage_Roles_Backend_Plan.md           existing reference
|   `-- ADM-01_Independent_Backend_And_Integration_Plan.md new active plan
|-- ASI.Basecode.Services/
|   |-- Interfaces/
|   |   `-- IAccountAdministrationService.cs
|   |-- ServiceModels/AccountAdministration/
|   |   |-- AccountListRequest.cs
|   |   |-- AccountSummaryResponse.cs
|   |   |-- AccountPageResponse.cs
|   |   |-- ChangeAccountRoleRequest.cs
|   |   |-- ChangeAccountStatusRequest.cs
|   |   |-- DeactivationImpactResponse.cs
|   |   `-- AccountChangeResponse.cs
|   `-- AccountAdministration/
|       |-- AccountAdministrationCodes.cs
|       |-- AccountRuleInputs.cs
|       |-- AccountRuleDecision.cs
|       |-- AccountAdministrationRules.cs
|       `-- RegistrationCancellationRules.cs
`-- tests/ASI.Basecode.AdminRoles.UnitTests/
    |-- ASI.Basecode.AdminRoles.UnitTests.csproj
    |-- RequestValidationTests.cs
    |-- AccountAdministrationRulesTests.cs
    |-- RegistrationCancellationRulesTests.cs
    |-- ResponseContractTests.cs
    `-- Fixtures/AccountRuleSamples.cs
```

The SDK-style Services project automatically includes new C# files, so no Services.csproj change is expected. Tests run by their project path; do not edit the shared solution in Phase 1. Use net9.0, xUnit and compatible test SDK/runner versions selected during implementation. No new production dependency is expected. No EF provider or SQL/web host is needed for these tests.

### File-by-file responsibilities

| File | Purpose and boundary |
| --- | --- |
| `IAccountAdministrationService.cs` | Async list, preview, change-role and change-status contracts, with trusted actor ID, target ID where needed, explicit DTOs and CancellationToken. No implementation or DI registration in Phase 1. |
| `AccountListRequest.cs` | Page default 1; normalize invalid/missing pages to 1 and beyond-last pages to the last available page, without overflowing offset arithmetic. Fixed size 25, no user-selected page size. Necessary account search/filter fields are an integration contract to agree with the shared list helper; do not silently add advanced filtering or treat the shared search requirement as excluded. Reuse for impact paging if suitable. |
| `AccountSummaryResponse.cs` | Account ID, full name, email, semantic role and active state only. Never return a User entity. |
| `AccountPageResponse.cs` | Safe summaries, current page, fixed size, total count and total pages. |
| `ChangeAccountRoleRequest.cs` | Requested role, expected current role and reason. No actor ID, password or membership fields. |
| `ChangeAccountStatusRequest.cs` | Required requested/expected active states and reason. Missing boolean must be distinguishable from false. |
| `DeactivationImpactResponse.cs` | Calculation time, target state, full counts by registration status, and a 25-row safe detail page. May include its small detail record in the same file. No QR tokens. |
| `AccountChangeResponse.cs` | Target/resulting state, changed/no-op indication and actual cancellation counts. No claim that preview freezes state. |
| `AccountAdministrationCodes.cs` | Feature role strings and stable rule failures, e.g. InvalidRole, ReasonRequired, StaleAccountState, LastActiveAdministrator. No assumed numeric SQL IDs. Reconcile existing shared constants during integration. |
| `AccountRuleInputs.cs` | Immutable actor/target state and another-active-admin indicator; registration status, event status/start and valid-attendance inputs. Not public mutation requests or persisted entities. |
| `AccountRuleDecision.cs` | Allow/reject/no-op and business failure code. No HTTP status or database exceptions. Use simple types, not a generic rules framework. |
| `AccountAdministrationRules.cs` | Validation, actor eligibility, expected-state comparison, no-op and last-admin decisions. No queries or implicit system-clock calls. |
| `RegistrationCancellationRules.cs` | Pure eligibility predicate with explicit trusted UTC time, developed as the contract reference to reconcile/reuse with the registration owner. No mutations, token generation, SQL or promotion; not a separate production cancellation engine. |
| `RequestValidationTests.cs` | Missing/invalid fields, reason boundaries, omitted booleans, page normalization and expected-state validation. |
| `AccountAdministrationRulesTests.cs` | Actor/target combinations, self-changes, last admin, stale state and no-op. |
| `RegistrationCancellationRulesTests.cs` | Entire registration/event/time/attendance decision table. |
| `ResponseContractTests.cs` | Meaningful safe-serialization and pagination behavior tests; avoid tests that only repeat property definitions. |
| `AccountRuleSamples.cs` | Named scenario inputs, no real identities, connection strings or secrets. |

Do not create a stub production service that throws NotImplementedException or returns fake successes. No production test substitutes or mock DI bindings. The honest Phase 1 deliverable is rules plus contracts.

## 6. Phase 1 business behavior

### Account-change rules

- Role codes exactly admin/officer/member; reject unsupported values/casing.
- Reason required, trimmed and nonblank; proposed maximum 1,000 characters. Apply one consistent length policy at service and HTTP boundaries.
- Positive account/actor IDs. The eventual controller supplies actor ID from validated authentication, never the request body.
- Verify actor is an active admin before considering even a no-op request.
- Compare expected current role/active state with current state. Reject stale edits rather than overwrite another admin silently.
- When an active admin would be demoted/deactivated, another active admin must remain. Inactive admins do not count.
- Self-demotion/deactivation is allowed if this protection passes.
- Matching requested/expected/current states are a no-op: no metadata rewrite, repeated cancellation or change audit.
- Expected-state fields are not database version columns; they do not detect ABA changes away and back. Full version-history detection is out of scope.
- Role changes alone do not cancel participation; account and membership active states are separate.

### Registration cancellation rules

Future means starts_at > operationTimeUtc. Equality means started. Pass time explicitly into rules/tests.

| Situation | Decision on deactivation |
| --- | --- |
| Future draft/published event; pending/waitlisted/confirmed; no valid attendance | Eligible for cancellation. |
| Valid attendance exists | Preserve registration/attendance, per revised specification section 5. |
| Only invalidated attendance exists, with otherwise eligible future registration | Cancellation is allowed; retain the invalidated attendance row and its correction history. Never restore check-in eligibility. |
| Event starts now or earlier | Preserve, including unresolved pending/waitlisted history. |
| Completed/cancelled event regardless of schedule inconsistency | Preserve. |
| Already-cancelled registration | Leave unchanged. |
| No member profile | No participation updates; account operation can still succeed. |
| Unknown/invalid required status or missing decision input | Reject invalid input; do not assume it is safe to mutate. |

Draft events ordinarily have no registrations; this defensive handling does not authorize draft signup. Future cancellation eventually clears QR and releases confirmed capacity by status change, never promotion. Reactivation does not reverse cancellation. Historical attendance/certificates remain untouched.

### Paging and preview

Fixed size 25; stable unique ID ordering. Empty dataset means zero total pages. Normalize missing/invalid pages to 1 and requests beyond the last page to the last available page. For an empty dataset use page 1, zero total pages and empty items. Bound/clamp before calculating offsets to avoid overflow. Impact totals cover the full eligible set, not one page. Preview is advisory; recompute at execution and return actual results.

## 7. Phase 1 task sequence and exit criteria

1. Reinspect the checkout for new teammate/user changes and applicable instructions.
2. Add DTOs and service interface; explain what crosses each boundary.
3. Add small rule inputs, decisions and semantic codes without persistence coupling.
4. Implement account validation/decisions and registration eligibility rules.
5. Add isolated unit tests for meaningful combinations and boundary cases.
6. Build Services and run tests by project path; inspect for unintended shared-file edits.
7. Report exact files, tested rules and capabilities still deferred.

Exit criteria: new contracts/rules compile, unit tests pass, shared production files remain untouched, no endpoint/fake persistence is registered. SQL atomicity, true concurrency, login/session enforcement, CSRF and QR validation remain explicitly unverified.

## 8. Phase 2 - shared-data handoff and persistence

Begin after the teammate's schema/model work is available for inspection and the integration contract is reconciled. Do not ask for the ERD again or independently recreate their database.

### Handoff checklist

Inspect the actual branch/PR and scripts for:

- Canonical user/member/event/registration/attendance/activity-log mappings and names.
- Lookup table names, key types, seeded role/status codes and FKs. A semantic role string is not necessarily the value stored in an FK column.
- Numeric users.user_id versus the starter User.UserId string.
- Revised v2.1 creation/update actor fields, nullability and UTC datetime2(3) columns.
- Existing repository and UnitOfWork APIs, including methods that save immediately.
- Database setup instructions, application permissions and isolated testing prerequisites.
- Shared constants that should replace feature-local role codes without creating duplicate definitions.
- Required unique constraints, including member/event registration uniqueness and filtered nullable QR uniqueness.

The data teammate owns schema creation/canonical mappings. Provide a requirements checklist or coordinated narrow patch for missing pieces. Do not publish a competing ten-table SQL script. Scripts are not applied to a shared database simply because they appear in a plan.

### Required data operations to reconcile

| Operation | Required guarantee |
| --- | --- |
| Read current actor/account | Trusted numeric ID and current role/active state, not cached claims. |
| List/count accounts | Safe projection, consistent visibility and stable 25-row pagination. |
| Load target for change | Current state inside the chosen lock/transaction protocol. |
| Determine another active admin | Same transaction/protocol as the write; inactive admins excluded. |
| Read optional member link | Unique account linkage; no member creation or membership updates. |
| Preview/select future registrations | Equivalent to tested rule table; SQL filtering/paging rather than loading all rows for preview. |
| Cancel affected registrations | Status, QR clearing, cancellation metadata and update metadata; preserve creation/review/confirmation/snapshots. |
| Append audit | No independent commit and no update/delete API. |
| Save/commit | All account, registration and log writes share one context/transaction. |

C# decision rules and SQL predicates can diverge. Real database tests must exercise the same scenario table against the SQL selection. A passing pure predicate test is not proof of correct queries.

### Planned Phase 2 files

Paths below are proposed, not permission to duplicate an equivalent shared abstraction.

| File | Action |
| --- | --- |
| `ASI.Basecode.Services/Services/AccountAdministrationService.cs` | Add full use-case implementation using tested rules and real Data contracts. |
| `ASI.Basecode.Data/Interfaces/IAccountAdministrationRepository.cs` | Add only if existing interfaces do not reasonably supply these feature operations. |
| `ASI.Basecode.Data/Repositories/AccountAdministrationRepository.cs` | Real account persistence/lock operations; no independent save. |
| `ASI.Basecode.Data/Interfaces/IAccountParticipationRepository.cs` | Prefer the registration owner's shared cancellation contract; add a narrow adapter contract only if needed. |
| `ASI.Basecode.Data/Repositories/AccountParticipationRepository.cs` | Only add a thin data adapter if the registration owner needs one. Reuse that owner's shared cancellation operation; do not independently implement seat/cancellation rules. |
| `ASI.Basecode.Data/Interfaces/IActivityLogRepository.cs` and `Repositories/ActivityLogRepository.cs` | Reuse an existing append-only writer if available; otherwise add one coordinated implementation. |
| Shared `IUnitOfWork.cs` / `UnitOfWork.cs` | Focused edit only if async save/explicit transactions are missing; preserve current callers. |
| Shared models / `AsiBasecodeDbContext.cs` / SQL scripts | Reuse teammate work; any correction is coordinated with an explicit diff. No blanket regeneration. |
| `tests/ASI.Basecode.AdminRoles.IntegrationTests/ASI.Basecode.AdminRoles.IntegrationTests.csproj` | New relational integration test project. |
| `Infrastructure/SqlServerFixture.cs` inside that test project | Isolated test database from reviewed schema; explicit connection configuration and safe cleanup. |
| `AccountAdministrationPersistenceTests.cs` | Real queries, paging, cancellation fields, metadata and history preservation. |
| `AccountAdministrationTransactionTests.cs` | Rollback on cancellation/audit/save failure; audit immutability. |
| `AccountAdministrationConcurrencyTests.cs` | Independent connections/contexts for last-admin and participation races. |

Do not introduce a new context or second User/Registration entity to avoid mapping differences. Services maps canonical entities/query results to rule inputs; Data does not reference Services DTOs.

### Transaction workflow

1. Start a short transaction and acquire the administration lock before reading actor/target/admin count.
2. Reload trusted current actor/target and enforce authorization, expected state and last-admin protection.
3. Capture trusted UTC time after relevant lock acquisition; recheck time-sensitive cutoffs if later contention could make an earlier timestamp stale.
4. Select and lock eligible registration/event/attendance facts using the coordinated writer protocol.
5. Invoke the registration owner's shared cancellation operation for selected registrations and QR clearing within this same transaction; do not duplicate its writes.
6. Update the account role or active state as requested, plus update metadata.
7. Append one account log; ensure the shared cancellation operation appends exactly one cancellation log per affected registration. Assign log ownership explicitly to avoid duplicate entries. All logs use the same transaction.
8. Save/commit together. Any failure rolls back the operation; no success response before commit.

Proposed SQL Server approach: transaction-owned sp_getapplock resource `AttendTa.AccountAdministration`, bounded timeout, serializable isolation/appropriate row-range locking. Finalize against the teammate's schema and existing writers, not by assuming the earlier proposal is already proven.

All account role/status writers must follow the same protocol. Coordinate account -> event -> registration lock ordering and eligibility rechecks with signup, review, promotion and check-in writers. An administration-only application lock cannot make unrelated writers safe. Deadlocks/timeouts produce controlled retry/conflict responses; do not retry nontransactional side effects.

### Revised ERD metadata and audit requirements

- Set users.updated_by to the authenticated numeric actor ID and updated_at to UTC time on actual role/status changes.
- Cancellations set cancelled_by/at/reason and registration updated_by/at in the same transaction.
- Preserve created_by/at and prior review/confirmation data, academic snapshots, attendance and certificates.
- Account update metadata is nullable in the ERD until updated; registration created_by is required. Fixtures supply valid creation actors, not invented production history.
- No-op requests do not stamp metadata or create change logs.
- Keep actor/history FKs non-cascading; deactivation is an UPDATE and preserves even self-referencing actors.
- activity_logs stores actor, action, affected entity/ID, reason, UTC time and whitelisted old/new values. Use actions account.role_changed, account.deactivated, account.reactivated and registration.cancelled.
- Never serialize whole entities, passwords, hashes, cookies, bearer tokens, QR tokens or physical storage paths into logs. Validate old/new payloads as JSON and validate the polymorphic entity type/ID in application logic. A QR-invalidated boolean is sufficient.
- updated_by/at captures only the latest update and cannot replace append-only history. It is not a concurrency token in this plan.
- Coordinate SQL permissions and/or an UPDATE/DELETE-blocking trigger for audit immutability. A repository without Delete is not enough by itself.

Phase 2 exit: real persistence, rollback and SQL last-admin concurrency tests pass. Do not claim complete session enforcement until Phase 3.

## 9. Phase 3 - HTTP and security integration

### Planned files

| File | Change |
| --- | --- |
| `ASI.Basecode.WebApp/Controllers/AdminAccountsController.cs` | Thin MVC JSON controller using the service interface. No EF/business-rule code. |
| `ASI.Basecode.Services/Interfaces/ICurrentAccountService.cs` and `Services/CurrentAccountService.cs` | Add only if auth has no equivalent current-account reader. No password validation or token issuance here. |
| `ASI.Basecode.WebApp/Authentication/CurrentAccountValidator.cs` | Add/reuse current-account adapter before authorization. |
| `ASI.Basecode.WebApp/Startup.Auth.cs` | Coordinated cookie hooks and API 401/403 behavior, preserving the auth owner's mechanisms. |
| `ASI.Basecode.WebApp/Startup.DI.cs` | Register real scoped dependencies, clock and antiforgery header; never test substitutes. |
| Integration project `AccountAdministrationHttpTests.cs` | Endpoint contracts, authorization and CSRF with the web host. |
| Integration project `CurrentAccountSessionTests.cs` | Real signed cookie sessions after role/status changes, expiry and invalid signatures. |
| `ASI.Basecode.sln`, `README.md` | Register tests and document setup/usage/dependencies when shared integration is ready. |
| `ASI.Basecode.WebApp/Program.cs` | Conditional small test-host entry point if needed, not a startup rewrite. |

AccountController, SignInManager, UserService, PasswordManager and token providers remain authentication-owner files. If the starter bypass still exists, it is a completion blocker to integrate with its owner, not permission to rebuild authentication independently.

### Endpoint contracts

| Method/route | Contract |
| --- | --- |
| GET `/admin/accounts?page=1` | Safe account summaries and fixed pagination. |
| GET `/admin/accounts/{id}/deactivation-impact?page=1` | Current target, calculation time, full counts and bounded details. |
| POST `/admin/accounts/{id}/role` | Role, expectedRole and reason; returns resulting state/no-op status. |
| POST `/admin/accounts/{id}/status` | isActive, expectedIsActive and reason; returns actual cancellation counts. |
| GET `/admin/accounts/antiforgery` | Authenticated token for backend testing; future Razor can issue tokens normally. |

DTOs prevent overposting. Actor comes from validated claims, not the body. Require antiforgery validation on cookie-authenticated mutations, e.g. X-CSRF-TOKEN header. No mutation on GET. Account, impact and token responses are no-store.

Error mapping: 400 invalid input, 401 absent/inactive authentication, 403 authenticated non-admin, 404 missing target, 409 last-admin/stale/concurrency conflict. Missing integration must not return fake success: do not enable unwired endpoints, or return controlled unavailability where appropriate. Unexpected errors use generic responses without database/configuration details.

### Cross-module contracts

- Authentication supplies real credential checking, stable numeric account-ID claims, signature/expiry validation and inactive-login rejection.
- Current-account checks reject missing/inactive users and replace stale role claims before authorization on supported session schemes. Do not trust old claims indefinitely.
- Registration owns signup/review/promotion and must recheck account eligibility transactionally. Cancelled member/event rows remain unique and cannot be reapplied to.
- Attendance/QR validates online: inactive account or cancelled registration means no new check-in, including saved QR copies. Preserve existing attendance.
- Membership status stays separate; account reactivation does not activate membership.

No frontend is added here. Later Razor work can reuse the use cases/contracts for account lists, forms, previews and validation messages.

## 10. Test matrix and evidence limits

| Behavior | Phase 1 | Phase 2 | Phase 3 |
| --- | --- | --- | --- |
| Validation/role decisions | Pure unit tests | Reuse | HTTP mapping |
| Last-admin protection | Decisions from supplied snapshots | Real concurrent SQL changes | Endpoint/session behavior |
| Cancellation eligibility | Complete decision table | Actual SQL selection/writes | Connected action and QR denial |
| Pagination | Contract/math boundaries | SQL 0/1/25/26/multiple pages | Serialized endpoint results |
| Audit consistency | Not proven | Forced real failures and rollback | End-to-end error handling |
| Existing sessions | Not tested | Not enough alone | Real signed cookies |
| CSRF | Not tested | Not tested | Real web-host requests |
| Cross-module races | Cannot prove | Real independent connections/writers | Integrated scenarios |

Required scenarios include:

1. Unknown/case-mismatched roles; missing/whitespace/overlong reasons; omitted booleans versus false; normalization of malformed, negative, oversized and beyond-last page values without offset overflow.
2. Inactive/non-admin actor, missing target, stale expected state, legitimate repeated no-op.
3. Sole active admin, other inactive admins only, two active admins, self-demotion/deactivation.
4. Two independent connections attempting to remove admin access simultaneously: zero active admins must never result.
5. Future, exact-start and past events; all event/registration states; recorded and invalidated attendance history.
6. No member profile, membership unchanged, original creation/review/confirmation/snapshot metadata retained.
7. Reactivation never restores seats, registrations, QR or priority; no promotion and no cancelled-row reapplication.
8. Cancellation/audit/save failures roll back every related write; audit update/delete rejected.
9. Deactivation versus signup, review/promotion and check-in races; no bypass of committed account eligibility.
10. Safe response/audit serialization with no credentials/tokens, no raw SQL errors.
11. Existing session loses admin privileges after demotion and protected access after deactivation; expiry/signature checks remain intact.
12. Missing/invalid CSRF rejected; valid CSRF succeeds; no state-changing GET.

Real SQL Server or LocalDB is required to prove relational/locking behavior. EF InMemory or sequential fake calls cannot do so. Use an isolated test database with explicit configuration; never reset/drop a shared development database for tests. Report unavailable integration tests as not run rather than silently skipping them and claiming completion.

## 11. Build, review and delivery

Phase 1 commands after implementation:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build ASI.Basecode.Services/ASI.Basecode.Services.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/ASI.Basecode.AdminRoles.UnitTests/ASI.Basecode.AdminRoles.UnitTests.csproj
git -c safe.directory=F:/AttendTaBakcend/AttendTa diff --check
git -c safe.directory=F:/AttendTaBakcend/AttendTa status --short --untracked-files=all
```

Later build the full solution and run SQL/HTTP tests with explicit isolated SQL configuration. Preserve net9.0. Report baseline warnings separately from regressions. Tests exercise production rule code, not a copied implementation in the test project.

Suggested focused conventional commits, when implementation is approved:

- `feat: define admin account management contracts`
- `feat: add account access decision rules`
- `test: cover admin role and cancellation rules`
- Later: persistence/transaction integration, then endpoint/security integration with tests.

Push only the feature branch; PR target is team dev. CI, self-review and at least one peer approval precede merge. No direct main/dev push. Phase 1 can be a draft PR or explicitly partial reviewed PR; describe its limited scope honestly. Do not automatically merge a teammate's whole unfinished branch merely to acquire scaffolding.

## 12. Education and teammate communication

At each checkpoint, explain to Pinca:

1. What behavior is being added.
2. Why its file belongs in Services, Data or WebApp.
3. What data crosses boundaries and who supplies trusted values.
4. What the tests prove and what still needs integration.
5. Whether a shared file will need a coordinated edit.

Example Phase 1: a DTO carries role/reason, while a pure rule decides whether a supplied current account state allows the change. Neither performs SQL or authenticates the caller.

Example Phase 2: the service reads trusted current state through a repository inside a transaction, passes it into the tested rule, changes canonical entities, and saves the audit entries together.

Suggested message for the user to send:

> I am starting ADM-01 with feature-scoped DTOs, service contracts, pure business rules and unit tests. I will not edit your SQL scripts, entities, DbContext, shared repositories, UnitOfWork or authentication in Phase 1. Once your shared data work is pushed, I will integrate against it. Please retain the revised v2.1 user/registration actor metadata and share your lookup/transaction conventions.

No message is automatically sent by this plan.

## 13. Completion gates and summary

### Gate A: independent foundation ready

Contracts/rules compile; unit tests pass; shared production files remain untouched. No endpoint or fake persistence is registered. Delivery explicitly says SQL, sessions, CSRF, QR and real concurrency are pending.

### Gate B: persistence ready

Canonical shared models are reused. Account changes, cancellation, QR clearing, update metadata and audits commit atomically. SQL paging, rollback and last-admin concurrency checks pass. SQL eligibility matches the tested rule table. Current-session enforcement is still not claimed until Gate C.

### Gate C: ADM-01 backend complete

Real admin endpoints, CSRF and current-session enforcement work. Connected signup/check-in/QR paths respect account state. SQL concurrency, preservation and reactivation constraints are verified. CI and peer review complete before merge.

The immediate work is Gate A. Files in Services and a standalone unit-test project are the only planned production-code/test additions in that phase. The shared SQL, entities, context, repositories, UnitOfWork, authentication, DI, solution and frontend stay untouched. Later phases are documented handoff work, not a reason to silently expand Phase 1.


## 14. Markdown v2.1 reconciliation record (2026-11-02)

Reviewed all three Markdown files in `F:\AttendTaBakcend\docs`: System Specification, User Stories and Sequence Diagrams. They now declare version 2.1; earlier plan references to broader v1.3 behavior are historical. Their prose still references newdocs/original files, but these are the actual workspace copies inspected. User instructions and reduced-WBS precedence remain controlling.

### Changes adopted

1. **Valid attendance, not attendance-row existence** (specification section 5 and stories shared rules): future confirmed registrations without valid attendance can be cancelled even if an invalidated attendance row exists. Preserve that row and correction history. Update rule inputs and SQL predicates accordingly; tests must distinguish absent, valid and invalidated attendance. Pending/waitlisted rows should not ordinarily have attendance; treat inconsistent valid-attendance state conservatively and preserve it.
2. **Page normalization** (specification section 19, US-29): missing/invalid page -> 1; beyond-last -> last page; empty result -> page 1 and zero total pages. Previous/Next state follows normalized results. This replaces the previous reject-invalid/return-empty-beyond-last proposal. Ensure HTTP binding permits intentional normalization rather than accidentally returning automatic 400 for malformed page input. Other malformed business inputs still return 400.
3. **Shared registration cancellation** (specification section 1.4): account deactivation invokes the registration owner's cancellation logic, also reusable for membership inactivity. It participates in the same transaction and must not independently commit. Phase 1's cancellation decision table is a contract/test specification, not a second production cancellation engine: transfer/reuse the rule implementation with the owner during integration. Do not duplicate seat rules across slices.
4. **Cookie authentication baseline** (specification section 4, US-03 and login sequence): signed expiring cookies are the required mechanism. Do not add a new bearer-auth feature because starter classes exist. If a bearer path remains enabled in the integrated app, it must enforce current account state too or be disabled in coordination with its owner.
5. **Audit payload validation** (specification section 17): valid JSON old/new values, real actors for user actions, application-validated entity references, and no physical storage paths. Latest update fields still do not replace audit history.
6. **Shared pagination/auth/audit helpers**: reuse the responsible owners' helpers when available. Phase 1 defines feature contracts only; it does not build another general pagination/auth/audit framework. Search/filter capability remains limited to necessary agreed list requirements; no advanced admin filtering is inferred.

### Additional consistency details

- Valid attendance means an attendance record exists and is not invalidated. Merely checking whether a row exists is insufficient. Preserve the original row even when a future registration becomes cancelled after invalidation.
- Malformed paging normalization belongs at the web boundary; clamping to the last available page requires the authorized filtered count in the service/query layer. A request DTO alone cannot know the last page.
- Ordinary attendance invalidation initially leaves a registration confirmed (specification section 11). A later eligible cancellation is a distinct operation, so this does not conflict with cancelling future rows whose attendance has already been invalidated.
- Cookie enforcement remains mandatory. Bearer tests are conditional only if that path stays enabled; no extra authentication mechanism is introduced.
- Necessary account search/filter behavior is not expressly enumerated in the documents. Keep the final filter set as a small shared-list integration decision, apply authorization/filtering before count/page, and retain query state. This is not a reason to build an advanced filter system in Phase 1.

### Ownership discrepancy and controlling instruction

Specification section 1.4 and the stories assign Accounts and Administration to Member 1, Certificates and Reports to Member 5. Pinca explicitly instructed this chat that Member 5 owns ADM-01/ADM-02 as well as CERT/REP. Preserve that explicit assignment in this plan; do not silently move the work or edit the requirements' ownership table. Flag this discrepancy to teammates before overlapping integration work. The schema teammate's current ownership of SQL/shared data and this plan's Phase 1 isolation remain unchanged.

### Effect on phases and file inventory

No extra Phase 1 production files are required. Update AccountListRequest/response behavior, valid-attendance rule inputs, rule scenarios and validation tests. Phase 2's participation repository entries are conditional adapters to the registration owner's shared operation, not instructions to create an independent cancellation implementation. Full use-case service continues to orchestrate account change plus shared cancellation plus audit in one transaction.

The phased strategy remains appropriate: Phase 1 touches no shared production file; Phase 2 integrates canonical data and shared cancellation; Phase 3 verifies web/security behavior. No frontend, SQL scripts or application changes were performed during this documentation reconciliation.
