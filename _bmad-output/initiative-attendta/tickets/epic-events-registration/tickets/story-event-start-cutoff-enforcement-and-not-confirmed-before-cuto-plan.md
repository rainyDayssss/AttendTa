---
title: 'Event start cutoff enforcement and Not confirmed before cutoff status'
type: 'feature'
ticket: '10'
created: '2026-11-06'
status: 'built'
baseline_revision: '5ccbd1cb2acfc116c845537e0153387edbcffd0f'
route: 'full'
route_source: 'auto'
risk: 'low'
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

**Problem:** The review, promotion and member-cancellation commands already refuse work at or after the Event start, but `Services.Tests` does not prove the exact boundary for approval, decline and promotion, and Pending/Waitlisted rows do not show the derived "Not confirmed before cutoff" label once the event starts.

**Approach:** Add fake-clock boundary tests for approval, decline, administrative cancellation, promotion and member cancellation; add a small derived-label helper and use it on the member event detail badge; prove Pending/Waitlisted rows keep their stored status and stay out of No-shows.

## Boundaries & Constraints

**Always:** Use the injected `TimeProvider`; the cutoff is `now >= starts_at` in UTC, so one tick before succeeds and the exact start refuses with `CutoffPassed`. Leave Pending/Waitlisted rows unchanged; never cancel, convert or promote them automatically. The label is display-only and applies only to Pending/Waitlisted after start. No-shows remain Confirmed-only. Reuse `ParticipationFacts.StartCutoffPassed` and the existing review/cancellation guards.

