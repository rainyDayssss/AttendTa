---
id: 4
type: story
title: "Account deactivation with registration cancellation cascade and session refusal"
parent: epic-administration
covers: [R3, R4]
after: [2, 3, 11, 1.1, 3.15]
hitl: false
risk: medium
tracker_id: "55"
remote: "https://github.com/rainyDayssss/AttendTa/issues/55"
tracker_status: review
---

# Account deactivation with registration cancellation cascade and session refusal

## Description

Applies Account deactivation with a mandatory reason in one ExecuteInTransactionAsync under the Administrator policy. When the target is an active Administrator it takes the app lock attendta:last-admin (AD-4 step 1; refusing deactivation if the target is the last active Administrator), then locks the target's users row, then calls IRegistrationCancellationService.CancelUpcomingForUserAsync(userId, AccountDeactivated, reason) from epic-events-registration (AD-6), which cancels the upcoming Registrations, clears their QR tokens and locks their events in ascending order. It then sets users.is_active to false and appends one user.deactivated Activity Log entry. Session refusal on the next request comes from epic-platform-access's OnValidatePrincipal (AD-8); this entry adds no session code and only verifies it. The confirm step opens the entry 3 preview dialog. It replaces 5.11's demo account commands.

## Requirements

Verify: Executing the UJ-5 flow, deactivating an officer account with a valid reason cancels their upcoming registrations, releases their seats without auto-promoting waitlisted members, records an attributable activity log entry, and immediately refuses their active session on their next request. An integration test in ASI.Basecode.IntegrationTests with parallel connections runs two account deactivations of the two active Administrators at once; exactly one succeeds and one Administrator stays active.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-administration/epic-administration.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Backend command only; the account deactivation dialog with preview, reason and last-admin states is already delivered by 5.11. This issue wires the real command and verifies session refusal, adding no competing dialog or session code.
- Development check: build plus one focused deactivate/cascade/session-refusal smoke; existing CI unchanged. Broad automation goes to 8.5 (#78), the full suite runs once at 8.4 (#77), final manual acceptance is 8.6–8.8.
- Final rules unchanged: the last active Administrator cannot be deactivated, seats are released without promotion, and the next request of a deactivated session is refused.
- Serialization: 5.4 (#55) and 5.5 (#56) land before 5.6 (#57) and 5.7 (#58) because all four change the shared administration source.

## Notes

- Removes demo data M-14a (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
