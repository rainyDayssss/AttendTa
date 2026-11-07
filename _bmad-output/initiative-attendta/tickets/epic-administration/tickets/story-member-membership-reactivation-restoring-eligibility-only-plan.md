---
title: 'Member membership reactivation restoring eligibility only'
type: 'feature'
ticket: '7'
created: '2026-11-07'
status: 'built'
baseline_revision: '9e61a008d795b39704d03d5821c2ff6df7ae32c1'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** The membership reactivation action behind 5.11's dialog still writes only the in-memory demo store (register M-15b), so `members.membership_status_id` stays inactive, the member never becomes an Eligible Participant again, and no `member.membership_reactivated` audit row exists.

**Approach:** Implement the real `IMembershipAccessCommands.ReactivateAsync` in one `ExecuteInTransactionAsync`: Management actor from `ICurrentActor`, trimmed reason validation, lock the member's `users` row, set `members.membership_status_id` to active with `updated_by`/`updated_at`, and append exactly one `member.membership_reactivated` Activity Log row. Delete the M-15b demo provider, overlay and store seam. No view or dialog change.

## Boundaries & Constraints

**Always:** Management (officer or admin) caller checked in the service from `ICurrentActor`; trimmed 1–1000 character reason validated before any state read; `users` row lock inside the transaction; one audit row carrying the reason and the inactive→active status; leave `users.is_active`, cancelled Registrations and Certificates untouched.

**Never:** change `users.is_active`; restore, cancel or promote Registrations; touch seats, QR tokens or Certificates; add a competing dialog or change Razor markup; change the schema, EF mappings or authentication; add broad automation beyond the one focused smoke (8.5 owns broad cases).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reactivate inactive membership | Inactive member, valid reason | Status active, `updated_by`/`updated_at` set, one `member.membership_reactivated` row with trimmed reason and inactive→active | No error |
| Cancelled Registrations | Target has Cancelled rows with null QR tokens | Rows unchanged; no seat or token restored | No error |
| Eligibility restored | Inactive then reactivated member with a Pending Registration | Officer can approve it through `ParticipationFacts` | No error |
| Login unchanged | `users.is_active = 1` throughout | Linked account still signs in | No error |
| Already active | Status active | No write, no audit row | `Invalid` "This membership is already active." |
| Unknown member | Unknown member id | No write | `NotFound` "Member not found." |
| Bad reason | Blank or over 1000 trimmed characters | No state read, no write | `Invalid` with field `reason` |
| Non-Management caller | Member role or anonymous | No write | `Forbidden` "Access denied." |

</intent-contract>

## Code Map

