# REP-01: Participation Reports - Independent Backend and Integration Plan

## Integration quick reference

| Question | Summary |
| --- | --- |
| Status | Proposed plan only; no REP application code yet. |
| Provides | Planned IParticipationReportService, report DTOs, calculations and pagination contracts. |
| Depends on | Canonical Data, Registration signup snapshots, Attendance validity/completion facts and current Auth. |
| Connect or adapt later | After approved Phase 1, retain calculations/contracts; add SQL full-cohort projections, Services mapping, controller and DI. Production queries replace no test fixtures and do not call participation mutation services. |
| Completion evidence | Unit tests prove formulas on supplied facts; SQL tests prove selection/aggregation/consistency; HTTP tests prove protected counts. Details: sections 6-8. |

Shared references: [coding standards](../CODING_STANDARDS.md), [integration tutorial and handoff checklist](../INTEGRATION_GUIDE.md), [active plan index](README.md). These summaries do not authorize deferred implementation or supersede explicit scope decisions.

Owner: Pinca, Member 5. Prepared: 2026-11-02 (Asia/Shanghai).

Status: **Planning only; awaiting explicit approval. No REP-01 application code is implemented or authorized by this document.**

## 1. Verified checkout and available shared work

Inspection performed in `F:\AttendTaBakcend\AttendTa`, inside workspace `F:\AttendTaBakcend`.

| Check | Observed evidence |
| --- | --- |
| Active branch | `feature/participation-reports`, tracking `upstream/dev`. Branch reflog says `Created from upstream/dev` at 2026-11-02 21:20:00 +0800; HEAD reflog records checkout from `feature/admin-manage-roles`. This verifies the reported checkout succeeded here. |
| HEAD and actual branch base | `174d1fd7e20c6a8b9095d9a351e1d058da550511`, initial starter commit; no feature commits yet. |
| Remote freshness | Live `git ls-remote --heads upstream` succeeded after an initially blocked connection. Team `dev`, `main`, `feature/auth-account-request` and `feature/master-data` all point to that same SHA. Local `upstream/dev` matches the live dev head at inspection time. No fetch/merge was necessary. |
| Worktrees | `git worktree list --porcelain` lists only `F:/AttendTaBakcend/AttendTa`. The previously described `AttendTa-CERT01` directory is absent from the workspace and is not registered. No new worktree is needed. |
| Worktree status | No tracked modifications. Five existing untracked implementation plans/handoffs under `docs/implementation-plans`; preserved unchanged. Thus the checkout is not entirely clean. This plan adds a sixth untracked document. |
| Fork divergence | Local/fork dev remains `65c32a431584d1ea49de492d70c9224bcd48c337`, 18 commits ahead of team dev. None belongs in REP-01 merely to obtain helper code. |
| ADM-01 | Local and live origin `feature/admin-manage-roles` now both point to `e262e4ee4e774bc143004f8f7155d3e4a7321e6a`, including contracts, rules and tests. Earlier remote-behind statement is stale. |
| CERT-01 | Local `feature/certificate-eligibility` is `e65146e`; live origin is `d0f1e01885e162ca8911eecd39bd605b34a2685e`, one test commit ahead. The remote test commit exists; local branch was not moved. |
| Preserved branches | `backup/certificate-eligibility-fork-base`, `feature/admin-manage-roles-clean`, other local branches and remote refs remain untouched. |
| Remote configuration | origin = Wynnwrath/AttendTav2 fork; upstream fetch = rainyDayssss/AttendTa team repository; upstream push URL is `DISABLE`. Future PR target is team `dev`. |

Commands inspected worktree list, detailed status, branches, reflogs, history, remote heads and feature diffs. No checkout, reset, stash, merge, commit, push or branch deletion was performed. No applicable `AGENTS.md` was found in the workspace tree or drive-root ancestor.

The current Data project has only the starter `User` model and `Users` DbSet. Its string `UserId`, string actor fields and `datetime` mapping are not the agreed numeric user ID / UTC `datetime2(3)` implementation. No canonical participation SQL scripts, DBML file, registration/attendance entities, report repository or shared pagination helper were found in the current checkout/team base. A deployed SQL database was not inspected; absence from this checkout does not establish absence on a teammate's machine. The ERD attachment below is accessible and supplies design evidence, not deployed schema evidence.

