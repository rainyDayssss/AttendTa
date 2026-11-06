---
id: 9
type: story
title: "Member certificate download links in My Events"
parent: epic-certificates
covers: [R2]
after: [8, 13, 3.13]
hitl: false
risk: low
tracker_id: "69"
remote: "https://github.com/rainyDayssss/AttendTa/issues/69"
tracker_status: backlog
---

# Member certificate download links in My Events

## Description

Replace 6.13's demo certificate source with the real owner-scoped issued-certificate query and agreed link fields consumed by the existing _CertificateLinkPartial. Preserve the 3.17/3.13 host contract; only an issued certificate owned by the current member receives the authorized 6.8 download URL. No second My Events controller, card model or presentation rewrite.

## Requirements

Verify: A Member views a completed attended event in My Events and sees a download link for their issued certificate, while a review that is not yet issued shows no link.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- The real owner query consumes 3.13 and the 6.13 partial; it does not construct a second My Events controller, card model or presentation rewrite. Demo links do not close the functional issue.
- Development check: build plus a focused issued-versus-unissued link smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Backend-only: no per-ticket design critique or polish requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- Owner scoping and issued-only link visibility stay final acceptance even while test authoring is deferred.

## Notes

- Removes demo data M-12 (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
