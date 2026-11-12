# CERT-01: Certificate Eligibility Review - phased implementation plan

## Integration quick reference

| Question | Summary |
| --- | --- |
| Status | Active eligibility plan; independent Phase 1 exists, persistence and HTTP pending. |
| Provides | ICertificateEligibilityService, review DTOs, creation/review rules and certificate-side correction consequences. |
| Depends on | Canonical Data/Auth/audit, Event completion orchestration and Attendance correction orchestration. |
| Connect or adapt later | Keep rules/DTOs; add canonical mapping and concrete use cases. Event/Attendance call agreed certificate collaborators in their transactions; review endpoints call eligibility service. Never replace the whole Attendance workflow with a certificate rule. |
| Completion evidence | SQL tests prove preparation uniqueness, atomic audit and writer races; HTTP tests prove actual access. PDF/issuance race completion requires CERT-02. Details: sections 7-10. |

Shared references: [coding standards](../CODING_STANDARDS.md), [integration tutorial and handoff checklist](../INTEGRATION_GUIDE.md), [active plan index](README.md). These summaries do not authorize deferred implementation or supersede explicit scope decisions.

> **Active CERT-01 execution plan; planning-time Git inventory below is historical.** Phase 1 now exists on its feature branch. Do not repeat section 2's branch preparation from stale paths or refs. See [the plan index and review](README.md) for current status and the handoff for the limits of Phase 1 evidence. Persistence and HTTP integration remain pending.

Owner: Pinca, Member 5. Prepared: 2026-11-02 (Asia/Shanghai).

Status: **Plan for review only. No implementation is authorized by this document.**
Suggested future branch: `feature/certificate-eligibility`. Repository: `F:\AttendTaBakcend\AttendTa`.

## 1. Inspection evidence and safe starting point

The planning session inspected the checkout before any branch operation:

| Item | Verified result |
| --- | --- |
| Current branch | `dev`, commit `65c32a4`; tracked files clean, untracked `docs/` and `tests/` present before this plan. |
| Local/fork dev | Local `dev` is 18 commits ahead of `upstream/dev`; live `origin/dev` and `origin/main` point to `65c32a4`. Do not equate fork dev with the agreed team base. |
| ADM-01 | Local `feature/admin-manage-roles` is `8d2b181`, one commit ahead of its remote branch. Live origin feature branch remains `174d1fd`. The local commit contains 13 Services files; existing plan/handoff and test files are untracked in this checkout. The previous commit/push workflow is not complete. |
| Team branches | Live `upstream/dev`, `main`, `feature/auth-account-request`, and `feature/master-data` all point to `174d1fd`. Its tree is the starter application. |
| Other remote work | Upstream advertises unmerged PR refs. Their contents were not inspected; do not claim no teammate has written or submitted shared work. No canonical v2.1 data implementation is available in the checked-out files or verified team branch tips. |
| Data | Current context exposes only starter `User`; string identity/actor fields and `datetime` mappings do not implement the revised ERD. UnitOfWork exposes a DbContext and synchronous SaveChanges. |
| Auth | Starter cookie/bearer configuration exists; AccountController's login uses a fabricated User. Real current-account enforcement is not established. Coordinate with auth owner; do not copy this behavior. |
| Architecture | Services references Data and Resources; projects target net9.0. No separate Domain project. |
| Instructions | No applicable AGENTS.md found in the workspace/repository or checked parent locations. Historical commits mentioning one do not establish a current file. |

Live remote heads were checked with `git ls-remote` after a sandbox network failure and an approved retry. No fetch, checkout, reset, commit or push was performed. This plan is added alongside the existing untracked planning documents; those documents and tests remain untouched.

**Final verification update:** During planning, an external checkout changed the active branch from `dev` to `feature/certificate-eligibility`. Final HEAD is still `65c32a4`; reflog confirms that checkout. The assistant did not create or switch this branch. Preserve it, but do not assume it has the agreed shared base: it currently includes the same 18 commits beyond verified team dev. The branch-preparation steps below must now account for this existing branch and must not overwrite/reset it. Resolve its base before implementation; a distinct worktree/branch name may be needed. Final tracked diff is clean and the plan is untracked alongside the pre-existing files.