Services references Data and Resources. The current UnitOfWork exposes `Database` and `SaveChanges`; it does not establish a coordinated report read transaction API. Startup has cookie/JWT plumbing and an authenticated-user policy, not verified current-account officer/admin report authorization. Feature-branch diffs show independent ADM/CERT contracts/rules/tests, not the missing shared infrastructure. Their feature-local pagination code can inform behavior; it is not a merged common helper.

Projects remain `net9.0`; the required `C:\Program Files\dotnet\dotnet.exe` currently reports SDK `10.0.401`. Do not downgrade project targets. No build, restore or unit test was run for this documentation-only task. Prior 38 ADM and 134 CERT passes are historical results, not reverified here. CERT handoff worktree/branch/commit statements are stale; its selected test combinations are not proof of a full Cartesian matrix, and the null correction fixture meant absent certificate, not an existing certificate with null status.

## 2. Authority, traceability and scope

Pinca's explicit instructions and reduced WBS control scope, even where Markdown asserts another precedence. Pinca owns CERT-01, CERT-02, REP-01, ADM-01 and ADM-02. Markdown's allocation of Administration to Member 1 is a coordination overlap, not a reassignment. Agree shared Auth/audit boundaries with that owner.

| Source read | REP-01 consequence |
| --- | --- |
| `F:\AttendTaBakcend\docs\WBS_Account_Name_Application_Name_yyyymmdd.pdf`, p.3 REP-01; p.2 REG-05/ATT-02 | Participants/statuses, confirmed/attendee counts, rate, completed-event no-shows, attendee course/year distribution; signup snapshots, full totals, 25-row details; no exports. Cutoff history is not expected attendance; invalidation removes attendance contribution. |
| `F:\AttendTaBakcend\docs\AttendTa-System-Specification.md`, v2.1, sections 2, 9, 12-13, 15-19 | Officer/admin access, retained history, explicit completion, exact denominator and zero N/A, attendee-only distribution, snapshot retention, authorization before counts, stable paging. |
| `F:\AttendTaBakcend\docs\AttendTa-User-Stories.md`, v2.1, US-26/27/29 and shared rules | Report acceptance, cancelled events separate, historical retention, fixed paging, protected counts. |
| `F:\AttendTaBakcend\docs\AttendTa-Sequence-Diagrams.md`, v2.1, shared pagination and policy reference | Authorized filtered count before stable 25-row slice. No dedicated report-query sequence is supplied; the later query pipeline below is a proposal. |
| `C:\Users\cruiz\.codex\attachments\80a59153-8dec-4d7c-9b16-5807145bce3e\Pasted text.txt` | Accessible v2.1 ERD: registrations contain academic snapshots; attendance is unique per registration with retained invalidation metadata; roles belong to users. Seeded SQL lookups/FKs, ten business tables, numeric actor IDs, UTC `datetime2(3)`. |
| `F:\AttendTaBakcend\docs\git_qa_guidelines.pdf`, pp.1-3 | Feature branch, conventional commits, local checks, CI and at least one peer approval, integrated system testing. User-specific team-upstream base and preservation instructions supersede generic fork-dev/cleanup commands. |
| Existing ADM-01 phased plan/handoff, earlier ADM full plan, CERT-01 phased plan/handoff in this document's directory | Reuse independent-first workflow and honest evidence limits. ADM phased plan supersedes earlier execution order. These documents do not authorize merging their branches or implementing REP now. |

Attachment/local paths record provenance, not runtime dependencies. No CSV/PDF report exports, confirmed-participant academic distribution, duration tracking, scheduler, automatic closeout/promotion, reapplication, archive/restore, profile pictures, auth rewrite or frontend work. Certificate PDF work stays CERT-02. Report reads do not create eligibility, correct attendance, change registrations or append business mutation logs. No new report table or persisted paging fields.

## 3. Exact report semantics

**Settled requirements:** A valid attendance record is retained attendance whose `invalidated_at` is null. An actual attendee for the report is a confirmed registration with valid attendance. Current account/member/type/venue inactivity must not remove historical participation. Academic grouping uses registration snapshots, never current member course/year. Certificate existence/status does not enter report predicates.

**Proposed contract:** One event per report request. This is the smallest useful scope; cross-event rollups and date-range dashboards are not included without a later decision. Registration rows are the counting unit (unique registration ID), not joined rows or current distinct users across events.

