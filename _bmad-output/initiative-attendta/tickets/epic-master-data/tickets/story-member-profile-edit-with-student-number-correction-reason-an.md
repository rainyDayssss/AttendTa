---
id: 6
type: story
title: "Member profile edit with student-number correction reason and audit"
parent: epic-master-data
covers: [R2, R6]
after: [1.3, 1.7, 1.9]
hitl: false
risk: medium
tracker_id: "25"
remote: "https://github.com/rainyDayssss/AttendTa/issues/25"
tracker_status: done
assignee: "wigglebop25"
---

# Member profile edit with student-number correction reason and audit

## Description

Updates Member edit so Management changes users.full_name, course, year level, section, mobile and membership type under the users row lock, with email read-only; a student-number correction requires a reason validated inside the transaction and a unique check whose 2601/2627 failures map to Duplicate; UpdateMember no longer writes membership status, and MemberService.ToggleStatus with its controller action are removed for e5; MemberService.CreateMemberFromApprovedAccountRequest drops actorUserId; the approval call in epic-platform-access entry 7 moves to the new signature (async comes from its entry 11); it sets membership_status_id to active and stays the only member create path, called inside the e1 approval transaction; every write logs explicit old and new subsets through IActivityLogWriter.

## Requirements

Verify: An Officer edits a member's full name and academic fields, corrects the student number with a reason, and sees old/new values and the reason in activity_logs with no password_hash; email is read-only; the member form has no status control and no ToggleStatus action exists; a duplicate student number is refused as Duplicate on student_number; with seeded registrations and certificates rows, a member edit leaves their values unchanged; and an Account Request approval creates the member through the migrated signature.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
