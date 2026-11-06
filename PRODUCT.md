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
- Events move through draft, published, completed, and cancelled states with capacity, deadline, and venue checks.
- Registrations are pending, confirmed, waitlisted, or cancelled; seat allocation is transactional.
- Attendance is recorded by QR or manual lookup; certificates are issued one at a time from an eligibility review.
- Participation reports are per event and include attendance rate, no-shows, and course/year distribution.
- No hard deletes. Activity logs are append-only. Passwords are stored only as Argon2id hashes.
- The MVP is a light-theme web application; there is no dark mode, native app, or offline mode.

## Brand Commitments

The product name is AttendTa, used by a University of Cebu student organization. The approved identity is the AttendTa logo with the University of Cebu institutional line. The application is a restrained campus operations interface: pale slate canvas, white panels, dark navy primary actions, thin borders, and limited amber accents. `docs/DESIGN.md` is the visual authority.

## Evidence on Hand

- `docs/UC-Project-FL.pdf` — approved function list and module ownership.
- `docs/AttendTa-System-Specification.md` — authoritative rule detail.
- `docs/DESIGN.md` — approved visual specification for the 20 core screens.
- `docs/architecture.md` and `_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md` — implementation baseline.
- `_bmad-output/initiative-attendta/prd-attendta/` — product requirements and decisions.
- Working ASP.NET Core MVC application with seeded local demo data; no production customer data or testimonials exist.
- Preliminary shared-dialog polish for issue #153 covers centering, long-content scrolling and safe backdrop dismissal only. Final 20-screen integrated acceptance remains blocked pending #152 (real integrations) and #135 (demo rehearsal findings); component checks are not whole-app acceptance.

## Product Principles

1. Every decision leaves a record: approvals, rejections, edits, and attendance changes are auditable.
2. The participant record is the spine: one identity carries from request through registration, attendance, and certificate.
3. Server-side rules are the truth: capacity, deadlines, roles, and state guards are enforced on the server.
4. Officers need density and speed; members need clarity and confidence.
5. No dead ends: every empty, error, and blocked state explains what happened and what to do next.

## Accessibility & Inclusion

Target WCAG 2.1 AA: 4.5:1 normal-text contrast, visible keyboard focus, keyboard-operable controls and dialogs, labelled form fields, text alternatives for meaningful images, and usable layouts at 200% zoom. Status must never rely on color alone.