Let R be the full authorized, event-scoped, filtered registration dataset before paging; C its confirmed registrations; A the members of C with a valid attendance row. Counts are `|R|`, `|C|`, `|A|`. For a completed event, no-shows are `C minus A`, and attendance percentage is `100m * |A| / |C|`. Zero C returns nullable percentage = null, rendered N/A; never 0%, NaN or infinity. Zero attendees with positive C returns 0%. Pending, waitlisted and cancelled registrations remain listable but do not enter C, A or no-shows.

Proposed percentage rounding: decimal arithmetic, two decimal places, `MidpointRounding.AwayFromZero`, once at the response boundary. Do not round operands. Example: C=3/A=1 -> 33.33%; C=32/A=1 -> 3.13%. All counts use nonnegative `long`; cast to decimal before multiplying by 100 and validate arithmetic boundaries.

| Event state | Proposed output behavior |
| --- | --- |
| Completed | Final current report: counts, rate (N/A if C=0), no-show count/list and attendee distribution. Later valid correction can change this retained-data report; it is not a frozen historical export. |
| Published | Participant/status list, confirmed and actual-attendee counts and attendee distribution marked provisional. Rate and no-show fields null/not applicable until explicit completion. Start time or closed check-in alone does not make no-shows final. |
| Draft | Retained list and counts only; normally empty. Rate/no-shows not applicable. Attendance in a draft is inconsistent data. |
| Cancelled | Separate event-status result, retained participant/status list and counts for history. Rate/no-shows/distribution not applicable; never combine cancelled events with completed results. Valid attendance here is inconsistent with ordinary cancellation. |

Only completed-event rate/no-show semantics are prescribed explicitly; the non-completed display choices above require plan approval. Null no-show count distinguishes unavailable from completed with zero no-shows. Per-row no-show is nullable outside completed events.

Proposed distributions are two separate count lists (course and year), derived only from A. Each list sums to attendee count; no confirmed-participant distribution or percentages are necessary. Preserve snapshot course text verbatim; do not silently trim/recase/merge historical values. Agree an explicit ordinal-equivalent SQL grouping/sort mapping with Data so C# and SQL do not disagree on case or trailing spaces. Year sorts numerically; course uses the agreed text comparison. No snapshot correction editor is added. Participant name/student number are current retained identity fields because this ERD does not snapshot them at signup; do not claim historical name fidelity. Section may appear as a registration snapshot but does not introduce another distribution.

### Filters and paging (proposed allowlist)

- Required positive `EventId`; optional one registration status (`pending`, `confirmed`, `waitlisted`, `cancelled`); optional search text matching current full name or student number; raw page string. No arbitrary sort, size, date range, course/year or attendance-state filter initially.
- Trim search, blank means absent, maximum 100 characters; longer search and unknown statuses are validation errors. Search is literal substring with parameterized SQL and explicit wildcard escaping; align SQL comparison/collation with the owner's identity-search convention before integration.
- Every supported filter applies to R before totals, distribution, no-show selection and paging. Thus a pending-only filter has confirmed=0, attendee=0, rate=N/A, not the event's unfiltered headline values. Response echoes normalized filters and identifies totals as filtered totals. No hidden detail-only filter exception.
- Default list includes every registration status. Propose detail kind `participants` (all R), or `no_shows` (C minus A; completed only), as a view selector rather than a cohort filter. Summary always describes R; `DetailTotalCount` describes the selected detail kind. This explicit difference prevents a no-show list from redefining the rate denominator. Reject no-show detail for non-completed events.
- Stable detail sort: registration `submitted_at ASC, registration_id ASC`. No raw client ordering. All counts/aggregates precede Skip/Take; each page contains at most 25 detail rows. Both views use the same safe row projection.
- Null/empty/whitespace/malformed/nonpositive/out-of-int-range page -> 1; beyond-last -> last available page. Empty result uses current page=1, total pages=0, no navigation, empty items, matching existing ADM/CERT contracts. Compute total pages as `count / 25 + (count % 25 == 0 ? 0 : 1)` using long arithmetic; clamp current page against `max(1, totalPages)`. Raw page parsing follows existing int-sized feature contracts; SQL offset arithmetic uses long, with provider limit handling explicitly tested.
- Filter or detail-kind changes reset page to 1 in the eventual caller; stateless backend cannot infer previous filters. Navigation preserves normalized supported filters. Paging never changes summary totals.