### Branch preparation after plan approval

1. Recheck status, branch tips, remote heads and applicable instructions. Explicitly inventory untracked ADM-01 documents/tests; do not stage them into CERT-01 by using `git add .`.
2. Reconcile the shared base with the team. Default is freshly fetched `upstream/dev`, currently `174d1fd`, not local/fork `dev` at `65c32a4` and not the ADM-01 feature tip. Do not import the 18 unrelated commits merely to obtain scaffolding.
3. Prefer a separate worktree under the workspace, e.g. `F:\AttendTaBakcend\AttendTa-CERT01`, on `feature/certificate-eligibility` from verified `upstream/dev`. First ensure the path/branch do not already exist. This preserves the original checkout and its untracked work without stashing or deleting it. Re-read instructions in the selected base.
4. Copy only this plan into the feature worktree. Read ADM-01 as a reference; do not merge/cherry-pick its whole branch. Its rule and test patterns may inform design but are not a required feature dependency.
5. Validate the chosen base's net9.0 projects and dependencies. If the team changes its base, record the exact accepted commit here before implementation.
6. Future pushes go only to `origin/feature/certificate-eligibility`; PR targets `rainyDayssss/AttendTa:dev`. No direct main/dev push. The plan does not authorize committing or publishing other work.

## 2. Sources, precedence and ownership

Controlling scope is the user's explicit decisions and reduced WBS, even where Markdown asserts a different authority hierarchy. Reviewed sources:

- `F:\AttendTaBakcend\docs\WBS_Account_Name_Application_Name_yyyymmdd.pdf`, page 2: CERT-01, ATT-02, EVT-04 and CERT-02 boundary. Read the actual PDF text, not only its cached text export.
- `F:\AttendTaBakcend\docs\git_qa_guidelines.pdf`, all three pages: branch from shared dev, conventional commits, local checks, CI plus peer review, integrated QA.
- `F:\AttendTaBakcend\docs\AttendTa-System-Specification.md` v2.1: sections 1.4, 2, 11, 13-14, 17-19.
- `F:\AttendTaBakcend\docs\AttendTa-User-Stories.md` v2.1: US-22, US-24, boundary US-25, US-29 and shared rules.
- `F:\AttendTaBakcend\docs\AttendTa-Sequence-Diagrams.md` v2.1: Complete, approve and issue PDF. Its completion-to-review creation sequence informs the proposed handoff below; it is not an implemented transaction.
- Revised MVP v2.1 ERD attachment: `C:\Users\cruiz\.codex\attachments\80a59153-8dec-4d7c-9b16-5807145bce3e\Pasted text.txt` (accessible). Referenced repository DBML is not present in the inspected files.
- Existing `ADM-01_Independent_Backend_And_Integration_Plan.md`, `ADM-01_Phase1_Handoff.md` and historical `ADM-01_Admin_Manage_Roles_Backend_Plan.md` in this folder. Follow the phased plan/reconciliation over superseded execution details.

The Markdown assigns Administration to Member 1. Pinca's explicit assignment remains CERT-01, CERT-02, REP-01, ADM-01 and ADM-02. Flag overlap before shared integration; do not reassign Pinca or edit the requirements' ownership tables silently. Member 1's auth/audit helpers, Member 2's pagination helpers, Member 3's completion/registration operations and Member 4's attendance operations remain coordination dependencies.

The ten business tables are users, account_requests, members, event_types, venues, events, registrations, attendance, certificates and activity_logs. Seeded lookup tables implement enum concepts; these are not competing business tables. ERD agreement is not proof of deployed SQL or scaffolded entities.

## 3. Exact feature scope and business decisions

CERT-01 provides the backend to list pending certificate reviews and approve or decline one review. Its eventual UI belongs in event-attendee management. No frontend is built now.

### Required invariants

