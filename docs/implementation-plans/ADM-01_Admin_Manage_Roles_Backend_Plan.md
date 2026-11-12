# ADM-01: Admin Manage Roles and Account Access - Backend Implementation Plan

## Integration quick reference

| Question | Summary |
| --- | --- |
| Status | Superseded historical reference, not an executable plan. |
| Provides | Background on the original full-scope approach only. |
| Depends on | The active phased ADM plan is required before making implementation decisions. |
| Connect or adapt later | Do not implement or replace code from this document. Its older cancellation and paging instructions are superseded; use the active plan and shared integration guide. |
| Completion evidence | No implementation completion may be claimed from this historical plan; use the current handoff and integration gates. |

Shared references: [coding standards](../CODING_STANDARDS.md), [integration tutorial and handoff checklist](../INTEGRATION_GUIDE.md), [active plan index](README.md). These summaries do not authorize deferred implementation or supersede explicit scope decisions.

> **SUPERSEDED - historical reference only.** Use [the phased ADM-01 plan](ADM-01_Independent_Backend_And_Integration_Plan.md) for all current implementation decisions, including later integration. Do not execute the old cancellation, pagination or file-edit instructions below independently. See [the plan index and review](README.md) for current status. Preserved to explain earlier decisions, not a second active plan.

- Branch: `feature/admin-manage-roles`
- Owner: Pinca (Member 5)
- Prepared: 2026-11-02
- Plan revision: 1.1 - adopts the revised MVP v2.1 ERD supplied on 2026-11-02.
- Status: Proposed plan; implementation has not been authorized by this document.
- Repository: `F:\AttendTaBakcend\AttendTa`
- This change adds this planning document only. No application code or database changes.

> Historical full-scope reference: follow `ADM-01_Independent_Backend_And_Integration_Plan.md`, especially its section 14 reconciliation, for the current execution plan. Its valid-attendance cancellation rule, normalized pagination and shared registration cancellation contract supersede the older proposals below.

## 1. Outcome and scope

Build the ADM-01 backend as one vertical slice in the existing ASP.NET Core MVC application. An active administrator can list accounts, assign one of the fixed roles, preview deactivation effects, and deactivate/reactivate an account with a reason. Changes preserve identity and participation history, enforce current account permissions, and write audit records atomically with related updates.

Keep the existing Data, Services, Resources, and WebApp projects, C#, SQL Server, EF Core database-first, dependency injection, and eventual Razor views. Flow: thin MVC controller -> business service -> repositories and Unit of Work -> EF Core / SQL Server. No frontend work in this slice.

The entire team has agreed to the ten-business-table ERD. That is the shared design contract; teammates do not have to finish or merge their features before ADM-01 can progress. Their implementation is not presumed to be present in this checkout. Independently testable work can proceed against agreed contracts; real integration must be verified before declaring the feature complete.

### Requirements precedence

1. User instructions and reduced WBS: `F:\AttendTaBakcend\docs\WBS_Account_Name_Application_Name_yyyymmdd.pdf`.
2. Current ERD baseline: revised MVP v2.1, `C:\Users\cruiz\.codex\attachments\80a59153-8dec-4d7c-9b16-5807145bce3e\Pasted text.txt`. This supersedes the earlier ERD attachment for this plan.
3. Compatible details in `AttendTa-System-Specification.md` (especially sections 4 and 5), `AttendTa-User-Stories.md` (US-05), and `AttendTa-Sequence-Diagrams.md`, under the workspace requirements folder.
4. `F:\AttendTaBakcend\docs\git_qa_guidelines.pdf` for delivery and review.

The Markdown's internal authority claim does not override the user's reduced-WBS instruction. Attachment paths are local provenance; copy approved contract details into team documentation as needed rather than relying on teammates having those paths.

### Decision on revised MVP v2.1 ERD

Adopt the revised ERD for this plan. It retains the same ten business tables, account/member identity relationships, fixed roles and account-active flag, while removing design remnants that conflicted with the reduced MVP. The architecture and planned file inventory do not need to change; mappings, write metadata, SQL preflight checks and test fixtures do.

