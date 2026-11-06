---
id: 4
type: story
title: "Frontend per-event report with summary breakdowns and participant table"
parent: epic-participation-reports
covers: [R1, R2]
after: ["1.13", "1.14"]
hitl: false
risk: medium
tracker_id: "140"
remote: "https://github.com/rainyDayssss/AttendTa/issues/140"
tracker_status: backlog
---

# Frontend per-event report with summary breakdowns and participant table

## Description

Build DESIGN screen 18 from the 1.13 contracts: event selector, registration status counts, Confirmed and Valid attendee summary, rate (N/A at zero Confirmed; final only for completed events), completed-only no-shows, course/year breakdowns and a 25-row paged detail table. Until 7.1–7.3 the report reads demo data (M-13a, M-13b, M-13c) computed from the real registrations and the demo store's attendance and completion state, so its numbers agree with the dossier and scanner; no invented global metrics or exports. Own the report surface and its scoped assets. Follow docs/DESIGN.md. The event selector is the shared partial from 1.13.

## Requirements

Verify: An Officer selects a completed event in the demo store and sees counts, rate, no-shows, breakdowns and detail rows that agree with that event's seeded check-ins; a published event shows no final rate, a cancelled one no no-shows, and an event with zero Confirmed shows N/A; summary totals do not change with detail paging; the layout works on desktop, keyboard and phone width; nothing on screen says the data is demo data.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-participation-reports/epic-participation-reports.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per `demo-mock-register.md` (rows M-13a, M-13b, M-13c) where it does not; nothing on screen may show that data is demo data. Add or fill this ticket's register rows in the same pull request.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