1. A valid attendee has a retained attendance row whose invalidation timestamp is null. Missing and invalidated attendance are distinct history states, but neither permits pending creation or review.
2. The event must explicitly be `completed`. A past start time or a concluded-looking published event is insufficient. CERT-01 does not complete events or infer completion from the clock.
3. Only a currently active officer/admin may review. Roles belong to users; account active state is separate from membership status. An officer/admin may review their own certificate under identical checks and audit requirements. Do not require a reviewing admin to have a member profile.
4. No-shows receive no certificate record. Confirmation alone is insufficient. There is no attendance-duration requirement.
5. At most one certificate exists per registration. Pending creation never resets an existing certificate, even if that certificate is declined or issued.
6. Approval is `pending_approval -> eligible`; decline is `pending_approval -> not_eligible`, requiring a nonblank reason. Both store numeric reviewer ID and trusted UTC review time, update metadata and append audit atomically.
7. No ordinary review transition from `eligible`, `not_eligible` or `issued`. No reopening a decline, toggling an approval, revocation or reissue. A repeated/stale review is a conflict, not another audit or an overwritten reviewer.
8. Re-read current actor, event, attendance and certificate on submission. Client-supplied state is only an expectation, never proof. Fail closed on unknown codes or inconsistent links.
9. Attendance correction can invalidate unissued eligibility; see the separate integration contract below. Preserve attendance and certificate records and original review history. Issued certificates block ordinary correction.
10. Recipient account/membership inactivity alone is not an extra retrospective eligibility veto in the cited CERT-01 rules. Current authorization concerns the reviewer. Do not silently add a new historical disqualification policy; inconsistent registration/attendance relationships require coordinated investigation, not fabricated attendance.

| Current certificate state | Explicit review | Attendance correction consequence |
| --- | --- | --- |
| Absent | No review; create pending only through completion integration when valid | No certificate insertion for invalidated/missing attendance |
| pending_approval | Approve -> eligible; decline with reason -> not_eligible | -> not_eligible with correction reason |
| eligible | Reject further review | -> not_eligible with correction reason |
| not_eligible | Reject; cannot reopen | Preserve existing decision/reason; attendance correction has its own audit |
| issued | Reject all review mutations | Block correction; no mutation |
| Unknown/inconsistent | Fail closed | Fail closed |

Decline is irreversible in this release. A later UI must show that warning before submission; tests of backend rules do not prove a warning was shown.

### Explicit exclusions

CERT-02 PDF generation/download/storage, certificate numbers and issuance snapshots; bulk issuance, email, revocation/reissue; attendance recording/correction UI or QR implementation; reports/exports; account-management implementation; new SQL/entities; auth rewrite; frontend framework; archive/delete/restore, profile pictures, automatic waitlist promotion or registration reapplication. CERT-02 later uses one fixed landscape A4 retained PDF in private persistent server-local storage; nothing in Phase 1 provisions that storage.

## 4. Architecture and teaching checkpoints

Flow after integration: WebApp controller -> Services use case/rules -> Data repository/UnitOfWork -> SQL Server. Resources supplies shared text/constants when agreed, not entities.

- **DTOs in Services** define what the caller may ask and what safe results it receives. They are not EF entities and cannot accept review actor/time or issuance fields from a browser.
- **Pure rules in Services** decide from supplied facts. Their inputs are small immutable snapshots, not a second data model. They cannot authenticate, fetch current state, save, lock, or append a real audit.
- **Use-case service in Services, later** loads trusted facts through Data and coordinates transaction, rules, update and audit.
- **Repository contracts and implementations in Data, later** follow existing conventions. Data must not reference Services DTOs/rule types; Services maps Data entities/projections into rule inputs. No Data -> Services circular reference.
- **WebApp, later** handles current authentication, authorization, model binding, antiforgery and status mapping. It contains no EF queries or eligibility decision logic.

At each approved implementation checkpoint, explain the file's responsibility, why it belongs in that layer, who supplies trusted values, the test evidence and remaining integration limits. Do not generate all phases without discussing those boundaries.

## 5. Phase 1 - independent contracts, rules and tests

Begin only after explicit plan approval and safe base preparation. No shared data or production wiring is needed.

All paths below are relative to the actual repository. Names are planned additions, not claims that files exist.

