---
id: 17
type: story
title: "Frontend My Events history host and extension slots"
parent: epic-events-registration
covers: [R12, R15]
after: ["1.13", "1.14"]
hitl: false
risk: medium
tracker_id: "137"
remote: "https://github.com/rainyDayssss/AttendTa/issues/137"
tracker_status: backlog
---

# Frontend My Events history host and extension slots

## Description

Build the member My Events / Participation History host with Registrations, Attendance and Certificates tabs, accurate status presentation (reuse RegistrationStatusDisplay and ManilaTimeFormatter) and separate pager state per tab, linked from the member navigation. Until 3.13 the registrations list is demo data (M-02) composed from existing reads where they exist, so the member's real registrations appear. Fill the My Events host and its Registrations tab that 1.13 created; keep its three slot partials (_PassPartial, the attendance-history partial and _CertificateLinkPartial) in place without editing them, because 4.10, 4.12 and 6.13 fill them in parallel. Follow docs/DESIGN.md.

## Requirements

Verify: A Member opens My Events from the navigation, sees their registrations including one just made on the real event page, switches the three tabs and pages each independently, and sees populated, empty and Not confirmed before cutoff states without dead ends; nothing on screen says the data is demo data.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per `demo-mock-register.md` (row M-02) where it does not; nothing on screen may show that data is demo data. Add or fill this ticket's register rows in the same pull request.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
