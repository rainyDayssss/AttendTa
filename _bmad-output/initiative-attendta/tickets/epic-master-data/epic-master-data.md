---
type: epic
title: "Management maintains members, event types and venues"
parent: initiative-attendta
covers: [R4, R5, R8, R6, R7, R33, R36]
sources: [_bmad-output/initiative-attendta/initiative-attendta.md]
companions: [_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md, docsss/architecture.md, docsss/design.md, docs/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/EXPERIENCE.md, _bmad-output/initiative-attendta/delivery-queue.md, _bmad-output/initiative-attendta/finale-test-cases.md, docsss/AttendTa-System-Specification.md]
after: []
refined: true
risk: medium
---

# Management maintains members, event types and venues

## Description

Brings the existing Member, Event Type and Venue code to the PRD rules on the real login: Management edits Member Profiles (all fields but membership status), maintains Event Types and Venues with photos, and a Member reads their own profile. It builds `IPrivateFileStore` (AD-12) that Event Covers, certificates and renderer temporary files reuse. The backend and the 2.9 UI sweep are on `dev`; this epic now also hosts the Master Data frontend acceptance slices 2.10–2.14, which consume the real endpoints and close the remaining visual and interaction gaps.

## Outcome

For Management, the reference data every Event depends on is maintained in one audited place, shown by an Officer editing a member, a type and a venue with photo on the dev build.

## Delivery (2026-11-06, demo rescope)

