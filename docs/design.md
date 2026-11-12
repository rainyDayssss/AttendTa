# AttendTa visual design specification

Version 1.2 · 4 November 2026

## 1. Purpose and baseline

Implement the screens in the submitted **[Alliance Jumpstart HLD, Group 7, AttendTa](Alliance_Jumpstart_HLD_Group_7_AttendTa.pdf)** with the same visual hierarchy, page composition, navigation, typography, component treatment, and meaningful interface states.

**Implementation context:** ASP.NET Core MVC with Razor views and Tailwind CSS. The [PRD](prd.md) consolidates business requirements and project-owner decisions; [architecture.md](architecture.md) defines the implementation baseline, folder structure, MVC responsibilities, security, persistence, and local/hotspot setup. This document defines visual presentation. Razor views do not imply the separate Razor Pages/PageModel programming model. Read the original specification, WBS, UC, and ERD through the conflict resolutions recorded in PRD §2; do not revive superseded wording from an older source.

This is a visual adaptation specification, not a requirement to translate prototype files directly. Preserve the HLD's visual language and recognizable screen composition while applying the approved scope corrections below. The final PDF has **65 pages and 20 numbered core screens**. PDF page references below are one-based physical pages, not screen numbers. The red headings, circuit-pattern slide backgrounds, and presentation framing belong to the HLD document; **do not reproduce them in the application**.

### Instructions for the implementing AI or developer

Read the linked PRD, architecture document, supplied requirements, repository instructions, and existing base code before implementing a screen. Use this document for **visual design and interface presentation only**: page composition, typography, spacing, colors, images, navigation appearance, forms, tables, dialogs, responsive behavior, and visible states. Argon2 and other security mechanisms belong in the architecture, not in ordinary user-facing form copy.

- Inspect the main application's MVC controllers, `.cshtml` views, shared layouts/partials, styling entry points, and frontend scripts before choosing where to place new work. Preserve its MVC structure and use Tailwind for the intended visual implementation. Do not assume Tailwind is already configured merely because it is specified here; document any setup or styling migration in the architecture work without replacing the application scaffold.
- The **PRD owns business behavior**: validation rules, permissions, eligibility, state transitions, cutoffs, and allowed actions. Business-rule descriptions here explain the visual states to represent; they are not an independent specification that overrides the PRD.
- The **architecture and base code own implementation**: routes, MVC controller actions, view models, component boundaries, services, data access, authentication, and state management. Existing implementation gaps do not override approved requirements. Resolve material conflicts through the project's documented decision process rather than guessing.
- Prototype HTML/CSS, when available, are optional visual evidence, not code that must be copied. Prototype JavaScript is **not the implementation contract**. Do not port seeded data, localStorage persistence, mock authentication, demo role switching, or client-side business logic merely because a reference uses it.
- Implement the UI interactions needed to reproduce the experience—opening and closing dialogs, keyboard focus, menus, tabs, previews, and feedback—using the main repository's established approach. Visual-only scope does not mean inert controls or disabling necessary frontend interaction.
- Obtain each visible state from the real application behavior defined by the PRD and architecture. Do not hardcode a success badge, approval outcome, report total, or enabled action to imitate a screenshot.
- Match the final HLD's application visuals, then verify the rendered result. Do not copy the presentation framing or reorganize the frontend solely to resemble the prototype's folders.

This document does not assume an AI already knows the main repository. Its implementing agent must inspect that repository; this workspace's file structure is not a substitute for that context.

### Separate visual reference repository

The visual reference is available locally at **`F:/AttendTa`**. Its inspected HEAD was `f65b054` on 4 November 2026; the review used working files and does not certify a clean worktree. The project owner used HTML as a design tool instead of Figma. Its folders, JavaScript behavior, and mock data are not the target application's structure. Its documentation is not a requirements source for this adaptation.

References below to root HTML, `assets/`, `images/`, `account/`, `student/`, and other prototype folders are relative to **that reference repository's root**, not this documentation folder or the backend repository. The local HLD, UC, specification, ERD, PRD, and architecture links remain relative to this document. These prototype-relative links are source locators; resolve them in the supplied checkout rather than expecting them to open beside this file.

Recheck and record the supplied reference repository path/URL and revision when implementing; the recorded local path need not exist on a teammate's machine. Read relevant source files to verify measurements, fonts, assets, responsive rules, and interactions. Their absence beside this Markdown file is expected and is not a reason to discard the references. If a file differs or is unavailable, record that specific gap and use the HLD for visible composition; do not claim source-level verification or substitute prototype business logic.

### Visual source precedence

1. Explicit project-owner decisions consolidated in the PRD and clarification register below, including scope and branding corrections.
2. Final PDF: screen composition and visual baseline wherever not superseded by those decisions.
3. Supplied reference repository's root HTML/CSS/assets: evidence for dimensions, styling, responsive behavior, and controls not fully legible in screenshots, checked against the recorded revision. Where source differs from the PDF, follow the PDF's visible composition and record the difference.
4. Older decks, `v2/`, screenshot folders, and audit documents: supporting history only. They are not interchangeable with the submitted PDF.

The root and `deliverables/` PowerPoint drafts differ from the final PDF. Do not use their slide numbers. Root styles still contain “v2” comments, but those files are actively referenced by the root prototype; this does not make the historical `v2/` directory the baseline.

