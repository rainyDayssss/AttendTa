---
type: epic
title: "Administrators control roles, access and the audit trail"
parent: initiative-attendta
covers: [R5, R31, R32, R33, R34, R36]
sources: [_bmad-output/initiative-attendta/initiative-attendta.md]
companions: [_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md, docsss/architecture.md, docsss/design.md, docs/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/EXPERIENCE.md, _bmad-output/initiative-attendta/delivery-queue.md, _bmad-output/initiative-attendta/finale-test-cases.md, docsss/AttendTa-System-Specification.md]
after: []
refined: true
risk: high
---

# Administrators control roles, access and the audit trail

## Description

Gives Administrators control of Roles and Account access and gives Management the membership-status change, both with a preview of affected upcoming Registrations, a reason, and cancellation through the Registration-owned service. It protects the last active Administrator and adds the read-only Activity Log viewer. The accounts list (5.1) and the deactivation preview (5.3) are built on `dev`; the role, deactivation and log backend remain, and the epic now also hosts the access/status dialog frontend slice 5.11.

## Outcome

For Administrators, cutting off a departed officer is one attributable action, shown by UJ-5 on the dev build.

## Delivery (2026-11-06, demo rescope)

- **Demo-critical surface.** 5.11 (queue 100) is demo-critical: account and membership access dialogs on the real 5.3 preview and accounts list. It waits on 5.3, 1.13 and 1.14; 2.13 and 5.11 touch different partials (5.11 the 1.13 slot partial, 2.13 the Edit trigger and form). Missing backend commands are served by demo data per `demo-mock-register.md` (rows M-14a, M-14b, M-15a, M-15b) with no visible sign in the UI.
- **Integration tickets.** 5.4 and 5.5 remove rows M-14a and M-14b with the real account deactivation/reactivation commands; 5.6 and 5.7 remove rows M-15a and M-15b with the real membership status commands and cancellation cascade.
- **Replaced delivery rules.** This rescope replaces the Development-only/conspicuous mock bullet and the per-ticket Impeccable critique and polish bullet below. Per-ticket checks in the demo phase are `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish, and no separate code review; critique and polish run once in 8.15 and in 8.12.

## Delivery (2026-11-06)

The bullets below stand where the demo rescope above does not replace them. It replaces the Development-only/conspicuous mock bullet and the per-ticket Impeccable critique and polish bullet.

- 5.11 is the frontend account and membership access dialogs: the role, account deactivation/reactivation and membership status dialogs built on the real 5.1 accounts list and the real 5.3 preview. It waits on 5.3, 1.13 and 1.14, not on 2.13 or the deactivation commands: it fills the access-action slot partials 1.13 placed in the Accounts and Members list rows, separate from the 2.13 edit dialog. It is a parent-owned frontend slice recorded here as the epic's UI order. Reason: the accounts list and preview already exist, so the decision dialogs can be built and reviewed before the deactivation commands land.
- (Replaced by demo rescope above) Demo data per demo-mock-register.md (rows M-14, M-15) serves 5.11 without visible sign on screen until 5.4–5.7 remove it.
- (Replaced by demo rescope above) Per-ticket design critique and polish are replaced by 8.15 and 8.12.
- Development checks are a clean build, a reproducible CSS build when views/styles change, and a manual smoke of the changed surface. Keep focused guards for the live last-administrator and deactivation-cascade mutations. Finale 8.5 (#78) authors/maps important cases before 8.4 (#77) runs the combined suite, followed by 8.6–8.8 acceptance.
- 5.10 remains a focused backend cleanup at the finale; it is not a repeated whole-app or UI audit. Frontend review belongs to 8.12; backend hardening to 8.13.
- Dependency note: 5.11 waits on 5.3 and 1.13; these are contract-level waits, not whole-backend epic gates. The epic-level waits in Notes gate backend completion only.

## Requirements

- R1 (parent R5): Setting membership to inactive makes the Member no longer an Eligible Participant and applies the FR-32 preview, reason and upcoming-Registration cancellation without changing Account access.
- R2 (parent R31): An Administrator assigns the Member, Officer or Administrator Role to an Account. The last active Administrator cannot be demoted. Roles are fixed.
- R3 (parent R32): Before a membership-status or Account-access deactivation the screen previews affected upcoming Registrations and requires a reason. Deactivation cancels the person's Registrations on not-yet-started Events that lack Valid Attendance, invalidates their QR Passes and releases Seats without Promotion; Account deactivation ends existing sessions; the last active Administrator cannot be deactivated. Reactivation restores access only; issued Certificates are kept.
- R4 (parent R33): Role, access and membership changes each append an Activity Log entry.
- R5 (parent R34): An Administrator views a read-only, newest-first Activity Log showing actor, action, affected record, time and reason.
- R6 (parent R36): The accounts list and the Activity Log list are paged.

## Constraints

- Passwords only as salted Argon2id hashes; non-persistent 8-hour cookies with no sliding and no login throttling (AD-8); antiforgery on every mutation; ownership checks on personal resources; every time, capacity and state guard on the server; secrets, hashes and QR tokens never logged (parent Constraints; prd NFR-1).
- Multi-record mutations run in one caller-owned SQL transaction with the audit write, serialized where they compete; uniqueness enforced in the database (parent Constraints; prd NFR-2; AD-3, AD-4, AD-10).
- No hard deletes; Activity Log append-only (parent Constraints; prd NFR-3; AD-7: INSTEAD OF trigger; runtime SELECT/INSERT only).
- UTC datetime2(3) storage, Asia/Manila display with a label, no scheduled jobs; every time guard evaluated per request (parent Constraints; prd NFR-5; AD-11).
- net10.0 MVC (AD-18); layers WebApp → Services → Data (AD-1); canonical schema script and fluent mapping, no EF migrations (AD-10); compiled Tailwind, no runtime CDN (AD-14).
- feature/* or fix/* branches, PRs to dev with green CI and a peer approval, Conventional Commits (parent Constraints; addendum D).
- SPEC v2.1 in docsss/ is authoritative for rule detail unless prd §9 overrides (parent Constraints).

## Done when

- UJ-5 runs on the dev build: an Administrator previews and deactivates an officer with a reason, the officer's session is refused on its next request (AD-8, epic-platform-access), their upcoming Registrations are cancelled, and the Activity Log shows the change.
- An integration test (AD-17) with parallel connections shows that concurrent demotions or deactivations cannot remove the last active Administrator (AD-4 step 1, attendta:last-admin).
- Account reactivation restores login access only; membership reactivation restores Eligible Participant status only.
- The Activity Log viewer lists entries newest-first, read-only.
- Merged to dev with integrated verification.

## Boundaries

- Belongs to epic-events-registration: the participation cancellation logic (CancelUpcomingForUserAsync and PreviewUpcomingForUserAsync, AD-6), which this epic calls and never duplicates.
- Belongs to epic-platform-access: per-request active/role re-checks via OnValidatePrincipal (AD-8) and IActivityLogWriter (AD-7).
- Must not change: Master Data's other Member fields and their edit screen behavior.

## References

- parent — _bmad-output/initiative-attendta/initiative-attendta.md, Requirements R5, R31–R34, R36
- architecture — _bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md: AD-2 writers, AD-3 unit of work, AD-4 last-administrator app lock and lock order, AD-6 cancellation service, AD-7 audit writer and viewer, AD-8 session and policies, AD-10 role_id lookup, AD-13 paging; docsss/architecture.md
- spec — docsss/AttendTa-System-Specification.md, §5 deactivation, §17 audit
- design — _bmad-output/initiative-attendta/ux-attendta/DESIGN.md; docsss/design.md
- ux — _bmad-output/initiative-attendta/ux-attendta/EXPERIENCE.md, UJ-5

## Notes

- Historical delivery attribution: Pinca (addendum C). Admin-role pure rules exist on branch 7477e95 (architecture §1) and may be reused after validation into ASI.Basecode.Services with AD-17 tests; AD-1 forbids Data-layer rules. The attribution is history only; future work is claimed per ticket from the shared queue and no fixed human owner is assigned (2026-11-06 decision).
- Decision: 2026-11-04 — refined from the initiative's lines alone.
- Waits on epic-master-data because: the Member edit screen whose status control this epic replaces (entry 2.6).
- Waits on epic-events-registration because: the participation cancellation service (entry 3.15, AD-6).
- Does not wait on epic-check-in: the Valid Attendance exemption comes through epic-events-registration's cancellation service (3.15) and ParticipationFacts (3.3).
- Waits on epic-platform-access because: the authenticated navigation shell, shared 25-row pager and Activity Log writer.
- Decision: 2026-11-04 — tracer bullet: entry 1, Console accounts list with role and status badges; next: entry 2, Role assignment with last-active-Administrator app lock (AD-4) (least certain).
- Decision: 2026-11-04 — no closing end-to-end suite in this epic; epic-release-acceptance runs SPEC §20 across the build.
- Decision: 2026-11-04 — test-only entry 9 deleted and its UJ-5 verification folded into entry 4 in accordance with no closing end-to-end suite.
- Decision: 2026-11-04 — entry 1 hitl set to false as it requires no manual starter or user setup.
- Decision: 2026-11-04 — re-sliced against the architecture spine (AD-2, AD-3, AD-4, AD-6, AD-7, AD-8, AD-10, AD-11, AD-13, AD-14, AD-17, AD-18): adopted sp_getapplock attendta:last-admin, consumed IRegistrationCancellationService from 3.15, moved membership deactivation command and cascade to entry 6 with entry 7 reactivation-only, scoped activity log viewer to Administrator policy with AD-7 ordering and AD-13 pager without search, and aligned audit writer and session handling to platform access.
- Decision: 2026-11-06 — frontend-first replan: 5.11 is the frontend account and membership access dialogs, built on the real 5.1 accounts list and 5.3 preview, with a Development-only mock of the missing 5.4–5.7 commands. It waits on 5.3, 1.13 and 2.13. The parent owns the entry.
- Decision: 2026-11-06 — no fixed future human owner. Historical attribution above is preserved as history; claims from `delivery-queue.md` win over the addendum assignment.
- Decision: 2026-11-06 — development checks are minimal (build, reproducible CSS, manual smoke); the last-administrator guard and deactivation cascade keep focused regressions. Finale 8.5 (#78) authors/maps important cases before 8.4 (#77) executes the combined suite, then 8.6–8.8 acceptance.
- Decision: 2026-11-06 — 5.10 stays a focused backend cleanup at the finale, not a repeated whole-app/UI audit. Frontend review is 8.12; backend hardening is 8.13.
- Note: 2026-11-06 — no container status change and no implementation starts from this planning edit.
- Decision: 2026-11-06 — demo rescope: 5.11 is demo-critical (queue 100) on demo data M-14 and M-15 and no longer waits on 2.13 (its membership actions live in the Members list rows, separate from the 2.13 edit dialog); 5.4–5.7 integrate the real commands and remove M-14 and M-15.
- Decision: 2026-11-06 — review of the demo rescope: Split M-14 into M-14a/b and M-15 into M-15a/b with sole removers, add 1.14 dependency to 5.11, and clarify that 2.13 and 5.11 touch different partials in the Members list.