### Inconsistent data policy (proposal)

Fail the affected report with `InconsistentData`, returning no misleading partial success, rather than capping rates or silently repairing rows. Detect unknown event/registration statuses, nonpositive or mismatched identifiers, duplicate member/event registrations or attendance links, orphaned links in the scoped data, valid attendance on an unconfirmed registration, invalidation metadata with unpaired actor/time/reason, draft/cancelled valid attendance, and missing/blank course or nonpositive year snapshots. Missing attendance is normal; invalidated attendance with complete metadata is normal and counts as no valid attendance. Inactivity is not inconsistency.

Integrity checks cover the authorized event scope before optional user filters so a status/search filter cannot conceal a broken relationship. Never inspect/disclose unauthorized events to provide diagnostics. Return a safe failure code; internal diagnostics identify the integrity issue without credentials or QR tokens. SQL constraints are the primary integrity defense; reporting must not manufacture a correction. Coordinate legacy-data remediation with Data/Registration/Attendance owners outside REP-01.

## 4. Architecture and proposed contracts

Keep WebApp -> Services -> Data; Resources supplies shared text/constants, not entities. There is no separate Domain project. Data repository interfaces and implementations stay in Data, returning Data-owned read projections; Services maps these to report DTOs. Never introduce Data -> Services.

Proposed interface only in Phase 1:

```csharp
Task<ParticipationReportResult> GetEventReportAsync(
    int actorUserId, ParticipationReportRequest request,
    CancellationToken cancellationToken = default);
```

Actor ID is trusted server context, not a request field. The eventual implementation must load current account state/role; an integer parameter or pure actor snapshot does not authenticate anybody. Result is either a complete response or a stable failure (`InvalidRequest`, `Unauthenticated`, `Forbidden`, `NotFound`, `InconsistentData`, `TemporarilyUnavailable`); it contains no partial totals on failure.

All paths below are proposed repository-relative files, not files created by this planning task.

| Files | Responsibility |
| --- | --- |
| `ASI.Basecode.Services/Interfaces/IParticipationReportService.cs` | Async use-case contract; no concrete service/DI yet. |
| `ASI.Basecode.Services/ServiceModels/ParticipationReports/ParticipationReportRequest.cs` | Event ID, status, search, detail kind, raw page; no actor, client totals, page size or writable persistence fields. |
| Same folder: `ParticipationReportResponse.cs`, `ParticipationReportResult.cs` | Safe event header, event status/provisional flag, normalized scope, summary, distributions, detail page; typed failure. No EF entity serialization. |
| Same folder: `ParticipationSummaryResponse.cs`, `ParticipantReportRowResponse.cs` | Long registration/confirmed/attendee counts, nullable rate/no-show count; row registration/member IDs, name, student number, status, academic snapshots, submitted/check-in times, attendance state (absent/valid/invalidated), nullable no-show. No hashes, email, QR credential, private path or audit row dumps. |
| Same folder: `AcademicDistributionResponse.cs`, `ParticipationDetailPageResponse.cs` | Course/year counts; detail kind, detail total, fixed size, current/total pages, previous/next and rows. |
| `ASI.Basecode.Services/ParticipationReports/ParticipationReportCodes.cs` | Semantic role/status/detail/failure codes; not numeric lookup IDs. |
| Same folder: `ParticipationReportInputs.cs` | Immutable actor, event, registration/attendance and aggregate input snapshots; no EF attributes/navigation, DbSets or copied entities. |
| Same folder: `ParticipationReportRules.cs` | Supplied actor eligibility, request normalization, lifecycle applicability and integrity classification. No database/system clock. |
| Same folder: `ParticipationReportCalculations.cs`, `ParticipationReportPaging.cs` | Decimal rate, aggregate invariants, grouping/reference fixture calculations and safe paging. Reuse a merged compatible shared helper if available when implementation starts. |
| `tests/ASI.Basecode.ParticipationReports.UnitTests/` | Standalone net9.0 xUnit project plus request/rule, calculation, snapshot/distribution, paging and result-safety tests. No shared solution edit in Phase 1. |

