# CERT-01 Phase 1 Handoff: Certificate Eligibility Review

## Integration quick reference

| Question | Summary |
| --- | --- |
| Status | Implemented foundation on the CERT feature branch; environment/test records below include historical facts. |
| Provides | Eligibility interface, review contracts, pure decision functions and unit tests; no live database/HTTP integration. |
| Depends on | Canonical data and current Auth; Event and Attendance transaction entry points; shared audit writer. |
| Connect or adapt later | Keep rule functions; feed real mapped snapshots, add preparation/review collaborators, and wire correction consequences once with Attendance. Do not infer a PDF dependency or create another certificate entity. |
| Completion evidence | Historical 134-test pass is limited to tested inputs. Re-run at integration; add SQL/HTTP and real concurrent-writer tests before completion. |

Shared references: [coding standards](../CODING_STANDARDS.md), [integration tutorial and handoff checklist](../INTEGRATION_GUIDE.md), [active plan index](README.md). These summaries do not authorize deferred implementation or supersede explicit scope decisions.

**Status:** Independent contracts, pure business decisions, and unit tests implemented, with a historical reported run of 134 passing tests. This does not establish exhaustive coverage of every planned combination. Persistence, canonical SQL/EF integration, and HTTP/auth integration remain deferred (Phases 2 and 3).

**2026-11-02 review update:** The environment and command results below describe the earlier implementation session. Current local branch is `feature/certificate-eligibility` at `e65146e`; origin is `d0f1e01`, including the test commit. Only `F:\AttendTaBakcend\AttendTa` is currently registered as a worktree, checked out on `feature/participation-reports`. Do not recreate or switch to the historical worktree/branch automatically. See [the plan index and review](README.md). Tests were not rerun during this documentation review.

---

## 1. Environment & Branch Base Resolution

- **Base commit:** `174d1fd7e20c6a8b9095d9a351e1d058da550511` (verified `upstream/dev`, the clean team starter commit).
- **Worktree path:** `F:\AttendTaBakcend\AttendTa-CERT01`
- **Branch:** `feature/certificate-eligibility-phase1` tracking `upstream/dev`.
- **Preserved checkout:** `F:\AttendTaBakcend\AttendTa` on `feature/certificate-eligibility` (based on fork dev `65c32a4`) remains untouched, preserving all untracked ADM-01 plans, handoffs, and unit tests.
- **Git policy:** No commit, push, or pull request was performed.

---

## 2. What Exists (Phase 1 Deliverables)

### A. Contracts & Service Models (`ASI.Basecode.Services`)
1. `Interfaces/ICertificateEligibilityService.cs`:
   - Outlines `GetPendingReviewsAsync` and `ReviewAsync` contracts with `CancellationToken`. No concrete implementation or DI binding.
2. `ServiceModels/CertificateEligibility/CertificateReviewListRequest.cs`:
   - Enforces event-scoped pending reviews with fixed 25-row page size.
   - Paging normalization: blank, whitespace, non-numeric, zero, negative, or overflow values normalize to page 1.
   - Paging clamping: requests beyond total pages safely clamp to `totalPages`.
3. `ServiceModels/CertificateEligibility/ReviewCertificateRequest.cs`:
   - Clean DTO accepting `CertificateId`, `Decision` (`approve`/`decline`), `ExpectedStatus`, and optional `DeclineReason`.
   - DTOs omit sensitive writable fields, but HTTP overposting protection has not yet been integration-tested.
4. `ServiceModels/CertificateEligibility/CertificateReviewSummaryResponse.cs`:
   - Event attendee queue projection (student number, full name, attendance timestamp, status, permitted actions).
5. `ServiceModels/CertificateEligibility/CertificateReviewPageResponse.cs`:
   - Items list, current normalized page, authorized total count, total pages, and navigation boolean flags.
6. `ServiceModels/CertificateEligibility/CertificateReviewResponse.cs`:
   - Result shape returning resulting status, reviewer ID, reviewed timestamp, and normalized reason.

### B. Pure Business Decision Rules (`ASI.Basecode.Services/CertificateEligibility`)
1. `CertificateEligibilityCodes.cs`:
   - Domain constants for roles (`admin`, `officer`, `member`), event statuses, certificate statuses (`pending_approval`, `eligible`, `not_eligible`, `issued`), decisions (`approve`, `decline`), and stable failure codes.
2. `CertificateEligibilityRuleInputs.cs`:
   - Small immutable snapshots (`CertificateReviewerInput`, `CertificateParticipationInput`, `CertificateStateInput`). Decoupled from EF Core entities.
3. `CertificateEligibilityDecision.cs`:
   - Decision record (`Allowed`, `Changed`, `ProposedStatus`, `FailureCode`, `NormalizedReason`).