This precedence governs appearance, not permission to add features. When a screenshot contains an action excluded by the reduced MVP, preserve the surrounding composition and omit the unsupported action, recording the discrepancy. Visual fidelity must not enable a deferred workflow.

### Evidence labels

- **HLD visual:** directly visible in a final PDF screen.
- **Source detail:** extracted from the current prototype to fill in measurements or behavior absent from a static screenshot.
- **Implementation requirement:** a necessary state/accessibility behavior described here, not a claim that the screenshot already demonstrates it.
- **Clarification:** a source discrepancy or unresolved product decision. Preserve the surrounding design while resolving it through the PRD/HLD change process.

### Clarification register

| ID | Evidence and decision | Implementation treatment |
|---|---|---|
| D-01 Registered email | HLD p.7 says institutional email; the supplied [function list](UC-Project-FL.pdf), p.1, permits email or student number. The [reduced system specification](AttendTa-System-Specification.md), §§1.1 and 4, specifies email/password and does not define a permitted institutional domain. | Use **Email address**, helper **Enter the email address registered to your account**, consistent with the reduced specification. Keep approval and active-account restrictions. Record the wording difference from the HLD; do not add student-number login or domain restrictions without an explicit requirements change. |
| D-02 Certificate identity | Resolved by project owner: the AttendTa logo is final and institution wording is **University of Cebu**. HLD USTP wording and prototype “Main Campus” wording are superseded. Recipient is the student; activity title is the event. | Preserve certificate hierarchy and adapt the fixed HTML/CSS template to these values. Do not invent an institutional seal, adviser name, job title, or signature. Only signatory details/signature remain pending; development placeholders must be visibly marked. |
| D-03 Attendance examples | HLD pp.46–48 repeat the Participation History view with the registrations tab visible. | Implement the three visible tabs. Use p.49 and current history source for attendance/certificate content; do not invent three separate page layouts from the repeated screenshots. |
| D-04 Last administrator protection | HLD p.61 shows the accounts list; it does not clearly depict a dedicated protection dialog. The rule is explicit on p.62. | Keep the list layout. Show a clear blocked-action explanation using the shared feedback pattern; this is an implementation requirement, not a screenshot-exact dialog. |
| D-05 Extra routes/actions | Landing page, prototype navigator, password-change page, and standalone account-creation page are not among the final 20 numbered core screens. The accounts screen nevertheless shows a New User action. The reduced specification creates member accounts through request approval, with a separate one-time initial-admin setup. | Omit New User/manual account creation and password-change actions from this MVP unless explicitly added by a requirements change. Initial-admin setup is not a general account-creation screen. Never ship the evaluator navigator or demo role switcher as product navigation. |
| D-06 Low-contrast text | Some palette pairs fail the normal-text contrast threshold; see §3. | Preserve layout and palette roles. Use an existing darker semantic token where it passes; track any necessary color correction explicitly. Do not reproduce a readability defect solely for screenshot fidelity. |
| D-07 Output actions | Prototype/HLD controls may suggest bulk certificates, printing, or report exports. The reduced specification includes one-at-a-time retained PDF issuance and authorized downloads, plus on-demand QR PNG downloads; bulk issuance, in-system QR printing, and CSV/PDF report exports are deferred. | Keep individual Issue/View/Download PDF and View/Download QR PNG actions within their permissions. Omit bulk issuance, dedicated QR printing, and report export/print actions. Members may print a saved QR externally; do not add an in-app printing workflow. |
| D-08 Displayed data and metrics | Project owner selected the specified per-event report scope. Prototype global tiles, category accents, pass codes, and account last-activity columns are visual evidence, not definitions of new stored fields or aggregate formulas. | Adapt summary cards to approved per-event values; omit global metrics and unsupported data controls/columns instead of displaying fake values. Do not invent login/session tracking, extra ERD columns, editable category-color fields, or a second credential scheme. A field may be restored only with a defined requirement/data source. |
| D-09 Default avatars | Resolved by project owner: default avatar visuals/initials are included; uploaded avatar images are excluded. | Keep the identity squares/circles and initials. No upload, change-photo, cropping, or user image storage workflow. |
| D-10 Prototype behavior | Source inspection found membership-inactive login blocking, additional registration states, request-time promotion, predictable QR tokens, browser storage, bulk certificates, mutable certificate rendering, and zero-denominator rates rendered as 0%. | Reuse visual presentation only. Apply the PRD's current account/member distinction, state model, queue rules, and report formulas; use architecture's real server services, secure tokens, and retained PDFs. Do not port these prototype behaviors as implementation shortcuts. |

## 2. Visual character

AttendTa is a restrained campus operations interface: pale slate canvas, white panels, dark navy text and primary actions, thin borders, compact records, and limited amber accents. Member screens emphasize event imagery and passes; officer/admin screens emphasize records and decisions.

- Keep broad whitespace around focused forms; keep tables compact and aligned.
- Use borders and surface changes as the primary grouping mechanism. Shadows are subtle, except for overlays.
- Main actions are usually dark navy. Amber highlights selected navigation, contextual actions, and small labels; it is not a background for every panel.
- Use green, amber, red, and neutral status treatments with readable text labels. Event-category colors are separate from operational status colors.
- Preserve the actual page's density. A data table must not become a collection of oversized dashboard cards.
- The approved application is a **light theme**. Dark scanner surfaces and dark identity strips are local treatments, not a dark-mode specification.
- Example names, event titles, dates, counts, academic year, and results are sample data. Reproduce their layout, not hardcoded values or stale “Live” labels.