Independent calculation inputs must explicitly describe a **complete** single-event authorized filtered cohort, never a displayed page. A small in-memory reference evaluator can validate links/classify/count supplied rows for tests; it is not the production retrieval strategy. For later preaggregated inputs require `0 <= A <= C <= R`, valid status partitions summing to R, each distribution sum=A, and completed no-show count=C-A. Do not derive no-shows from R-A. Negative counts, inconsistent sums, null required collections or overflow fail validation. These checks cannot prove the caller selected correct SQL rows or a complete cohort. Keep production SQL and the reference evaluator independently tested against manually calculated fixtures, not each other alone.

## 5. Phases and completion gates

### Phase 1 - independent contracts, calculations and unit tests

After explicit approval: (1) finalize section 3 proposals; (2) define safe DTOs/interface/codes; (3) implement pure validation/classification and arithmetic; (4) implement fixed paging contract; (5) add meaningful table-driven fixtures and tests; (6) build/test and write a handoff explaining evidence limits.

Permitted additions are the feature Services files and standalone test project above. No new production package should be necessary. Use compatible test package versions and `net9.0`. SDK-style Services includes new sources automatically. Do not make shared project/solution changes simply to run standalone tests.

Gate A: reviewed contracts and semantics; applicable cases in section 8 pass; no shared production wiring changes; results accurately labelled independent foundation. This is **not a working report endpoint or completed REP-01**.

Explicitly deferred: SQL/lookup scripts, scaffolded models, DbContext, repositories, UnitOfWork/transaction support, existing User/auth/session code, Startup/DI/AutoMapper, Resources generators, solution/existing project files, controllers, Razor, and teammates' Registration/Attendance/Certificate writers. No mock service registered as production success.

### Phase 2 - canonical Data and full-dataset SQL integration

Start only when the canonical data handoff is available and reconciled. Add proposed Data `Interfaces/IParticipationReportRepository.cs`, `Repositories/ParticipationReportRepository.cs` and Data-owned query/projection types. Add Services `Services/ParticipationReportService.cs` for authorization, normalized request orchestration, mapping and calculation. Coordinate any shared transaction/index changes with their owner; do not scaffold a second model set.

Gate B: real SQL Server integration tests establish exact query selection, full totals, consistent counts/rows, historical retention, mapped lookups and safe failures. EF in-memory or SQLite alone cannot prove SQL Server behavior. Missing SQL environment means gate remains pending.

### Phase 3 - MVC/HTTP and current authorization

Propose read-only `GET /events/{eventId}/participation-report` with query status/search/detail/page, using a thin `ParticipationReportsController`, coordinated DI and existing error conventions. Current active officer/admin only; an admin need not have a member profile. Participant inactivity must not be reused as a viewer authorization rule. Read current user role/state on each request, reject stale privileged claims, and apply event scope before counts. Bind actor only from validated identity. No frontend now.

Map invalid non-page input to 400, absent/invalid authentication to shared 401/login behavior, insufficient role to 403, missing/inaccessible event to non-disclosing 404, integrity/availability failures to generic server responses without partial data. Agree concrete mapping with WebApp conventions. Sensitive responses use no-store; GET has no business mutations and does not need a new antiforgery mutation flow. Do not rewrite auth to deliver reports; integrate the shared fix.

Gate C: real HTTP/session tests prove access, route/query tamper handling, no sensitive serialization, DI and service wiring, and no writes. Backend completion is distinct from future Razor acceptance. Full REP-01 acceptance still requires the deferred presentation integration and team system test.

## 6. Canonical handoffs and responsibility boundaries

| Owner | Required agreement/evidence before integration |
| --- | --- |
| Canonical SQL/Data teammate | Actual scripts/scaffold commit; seeded lookup IDs mapped to semantic codes; numeric users.user_id; FK/unique registration(member,event) and attendance(registration) constraints; retained rows and no active-only global query filters; snapshot nullability; UTC datetime2(3); safe Data read projections; transaction API and isolation configuration. Review query indexes together. |
| Registration/Event owner (Member 3) | Capture course/year/section once on signup, preserve through profile edits/cancellation; authoritative confirmation/status mapping and event linkage; retain pending/waitlisted at cutoff; explicit completion only. Supply representative fixtures and agreed search identity fields. REP reads these states, never promotes or closes registrations. |
| Attendance owner (Member 4) | Unique attendance link, invalidation predicate and paired metadata, retention/correction semantics, correction commit boundaries. Check-in requires confirmed registration. Supply valid/absent/invalidated fixtures and real concurrent-correction tests. REP does not call correction or duplicate its business logic. |
| Auth/shared access owner | Numeric principal mapping, current role/active checks, officer/admin policy, inaccessible-event behavior, session-expiry and demotion/deactivation tests. Pinca's ADM ownership overlaps documentation's Member 1 allocation; coordinate shared changes rather than duplicating auth. |
| Shared paging owner (Member 2) | Reuse fixed-size normalization/projection helpers if actually merged and compatible; settle long counts/provider offset bounds and search collation. Do not merge unrelated branches just for a helper. |
| Pinca | Own REP contracts, calculations, queries/integration and tests; coordinate CERT/ADM interactions without turning report reads into certificate/audit writers. Shared append-only activity_logs remains the audit system; routine report-view logging is not a new requirement. |