| ERD change | ADM-01 planning consequence |
| --- | --- |
| Removes `deleted_by`, `deleted_at`, `deletion_reason` and user `image_path` | No archive filters, deletion metadata or user-photo mapping. Active-admin checks use role and `is_active` only. Deactivation reasons belong in `activity_logs.reason` and affected registrations' `cancellation_reason`. |
| Adds `created_by`/`created_at` and `updated_by`/`updated_at` to users and registrations | Preserve creation metadata. Stamp update actor and UTC time for every actual account change and registration cancellation, in the same transaction as audit history. No-op requests do not change metadata. |
| Actor metadata references `users.user_id` | Map nullable/required integer FKs correctly; never write email, OS username or a display-name string into actor columns. Preserve actor references when an account is deactivated. |
| Removes manual registration-opening fields; documents publication opening signup | No query, mapping or test fixture may require `registration_opened_by` or `registration_opened_at`. This does not change ADM-01's future-event cancellation rule. |
| Makes no reapplication, officer-only promotion and start-time history explicit | Confirms the existing plan: cancel without promotion, retain cancelled rows and member/event uniqueness, and preserve unresolved registrations once the event starts. |
| Adds unique-ID tie-breakers to candidate indexes | Reuse/review the shared indexes against real queries; fixed 25-row pagination stays query state, never a persisted field. No new ADM-02 filtering scope. |
| Corrects local image storage and private PDF certificate paths | Better alignment with the reduced MVP, but no image/certificate implementation is added to ADM-01. |

The ERD's reference to user stories does not make older Markdown behaviors authoritative again. Reduced-WBS precedence remains unchanged. ERD comments are design constraints; they do not themselves enforce transactions, permissions, file validation, audit immutability or last-admin protection.

The ERD still intentionally leaves physical lookup-table names and keys to SQL Server implementation. Resolve role/status codes through canonical lookup mappings; do not assume a numeric FK is a literal string or hard-code unagreed numeric IDs.

### Explicit exclusions

- No automatic waitlist promotion or registration restoration.
- No reapplication after registration cancellation.
- No account/member archive or restore features, and no hard deletion.
- No configurable roles, custom permissions, profile pictures, or report exports.
- No certificate/report screens or ADM-02 activity-list endpoint in this feature. Writing the shared audit history is required now.
- No authentication-module rewrite, public signup/approval implementation, password-change workflow, session table, or new frontend framework.
- No mandatory rowversion or auth-version column.
- No changes to event publication behavior: publication opens registration under the reduced WBS.
- Preserve the separate later certificate work: PDF output, and declined eligibility cannot reopen.

## 2. Verified starting point and limits

The checkout was clean when this plan was prepared. No AGENTS.md was found in the inspected workspace/repository. The earlier implementation attempt was reverted; its code is not the basis for this plan.

Current code is starter scaffolding:

- `Data/Models/User.cs` has `Id`, string `UserId`, `Name`, `Password`, and creation/update metadata. It does not yet represent all agreed `users` columns.
- `AsiBasecodeDbContext.cs` currently maps only User.
- UserRepository and UnitOfWork exist; the current repository add method saves immediately.
- AccountController and SignInManager still contain dummy/placeholder authentication paths.
- No business models for members, events, registrations, attendance, or activity logs are present here.
- At the last successful remote refresh, the named team auth/master-data branches still resolved to starter code. This does not describe teammates' unpushed work on their machines.
- The actual SQL Server database schema has not been verified. The ERD is not proof that tables exist.
- Projects target .NET 9. PATH previously resolved to the D: .NET 8 installation; C: has SDKs 9.0.320 and 10.0.401. Use `C:\Program Files\dotnet\dotnet.exe`, and do not downgrade targets.

Use the agreed `users.user_id` integer as account identity. Do not assume the starter's string `User.UserId` means the same thing, or silently create another account table to avoid reconciling it.

## 3. Team ownership and coordination notice

Pinca owns the ADM-01 controller, request/response contracts, service rules, account administration repository, audit writes, deactivation orchestration, and tests.

