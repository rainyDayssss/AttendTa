---
id: 5
type: story
title: "Public Account Request submission with field validation and Argon2id hash"
parent: epic-platform-access
covers: [R1]
after: [1, 2, 3, 4, 9]
hitl: false
risk: medium
tracker_id: "16"
remote: "https://github.com/rainyDayssss/AttendTa/issues/16"
tracker_status: done
assignee: "FivePesos"
---

# Public Account Request submission with field validation and Argon2id hash

## Description

Allows an applicant to submit an account request with student number, academic details, and password, validating field constraints and uniqueness against existing users, members, and pending requests while storing the password solely as an Argon2id hash through IPasswordHasher. Adds the page-controller outcome mapping for DomainException (ModelState or TempData with POST-redirect-GET, and HTTP status with JSON { outcome, message } for AJAX), which later epics reuse. The request is anonymous and writes no audit entry. Uniqueness is checked first and enforced by the unique and filtered indexes.

## Requirements

Verify: An applicant submits valid registration details and sees the submission acknowledgment page, while duplicate submissions or mismatched confirmation passwords redisplay field errors without persisting credentials.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-platform-access/epic-platform-access.md