## 7. SQL aggregation, filtering and consistency strategy

Proposed read pipeline:

1. Validate authentication and request shape without revealing target data. Start a short coordinated read unit; load current actor and authorize role/event scope.
2. Load event, perform scoped integrity checks and build a reusable authorized registration query with supported filters. Retain inactive related records; never use an active-member join as historical eligibility. Resolve required links without silently hiding corruption via inner joins.
3. Use SQL `COUNT_BIG`/equivalent translated long counts and conditional predicates over the full R. Use existence/one-to-one attendance mapping to avoid multiplicative joins. Do not join certificates into the report. Validate canonical uniqueness rather than masking duplicate data with arbitrary DISTINCT.
4. Compute C and A with identical confirmed/valid predicates; completed no-shows use confirmed plus NOT EXISTS valid attendance (an invalidated row still means no valid attendance). SQL group actual attendees by captured course/year before paging. No materialization of all registrations to calculate production totals.
5. Count selected detail dataset, normalize/clamp page, then project only safe columns in stable order and apply the fixed 25-row slice. Summary and detail counts are distinct where detail kind=no_shows.
6. Map aggregates to Services DTOs, check invariants, calculate decimal rate and return one complete response. No SaveChanges or business mutation/audit transaction.

Preferred consistency proposal: all related report statements share a SQL Server SNAPSHOT transaction once the Data owner verifies/enables the required database setting. Ordinary read committed (including statement-level RCSI alone) does not guarantee a common snapshot across summary, grouping and detail queries. If SNAPSHOT is unavailable, agree and test a short SERIALIZABLE read alternative with appropriate indexes, lock ordering and bounded timeout/deadlock handling; do not silently fall back to inconsistent reads or NOLOCK. Retry the whole read at most once for a recognized transient failure; otherwise return unavailable, never mixed partial results.

Authorization is checked against current persisted state for every request; document its read-time boundary. A later account change affects subsequent requests, while an already authorized in-flight read can finish at its consistent read point. If the shared auth owner requires stronger revocation ordering, agree/test that before Gate C rather than claiming snapshot isolation revokes sessions. No new session/version schema is assumed.

Within a consistent read, clamp and page use the same detail count. Each navigation request is a new read and may change after correction/completion; stable sorting is not a promise of immutable pages across requests. A read timestamp is informational, not an audit snapshot/version token. Coordinate indexes beginning with event/status/registration access and the unique attendance registration key; choose final covering/grouping indexes from actual SQL execution plans and representative volumes, not invented benchmarks. Test provider offset limits explicitly and return a controlled failure for an unsupported offset rather than overflowing or returning the wrong page.

## 8. Test matrix and what each level proves