## 3. Foundations and tokens

Exact source: [tokens.css](assets/tokens.css). Utility aliases: [tailwind.config.js](assets/tailwind.config.js). Shared styles: [components.css](assets/components.css). Page-specific styles can override these; preserve the rendered result rather than assuming a generic class always wins.

### Color roles

| CSS token | Value | Usage |
|---|---|---|
| `--ink-primary` | `#090D16` | Primary copy, dark actions |
| `--ink-secondary` | `#334155` | Supporting copy and secondary controls |
| `--ink-muted` | `#64748B` | Metadata and helper text |
| `--ink-faint` | `#94A3B8` | Decorative or inactive details; avoid essential small text |
| `--ink-inverse` | `#FFFFFF` | Text on dark surfaces |
| `--canvas-ground` | `#F8FAFC` | Page background |
| `--canvas-surface`, `--canvas-elevated` | `#FFFFFF` | Cards, forms, menus, dialogs |
| `--canvas-subtle` | `#F1F5F9` | Secondary sections, subdued cells |
| `--canvas-dark`, `--navy-hover` | `#0B1120` | Scanner/dark surfaces and dark-action hover |
| `--accent-amber` | `#D97706` | Brand accents and focus |
| `--accent-amber-deep` | `#B45309` | Stronger amber text/hover |
| `--accent-amber-subtle` | `#FEF3C7` | Amber-tinted surfaces |
| `--accent-amber-glow` | `rgba(217,119,6,.12)` | Subtle accent effect |
| `--status-emerald` / `--status-emerald-text` / `--status-emerald-subtle` | `#059669` / `#047857` / `#ECFDF5` | Success, active, approved |
| `--status-amber` / `--status-amber-subtle` | `#D97706` / `#FFFBEB` | Pending, caution; use deep amber for small text |
| `--status-rose` / `--status-rose-subtle` | `#DC2626` / `#FEF2F2` | Destructive actions and errors; verify text contrast |
| `--status-neutral` / `--status-neutral-subtle` | `#475569` / `#F1F5F9` | Neutral/inactive metadata |

Existing HTML also uses Tailwind slate colors directly, particularly `slate-900`, `slate-700`, `slate-500`, and `slate-200`. Do not blindly replace every literal with a visually different semantic value. Consolidation is acceptable when the screen remains faithful.

Event accents use these base/deep/subtle values. They scope to event cards, category markers, pass rules, and event-specific details, not the whole officer workspace.

| Accent | Base | Deep | Subtle |
|---|---|---|---|
| Moss | `#4D7C0F` | `#3F6212` | `#F7FEE7` |
| Indigo | `#4F46E5` | `#4338CA` | `#EEF2FF` |
| Teal | `#0D9488` | `#0F766E` | `#F0FDFA` |
| Violet | `#7C3AED` | `#6D28D9` | `#F5F3FF` |
| Crimson | `#BE123C` | `#9F1239` | `#FFF1F2` |
| Orange | `#EA580C` | `#9A3412` | `#FFF7ED` |
| Slate | `#3D5A80` | `#2E4560` | `#EFF4F9` |
| Amber | `#D97706` | `#B45309` | `#FEF3C7` |

Each event glow is its base RGB at 12% opacity. Preserve existing category assignment from the prototype; do not infer business status from an event accent.

### Measured text contrast

Calculated from opaque sRGB token values. Ratios apply only to the named pair, not arbitrary image/gradient backgrounds. “Normal-text pass” uses a 4.5:1 review threshold; 7:1 is the enhanced threshold. These measurements are not an accessibility certification.

| Foreground / background | Ratio | Treatment |
|---|---:|---|
| Primary ink / white | 19.43:1 | Normal-text pass; enhanced threshold met |
| Secondary ink / white | 10.35:1 | Normal-text pass; enhanced threshold met |
| Muted ink / white | 4.76:1 | Normal-text pass |
| Muted ink / ground | 4.55:1 | Normal-text pass |
| Faint ink / white | 2.56:1 | Fails normal text; decorative use only |
| Amber / white | 3.19:1 | Fails normal text; use deep amber for small labels |
| Deep amber / amber subtle | 4.51:1 | Normal-text pass |
| Emerald text / emerald subtle | 5.21:1 | Normal-text pass |
| Rose / rose subtle | 4.41:1 | Fails normal text; needs darker text or white background |
| Neutral / neutral subtle | 6.92:1 | Normal-text pass |
| Primary ink / amber | 6.10:1 | Normal-text pass |

### Typography

| Role | Source specification |
|---|---|
| Body and display | **Outfit**, system sans-serif fallback |
| IDs, dates, numeric metadata | **JetBrains Mono**, monospace fallback; tabular numerals for aligned numbers |
| Certificate heading | **Cinzel**, Georgia/serif fallback |
| Base copy | 15px (`.9375rem`), line-height 1.5 |
| Compact density | 14px base; use only where the source applies compact density |
| Page headings | Usually 24px, growing to 30px on larger screens; bold/extrabold with tight tracking |
| Form section labels | Usually 12px semibold/bold; uppercase and tracking where shown |
| Controls | Shared buttons/inputs 14px; many compact page-local controls are 12px |
| Ledger cells | Shared component 13px; numeric/mono cells 12.5px |
| Dialog heading | 18px, weight 700, line-height 1.4 |

