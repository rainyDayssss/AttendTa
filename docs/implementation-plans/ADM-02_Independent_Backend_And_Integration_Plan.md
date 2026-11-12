# ADM-02: Activity History - Phased Backend Plan

Owner: Pinca, Member 5. Prepared: 2026-11-02 (Asia/Shanghai).

Status: **Proposed plan for review. No ADM-02 code or tests implemented by this document.**

## Integration quick reference

| Question | Summary |
| --- | --- |
| Provides | Proposed IActivityHistoryService, safe chronological list DTOs, pure access/paging/projection rules; later Data query and admin HTTP endpoint. |
| Depends on | Canonical activity_logs and actor lookup, shared append-only audit writer, current active-admin authorization, shared paging and organization timezone. |
| Keep | Tested viewer contracts and pure rules; retained log rows and the shared writer remain authoritative. |
| Connect/adapt later | Map canonical Data projections, reuse compatible paging/status resources, implement service/repository and MVC/DI. No second audit store, producer workflow or auth implementation. |
| Completion evidence | Pure tests establish behavior for supplied inputs. SQL/HTTP tests must establish correct selection, retained history, protected counts, current permissions and read-only behavior. Producer/SQL checks separately establish atomic append-only logging. |

Read [coding standards](../CODING_STANDARDS.md), [integration tutorial](../INTEGRATION_GUIDE.md) and [plan index](README.md). The same three phases apply: independent backend; Data/shared integration; HTTP/security integration. Phase 3 is not frontend implementation. Razor and end-to-end presentation acceptance remain deferred, not silently included.

## 1. Verified availability and safe starting point

Local checkout rechecked: `F:\AttendTaBakcend\AttendTa`, branch `feature/participation-reports`, HEAD `174d1fd7e20c6a8b9095d9a351e1d058da550511`. No tracked changes; existing plans/guides are untracked. Earlier same-session worktree inspection found only this checkout. No branch/worktree was created or switched for planning.

Earlier same-session remote verification found team dev at this SHA; this pass rechecked local state, not fresh remote heads. Before implementation, verify live team dev, actual worktrees and working changes again. Use an agreed feature-only branch, proposed `feature/activity-history`, in a suitable existing checkout; do not implement ADM-02 on REP by accident or import unrelated fork-dev commits. Preserve branches and all uncommitted/untracked work; no automatic stash/reset/cleanup/commit/push.

Current Data/Services have starter User code and no canonical activity_logs model/repository or shared business audit writer. WebApp has Serilog file/Seq diagnostics and Services has ILogger plumbing. Those are operational diagnostics, not evidence of transactional business activity history. Do not parse application log files into ADM-02 or replace Serilog. No deployed database was inspected. The ERD is accessible but does not prove schema enforcement. Prior workspace inspection found no applicable AGENTS.md; recheck before implementation.

## 2. Requirements and traceability

Pinca's explicit assignments and reduced WBS control scope. Preserve Pinca's ADM-01/02 ownership despite Markdown allocating Administration to Member 1; coordinate shared Auth/audit work without duplicating it.

| Source | Requirement used |
| --- | --- |
| `F:\AttendTaBakcend\docs\WBS_Account_Name_Application_Name_yyyymmdd.pdf`, p.3 ADM-02, read earlier in this session | Admin-only basic read-only activity list: actor, action, affected record, timestamp, reason; retained append-only history and useful changes; no credentials/tokens, advanced filters or field-comparison screen; fixed 25-row paging. |
| `AttendTa-System-Specification.md` in that folder, v2.1 sections 2, 4, 16-19 | Current account/role checks; retain history; actor type/account, polymorphic entity reference, UTC time; safe validated JSON old/new values; mutation/audit transaction atomicity; stable newest-first paging. |
| `AttendTa-User-Stories.md`, US-28/29 and shared rules | One chronological viewer, no log edit/delete controls, authorized counts and normalized pages; necessary list search/filters not individually enumerated. |
| `AttendTa-Sequence-Diagrams.md`, shared pagination/policy | Count authorized applicable rows then stable slice; these workflows do not require a new screen for every story. |
| ERD attachment `C:\Users\cruiz\.codex\attachments\80a59153-8dec-4d7c-9b16-5807145bce3e\Pasted text.txt` | activity_logs.log_id is bigint; actor_user_id nullable integer FK; entity_id integer application-validated reference; occurred_at datetime2(3); action/entity_type strings and nullable reason/old/new JSON. |
| ADM-01, CERT-01/02 active plans; standards and Git QA guidelines | Shared append-only writer, safe audit payloads, one transaction per mutation, existing project boundaries, feature PR/CI/peer review. REP reads do not create mutation logs. |

