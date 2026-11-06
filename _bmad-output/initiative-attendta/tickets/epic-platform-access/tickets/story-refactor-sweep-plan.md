---
title: 'Platform access refactor sweep'
type: 'refactor'
ticket: '8'
created: '2026-11-06'
status: 'in-review'
baseline_revision: 'e9b703a161bbcd90d9f7274b3ea2539c10f75b0f'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.8 requires a sweep of deferred platform-access cleanup and legacy code, with a warning-free build and passing CI tests.

**Approach:** Inspect the epic's deferred findings and active production code, address any remaining in-scope debt without disrupting later epics, and record verification and any limits honestly. Preserve active private-file exception contracts outside the platform-access epic.

</frozen-after-approval>

## Implementation Notes

One-shot route: the identified deferred venue cleanup has already been fixed and covered by an integration test; no broad refactor is justified without a remaining in-scope finding.

- All prerequisite GitHub issues are closed. The old venue controller cleanup was replaced by `VenueService.DeleteBestEffort`, which logs a constant message without the exception; `VenueTransactionPhotoTests.OldPhotoCleanupFailureDoesNotUndoCommittedReplacement` verifies this behavior.
- The legacy symbols named in the story are absent from active production code except `InvalidDataException` in later shared private-file storage, event, and certificate code. Those active contracts are outside the platform-access sweep and must not be removed indiscriminately. The backup projects remain intentionally outside the solution per the platform baseline plan.
- Release solution build on SDK 10.0.403 passed with zero warnings and errors. The most recent successful PR CI run for predecessor commit `ef71b6d7a1baf3f48fef4f34ba6330388a51096b` ran SQL integration tests; it is not a run of this branch or its merge commit.
- Local service tests passed: 162 passed, 0 failed, 0 skipped.
- Local SQL integration run: 189 total, 185 passed, 4 failed because Poppler `pdftotext` is not installed; the four failures are certificate PDF inspection tests. GitHub Actions installs Poppler. The initial local attempt timed out and did not yield a test result.

## Plan Change Log

## Review Triage Log

- Current-branch GitHub Actions acceptance is unverified (confirmed): predecessor CI does not establish a run for this branch; a pushed branch and pull request are needed to trigger the existing workflow.

## Verification

- Build `ASI.Basecode.sln` in Release and check for zero warnings.
- Run the service tests and, where SQL Server is available, integration and concurrency tests.
- Inspect the platform-access legacy symbols and deferred findings; distinguish current CI evidence from a new branch run.