| Scenario | Phase 1 evidence | Later SQL/HTTP evidence |
| --- | --- | --- |
| Empty and zero denominator | R=C=A=0 -> N/A; pending-only cohort -> N/A; completed no-shows=0 | Correct SQL selection/empty response, no divide-by-zero |
| Formula and rounding | C=3/A=1 ->33.33; C=32/A=1 ->3.13; C>0/A=0 ->0; A=C ->100; reject negative/A>C/overflow | Decimal mapping and full authorized aggregates match known fixtures |
| Mixed statuses | Confirmed only expected; pending/waitlisted/cancelled excluded from A/C/no-shows but visible in participant list | Real lookup mapping and predicate agreement; no join inflation |
| Attendance correction | Absent/valid/invalidated classification; completed invalidated confirmed row becomes no-show | Actual retained invalidation changes totals/distributions without deleting history; certificate state does not bias queries |
| Lifecycle | Completed required for no-shows; published past start is not completed; cancelled separate; invalid draft attendance fails | Real completion/correction writes observed atomically; no scheduler assumptions |
| Academic snapshots | Changed current profile does not alter fixture distribution; both grouping sums=A; ordinal/text and numeric-year cases | SQL groups signup columns, never current profile; actual collation/trailing-space cases |
| Retention | Inactive subjects remain in supplied cohort | Account/member/type/venue deactivation does not remove rows through joins/global filters |
| Filters | Validate positive event, status/search/detail allowlist; whitespace/100/101 search; pending filter N/A; no-show view preserves cohort totals | All supported filters before aggregate/count/slice, escaped wildcard and injection-like inputs, other events excluded |
| Paging | Invalid pages ->1; counts 0/1/25/26/50/51; beyond-last; long arithmetic, fixed 25, empty navigation | More than 25 rows with attendees on later pages; totals unchanged across pages; equal submission times ordered by ID; provider limits |
| Integrity | Unknown/null state, duplicate links, mismatched IDs, bad metadata/snapshots and aggregate sums -> failure | Constraints and scoped checks catch corruption without silently dropping or repairing it |
| Permissions | Supplied active officer/admin eligible, member/inactive/unknown actor denied | Anonymous, expired/tampered cookie, stale role, deactivated actor, member URL tampering; no unauthorized counts/distributions/rows; admin without member profile allowed |
| Consistency/failures | Pure calculations make no concurrency promise | Controlled concurrent check-in/invalidation/completion between SQL statements; response entirely before or after; timeout/retry/cancellation no partial payload; unauthorized requests do not reach report data queries |
| Safe response/read-only | Meaningful serialization fixture omits secrets | HTTP no-store, no EF/QR/password/path leakage; no business table or audit mutation on GET |

Tests cover named scenarios and explicitly enumerated combinations, not an asserted exhaustive Cartesian matrix. Calculation tests over supplied totals do not prove SQL selection. Pure actor tests do not prove authentication. Unit tests cannot establish SQL constraints, isolation, rollback, session freshness, real writer compatibility or UI behavior.

## 9. Validation and delivery gates

After approved Phase 1 implementation, from the existing repository:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build ASI.Basecode.Services/ASI.Basecode.Services.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/ASI.Basecode.ParticipationReports.UnitTests/ASI.Basecode.ParticipationReports.UnitTests.csproj
git -c safe.directory=F:/AttendTaBakcend/AttendTa diff --check
git -c safe.directory=F:/AttendTaBakcend/AttendTa status --short --untracked-files=all
```

Keep AutoMapper 14.0.0 NU1903 as a separately reported known baseline warning, not an unrelated upgrade in REP. If restore runs out of disk, inspect free space and report; do not delete user files. Record actual passed/failed/skipped totals and unavailable checks. Later run solution build, real SQL integration and HTTP/session suites against explicit isolated test configuration.

Planning completion means sources/Git inspected and this plan ready for review only. Gate A is independent foundation; Gate B is verified persistence/query integration; Gate C is working authorized backend. Final feature acceptance additionally requires deferred UI and integrated team scenarios. PRs target team dev, require self-review, CI and at least one peer approval; whole-team system checks precede release. No direct main/dev push, automatic commit/push, branch deletion or new delivery-date promise. WBS estimates are not a verified REP implementation schedule.

## 10. Decisions for review

Settled: Pinca's assignment, report metrics, confirmed denominator, zero N/A, retained non-invalidated attendance, completed-event no-shows, signup academic snapshots, historical retention, 25-row details and page normalization, full authorized filtered totals, exclusions, project architecture and no code before approval.

Approval-time proposals: single-event scope; completed-only rate with provisional published counts; two separate attendee distribution lists; two-decimal away-from-zero rounding; exact course grouping; status/search filter allowlist and all-summary filter scope; participant/no-show detail kinds with separate detail count; stable submission-time/ID order; empty-page convention; strict integrity failure policy; DTO/error shapes and long-count arithmetic.

Integration-time decisions: canonical SQL/EF commit and lookup mapping; shared helper availability; search/grouping collation and provider offset limits; SNAPSHOT availability or approved SERIALIZABLE fallback; current-auth read boundary, route/error mapping, query indexes and test environment. These are handoffs, not permission to replace shared modules.

**Stop here for Pinca's review. Explicit approval is required before implementing REP-01 application code or tests.**