| Planned file | Responsibility and reason for its location |
| --- | --- |
| `ASI.Basecode.Services/Interfaces/ICertificateEligibilityService.cs` | Async list and single-review use-case contract with CancellationToken; no concrete implementation or DI registration. |
| `ASI.Basecode.Services/ServiceModels/CertificateEligibility/CertificateReviewListRequest.cs` | Required positive event ID and raw page text; pending queue only initially. No page-size selector or free-form sort. |
| `.../ReviewCertificateRequest.cs` | Certificate ID (or route-bound equivalent), approve/decline decision, expected status and decline reason. No actor, timestamps, FK status IDs or issuance properties. |
| `.../CertificateReviewSummaryResponse.cs` | Safe certificate/registration/event/member IDs, display identity, valid-attendance time, semantic status and permitted review actions. Include only identity fields required by the shared attendee screen. |
| `.../CertificateReviewPageResponse.cs` | Items, normalized page, fixed size 25, authorized filtered total count, total pages and Previous/Next flags. |
| `.../CertificateReviewResponse.cs` | Committed result shape: ID, resulting status, reviewer/time and normalized reason; rule outcome alone must never be returned as persisted success. |
| `ASI.Basecode.Services/CertificateEligibility/CertificateEligibilityCodes.cs` | Feature semantic statuses, decisions and stable failure codes; do not guess lookup numeric IDs. Reuse equivalent shared codes if available on the agreed base. |
| `.../CertificateEligibilityRuleInputs.cs` | Separate actor, participation and certificate snapshots; distinguish absent/valid/invalidated attendance and absent/existing certificate. No EF attributes, navigation properties or repositories. |
| `.../CertificateEligibilityDecision.cs` | Allowed/denied result, proposed next state, normalized reason and failure code. Explicitly means a decision, not a committed write. |
| `.../CertificateEligibilityRules.cs` | Pure pending-creation and review preconditions/transitions. No wall-clock access, SQL, HTTP or file operations. |
| `.../AttendanceCorrectionCertificateRules.cs` | Pure certificate-side consequence specification only; reconcile with Attendance owner before wiring so there is one production correction path. |
| `tests/ASI.Basecode.CertificateEligibility.UnitTests/ASI.Basecode.CertificateEligibility.UnitTests.csproj` | Standalone net9.0 xUnit project referencing Services; no solution edit required. Match existing test-package conventions after verifying the selected base. |
| Test project: `PendingReviewCreationTests.cs`, `CertificateReviewRulesTests.cs`, `AttendanceCorrectionCertificateRulesTests.cs` | Exercise production rule functions, state table, authorization snapshots and correction consequences. |
| Test project: `RequestValidationTests.cs`, `ResponseContractTests.cs` | Validate shape/reasons/IDs/decision codes and safe fixed paging contract; no copied rule implementation. |

The `.../` entries inherit the immediately preceding full folder. Do not add generic auth/audit/paging frameworks or copy ADM-01's implementation into a new shared helper.

### Proposed contract choices for this plan's review

- Service methods: `GetPendingReviewsAsync(eventId/listRequest, trustedActorId, cancellationToken)` and `ReviewAsync(request, trustedActorId, cancellationToken)`, with final signatures avoiding duplicate event IDs. The trusted actor ID comes from validated authentication, not body/query input; the service reloads its account.
- List is event-scoped and pending-only. Existing decisions remain readable through the shared attendee view later. No global search, advanced filters or standalone certificate management screen is implied.
- Expected status is explicitly `pending_approval`. Missing/unknown decision or expected status is validation failure; a current state different from expectation is conflict. There is no arbitrary target-state setter.
- Proposed reason validation follows ADM-01's convention: maximum 1,000 raw characters, then trim; decline rejects null/empty/whitespace. Approve accepts no nonblank reason in this minimal contract. The cap is an application proposal, not an ERD nvarchar(max) limit; verify the shared convention before implementation.
- Stable failures distinguish invalid input, actor denied, target missing, stale/nonreviewable state, invalid attendance, incomplete event and inconsistent data. HTTP mapping is deferred. Do not return raw exceptions or sensitive fields.
- No expected timestamp is treated as a concurrency token. `updated_at` is not rowversion. A shared rowversion may be adopted later if the schema owner supplies one; do not add a schema column now.

### Pure paging behavior

