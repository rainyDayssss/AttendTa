---
id: 12
type: story
title: "Frontend certificate ledger decision dialogs and document viewer"
parent: epic-certificates
covers: [R4, R5, R6, R7, R8]
after: ["1.13", "1.14", 2, 3]
hitl: false
risk: medium
tracker_id: "139"
remote: "https://github.com/rainyDayssss/AttendTa/issues/139"
tracker_status: backlog
---

# Frontend certificate ledger decision dialogs and document viewer

## Description

Build DESIGN screens 16–17 with the existing fixed template, renderer and configured signatory: completed-event selector, Eligibility Review and Issued Registry tabs, approve, decline and issue dialogs with reason and consequence, and a quiet PDF view and download surface. Until 6.4–6.8 exist, the reviews created when an event is completed, the decisions and the issuance run on demo data (M-10a, M-10b, M-11a, M-11b): the PDF is rendered in memory through the real snapshot render (never the watermarked sample render) with a certificate number from the demo store, and nothing is retained. No bulk issuance; success shows only after the command returns. Own certificate presentation and scoped assets, not the member-history partial. 6.13's member history reads its issued certificates from the same demo store; 6.4 and 6.6–6.8 later replace its review, decision and issuance sources. The completed-event selector is the shared event selector partial from 1.13.

## Requirements

Verify: An Officer selects the seeded completed event, sees a review for each member checked in, approves one, declines one with a required reason, issues the approved one, sees that deciding an already-decided review or issuing an unapproved one is refused, and opens its landscape A4 PDF with the configured signatory; the registry lists it; nothing on screen or in the PDF says it is demo data; tabs and dialogs work on desktop and phone width.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per `demo-mock-register.md` (rows M-10a, M-10b, M-11a, M-11b) where it does not; nothing on screen may show that data is demo data. Add or fill this ticket's register rows in the same pull request.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
- Real review/issue/download acceptance remains in #64–#68; the signatory configured by #63/PR102 is reused, not a new blocker.
