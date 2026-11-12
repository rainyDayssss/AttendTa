# CERT-02: PDF Certificate Issuance and Download - Phased Plan

Owner: Pinca, Member 5. Prepared: 2026-11-02 (Asia/Shanghai).

Status: **Proposed plan for review only. No CERT-02 application code, PDF artifact or package is created by this document.**

## Integration quick reference

| Question | Summary |
| --- | --- |
| Provides | Proposed issuance/download/list contracts, issuance and access rules, immutable render-data contract; later one fixed PDF renderer and real use-case implementation. |
| Depends on | CERT-01 eligible state; canonical Data; current Auth; shared private storage/audit; Attendance's correction transaction protocol; shared My Events/event-attendee presentation. |
| Keep | Reviewed rules, DTOs, immutable snapshot contracts and tests. CERT-01 continues to own eligibility review; CERT-02 never auto-approves. |
| Connect/adapt later | Map canonical entities to inputs, reconcile semantic codes/paging/storage interfaces, add concrete repository/service/renderer, coordinate locking with Attendance, then wire MVC/DI. Never create a competing Certificate entity or storage system. |
| Completion evidence | Pure tests for supplied facts; real PDF inspection, filesystem failure tests, SQL uniqueness/rollback/concurrency tests, HTTP/session/private download tests. No phase alone proves all of these. |

Shared references: [coding standard](../CODING_STANDARDS.md), [integration tutorial](../INTEGRATION_GUIDE.md), [active plan index](README.md), [CERT-01 plan](CERT-01_Independent_Backend_And_Integration_Plan.md). Follow the phased plan, not stale historical checkout instructions.

## 1. Starting point and branch boundaries

Rechecked local state: only `F:\AttendTaBakcend\AttendTa` is registered as a worktree. Current branch remains `feature/participation-reports`, HEAD `174d1fd7e20c6a8b9095d9a351e1d058da550511`. No tracked changes; documentation is untracked. Planning here does not authorize implementing CERT-02 on the REP branch. Do not create another folder for this feature.

The earlier same-session live remote check found team dev at that SHA. This planning pass rechecked local state, not live remote freshness. Local CERT-01 is `e65146e`; its existing origin ref is `d0f1e01` with tests. Its code is not merged into the current team starter. Read it through Git when needed; do not merge the unrelated fork dev's 18 commits or whole feature branches merely for reference.

Before approved implementation, recheck status/worktrees/live team dev and agree a feature-only base, normally a `feature/certificate-issuance` branch from current team dev in an existing suitable checkout. Preserve all branches and uncommitted/untracked files; no reset, auto-stash, cleanup, commit or push is implied. CERT-01 integration requires its actual reviewed implementation, not just its document.

The inspected starter has net9.0 projects, starter User/auth code, and no canonical Certificate/Attendance entities, SQL scripts, private storage service or PDF renderer. No PDF package was found in Services/WebApp package references. A deployed database and teammate-private work have not been inspected. No applicable AGENTS.md was found in the inspected workspace previously; recheck before implementation.

## 2. Scope and source traceability

User instructions and reduced WBS control scope. Pinca owns CERT-01/02, REP-01 and ADM-01/02. Coordinate shared Auth/audit ownership despite Markdown assigning Administration to Member 1.

| Source | Binding consequence |
| --- | --- |
| `F:\AttendTaBakcend\docs\WBS_Account_Name_Application_Name_yyyymmdd.pdf`, p.2 CERT-02, read earlier this session | One retained fixed-template PDF per approved eligible participant; number, snapshots, issuer; duplicate prevention, file/database failure handling, private downloads and 25-row lists. |
| `AttendTa-System-Specification.md` under that requirements folder, v2.1 sections 2, 11, 14, 17-19 | Completed/valid/approved issuance, fixed landscape A4 with embedded fonts, immutable PDF, current access checks, private persistent local files, atomic SQL audit, seeded lookups and fixed pagination. |
| `AttendTa-User-Stories.md`, US-24/25/27/29 | Self-issuance under ordinary officer/admin rules; owner/management downloads; membership inactivity does not revoke history; no public PDFs or reissue. |
| `AttendTa-Sequence-Diagrams.md`, complete/approve/issue and authorized-download sequences | Prepare snapshots/file before final SQL recheck/commit; backend application/pdf stream, no GET mutation. |
| Accessible ERD attachment `C:\Users\cruiz\.codex\attachments\80a59153-8dec-4d7c-9b16-5807145bce3e\Pasted text.txt` | certificates has unique registration, nullable unique number, recipient/event/start snapshots, relative file_path, issued_by/at; UTC datetime2(3), numeric actor FKs. No issuance-job or token table. |
| CERT-01 active plan and corrected handoff | eligible -> issued belongs here; Attendance correction rejects issued state and shares serialization; prior 134-test result is historical and does not prove issuance races. |
| Repository README, coding standard, Git QA guidelines | Existing layers, no Data -> Services, canonical database-first models, feature PR to team dev, local checks/CI/peer review. |