Use raw page text so malformed HTTP input can later normalize rather than accidentally causing integer model-binding rejection. Missing, blank, nonnumeric, zero, negative and integer overflow inputs normalize to 1. A valid positive page beyond the last page clamps to the last page. Empty results: page 1, total pages 0, no navigation. Calculate offsets with overflow-safe arithmetic. Shared helper reuse is preferred; isolate only the feature contract if it is unavailable.

Pure tests can prove page arithmetic given a count. They cannot prove the count was authorized, filtered correctly or obtained consistently from SQL.

### Phase 1 exit

Contracts/rules compile and their unit tests pass against the production rule code. Only feature Services additions and the standalone test project change. No concrete use-case implementation, fake persistence, production endpoint or DI binding exists. This is an independently reviewable foundation, **not a working CERT-01 backend**.

## 6. Shared files explicitly deferred

Do not modify these in Phase 1:

- Data `Models/*`, `AsiBasecodeDbContext.cs`, SQL/setup scripts, migrations or lookup seeds. Use teammates' canonical database-first scaffold when available; never create a second Certificate/Attendance/User entity.
- Data `Interfaces/IUnitOfWork.cs`, `UnitOfWork.cs`, base repositories or shared repository interfaces. Transaction support must be coordinated, not independently replaced.
- WebApp `Startup.Auth.cs`, `Startup.DI.cs`, `Program.cs`, AccountController, SignInManager and token/cookie providers; Services UserService, PasswordManager and SessionManager.
- Shared event completion, registration and attendance correction implementations; shared audit writer and pagination helpers; shared Resources constants/resx files.
- Existing csproj files, solution, dependency versions, configuration/connection strings, deployment/storage and frontend/Razor files.

Later edits to these are conditional integration work with their owners. They are not authorized merely because the full plan describes them. Keep net9.0; use `C:\Program Files\dotnet\dotnet.exe`. Report the existing AutoMapper 14.0.0 NU1903 warning separately; do not upgrade it as collateral CERT-01 work. If restore hits disk-full again, inspect free space and report it; do not delete user files.

## 7. Dependency and handoff contracts

| Provider | Required handoff before integration | CERT-01 responsibility |
| --- | --- | --- |
| Shared data owner | Canonical SQL scripts/scaffold, stable lookup code mappings, unique certificate.registration_id, FK rules, UTC datetime2(3), actor/time pairs and transaction conventions | Map canonical records to rules; contribute reviewed constraints/index needs without competing scripts |
| Auth owner | Validated numeric user ID, real signed expiring cookies, current active/role reader, inactive-session rejection and supported scheme policy | Enforce current officer/admin permission for reads and mutations; transactional recheck for decisions |
| Audit owner | Append writer on caller's context/transaction, action vocabulary, validated JSON, immutability enforcement | Supply minimal safe certificate deltas and real actor; no independent audit commit |
| Event owner | Explicit completion closes check-in and exposes valid attendees; transaction participation and retry semantics | Create missing pending rows once for eligible attendees, without resetting existing decisions |
| Attendance owner | Invalidation keeps row/history, requires reason, rejects issued certificates; shared locking/transaction entry point | Supply certificate consequence rules and safe update/audit contract; do not implement attendance correction module |
| Pagination owner | Fixed-size normalization and result conventions | Apply authorized event/pending/valid-attendee filters before count/page; stable sort and safe projection |
| CERT-02 later | Final issuance rechecks approval, valid attendance, completion and current actor under same synchronization protocol | Preserve issuance fields and expose semantic eligible state; no PDF dependency in CERT-01 |

### Pending-review creation proposal

Use event completion as the normal creation trigger, matching the sequence source. Event owner orchestrates one transaction that completes the event/closes check-in and invokes certificate preparation through a Services-level collaboration (Data never invokes Services). Insert only missing rows for current valid attendees. Unique registration FK plus locking provides duplicate protection; don't rely on an unchecked `Any` followed by insert. Creation does not auto-approve.

Creation sets pending status, created_at, and the completion actor as created_by when human-triggered; review and issuance fields remain null. A genuinely system-triggered operation may have null creator with actor_type system; never invent user ID 0. Agree creation audit action `certificate.review_created` with the audit owner, distinct from human review.