This feature is a **reader** of the audit trail. The producing feature owns writing its own correct entries; a shared writer supplies consistent validation and append behavior. ADM-02 records that dependency and validates integration, but does not implement all producers.

Excluded: edit/delete/archive/restore, retention purge, CSV/PDF export, advanced search/filter builder, field-by-field comparisons, raw JSON inspector, public/member audit history, monitoring dashboard, live push updates, impersonation, authentication rewrite and new frontend framework. No new table or persisted paging field.

## 3. Exact viewer behavior and proposed choices

### Access and scope

Settled: only a currently authenticated active administrator may read the activity list or its count. An officer/member is denied, even for their own activity. An admin need not have a member profile; membership status does not determine administrative access. Deactivated/demoted actors remain in historical results; their current state is not a filter on logs. Recheck the viewer's current account/role on every request, not only a cached claim.

Proposed initial request is page-only: one organization-wide chronological list with no owner/event restriction for an authorized admin. No tenant dimension is invented. US-29 mentions necessary search/filters generally but does not identify a required log search. The specific ADM-02 reduced scope defers advanced filtering; this plan proposes the smallest page-only list. Confirm that choice at review. If basic search is later required, define one small allowlist, comparison semantics and before-count predicate explicitly; do not silently introduce date/actor/entity/action filter combinations.

### Row contract

| Field | Meaning and handling |
| --- | --- |
| LogId | Positive long, mapped from bigint; never narrow to int. |
| ActorType / ActorUserId | Recognized user/system semantic code; numeric user FK when applicable. Real user mutations require a real positive actor ID; do not invent ID 0. |
| ActorDisplayName | Proposed current retained users.full_name for user actor; fixed Resources label for system. This ERD has no actor-name snapshot, so do not claim the current label is the name at mutation time. Preserve stable actor ID. |
| Action | Stored action code plus optional Resources label for known codes; unknown nonblank action codes remain visible as safely encoded text, not discarded. New producer actions must not break old viewer versions. |
| EntityType / EntityId | Stored affected-record type/positive integer. Display type and ID; do not infer a SQL FK or fetch every domain entity. Initial viewer has no deep links requiring additional access checks. |
| OccurredAtUtc | Stored UTC datetime2(3), returned with unambiguous UTC semantics; eventual UI formats labelled organization-local time. Do not infer organization timezone from this chat's timezone. |
| Reason / Description | Plain text, safely encoded by eventual UI. Proposed bounded preview: first 500 Unicode text elements of nonblank reason, otherwise description; expose an IsTruncated flag. Preserve original database text; this is not data deletion. |

The 500-element preview is a proposed application convention, not an ERD storage limit. If neither reason nor description is usable, show a Resources-backed unavailable label and data-quality marker. No raw HTML rendering. Counts include retained rows even when labels are incomplete.

**Never select/return old_values or new_values for this list.** They remain stored for accountability, but their display/comparison is deferred. Do not expose complete user/entity objects, credentials, email, QR tokens, connection strings, physical paths or exception dumps. Excluding JSON alone cannot prevent secrets already embedded in descriptions/reasons: safe producer payloads and shared-writer validation are integration prerequisites. HTML encoding prevents markup execution, not secret disclosure. Do not promise regex-based credential detection. Suspected legacy sensitive rows need controlled investigation/remediation by the audit owner before enabling the viewer; ADM-02 must not silently rewrite them.

