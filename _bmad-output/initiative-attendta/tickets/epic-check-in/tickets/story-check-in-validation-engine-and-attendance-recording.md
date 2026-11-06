---
id: 3
type: story
title: "Check-in validation engine and attendance recording"
parent: epic-check-in
covers: [R4, R8]
after: [1, 2, "3.3", "3.7"]
hitl: false
risk: high
tracker_id: "46"
remote: "https://github.com/rainyDayssss/AttendTa/issues/46"
tracker_status: backlog
---

# Check-in validation engine and attendance recording

## Description

Adds LockAttendanceAsync to the attendance repository (UPDLOCK, HOLDLOCK, ROWLOCK on primary key seek, AD-4). Implements the shared check-in use case behind the scanner endpoint and the manual lookup (entry 5): a Management-only POST that runs in IUnitOfWork.ExecuteInTransactionAsync, takes the owner's users row then the event through repository Lock…Async methods (AD-4), and rechecks event match, Check-in open, Confirmed status and Eligible Participant through ParticipationFacts. A Valid Attendance returns its original time and inserts nothing; an invalidated attendance is refused. Otherwise it inserts one Attendance Record (method qr_code, recorder from ICurrentActor, UTC time) and appends one attendance.recorded entry whose payload excludes qr_token (AD-7). Responds per AD-9 with an HTTP status and JSON {outcome, message}, plus participant name and student number on success (fields Deferred to e4) (delivering the validation and recording part of R4, the attendance record part of R8, and SM-4 attendance concurrency).

## Requirements

Verify: Focused development check: a valid scan commits one attendance/audit row and a repeat returns its original time without another insert; an invalid/closed request cannot write. Preserve all refusal rules. Finale 8.5 adds the critical refusal matrix and real-SQL duplicate-scan race from finale-test-cases.md; 8.4 runs the complete suite with exactly one Attendance Record and one audit entry under concurrency.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Backend engine only; the scanner and manual surfaces are already delivered by 4.11 and are wired by 4.4 (#47) and 4.5 (#48), which are independent of each other once this engine and 4.11 exist. No competing UI host or service here.
- Development check: build plus one focused valid-scan/repeat-scan smoke; existing CI unchanged. The critical refusal matrix and the real-SQL duplicate-scan race are 8.5 (#78); the full suite runs once at 8.4 (#77); final manual acceptance is 8.6–8.8.
- Final rules unchanged: every refusal rule and the single-Attendance-Record guarantee stay acceptance criteria, and no existing guard test is removed.

## Notes

- Waits on 4.1 so passes carry real tokens before the real engine validates them.
- Removes demo data M-05 (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. From this commit the M-10a demo reviews read real attendance (register rule 12). The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
