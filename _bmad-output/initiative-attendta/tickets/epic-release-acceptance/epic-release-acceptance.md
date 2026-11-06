---
type: epic
title: "The integrated build passes SPEC §20 and is demo-ready"
parent: initiative-attendta
covers: []
sources: [_bmad-output/initiative-attendta/initiative-attendta.md, _bmad-output/initiative-attendta/prd-attendta/prd-attendta.md, docsss/AttendTa-System-Specification.md]
companions: [_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md, docsss/architecture.md, docsss/design.md, docs/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/DESIGN.md, _bmad-output/initiative-attendta/ux-attendta/EXPERIENCE.md, _bmad-output/initiative-attendta/delivery-queue.md, _bmad-output/initiative-attendta/finale-test-cases.md]
after: []
refined: true
risk: medium
---

# The integrated build passes SPEC §20 and is demo-ready

## Description

Delivers the initiative's Done when on the integrated build: the full SPEC §20 acceptance run, the owners' concurrency tests together, the demo host with trusted HTTPS for phone scanning over the hotspot, persistent storage with backup and restore, and the Tier 3 release from dev to main. The finale phase now also removes all demo data and mocks, runs the final frontend walkthrough/refactor/polish and backend hardening, and records real demo-host evidence. #74 delivered host tooling only; 8.14 is the follow-up that records actual host acceptance.

## Outcome

For the client and approver, the MVP is shown to meet every acceptance item, signalled by SM-1 and SM-2 at 100%.

## Delivery (2026-11-06, demo rescope)

