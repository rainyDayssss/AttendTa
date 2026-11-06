---
id: 15
type: story
title: "Demo rehearsal with Impeccable critique and polish"
parent: epic-release-acceptance
after: ["1.13", "1.14", "3.16", "3.17", "4.10", "4.11", "4.12", "5.11", "6.12", "6.13", "7.4"]
hitl: true
risk: medium
tracker_id: "135"
remote: "https://github.com/rainyDayssss/AttendTa/issues/135"
tracker_status: backlog
---

# Demo rehearsal with Impeccable critique and polish

## Description

A person runs the demo script end to end on the demo build with DemoData__Enabled=true, in the Administrator, Officer and Member roles at desktop and 390px width: account request and approval, master data, event create and publish, signup and review, My Events and the pass, the door scan, completion, certificate review, issue and download, the report, access changes and the Activity Log. Run Impeccable critique on the demo-critical screens, then fix dead ends, broken links, misleading copy, any visible sign of demo data and the highest-value findings with Impeccable polish. Findings not fixed go to 8.12. This is the one critique-and-polish pass of the demo phase; it adds no new screens or backend. The demo script and its starting state are written to _bmad-output/initiative-attendta/demo-script.md for the presenter. Confirm that unlinked development-only pages, such as the certificate tracer and its watermarked sample, are not reachable on the demo machine. It also checks each demo-phase ticket's full Verify line once; a failing line goes back to its ticket before the demo. The script leaves out signing in as a deactivated account, because the demo does not block that login until 5.4 (see the register's Known limits).

## Requirements

Verify: The demo script runs start to finish in each role without a dead end, an error page or a visible sign of demo data, on desktop and at 390px width; each critique finding is fixed or listed for 8.12; demo-script.md holds the steps and the starting state. Every demo-phase ticket's Verify line passes or is recorded against its ticket.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Decision: 2026-11-06 — demo rescope: the old 1.12 opening walkthrough spike became this closing demo rehearsal and moved here from epic-platform-access because it waits on demo screens in later epics; 1.13 no longer waits on it. Existing issue #135 is reused.
- A person walks the script and judges the result with the project owner; the builder fixes what the walkthrough finds.
- Done check: dotnet build, npm run css:build when views or styles change, and the walkthrough itself. No new automated tests.
