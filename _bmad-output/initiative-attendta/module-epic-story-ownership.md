# AttendTa module, epic, and story ownership

## Current delivery rule — 6 November 2026

**The project owner superseded fixed future assignments with five interchangeable builders
using one prioritized shared queue.** Whoever is available claims the next unblocked ticket,
updates its status before editing and hands it to review. No named person below owns all future
work in a module. Human approval/environment contacts remain contacts, not implementation lanes.

Use [delivery-queue.md](delivery-queue.md) for the current demo-first order, claim protocol,
demo data removal responsibilities ([demo-mock-register.md](demo-mock-register.md)) and finale.
The numeric `queue` and `phase` in each `tickets.toml` order open work; the GitHub issue is the
live claim/status source once it is synced with the 2026-11-06 demo rescope.

The architecture's code/table writers and shared service boundaries still apply. Since the
2026-11-06 demo rescope, Impeccable critique and polish run only in 8.15 (demo rehearsal),
2.11 and 8.12 (finale), not on each frontend ticket.
Historical assignees and the WBS mapping below are preserved for attribution, **not dispatch**.

## Historical WBS mapping and original baseline waves

The rest of this document records the earlier module assignment and original planning snapshot.
Its old waves and ownership language are not the active queue and do not override the rule above.

This is a mapping of the existing BMAD epics and stories to the module owners in the [WBS](../../docs/WBS_J4S_ATTENDTA_20261103.pdf), pages 9–11. Story IDs are written as `epic.story` (for example, `3.15`); the linked `tickets.toml` files contain the full descriptions, verification criteria, and dependencies. This document is an ownership reference, not a separate set of BMAD tickets or a record of completed work.

| WBS member | Owner | WBS modules | BMAD epics |
|---|---|---|---|
| Member 1 | Gabutan, Jerhon Clyde | AUTH-01–03 | Epic 1: Platform access (authentication and shared access checks) |
| Member 2 | Taguiam, Jasper | MD-01–05 | Epic 2: Master data; shared pager in Epic 1 |
| Member 3 | Sinco / Singco, John Paul | EVT-01–04, REG-01–05 | Epic 3: Events and registration |
| Member 4 | Talisic, Jhon Rosell | QR-01, ATT-01–03 | Epic 4: Check-in |
| Member 5 | Pinca | CERT-01–02, REP-01, ADM-01–02 | Epics 5–7: Administration, certificates, and participation reports; administrator setup in Epic 1 |

The WBS spells Member 3 **Sinco**; the [BMAD ownership addendum](prd-attendta/addendum.md) notes the alternate spelling **Singco**. The WBS and BMAD addendum both place administration with Member 5. Some older implementation notes describe a conflicting Member 1 allocation; coordinate shared authentication and audit interfaces with Gabutan.

## BMAD sequence and dependencies

The planned epic order is **1 → 2 → 3 → 4 → 5 → 6 → 7 → 8**. This is a planning order, not a rule that every epic must wait for the preceding one: the dependency graph notes, for example, that Epic 5 does not wait for Epic 4. The owner tables below group stories by module and are not a story execution order.

The current dependency graphs are [dependency-graphs-demo-first.md](dependency-graphs-demo-first.md) (2026-11-06 demo rescope). The original [BMAD dependency graphs](dependency-graphs.md) below describe the ticket tree at commit `420f72c` and are history; check each story's `after` field and the current BMAD ticket status before starting work. The graph's earliest-start waves are reproduced here for convenience. Stories in a wave can start in parallel only when their prerequisites and applicable epic gates are met; the waves do not indicate completion or assignment.

| Wave | Stories |
|---|---|
| 0 | 1.10 |
| 1 | 1.2 |
| 2 | 1.1 |
| 3 | 1.3, 1.9 |
| 4 | 1.11, 2.2, 2.8 |
| 5 | 1.4, 2.3, 6.1 |
| 6 | 1.5, 6.2, 8.1 |
| 7 | 1.6, 6.3 |
| 8 | 1.7, 2.1, 2.4, 5.1, 5.8 |
| 9 | 1.8, 2.5, 2.6, 5.2 |
| 10 | 2.7, 3.1 |
| 11 | 2.9, 3.2 |
| 12 | 3.3 |
| 13 | 3.4, 3.5 |
| 14 | 3.6, 3.15 |
| 15 | 3.7, 5.3 |
| 16 | 3.8, 3.9, 3.11, 5.4, 5.6 |
| 17 | 3.10, 3.12, 5.5, 5.7 |
| 18 | 3.13, 4.2, 5.10 |
| 19 | 3.14, 4.1, 4.3 |
| 20 | 4.4, 6.4 |
| 21 | 4.5 |
| 22 | 4.6, 8.2 |
| 23 | 4.7, 6.5, 7.1 |
| 24 | 4.9, 6.6, 7.2 |
| 25 | 6.7, 7.3 |
| 26 | 6.8, 8.3, 8.4 |
| 27 | 6.9 |
| 28 | 6.11 |
| 29 | 8.5 |
| 30 | 8.6 |
| 31 | 8.7 |
| 32 | 8.8 |
| 33 | 8.9 |
| 34 | 8.10 |