| Shared area | Intended collaboration |
| --- | --- |
| User model, user-role lookup, EF context | Reuse one canonical database-first account mapping. Coordinate changes with the authentication owner; no second User entity or competing scaffold. |
| Login and identity claims | Authentication owner provides real credential validation, stable numeric account-ID claims, signed expiring sessions, and rejection of inactive accounts. ADM-01 supplies current-account validation integration. |
| Members | Reuse the unique optional `members.user_id` relationship. Do not alter membership status or create/delete profiles. |
| Registrations/events/attendance | Agree on a narrow participation-cancellation repository contract and shared locking order. ADM-01 changes only registrations affected by account deactivation. |
| Activity logs | Reuse the ERD's `activity_logs` and actor-type lookup. Pinca writes audit records now; ADM-02 adds read-only browsing later. |
| UnitOfWork and DI | Add compatible methods/registrations rather than replacing common infrastructure. All participating repositories share one scoped context and transaction. |

Before implementation, share the file inventory below with teammates. No message to teammates is sent automatically by this plan.

Do not copy entire unmerged branches, independently rebuild teammates' modules, or wait indefinitely for centralized scaffolding. If their mappings are not ready, agree on the minimal shared database-first mapping changes and develop feature logic/tests against the interfaces below. Test doubles are test-only: missing production integration must fail explicitly, never silently report zero affected registrations.

## 4. Backend behavior

### Accounts and roles

- Only an authenticated, currently active admin may use ADM-01 endpoints.
- List active and inactive accounts, ordered by a unique account ID; fixed 25 rows per page. Return only safe account summary fields, never password hashes.
- Accept exactly `admin`, `officer`, or `member`; SQL lookup/FK validation also enforces this.
- A role assignment changes the account role and update metadata, retains the same ID and optional member profile, and does not cancel participation.
- Require a trimmed, nonblank reason of at most 1,000 characters for role/status changes. This limit is a proposed backend validation decision, not a claimed WBS field limit.
- Self-demotion or self-deactivation is allowed only if another active administrator remains. Permissions change on the next authenticated request.
- Repeated requests for an already-current role/status return the current result without manufacturing duplicate change logs.

### Deactivation rules reconciled with specification section 5

Use one server UTC timestamp per transaction. Proposed exact boundary: future means `starts_at > now`; at equality the event has started. Convert to labelled local time only for presentation later.

| Situation | On account deactivation |
| --- | --- |
| No member profile | Deactivate account, no participation changes. |
| Future draft/published event; pending or waitlisted registration; no attendance record | Cancel with actor/time/reason. Clear any QR token defensively. |
| Future draft/published event; confirmed registration; no attendance record | Cancel, clear QR token, release its seat by changing confirmed status. No promotion. |
| Registration with any recorded attendance, including invalidated attendance history | Preserve registration and attendance. This conservative interpretation follows section 5's instruction to preserve already-recorded attendance and its registration. |
| Event starts at or before transaction time | Preserve registrations and attendance, including unresolved requests. |
| Completed or cancelled event | Preserve registration status and history regardless of schedule inconsistencies. |
| Already-cancelled registration | Leave it unchanged. |
| Issued certificate or historical participation | Preserve; no revocation, regeneration, or deletion. |

All new signup/check-in/QR validation must reject an inactive account, even where a historical registration or token is retained. Clearing tokens on cancelled future registrations invalidates saved QR copies when the scanner validates them online. ADM-01 does not implement the scanner itself.

Reactivation changes `users.is_active`, `users.updated_by` and `users.updated_at`, plus its audit entry. It does not activate membership, recreate QR credentials, restore registrations or priority, reclaim seats, or promote anyone. Cancelled rows remain cancelled and cannot be reapplied to under REG-01.

### Preview and concurrent edits

Expose a bounded impact preview before deactivation: total eligible cancellations, counts by registration status, and paginated safe details at 25 rows. Use the same eligibility predicate as execution. This is not a seat reservation and does not lock the database across user interaction.

Recompute eligibility at submission. Return actual cancellation counts; the UI must not promise that the earlier preview is frozen. Requests include the expected current role or active state to detect a stale target edit and return 409 rather than silently overwrite it. These are ordinary request fields, not a requirement for a new database version column. They do not provide full ABA/version-history detection.

## 5. Transactions, locking, and audit integrity

### Proposed SQL Server strategy

Use a short explicit transaction with serializable isolation and a transaction-owned `sp_getapplock` resource named `AttendTa.AccountAdministration` for ADM-01 writers. Acquire it before reading the acting admin, target, and active-admin count. This serializes competing admin changes and is appropriate for low-volume administrative operations without adding rowversion.