**Never:** Add a new registration status or schema column; add My Events (3.13); change review, promotion or cancellation behavior; hide or redesign the review queue; convert unresolved rows to Cancelled or No-show; log QR tokens.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Approve before start | Pending, Published, now = start − 1 tick | Confirmed or Waitlisted by seat/priority, audit written | No error |
| Approve at/after start | Same, now >= start | No row or audit change | `CutoffPassed` |
| Decline before start | Pending, now = start − 1 tick | Cancelled, audit written | No error |
| Decline at/after start | Same, now >= start | No row or audit change | `CutoffPassed` |
| Admin cancel before start | Confirmed/Waitlisted, now = start − 1 tick | Cancelled, audit written | No error |
| Admin cancel at/after start | Same, now >= start | No row or audit change | `CutoffPassed` |
| Promote before start | Waitlisted, free seat, now = start − 1 tick | Confirmed with 64-hex QR, audit written | No error |
| Promote at/after start | Same, now >= start | No row or audit change | `CutoffPassed` |
| Member cancel before start | Pending/Confirmed/Waitlisted, now = start − 1 tick | Cancelled, QR cleared | No error |
| Member cancel at/after start | Same, now >= start | No row change | `CutoffPassed` |
| Badge before start | Pending/Waitlisted | "Pending review" / "Waitlisted" | No error |
| Badge at/after start | Pending/Waitlisted | "Not confirmed before cutoff", stored status unchanged | No error |
| Badge at/after start | Confirmed/Cancelled | "Confirmed" / "Cancelled" | No error |
| No-shows | Completed event with Pending, Waitlisted and Confirmed rows | Only Confirmed rows are No-shows | No error |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Queries/ParticipationFacts.cs` — `StartCutoffPassed(nowUtc)` is `nowUtc >= event.StartsAt`; `NoShows` starts from `SeatHolders` (Confirmed only). Reuse both; do not duplicate the rule.
- `ASI.Basecode.Services/Services/RegistrationReviewService.cs` — `ExecuteAsync` guards approve/decline/admin-cancel with `StartCutoffPassed`; `PromoteNextAsync` guards promotion the same way. Behavior is already correct; only tests are missing.
- `ASI.Basecode.Services/Services/MemberEventService.cs` — `GetDetailAsync` already computes `CanCancel` with `StartCutoffPassed`; add `IsStartCutoffPassed` from the same captured `now`. `CancelRegistrationAsync` already refuses at/after start.
- `ASI.Basecode.Services/ServiceModels/MemberEventDetailViewModel.cs` — add `IsStartCutoffPassed`.
- `ASI.Basecode.Services/ServiceModels/RegistrationStatusDisplay.cs` (new) — `Label(status, startCutoffPassed)` and `IsNotConfirmedBeforeCutoff(...)`; the only place the derived label is decided.
- `ASI.Basecode.WebApp/Views/MemberEvents/Details.cshtml` — replace the inline status-label switch with the helper; use `badge-closed` for the cutoff label (class already compiled).
- `tests/ASI.Basecode.Services.Tests/RegistrationReviewTests.cs` — keep the existing authorization tests; add a fake-clock harness and the four review/promotion boundary tests.
- `tests/ASI.Basecode.Services.Tests/MemberEventServiceTests.cs` — extend `CancelRegistrationUsesTickPrecisionAfterBothLocks` to Pending/Confirmed/Waitlisted; assert `IsStartCutoffPassed` on the detail.
- `tests/ASI.Basecode.Services.Tests/RegistrationStatusDisplayTests.cs` (new) — label and cutoff-flag matrix.
- `tests/ASI.Basecode.Services.Tests/ParticipationFactsTests.cs` — add the No-shows exclusion case.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Services/ServiceModels/RegistrationStatusDisplay.cs` — add the derived-label helper — one rule, reused by the detail page and later by My Events.
- [x] `ASI.Basecode.Services/ServiceModels/MemberEventDetailViewModel.cs`, `ASI.Basecode.Services/Services/MemberEventService.cs` — add and populate `IsStartCutoffPassed` from the captured clock — the view must not read the wall clock.
- [x] `ASI.Basecode.WebApp/Views/MemberEvents/Details.cshtml` — use the helper for the badge label and class — show the cutoff label only where it applies.
- [x] `tests/ASI.Basecode.Services.Tests/RegistrationReviewTests.cs` — add a harness and boundary tests for approve, decline, admin cancel and promote — prove one tick before succeeds and at/after refuses with no mutation.
- [x] `tests/ASI.Basecode.Services.Tests/MemberEventServiceTests.cs` — extend cancellation boundary coverage to all open statuses and assert the detail cutoff flag — prove the member path.
- [x] `tests/ASI.Basecode.Services.Tests/RegistrationStatusDisplayTests.cs` — cover the label matrix — prove the derived label never changes the stored status.
- [x] `tests/ASI.Basecode.Services.Tests/ParticipationFactsTests.cs` — prove Pending/Waitlisted are not No-shows — protect the reporting rule.
- [x] Run build, service tests and the existing integration tests; run the UI critique/polish on the changed badge surface — confirm no regressions.

**Acceptance Criteria:**
- Given a Published event, when approval, decline, administrative cancellation or promotion runs one tick before start, then it succeeds and writes its normal audit row.
- Given the same state at or one tick after start, when each command runs, then it returns `CutoffPassed` and changes no registration, QR token or audit row.
- Given a Pending or Waitlisted registration after start, when the member views the event detail, then the badge reads "Not confirmed before cutoff" and the stored status is unchanged.
- Given a Completed event with Pending, Waitlisted and Confirmed rows, when No-shows are read, then only Confirmed rows are returned.
- Given the same registrations before start, when the member views the detail, then the normal Pending review/Waitlisted labels are shown.

## Implementation Notes