Match actual heading wraps and line lengths in the PDF. Do not replace Outfit with a generic sans-serif and compensate with spacing. Keep fonts available before visual comparison. Small metadata must remain readable; the scaled slide screenshot is not a font-size measurement.

### Spacing, shape, and effects

- Use the source's 4px spacing rhythm: 4, 8, 12, 16, 20, 24, 32, 48px. Common section gap is 24px; compact gap is 16px.
- Page padding is usually 16px on narrow screens and 32px in officer/admin desktop content. Member containers use 16/24/32px gutters.
- Radius tokens: small **6px**, medium **10px**, large **16px**, pill **9999px**. Existing page-local 8px controls and dialog close buttons are deliberate local details.
- Tailwind's source configuration replaces radius and shadow scales. Do not assume the target project's stock `rounded-lg` or `shadow-*` produces the same result. Match computed values and preserve local exceptions.

| Token | Exact value |
|---|---|
| Hairline light | `1px solid rgba(226,232,240,.9)` |
| Hairline medium | `1px solid rgba(203,213,225,.85)` |
| Hairline dark | `1px solid rgba(15,23,42,.12)` |
| Hairline amber | `1px solid rgba(217,119,6,.4)` |
| Tactile shadow | `0 1px 2px 0 rgba(15,23,42,.04)` |
| Subtle shadow | `0 4px 12px -2px rgba(15,23,42,.05)` |
| Modal shadow token | `0 20px 40px -15px rgba(15,23,42,.14)` |
| Actual shared task-dialog shadow | `0 24px 64px rgb(15 23 42 / .2)` |
| Motion | 120ms fast, 180ms base, 240ms slow; `cubic-bezier(.2,0,0,1)` |
| Focus | 2px amber outline, 2px offset; close button uses deep amber and 3px offset |

Navigation/sticky/drawer/modal token levels are 30/40/50/60. Current task dialogs override to 1000. Preserve their stacking relationship; an open task dialog must cover sticky headers and navigation.

## 4. Page shells and responsive composition

These are visual shell categories, not prescribed Razor layout files.

| Shell | Composition and source details |
|---|---|
| Authentication | Small logo header, 56px tall; minimal contextual link opposite. Pale canvas, centered white form, quiet footer. Login max width 448px; registration max width 576px. |
| Member | White 64px top navigation with logo, academic-year label, Events, My Passes, History, identity and Profile menu. Main content generally max width 1152px with responsive gutters. No officer sidebar. |
| Officer/admin | White left sidebar, slim breadcrumb/top bar, pale main canvas, heading/description, then white content sections. Root pages use **256px** sidebar (`w-64`), visible from 1024px. Main content generally max width 1280px. The older generic `.sidenav` has 240px width; do not substitute it for the root screen shell. |
| Scanner | Focused operational page, max width 1024px, event selection and check-in controls above scanning area. No persistent officer sidebar. Exit/back remains visible. |
| Certificate | Quiet document presentation centered on pale canvas. Certificate artwork dominates; controls and chrome are secondary and excluded from document output. |

Officer navigation groups include operations and master data, with administrator controls where authorized. Preserve labels and order from the matching root page and HLD. Officer active items use amber; the admin examples use a teal treatment. Keep one account menu at the bottom of the desktop sidebar; use the mobile header equivalent below the sidebar breakpoint.

### Responsive rules

Source-derived behavior fills the HLD's desktop-only gaps:

- Below **1024px**, replace desktop sidebar with navigation drawer; never squeeze the same sidebar beside a phone-width table.
- Member event cards follow their source grid; preserve the three-column desktop composition on p.33 and collapse toward one column. Passes use two columns from 768px and one below.
- Registration field groups collapse from two/three columns to one below 640px. Profile record grids collapse below 768px.
- Tables retain meaningful column labels. Use contained horizontal scrolling for dense operational tables; use card conversion only where the prototype explicitly provides it.
- Wrap toolbars and filters without hiding primary actions, selection state, or result count. Do not let the document itself scroll horizontally.
- Dialogs remain centered. At widths up to 600px, use 12px outer clearance, 20px body padding, and single-column field grids.
- Preserve all meaningful information at narrow widths; sample desktop density is not permission to clip names, actions, validation messages, or QR codes.

## 5. Shared component contract

### Buttons and links

Shared sizes: small 32px high, default 40px, large 48px; default horizontal padding 16px and icon gap 8px. Dark primary buttons use white text; white secondary buttons use a thin border; ghost controls are quiet; destructive buttons are red. Use the screenshot's actual action emphasis rather than making every action amber.

Provide rest, hover, focus, pressed, disabled, and submitting states. Keep labels visible while submitting and prevent repeat submission. Do not use color alone to distinguish destructive actions. Scanner controls provide 44px minimum targets, increasing to 56px for coarse pointers in the current source.

### Forms