- 8.15 (was 1.12, issue #135) is the demo rehearsal that closes the demo phase: a person walks the demo script on demo data in every role, runs the one Impeccable critique and polish pass of the demo phase, checks each demo-phase ticket's full Verify line once, and writes `demo-script.md`. It sits in this epic because it waits on 1.13, 1.14 and demo screens from epics 3, 4, 5, 6 and 7.
- The finale phase removes every piece of demo data added during the demo phase, proves complete real integrations against one coherent database, polishes the integrated application, and executes acceptance.
- 8.13 deletes the demo seam (register row M-01) once every other register row is removed; 8.11 is the verify-only gate that confirms every row in `demo-mock-register.md` (M-01 to M-15b) is `removed` with its commit, proves the demo seam is gone with checks that fail if any part remains, and proves all real integrations end to end. 2.11 joins the finale before 8.12 to accept the three centered record editors against `docs/DESIGN.md`.
- 8.12 runs the single whole-app frontend walkthrough, refactor and Impeccable polish pass against `docs/DESIGN.md` and the 8.15 demo rehearsal findings; individual demo-phase and integration tickets do not run per-ticket critique or polish.
- 8.10 pre-acceptance cleanup removes enabled throwaway harnesses and confirms no demo data configuration remains in deployment settings before the 8.5/8.4 test execution and 8.6–8.8 acceptance run. This replaces the earlier mock and demo data removal bullets below.

## Delivery (2026-11-06)

The bullets below stand where the demo rescope above does not replace them.

- 8.14 is the early real-host evidence follow-up after 8.1 for closed #74; it is not a substitute for actual hotspot/HTTPS acceptance. The finale implementation sequence is 8.13 backend hardening (no Impeccable) → 8.11 remove demo data/prove real integrations → 8.12 final frontend walkthrough, targeted refactor and Impeccable polish → 8.10 cleanup → 8.5 important-case authoring/mapping (#78) → 8.4 combined suite execution (#77) → 8.6 manual acceptance → 8.7 demo → 8.8 signoff → 8.9 release last. These are parent-owned entries. Reason: frontend-first slices leave demo data and deferred backend work that must be closed, reviewed and proven before acceptance and release.
- [Replaced by demo rescope] 8.11 is a hard gate: no demo data, mock, fixture fallback or mock route may remain reachable before the acceptance run. Every mocked contract is served by its real backend. It follows 8.13, 2.10 and 2.11; 8.12 follows 8.11.
- 8.14 follows 8.1 as an early, separate real-host evidence follow-up. 8.2 and 8.3 retain their declared host-operation dependencies. 8.10 cleanup follows 8.11/8.12/8.13 and runs before 8.5 case authoring, 8.4 suite execution and 8.6–8.8 acceptance; 8.9 release is last. This overrides the earlier 8.10-after-8.9 ordering.
- 8.5 (#78, “Important automated acceptance cases and SPEC coverage mapping”) authors/maps the important automated cases first and maps the remainder to concrete manual verification; it does not automate every minor screen. 8.4 (#77) then runs the combined suite on SQL Server. Full SPEC §20 scenario acceptance remains required in 8.6–8.8; the existing CI workflow is unchanged.
- 8.12 is the only whole-app frontend walkthrough; it uses Impeccable for implementation of any UI fix, rendered critique and polish. 8.13 is backend hardening and does not use Impeccable. The feature-epic sweeps 3.14, 4.9, 5.10 and 6.11 remain focused backend cleanup, not repeated UI audits.
- Q2 remains unresolved as a real host and HTTPS acceptance item. #74 is closed as tooling delivered, not as demo-host acceptance; 8.14 records the actual host, certificate and phone-scan evidence and files fixes.
- Dependency note: 8.11 waits on 8.13/2.10/2.11, 8.12 on 8.11, 8.10 on 8.11/8.12/8.13, 8.5 on 8.10 plus the focused backend sweeps and 7.3, 8.4 on 8.5, and 8.14 on 8.1. These declared dependencies produce the sequence above; 8.14 is an early host-evidence branch, not a late gate.

## Requirements

- R1 (prd SM-1; spec §20): Every SPEC §20 acceptance scenario passes on the integrated dev build, with the evidence recorded.
- R2 (prd SM-2): All 26 functional IDs, FR-35 and FR-36 are demonstrated end to end on the integrated dev build.
- R3 (prd SM-4): The feature epics' seat, attendance and certificate concurrency tests pass together on SQL Server with zero overbooking or duplicates.
- R4 (prd NFR-1, NFR-6, Q2): Officers' phones scan a QR Pass in Chrome over the hotspot through trusted HTTPS on the demo host, with manual lookup still available.
- R5 (prd NFR-4): SQL Server and stored images and Certificates survive app restart and redeploy and are backed up and restored together into a disposable location.
- R6 (prd SM-3; addendum D): Tier 3 manual E2E passes and dev is released to main.

## Constraints

- Passwords only as salted Argon2id hashes; signed expiring cookies; antiforgery on every mutation; ownership checks on personal resources; every time, capacity and state guard on the server; secrets, hashes and QR tokens never logged; no login throttling (parent Constraints; prd NFR-1; AD-8, AD-15).
- Files on persistent server-local storage outside the web root, separate venue/event/certificate directories, relative paths only in SQL (parent Constraints; prd NFR-4; AD-12, AD-16).
- One MVC app and one SQL Server database; Chrome target; mobile-responsive (parent Constraints; prd NFR-6); no IIS, reverse proxy, cloud service or background job (AD-16); net10.0 runtime (AD-18).
- feature/* or fix/* branches, PRs to dev with green CI and a peer approval, Conventional Commits (parent Constraints; addendum D).
- SPEC v2.1 in docsss/ is authoritative for rule detail unless prd §9 overrides (parent Constraints).

## Done when

- Every SPEC §20 acceptance scenario passes on the integrated dev build (SM-1).
- All 26 functional IDs, FR-35 and FR-36 are demonstrated end to end (SM-2).
- The seat, attendance and certificate concurrency tests pass together (SM-4).
- A phone in Chrome scans a QR Pass over the hotspot under trusted HTTPS, and SQL, stored files and the Data Protection keys (SetupRoot/keys) survive restart, redeploy and a restore to a disposable location (AD-12, AD-16, NFR-4, NFR-6, Q2).
- Tier 3 manual E2E passes and dev is released to main (SM-3).
- No demo data, mock, fixture fallback or mock route remains reachable; every contract is served by its real backend (8.11).
- Real demo-host evidence is recorded for the host, trusted HTTPS and phone scan; tooling delivery alone does not satisfy it (8.14).

## Boundaries

- Belongs to the feature epics: fixing behavior; this epic runs and records acceptance and reports defects back as bugs.
- Out: production hosting, uptime, cloud services (AD-16: no IIS, reverse proxy, cloud service or background job).

## References

- parent — _bmad-output/initiative-attendta/initiative-attendta.md, Done when
- prd — _bmad-output/initiative-attendta/prd-attendta/prd-attendta.md, §8 Success Metrics, §10 Q2
- spec — docsss/AttendTa-System-Specification.md, §20 acceptance coverage
- spine — _bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md: AD-16 (Environments and operations), AD-17 (Test harness), AD-7 (runtime login), AD-12 (SetupRoot), AD-18 (runtime), spine Deferred (Q2). docsss/architecture.md §10–§11 is companion context only.
- qa process — docsss/git_qa_guidelines.pdf, Tier 3

## Notes

- Historical delivery attribution: team-owned; Technical Manager (Gabutan) for the hotspot and HTTPS setup (Q2). This is history only; future work is claimed per ticket from the shared queue and no fixed human owner is assigned (2026-11-06 decision). Q2 remains a decision the Technical Manager owns.
- Open question (still open 2026-11-06): Q2 — host address and trusted HTTPS certificate for officers' phones (parent Notes). Decision owner: Technical Manager (Gabutan) with e8 (spine Deferred). When decided, record here the mechanism (CA or certificate path), host address and the phone trust steps; entry 2 and 8.14 cite this record. Q2 is a real host/HTTPS acceptance item, not a tooling item.
- Check beyond criteria (entry 2, high risk): repeat the scan on a second officer phone that has never had the certificate installed, to show trust comes from the certificate chain and not from device setup.
- Decision: 2026-11-04 — re-sliced against the architecture spine (AD-7, AD-10, AD-12, AD-16, AD-17, AD-18): 8.1 provisions demo host with AttendTa_Demo, restricted activity_logs login, SetupRoot/keys, and Playwright Chromium; 8.2 specifies direct Kestrel HTTPS and mobile scanning; 8.3 bundles Data Protection keys in backup; 8.4 executes whole IntegrationTests suite on ATTENDTA_TEST_SQL including approval and last-admin concurrency tests; 8.5 keeps automated tests in existing test projects.
- Decision: 2026-11-04 — refined from the initiative's Done when; covers no requirement id because it verifies the whole set.
- Waits on epic-platform-access, epic-master-data, epic-events-registration, epic-check-in, epic-administration, epic-certificates and epic-participation-reports because: the acceptance run needs every finished feature epic, the concurrency run needs 3.7, 4.3 and 6.7, phone scanning needs 4.4, and host provisioning needs the Playwright runtime from 6.1. These epic-level waits gate backend completion, not the frontend entries; 8.5 waits on 8.10, the backend sweeps (3.14, 4.9, 5.10, 6.11), 7.3, 1.8 and 2.9.
- Decision: 2026-11-04 — tracer bullet: entry 1, Demo host runtime environment provisioning; next: entry 2, Hotspot network binding and trusted HTTPS mobile camera scanning (least certain).
- Historical decision (superseded 2026-11-06): 2026-11-04 — sequencing: host provisioning (1) unlocks hotspot scanning (2) and backup/restore (3); concurrency (4) and automated SPEC §20 (5) run once feature epics land; manual checklist (6) and functional demo walkthrough (7) gate Tier 3 regression (8) before dev-to-main release (9); sweep (10) closes.
- Decision: 2026-11-04 — keep entry 10 (Refactor sweep) although the epic is verification-only; the suggestion to drop it was declined by the user.
- Historical decision (superseded by the final 2026-11-06 handoff above): the earlier finale order placed cleanup before host-evidence/test/signoff entries 8.2–8.8. The final order keeps 8.14 as early evidence after 8.1 and puts 8.10 before the 8.5→8.4→8.6→8.7→8.8 acceptance chain; 8.9 is last. The parent owns these entries.
- Decision: 2026-11-06 — final test/release handoff: 8.5 (#78) authors/maps important automated cases before 8.4 (#77) executes the combined suite; 8.6 manual SPEC §20 acceptance, 8.7 functional demo and 8.8 Tier 3 signoff follow. 8.10 cleanup precedes this chain, 8.9 is last, and the existing CI workflow is unchanged.
- Decision: 2026-11-06 — 8.12 is the only whole-app frontend review and polish and uses impeccable; 8.13 is backend hardening and does not. The feature-epic sweeps 3.14, 4.9, 5.10 and 6.11 remain focused backend cleanup, not repeated UI audits.
- Decision: 2026-11-06 — #74 is closed as tooling delivered, not as real demo-host acceptance. 8.14 records the actual host, trusted HTTPS and phone-scan evidence and files fixes; Q2 stays open until that evidence exists.
- Note: 2026-11-06 — no container status change and no implementation starts from this planning edit.
- Decision: 2026-11-06 — demo rescope: 2.11 joins the finale before 8.12; 8.11 proves the demo-mock-register empty and removes the demo store and switch; 8.12 covers the single integrated Impeccable pass incorporating 8.15 demo rehearsal findings; 8.10 removes leftover demo configuration before acceptance.
- Decision: 2026-11-06 — demo rescope: 8.15 joins this epic from epic-platform-access (old 1.12) as the closing demo rehearsal, first in build order among the open entries; it waits on 1.13 and the demo-critical surfaces 3.16, 3.17, 4.10, 4.11, 4.12, 5.11, 6.12, 6.13 and 7.4.
- Decision: 2026-11-06 — review of the demo rescope: 8.15 waits on 1.14 and checks each demo ticket's full Verify line, 8.13 deletes demo seam M-01 once all other rows are removed, 8.11 becomes verify-only with failing startup and search checks, and 8.10 confirms no demo configuration remains in deployment settings.
