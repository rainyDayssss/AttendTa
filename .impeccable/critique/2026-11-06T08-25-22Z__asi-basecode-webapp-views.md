---
target: Master Data epic-2 surface (Member, EventType, Venue, Profile)
total_score: 18
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 3
target_identity: "file:F:\\AttendTaBMAD\\ASI.Basecode.WebApp\\Views"
timestamp: 2026-11-06T08-25-22Z
slug: asi-basecode-webapp-views
---
# Impeccable critique — Master Data (epic 2)

Method: dual-agent (A: design review subagent · B: detector subagent)
Target: `ASI.Basecode.WebApp/Views/Member`, `Views/EventType`, `Views/Venue`, `Views/Profile`; shared `_Pager`, `_Header`, `_Layout`; `Styles/app.css`
Mode: Operate
Date: 2026-11-06

## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|-----------|-------|-----------|
| 1 | Visibility of System Status | 2 | No result count; no submitting state |
| 2 | Match System / Real World | 2 | Raw lowercase `active`; "Year" vs "Year level"; three names per screen |
| 3 | User Control and Freedom | 2 | No confirm before Deactivate; no breadcrumb; no clear-search |
| 4 | Consistency and Standards | 2 | h1 vs h2; dialog vs page; Title vs sentence case; search only on Member |
| 5 | Error Prevention | 1 | Zero required markers across 17 fields |
| 6 | Recognition Rather Than Recall | 2 | Email absent from list; photo detached from input |
| 7 | Flexibility and Efficiency | 2 | No filters, sort, count, or bulk |
| 8 | Aesthetic and Minimalist Design | 2 | Inputs stretch to ~1476px; `.field-error` burns ~200px per form |
| 9 | Error Recovery | 2 | No focus move to summary; no error count |
| 10 | Help and Documentation | 1 | One photo helper line in the epic |
| **Total** | | **18/40** | **Poor** |

## Design Specificity Verdict

LLM: a competent generic admin CRUD shell; AttendTa's own character is absent from all four folders.

Deterministic scan: `impeccable detect` silently skips `.cshtml` when given a directory (proven with a probe file). Explicit file runs over the 17 views are genuinely `[]`. `wwwroot/css/app.css` yields one true positive: `overused-font` — `.login-card` sets Plus Jakarta Sans at `Styles/app.css:54`, while DESIGN §3 mandates Outfit.

Overlays: not attempted (authenticated MVC app, not a static site).

## Strengths

1. Empty states retain table context and distinguish no-records from no-match.
2. Accessibility plumbing is above average: skip link, focus-visible, aria-describedby pairs, scope, alt text.
3. Token discipline matches DESIGN §3 verbatim.

## Priority Issues

- [P1] No required markers or `required` attributes on any field. DESIGN §5, WCAG 3.3.2.
- [P1] Input borders at 2.55:1 (#94a3b8 on white) fail WCAG 1.4.11; use #64748b.
- [P1] Editors unbounded width, no fieldset grouping.
- [P2] Profile ignores DESIGN §6.4: no badge, notice, identity strip, grouping; raw lowercase status.
- [P2] Member list omits Email; no list has a result count.
- [P2] Login card uses Plus Jakarta Sans instead of Outfit.

## Persona Red Flags

Alex: no filters/sort, email not in table, no confirm, 40 tab stops per page.
Sam: no h1 on management pages, required state unannounced, 22.5px dead error slots, 2.55:1 borders, sticky column pins Course not Student Number below 1024px.

## Minor Observations

- `<th class="actions">` inconsistent across the three tables.
- Title Case headers vs sentence case labels.
- Empty `label asp-for` on five fields.
- Save/Cancel with no footer container.
- No submitting state.
- `_Pager` aria-live on a static counter.
- `_Header` has 9 flat nav items, duplicated mobile/desktop nav that drifted, and one controller with two names.
- Correction: `data-*` row attributes are consumed by integration tests, not dead.

## Questions to Consider

- Should the editor be the one thing redesigned, given it fixes four findings at once?
- Is Plus Jakarta Sans on the login card a deliberate brand choice or drift?
- Should the DESIGN §6 canonical screen names be adopted now, or in a separate rename?
