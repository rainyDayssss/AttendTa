# AttendTa integration guide

Status: tutorial and handoff guide for Pinca's slices, 2026-11-02. It explains how to connect the existing phased plans; it does not authorize all phases or invent teammate APIs. Service names not yet implemented are proposals. Read [coding standards](CODING_STANDARDS.md), then the relevant [active plan](implementation-plans/README.md).

## Quick reference: what connects to what

WebApp controller -> Services use-case interface -> Services implementation -> Data repository/UnitOfWork -> canonical SQL Server schema.

Pure rules/calculations are called by the Services implementation using trusted facts loaded through Data. They do not fetch data or authenticate a caller. A DTO carries a request/result; an interface declares an operation; neither is a working database connection. Constructor injection and DI connect the real implementation only after its dependencies exist.

This guide supplies the shared integration procedure. Each feature document has its own quick reference. It replaces no code. Its completion evidence is the feature's real SQL/HTTP test suite plus a truthful handoff.

## 1. Who needs whose work

| Slice | What Pinca supplies | What Pinca needs | Who calls it |
| --- | --- | --- | --- |
| ADM-01 | IAccountAdministrationService contract, account rules and cancellation-eligibility reference; concrete service later | Canonical accounts/registrations/audit; current Auth; Registration's shared cancellation operation in the caller's transaction | Admin MVC actions call the account service. Membership owner reuses Registration cancellation, not account deactivation to change membership. |
| CERT-01 | ICertificateEligibilityService, creation/review rules and certificate correction consequences | Canonical event/registration/attendance/certificate data, Auth, audit and coordinated completion/correction transactions | Review actions call eligibility service. Event completion invokes an agreed preparation collaborator; Attendance invokes certificate consequences inside its correction workflow. |
| REP-01 | Proposed IParticipationReportService, formulas and safe report contracts; queries later | Canonical participation data, signup snapshots, current Auth, fixed paging conventions | Report MVC action calls report service. Report reads persisted state; it does not invoke Registration/Attendance mutation services to calculate totals. |
| CERT-02 | [Proposed issuance/download contracts and rules](implementation-plans/CERT-02_Independent_Backend_And_Integration_Plan.md), then fixed renderer and real use cases | CERT-01 eligible state plus canonical Data, shared private storage/Auth/audit and issuance/correction concurrency agreement | Issuer/download actions call issuance service; no PDF implementation inferred from CERT-01 or from this plan. |
| ADM-02 | [Proposed read-only history contracts and rules](implementation-plans/ADM-02_Independent_Backend_And_Integration_Plan.md), then real query/service | Shared append-only activity_logs, safe producer payloads and current admin authorization | Admin history GET calls IActivityHistoryService; no second audit store, Serilog reader or append on viewing. |

Member numbers are coordination labels, not permissions. Pinca explicitly owns ADM-01/02 as well as CERT/REP; documentation's Member 1 Administration allocation must be reconciled with the team. Data owner supplies canonical schema; Auth owner supplies shared current-account checks; Registration/Event and Attendance owners retain their workflows.

## 2. Keep, adapt, add - do not blindly replace

| Item during independent work | Integration action |
| --- | --- |
| Pure rules and calculations | Keep reviewed business behavior and tests. Feed real mapped facts to it; do not replace rules with unchecked SQL or claims. |
| Immutable input snapshots | Keep as rule inputs. Add Services mapping from canonical Data types; snapshots never become EF entities. |
| Public DTOs/service interfaces | Keep unless a reviewed compatibility change is necessary. Update callers/tests/docs together when changing them. |
| Feature-local role/status constants | Map to seeded lookup codes/IDs. Adopt equivalent shared constants when available without changing meanings. |
| Feature-local paging calculation | Adapt to the compatible shared helper after comparing empty/out-of-range behavior; retain contract tests. Do not ship competing general helpers. |
| Test fixtures/fakes | Keep in tests only. Production receives repository-loaded facts. Do not replace test fixtures with live database calls in unit tests. |
| Missing concrete service/repository | Add when canonical dependencies are ready. Nothing needs to be deleted merely because Phase 1 exposed only an interface. |
| Proposed class/method/route names | Reconcile with actual teammate APIs. Do not rename their entities just to match the planning inventory. |
| Starter User/auth implementation | Shared-owner integration required. Never map numeric actor IDs to starter string IDs by guesswork or duplicate Auth to make a feature run. |
| Cancellation/correction logic | Reconcile one production workflow with its owner; remove duplication only as a reviewed change with tests. A certificate consequence is not the full attendance correction command. |