- `ASI.Basecode.Services/Services/MembershipAccessService.cs` — `DeactivateAsync` is the transaction/lock/audit pattern to mirror; `ReactivateAsync` currently refuses with the not-available message and becomes the real command; reuse `RequireManagementActor`, `RequiredReason`, `MembershipStatusAuditValues`.
- `ASI.Basecode.Services/Services/AccountAccessService.cs` — `ReactivateAsync` is the closest sibling: no app lock, no cancellation cascade, one audit row.
- `ASI.Basecode.Data/Interfaces/IMemberRepository.cs` — `GetMemberRecord(int)` (carries `UserId`), `GetMember(int)` (tracked), `UpdateMemberAsync`.
- `ASI.Basecode.Data/Interfaces/IUserRepository.cs` — `LockUserAsync(int userId)`.
- `ASI.Basecode.Data/Queries/ParticipationFacts.cs` — `EligibleParticipant` reads `user.IsActive && member.MembershipStatusId == Active`; this is the eligibility the command restores.
- `ASI.Basecode.Services/Services/RegistrationReviewService.cs` — `ExecuteAsync` rejects approval when `EligibleParticipant` is false, so reactivation re-enables the officer approval.
- `ASI.Basecode.Resources/Constants/AuditActions.cs` — add `MemberMembershipReactivated = "member.membership_reactivated"`.
- `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `Messages/Audit.resx` — action template; `ActivityLogWriter` throws without one.
- `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — extend the `members` entity-type guard with the new action.
- `ASI.Basecode.Services/Interfaces/IMembershipAccessCommands.cs` — update the contract comment; both commands are real.
- `ASI.Basecode.WebApp/Controllers/MemberController.cs` — POST policy/antiforgery already correct; remove the `IAccessStateOverlay` dependency and its two `Apply` calls.
- `ASI.Basecode.WebApp/DemoData/DemoMembershipAccessCommands.cs` — delete (M-15b).
- `ASI.Basecode.WebApp/DemoData/DemoAccessRegistration.cs` — delete; `DemoDataRegistration.cs` — drop its call and the switch-off overlay line, update the comment.
- `ASI.Basecode.WebApp/DemoData/IAccessStateOverlay.cs`, `DemoAccessStateOverlay.cs`, `NoOpAccessStateOverlay.cs` — delete; no demo field remains.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — delete `_memberships`, `TryGetMembershipStatus`, `SetMembershipStatus`, `ClearMembershipStatus`, `DemoMembershipStatus`.
- `ASI.Basecode.WebApp/Startup.DI.cs` — `MembershipAccessService` registration already exists; no change.
- `tests/ASI.Basecode.IntegrationTests/AdminAccountReactivationTests.cs:232`, `AdminMembershipDeactivationTests.cs:268-332` — the deleted overlay and demo membership provider are referenced; update those demo cases to assert the real command/row.
- `tests/ASI.Basecode.IntegrationTests/AdminMembershipReactivationTests.cs` — new focused reactivate/eligibility smoke.
- `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-15b `removed`; rules 7–8, detail, source/store and known-limits sections.

## Tasks & Acceptance

**Execution:**
- [ ] `ASI.Basecode.Resources/Constants/AuditActions.cs` — add `MemberMembershipReactivated`.
- [ ] `ASI.Basecode.Resources/Messages/AuditDescriptions.cs`, `Messages/Audit.resx` — add the `MemberMembershipReactivated` template.
- [ ] `ASI.Basecode.Services/Services/ActivityLogWriter.cs` — guard the new action to entity type `members`.
- [ ] `ASI.Basecode.Services/Services/MembershipAccessService.cs` — implement `ReactivateAsync` in one transaction with the `users` row lock, active status, and one audit row; drop the not-available constant.
- [ ] `ASI.Basecode.Services/Interfaces/IMembershipAccessCommands.cs` — update the contract comment.
- [ ] `ASI.Basecode.WebApp/DemoData/DemoMembershipAccessCommands.cs`, `DemoAccessRegistration.cs`, `IAccessStateOverlay.cs`, `DemoAccessStateOverlay.cs`, `NoOpAccessStateOverlay.cs` — delete the M-15b demo seam.
- [ ] `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` — drop the access registration call and the switch-off overlay registration; update the comment.
- [ ] `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — remove the membership status store and record.
- [ ] `ASI.Basecode.WebApp/Controllers/MemberController.cs` — remove the overlay dependency and calls.
- [ ] `tests/ASI.Basecode.IntegrationTests/AdminAccountReactivationTests.cs`, `AdminMembershipDeactivationTests.cs` — remove references to deleted types; keep the demo cases green against the real command.
- [ ] `tests/ASI.Basecode.IntegrationTests/AdminMembershipReactivationTests.cs` — add the focused reactivate/eligibility smoke.
- [ ] `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-15b `removed` and correct the detail and limits.

**Acceptance Criteria:**
- Given an inactive member with cancelled and pending registrations, when Management posts a reactivation with a reason, then the profile reads active with `updated_by`/`updated_at` set, the cancelled registration stays Cancelled with a null QR token, `users.is_active` stays true, and exactly one `member.membership_reactivated` row carries the trimmed reason and inactive→active.
- Given the reactivated member, when an officer approves one of their Pending Registrations, then it becomes Confirmed and login still reaches `/MemberEvents`.
- Given a blank or overlong reason, an already-active membership, an unknown member, or a Member-role caller, then nothing is written and no `member.membership_reactivated` row exists.

## Implementation Notes

No subagent capability was available, so the plan was implemented directly. `ReactivateAsync` mirrors
`AccountAccessService.ReactivateAsync` plus the member `users` row lock from `DeactivateAsync`; it
reuses `RequireManagementActor`, `RequiredReason` and `MembershipStatusAuditValues`. The M-15b demo
seam (provider, overlay, registration file and store state) was deleted, which also removed the
Members list and access dialog overlay; the SQL row is authoritative in both switch positions.
`AdminMembershipDeactivationTests` and `AdminAccountReactivationTests` had references to those deleted
types, so their demo cases now assert the real command instead.

## Plan Change Log

## Review Triage Log

### 2026-11-07 — Quick review (self-review; no reviewer subagent available)
- verdicts: 0 findings
- findings: none. Checked the acceptance criteria against the endpoint test, the transaction/audit
  shape against `AccountAccessService`, and the deleted demo types against the build (0 errors).

## Design Notes

Reactivation is deactivation's inverse without the removal-only parts: no application lock and no cancellation cascade, so cancelled Registrations, seats, QR tokens and Certificates are untouched by construction. Eligibility returns because `ParticipationFacts.EligibleParticipant` reads the same `members.membership_status_id` the command writes. The audit payload reuses the private `MembershipStatusAuditValues` record, so the log carries only the status. Removing the overlay is safe because no demo writer remains on the access surface; the SQL row is authoritative in both switch positions.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — expected: 0 warnings, 0 errors.
- With `ATTENDTA_TEST_SQL` set to a local SQL Server master connection: `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter "FullyQualifiedName~AdminMembershipReactivationTests|FullyQualifiedName~AdminMembershipDeactivationTests|FullyQualifiedName~AdminAccountReactivationTests"` — expected: all pass.