Inside the transaction:

1. Acquire the administration lock with a bounded timeout.
2. Reload the actor and target from the database, not cached claims/tracked stale instances; enforce active-admin authority.
3. Validate expected state and the proposed change. Reject removal of the final active admin.
4. For deactivation, lock and select eligible registrations with the event/attendance data needed to evaluate the rules.
5. Cancel selected rows, clear QR tokens, and set cancellation actor/time/reason. Set each changed registration's `updated_by` to the acting account ID and `updated_at` to the transaction timestamp; preserve creation, review, confirmation and snapshot metadata.
6. Change the account's role or active state only as requested, and set `users.updated_by` and `users.updated_at` to the same actor/time. Preserve `created_by` and `created_at`; unchanged requests produce neither metadata changes nor change logs.
7. Append one account audit record and one cancellation audit record per affected registration.
8. Save once through the shared UnitOfWork and commit. Any failure rolls back all updates and logs.

Coordinate a consistent account -> event -> registration locking/recheck protocol with registration and attendance writers. They must recheck account eligibility inside their own mutation transaction, so a signup/check-in racing deactivation cannot slip through. A named administration lock alone does not make unrelated writers safe. Validate the actual SQL lock behavior with real SQL Server tests; EF InMemory cannot prove this.

All account role/status writers must use this service/lock protocol. A future administrative SQL tool that bypasses it is outside the application's concurrency guarantee. Return a controlled conflict/retry response for deadlocks, lock timeouts, or stale edits; do not retry side effects outside the transaction.

### Row metadata and actor relationships (v2.1)

- `users.created_by` and `users.updated_by` are nullable integer user FKs; `created_at` is required and `updated_at` is nullable. A bootstrap user can have no creation actor. ADM-01 never backfills unknown creation actors with invented values.
- `registrations.created_by` is a required integer user FK; `created_at` is required, while `updated_by` and `updated_at` are nullable until updated. Fixtures must supply valid creation actors; the registration owner's creation workflow owns that requirement.
- On ADM-01 cancellation, `cancelled_by` and `updated_by` identify the same authenticated admin; `cancelled_at`, `updated_at` and corresponding audit timestamps use the same server UTC value, persisted with `datetime2(3)` precision.
- Member, event and certificate metadata remain unchanged when their rows are not modified by this operation. Account deactivation must not cascade into those records or actor FKs.
- Use non-cascading delete behavior for actor/history relationships. Account deactivation is an UPDATE, not a deletion; it must preserve even self-referencing user actor links.
- Row update metadata records only the latest change. It neither stores the required reason nor replaces append-only `activity_logs`, and `updated_at` is not a concurrency token in this plan.

### Audit shape

Use only `activity_logs`: actor type `user`, actor user ID, stable action code, entity type/ID, UTC time, reason, description, and whitelisted old/new values.

Actions: `account.role_changed`, `account.deactivated`, `account.reactivated`, and `registration.cancelled`.

Old/new values include role, active status, registration status, and a QR-invalidated boolean where relevant. Never serialize entire entities or request bodies into audit history. Never record passwords, password hashes, raw cookies, bearer tokens, or QR tokens.

Provide append-only access in the application repository. Enforce append-only history in SQL Server through restricted application permissions and/or a shared UPDATE/DELETE-blocking trigger, coordinated with the schema owner. Tests must verify that audit-write failure rolls back the business change. Do not invent a second audit table.

## 6. Proposed HTTP contracts

Use MVC controllers returning JSON now; Razor will call/use the same service later. Paths are proposed API contracts, not existing endpoints.

| Method and route | Request | Response |
| --- | --- | --- |
| GET `/admin/accounts?page=1` | Positive page number; no page-size option | Safe account items, page, pageSize=25, totalCount, totalPages. |
| GET `/admin/accounts/{id}/deactivation-impact?page=1` | Target ID and positive page | Computed-at UTC time, target state, full impact counts, paginated affected registration/event summaries. |
| POST `/admin/accounts/{id}/role` | role, expectedRole, reason | Current account state; no credentials. |
| POST `/admin/accounts/{id}/status` | isActive, expectedIsActive, reason | Current account state and actual cancellation counts. |
| GET `/admin/accounts/antiforgery` | Authenticated admin session | Request token for backend HTTP testing; future Razor can generate tokens normally. |