No bulk issuance, editor/multiple templates, email, revocation, correction/reissue, public verification portal, digital signing scheme, PDF report export, attendance duration, new scheduler, frontend or auth rewrite. Certificate downloads are distinct from excluded report exports. Final UI acceptance remains later work in shared screens.

## 3. Rules and explicit contract proposals

### Issuance

Settled: authenticated **current active officer/admin** issues one eligible certificate for completed participation with retained non-invalidated attendance. Self-issuance uses identical checks/audit. Confirmation alone does not confer eligibility. CERT-02 does not create a missing CERT-01 row, reopen a decline, approve a pending certificate or change attendance.

| Current state | Result |
| --- | --- |
| Missing certificate | NotFound; no implicit preparation or approval |
| pending_approval / not_eligible | NotEligible; no transition/file commitment |
| eligible, completed, valid attendance, consistent linkage/review | May prepare, then revalidate and atomically commit issued metadata/audit |
| issued | Never generate/overwrite; proposed authorized repeat returns AlreadyIssued with existing safe metadata and no duplicate audit |
| Unknown/inconsistent state or linkage | InconsistentData; no silent repair |

Proposed integrity guards: positive numeric IDs; certificate -> registration -> event/member/user links agree; valid attendance is linked to that registration; registration is confirmed, consistent with Attendance's check-in invariant; eligible state has reviewer/time and no preexisting issuance fields. Treat conflicting issuance fields on an eligible row as corruption, not permission to overwrite. Recipient account/member inactivity alone does not retroactively disqualify valid historical participation; viewer/issuer authorization remains current.

Proposal for immutable content: capture recipient `users.full_name`, event title/start, generated unique certificate number, candidate issuance UTC time and issuer identity from trusted persisted/configured sources. Store all ERD-defined snapshots and issued_by/at; the rendered PDF also freezes issuer display text/artwork. Do not invent an issuer-name snapshot column. Later current names cannot be used to reconstruct an issued PDF. Configure organization-local display timezone explicitly; client Asia/Shanghai context is not proof of the organization's timezone. Never substitute server-local time silently.

Candidate time is captured during preparation and used consistently in PDF and persisted issued_at; it is not a claim to be the exact database commit instant. Final recheck compares every mutable content input (recipient/issuer display names, event fields and selected fixed configuration) as well as eligibility. If changed, discard/reconcile the unreferenced candidate and return conflict; do not commit metadata for different bytes or silently issue a stale-name PDF. No rowversion schema change is assumed; compare the captured facts under the shared synchronization protocol.

Proposed number format: `AT-` plus a server-generated GUID in N format (35 ASCII characters), within varchar(100), backed by canonical filtered unique index. No sequential/gapless numbering requirement is inferred. Storage uses a separate opaque per-attempt identifier so competing candidates never share a writable path. Confirm display format before implementation; uniqueness requires SQL enforcement, not only random generation.

### Downloads and lists

Settled: current active account owner or current active officer/admin may download an **issued retained file**. Link ownership through certificate -> registration -> member -> user; a member cannot request arbitrary owners' records. Membership inactivity does not block an active account's historical download; account inactivity does. Management does not need a member profile. Do not re-run current participation eligibility to revoke already issued downloads. Missing/invalid issued metadata fails safely; missing bytes do not trigger regeneration/reissue.

Proposed contracts provide two fixed list scopes: own issued certificates (owner inferred from actor), and management's event-scoped eligible/issued list integrated into the attendee workflow. No separate global dashboard or free-form search/sort. Apply authorization and scope before count/page. My list sorts issued_at DESC, certificate_id DESC; management sorts certificate.created_at ASC, certificate_id ASC. Fixed 25, malformed page ->1, beyond-last clamped, empty Page=1/TotalPages=0, no navigation. Reuse compatible shared paging; no per-feature general framework. Exact list route placement remains a shared-screen decision.