## Member 1 — Gabutan: Authentication and platform access

**Epic 1:** [Members sign in to a real, audited platform](tickets/epic-platform-access/epic-platform-access.md) · [Story details](tickets/epic-platform-access/tickets/tickets.toml)

| Story | Existing story | Module or ownership note |
|---|---|---|
| 1.1 | Provisioned administrator logs in by email via Argon2id into authenticated session with active role checks | AUTH-03: Gabutan owns login, sessions, and access checks; Pinca owns the administrator setup script within this story. |
| 1.5 | Public Account Request submission with field validation and Argon2id hash | AUTH-01 |
| 1.6 | Shared 25-row pager helper and Management Account Request queue | AUTH-02 queue: Gabutan; shared pager helper: Taguiam. |
| 1.7 | Management Account Request approval and rejection in caller transaction with audit | AUTH-02; coordinate Member Profile creation with Taguiam. |

The remaining Epic 1 stories provide shared infrastructure. The existing epic does not assign each of these stories to a named individual, so their individual assignees should be agreed by the team:

| Story | Existing story |
|---|---|
| 1.2 | Canonical database schema update and EF Core mapping for all ten tables with lookup foreign keys |
| 1.3 | Caller-owned transaction unit of work and append-only Activity Log writer |
| 1.4 | Asia/Manila timezone display helper and UTC server-side time standard |
| 1.8 | Refactor sweep |
| 1.9 | Compiled Tailwind CSS pipeline and role-routed layout shell replacing Bootstrap |
| 1.10 | Retarget solution to net10.0 LTS, remove legacy packages, stand up xUnit v3 test projects and CI container baseline |
| 1.11 | Master-data repositories and services write inside the caller-owned transaction |

## Member 2 — Taguiam: Master data

**Epic 2:** [Management maintains members, event types and venues](tickets/epic-master-data/epic-master-data.md) · [Story details](tickets/epic-master-data/tickets/tickets.toml)

| Story | Existing story | WBS module |
|---|---|---|
| 2.1 | Event Type management with required description, audit logging, and paged Tailwind list | MD-03 |
| 2.2 | Shared IPrivateFileStore with venue, event, certificate and temporary areas and authorized photo route | MD-05; shared storage for other modules |
| 2.3 | Venue create and edit with required photo, replacement-on-commit, and capacity guard | MD-04, MD-05 |
| 2.4 | Venue paged list with photo thumbnails, active toggle, and Tailwind views | MD-04, MD-05 |
| 2.5 | Active Event Type and Venue read queries for event lifecycle | MD-03, MD-04; consumed by EVT |
| 2.6 | Member profile edit with student-number correction reason and audit | MD-02 |
| 2.7 | Member paged list with search and Tailwind shell | MD-02 |
| 2.8 | Member read-only self profile with ownership authorization | MD-01 |
| 2.9 | Refactor sweep | Master data |

MD-02 membership-status deactivation and reactivation are implemented by Pinca in stories 5.6 and 5.7; coordinate those changes with Taguiam's member edit screen. The shared pager helper is in story 1.6.

## Member 3 — Sinco / Singco: Events and registration

**Epic 3:** [Officers publish events and fill seats fairly](tickets/epic-events-registration/epic-events-registration.md) · [Story details](tickets/epic-events-registration/tickets/tickets.toml)

| Story | Existing story | WBS module |
|---|---|---|
| 3.1 | Draft event creation, edit, and management event list | EVT-01, EVT-03 |
| 3.2 | Event Cover upload, replacement, and draft removal via shared storage | EVT-01, EVT-03, MD-05 integration |
| 3.3 | Event publication with active master revalidation and activity audit | EVT-01 |
| 3.4 | Published event limited edits and immutable states | EVT-03 |
| 3.5 | Member event browsing and registration submission with academic snapshot | REG-01 |
| 3.6 | Signup deadline enforcement at exact local boundary | EVT-02, REG-05 |
| 3.7 | Registration review and transactional seat allocation with SQL Server concurrency test | REG-02 |
| 3.8 | Explicit waitlist promotion across full queue | REG-04 |
| 3.9 | Member self-cancellation and re-registration refusal | REG-03 |
| 3.10 | Event start cutoff enforcement and Not confirmed before cutoff status | REG-05 |
| 3.11 | Event cancellation with atomic registration cancellation and check-in closure | EVT-04 |
| 3.12 | Event completion closing check-in | EVT-04; certificate review integration with Epic 6 |
| 3.13 | My Events personal registrations list | REG-01–05; shared with attendance and certificates |
| 3.14 | Refactor sweep | Events and registration |
| 3.15 | Registration-owned participation cancellation service (AD-6) | REG-03 and shared cancellation for MD-02 / ADM-01 |

## Member 4 — Talisic: Attendance and QR

**Epic 4:** [Confirmed members get a QR Pass and officers record attendance](tickets/epic-check-in/epic-check-in.md) · [Story details](tickets/epic-check-in/tickets/tickets.toml)

