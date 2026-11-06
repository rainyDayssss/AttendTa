---
id: 13
type: story
title: "Frontend shell routes and isolated prototype data contracts"
parent: epic-platform-access
after: [9]
hitl: false
risk: low
tracker_id: "136"
remote: "https://github.com/rainyDayssss/AttendTa/issues/136"
tracker_status: backlog
---

# Frontend shell routes and isolated prototype data contracts

## Description

After a short inventory of the current routes and screens against docs/DESIGN.md, prepare the shared role-appropriate navigation and, for every screen the demo needs, the route, an authorized controller action returning its view with a typed screen contract, and an empty view: My Events, the officer event dossier, the door scanner, the certificate ledger and viewer, the participation report, and the account and membership access dialogs. Surface tickets fill those views and change only their own action's model loading. Create the My Events host with Registrations, Attendance and Certificates tabs and three empty slot partials (_PassPartial on each registration card, the attendance-history partial and _CertificateLinkPartial) that render nothing visible until their owners fill them; 3.17 fills the host and the Registrations tab, 4.10, 4.12 and 6.13 fill only their partials. Add two empty access-action slot partials, one in each Accounts list row and one in each Members list row, for 5.11 to fill; 2.13 leaves them in place. Add the shared event selector partial the scanner, ledger and report use. Prepare feature-scoped asset seams so later UI tickets do not all edit the shell or global JavaScript. Preserve Razor/Tailwind, the current shell and docs/DESIGN.md. The demo data seam is 1.14; this ticket adds no demo data.

## Requirements

Verify: Every new navigation link opens an authorized screen for the right role, not a dead link or an error page; another role is refused. The My Events tabs and the Accounts and Members rows render with their empty slots and no placeholder copy. The existing test suites still pass.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-platform-access/epic-platform-access.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only, real backend only; this ticket adds no demo data. 1.14 runs alongside it.
- Done check: dotnet build, npm run css:build when views or styles change, and one look at the changed screen with DemoData__Enabled=true. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
- Decision: 2026-11-06 — demo rescope: no longer waits on the old 1.12 walkthrough (now the 8.15 demo rehearsal) and absorbs its route inventory. After review the demo data seam moved to 1.14, so the shell and the seam can run in parallel and the cosmetic tickets need only this one. The title is kept to match issue #136.
- The final real-data integration gate is 8.11; demo-phase delivery never closes the existing functional integration tickets.