Use explicit request DTOs to prevent overposting. Obtain actor identity only from the validated principal. Mutating cookie-authenticated endpoints require antiforgery validation; configure a documented header such as `X-CSRF-TOKEN`. The token endpoint and account responses use `Cache-Control: no-store`.

Use 400 for validation, 401 for unauthenticated/inactive sessions, 403 for authenticated non-admins, 404 for missing targets, and 409 for last-admin/stale-state/concurrency conflicts. Missing required integration yields a controlled unavailable response (503), never a success or raw SQL exception. Unexpected errors return a generic 500 with a safe server-side diagnostic. Do not expose configuration or database details.

Pagination: reject invalid/overflowing page numbers; an empty list returns zero totals; a valid page beyond the result set returns empty items with the current total. Apply ordering, filtering, count and paging consistently; no optional search/filter scope expansion in this slice.

## 7. Planned final folder and file structure

Paths below are relative to the actual repository. `NEW` = feature-owned addition; `MOD` = existing file with focused edits; `SHARED` = canonical mapping to reuse or coordinate, not an independently owned duplicate. This is a planned inventory, not a list of files already created.

```text
AttendTa/
|-- docs/implementation-plans/
|   `-- ADM-01_Admin_Manage_Roles_Backend_Plan.md       NEW (this document)
|-- database/changes/
|   `-- ADM-01_Account_Access_And_Audit.sql             NEW, schema-dependent
|-- ASI.Basecode.Data/
|   |-- Models/
|   |   |-- User.cs                                  SHARED/MOD
|   |   |-- UserRole.cs                              SHARED
|   |   |-- Member.cs                                SHARED
|   |   |-- Event.cs                                 SHARED
|   |   |-- Registration.cs                          SHARED
|   |   |-- Attendance.cs                            SHARED
|   |   |-- ActivityLog.cs                           SHARED
|   |   `-- ActivityActorType.cs                     SHARED
|   |-- Queries/AccountAdministration/
|   |   `-- RegistrationImpactRow.cs                 NEW
|   |-- Interfaces/
|   |   |-- IUnitOfWork.cs                           MOD
|   |   |-- IAccountAdministrationRepository.cs      NEW
|   |   |-- IAccountParticipationRepository.cs       NEW
|   |   `-- IActivityLogRepository.cs                NEW or reuse equivalent
|   |-- Repositories/
|   |   |-- AccountAdministrationRepository.cs       NEW
|   |   |-- AccountParticipationRepository.cs        NEW
|   |   `-- ActivityLogRepository.cs                 NEW or reuse equivalent
|   |-- AsiBasecodeDbContext.cs                      SHARED/MOD
|   `-- UnitOfWork.cs                               MOD
|-- ASI.Basecode.Services/
|   |-- Interfaces/
|   |   |-- IAccountAdministrationService.cs         NEW
|   |   `-- ICurrentAccountService.cs                NEW or reuse auth equivalent
|   |-- ServiceModels/AccountAdministration/
|   |   |-- AccountListRequest.cs                    NEW
|   |   |-- AccountSummaryResponse.cs                NEW
|   |   |-- AccountPageResponse.cs                   NEW
|   |   |-- ChangeAccountRoleRequest.cs              NEW
|   |   |-- ChangeAccountStatusRequest.cs            NEW
|   |   |-- DeactivationImpactResponse.cs            NEW
|   |   |-- AccountChangeResponse.cs                 NEW
|   |   `-- AccountAdministrationException.cs        NEW
|   `-- Services/
|       |-- AccountAdministrationService.cs          NEW
|       `-- CurrentAccountService.cs                 NEW or reuse auth equivalent
|-- ASI.Basecode.Resources/Constants/
|   `-- AccountRoles.cs                              NEW or reuse shared constants
|-- ASI.Basecode.WebApp/
|   |-- Controllers/AdminAccountsController.cs       NEW
|   |-- Authentication/CurrentAccountValidator.cs   NEW or reuse auth equivalent
|   |-- Startup.Auth.cs                             MOD, coordinate auth owner
|   `-- Startup.DI.cs                               MOD
|-- ASI.Basecode.Tests/
|   |-- ASI.Basecode.Tests.csproj                    NEW
|   |-- AccountAdministrationServiceTests.cs         NEW
|   |-- AccountAdministrationValidationTests.cs      NEW
|   `-- TestDoubles/                                NEW, test-only dependencies
|-- ASI.Basecode.IntegrationTests/
|   |-- ASI.Basecode.IntegrationTests.csproj         NEW
|   |-- Infrastructure/SqlServerFixture.cs          NEW
|   |-- AccountAdministrationTransactionTests.cs    NEW
|   |-- AccountAdministrationConcurrencyTests.cs    NEW
|   |-- AccountAdministrationHttpTests.cs           NEW
|   `-- CurrentAccountSessionTests.cs               NEW
|-- ASI.Basecode.sln                                 MOD
`-- README.md                                       MOD
```

