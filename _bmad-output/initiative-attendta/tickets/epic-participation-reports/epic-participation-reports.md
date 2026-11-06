---
type: epic
title: "Officers read per-event participation reports"
parent: initiative-attendta
covers: [R30, R36]
sources: [_bmad-output/initiative-attendta/initiative-attendta.md]
companions: [_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md, docsss/architecture.md, docsss/design.md, docs/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/EXPERIENCE.md, _bmad-output/initiative-attendta/delivery-queue.md, _bmad-output/initiative-attendta/finale-test-cases.md, docsss/AttendTa-System-Specification.md]
after: []
refined: true
risk: medium
---

# Officers read per-event participation reports

## Description

Gives Management a per-Event participation report computed from canonical Registration, snapshot and attendance facts: status counts, attendees, No-shows, attendance rate, attendee distribution and paged detail rows. The report backend is not built yet; the epic now opens with the frontend slice 7.4 against demo data per demo-mock-register.md, and the integration phase builds 7.1–7.3 and replaces the demo data.

## Outcome

For the organization, participation evidence for advisers comes straight from recorded attendance, shown by the UJ-4 report figures.

## Delivery (2026-11-06, demo rescope)

- 7.4 (queue 60) is demo-critical: frontend per-event report with summary counts, rate, completed-only no-shows, breakdowns and paged detail table; reads demo data M-13a, M-13b, M-13c computed from real registrations and demo-store attendance/completion, with no visible sign in the UI.
- 7.1–7.3 are integration tickets that replace M-13a, M-13b, M-13c: 7.1 replaces the summary KPIs (removes M-13a), 7.2 replaces the completed-only no-shows and breakdowns (removes M-13b), and 7.3 replaces the paged detail rows (removes M-13c).
- This replaces the bullets below on Development-only, conspicuous mocks and per-ticket Impeccable critique and polish; critique and polish run in 8.15 and 8.12.

## Delivery (2026-11-06)