Completion-trigger ownership/transaction placement must be agreed before Phase 2. No database writes on GET. If legacy completed events lack rows, define a controlled, authorized idempotent preparation operation before enabling their review, not an unannounced scheduler or side effect of list loading. This recovery is an integration decision, not additional Phase 1 implementation.

### Attendance correction proposal

The Attendance service owns the public command and transaction. It locks/reloads attendance and certificate with the shared protocol, blocks issued certificates, invalidates attendance, and applies the certificate consequence in that same transaction. Neither side commits independently.

For pending/eligible certificates, set not_eligible, correction reason in review_notes, updated_by/at to correction actor/time. Preserve prior reviewed_by/at as the original human review; pending rows may still have no original reviewer. The correction actor/time and previous review_notes/status must be retained in append-only history. This proposed metadata distinction must be agreed with Attendance/data owners so corrected pending records are not mistaken for human declines. If they choose another field convention, document it without losing prior review facts.

Already not_eligible: preserve original decline/reason/reviewer; no duplicate certificate transition log. Attendance still records its correction reason/audit. Absent certificate: do not create one. Issued: roll back/reject the entire correction. No re-check-in or restoration.

Assign audit ownership explicitly: Attendance writes `attendance.invalidated`; certificate collaboration writes one `certificate.eligibility_invalidated` when a certificate state changes. Both use one transaction. These action strings are proposals to reconcile with shared vocabulary.

## 8. Phase 2 - canonical persistence, transactions and concurrency

Start only when canonical data and handoff agreements are available. Refresh the availability check instead of assuming today's branch state persists.

| Later file/change | Responsibility |
| --- | --- |
| `ASI.Basecode.Services/Services/CertificateEligibilityService.cs` | Implement list/review orchestration with trusted current reads, pure rules and atomic audit/update. |
| `ASI.Basecode.Data/Interfaces/ICertificateEligibilityRepository.cs` | Data-native query/update/locking contract; no Services DTO references. Reuse an equivalent canonical repository if supplied. |
| `ASI.Basecode.Data/Repositories/CertificateEligibilityRepository.cs` | EF/SQL authorized projections, count/page queries and guarded writes, without hidden commits. |
| Conditional `ASI.Basecode.Data/QueryModels/CertificateReviewRow.cs` | Minimal Data projection if canonical entities cannot efficiently represent list results; mapped in Services, not exposed directly to HTTP. |
| Services completion/correction collaborators | Add feature-scoped collaborators only after agreeing ownership; reuse one rule implementation across all callers. |
| `tests/ASI.Basecode.CertificateEligibility.IntegrationTests/` | Real SQL fixture plus persistence, preparation, audit rollback, paging and concurrent-writer tests. New net9.0 test csproj. |
| Shared context/UoW/audit/event/attendance files | Small coordinated integrations against canonical models; no parallel schema or transaction framework. |

### Review transaction

1. Begin a short SQL transaction under the agreed writer protocol. Acquire locks in the agreed global order before reading protected facts.
2. Reload current actor account/role, event, linked registration, retained attendance and certificate. Validate route/event/certificate linkage and authorization before disclosing details.
3. Compare expected state and invoke pure review rules. Missing or invalidated attendance and noncompleted events cannot be approved or declined through the review command. Correction has its own path.
4. Capture trusted UTC time; update status, reviewed_by/at, review_notes and updated_by/at. Preserve created metadata, attendance, registration history and all issuance fields.
5. Append exactly one approved/declined audit (`certificate.eligibility_approved` or `certificate.eligibility_declined`, subject to shared vocabulary), including safe old/new status and review facts, trimmed decline reason, actor and time.
6. Save and commit once. Audit/save/constraint failure rolls everything back; return success only after commit. Reject duplicate/stale submissions without rewriting history or generating duplicate change logs.

Audit JSON is an allowlist, not serialized entities: no passwords/hashes, cookies, QR tokens or physical storage paths. Validate affected entity reference in application logic. Append-only protection requires database permissions and/or agreed triggers; absence of a repository Delete method is insufficient proof.

### Concurrency protocol to agree and test