Use labels above fields, helper text beneath, clear required markers, and inline error text near the relevant field. Shared inputs/selects are 40px high, with 12px horizontal padding and 6px radius. Textareas start at 96px high. Preserve page-specific tighter controls where shown.

Show existing values in edit forms. Read-only records must look read-only, not like broken editable inputs. On validation failure retain user input, identify the field, and keep the correction visible. Upload controls show the existing image or selected preview plus format/required-photo guidance.

### Tables, tabs, filters, and pagination

Use white table surfaces, subtle header rows, thin horizontal rules, left-aligned descriptive text, and aligned numeric columns. Keep the title/filter toolbar, table, and footer visually connected. Shared ledger headers use 10px × 12px padding; root page tables often use 12px × 16px. Match the individual screen.

Tab selection must remain visible with label, border/background, and count where shown. Search and status filters sit above records. Place result count and pagination under the table; do not repeat page-size implementation details in the top bar or footer branding. Empty results retain the table context and explain whether no records exist or filters matched none.

### Badges and status

Keep badges small with restrained tinted backgrounds and readable labels. Distinguish these domains:

- Event lifecycle: draft, published, completed, cancelled.
- Registration: pending, confirmed, waitlisted, cancelled.
- Attendance: recorded/valid and invalidated, with correction context.
- Certificate: pending review, approved, declined, issued.
- Account and membership: separate active/inactive statuses.

“Live,” “upcoming,” and deadline labels describe timing; they must not silently replace lifecycle or registration state. Counts and controls must reflect the same record state.

### Dialogs

Canonical source: [dialogs.css](assets/dialogs.css), supported by [dialogs.js](assets/dialogs.js). Task forms and confirmations are **centered dialogs**; drawers are for navigation. Old element IDs containing “drawer” do not change this visual contract.

- Standard dialog: max width 480px. Record editor: max width 680px. Both fit the available viewport.
- White panel, 16px radius, 1px slate border, modal shadow, 24px normal outer clearance; maximum height viewport minus 48px with internal scrolling.
- Overlay: `rgb(15 23 42 / .48)` with 3px background blur.
- Body padding 24px; footer usually 16px × 24px, 8px action gap; title leaves space for a 32px close button at top right.
- Record editors place a status/destructive action separately from Cancel and Save. Destructive confirmations use an explicit verb and reason field where required.
- Focus moves into the dialog, stays inside while open, and returns to the trigger on dismissal. Associate title and description; keep background content inert. Closing must never submit or confirm.

### Feedback and system states

Use inline feedback for field errors and blocked decisions; use the existing toast pattern for nonblocking completion messages. Announce meaningful updates to assistive technology. Do not use browser-native alert/confirm dialogs to replace HLD dialogs.

Every data-driven screen needs loading, populated, empty, error/retry, and unavailable/unauthorized behavior appropriate to its context. These are implementation requirements, not additional HLD screen numbers. Preserve the shell during loading and errors; never use an indefinite blank white panel.

## 6. Screen inventory and reproduction requirements

The HTML links below identify visual source files in the separate reference repository, **not target MVC routes**. Resolve them against that repository's root. Reuse source assets and styling evidence, while implementing behavior from the approved requirements. Sample localStorage and seeded data are not production requirements.

| HLD screen | Final PDF visual pages | Logic pages | Root reference |
|---|---|---|---|
| 1 Login | 6 | 7 | [account/login.html](account/login.html) |
| 2 Membership request | 8 | 9 | [account/register.html](account/register.html) |
| 3 Account requests | 10 | 11 | [account/approvals.html](account/approvals.html) |
| 4 My profile | 12 | 13 | [account/profile.html](account/profile.html) |
| 5 Members | 14–15 | 16 | [masterdata/members.html](masterdata/members.html) |
| 6 Event types | 17 | 18 | [masterdata/eventtypes.html](masterdata/eventtypes.html) |
| 7 Venues | 19–20 | 21 | [masterdata/venues.html](masterdata/venues.html) |
| 8 Event management | 22 | 23 | [events/index.html](events/index.html) |
| 9 Create event | 24 | 25 | [events/create.html](events/create.html) |
| 10 Event details and registrations | 26–29 | 30–32 | [events/details.html](events/details.html) |
| 11 Browse events | 33 | 34 | [student/events.html](student/events.html) |
| 12 Member event details | 35–37 | 38–39 | [student/details.html](student/details.html) |
| 13 My passes | 40 | 41 | [student/passes.html](student/passes.html) |
| 14 Door scanner | 42–43 | 44–45 | [kiosk/index.html](kiosk/index.html) |
| 15 My attendance | 46–48 | 49 | [student/history.html](student/history.html) |
| 16 Certificates | 50–52 | 53–54 | [certificates/index.html](certificates/index.html) |
| 17 Certificate viewer | 55 | 56 | [certificates/view.html](certificates/view.html) |
| 18 Participation reports | 57 | 58 | [reports/participation.html](reports/participation.html) |
| 19 Accounts and roles | 59–61 | 62 | [admin/users.html](admin/users.html) |
| 20 Activity history | 63 | 64 | [admin/auditlogs.html](admin/auditlogs.html) |

### 1. Login