## 4. Proposed files, interfaces and layer placement

All names below are proposals relative to the repository; none is created now. Follow [coding standards](../CODING_STANDARDS.md), including file-local explicit nullability, CancellationToken for I/O, semantic failures and immutable safe contracts.

| File/folder | Responsibility |
| --- | --- |
| `ASI.Basecode.Services/Interfaces/ICertificateIssuanceService.cs` | Proposed IssueAsync(trustedActorId, request, cancellationToken); list own/management; OpenDownloadAsync(trustedActorId, certificateId, cancellationToken). No concrete implementation/DI in Phase 1. |
| `ServiceModels/CertificateIssuance/IssueCertificateRequest.cs` under Services | CertificateId, EventId for route linkage, expected status eligible. No supplied number, file path, actor, time, recipient name or template. |
| Same DTO folder: issuance result, list request/page/row types | Issued/AlreadyIssued or semantic failure; safe IDs/number/snapshots/issued time. No storage keys, physical paths or EF entities in HTTP responses. |
| `CertificateIssuance/CertificateIssuanceInputs.cs`, `CertificateIssuanceRules.cs`, `CertificateDownloadRules.cs`, `CertificateIssuanceCodes.cs` under Services | Pure actor/participation/certificate snapshots, transition/access checks, stable semantic codes. Reconcile CERT-01/shared constants; no competing certificate lifecycle. |
| `CertificateIssuance/CertificateRenderData.cs` under Services | Trusted immutable content contract; render-data validation and consistent snapshots. No HTML supplied by browser or EF dependencies. |
| `Interfaces/ICertificatePdfRenderer.cs` under Services | Render fixed template from render data to bounded output/stream; interface only initially. Reconcile stream ownership with shared storage before Phase 2. |
| `CertificateIssuance/CertificateDownloadContent.cs` under Services | Internal application result with readable stream, safe filename, application/pdf and optional length; IDisposable/async disposal contract. Never JSON-serialize this object; controller disposes after response completion, including disconnects. |
| `tests/ASI.Basecode.CertificateIssuance.UnitTests/` | Standalone net9.0 test project; issuance/download rules, snapshot validation, paging and safe-result tests. |
| Later: `Services/CertificateIssuanceService.cs` under Services | Real orchestration, trusted reads, renderer/storage coordination, final transaction/audit and safe download open. |
| Later: Data `Interfaces/ICertificateIssuanceRepository.cs`, `Repositories/CertificateIssuanceRepository.cs`, conditional `QueryModels/*` | Canonical projections, guarded transition, lookups and transactional reads/writes without hidden commits. Data-owned types only. Reuse equivalent shared repositories. |
| Later: feature renderer adapter under Services `CertificateIssuance/Rendering/` | Library-specific PDF implementation using one template. Reconcile actual placement with shared adapter convention; no new project. |
| Later: WebApp controller and coordinated DI | Thin issue/list/download actions; shared Auth, antiforgery and error mapping. |

Storage interfaces/adapters must reuse the shared storage owner's actual API. If absent, agree a narrow shared private-file contract and its location before implementing it; the feature does not establish a second public/private storage root. It must support exclusive candidate writes, safe open, bounded streams, atomic publish where supported, and cleanup only when safe. Resources owns agreed display text; fixed artwork/fonts are deployment assets packaged under the shared asset convention, not user uploads.

Stable failure proposals: InvalidRequest, Unauthenticated, Forbidden, NotFound, NotEligible, StaleState, InvalidAttendance, IncompleteEvent, InconsistentData, StorageUnavailable and OutcomeUnknown. Pure allowed decisions never become a persisted success response without a successful final commit.

## 5. Phase 1 - independent foundation

After explicit plan approval: define contracts/codes and immutable inputs, implement pure issuance/download decisions and render-data validation, define paging, then add table-driven tests and a precise handoff. No PDFs are generated yet. Use positive IDs and supplied actor/ownership facts, required snapshot lengths consistent with ERD, UTC semantics and safe result fields. Do not assert a fake file exists from a rule's boolean input.

No SQL/EF entities, migrations, repositories, UnitOfWork, shared auth/storage/audit, controllers/DI, existing project/solution edits or new production PDF packages in this phase. Do not modify CERT-01 on the REP branch. A proposed renderer interface is not proof of PDF output. Test doubles live only in tests.