Proposed baseline: all relevant writers use compatible SQL transaction locks, stable ordering and guarded state updates. A practical order to reconcile with existing writers is actor account -> event -> registration -> attendance -> certificate; bulk preparation processes registration IDs deterministically. Lock existing parent registration rows to serialize creation even when certificate rows are absent; retain the unique constraint as a final guard. Use appropriate update/range locks or transaction-owned application locks only if every participating path follows the protocol.

Keep actor permission stable through the decision's commit using locks honored by account role/status writers. A certificate-only lock cannot serialize against account deactivation, attendance correction or completion. Ordinary read-committed reads plus a later save do not establish these guarantees. Define bounded deadlock/timeout handling and retry the entire transaction only when safe; do not blindly retry stale decisions. A lost response after commit is not permission to audit another approval.

Required races: approve vs decline, double approve, prepare vs prepare, correction vs approval, correction vs preparation, completion vs final check-in, reviewer demotion/deactivation vs review, and later correction vs issuance. If correction wins, approval fails; if approval wins, subsequent correction makes the unissued certificate not_eligible. If issuance wins, correction fails; if correction wins, final issuance fails. CERT-01 cannot claim the issuance race is proven until CERT-02's actual writer exists.

### Query behavior

Authorize first, filter by event, pending state, completed event and valid attendance, then count and page in SQL. Proposed stable queue order: certificate.created_at ASC, certificate_id ASC. Do not let client text become SQL ordering. Project safe identity details and retain event/page query state. Count and slice should use one consistent read strategy where available; otherwise re-count/re-clamp once if concurrent changes empty the chosen page. Document chosen consistency behavior and test it. After a decision removes an item, refresh with normalized pagination.

Use isolated SQL Server/LocalDB for tests; EF InMemory/SQLite cannot establish SQL Server lock/constraint behavior. Do not reset/drop a shared development database.

## 9. Phase 3 - MVC/HTTP and current security integration

Later add `ASI.Basecode.WebApp/Controllers/CertificateEligibilityController.cs`, or contribute equivalent actions to the agreed attendee controller. Reuse one service either way; routes below are proposals, not a requirement for a separate screen:

| Method/route | Behavior |
| --- | --- |
| GET `/events/{eventId}/certificate-reviews?page=1` | Active officer/admin pending queue; no creation or other mutation. |
| POST `/events/{eventId}/certificate-reviews/{certificateId}/decision` | Approve/decline DTO, server actor/time, current recheck and atomic result. |

Register only the real implementation/repositories with scoped lifetimes in coordinated `Startup.DI.cs` changes. Reuse auth owner's current-account reader, signed-cookie enforcement and antiforgery convention. A stale role claim must not preserve authority after demotion; an inactive account cannot use its old session. If a bearer path remains enabled, it must enforce equivalent current-account checks or be disabled with its owner. No new auth scheme is introduced.

Validate antiforgery on cookie-authenticated POSTs; reject invalid/missing tokens. Actor/time/status FK/issuance fields cannot be overposted. Return no-store for sensitive results. Map malformed business input to 400; absent/inactive authentication to 401; active insufficient role to 403; missing or inaccessible linked target to an agreed non-disclosing 404; stale state/changed attendance/incomplete event/concurrency conflict to 409. Normalize malformed page inputs rather than returning 400 for pagination. Unexpected failures get generic errors, not raw SQL/configuration details.

Add `CertificateEligibilityHttpTests.cs` and `CertificateEligibilitySessionTests.cs` in the integration test project. Coordinate test-host entry point, solution registration and documentation only at this stage. No fake implementation is registered to make endpoints appear complete. No frontend now; later attendee UI must display the irreversible-decline warning, action feedback and fixed pagination.

## 10. Test matrix and honest evidence