The shared list is only the subset this feature reads/writes; the agreed ERD still has ten business tables. Other canonical enum lookup entities (event status, registration status, membership status, etc.) remain owned by their shared mappings. Actual scaffolded class names may differ; reuse them instead of introducing aliases or duplicate entities solely to match this tree.

### Context and responsibility for each folder/file

| File or group | Responsibility and boundary |
| --- | --- |
| This plan | Scope, coordination notice, design decisions, inventory and completion criteria. Keep updated if implementation changes. |
| `database/changes/ADM-01_Account_Access_And_Audit.sql` | Reviewed incremental SQL against the confirmed shared schema: role/active constraints, required lookup seeds/FKs, v2.1 user/registration actor-time metadata, relevant indexes and append-only audit protection only where missing. Coordinate registration-column changes with its owner; if the shared schema already supplies them, validate/reuse them rather than add a second script. Include preflight checks for v2.1 columns, types, nullability and FK targets. Do not blindly DROP legacy fields or fill unknown required metadata; report mismatches for a reviewed data-preserving migration. Do not recreate the ten-table database, rename identity columns blindly, overwrite existing roles, or add default credentials. Use `database/changes`, since the existing gitignore excludes a folder named `sql`. |
| Shared `User.cs`, `UserRole.cs` | Canonical login identity, fixed role, account-active and v2.1 creation/update metadata mapping; role belongs to users, never members. Actor metadata is integer user FKs, not the starter model's strings. Preserve existing IDs and credential ownership; do not map removed image/archive fields. |
| Shared Member/Event/Registration/Attendance mappings | Read account linkage, schedule/status, registration state and recorded attendance; update only eligible registration cancellation fields and their `updated_by`/`updated_at` metadata, preserving `created_by`/`created_at`. Do not introduce competing registration entities. |
| Shared ActivityLog/ActivityActorType mappings | Canonical ERD audit table and lookup; no extra audit store. |
| `AsiBasecodeDbContext.cs` | Reconcile database-first scaffolded mappings/DbSets/FKs with the v2.1 canonical schema, including actor nullability, non-cascading actor relations and UTC `datetime2(3)` columns. Keep business rules out; avoid wholesale regeneration over teammate edits. |
| `RegistrationImpactRow.cs` | Internal safe projection for impact selection and paging; not a persistent entity or second registration model. |
| `IUnitOfWork.cs`, `UnitOfWork.cs` | Add async save and explicit transaction support while preserving existing synchronous callers. Single context shared by all repositories. Repositories in this feature must not commit independently. |
| AccountAdministration repository/interface | Account query/projection, current-state reads, active-admin checks, and SQL Server administration lock acquisition. No HTTP concerns. |
| AccountParticipation repository/interface | One reusable eligibility query for preview/execution, locked mutation selection, and cancellation/update-metadata writes. No automatic promotion, member-status updates, or registration module rebuild. |
| ActivityLog repository/interface | Append only, without independent SaveChanges. Reuse an equivalent shared abstraction if introduced by a teammate. No update/delete API. |
| `IAccountAdministrationService.cs` | List, preview, change-role and change-status use cases with cancellation tokens. |
| `AccountAdministrationService.cs` | Validation, active-admin recheck, last-admin protection, transactional orchestration, audit payload creation, consistent actor/time metadata and change results. Inject a clock for deterministic boundary tests. |
| `ICurrentAccountService.cs`, `CurrentAccountService.cs` | Resolve trusted current account ID, role and active state through the repository. No password verification or token issuance. Reuse the auth owner's equivalent when available. |
| `AccountListRequest.cs` | Validated page number, fixed page size; no arbitrary size parameter. |
| `AccountSummaryResponse.cs` | ID, name, email, role and account active state only. |
| `AccountPageResponse.cs` | Safe account items and page metadata. |
| `ChangeAccountRoleRequest.cs` | Requested role, expected current role and reason; never actor ID. |
| `ChangeAccountStatusRequest.cs` | Requested active state, expected active state and reason; distinguish omitted bool from false. |
| `DeactivationImpactResponse.cs` | Full status counts, computed time and bounded safe details; no QR or credential material. |
| `AccountChangeResponse.cs` | Resulting role/active state and actual registration cancellation counts. |
| `AccountAdministrationException.cs` | Stable domain failure codes; controller maps them to HTTP responses. Do not leak SQL errors into domain messages. |
| `AccountRoles.cs` | One shared set of fixed role codes; reuses SQL lookup semantics and existing team constants if present. |
| `AdminAccountsController.cs` | Routing, binding, authorization/CSRF, response mapping. No EF calls or business rules. |
| `CurrentAccountValidator.cs` | Web adapter that validates a session against the current account service and replaces stale role claims before authorization. Reject missing/inactive identities. Do not trust client-submitted actor/role values. |
| `Startup.Auth.cs` | Register current-account validation with cookie and bearer handlers actually supported by the app, admin policy, and API 401/403 behavior. Preserve auth-owner choices for signing, expiry and cookies; do not silently replace their session format. |
| `Startup.DI.cs` | Scoped services/repositories/validator, deterministic clock binding, antiforgery header setup. No second service provider/context per operation. |
| Unit test project and files | Domain validation, role/status transitions and mocked repository contracts. Keep fixture substitutes out of production DI. |
| SQL integration fixture | Disposable isolated test database from reviewed v2.1 schema, including required creation actors, lookup FKs and filtered QR uniqueness; explicit connection configuration and deterministic cleanup. Never run destructive setup against a development/team database. |
| Transaction tests | Real cancellation/audit atomicity, retained history and DB constraints. |
| Concurrency tests | Two independent contexts/connections exercising races; no reliance on EF InMemory or a single connection. |
| HTTP tests | Admin authorization, safe DTOs, response codes and real antiforgery behavior via test host. |
| Session tests | Existing cookie/bearer behavior after demotion/deactivation, signature/expiry and inactive login using the real auth integration. |
| Solution and README | Register tests; document schema prerequisites, API usage, SDK commands, test setup and remaining integration requirements. |