Gate A: contracts/rules compile; applicable unit tests pass; shared production files untouched. It proves behavior for supplied facts, not trusted identity, file validity, SQL uniqueness or race safety.

## 6. Phase 2 - renderer, private storage and canonical persistence

### PDF and storage acceptance

Select the library only after verifying current license terms, net9/deployment support, font embedding and needed layout features from its official documentation. No particular library/version is approved by this plan. Package installation and shared project edits belong to this phase. One fixed landscape A4 page, embedded supported fonts, required number/name/event/start/issue/issuer content, controlled wrapping and a defined minimum readable size. Test maximum-length and Unicode names/titles; unsupported glyphs/overflow fail rendering before issuance, rather than silently truncating names or producing missing glyphs. Artwork/text/timezone need approval before final acceptance; no template editor is introduced.

Inspect rendered pages visually and validate PDF page size/count, extractable required content and embedded fonts programmatically. A `%PDF` prefix or passing mock is insufficient. Use real filesystem integration tests for safe path handling and failures. Persistent private root sits outside wwwroot and deployment; separate from public event/venue assets. SQL stores only a relative path <=500 characters. Generate paths server-side; reject traversal/rooted paths and enforce canonical containment including Windows junction/symlink escapes with the shared storage owner. Private ACLs prevent untrusted path replacement. Never use client filenames as storage paths.

Write each candidate exclusively to a temporary file, flush/close, validate, then publish under its unique final private candidate path without overwrite. No row points to partially written bytes. All later downloads open the retained file, never regenerate it. Backups must coordinate SQL metadata and files; restart/redeployment tests prove persistence. Disk-full, permissions and missing-root failures return controlled errors without changing eligible state.

### Proposed two-stage issuance protocol

1. Validate current active management actor and route linkage. Read canonical certificate/participation; return an authorized AlreadyIssued result for an already committed issuance, without generating another PDF or audit. Otherwise require eligible, completed, valid linked attendance and consistent metadata.
2. Capture trusted immutable content, candidate number/time and exclusive path; render/store validated candidate **outside a long-lived SQL write transaction**. Preliminary checks are advisory: no issued status yet.
3. Begin a short shared transaction. Reload/lock current actor, event, registration, attendance, certificate and content dependencies using the agreed global protocol. CERT-01 proposes actor -> event -> registration -> attendance -> certificate; reconcile recipient/issuer name locks with profile writers to avoid conflicting orders. Every relevant writer must participate. No certificate-only lock claim.
4. Recheck active role, linkage, eligible status, completion, non-invalidated attendance and captured content. If another issuer committed, do not overwrite; dispose this unreferenced candidate safely and return existing issuance metadata after authorization. If correction committed, reject. If content changed, reject stale preparation. Verify candidate remains available under the protected storage contract before setting issued.
5. Write status=issued, number, recipient/event/start snapshots, relative file_path, issued_by/at and updated_by/at. Preserve created and original review metadata. Append one proposed `certificate.issued` log with safe IDs/number/status/actor/time; no physical path, credentials or full entity dump. Save and commit once.
6. Return success only for a confirmed successful commit. A failed file operation cannot mark issued. A failed SQL/audit operation cannot leave partial database state. File and SQL are **not** one atomic transaction; handle residual candidates as below.

Unique certificate.registration_id and filtered unique certificate_number remain required. A number collision aborts; any retry starts a bounded fresh attempt with new number/content/path and full validation. No blind retry of a stale or ambiguous mutation. No new `issuing` status, job/outbox/session table or background scheduler is assumed.

### Failure and recovery boundaries

| Failure point | Required behavior |
| --- | --- |
| Render/write/validation before transaction | Leave certificate eligible; remove only this attempt's safely identified unreferenced incomplete/candidate file or retain for controlled cleanup if removal fails. |
| Known rollback after final checks/audit/save | No issued metadata or audit survives. Cleanup only after definitive rollback and absence of any database reference. |
| Connection loss/timeout during commit | OutcomeUnknown, not assumed failure. Do not delete candidate or auto-issue again. Query canonical state with a fresh connection and the candidate path/number; a matching committed issued row resolves success. If outcome cannot be resolved, retain file and return controlled retry/reconciliation guidance. |
| Response lost after successful commit | A later authorized issuance request returns AlreadyIssued; original bytes/number/issuer/time/audit remain unchanged. |
| Process crash before SQL reference | May leave orphan file; no false issuance success. Controlled operator reconciliation identifies candidates; no automatic blanket directory deletion. |
| Referenced PDF missing/unreadable later | Report unavailable safely, preserve issued metadata, investigate storage/restore exact backup. No automatic regeneration, revocation or ordinary reissue. |