- 7.4 is the frontend per-event report with summary breakdowns and participant table, built to `docs/DESIGN.md` after 1.13 and 1.14. It is a parent-owned frontend slice recorded here as the epic's UI order. Reason: the report is read-only and its layout can be agreed against demo figures while the aggregation backend is still missing.
- The report backend (7.1–7.3) is missing, so 7.4 uses demo data per `demo-mock-register.md` (rows M-13a, M-13b, M-13c): no visible sign in the UI, no real writes, no silent fallback, tracked removal. 7.1, 7.2 and 7.3 each remove one of those rows; 8.13 removes the demo data seam and 8.11 verifies none remains.
- Critique and polish run in 8.15 and 8.12; per-ticket Impeccable critique and polish do not run on 7.4.
- Development checks are a clean build, a reproducible CSS build when views/styles change, and a manual smoke of the changed surface. Reports are read-only and have no high-risk mutation, so no focused guard regression is required at development time. Finale 8.5 (#78) authors/maps important report cases before 8.4 (#77) runs the combined suite; 8.6–8.8 record acceptance.
- This epic has no refactor sweep; the final frontend review is 8.12 and backend hardening is 8.13.
- Dependency note: 7.4 waits on 1.13 only; initial real report reads 7.1/7.2 are not gated on the whole check-in epic. Only 7.3 waits on 4.6 to pin final detail-row proof against real invalidated attendance. These are story-level dependencies; epic-level waits in Notes gate backend completion only.

## Requirements

- R1 (parent R30): Management views per-Event participation: Registrations by status, Confirmed and Valid attendees, No-shows (Completed Events only), attendance rate (N/A with no Confirmed), attendee course/year distribution from signup snapshots, and detail rows (name, student number, course, registration status, attendance result, check-in time). Totals cover the full dataset; inactivity never hides history.
- R2 (parent R36): Report detail rows are paged while totals cover the full dataset.

## Constraints

- Passwords only as salted Argon2id hashes; signed expiring cookies; antiforgery on every mutation; ownership checks on personal resources; every time, capacity and state guard on the server; secrets, hashes and QR tokens never logged; no login throttling (parent Constraints; prd NFR-1).
- UTC datetime2(3) storage, Asia/Manila display with a label, no scheduled jobs; every time guard evaluated per request (parent Constraints; prd NFR-5; AD-11, AD-16).
- net10.0 MVC (AD-18), EF Core database-first, WebApp → Services → Data (AD-1), compiled Tailwind with no runtime CDN (AD-14).
- feature/* or fix/* branches, PRs to dev with green CI and a peer approval, Conventional Commits (parent Constraints; addendum D).
- SPEC v2.1 in docsss/ is authoritative for rule detail unless prd §9 overrides (parent Constraints).

## Done when

- On the dev build the report for a completed Event shows status counts, Confirmed and Valid attendees, No-shows, rate and distribution matching hand-computed figures.
- Zero Confirmed shows N/A; invalidated attendance and pending/waitlisted/cancelled rows are excluded as FR-30 states, under test.
- Detail rows show the FR-30 fields with 25-row paging and full-dataset totals.
- Merged to dev with integrated verification.

## Boundaries

- Out: report export (parent Boundaries).
- Must not change: reports never mutate participation records; reads only (AD-2).

## References

- parent — _bmad-output/initiative-attendta/initiative-attendta.md, Requirements R30, R36
- spine — _bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md: AD-5 (ParticipationFacts, No-show, Attendance rate), AD-2 (reads only), AD-13 (paging), AD-11 (time), AD-14 (shell), AD-8 (Management policy), AD-18 (net10.0); Capability map row R30. docsss/architecture.md §9 is companion context only.
- spec — docsss/AttendTa-System-Specification.md, §15 reports
- design — _bmad-output/initiative-attendta/ux-attendta/DESIGN.md; docsss/design.md

## Notes

- Historical delivery attribution: Pinca (addendum C). This is history only; future work is claimed per ticket from the shared queue and no fixed human owner is assigned (2026-11-06 decision).
- Decision: 2026-11-04 — refined from the initiative's lines alone.
- Historical source-precedence question (resolved for implementation 2026-11-06): R30 and SPEC §15 differ on when the rate is final. Implementation follows the existing authority, SPEC v2.1 and `docs/DESIGN.md`: final rate only for completed, non-cancelled Events, N/A with zero Confirmed, and the unchanged R30 Valid-attendees/Confirmed formula. A PM clarification may still be recorded, but this is not a 7.1 build blocker; 7.4's mock does not decide the rule.
- Decision: 2026-11-04 — re-sliced against the architecture spine (AD-2, AD-5, AD-8, AD-11, AD-13, AD-14, AD-18): report consumes canonical ParticipationFacts from e3 without defining facts or mutating data, cuts unneeded status filtering, uses unique-id tiebreak for paging, and gates on Management policy.
- Waits on epic-platform-access because: role claims, Tailwind shell, and shared 25-row pager helper.
- Waits on epic-events-registration because: Registration states, snapshots and Completed Events.
- Waits on epic-check-in because: Attendance Records with invalidation.
- Decision: 2026-11-04 — tracer bullet: entry 1, Event selector and participation summary KPIs; next: entry 2, Completed-event no-shows and signup snapshot attendee distributions (least certain); then: entry 3, Paged participant detail rows with full-dataset totals completing the report view.
- Decision: 2026-11-04 — entry 3 waits on entry 2 because participant detail paging verifies stability of full-dataset totals and attendee distributions, and both extend the report view (single ordered lane).
- Decision: 2026-11-04 — three entries total (≤ 3); no refactor sweep required per slice rules.
- Decision: 2026-11-04 — no closing end-to-end suite in this epic; epic-release-acceptance runs SPEC §20 across the build.
- Decision: 2026-11-06 — frontend-first replan: 7.4 is the frontend per-event report with summary breakdowns and participant table, after 1.13, against Development-only mock data with the same view-model/route contract; the finale builds 7.1–7.3 and removes the mock. The parent owns the entry.
- Decision: 2026-11-06 — report dependency clarification: initial report reads/UI proceed without waiting for the whole check-in epic; only 7.3 waits on 4.6 for actual invalidated-attendance detail proof.
- Decision: 2026-11-06 — no fixed future human owner. Historical attribution above is preserved as history; claims from `delivery-queue.md` win over the addendum assignment.
- Decision: 2026-11-06 — 7.4 is UI-facing and uses impeccable for implementation, rendered critique and polish. Development checks are minimal (build, reproducible CSS, manual smoke); reports are read-only, so no focused guard regression is required. Finale 8.5 (#78) authors/maps important cases before 8.4 (#77) runs the combined suite, then 8.6–8.8 acceptance.
- Note: 2026-11-06 — no container status change and no implementation starts from this planning edit.
- Decision: 2026-11-06 — demo rescope: the client demo is close, so frontend-only demo work comes first against demo data recorded in `demo-mock-register.md` with no visible sign in the UI. 7.4 is demo-critical (queue 60) and reads demo data M-13; 7.1–7.3 are integration tickets replacing M-13 in parts until 7.3 sets M-13 removed. Per-ticket Impeccable critique and polish are replaced by 8.15 demo rehearsal and 8.12.
- Decision: 2026-11-06 — review of the demo rescope: 7.4 waits on 1.13 and 1.14 and verifies against seeded data only; M-13 is split into M-13a (7.1), M-13b (7.2) and M-13c (7.3) with each ticket the sole remover.
