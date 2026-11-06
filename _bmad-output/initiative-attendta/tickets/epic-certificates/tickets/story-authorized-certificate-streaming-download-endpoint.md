---
id: 8
type: story
title: "Authorized certificate streaming download endpoint"
parent: epic-certificates
covers: [R6]
after: [7, 12]
hitl: false
risk: medium
tracker_id: "68"
remote: "https://github.com/rainyDayssss/AttendTa/issues/68"
tracker_status: backlog
---

# Authorized certificate streaming download endpoint

## Description

Delivers R6 through a GET action that takes a certificate id and never a path, admits the owner (matching ActorUserId) or any caller with the Management policy, lets inactive memberships through, relies on the AD-8 session check that refuses inactive Accounts, and streams the file_path read through IPrivateFileStore.OpenReadAsync after a containment check as application/pdf.

## Requirements

Verify: The owner and a Management user each download an issued certificate as application/pdf; an unauthenticated request and an inactive Account are sent to sign-in, a non-owner Member and an unknown id receive the identical not-found response, and no request can name a path.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- The endpoint serves the 6.12 viewer/download affordance and 6.13 history link through one authorized route; it does not build a second screen or change presentation. Demo downloads do not close the functional issue.
- Development check: build plus a focused owner/Management/not-found smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Backend-only: no per-ticket design critique or polish requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- Per-request permission, identical not-found responses, inactive-Account refusal and path containment stay final acceptance even while test authoring is deferred.

## Notes

- Removes demo data M-11b (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