### Missing/inconsistent history

Proposed read policy preserves visibility rather than hiding malformed rows through inner joins: left join actors, keep inactive users, and display an unavailable-actor label with stored ID if a legacy user link cannot resolve. Flag unknown actor type or invalid identity metadata as incomplete; never relabel an unknown actor as System. The canonical FK should prevent missing actors, but defensive rendering is not evidence of correct constraints. System rows normally have no user ID; inconsistent pairs are flagged, not used as authorization facts. A malformed row does not grant access or make the entire historical dataset disappear.

Unknown action/entity codes are extensibility cases, not automatically corruption; blank required codes and nonpositive IDs are integrity issues. Do not dynamically resolve entity types into SQL/table names. Preserve invalid row evidence without logging raw sensitive content. Bad JSON in old/new payloads remains a writer/integrity issue; the basic viewer never parses those fields. Operational diagnostics may record safe log IDs and issue codes, not full records. No corrective writes on GET.

### Ordering, totals and pagination

Required order: `occurred_at DESC, log_id DESC`. Log ID is the unique tie-breaker; timestamp alone is insufficient at millisecond precision. Fixed 25 rows. Missing/blank/malformed/nonpositive/out-of-int-range page normalizes to 1; beyond-last clamps to the last page. Empty result: Page=1, TotalPages=0, Items empty, Previous/Next false, matching all existing feature plans.

Use long total count/pages and overflow-safe arithmetic: `count / 25 + (count % 25 == 0 ? 0 : 1)`, then clamp current page against max(1,totalPages). Raw page parsing follows existing int-sized request contracts. Calculate offsets in long; verify actual provider bounds before integration and return a controlled error for unsupported offsets, never overflow. Reject negative supplied totals in pure calculations. No client-selectable page size/order.

Authorization applies before returning counts or rows. Count is full authorized matching history, not visible-page length. No automatic refreshing requirement. Each request is a new view of history, so new inserts can move rows between pages; deterministic sorting is not a frozen cross-request snapshot.

## 4. Proposed contracts and file responsibilities

Names below are proposals relative to the repository, not files already implemented.

```csharp
Task<ActivityHistoryResult> GetPageAsync(
    int actorUserId, ActivityHistoryListRequest request,
    CancellationToken cancellationToken = default);
```

The actor is server-supplied validated identity, not request data. Result contains a safe page or a stable failure (InvalidRequest, Unauthenticated, Forbidden, TemporarilyUnavailable); no unauthorized partial counts. Pure access decisions are not authentication. I/O is async; pure functions remain synchronous.

| Proposed files | Responsibility |
| --- | --- |
| Services `Interfaces/IActivityHistoryService.cs` | Read-only application interface; no append/update/delete method or DI implementation in Phase 1. |
| Services `ServiceModels/ActivityHistory/ActivityHistoryListRequest.cs` | Raw page string only initially; explicit optional/null semantics. |
| Same folder: `ActivityHistoryRowResponse.cs`, `ActivityHistoryPageResponse.cs`, `ActivityHistoryResult.cs` | Safe fields above, bounded summary, data-quality/truncation indicators, normalized page and authorized long totals; no raw JSON/EF objects. |
| Services `ActivityHistory/ActivityHistoryInputs.cs`, `ActivityHistoryRules.cs`, `ActivityHistoryPaging.cs`, `ActivityHistoryCodes.cs` | Immutable actor/projection snapshots, supplied active-admin decision, safe summary/metadata fallback and paging rules. No database/HTTP/implicit clock. Reuse compatible shared helpers when available. |
| `tests/ASI.Basecode.ActivityHistory.UnitTests/` | Standalone net9.0 project for access, display/projection, null/Unicode boundaries and pagination. |
| Later Services `Services/ActivityHistoryService.cs` | Current actor loading, orchestration, Data-to-safe-DTO mapping, no writes. |
| Later Data `Interfaces/IActivityHistoryRepository.cs`, `Repositories/ActivityHistoryRepository.cs`, conditional `QueryModels/ActivityHistoryRow.cs` | Canonical SQL projections, count/order/paging, actor left join and read consistency; no Services references and no audit-writer duplication. |
| Later WebApp `Controllers/ActivityHistoryController.cs` and coordinated DI | Thin admin GET action, current Auth and status mapping; eventual Razor is separate. |
| Later Resources | Viewer labels and known action display text under shared conventions; not a new entity/action lookup schema. |