Centered compact card on the authentication canvas. Heading “Sign in to your account,” supporting sentence, email and password fields, dark full-width Sign in action, and membership-request link. Preserve password visibility control and the modest branding/footer. Apply D-01 wording clarification without changing layout. Include invalid credentials, inactive account, required-field, and submitting feedback. Demo identities or role shortcuts are prototype-only.

### 2. Membership request

Centered taller form headed “Request Membership.” Preserve section order: **Student Identity**, **Program & Section**, **Account Password**. Full name spans the form; student number/email share a row; program selection precedes year, section, and mobile; password and confirmation share a row. Keep dark full-width submission and sign-in link. Include validation, duplicate identity, pending submission success, and rejected-request reapplication messaging according to the PRD.

### 3. Account requests

Officer shell, “Membership Account Requests” heading, search, Pending/Approved/Rejected tabs with counts, applicant identity and academic/email details, request/review metadata, and right-aligned decisions. Pending rows show Approve/Decline; finalized rows show outcomes. Decline requires a reason dialog. Empty queues preserve the toolbar and tab structure.

### 4. My profile

Member shell, “My Member Profile,” current active/inactive membership badge, amber information notice, and a dark identity strip with default initials/avatar, name/email, and student number. Below, use grouped read-only academic, membership, and account fields. Preserve the compact bottom action leading to event passes where applicable. An active account with inactive membership can still reach personal history and issued certificates; explain participation restrictions without falsely claiming the account cannot sign in. No profile-photo upload or password-change workflow is included.

### 5. Members

Officer shell, “Organization Members,” search and status filters, dense directory with identity, academic information, email, membership type, status, and Edit action. Edit opens a centered record dialog with identity summary, personal/academic fields, membership type, and future-registration notice. Preserve separate membership status action and Cancel/Save. Student-number correction introduces a reason field; membership type must remain distinct from account role.

### 6. Event types

Officer shell, “Event Classifications,” New Event Type action, search/status filters, and table with name, description, badge accent, active state, and row actions. Create/edit uses the shared dialog language. Inactive types stay visible in historical records; the UI must distinguish inactive selections from deleted records. Do not let category accent imply eligibility or attendance status.

### 7. Venues

Officer shell, “Campus Facilities & Venues,” New Venue action, search/status filters. Table preserves image thumbnail, facility name, location, capacity, contact, status, and Edit. Centered editor includes name, building/location, capacity, contact, availability notes, and required photo preview/replacement. Keep deactivation separate from save. Both entry points, if retained, must show the same editor design.

### 8. Event management

Officer shell, “Events Management,” New Event action, search, lifecycle tabs, and compact event table. Preserve title/description, venue, schedule, registration/capacity, status, and management actions. Draft rows and published/completed rows must have visibly appropriate actions. Do not restore an archive tab solely because historical code contains one.

### 9. Create event

Officer shell with centered form, “Create Event Session,” and sections for event details, schedule/capacity, and cover image. Preserve title, type/venue selection, description, date/time, capacity, registration deadline, and cover preview. Footer contains Cancel, Save Draft, and Publish/Open Signup with source emphasis. Required upload and field errors appear inside the form. Do not add decorative mood controls absent from the final composition.

### 10. Event details and registrations

Officer shell, breadcrumb back to Events, large event title and metadata, capacity summary, top actions, and tabbed registration roster. Keep Pending Review, Confirmed, and Waitlisted counts visible. Empty roster remains a proper table section.

Supporting states are explicit HLD visuals: **Edit Published Event** (p.27), **Cancel Event** with consequence warning/reason (p.28), and **Mark Event as Completed?** (p.29). Published edit exposes the permitted descriptive fields and shows operational fields as locked. Completed/cancelled records become read-only. Review, waitlist promotion, and cutoff feedback use the existing roster/dialog structure; business calculations remain in the PRD.

### 11. Browse events

Member shell, “Campus Events,” compact white live-event shortcut, filter pills, and a three-column desktop event grid. Each card shows cover image, category/timing badges, title, venue/schedule, registration information, and View details. Retain neutral canvas and white surfaces per [browse-events.css](assets/browse-events.css). Do not revive historical animated backdrop or enlarged live-hero treatments that differ from p.33. Collapse columns responsively and retain useful empty/search-result feedback.

### 12. Member event details

Member shell with back-to-events link. Wide photo hero has a dark scrim, readable badges, title, and schedule/venue. Under it, description and capacity sit beside a narrower registration-status panel and guideline card. Preserve that reading order when stacked on mobile.

P.35 shows confirmed registration and pass/cancel actions; p.36 shows signup closed; p.37 shows the centered cancellation confirmation. Pending and waitlisted states reuse the same status panel with truthful labels and allowed actions. Full capacity may permit a request with a waitlist warning if the PRD allows it; do not replace all full states with an unconditional disabled action.

### 13. My passes

Member shell, “Door Passes,” active and past sections. Use two-column desktop pass cards with a thin event-colored top rule, event metadata, holder/student identity, high-contrast QR, monospaced gate-code treatment where backed by the approved credential contract, status, and available output controls. Keep QR quiet zone and aspect ratio intact; never crop, blur, decorate, or stretch the code. Cancelled/unconfirmed records cannot appear as usable active passes. Provide on-demand QR PNG download; omit the dedicated print control visible on p.40 under D-07. Do not introduce a second credential scheme merely to reproduce a sample gate code.

