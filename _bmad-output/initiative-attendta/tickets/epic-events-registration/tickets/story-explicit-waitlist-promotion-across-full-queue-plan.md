---
title: 'Explicit waitlist promotion across full queue'
type: 'feature'
ticket: '8'
created: '2026-11-06'
status: 'built'
baseline_revision: 'a16daa77560519dc3315ed665e71d5d63b67502e'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
  - '_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Officers can review registrations and waitlist them, but there is no explicit action to fill a released seat from the waitlist. Seats stay empty and the queue cannot progress.

**Approach:** Add a management-only "Promote next eligible participant" command to the registration review queue. It selects the earliest eligible waitlisted registration for the event, serializes with approvals on the existing user → event locks, confirms it with a fresh QR token, and writes the audit row in the same transaction.

## Boundaries & Constraints

**Always:** Management only; POST with antiforgery. Discover the candidate, lock the candidate's `users` row then the event, and recheck candidate, priority, eligibility, event state, cutoff and free capacity inside the transaction. Use `ParticipationFacts` for eligibility and waitlist order (`reviewed_at`, then `registration_id`) across the whole event queue. Require Published, `now < starts_at`, and confirmed count below the event's participant limit. Keep `reviewed_by`, `reviewed_at` and `review_notes`; set `confirmed_by`, `confirmed_at`, `updated_by`, `updated_at` and a lowercase 64-hex QR token. Append `registration.promoted` in the same transaction, never containing the token. No mutation on no candidate, no seat, stale state or audit failure.

**Never:** Promote automatically on cancellation, deactivation or any other action. Add a self-promotion prohibition. Promote a specific row out of queue order. Change approval, cancellation, event lifecycle or schema behavior. Expose QR tokens in audit or diagnostics. Hard-delete records. Expand into unrelated modules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Promote | Published, before start, free seat; earliest eligible waitlisted A, later B, ineligible C earlier | A confirmed with new QR; review metadata kept; one `registration.promoted` audit; B and C unchanged | No error |
| No candidate | No eligible waitlisted registration | No mutation | `Invalid` with a clear message |
| No seat | Confirmed count equals participant limit | No mutation | `CapacityUnavailable` |
| Stale | Candidate, priority, eligibility, owner or event state changed while waiting for locks | No mutation | `Stale` |
| Cutoff or status | At/after start; Draft, Cancelled or Completed event | No mutation | `CutoffPassed` or `Stale` |
| Concurrent promotion and approval | One free seat, pending P, waitlisted W | W confirmed, P waitlisted, exactly one confirmed, no overbooking | Loser gets its normal review outcome |
| Audit failure | Writer throws while appending | All promotion changes roll back | Exception surfaces; no partial state |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs` — add `DiscoverPromotionCandidateAsync(eventId)` returning a new `RegistrationPromotionCandidate(RegistrationId, UserId, MemberId, EventId, ReviewedAt)` record beside `RegistrationReviewIdentity`.
- `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` — join registrations → members → users, filter event + Waitlisted + `ParticipationFacts.EligibleParticipantMemberIds`, order with `ParticipationFacts.WaitlistPriority`, return the first or null. `HasEligibleWaitlistAsync` already shows the eligibility join pattern.
- `ASI.Basecode.Services/Interfaces/IRegistrationReviewService.cs` — add `PromoteNextAsync(int eventId, CancellationToken ct = default)`.
- `ASI.Basecode.Services/Services/RegistrationReviewService.cs` — implement the command with `RequireManagement`, `unitOfWork.ExecuteInTransactionAsync`, `users.LockUserAsync`, `events.LockEventAsync`, `registrations.GetForCancellationAsync` for the tracked recheck, and `audit.Append`. Extract the existing 32-random-byte lowercase 64-hex token generation into one private helper used by approval and promotion.
- `ASI.Basecode.Resources/Constants/AuditActions.cs`, `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `ASI.Basecode.Resources/Messages/Audit.resx`, `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — register `registration.promoted`, its description template and its `registrations` entity guard.
- `ASI.Basecode.WebApp/Controllers/RegistrationReviewController.cs` — add `[HttpPost, ValidateAntiForgeryToken] Promote(int eventId, string status, int? page)`; catch `DomainException` into `TempData["ErrorMessage"]`, success into `TempData["SuccessMessage"]`, and redirect to `Index` with the filter state.
- `ASI.Basecode.WebApp/Views/RegistrationReview/Index.cshtml` — show a Promote form only when `status=waitlisted` and an `eventId` filter is present; carry hidden `status`, `eventId` and `page`; include the antiforgery token.
- `ASI.Basecode.WebApp/Views/Event/Index.cshtml` — add a Review link per event row to `RegistrationReview/Index?eventId=<id>` so the event-specific queue and Promote action are reachable.
- `tests/ASI.Basecode.Services.Tests/RegistrationReviewTests.cs` — add non-management promotion rejection.
- `tests/ASI.Basecode.IntegrationTests/RegistrationPromotionTests.cs` (new) — cover the matrix, stale recheck, audit rollback, concurrent promotion/approval on SQL Server, and browser policy/antiforgery. Reuse `MemberEventTestSupport`, `SqlServerDatabaseFixture`, `AttendTaWebApplicationFactory`, `EventLockAttempts` and `FailingWriter` patterns from `RegistrationReviewTests.cs`.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs`, `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` — add whole-queue candidate discovery using the canonical facts and stable order.
- [x] `ASI.Basecode.Services/Interfaces/IRegistrationReviewService.cs`, `ASI.Basecode.Services/Services/RegistrationReviewService.cs` — add the locked promotion command, rechecks, confirmation fields and token helper.
- [x] `ASI.Basecode.Resources/Constants/AuditActions.cs`, `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `ASI.Basecode.Resources/Messages/Audit.resx`, `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — register safe `registration.promoted` audit vocabulary.
- [x] `ASI.Basecode.WebApp/Controllers/RegistrationReviewController.cs`, `ASI.Basecode.WebApp/Views/RegistrationReview/Index.cshtml`, `ASI.Basecode.WebApp/Views/Event/Index.cshtml` — add the management-only POST, queue Promote control and event review link.
- [x] `tests/ASI.Basecode.Services.Tests/RegistrationReviewTests.cs`, `tests/ASI.Basecode.IntegrationTests/RegistrationPromotionTests.cs` — cover the matrix, stale and concurrent outcomes, audit rollback, authorization and antiforgery.