| Story | Existing story | WBS module |
|---|---|---|
| 4.1 | On-demand QR Pass PNG rendering and download | QR-01 |
| 4.2 | Check-in window toggle with audit logging | ATT-01 |
| 4.3 | Check-in validation engine and attendance recording | ATT-01 |
| 4.4 | In-browser camera QR scanner on localhost | ATT-01 |
| 4.5 | Manual check-in fallback by name and student number | ATT-01 |
| 4.6 | Attendance invalidation with confirmation and reason | ATT-02; coordinate certificate effects with Pinca |
| 4.7 | Member attendance view on My Events | ATT-03 |
| 4.9 | Refactor sweep | Attendance and QR |

## Member 5 — Pinca: Administration, certificates, and reports

**Epic 5:** [Administrators control roles, access and the audit trail](tickets/epic-administration/epic-administration.md) · [Story details](tickets/epic-administration/tickets/tickets.toml)

| Story | Existing story | WBS module |
|---|---|---|
| 5.1 | Console accounts list with role and status badges | ADM-01 |
| 5.2 | Role assignment with last-active-Administrator app lock | ADM-01 |
| 5.3 | Upcoming registration impact preview for deactivation | ADM-01, MD-02 integration |
| 5.4 | Account deactivation with registration cancellation cascade and session refusal | ADM-01 |
| 5.5 | Account reactivation restoring login access only | ADM-01 |
| 5.6 | Member membership deactivation with preview, reason and cancellation cascade | MD-02; coordinated with Taguiam |
| 5.7 | Member membership reactivation restoring eligibility only | MD-02; coordinated with Taguiam |
| 5.8 | Read-only newest-first paged Activity Log viewer | ADM-02 |
| 5.10 | Refactor sweep | Administration |

**Epic 6:** [Officers issue retained PDF certificates](tickets/epic-certificates/epic-certificates.md) · [Story details](tickets/epic-certificates/tickets/tickets.toml)

| Story | Existing story | WBS module |
|---|---|---|
| 6.1 | Playwright .NET setup and certificate PDF issuance tracer | CERT-02 |
| 6.2 | Fixed-template landscape A4 Razor certificate rendering with embedded fonts | CERT-02 |
| 6.3 | Configure official authorized signatory details | CERT-02; PM Talisic supplies the signatory details. |
| 6.4 | Create certificate reviews in event completion transaction | CERT-01; coordinate with EVT-04 |
| 6.5 | Attendance invalidation review disqualification and issued-certificate lock | CERT-01, CERT-02; coordinate with ATT-02 |
| 6.6 | Management certificate review queue and approval decisions | CERT-01 |
| 6.7 | Transactional certificate issuance pipeline and audit | CERT-02 |
| 6.8 | Authorized certificate streaming download endpoint | CERT-02 |
| 6.9 | Member certificate download links in My Events | CERT-02 |
| 6.11 | Refactor sweep | Certificates |

**Epic 7:** [Officers read per-event participation reports](tickets/epic-participation-reports/epic-participation-reports.md) · [Story details](tickets/epic-participation-reports/tickets/tickets.toml)

| Story | Existing story | WBS module |
|---|---|---|
| 7.1 | Event selector and participation summary KPIs | REP-01 |
| 7.2 | Completed-event no-shows and signup snapshot attendee distributions | REP-01 |
| 7.3 | Paged participant detail rows with full-dataset totals | REP-01 |

Pinca also owns the initial administrator setup script in story 1.1.

## Shared team work — release acceptance

**Epic 8:** [The integrated build passes SPEC §20 and is demo-ready](tickets/epic-release-acceptance/epic-release-acceptance.md) · [Story details](tickets/epic-release-acceptance/tickets/tickets.toml)

The epic is team-owned; Gabutan, as Technical Manager, owns the hotspot and HTTPS decision for story 8.2. Other individual story assignees are not specified in the existing epic.

| Story | Existing story |
|---|---|
| 8.1 | Demo host runtime environment provisioning |
| 8.2 | Hotspot network binding and trusted HTTPS mobile camera scanning |
| 8.3 | Database and persistent file storage backup, restore, and redeploy survival |
| 8.4 | Whole IntegrationTests suite execution on SQL Server |
| 8.5 | Automated SPEC §20 acceptance test suite execution |
| 8.6 | Recorded manual SPEC §20 acceptance checklist verification |
| 8.7 | Full functional demo script walkthrough across all 26 feature IDs |
| 8.8 | Tier 3 manual system integration and regression verification |
| 8.9 | Dev to main release pull request and submission preparation |
| 8.10 | Refactor sweep |

## Using BMAD with this map

The repository already has its epic and story breakdowns in `tickets.toml`; this Markdown file makes their ownership easier to read. The local command `.opencode/commands/bmad-create-epics-and-stories.md` describes a `bmad-create-epics-and-stories` workflow for generating a breakdown, but its referenced skill is not present in this checkout. The available `_bmad/method/scripts/tickets.py` supports `status`, `next`, `find`, `pull`, `mark`, `mirror`, and `move`; its `status` and `next` commands inspect the existing tree rather than automatically assigning every story by WBS module. This map does not set BMAD tracker assignees.