Use existing Services/Data/Resources/WebApp projects; no Domain or infrastructure project. Current Serilog files stay untouched. Match [coding standards](../CODING_STANDARDS.md); semantic codes do not mean hard-coded SQL lookup IDs. Public serialization maps only allowed DTO properties. No generically serialized object bag for audit details.

## 5. Phase 1 - independent backend

After explicit approval: agree proposed list/preview/fallback choices, define interface/DTOs, implement pure active-admin/paging/projection decisions, then add tests and an honest handoff. Test text truncation without splitting Unicode text elements and clear null/whitespace fallback semantics. Verify a malicious markup string remains plain data; eventual HTML encoding itself requires web/UI tests.

Only new feature Services files and standalone tests. No production endpoint/fake service, audit writer, Data models/SQL, repositories, UnitOfWork, auth/session changes, DI, Resources generators, existing project/solution edits or frontend. No new production package expected. These rules do not validate stored JSON or establish append-only storage.

Gate A: compile/test succeeds with no shared wiring; safe contract and supplied-input behavior established. Historical ADM/CERT test counts are not ADM-02 results.

## 6. Phase 2 - canonical read integration and audit handoff

Inspect the actual canonical Data and shared-writer commit first. Verify activity_logs types, actor FK/lookup mapping, nullable fields, UTC precision and `(occurred_at, log_id)` ordering index. Use Data-owned projections and map in Services. Do not scaffold another ActivityLog entity or supply a competing ten-table script.

Proposed query pipeline:

1. Authenticate and load current viewer account through the shared reader; require active admin before reading history/counts.
2. Open a short coordinated read unit. Query full authorized history, without actor-account/member active predicates or speculative entity joins.
3. Count with COUNT_BIG/equivalent translated long count, normalize/clamp page, then read stable 25-row projection. Do not select old/new JSON. Project only fields needed for safe display; bound nvarchar(max) text transfer through an agreed SQL projection budget and apply Unicode-safe preview in Services. Exact SQL/text budgeting must preserve the documented preview semantics and be tested; never materialize the entire log table.
4. Map safe actor labels/summary flags and return one complete response. No SaveChanges or audit append for viewing the audit list.

Preferred proposal: count/page statements share a SQL Server SNAPSHOT read transaction, only if Data owner verifies the database setting. Statement-level RCSI alone does not guarantee one snapshot for two statements. If unavailable, coordinate/test a short SERIALIZABLE read alternative with appropriate indexes and timeout behavior; no silent NOLOCK fallback. Authorization is current at request/read time; already-started authorized reads may complete, while later requests reflect demotion/deactivation. Do not claim instant recall of delivered data. Use consistent read scope for actor check where supported by shared Auth.

Concurrent appends may change later requests but must not produce an internally inconsistent count/page in one response. Bound transient retry to the entire read, at most once as a proposal; cancellation/timeout returns safe unavailability without partial totals. Test provider offset behavior and representative volumes; do not invent performance thresholds without team agreement. Keyset/cursor pagination is not silently introduced because the existing contract uses numbered pages and last-page clamping.

### Writer requirements, owned outside this viewer

