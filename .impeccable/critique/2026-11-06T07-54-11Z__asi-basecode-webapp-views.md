---
target: Master Data management views (Member, EventType, Venue)
total_score: 20
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 3
target_identity: "file:F:\\AttendTaBMAD\\ASI.Basecode.WebApp\\Views"
superseded_by: "2026-11-06T08-25-22Z__asi-basecode-webapp-views.md"
timestamp: 2026-11-06T07-54-11Z
slug: asi-basecode-webapp-views
---
# Impeccable critique — Master Data management views

Method: dual-agent (A: design review subagent · B: deterministic detector, CLI)
Target: `ASI.Basecode.WebApp/Views/Member`, `Views/EventType`, `Views/Venue` (shared shell: `Views/Shared/_Pager.cshtml`, `_Layout.cshtml`; tokens: `Styles/app.css`)
Mode: Operate
Date: 2026-11-06

## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|-----------|-------|-----------|
| 1 | Visibility of System Status | 2 | No record count; `_Pager` renders nothing at ≤1 page |
| 2 | Match System / Real World | 3 | Scaffold headings instead of DESIGN §6 campus vocabulary |
| 3 | User Control and Freedom | 2 | One-click deactivate, no confirm/undo; no clear-search |
| 4 | Consistency and Standards | 2 | Diverges from `AccountRequest/Index.cshtml` on badges, `.numeric`, `.row-actions`, `.result-count` |
| 5 | Error Prevention | 2 | Required fields unmarked; destructive toggle unguarded |
| 6 | Recognition Rather Than Recall | 2 | Edit form omits current membership status; Venue Edit claimed a photo it did not show |
| 7 | Flexibility and Efficiency | 2 | No sorting, page jump, or status filter |
| 8 | Aesthetic and Minimalist Design | 2 | Clean but uncomposed; full-bleed forms |
| 9 | Error Recovery | 2 | `ModelOnly` summary stays empty for field-level errors |
| 10 | Help and Documentation | 1 | Only the venue photo has helper text |
| **Total** | | **20/40** | **Acceptable** |

## Design Specificity Verdict

**LLM assessment:** Structurally AttendTa, compositionally generic. The shared shell, Outfit type, amber focus ring, and token palette are inherited, but nothing inside these three screens is authored for the domain. They are interchangeable with any ASP.NET CRUD admin.

**Deterministic scan:** `impeccable detect --json` over the three view folders returned `[]` — no findings.

## Strengths

1. Genuinely restrained: one accent, thin borders, 44px targets, no card bloat.
2. Accessibility groundwork is real: skip link, `role="region"` + `tabindex="0"` scroll containers, `scope="col"` headers, `aria-describedby`, `aria-current`, `role="alert"`, visible focus.
3. The toggle is honest about the domain: no hard deletes, POST + antiforgery, label reflects current state.

## Priority Issues

- **[P1] No record count and no pagination on single-page results.** `_Pager.cshtml:28` is gated on `TotalPages > 1`; no Master Data view uses `.result-count`. Operators cannot distinguish zero records from a filter that matched none.
- **[P1] Validation failures are silent to assistive technology.** All five create/edit views use `asp-validation-summary="ModelOnly"`, but controllers return field-level ModelState errors, which `ModelOnly` excludes.
- **[P1] Forms have no surface, no width constraint, and mark nothing required.** `.management-header ~ .main-content` is 1536px wide; four `[Required]` member fields carry no marker.
- **[P2] Status is plain text and row actions are full-size buttons.** Bare "Active"/"Inactive"; `.button` at 44px inflates rows; `EventType/Index.cshtml:34` had no `actions` class.
- **[P2] Venue Edit did not show the current photo** while telling the operator a photo was stored.

## Persona Red Flags

**Alex (power user):** no sortable headers; Previous/Next only; Event Type toggle dropped the page; no status filter.

**Sam (accessibility/keyboard):** field errors never announced; `.field-error` reserves 22px of dead space under every field; the sticky third column pins Course on Member, not identity; `_Pager` omits rather than disables buttons so positions shift.

## Minor Observations

1. Three different empty-state shapes across the three lists.
2. `aria-label="EventType table"` leaked the class name into a spoken label.
3. Action-cell classes inconsistent; no `.row-actions` wrapper.
4. Double blank line between `<h2>` and `<form>` in all five create/edit views.
5. Indentation drift in `Venue/Index.cshtml`.
6. Empty `<label asp-for>` with no `[Display]` on Event Type/Venue view models.
7. No `<h1>` on any of the eleven views.
8. No `<caption>` on any Master Data table.
9. Numeric columns not marked `.numeric`.
10. `_Pager.cshtml:30` reuses `page-heading` for a `<nav>` and puts `aria-live` on a static span.
11. Topbar title and page heading use different names for the same screen.
12. Unbounded `AvailabilityNotes` free text in a dense table.
13. Search has no visible label and no clear affordance.

## Questions to Consider

- Should the identity column, not the third column, be the one that stays pinned?
- Does an Officer ever need to see more than 25 rows without filtering?
- What would a confident version of these three screens look like if the campus vocabulary in DESIGN §6 were used in the headings?

## Scope note

This critique informed BMAD 2.9, a low-risk refactor sweep. Only the consistency and artifact findings were actioned (badges, `.numeric`, `.row-actions`, `button-small`, empty-state shape, aria-label, blank lines, indentation, current photo). The P1 items and the sticky-column defect are new scope or app-wide decisions and were deferred.