### Files intentionally outside planned ownership

Do not proactively edit AccountController, SignInManager, UserService, PasswordManager, token providers/formats, or registration/attendance controllers owned by others. The starter authentication bypass is a real end-to-end blocker, not something to ignore. Integrate the auth owner's completed work or explicitly coordinate a narrowly agreed shared edit before touching those files.

No Razor views, CSS, JavaScript, images, certificate templates, local-storage implementation, or generated Resources designer changes are planned. Existing project package references change only if a verified implementation need arises. Test-host exposure may require a small `Program.cs` partial entry point for integration tests; treat that as a conditional shared edit, not a startup rewrite. CI configuration is a conditional addition/reuse once the team's current workflow is inspected.

## 8. Database-first implementation sequence

1. Review this plan and notify teammates about shared files/contracts.
2. Inspect the real schema read-only and compare available teammate mappings to the ERD. Confirm lookup table/key names, SQL types, v2.1 actor metadata/nullability and account identity mapping. Verify existing schema indexes before proposing extras, including registration member/event uniqueness and nullable QR filtered uniqueness. These are physical implementation details, not a request to redesign the ERD.
3. Prepare the incremental SQL script only for missing ADM-01-owned pieces. Do not apply it to a shared database without identifying the target and agreeing on the schema change. No EF code-first migrations/EnsureCreated as production setup.
4. Apply reviewed schema to an isolated development/test database, scaffold/reconcile the canonical required mappings without overwriting unrelated work.
5. Implement request/response contracts and domain tests, then service/repositories and transaction behavior.
6. Add MVC endpoints and current-account validation integration. Keep authentication issuance and registration/check-in ownership boundaries explicit.
7. Run SQL integration and HTTP/session tests. Fix concurrency/integration issues before claiming completion.
8. Self-review the diff, document remaining dependencies truthfully, then prepare conventional commits and a PR from the feature branch to team `dev`. Never push directly to `main` or `dev`; CI and at least one peer approval precede merging.

