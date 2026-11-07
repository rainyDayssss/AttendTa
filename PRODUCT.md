# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Officers and administrators are the primary operators: they review membership requests, maintain member/event-type/venue records, publish events, review registrations, run check-in, and read participation reports. Members are the other core audience: they request membership, browse events, register, hold passes, and view attendance and certificates. The demo prioritizes a complete officer flow with a member registration and attendance path.

## Product Purpose

AttendTa is a student-organization event registration and attendance system. It replaces in-person sign-ups and paper attendance with a reviewed membership request, online registration, door check-in, certificate eligibility, and post-event reporting. Success means an organization can run an event end to end and show a trustworthy record of who registered, who attended, and who earned a certificate.

## Positioning

One auditable lifecycle for student-organization events: request, approve, publish, register or waitlist, check in, certify, and report — with an append-only activity log behind every decision. Generic event tools stop at registration; AttendTa carries the same participant record through attendance and certification.

## Operating Context

Used by a University of Cebu student organization on a laptop at the door and on phones between classes. Officers work from a management shell with dense tables, filters, and decision actions. Members work from a simpler shell focused on events, passes, and personal history. The system runs as one ASP.NET Core MVC application against one SQL Server database; the demo runs on a local or hotspot host. Times are stored in UTC and shown in Asia/Manila.

## Capabilities and Constraints

- Authentication by email and password with role-based access for administrator, officer, and member.
- Membership requests are reviewed and approved or declined with a reason; decisions are audited.
- Master data covers members, event types, and venues; records are deactivated rather than hard-deleted.
- Master Data lists announce a loading state while a filter, pager, search or status toggle is in flight, and a failed read keeps the shell, tabs and table headers with an in-shell notice and a Try again link instead of an empty-state row.
- Member, Event Type and Venue create/edit use the same centered keyboard-operable record dialog with required markers, retained server validation, focus containment and restore, and a venue photo preview. Clicks on the dialog's own padding keep it open; only a backdrop click, Escape or Cancel dismisses it. Standalone form routes remain available.
- Events move through draft, published, completed, and cancelled states with capacity, deadline, and venue checks.
- Officers open an event dossier from Events to review metadata, confirmed capacity and paged status rosters, make explicit registration decisions, and confirm check-in and lifecycle changes in contextual dialogs.
- Failed dossier registration decisions retain the participant/registration context and entered reason for correction; review-queue failures stay within their original workflow.
- Registrations are pending, confirmed, waitlisted, or cancelled; seat allocation is transactional.
- Attendance is recorded by QR or manual lookup; certificates are issued one at a time from an eligibility review.
- The door scanner keeps scan and explicit manual check-in outcomes visible with participant identity and the original check-in time. Camera permission failure offers pass-code and manual alternatives; leaving camera mode cancels pending camera startup.
- Participation reports are per event and include attendance rate, no-shows, and course/year distribution.
- No hard deletes. Activity logs are append-only. Passwords are stored only as Argon2id hashes.
- The MVP is a light-theme web application; there is no dark mode, native app, or offline mode.
- The officer certificate ledger supports completed-event review, final decline reasons, individual issuance, and PDF viewing/download with the approved template and signatory. In the guarded demonstration configuration, reviews and issued documents live only in shared process memory; retained issuance and audit integration remain scheduled backend work.
- My Certificates shows the member's pending, eligible, declined, issued and unavailable records with review reasons, independently paged history, and issued-only View/Download PDF. Every document read checks the current owner; in the guarded demonstration configuration, the member and officer share the same captured snapshots and in-memory PDF bytes.

## Brand Commitments

The product name is AttendTa, used by a University of Cebu student organization. The approved identity is the AttendTa logo with the University of Cebu institutional line. The application is a restrained campus operations interface: pale slate canvas, white panels, dark navy primary actions, thin borders, and limited amber accents. `docs/DESIGN.md` is the visual authority.

## Evidence on Hand

- `docs/UC-Project-FL.pdf` — approved function list and module ownership.
- `docs/AttendTa-System-Specification.md` — authoritative rule detail.
- `docs/DESIGN.md` — approved visual specification for the 20 core screens.
- `docs/architecture.md` and `_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md` — implementation baseline.
- `_bmad-output/initiative-attendta/prd-attendta/` — product requirements and decisions.
- Working ASP.NET Core MVC application with seeded local demo data; no production customer data or testimonials exist.

## Product Principles

1. Every decision leaves a record: approvals, rejections, edits, and attendance changes are auditable.
2. The participant record is the spine: one identity carries from request through registration, attendance, and certificate.
3. Server-side rules are the truth: capacity, deadlines, roles, and state guards are enforced on the server.
4. Officers need density and speed; members need clarity and confidence.
5. No dead ends: every empty, error, and blocked state explains what happened and what to do next.

## Accessibility & Inclusion

Target WCAG 2.1 AA: 4.5:1 normal-text contrast, visible keyboard focus, keyboard-operable controls and dialogs, labelled form fields, text alternatives for meaningful images, and usable layouts at 200% zoom. Status must never rely on color alone.