### 14. Door scanner

Focused scanner shell with event selector, check-in open/paused control, dark active-session strip, and Camera Scan/Pass Code/Student Number entry modes. Camera viewport is large and dark; camera-unavailable text and manual alternatives remain visible. Recorded check-in log sits below.

Provide clearly distinct successful, duplicate, invalid, paused, and unavailable results using text, identity/context, and color. Manual lookup is an explicit action, not automatic check-in while typing. P.43 shows attendance invalidation with participant context, required reason, and destructive confirmation. Preserve the large operational targets and avoid transient feedback that disappears before an operator can read it.

### 15. My attendance

Member shell, “Participation History,” white tabbed panel containing My Registrations, My Attendance, and My Certificates with counts. Registrations use compact cards with category, event title, schedule, status, identifier, and View dossier. Attendance and certificate tabs reuse this container and density, with corresponding record details/actions. See D-03: repeated HLD screenshots do not define separate layouts. Show invalidated/corrected records clearly and keep personal history read-only.

### 16. Certificates

Officer shell, “Certificate Eligibility & Issuance,” completed-event filter, Eligibility Review/Issued Registry tabs, and compact table for member, event, attendance, eligibility, certificate, and actions. Preserve green approval, red decline, and amber issuance emphasis. Align row actions to one baseline and right edge using [certificates.css](assets/certificates.css).

P.51 defines decline with required reason; p.52 defines issue confirmation with recipient/event context. Pending, approved, declined, issued, and failed issuance must be distinct. Do not offer issuance before approval or present issued records as awaiting another decision. Issue one certificate at a time; omit prototype bulk-issuance controls under D-07. A failed attempt is feedback, not a new persisted certificate lifecycle state.

### 17. Certificate viewer

Adapt the document composition on p.55: centered white certificate, final AttendTa logo and **University of Cebu** institutional line, approved organization/year content, short amber divider, large ceremonial heading (for example, “Certificate of Participation”), recipient emphasis, event title and schedule, then a lower row for certificate number/date and authorized signatory information. Preserve whitespace, hierarchy, and alignment. A decorative/sample seal or signature is not approved identity; follow D-02 for pending signatory content.

Keep the certificate's landscape A4 proportions stable. Implement the artwork as a dedicated Razor HTML template with compiled Tailwind/CSS; fill values from issuance snapshots and render/save its PDF through the server pipeline in architecture §8. This retains the HTML design approach without making browser JavaScript responsible for official certificate data. Display the saved PDF using the browser's PDF viewer or an accessible View/Download link; a separate live HTML recreation of an issued certificate is unnecessary. Document output excludes navigation and action controls. Show unavailable/unauthorized/failure feedback outside the artwork. A browser print view is not a substitute for retained PDF issuance.

### 18. Participation reports

Officer shell, “Participation & Attendance Report.” Adapt the HLD's compact cards/table/breakdown treatment to the approved **per-event** report. Provide an event selector/list, selected-event summary, participant/status detail table with the completed-event no-show view, then attendee course/year breakdowns side by side on desktop. Summary values are the specified confirmed count, valid-attendee count, attendance rate, and completed-event no-show count. The prototype's cross-event dashboard totals and full Turnout by Event aggregate table are not mandatory. Preserve typography, spacing, card treatment, and numeric alignment without adding unsupported metrics to reproduce the screenshot.

Label the selected event and applied filters clearly. Totals and detail rows must describe the documented scope, and summary calculations must remain independent of detail pagination. Show **N/A** for a zero confirmed denominator; do not reproduce the prototype's 0%. Final no-show/rate reporting applies to completed, non-cancelled events; do not label unfinished attendance as final no-shows. Include required registration statuses in participant details rather than silently dropping pending/waitlisted/cancelled rows. Course/year breakdowns include actual attendees only and use signup snapshots. An empty breakdown explains that no data is available; populated results use the established chart treatment. Omit report export/print actions under D-07.

### 19. Accounts and roles

Admin shell, “Console Accounts & Roles,” search, role/status filters, and user table with identity, assigned role, account state, and actions. Retain the visual treatment of summary cards only where their values have a defined authorized source; the prototype's exact four metrics are not required. Omit the last-activity/login column until its meaning and data source are agreed (D-08). Preserve role colors, default initial squares, and admin navigation treatment (D-09). P.60 is the deactivation reason dialog. Reactivation uses the same visual family. Show the effect on upcoming registrations before applying access changes; do not promise automatic seat restoration. Last-active-admin protection requires visible explanatory feedback (D-04). Account access is separate from membership status. Omit New User under D-05 and keep login email read-only; adapting an existing-user editor does not authorize email changes.

### 20. Activity history

Admin shell, “Governance Audit Trail,” concise explanation, search, record-count context, and read-only table. Columns preserve timestamp, actor, action badge, target/context, and reason/remarks. Use monospaced timestamps and stable row spacing. Permit long reasons to wrap or be fully inspected without losing context. Do not introduce editing, credential exposure, advanced filter builders, or field-comparison views from older prototypes.

## 7. Assets and content handling