## 9. Test and acceptance matrix

| Area | Required evidence |
| --- | --- |
| Listing | Zero, 1, 25, 26 and multiple-page datasets; stable order; inactive accounts included; invalid/overflow page rejected; no hashes/tokens in serialization. |
| Validation | Unknown/case-mismatched role, missing boolean, blank/overlong reason, invalid IDs, stale expected state, and no-op repeat. |
| Authorization | Anonymous 401, member/officer 403, active admin success; forged actor fields ignored; service rechecks admin authority after acquiring the transaction lock. |
| Session enforcement | Previously issued cookie and any supported bearer token lose admin access after demotion; inactive accounts lose protected access; expired/invalid signatures rejected; stale extra role identities cannot preserve privilege. |
| Last administrator | Sole active admin cannot demote/deactivate; an inactive admin does not count; two admins concurrently attempting to remove admin access cannot leave zero active admins. |
| Deactivation | Future pending/waitlisted/confirmed rows cancelled when eligible; QR cleared; actor/time/reason written; no promotion. No member profile is a valid case. |
| Preservation | Exact-start and past events, completed/cancelled events, recorded/invalidated attendance, issued certificates and already-cancelled registrations remain unchanged. |
| Reactivation | Only account access restored; membership unchanged; no seat, QR, priority or registration restoration. |
| v2.1 metadata | Account role/status changes stamp numeric actor/time; cancellations stamp cancellation and update metadata consistently; creation/review/confirmation data remains intact; no-op requests do not rewrite metadata. FK/nullability tests use real lookup keys, and self-deactivation preserves actor references. |
| Atomicity | Force audit insert failure, cancellation failure and save failure; all related changes roll back. Append-only enforcement rejects log update/delete. |
| Races | Deactivate vs check-in, signup/review/promotion, and actor demotion vs an admin mutation; verify transaction ordering and no ineligible participation after committed deactivation. |
| Preview | Same eligibility as execution; full counts despite paginated details; preview change before submission handled honestly. |
| Security | Missing/invalid CSRF rejected on mutations; real token succeeds; no mutation on GET; malicious inputs remain parameterized; error responses do not reveal secrets. |
| Missing integration | A missing registration/schema/auth integration is visible and cannot produce a false successful deactivation or completion claim. |

Use a real SQL Server engine for transaction/lock tests. LocalDB is acceptable on Windows; CI may use a SQL Server service/container. Unit tests can run before teammates' implementations arrive, but they do not prove end-to-end authentication, QR or registration enforcement.

Planned commands (after the projects exist):

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build ASI.Basecode.sln
& 'C:\Program Files\dotnet\dotnet.exe' test ASI.Basecode.Tests/ASI.Basecode.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj
git -c safe.directory=F:/AttendTaBakcend/AttendTa diff --check
```

Integration tests require an explicitly configured isolated SQL Server target. They must not silently skip and produce a misleading all-green completion report. Report unavailable tests as not run. Separate baseline package/build warnings from regressions introduced by this feature.

## 10. Completion and implementation summary

The backend is complete only when account management, existing-session enforcement, registration cancellation/QR invalidation, last-admin concurrency safety, history preservation and append-only audit consistency are implemented and tested together. A service tested against substitutes is a useful intermediate milestone, not a completed ADM-01 feature.

Planned change summary for teammates:

- Add feature-owned controller, DTOs, account administration service/repositories, current-account validation adapter, audit writer and tests.
- Modify shared DI, authentication event hooks, UnitOfWork, solution and setup documentation in focused edits.
- Coordinate canonical v2.1 User/lookup/participation/audit mappings, actor/time metadata and incremental SQL with owners; do not duplicate their modules or overwrite their work.
- No frontend changes, no additional feature modules, no automatic promotions and no production/shared-database actions in the planning stage.

No further ERD clarification is required: the team agreement is established. Remaining implementation checks are physical schema/mapping details, teammate ownership and transaction integration. Ask only if those reveal a real conflicting contract or an action outside the approved scope.
