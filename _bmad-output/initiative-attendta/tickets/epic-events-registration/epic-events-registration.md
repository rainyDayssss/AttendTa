---
type: epic
title: "Officers publish events and fill seats fairly"
parent: initiative-attendta
covers: [R8, R9, R10, R11, R12, R13, R14, R15, R16, R17, R18, R19, R20, R33, R36]
sources: [_bmad-output/initiative-attendta/initiative-attendta.md]
companions: [_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md, docsss/architecture.md, docsss/design.md, docs/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/EXPERIENCE.md, _bmad-output/initiative-attendta/delivery-queue.md, _bmad-output/initiative-attendta/finale-test-cases.md, docsss/AttendTa-System-Specification.md]
after: []
refined: true
risk: high
---

# Officers publish events and fill seats fairly

## Description

The highest-risk lifecycle and concurrency work: Management runs an Event from draft to publish, edit, cancel and complete, and Eligible Participants register before the deadline. Reviews, Promotion and cancellations allocate Seats without overbooking and respect the start cutoff through one Registration-owned participation cancellation service, which epic-administration later calls. Members follow their Registrations in My Events. On 2026-11-06 the lifecycle and registration backend through the start cutoff is built on `dev` (3.1–3.11 and 3.15); completion, My Events and the backend sweep remain, and the epic now also hosts the frontend slices 3.16–3.18.

## Outcome

For Officers and Members, seats are allocated by explicit, fair review instead of manual headcounts, shown by UJ-2 including the concurrent last-seat case.

## Delivery (2026-11-06, demo rescope)

- 3.16 (queue 20) and 3.17 (queue 30) are demo-critical: 3.16 builds the officer dossier and controls (using demo commands M-04 and M-09 provided by 1.14); 3.17 builds the My Events host and extension slots with demo registrations (adds row M-02). 3.18 (queue 120) is demo-polish against the real backend with no demo data.
- 3.12 (queue 210) and 3.13 (queue 200) are integration tickets that replace demo data: 3.12 implements the real completion transaction and hook (removes M-09); 3.13 implements the real owner-scoped registrations query (removes M-02).
- This replaces the bullets below on Development-only, conspicuous mocks and per-ticket Impeccable critique and polish; critique and polish run in 8.15 and 8.12.

## Delivery (2026-11-06)

