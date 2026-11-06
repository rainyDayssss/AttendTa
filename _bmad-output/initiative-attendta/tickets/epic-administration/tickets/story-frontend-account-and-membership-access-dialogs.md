---
id: 11
type: story
title: "Frontend account and membership access dialogs"
parent: epic-administration
covers: [R1, R2, R3, R4, R6]
after: [3, "1.13", "1.14"]
hitl: false
risk: medium
tracker_id: "149"
remote: "https://github.com/rainyDayssss/AttendTa/issues/149"
tracker_status: backlog
---

# Frontend account and membership access dialogs

## Description

Complete the account and membership access screens on the real 5.3 impact preview and the existing accounts list: deactivate and reactivate dialogs with a required reason, the consequence, and the blocked last-active-Administrator state, keeping account access distinct from membership eligibility. 5.11 fills the two access-action slot partials that 1.13 placed in the Accounts and Members list rows and changes no other markup in those lists; 2.13 changes only the Member Edit trigger and form. Until 5.4–5.7 exist, the commands run on demo data (M-14a, M-14b, M-15a, M-15b) and the lists show the changed state for the session. The preview's promise of a deactivation action is met by this action. Follow docs/DESIGN.md.

## Requirements

Verify: An Administrator previews an account's affected registrations, deactivates it with a reason and reactivates it; an Officer deactivates and reactivates a membership with a reason; an empty reason is refused; the last active Administrator shows the blocked explanation; the lists show the changed state; nothing on screen says the data is demo data. Account and membership states stay distinct on desktop and phone width.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-administration/epic-administration.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per `demo-mock-register.md` (rows M-14a, M-14b, M-15a, M-15b) where it does not; nothing on screen may show that data is demo data. Add or fill this ticket's register rows in the same pull request.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
- The backend account and membership mutation tickets serialize their shared administration source changes; this is a code-boundary dependency, not a permanent human/module assignment.
