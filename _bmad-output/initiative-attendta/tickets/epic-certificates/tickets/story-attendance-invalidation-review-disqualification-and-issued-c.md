---
id: 5
type: story
title: "Attendance invalidation review disqualification and issued-certificate lock"
parent: epic-certificates
covers: [R3]
after: [4, 4.6]
hitl: false
risk: high
tracker_id: "65"
remote: "https://github.com/rainyDayssss/AttendTa/issues/65"
tracker_status: backlog
---

# Attendance invalidation review disqualification and issued-certificate lock

## Description

Implements IAttendanceInvalidationHandler, which epic-check-in's invalidation transaction (4.6) declares and calls inside its own transaction: throws Forbidden when the registration's certificate is issued, and otherwise sets its pending_approval or eligible review to not_eligible with the correction reason, delivering R3.

## Requirements

Verify: Invalidating an attendance record whose review is pending_approval or eligible sets that review to not_eligible with the reason recorded in the same commit; invalidating one whose certificate is issued is refused with a Forbidden outcome, and the attendance row stays valid.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Integration implements the invalidation hook declared by 4.6 inside the existing transaction; it does not build a second invalidation screen or change presentation.
- Development check: build plus a focused invalidation-disqualification smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Backend-only: no per-ticket design critique or polish requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- The issued-certificate lock, the same-commit not_eligible write and the serialization against issuance stay final acceptance even while test authoring is deferred.

## Notes

- No register row of its own (2026-11-06 review): it replaces the 1.14 store's invalidation-to-review rule and must be in place before 6.7 removes M-11a.