| Source | Purpose |
|---|---|
| [assets/attendTa_logo.jpg](assets/attendTa_logo.jpg) | Application branding; preserve aspect ratio and clear space |
| [assets/icons.svg](assets/icons.svg) | Existing line-icon vocabulary; use consistent stroke/scale |
| [assets/](assets/) and [images/](images/) | Existing event covers, facility imagery, pass and supporting assets; choose the exact asset referenced by the corresponding page |
| [account.css](assets/account.css) | Account menu, profile, and record-grid details |
| [dialogs.css](assets/dialogs.css) | Final centered task-dialog geometry |
| [browse-events.css](assets/browse-events.css) | Neutral member event feed treatment |
| [certificates.css](assets/certificates.css) | Review/issuance action alignment |
| [ink-seam.css](assets/ink-seam.css) and [ink-seam.js](assets/ink-seam.js) | Existing decorative signature where present; do not spread to other screens |

Use real image elements for meaningful photographs, with useful alt text; decorative icons have no redundant spoken label. Icon-only buttons need accessible names. Cover crops use the source's aspect ratio and object position. Facility thumbnails must show the correct facility, not a fallback logo. QR images and certificate content must resolve correctly from nested pages.

Final PDF screenshots outrank similarly named old captures. `screenshots/manifest.json` includes historical entries and is not the final screen inventory. The `scratch/` and `deliverables/screenshots/` captures are useful comparison aids only after matching them to the PDF. Do not depend on those temporary folders at application runtime.

## 8. Accessibility, motion, and content resilience

- Use one main heading per screen, logical section headings, real table headers, explicit form labels, and a skip-to-content link.
- All controls and dialogs work by keyboard. Selected navigation and tabs expose their state programmatically as well as visually.
- Retain visible focus. Errors include text; status never relies on color alone.
- Honor reduced motion. Current source reduces animation/transition duration and removes decorative motion; keep loading state understandable without spinning.
- Preserve text and actions at 200% zoom, with long names, long event titles, missing optional data, and large result counts.
- Use a solid backing or sufficient scrim behind photo-overlay text. The token contrast table cannot validate photo text.
- Keep essential validation and blocked-action feedback available until resolved. Restore focus sensibly after save, cancel, or a failed operation.
- Read-only, disabled, unavailable, and loading are different states and need different explanations.

## 9. Implementation handoff and visual acceptance

For each numbered screen, open the specified final PDF pages and the supplied reference repository's source together. Record its revision before comparing source-derived details. Reproduce shell and typography first, then content grouping, controls, and states. Use the application's Tailwind setup to carry the visual values; any required setup and MVC view organization stay in architecture documentation.

### Review checklist

- [ ] All 20 core screens correspond to §6; no required dialog/state was skipped.
- [ ] Desktop composition follows the PDF's navigation, content widths, hierarchy, and component treatment, with the scope/branding adaptations in D-01–D-10 applied. Unsupported controls and values are not preserved merely for screenshot fidelity.
- [ ] Correct fonts loaded; no unintended heading wraps or replacement typefaces.
- [ ] Colors, borders, radii, and spacing follow source measurements and explicit exceptions.
- [ ] Image crops, logo, icon vocabulary, QR proportions, and certificate composition match the reference.
- [ ] Default, selected, hover, focus, disabled, submitting, empty, error, and completion states are covered where relevant.
- [ ] Dialogs match the centered family, scroll internally when needed, and return keyboard focus.
- [ ] Member and officer/admin navigation remain distinct; no duplicate desktop Account menu.
- [ ] Review representative screens at 1440px desktop, 1024px breakpoint, 768px tablet, and 390px phone; also check 200% zoom. These are review viewports, not asserted HLD capture dimensions.
- [ ] No horizontal page overflow; dense table scrolling is contained; mobile actions remain reachable.
- [ ] Report totals, registration states, pass validity, and certificate actions are visually consistent with the same fixture data.
- [ ] Each discrepancy in D-01–D-10 is resolved or explicitly retained as a tracked decision before release; no unresolved item is represented as approved behavior.
- [ ] The reference repository and inspected revision are recorded, and source-derived assets/measurements have been checked there.
- [ ] MVC controllers and Razor views remain the implementation model; no PageModel migration is inferred from this visual specification.
- [ ] No prototype-only role switches, fake authentication controls, sample data dependencies, or stale institution branding reach the delivered product.

Compare application screenshots against the **application region** within the PDF, excluding slide framing. Use matching viewport, fonts, record counts, and scroll position before attributing differences to styling. Review overlays separately from their parent pages. Record deliberate deviations with screen number, reason, and approving requirement/change reference.

### Scope of this document's verification

The original v1.0 extraction records the final PDF's text and embedded screen imagery, a rendered certificate page, prototype root HTML, shared/page-specific CSS, and the supplied function list as its sources. It reports the color ratios above as calculated from source values. These provenance statements are not a fresh verification of the separate reference repository.

Version 1.1 corrected MVC terminology and reduced-MVP action boundaries. Version 1.2 links the completed working PRD/architecture, records the local visual-reference checkout, incorporates confirmed logo/institution/default-avatar decisions, and adapts reporting and certificate presentation to the agreed scope. HTML/CSS/JavaScript were inspected as source, without treating frontend documentation as authority. This revision does not claim an end-to-end browser test, remeasurement of all visual tokens/contrast, or approval of pending signatory details. The original HLD and prototype source files are unchanged.