4. `CertificateEligibilityRules.cs`:
   - `EvaluateCreation`:
     - Allows creation of `pending_approval` only for retained, non-invalidated attendees of completed events.
     - Fails closed on non-positive IDs.
     - Validates existing certificate snapshots (positive IDs, matching registration, recognized status) and returns `Changed: false` to prevent resetting history.
   - `EvaluateReview`:
     - Verifies reviewer is an active admin or officer (self-review allowed under identical rules).
     - Expected-state comparison detects mismatches in supplied snapshots; it does not provide database synchronization.
     - Validates positive registration/event IDs, guarding against `0 == 0` false matches.
     - Verifies event is completed and attendance is retained without invalidation.
     - Transitions `pending_approval -> eligible` on approval; rejects requests passing a nonblank `DeclineReason` during approval.
     - Transitions `pending_approval -> not_eligible` on decline with mandatory nonblank reason (max 1,000 chars, trimmed).
     - Consistently fails closed on null or non-positive inputs, missing attendance, or mismatched IDs.
5. `AttendanceCorrectionCertificateRules.cs`:
   - Pure consequence engine for when Attendance owner invalidates attendance:
     - Absent certificate: allows attendance correction with `Allowed: true`, `Changed: false`, and no certificate creation (normal before certificate preparation).
     - `pending_approval` and `eligible` unissued certificates transition to `not_eligible` with the correction reason.
     - `not_eligible` certificates preserve prior decision history without duplicate mutation.
     - `issued` certificates reject/block attendance correction (`issued_cannot_mutate`).
     - Corrupt/unknown status fails closed with `InconsistentData`.

### C. Standalone Unit Tests (`tests/ASI.Basecode.CertificateEligibility.UnitTests`)
- Isolated test project targeting `net9.0` referencing `ASI.Basecode.Services.csproj`.
- 134 unit tests covering:
  - **Creation scenarios:** Selected combinations of event, attendance and existing-certificate states; not a verified full Cartesian matrix.
  - **Review scenarios:** Selected certificate states and approve/decline decisions, including incomplete events and missing/invalidated attendance; not a verified full Cartesian matrix.
  - **Correction scenarios:** Absent certificate, recognized states, unknown and empty status strings. The null matrix input constructs an absent certificate; it does not test an existing certificate whose status is null.
  - **Missing & invalid inputs:** Null reviewer, participation, certificate, or request; non-positive IDs for reviewer, request, or certificate; unknown or null decision/expected-status strings; request/certificate ID mismatches; missing attendance during review.
  - **Boundary & formatting checks:** Exactly 1,000 raw characters accepted; 1,001 rejected; whitespace trimming on decline reasons; approval rejecting non-blank decline reasons.
  - **Pagination calculations:** Safe normalization for null/empty/whitespace/negative/overflow strings, clamping to total pages, and exact boundaries at 0, 1, 25, 26, 50, and 51 items.

---

## 3. Evidence Limits

Unit tests verify the covered business-rule scenarios, supplied expected-state mismatch handling, and pagination calculations. They operate on supplied snapshots and do not establish exhaustive correctness or concurrent database safety. Real SQL transaction isolation, atomic audit logging, session expiration, and HTTP authorization remain deferred to Phases 2 and 3.

---

## 4. Validation Results

Executed via `C:\Program Files\dotnet\dotnet.exe`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build ASI.Basecode.Services/ASI.Basecode.Services.csproj
# Succeeded: 0 Errors, 2 AutoMapper baseline NU1903 warnings.

& 'C:\Program Files\dotnet\dotnet.exe' test tests/ASI.Basecode.CertificateEligibility.UnitTests/ASI.Basecode.CertificateEligibility.UnitTests.csproj
# Passed! - Failed: 0, Passed: 134, Skipped: 0, Total: 134, Duration: 34 ms
```

Tracked and untracked status inspection:
```powershell
git -c safe.directory=F:/AttendTaBakcend/AttendTa-CERT01 status --short --untracked-files=all
# 11 Services additions, 1 documentation handoff, and 5 test project files untracked. No modified shared files.
```

---

## 5. Phase 2 & 3 Dependencies (Deferred)

Before beginning Phase 2 (Persistence & Transactions):
1. **Shared Data:** Reconcile with canonical SQL/EF entities from the database owner (table mappings, foreign keys, and unique constraint on `registration_id`).
2. **Event Completion Trigger:** Coordinate with Event owner so event completion transaction creates pending certificate reviews for valid attendees.
3. **Attendance Correction Trigger:** Coordinate with Attendance owner so attendance invalidation executes `AttendanceCorrectionCertificateRules` inside the attendance transaction.
4. **Audit Logger:** Coordinate append-only audit actions (`certificate.eligibility_approved`, `certificate.eligibility_declined`, `certificate.eligibility_invalidated`).
5. **Phase 3:** WebApp controllers, signed cookie authentication, current-role checks, and CSRF protection.