- **Demo-polish phase.** 2.12 (queue 140), 2.13 (queue 150), 2.14 (queue 160) and 2.10 (queue 170) deliver in the demo phase as demo-polish against the existing real backend; this epic adds no demo data and has no rows in `demo-mock-register.md`. 2.13 and 5.11 touch different partials (5.11 the 1.13 slot partial, 2.13 the Edit trigger and form); 2.13 leaves the access-action slot partial that 1.13 placed in each Members list row (5.11 fills it) untouched and the two have no ordering between them.
- **Finale acceptance.** 2.11 moves to the finale (queue 515, phase finale) as an acceptance check for GitHub #128; it verifies all three centered editors against `docs/DESIGN.md` and runs with the 8.12 whole-app Impeccable pass.
- **Replaced delivery rules.** This rescope replaces the per-ticket Impeccable critique and polish bullet below. Per-ticket checks in the demo phase are `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish, and no separate code review; critique and polish run once in 8.15 and in 8.12.

## Delivery (2026-11-06)

The bullets below stand where the demo rescope above does not replace them. It replaces the per-ticket Impeccable critique and polish bullet.

- The Master Data backend is on `dev` (2.1–2.9; 2.1 is still marked in progress in the tracker although its code is present); these slices reuse it and add no backend contract. 2.10 finishes only #127's residual submitting/loading feedback, error/retry and truthful empty/no-match states; status filters and result counts already shipped in #134/#132 are preserved, not rebuilt.
- 2.12 is the shared dialog plus Event Type tracer; 2.13 applies the same dialog pattern to Member; 2.14 applies it to Venue. Then 2.11 is the #128 acceptance umbrella that verifies all three rendered editors after 2.13 and 2.14; it is not a separate 2.15 entry. These are parent-owned entries recorded here as the frontend order, not new backend scope.
- (Replaced by demo rescope above) Per-ticket design critique and polish are replaced by the single passes in 8.15 and 8.12.
- Development checks are a clean build, a reproducible CSS build when views/styles change, and a manual smoke of the changed surface. Keep the focused venue-capacity guard for this live high-risk mutation. Finale 8.5 (#78) authors/maps important cases before 8.4 (#77) runs the combined suite; 8.6–8.8 record acceptance.
- 2.9 is the historical Master Data sweep and is not repeated. The final whole-app frontend walkthrough, refactor and polish is 8.12; backend hardening is 8.13.
- Dependency note: 2.10 waits on 2.9, 1.13, 2.12, 2.13 and 2.14; 2.12 on 2.9 and 1.13; 2.13 on 2.12; 2.14 on 2.12; 2.11 on 2.13 and 2.14. These are contract-level waits, not whole-backend epic gates.

## Requirements

- R1 (parent R4): A Member views their own account, academic and membership details; nothing is editable.
- R2 (parent R5): Management edits a Member's full name, course, year level, section, mobile and membership type; a student-number correction requires a reason, stays unique and logs old/new values; login email is read-only; edits never rewrite Registration snapshots or issued Certificates. The membership-status change belongs to epic-administration.
- R3 (parent R6): Management creates, edits, activates and deactivates Event Types with a unique, non-blank name and a non-blank description. Inactive types cannot be chosen for new or draft Events; existing references are kept.
- R4 (parent R7): Management maintains Venues: room name, positive capacity, location, availability notes, contact person, active state and a required photo. Capacity never drops below the participant limit of a linked draft or published Event. Inactive Venues cannot be chosen for new or draft Events.
- R5 (parent R8): The shared private image storage accepts JPEG/PNG verified by content, at most 2 MiB, under server-generated names in separate venue and event directories, keeping the previous image until the new path commits; Venue photos are uploaded, replaced and displayed through an authorized route. Event Cover upload belongs to epic-events-registration.
- R6 (parent R33): Profile and identity corrections, Event Type and Venue changes, and image changes each append an Activity Log entry.
- R7 (parent R36): The Members, Event Types and Venues lists are paged with the shared pager.

## Constraints

- Passwords only as salted Argon2id hashes (AD-15); signed expiring cookies; antiforgery on every mutation; ownership checks on personal resources (AD-8); every time, capacity and state guard on the server; secrets, hashes and QR tokens never logged; no login throttling (parent Constraints; prd NFR-1).
- Multi-record mutations run in one caller-owned SQL transaction with the audit write, serialized where they compete; uniqueness enforced in the database (parent Constraints; prd NFR-2; AD-3, AD-4).
- No hard deletes; Activity Log append-only (parent Constraints; prd NFR-3).
- Files on persistent server-local storage outside the web root, separate venue/event/certificate directories, relative paths only in SQL (parent Constraints; prd NFR-4; AD-12).
- UTC datetime2(3) storage, Asia/Manila display with a label, no scheduled jobs; every time guard evaluated per request (parent Constraints; prd NFR-5; AD-11).
- net10.0 MVC (AD-18), EF Core database-first, WebApp → Services → Data (AD-1), compiled Tailwind with no runtime CDN (AD-14) (parent Constraints).
- States are seeded lookup tables with FKs; event_types.description NOT NULL; certificates.issuer_name_snapshot (parent Constraints; AD-10).
- feature/* or fix/* branches, PRs to dev with green CI and a peer approval, Conventional Commits (parent Constraints; addendum D).
- SPEC v2.1 in docsss/ is authoritative for rule detail unless prd §9 overrides (parent Constraints).

## Done when

- On the dev build an Officer corrects a member's student number with a reason, creates, edits and toggles an Event Type and a Venue with a photo, and each change shows old/new values in activity_logs (image changes without paths).
- Images over 2 MiB or whose content is not JPEG/PNG are refused, a failed write keeps the old photo, and Venue photos render through an authorized route.
- A Member opens their own read-only profile; another Member's profile is not reachable.
- Venue capacity below a linked draft/published Event's limit is refused under test (the refusal of inactive types/venues for new drafts is verified in epic-events-registration entry 1; e2 verifies the active read queries).
- AGENTS.md and docs/database-setup.md state the uploads/venues/… path form and 2 MiB; merged to dev with integrated verification.

## Boundaries

- Belongs to epic-administration: the membership-status change, its preview and cascade.
- Belongs to epic-events-registration: Event Cover upload and Event forms.
- Must not change: no standalone member creation; members are created only by Account Request approval (AD-2).
- Must not change: no hard deletes of members, types or venues.
- Belongs to epic-events-registration: the Eligible Participant fact in ParticipationFacts.cs (AD-5).
- Must not change: Registration snapshots (course, year level, section) or issued Certificates when a Member profile is edited.

## References

- parent — _bmad-output/initiative-attendta/initiative-attendta.md, Requirements R4–R8, R33, R36
- architecture — docsss/architecture.md, §5 database, §8 images (superseded for storage, lookups and transactions by AD-3, AD-4, AD-10, AD-12)
- spine — _bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md, AD-1, AD-2, AD-3, AD-4, AD-7, AD-8, AD-9, AD-10, AD-11, AD-12, AD-13, AD-14, AD-17, AD-18; Capability map R4–R8
- spec — docsss/AttendTa-System-Specification.md, §6 master data and §14.5 storage
- design — _bmad-output/initiative-attendta/ux-attendta/DESIGN.md; docsss/design.md

## Notes

- Historical delivery attribution: Member 2 (addendum C). This is history only; future work is claimed per ticket from the shared queue and no fixed human owner is assigned (2026-11-06 decision).
- Decision: 2026-11-04 — refined from the initiative's lines alone.
- Decision: 2026-11-04 — images capped at 2 MiB per PRD FR-8 (AD-12); the existing 5 MiB limit in VenueImageStorage.cs, AGENTS.md and docs/database-setup.md is corrected here.
- Waits on epic-platform-access because: real login and cookie session (AD-8), ICurrentActor, IUnitOfWork and the audit writer (AD-3, AD-7), TimeProvider (1.4), DomainException (AD-9), the pager (AD-13), the Tailwind shell (1.9), the canonical schema with lookups (AD-10), and the transaction-wrapped master-data writes (1.11).
- Decision: 2026-11-04 — tracer bullet: entry 1, Event Type management with required description, audit logging, and paged Tailwind list; next: entry 2, Shared IPrivateFileStore with venue, event, certificate and temporary areas and authorized photo route (least certain).
- Decision: 2026-11-04 — no closing end-to-end suite in this epic; epic-release-acceptance runs SPEC §20 across the build.
- Decision: 2026-11-04 — sequencing: Entry 1 (tracer bullet) establishes Tailwind CRUD and audit patterns; Entry 2 (least certain) establishes shared storage and doc limits; Venues follow in Entries 3-4 (create/edit with capacity guard and paged list); Entry 5 exposes active reference query contracts only (eligibility is e3's in ParticipationFacts.cs per AD-5); Member profile management and paging run in Entries 6-7, with Member self profile in Entry 8; Entry 9 closes with refactor sweep.
- Decision: 2026-11-04 — boundary: membership status mutation is removed from Member edit and index in this epic, leaving status read-only until epic-administration wires the deactivation modal and registration cancellation cascade.
- Decision: 2026-11-04 — venue capacity guard against linked draft/published events is verified under test/seeding in this epic prior to epic-events-registration building the event creation UI.
- Decision: 2026-11-04 — re-sliced against the architecture spine (AD-1..AD-18): Entry 2 delivers the full IPrivateFileStore across areas with venue path migration; Entry 5 shrinks to active reference queries as ParticipationFacts moves to e3; lock methods added for event_types and venues; ICurrentActor, DomainException and TimeProvider adopted; membership status write removed from Member edit.
- Decision: 2026-11-06 — final frontend handoff: 2.10 closes #127's residual submitting/loading feedback, error/retry and truthful empty/no-match states only (shipped filters/counts remain); 2.12 is the shared dialog and Event Type tracer; 2.13 is Member; 2.14 is Venue; 2.11 is the #128 whole-editor acceptance umbrella after 2.13/2.14. There is no 2.15. These reuse the real 2.1–2.9 backend and add no backend contract. The parent owns the entries. Reason: only visual and interaction acceptance remains.
- Decision: 2026-11-06 — no fixed future human owner. Historical attribution above is preserved as history; claims from `delivery-queue.md` win over the addendum assignment.
- Decision: 2026-11-06 — every slice here is UI-facing and uses impeccable for implementation, rendered critique and polish. Development checks are minimal (build, reproducible CSS, manual smoke); the venue-capacity guard keeps a focused automated regression because it is a live high-risk mutation. Finale 8.5 (#78) authors/maps important cases before 8.4 (#77) runs the combined suite, followed by 8.6–8.8 acceptance.
- Decision: 2026-11-06 — 2.9 is not repeated. Final frontend walkthrough/refactor/polish is 8.12; backend hardening is 8.13. No container status change and no implementation starts from this planning edit.
- Decision: 2026-11-06 — demo rescope: 2.10, 2.12, 2.13 and 2.14 deliver in the demo phase as demo-polish against the real backend without demo data; 2.13 and 5.11 split the Members list view (5.11 owns the row access-action partial, 2.13 the Edit trigger and form) instead of an ordering, which would cross epic order; 2.11 moves to the finale as an acceptance check with 8.12.
- Decision: 2026-11-06 — review of the demo rescope: Clarify that 2.13 and 5.11 touch different partials in the Members list (5.11 the 1.13 slot partial, 2.13 the Edit trigger and form), and update Done check verify line requirements for demo-phase tickets.