- Every producer supplies a real actor for user actions, valid affected-record reference, timestamp and minimal safe description/reason plus allowed old/new values. System actor type is reserved for actual system operations, not a way to bypass identity validation.
- Shared writer validates JSON and allowed payload structure and participates in the caller's transaction/context. No independent save. Feature owner decides which business transition happened; writer does not infer it from arbitrary entity dumps.
- Database permissions and/or coordinated enforcement must prohibit application log updates/deletes. A viewer with no edit button or a repository lacking Delete is not proof of append-only storage. Separate migration/operator permissions from ordinary application permissions.
- Audit failure rolls back the producing business mutation. No duplicate log for rejected/no-op actions where the feature contract requires none. Keep proposed action vocabulary coordinated across ADM/CERT/Registration/Attendance.
- Retained actor/record inactivity must not remove logs. Do not cascade-delete history or reconstruct missing logs from current updated_at fields. No claim of cryptographic tamper evidence or recovery of never-recorded events.

Gate B: actual SQL reader tests pass and producer/writer prerequisites have evidence or explicit pending status. A viewer may be query-complete while the system audit trail remains incomplete; report that distinction, never label the whole accountability goal complete prematurely.

## 7. Phase 3 - MVC and current authorization

Proposed GET `/admin/activity-history?page=...`. Resolve actor from validated identity, invoke IActivityHistoryService, and return safe DTOs under current active-admin policy. Do not accept actor IDs, raw order SQL, page size or arbitrary filter/entity selectors. Bind raw page text so malformed pages normalize rather than accidentally trigger integer-binding 400.

Use shared signed-cookie/current-account enforcement. If bearer authentication remains supported, enforce equivalent current checks through its owner; no new auth scheme. Return no-store; absent/invalid authentication follows shared 401/login behavior, active non-admin is 403, invalid non-page contract input is 400 and operational failures are generic server/unavailable results. No count/body leakage on forbidden/error paths. No mutation routes and no business writes on GET; antiforgery is required for mutations elsewhere, not a reason to invent a POST for this viewer.

No raw JSON details endpoint. Eventual Razor must HTML-encode action/type/actor/reason text, display explicit timezone and truncated/incomplete labels, and hide navigation for <=1 page. This describes future presentation acceptance, not Phase 3 frontend work.

Gate C: real current-session, binding, safe serialization, no-store and read-only HTTP tests pass with real DI. Frontend remains deferred; do not declare full user-facing ADM-02 complete until the later list is usable and team workflow acceptance passes.

## 8. Teammate integration checklist

| Provider | Need from them | What to adapt/connect |
| --- | --- | --- |
| Canonical Data owner | Reviewed schema/scaffold commit, actor mapping/FK, long log IDs, UTC types, indexes, read isolation and append-only permissions | Map canonical row to safe DTO; add narrow reader only if no equivalent exists; retain all existing records |
| Shared Auth / ADM-01 | Current numeric user/active/admin reader and real session enforcement | Replace test-supplied actor snapshots with trusted reads; retain pure rule tests, do not create a new login/role service |
| Shared audit owner | Append API, action vocabulary, safe JSON/description policy, same-transaction guarantee and failure behavior | Consume its rows; do not call append when viewing or replace it with Serilog |
| Each producer | Actual calls for committed business transitions, safe text/payloads and atomicity tests | Verify sample history from that feature; report missing events to owner, do not fabricate them in viewer |
| Shared paging/Resources owner | Compatible 25-row/empty-page conventions, display labels, timezone configuration | Adapt helper usage and labels without changing response semantics or copying general infrastructure |

