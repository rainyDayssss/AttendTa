---
id: 13
type: story
title: "Frontend member certificate history and download-link states"
parent: epic-certificates
covers: [R2, R6]
after: ["1.13", "1.14"]
hitl: false
risk: low
tracker_id: "146"
remote: "https://github.com/rainyDayssss/AttendTa/issues/146"
tracker_status: backlog
---

# Frontend member certificate history and download-link states

## Description

Fill the _CertificateLinkPartial slot that 1.13 placed in the My Events Certificates tab with the member's pending, eligible, declined, issued and unavailable states and an issued-only View/Download. Until 6.9 and 6.8 exist the list is demo data (M-12) from the same demo store 6.12 issues into, and View/Download streams the same in-memory render. Own only the certificate partial and its scoped assets, not the My Events controller, pass or attendance partial.

## Requirements

Verify: A Member with the seeded issued certificate opens Certificates, views and downloads the PDF, sees review states without a download, and an empty state when there is none; keyboard, phone width and independent paging work; nothing on screen says the data is demo data.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per `demo-mock-register.md` (rows M-12) where it does not; nothing on screen may show that data is demo data. Add or fill this ticket's register rows in the same pull request.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