Cleanup must not race an in-flight issuance whose file is not yet referenced. Prefer immediate per-attempt cleanup only after known completion/rollback; periodic/manual orphan cleanup runs under an agreed exclusion/maintenance protocol with active attempts drained and unresolved commits checked. File age alone does not prove orphanhood. No new cleanup scheduler is in scope. Restoration from an exact retained backup is an operational recovery procedure, not certificate editing.

Required races with real writers: two issuers (one committed artifact/audit), correction vs final issuance (correction first blocks issue; issuance first blocks correction), actor demotion/deactivation vs issuance, and name changes during render. Coordinate profile reads/writes if needed; state comparisons alone do not prevent a write after the final check. Read-only downloads enforce authorization at request time; they do not promise to revoke bytes already streamed when an account changes mid-transfer.

Gate B: actual PDF, filesystem and SQL tests pass, including failure injection and uncertain-commit safety. Record exact library/assets/configuration and protocol. A mocked storage test cannot prove disk durability; isolated SQL tests cannot prove a missing teammate writer follows the lock protocol.

## 7. Phase 3 - authorized HTTP integration

Proposed actions, adapted to shared controller conventions:

| Action | Boundary |
| --- | --- |
| POST `/events/{eventId}/certificates/{certificateId}/issue` | Current officer/admin, route linkage, antiforgery for cookie auth, server-owned content/actor; issue one only |
| GET `/my/certificates?page=...` | Current active account's issued records only; no ownerId override |
| GET `/events/{eventId}/certificates?page=...` | Management event scope, eligible/issued management rows, fixed paging; no mutations |
| GET `/certificates/{certificateId}/download` | Current active owner or management; issued metadata and retained stream; no path parameter or static URL |

Return application/pdf with safe server-generated filename based on certificate number (not raw personal names), content disposition attachment, no-store and no physical path in errors. Keep streams open for response lifetime and dispose on completion/disconnect. No GET creates PDFs, logs issuance, approves eligibility or repairs files. Routine download auditing is not prescribed; agree separately if required, without exposing sensitive content.

Map validation to 400, shared unauthenticated behavior to 401/login, insufficient management role to 403, absent/inaccessible certificate to non-disclosing 404, stale/not-eligible conflicts to 409, storage/unresolved outcome to controlled unavailable responses; corruption gets a generic server error and safe internal diagnostics. Propose AlreadyIssued as 200 with an explicit unchanged result; it is not a new issuance. Do not reveal file-existence details to unauthorized users. No fake DI bindings. Final routes and response shapes remain integration choices.

Gate C: real HTTP tests cover signed current sessions, route/owner tampering, antiforgery, safe responses, list counts and actual download bytes. Backend completion still leaves shared Razor acceptance pending.

## 8. Teammate handoffs and what changes

| Provider | Handoff required | CERT-02 action |
| --- | --- | --- |
| Data owner | Reviewed canonical commit, lookup IDs/codes, unique constraints, snapshot lengths/nullability, issued-state invariants, transaction API | Map entities to existing rule inputs; add narrow Data queries/guarded writes, no competing tables/models |
| CERT-01/Pinca | Real eligible decisions, preserved review metadata, preparation/correction integration | Consume eligible state, retain review history, share semantic meanings; no direct call to approve as part of issue |
| Attendance owner | Correction serializes on shared records, issued guard and rollback behavior | Integrate final issuance into the same protocol; jointly test both winner orders |
| Auth/ADM owner | Current numeric actor, role/access checks, compatible role/deactivation writer protocol | Reuse real reader/policy; no fake actor ID, auth rewrite or new session table |
| Storage owner (shared infrastructure, coordinated with Member 2) | Private root/ACLs, safe candidate/open API, durability, cleanup and backup procedures | Adapt renderer/output to shared storage API; no parallel root or public-static shortcut |
| Audit owner | Shared append-only writer, vocabulary, same-transaction guarantee | Append issuance once; no independent commit and no second log system |
| Event/member owners | Stable completed-event facts, current names and synchronized edits | Capture/compare correct content; preserve issued snapshots instead of rewriting them later |
| Shared screen/paging owners | My Events and attendee integration contracts, page helpers | Expose safe list/download/issue use cases; no new frontend now |

