---
id: 18
type: story
title: "Frontend member event feed and detail visual alignment"
parent: epic-events-registration
covers: [R6, R7, R10, R11, R15]
after: ["1.13", 10]
hitl: false
risk: low
tracker_id: "142"
remote: "https://github.com/rainyDayssss/AttendTa/issues/142"
tracker_status: backlog
---

# Frontend member event feed and detail visual alignment

## Description

Align the existing real member feed/detail screens to DESIGN screens 11–12: cover hierarchy, metadata, registration-status panel, truthful capacity/deadline messages and cancellation dialog. Preserve the ready registration/cancellation services and RegistrationStatusDisplay shipped by #133; no backend rule rewrite. Own only MemberEvents presentation and scoped assets, not the My Events host.

## Requirements

Verify: A Member browses events, opens a readable responsive detail page and sees consistent Pending/Confirmed/Waitlisted/Cancelled/cutoff states, with working existing registration/cancellation behavior and no unrelated mock replacing a ready real source.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only, real backend only; this ticket adds no demo data.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
