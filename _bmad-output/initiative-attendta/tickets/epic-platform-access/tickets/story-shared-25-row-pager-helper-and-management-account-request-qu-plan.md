---
title: 'Shared 25-row pager and Management Account Request queue'
type: 'feature'
ticket: '6'
created: '2026-11-05'
status: 'built'
baseline_revision: 'ff761a9d47a7c23f1bf0b5c7b5682cfaed80340d'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Management has no paged account-request queue, and future lists lack consistent paging behavior.

**Approach:** Introduce shared paging contracts and a reusable Razor pager, then use them for an authorized account-request queue with status filtering and Manila-local request times.

## Boundaries & Constraints

**Always:** Fix page size at 25; apply authorization and filters before counting; order by request timestamp and unique request ID; normalize invalid pages to 1 and pages beyond the end to the last page. Preserve all other query parameters in pager links. Hide navigation when there is at most one page and display an empty state. Show times labeled Asia/Manila. Protect the queue with the Management policy and never expose password hashes. Keep SQL paging server-side.

**Never:** Implement submission or approval/rejection decisions, modify authentication or schema, expose management data to members, or change existing master-data behavior. Story 1.5 submission is not yet on this branch; test queue data using isolated fixtures and integrate with submission once that prerequisite lands.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Multiple pages | 26 filtered requests, including equal timestamps | 25 then 1 in stable timestamp/ID order | No error |
| Page normalization | Missing, malformed, zero, negative, or excessively large page | First page for invalid input; last page for valid out-of-range input | No error |
| Filtering | Status filter and multiple query parameters | Count and rows reflect the filter; previous/next retain other parameters | No error |
| Empty or single page | Zero or at most 25 matching requests | Appropriate empty state for zero; no pager navigation | No error |
| Access | Anonymous or member requests the queue | Login or access-denied policy response; no data disclosed | No error |
| Display | UTC database request timestamp | Correct Manila date/time and Asia/Manila label | No error |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Models/AccountRequest.cs`, `CanonicalSchemaMappings.cs` — existing status, timestamp and unique ID mapping; index supports status/time/ID. Never select password hashes for display.
- `ASI.Basecode.Data/Interfaces/IAccountRequestRepository.cs`, `Repositories/AccountRequestRepository.cs` — existing locking method must remain intact; add an asynchronous filtered count and bounded query without loading all rows.
- `ASI.Basecode.Services/ServiceModels/` — place `PageRequest`, non-generic `IPagedResult` and `PagedResult<T>` here, as specified by AD-13; make normalization safe for extreme inputs and zero results.
- `ASI.Basecode.Services/Interfaces/`, `Services/` and `ASI.Basecode.WebApp/Startup.DI.cs` — add a queue service and registration, using safe display-only models.
- `ASI.Basecode.WebApp/Controllers/`, `Views/` and `Views/Shared/` — add Management-policy queue, status filter and shared `_Pager` typed to `IPagedResult`; use existing accessible table/shell patterns from `Views/Member/Index.cshtml`.
- `ASI.Basecode.WebApp/Formatting/ManilaTimeFormatter.cs` — reuse existing UTC-to-Manila formatter. `Views/_ViewImports.cshtml` already imports it.
- `tests/ASI.Basecode.Services.Tests/`, `tests/ASI.Basecode.IntegrationTests/` — use existing xUnit v3 fixtures and authenticated HTTP patterns; SQL fixtures create disposable databases.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Services/ServiceModels/` — implement shared fixed-size page request/result contracts and safe page normalization.
- [x] `ASI.Basecode.Data/Interfaces/IAccountRequestRepository.cs`, `Repositories/AccountRequestRepository.cs` — add filtered count and deterministic paged projection, retaining locking behavior.
- [x] `ASI.Basecode.Services/Interfaces/`, `Services/`, `ServiceModels/`, `ASI.Basecode.WebApp/Startup.DI.cs` — add queue service and display models; use status filter and page metadata.
- [x] `ASI.Basecode.WebApp/Controllers/`, `Views/AccountRequest/`, `Views/Shared/_Pager.cshtml` — expose the authorized queue, filter and accessible previous/next links; preserve query values and display labeled Manila time and empty state. Add a management navigation link where appropriate.
- [x] `tests/ASI.Basecode.Services.Tests/`, `tests/ASI.Basecode.IntegrationTests/` — cover the matrix, SQL ordering and filtering before count, query preservation, authorization, and absence of hashes in HTML.

**Acceptance Criteria:**
- Given a Management user and requests across statuses, when they filter and navigate the queue, then each page contains at most 25 matching requests in deterministic timestamp/ID order and links preserve the filter.
- Given a zero-result or single-page filter, when the queue renders, then it displays an appropriate empty state if empty and no pagination navigation.
- Given anonymous or member access, when the queue is requested, then the existing authorization policy prevents access.
- Given a stored UTC request time, when the queue renders, then it displays the correct labeled Asia/Manila time without exposing password hashes.
- Given an invalid or out-of-range page, when the queue is requested, then it resolves to page 1 or the last page respectively.

## Implementation Notes

Implemented queue, shared paging contracts and partial, and tests. .NET 10 build passed with one existing nullable warning; 27 service tests and 98 LocalDB integration tests passed. Manual desktop/mobile inspection remains outstanding. Story 1.5 remains absent on this branch.

Follow-up (2026-11-06): the queue exposes All/Pending/Approved/Rejected tabs with counts, backed by one grouped count query, and the status filter accepts `all`. The queue now hosts the 1.7 decision actions through centered Approve/Decline dialogs. Search over name, email, and student number, an unconditional result count, a Requested sort toggle, and a mobile sticky decision layout landed in the same pass. Reviewer metadata and the table-preserving empty state remain deferred.

## Plan Change Log

## Review Triage Log

- false / rejected: The queue crosses the default assignment boundary in AGENTS.md, but the user explicitly requested story 1.6, which specifically requires this queue; that instruction takes precedence. No other findings.

## Design Notes

The account-request submission prerequisite (story 1.5) is absent from the current `dev`; this story can exercise the existing canonical table using seeded integration fixtures without taking over that feature. Confirm integration once 1.5 merges. Default to the pending queue and expose status filtering; do not add decision actions owned elsewhere.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — solution builds.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — service tests pass.
- With `ATTENDTA_TEST_SQL` set to a local SQL Server master connection, `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — isolated SQL and HTTP tests pass.

**Manual checks:**
- With disposable test data and an officer account, inspect queue filtering, links, empty state, Manila labels and keyboard navigation at desktop and mobile widths.