Follow the [handoff checklist](../INTEGRATION_GUIDE.md#4-handoff-to-request-from-each-teammate): actual signatures/commit, transaction owner, setup and test evidence. Missing dependencies are blockers for the corresponding later gate, not permission to build another owner's module.

## 9. Test matrix

| Concern | Independent Phase 1 | Real Phase 2/3 evidence |
| --- | --- | --- |
| Actor | Active officer/admin, member/inactive/unknown/missing actor; self-issuance same rules | Signed/current sessions, stale claims, actor-change race and CSRF |
| Lifecycle | Every certificate state, completed vs other event states, absent/valid/invalidated attendance, invalid IDs/linkage/review fields | Actual lookups/joins and concurrent correction/issuance; no duplicate certificate/audit |
| History | Inactive recipient does not erase eligibility; active owner with inactive membership allowed; other owner denied | Real ownership joins and retained downloads after profile/master changes |
| Snapshot contract | Required strings/lengths, explicit UTC time, immutable input and candidate mismatch rejection | PDF and SQL carry identical values, later edits leave exact retained bytes unchanged |
| Paging | 0/1/25/26/50/51, malformed/overflow/beyond-last pages, empty Page=1/TotalPages=0 | Authorized counts, ID tie-breakers, scope leakage, paging after issuance |
| PDF | Render-data rules only; no claim of visual or font proof | Landscape A4 single page, embedded fonts, Unicode/max lengths, no clipping, required text and readable layout |
| Storage | Contract failures can be simulated only | Disk full/permission denial, interrupted write, traversal/rooted/junction paths, concurrent exclusive writes, restart persistence, backup restore |
| Commit/audit | Decision result is not committed result | Forced save/audit/unique failure rolls back; uncertain commit never deletes a potentially referenced file; candidate cleanup excludes active attempts |
| Retry | AlreadyIssued semantic result unchanged | Lost-response retry preserves same number/path/bytes and one audit; candidate loser cleanup safe |
| Download | Supplied actor/owner/issued-state decisions | Exact application/pdf bytes, attachment/no-store, unauthorized/nonexistent handling, missing file without regeneration, stream disposal on disconnect |

Record tested scenarios, not an exhaustive claim based on a test count. Use SQL Server, not only EF InMemory/SQLite, for SQL-specific guarantees. Do not drop/reset a shared database. Rendering acceptance requires real output and inspection; package presence is insufficient.

## 10. Validation, completion gates and review decisions

After approved Phase 1 implementation, run from the selected feature checkout:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build ASI.Basecode.Services/ASI.Basecode.Services.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/ASI.Basecode.CertificateIssuance.UnitTests/ASI.Basecode.CertificateIssuance.UnitTests.csproj
git diff --check
git status --short --untracked-files=all
```

Use `-c safe.directory=<actual-worktree-path>` where required. Keep net9.0; report baseline AutoMapper NU1903 separately. Do not upgrade unrelated packages or delete user files for disk space. Later run real renderer/storage/SQL integration and HTTP suites plus solution build. Report actual results and skipped/unavailable checks honestly.

Planning gate: coherent sources/contracts/ownership and document review. Gate A: independent foundation. Gate B: real PDF/storage/persistence with coordinated writers. Gate C: authorized working backend. Full CERT-02 acceptance adds shared UI, operational backup/restore and team end-to-end evidence. Conventional feature PR to team dev with CI/peer approval; no direct main/dev push, automatic commit, push or fixed delivery promise.

Settled: one eligible certificate at a time; completed/valid/approved final check; fixed landscape A4/embedded fonts; immutable snapshots/PDF; owner/management private download; issued correction guard; retained history; no export/editor/reissue/email/bulk scope.

Approval-time proposals: interface/result shapes, AlreadyIssued behavior, single management/own list scopes and ordering, candidate numbering/time semantics, strict inconsistent-metadata handling, snapshot recheck policy and the proposed phase boundaries.

Integration-time choices: actual PDF library/license, approved artwork/fonts/signatory text/timezone, shared storage API and cleanup exclusion, canonical constraints/lookups, exact concurrency/commit-resolution protocol, route/error mapping and test environment. The proposed protocol is not proven until real writers and failure paths are tested.

**Stop for review. No CERT-02 code or test implementation is authorized merely by this plan.**