| Scenario | Phase 1 evidence | Later required evidence |
| --- | --- | --- |
| Pending creation | Every event state x absent/valid/invalidated attendance x absent/existing certificate | Real joins, one row per registration, no no-show rows, repeat completion preparation preserves existing decisions |
| Reviewer | Active admin/officer succeeds, inactive/missing/member/unknown role denied; self-review allowed | Current DB account/role and old-cookie denial; reviewer role change race |
| Transitions | Every certificate state x approve/decline, expected-state mismatch, unknown codes | Concurrent approve/decline has one winner, one audit, stable reviewer metadata |
| Participation | Completed required, missing/invalidated attendance blocked, no duration rule | Actual completion flag/retained-row mapping, changed attendance between list and submit |
| Validation | IDs positive, omitted/unknown decisions, required decline reason, whitespace, 1,000/1,001 raw chars, trimming | HTTP binding/overposting/status mapping; supported Unicode stored safely |
| Correction | Absent/pending/eligible/declined/issued consequence table | Real atomic attendance + certificate + both logs; original decision history retained; races with approval and later issuance |
| Pagination | Missing/malformed/negative/zero/overflow pages; 0/1/25/26/50/51 counts; beyond-last and navigation flags | Authorized filtered SQL counts, deterministic tie ordering, final-row removal fallback; no unauthorized counts |
| Metadata | Safe DTO contract and rule output only | Numeric actors/FKs, UTC datetime2(3), paired review fields, unchanged creation/issuance fields |
| Failures | Rejected rules request no transition | Forced audit insert/save/constraint failures roll back all writes; timeout/deadlock controlled |
| Security | Supplied actor snapshot only, no claim of authentication | Real signed cookie, expiry/signature validation, CSRF, route linkage/ID tampering, no mutation on GET |
| Retention/audit | No destructive action in rule outputs | Original rows retained, logs cannot update/delete, no secret/path serialization |

Use table-driven tests across full combinations rather than only happy paths. Separate valid business states from inconsistent fixtures and assert fail-closed behavior for the latter. Pure tests cannot prove SQL concurrency, audit rollback, current sessions, QR invalidation, PDF persistence or integration with teammates' actual writers. The previous ADM-01 38-test result is recorded history, not a CERT-01 test run; no application tests were run for this documentation-only change.

## 11. Validation commands and delivery gates

After approved Phase 1 implementation, from the feature repository/worktree:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build ASI.Basecode.Services/ASI.Basecode.Services.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/ASI.Basecode.CertificateEligibility.UnitTests/ASI.Basecode.CertificateEligibility.UnitTests.csproj
git diff --check
git status --short --untracked-files=all
```

Use an appropriate `-c safe.directory=<actual-worktree-path>` if Git requires it. Later run the full solution build and isolated SQL/HTTP suites with explicit test configuration. Report tests unavailable/not run rather than silently skipping them and claiming success. Baseline restore warnings are separate from feature regressions.

- **Planning gate (this delivery):** sources/current Git inspected, scoped plan written and reviewed for consistency. Stop for Pinca's approval. No application implementation.
- **Gate A - independent foundation:** contracts/pure rules pass standalone tests, no shared production files or wiring changed. Partial feature delivery only.
- **Gate B - persistence:** canonical data reused; preparation/review/correction coordination and audit are atomic; real SQL uniqueness/paging/rollback/concurrency checks pass. Clearly list any missing cross-feature writer tests.
- **Gate C - CERT-01 backend:** real HTTP actions, current permissions/session enforcement and antiforgery verified, connected completion/attendance paths tested, no unwired success path. PDF issuance/download remains CERT-02. Frontend acceptance remains pending until shared attendee UI integration.
- **Merge/release:** self-review, CI build/tests and at least one peer approval for PR to team dev; team integration/system testing before release. No direct main/dev push, no inferred delivery-date commitment.

Suggested eventual commits: `docs: plan certificate eligibility phases`, `feat: define certificate eligibility contracts`, `feat: add certificate eligibility decision rules`, `test: cover certificate eligibility rules`. Persistence and HTTP/security belong in later clearly scoped changes; no automatic commit/push is part of this planning task.

## 12. Decisions to settle at the appropriate checkpoint

Plan approval can authorize the independent Phase 1 scope without waiting for SQL. Before implementation, confirm the base SHA and adopt or adjust the proposed DTO/reason/repeated-submission choices in section 5. Before Phase 2, agree completion-trigger ownership, correction metadata preservation, audit actions, canonical lookup mappings, shared lock order, transaction APIs and paging helper conventions. Before Phase 3, agree route placement and auth/session integration with the attendee/auth owners.

These are explicit integration decisions, not permission to create competing modules. The immediate next action after this document is Pinca's review; implementation must wait for explicit approval.