Pinca owns this reader; shared audit infrastructure ownership must be coordinated because of the Member 1 allocation overlap. No teammate has been contacted merely because a handoff appears here. Follow the [integration guide's handoff template](../INTEGRATION_GUIDE.md#4-handoff-to-request-from-each-teammate): actual API, commit, transaction owner, setup and test results.

## 9. Test matrix and limits

| Case | Phase 1 | Later integration evidence |
| --- | --- | --- |
| Access | Supplied active admin allowed; inactive/member/officer/unknown/null denied; member profile irrelevant | Real unsigned/expired/stale sessions, demotion/deactivation, numeric principal mapping; no unauthorized counts |
| History | User/system actors, inactive actor, missing actor fallback, unknown action/type preserved, invalid metadata flagged | Real left joins, actor updates do not rewrite logs; no active-only global filter; FK/permissions verified |
| IDs/time | Long IDs above int.MaxValue, positive references, unambiguous supplied UTC semantics | bigint mapping, datetime2(3), identical timestamps tie-break by log ID; labelled display remains UI acceptance |
| Summary | Null/blank reason fallback, 499/500/501 text elements, combining marks/emoji, truncation flag | Bounded SQL projection preserves agreed Unicode preview; safe serialization and eventual HTML encoding; source text unchanged |
| Paging | Counts 0/1/25/26/50/51, negative count rejected, malformed/overflow pages, last-page clamp, long offset arithmetic | Full count beyond visible 25, stable order, provider bounds, empty state |
| Sensitive fields | Allowed DTO only, no raw JSON/password/path-bearing entity serialization | SQL excludes old/new payloads; writer safe-content fixtures and legacy review; no secrets in diagnostics/errors |
| Concurrent reads | No concurrency claim from pure tests | Append during count/page produces a consistent response; next request may see new rows; retry/cancel/timeout no partial payload |
| Read-only | No mutation contract | GET causes no business/audit writes; no edit/delete route; application SQL identity cannot update/delete logs |
| Producer integration | Not proved by viewer unit tests | Representative ADM/CERT/Registration/Attendance mutation + exactly intended log; forced audit failure rolls back business mutation; rejected/no-op behavior follows each producer |

Use real SQL Server tests for permissions/isolation/constraints; EF InMemory is insufficient. A passing producer test for one feature is not proof for every module. No need to claim a full Cartesian matrix; record actual scenarios and omissions.

## 10. Validation and decisions for review

After approved Phase 1, from the selected feature checkout:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build ASI.Basecode.Services/ASI.Basecode.Services.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/ASI.Basecode.ActivityHistory.UnitTests/ASI.Basecode.ActivityHistory.UnitTests.csproj
git diff --check
git status --short --untracked-files=all
```

Use actual safe.directory override if needed. Keep net9.0, baseline AutoMapper warning separate, no unrelated dependency changes or user-file deletion. Later add actual SQL/HTTP suites and solution build against isolated test configuration, never reset a shared database. No tests were run for this planning-only document. Record exact outcomes and remaining missing integrations in the eventual handoff.

Settled: current active-admin-only read-only history; actor/action/affected record/time/reason or description; newest-first fixed 25; retained append-only logs; safe stored changes; no advanced filters/comparison/edit/delete/export; existing architecture and phased delivery.

Approval-time proposals: page-only scope given US-29's general search language; 500-text-element preview and fallback flags; unknown-code/missing-actor handling; DTO/error contract and long-count arithmetic. These are explicit choices, not claims that the WBS prescribes them.

Integration-time decisions: canonical schema/API/permissions; action vocabulary and safe-content guarantees; actor-name current-label convention; text projection budget/Unicode equivalence; read isolation/offset support; current Auth behavior and timezone; actual producer test coverage. Required producer gaps block the corresponding system audit-completeness claim, not independent Phase 1 work.

Planning gate is this reviewed document; A is independent foundation, B is canonical query/shared audit verification, C is authorized backend. Future UI/system acceptance completes the user-facing feature. All five assigned features now have plans, not completed implementations. Feature PRs target team dev with CI/peer approval; no direct main/dev push, automatic commit/push or new delivery-date promise.

**Stop for review; no ADM-02 application implementation is authorized merely by this plan.**
