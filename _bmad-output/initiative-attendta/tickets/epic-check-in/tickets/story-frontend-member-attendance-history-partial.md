---
id: 12
type: story
title: "Frontend member attendance history partial"
parent: epic-check-in
covers: [R1, R7, R9]
after: ["1.13", "1.14"]
hitl: false
risk: low
tracker_id: "145"
remote: "https://github.com/rainyDayssss/AttendTa/issues/145"
tracker_status: backlog
---

# Frontend member attendance history partial

## Description

Fill only the attendance-history slot partial that 1.13 placed in the My Events Attendance tab with a dedicated partial and its own page parameter. Show valid and invalidated records, event details and Asia/Manila times, with no member check-in or correction actions. Until 4.7 the records are demo data (M-08) from the same demo store the 4.11 scanner writes, so a check-in made at the door appears here. Do not edit the pass or certificate partials or the host model.

## Requirements

Verify: A Member with a seeded check-in opens Attendance History and sees that record with its Manila time, sees an invalidated record marked as such, pages independently of registrations at desktop and phone width, finds no self-check-in or correction control, and sees nothing saying the data is demo data.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per `demo-mock-register.md` (rows M-08) where it does not; nothing on screen may show that data is demo data. Add or fill this ticket's register rows in the same pull request.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