**Acceptance Criteria:**
- Given a Published event before start with a free seat and waitlisted rows, when an officer promotes, then the earliest eligible waitlisted registration is confirmed with a new 64-hex QR token, its original review metadata is unchanged, and one `registration.promoted` audit row is written in the same transaction.
- Given no eligible candidate, no free seat, a stale candidate or priority, a non-Published event, or an at/after-start request, when an officer promotes, then no registration or audit changes and the matching domain outcome is returned.
- Given concurrent promotion and approval for the last seat, when both complete against SQL Server, then exactly one registration is confirmed and the event never exceeds its participant limit.
- Given a member or anonymous request, or a POST without antiforgery, when promoting, then the request is rejected and no state changes.

## Implementation Notes

- Implemented in an isolated worktree (`bmad-3-8-promotion`) because the shared main tree was concurrently carrying another story's uncommitted work. Only the 3.8 promotion changes are present here.
- Promotion reuses the existing review transaction, user → event lock order, `ParticipationFacts` eligibility and waitlist facts, tracked recheck, cancellation-safe row refresh and audit writer. The QR token helper is shared with approval.
- Added a second concurrency test for two simultaneous promotions: one confirms the earliest candidate, the other gets `Stale`, and the later candidate stays waitlisted.
- Verification: build clean; 198 service tests pass; 17 promotion integration tests pass; 276 of 276 non-Playwright integration tests pass. The 5 certificate Playwright tests fail only because the Chromium headless shell is not installed on this machine.
- Quick review patched the waitlist display order to match promotion order, hid the Promote control on an empty queue, and made the Promote redirect always land on the waitlist queue.

## Plan Change Log

## Review Triage Log

- Quick review pass 1: 0 high, 1 medium, 3 low, 3 false, 0 maybe-false.
  - `RegistrationRepository.ReviewQueue` order vs promotion order — **medium**, patch. The waitlist screen ordered by `SubmittedAt` while promotion used `ReviewedAt`; fixed by ordering the waitlisted queue with the canonical waitlist fact and added `WaitlistQueueUsesPromotionOrder`.
  - Promote control on non-promotable events — **low**, rejected. The server guards the state; hiding the control needs event-state-aware UI beyond the plan, and the existing queue shows the same optimistic-action pattern.
  - Promote control on an empty waitlist — **low**, patch. Added `Model.Items.Any()` to the view condition.
  - Review link on every event row — **false**. The plan explicitly says a Review link per event row; the queue remains a read-only view for non-published events.
  - `Promote` accepts any posted `status` — **low**, patch. The action now always redirects to the waitlist queue and the hidden status field was removed.
  - Stale-candidate test only mutates the existing candidate — **false**. The priority case makes the second registration the new earliest, exercising `current != candidate`; the test asserts `Stale`.
  - No-seat test only at equality — **false**. The equality case catches a `>=` to `>` regression, and the happy path and concurrency test cover filling the last seat.

## Design Notes

Candidate discovery happens before the transaction only to identify which user row to lock. The recheck inside the transaction proves the same registration, owner and `reviewed_at` still hold; any change throws `Stale` rather than promoting a different person. The candidate's user lock serializes with a member cancellation or account/membership change for that person, and the event lock serializes seat allocation with approvals. No self-promotion prohibition is added because SPEC §9.3 explicitly says none applies; queue priority still governs.

## Verification

**Commands:**
- `dotnet build` — expected: succeeds.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all tests pass.
- Set `ATTENDTA_TEST_SQL` to a local SQL Server master connection string, then `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — expected: all tests pass, including the new promotion and concurrency tests.

**Manual checks:**
- Sign in as an officer, open Events → Review for a Published event, switch to Waitlisted, click Promote, and confirm the earliest eligible member is confirmed while the Activity Log shows `registration.promoted` without a token.