- `RegistrationStatusDisplay` owns both the label and the compiled badge class, so the view has no inline status switch left. `IsNotConfirmedBeforeCutoff` is the single derived rule and only matches Pending/Waitlisted.
- `MemberEventService.GetDetailAsync` captures `now` once, sets `IsStartCutoffPassed` from the canonical `ParticipationFacts.StartCutoffPassed` expression, and reuses that flag for `CanCancel` instead of evaluating the cutoff twice.
- The cutoff notice drops the green `notice-success` treatment; it keeps the neutral `notice` styling already used for Cancelled, so a "Not confirmed before cutoff" badge never sits inside a success box.
- `RegistrationReviewTests` gained a fake-clock harness (one published event, one eligible member, one registration) covering approve, decline, admin cancel and promote at `start - 1 tick`, `start` and `start + 1 tick`. The refusal cases assert the row, QR token and audit list are untouched.
- No production guard changed: `RegistrationReviewService` and `MemberEventService.CancelRegistrationAsync` already refused at/after start. The ticket added the proof and the derived label only.
- Verification: build clean (0 warnings, 0 errors); 271 service tests pass; 302 of 306 integration tests pass. The 4 failures are `CertificateTracerFlowTests`, which need Poppler's `pdftotext` on PATH (`ATTENDTA_PDFTOTEXT`) and are environment-only.
- UI: the Impeccable detector reported no findings on `Details.cshtml`. `badge-closed` is neutral (#f1f5f9 on #334155, ~9:1) and already compiled, so no Tailwind rebuild was needed.
- Review patch: `MemberSelfCancellationTests.DetailShowsTheDerivedCutoffBadgeWithoutChangingTheStoredStatus` now renders the detail page for Pending and Waitlisted rows after start and asserts the `badge-closed` label and the unchanged stored status. `BadgeClass` now returns null for a null status, matching `Label`.
- UI critique: Assessment A found no priority issues and endorsed the neutral badge reuse; Assessment B's detector returned no findings. The only optional note was label length on very narrow phones, but the PRD/SPEC require the full "Not confirmed before cutoff" wording, so it stays.

## Plan Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---------|---------|-------|----------|
| The cutoff badge is never exercised through the rendered view; AC 3 is proven only at the helper level. | medium | patched | Added `DetailShowsTheDerivedCutoffBadgeWithoutChangingTheStoredStatus` (Pending and Waitlisted) in `MemberSelfCancellationTests`; it asserts the rendered `badge-closed` label and the unchanged stored status. Integration suite now 308 tests. |
| `BadgeClass(null, …)` returns `badge-member` while `Label(null, …)` returns null. | low | patched | `BadgeClass` now returns null for a null status, matching `Label`; `NoRegistrationHasNoLabel` asserts it. |
| The AGENTS.md UI gate is not evidenced for the Razor change. | low | patched | Ran the Impeccable critique on `Details.cshtml`: Assessment A (design review) found no priority issues and called the neutral `badge-closed` reuse coherent; Assessment B (detector) returned no findings. |
| Ticket 9's plan status was flipped to `built` in this change set. | false | — | The user explicitly approved fixing the 3.9 plan status to `built` in the plan approval; it is intentional board bookkeeping, not ticket-10 code. |

## Design Notes

The label is derived at read time, not stored. `ParticipationFacts.StartCutoffPassed` is the single cutoff rule, and `RegistrationStatusDisplay` is the single label rule. The member detail page is the only member-facing badge today; 3.13 (My Events) will reuse the same helper. `badge-closed` already exists in the compiled stylesheet, so no Tailwind rebuild is needed.

## Verification

**Commands:**
- `dotnet build` — expected: succeeds with 0 warnings and 0 errors.
- `.\tests\ASI.Basecode.Services.Tests\bin\Debug\net10.0\ASI.Basecode.Services.Tests.exe -noColor` — expected: all tests pass, including the new boundary and label tests.
- `$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — expected: all tests pass; note any environment-only failures.

**Manual checks (if no CLI):**
- Open a published event detail as a member with a Pending or Waitlisted registration before start: badge reads "Pending review" or "Waitlisted".
- Advance the event to its start time: the same badge reads "Not confirmed before cutoff", and the registration row is still Pending/Waitlisted in the database.
