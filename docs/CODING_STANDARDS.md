# AttendTa coding standards

Status: working standard for Pinca's slices, documented at Pinca's request on 2026-11-02. Based on the starter repository README, observed code conventions and agreed v2.1 requirements. This is not a claim that the whole team has already approved new conventions or that existing code complies. Explicit user decisions and agreed requirements control behavior.

## Quick reference

Use this document when writing or reviewing code. Use [the integration guide](INTEGRATION_GUIDE.md) to connect teammates' work, and [the plan index](implementation-plans/README.md) to find the active feature contract. This standard provides conventions, not runnable services; it replaces no entities or shared infrastructure. Completion means the applicable review checks below have evidence, not merely that code compiles.

## 1. Layer responsibilities

| Project | Put here | Keep out |
| --- | --- | --- |
| WebApp | Thin MVC controllers, binding, validated identity, HTTP policy/status mapping, antiforgery for cookie mutations, eventual Razor | EF queries, report formulas, eligibility rules and transaction orchestration |
| Services | Interfaces, safe DTOs, pure business rules, calculations, use-case orchestration and Data-to-DTO mapping | A second set of persistence entities, raw HTTP concerns in rules, browser-supplied actor identity |
| Data | Canonical database-first entities/mappings, repository interfaces/implementations, Data-owned projections, SQL query and transaction support | References to Services types, independent business workflows, duplicate scaffolded models |
| Resources | Shared user-facing text, labels and agreed constants | Entities, database access and workflow logic |

Services already references Data and Resources. Keep that direction; no Data -> Services and no new Domain project. A small feature folder is appropriate; a new general framework requires an actual shared need. Data may translate agreed business predicates into SQL; Services defines their meaning and tests verify both implementations agree.

## 2. C# style and contracts

- Keep `net9.0`, existing project names and folder organization. Match surrounding formatting in existing files; do not mass-reformat.
- New code uses four-space indentation, PascalCase types/methods/properties, camelCase parameters/locals, `_camelCase` private fields, `I`-prefixed interfaces and `Async` suffixes for asynchronous operations. Prefer one main public type per file named after that type.
- New isolated feature files may use file-scoped namespaces and immutable records, matching ADM/CERT additions. Existing block namespaces/classes remain valid. Neither style requires rewriting other modules.
- DTOs describe allowed inputs and safe outputs; do not serialize EF entities. Actor ID/time, SQL lookup IDs, secrets and mutation results are not browser-writable fields. Use separate result types for pure decisions and committed outcomes.
- Represent optional values explicitly. With nullable annotations, use a file-local `#nullable enable` where needed rather than changing settings for a shared project. Guard external inputs at runtime. Do not turn missing booleans into false or unavailable report rates into zero.
- Use semantic status/failure codes; map canonical lookup keys explicitly. Never assume a string code equals its SQL FK value. Use decimal for report percentages and the approved rounding contract.
- Prefer clear small functions and named scenario inputs. Comments explain constraints, intent and ownership; public cross-module contracts document nullability, caller obligations, transaction ownership and whether a method saves. Avoid comments that merely repeat code.

## 3. Services, dependencies and I/O

- Depend on service/repository interfaces through constructor injection. Pure rules need no DI, database, HTTP context or hidden system clock. Pass trusted time/facts explicitly.
- Real I/O methods should be asynchronous and accept/propagate CancellationToken. Do not wrap pure arithmetic in Task.Run or block tasks with `.Result`/`.Wait()`.
- WebApp calls Services use cases. A Services use case can call another owner's Services collaborator where an agreed workflow requires it. Data never calls back into Services; avoid circular service dependencies too.
- Use Data-owned projections for queries and map to Services DTOs. Do not pass IQueryable to WebApp. Keep filtering, counts, grouping and paging server-side for production lists/reports.
- No fake production success or NotImplementedException service registered in DI. A Phase 1 interface is deliberately unwired until its implementation and dependencies exist.

## 4. Persistence and integration

- Reuse the canonical SQL/scaffold supplied by the Data owner. Never recreate tables/entities to match a plan's suggested class names. Preserve records and identifiers; no destructive cleanup or implicit schema repair.
- Multi-record mutations have one transaction owner. Called repositories/collaborators participate in its context/transaction and do not independently commit. Business changes and required append-only audit records succeed or fail together.
- Use trusted numeric user IDs and UTC times mapped to datetime2(3). Do not copy starter OS-user actor fields, local DateTime.Now metadata, reversible password encryption or login bypass behavior.
- Enforce uniqueness/relationships in SQL and revalidate workflow conditions under the coordinated concurrency protocol. A pure rule, expected status or preliminary read is not a lock.
- Reports do not call SaveChanges or repair source data. Use authorized filtered full-dataset aggregates and a tested consistent-read strategy. Apply stable ordering with an ID tie-breaker before paging.
- Fixed page size 25; malformed/missing/nonpositive pages normalize to 1, beyond-last clamps, empty result is Page=1/TotalPages=0 without navigation. Reuse a compatible shared helper when it is available. Client page-size/sort text cannot become arbitrary SQL.

## 5. Security and errors

- Identity comes from validated authentication; current persisted role/account state controls access. Distinguish caller eligibility from historical participant inactivity. Authorize before returning counts, details or diagnostics.
- Cookie-authenticated mutations require antiforgery protection. GET must not mutate business state. Sensitive responses use the agreed no-store policy.
- Parameterize database queries and allowlist filters. Escape wildcard characters when the contract promises literal search. Do not log passwords/hashes, cookies, QR tokens, connection strings or physical private paths.
- Pure rules return stable semantic failures. Services orchestrates outcomes; WebApp maps them to HTTP and Resources-backed user messages. Unexpected errors are handled/logged through shared infrastructure without exposing internals. Never catch a failure and return success or partial report totals.
- Reuse the Auth/audit owners' work. These rules do not authorize a feature-local auth rewrite.

## 6. Tests and review

Use meaningful unit tests for rules, arithmetic, invalid inputs and boundary cases. Assert outcomes, not merely that DTO properties exist. Use fixed inputs/time. Describe covered combinations honestly.

Use real SQL Server tests for selection, FK/unique constraints, transactions and concurrency; use HTTP/session tests for actual authorization, binding, antiforgery and safe responses. Neither pure tests nor EF InMemory proves these properties. Run standalone Phase 1 tests by project path without unrelated solution edits. Use `C:\Program Files\dotnet\dotnet.exe`; report actual results and unavailable checks. Preserve the known AutoMapper warning as a separately reported baseline issue.

Before a feature is presented as integrated, verify:

1. Layer boundaries and ownership follow this standard and the active plan.
2. Real dependencies, numeric/semantic mappings and DI are connected; no placeholders remain on the success path.
3. Required unit, SQL and HTTP checks have results appropriate to the completion claim.
4. Diff contains no secrets, competing shared models, unrelated packages or formatting changes.
5. Remaining limitations and the precise implementation commit are recorded in the handoff.

Use conventional commits and feature PRs targeting team dev, with CI and peer review under the Git QA guidelines. Commit/push only when requested. This Markdown standard is not an installed analyzer or formatting gate; `.editorconfig`/analyzer/CI enforcement is a separate coordinated change, not added here.