No shared SQL, auth or repository replacement is automatic. Preserve existing callers and data when integrating. Prefer a small explicit mapping/adapter where contracts genuinely differ, not a second business engine.

## 3. Walkthrough: connecting REP-01

1. Obtain the Data owner's exact reviewed commit and setup instructions. Verify fields, lookup codes, unique links, snapshots and invalidation representation in an isolated database. An ERD alone is insufficient.
2. Agree report scope with Registration/Attendance: confirmed denominator, valid attendance predicate, signup academic snapshots and explicit completion. Confirm the proposed filter/rounding choices in the REP plan.
3. Implement the Data report query using canonical models and Data-owned projections. Authorize/filter before count/group/page; no active-member filter for historical subjects. Counts cover the full cohort, not 25 visible rows.
4. Implement ParticipationReportService. Resolve the current actor, validate the request, coordinate consistent reads, map projections, apply approved calculations and return safe DTOs. Production must not load all rows just to reuse the small in-memory test evaluator.
5. Add the thin controller and scoped DI registrations using the shared Auth convention. The browser provides event/filter/page, never actor identity or computed totals.
6. Test known SQL fixtures, later-page attendees, corrected attendance, stale actor roles and concurrent source changes. Unit calculation success alone does not complete integration.

For ADM, the analogous use case owns one account + shared cancellation + audit transaction. For CERT, Event/Attendance owners orchestrate completion/correction and call agreed certificate collaborators inside that same transaction. Do not inject mutually dependent Event and Certificate services; agree a narrow collaborator to keep the dependency direction acyclic. The exact transaction API and lock order remain shared integration decisions.

## 4. Handoff to request from each teammate

Copy this into a collaboration note; fill unknowns as pending instead of guessing:

| Field | Required information |
| --- | --- |
| Provider and consumer | Named owner/module; which feature will consume it |
| Version | Branch/PR/commit inspected, merge status and canonical file paths |
| Contract | Actual interface/method signature, request/result shapes, nullability and failure codes |
| Data | Canonical keys, lookup mapping, constraints, snapshot fields and invalidation semantics |
| Trust | Where actor/time come from and which layer rechecks current permissions |
| Transaction | Who begins/commits, shared context requirements, whether any method saves independently, lock order |
| Setup | DI lifetime, safe configuration instructions and isolated test prerequisites; no secrets in the note |
| Evidence | Tests run, exact results, known omissions and compatibility risks |
| Consumer action | Mapping/helper/DI changes needed, files to retain, remaining blockers |

## 5. Keeping documentation useful

Every feature plan/handoff should start with a short **Integration quick reference**: status, what it provides, what it needs, what gets connected/adapted, and completion evidence. Link the shared standard/tutorial instead of copying their full text into every document. Keep requirements/specification sources unchanged by this documentation cleanup; they define scope, not local wiring instructions.

A plan records intended behavior and deferred decisions. A handoff records what actually exists, where, and what was tested. Update the handoff after an implementation phase, and the plan when a decision changes. Keep historical branch/test observations labelled with their date. Superseded plans link to their replacement and must not present a fresh executable tutorial.

Before calling a connection complete: inspect actual code, run applicable tests, verify real DI/no fake success, and record outstanding UI or cross-module work. No teammate has been contacted and no API handoff has been received merely because it appears in this guide.