- 3.16 is the officer dossier/roster/lifecycle dialogs, 3.17 is the My Events history host and extension slots, and 3.18 is the member feed/detail visual alignment. These parent-owned entries define frontend scope; builders claim them from the prioritized shared queue rather than following fixed human or module lanes. Reason: the lifecycle and registration backend already exists, so the officer and member surfaces can be built and reviewed before completion and My Events land.
- Real event, registration, review, promotion and cancellation endpoints are reused. 3.16 and 3.17 wait only on 1.13; 3.18 waits on 1.13 and 3.10. 3.12 completion follows the 3.16 surface; 3.13 My Events replaces the 3.17 fixture registrations with the real owner-scoped query. The independent 3.11 cancellation backend keeps its declared backend dependencies and does not wait on a frontend ticket. Until the completion/My Events contracts exist, 3.16 and 3.17 use demo data per `demo-mock-register.md` with no visible sign in the UI and no real writes; they must not hide a broken real endpoint. 3.18 uses the real 3.5 browse/detail data.
- Critique and polish run in 8.15 and 8.12; per-ticket Impeccable critique and polish do not run on individual slices.
- Development checks are a clean build, a reproducible CSS build when views/styles change, and a manual smoke of the changed surface. Keep focused guard regressions for the live high-risk seat-allocation and cancellation mutations. Finale 8.5 (#78) authors/maps important cases, including SM-4, before 8.4 (#77) runs the combined suite; 8.6–8.8 record acceptance.
- 3.14 remains a focused backend cleanup at the finale; it is not a repeated whole-app or UI audit. Frontend review belongs to 8.12; backend hardening to 8.13.
- Dependency note: 3.16/3.17 wait on 1.13 and 3.18 on 1.13/3.10; these are contract-level waits, not whole-backend epic gates. The epic-level waits in Notes gate backend completion only.

## Requirements

- R1 (parent R9): Management creates and edits draft Events: title, description, active Event Type, active Venue, start, participant limit > 0 and <= Venue capacity, Signup Deadline <= start, optional Cover. Drafts are hidden from Members.
- R2 (parent R10): Management publishes a draft only with an active type and venue (revalidated), a Cover, a valid capacity and a future Signup Deadline. Publication is audited, shows the Event in the Member feed and opens signup at once.
- R3 (parent R11): On a Published Event only title, description and Cover can change, with old/new values logged. Completed and Cancelled Events are read-only.
- R4 (parent R12): Management cancels a Draft or Published Event that has no Valid Attendance and no issued Certificate, with a reason. Cancellation atomically cancels its pending, confirmed and waitlisted Registrations, invalidates their QR Passes, closes Check-in and is audited; no Promotion.
- R5 (parent R13): Management completes a Published Event at or after its start; completion closes Check-in; nothing completes an Event automatically and completion before start is refused. Creating Certificate Reviews in the completion transaction belongs to epic-certificates.
- R6 (parent R14): An Eligible Participant registers for a Published Event before its Signup Deadline, creating a pending Registration with a course/year/section snapshot. A second Registration for the same Member and Event is refused, even after cancellation. Full Events accept signup with a no-seat-guaranteed warning. Overlapping Events are allowed.
- R7 (parent R15): At or after the Signup Deadline the server refuses new signups, stale forms included; the Event stays Published and existing Registrations are unaffected.
- R8 (parent R16): Before the Event Start Cutoff Management approves or declines (reason required) Pending Registrations, and administratively cancels Confirmed or Waitlisted ones without Valid Attendance (reason required, QR invalidated, Seat released, no Promotion). Approval confirms only when a Seat is free and no earlier eligible Waitlisted Registration has priority; otherwise it waitlists. Concurrent last-seat approvals never overbook. Confirmation generates a unique, unpredictable QR token.
- R9 (parent R17): Before the Event Start Cutoff Management promotes the earliest Waitlisted Registration whose owner is still an Eligible Participant (by review time, then registration ID, across the whole queue) when a Seat is free; otherwise nothing changes. Promotion is serialized with approvals and never automatic.
- R10 (parent R18): After an explicit confirmation, a Member cancels their own pending, confirmed or waitlisted Registration on a Published Event before the Event Start Cutoff and before Valid Attendance. The QR Pass is invalidated, the Seat released, no Promotion follows, and re-registration is refused.
- R11 (parent R19): At or after the Event start the server refuses reviews, confirmations, Promotions and member cancellations. Pending and Waitlisted rows stay unchanged and show as Not confirmed before cutoff; they are not No-shows.
- R12 (parent R20): A Member sees all their Registrations with status in My Events, including Not confirmed before cutoff. QR Pass, attendance state and certificate links are added by epic-check-in and epic-certificates.
- R13 (parent R8): Management uploads, replaces and (while a draft) removes an Event Cover through the shared image storage; publication refuses a missing Cover.
- R14 (parent R33): Event publication, edits, cancellation and completion, Registration reviews, Promotions and cancellations each append an Activity Log entry.
- R15 (parent R36): The Member events feed, Management event list, registration review queue and personal registrations are paged.

## Constraints

- Passwords only as salted Argon2id hashes; signed expiring cookies; antiforgery on every mutation; ownership checks on personal resources; every time, capacity and state guard on the server; secrets, hashes and QR tokens never logged; no login throttling (parent Constraints; prd NFR-1; AD-8; AD-15 for the hasher).
- Multi-record mutations run in one caller-owned SQL transaction with the audit write, serialized where they compete; uniqueness enforced in the database (parent Constraints; prd NFR-2; AD-3, AD-4).
- No hard deletes; Activity Log append-only (parent Constraints; prd NFR-3).
- Files on persistent server-local storage outside the web root, separate venue/event/certificate directories, relative paths only in SQL (parent Constraints; prd NFR-4; AD-12).
- UTC datetime2(3) storage, Asia/Manila display with a label, no scheduled jobs; every time guard evaluated per request against the injected TimeProvider (parent Constraints; prd NFR-5; AD-11).
- One MVC app and one SQL Server database; Chrome target; mobile-responsive (parent Constraints; prd NFR-6).
- net10.0 MVC (AD-18), EF Core database-first, WebApp → Services → Data (AD-1), compiled Tailwind with no runtime CDN (AD-14) (parent Constraints).
- States are seeded lookup tables with FKs; event_types.description NOT NULL; certificates.issuer_name_snapshot (parent Constraints; AD-10).
- feature/* or fix/* branches, PRs to dev with green CI and a peer approval, Conventional Commits (parent Constraints; addendum D).
- SPEC v2.1 in docsss/ is authoritative for rule detail unless prd §9 overrides (parent Constraints).

## Done when

- UJ-2 runs on the dev build: draft with cover, publish, signups, approvals confirm up to the limit and then waitlist, a confirmed Member cancels, and Promote confirms the earliest eligible waitlisted Registration.
- SQL Server tests show concurrent last-seat approvals and Promotion never overbook (SM-4 seats).
- Signup Deadline and Event Start Cutoff refuse stale forms at the exact boundary, stored in UTC and shown in Asia/Manila, using the injected TimeProvider (AD-11).
- Event cancellation cancels its open Registrations atomically through IRegistrationCancellationService, implemented with the four AD-6 methods that epic-administration calls; completion before start is refused.
- Merged to dev with integrated verification.

## Boundaries

- Belongs to epic-check-in: QR PNG rendering, the Check-in toggle (which adds SetCheckInWindowAsync to IEventLifecycleService with this epic's review), scans and attendance.
- Belongs to epic-certificates: Certificate Reviews created at completion through IEventCompletionHandler (AD-2), and certificate links.
- Belongs to epic-administration: deactivation-triggered cancellation, which calls CancelUpcomingForUserAsync and PreviewUpcomingForUserAsync (AD-6); the screens and reason entry are epic-administration's.
- Must not change: nothing promotes automatically; no reapplication after cancellation.

## References

- parent — _bmad-output/initiative-attendta/initiative-attendta.md, Requirements R8–R20, R33, R36
- spine — _bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md: AD-2 (writers, hooks), AD-3 (unit of work), AD-4 (lock order), AD-5 (ParticipationFacts), AD-6 (cancellation service), AD-7 (audit), AD-8 (actor, policies), AD-9 (outcomes), AD-10 (lookups, unique keys), AD-11 (TimeProvider), AD-12 (file store), AD-13 (paging), AD-14 (My Events ownership), AD-17 (test projects); Consistency Conventions (QR token format)
- architecture — docsss/architecture.md
- spec — docsss/AttendTa-System-Specification.md, §7–§10 events, registration, cutoffs
- design — _bmad-output/initiative-attendta/ux-attendta/DESIGN.md; docsss/design.md
- ux — _bmad-output/initiative-attendta/ux-attendta/EXPERIENCE.md, UJ-2

## Notes

- Historical delivery attribution: Member 3 (addendum C). This is history only; future work is claimed per ticket from the shared queue and no fixed human owner is assigned (2026-11-06 decision).
- Decision: 2026-11-04 — refined from the initiative's lines alone; kept as one epic (split declined, parent Notes).
- Waits on epic-master-data because: active Event Types and Venues and the shared image storage (IPrivateFileStore, AD-12). Member eligibility is the ParticipationFacts fact this epic creates (AD-5).
- Waits on epic-platform-access because: login, ICurrentActor and policies (1.1), canonical EF mappings (1.2), audit writer (1.3), local-time display helper (1.4), pager (1.6), approval-created members (1.7), role shell (1.9), and test projects (1.10).
- Decision: 2026-11-04 — tracer bullet: entry 1, Draft event creation, edit, and management event list (architectural slice through EF mapping, service validation, controller, Tailwind views, and pager); least-certain story entry 7 (seat allocation and SM-4 concurrency) cannot follow entry 1 directly because reviewing registrations functionally requires published events with covers (entries 2, 3) and member registration submission (entry 5).
- Decision: 2026-11-04 — no closing end-to-end suite in this epic; epic-release-acceptance runs SPEC §20 across the build.
- Decision: 2026-11-04 — re-sliced against the architecture spine (AD-2, AD-4, AD-5, AD-6, AD-11, AD-14, AD-18): added entry 15 for IRegistrationCancellationService (AD-6), created all AD-5 facts in ParticipationFacts in entry 3, named IEventLifecycleService in entry 1, declared IEventCompletionHandler in entry 12, moved cutoff guards into the actions they guard, established My Events ownership in entry 13, specified 64-hex QR tokens, and retargeted net10.0.
- Historical coordination (resolved 2026-11-06): PR #122 implemented entry 7 (registration review and transactional seat allocation) and has since merged. Local merge-tree checks showed no textual conflict with `dev` or the platform-access branch.
- Decision: 2026-11-06 — final frontend handoff: 3.16 is the officer dossier/roster/lifecycle-dialog surface, 3.17 the My Events host/extension slots, and 3.18 member feed/detail visual alignment. They wait only on 1.13 (3.18 also on 3.10). Only missing completion/My Events contracts may use tracked, conspicuous Development-only mocks. Backend 3.12 follows the 3.16 surface and 3.13 replaces 3.17's fixture registrations with the real query; 3.11 retains its own declared backend dependencies and does not wait on 3.16. The parent owns these frontend entries.
- Decision: 2026-11-06 — no fixed future human owner. Historical attribution above is preserved as history; claims from `delivery-queue.md` win over the addendum assignment.
- Decision: 2026-11-06 — development checks are minimal (build, reproducible CSS, manual smoke); seat allocation and cancellation keep focused guards because they are live high-risk mutations. Finale 8.5 (#78) authors/maps important cases including SM-4 before 8.4 (#77) runs the combined suite, then 8.6–8.8 acceptance.
- Decision: 2026-11-06 — 3.14 stays a focused backend cleanup at the finale, not a repeated whole-app/UI audit. Frontend review is 8.12; backend hardening is 8.13.
- Historical coordination at container read (2026-11-06): PR #133 (event start cutoff, 3.10) was merged at `997f290`; issue #39 was then pending closure. The parent has since closed #39 with the merge evidence. No container status change and no implementation starts from this planning edit.
- Decision: 2026-11-06 — demo rescope: the client demo is close, so frontend-only demo work comes first against the real backend or demo data recorded in `demo-mock-register.md` with no visible sign in the UI. 3.16 (queue 20) and 3.17 (queue 30) are demo-critical (using M-04/M-09 and adding M-02), 3.18 (queue 120) is demo-polish with no demo data, and 3.12/3.13 are integration tickets removing M-09 and M-02. Per-ticket Impeccable critique and polish are replaced by 8.15 demo rehearsal and 8.12.
- Decision: 2026-11-06 — review of the demo rescope: 3.16 and 3.17 wait on 1.13 and 1.14, 3.16 uses M-04/M-09 from 1.14 and checks completion refusal rules, 3.17 fills the 1.13 host while keeping extension slots, and 3.12 notes the Completed status overlay.
