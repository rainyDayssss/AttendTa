---
title: 'Member self profile: bring screen 4 up to DESIGN §6'
type: 'feature'
ticket: '13'
created: '2026-11-07'
status: 'built'
baseline_revision: '57e628b8427e3c7391b86928806da0845c5d0164'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - 'docs/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The shipped My Member Profile screen (`/Profile`) is missing the compact bottom action that DESIGN §4 requires to reach the member's passes. The `/Passes` route from 4.10 exists and works, but a member has no in-page path to it; only the top navigation links there.

**Approach:** Add one compact secondary action at the bottom of `ASI.Basecode.WebApp/Views/Profile/Index.cshtml`, after the last read-only panel, linking to the existing `Passes` controller (`/Passes`). Add one small presentational class in `ASI.Basecode.WebApp/Styles/app.css` for the action row and regenerate the compiled CSS. Change nothing else: the identity strip, badge, amber notice, inactive-membership explanation and the three read-only panels stay exactly as shipped by #132/#134.

</frozen-after-approval>

## Implementation Notes

- Oneshot route: the change is one anchor plus one CSS rule, well under 100 lines, with no new service, model, route or test.
- The action is always shown on `/Profile` because the controller is Member-only and `/Passes` always renders (Active/Past cards or its empty state). Inactive membership still reaches passes and history, so the action must not imply the account is blocked.
- Use the existing `button button-secondary` classes (44px target) so keyboard focus and hover states come from the shared contract; no new button variant.
- Keep the action outside the `<section class="panel">` blocks so the existing `AssertReadOnlyProfile` integration assertion (no inputs/selects/textarea inside panels) stays true.
- `ASI.Basecode.WebApp/Styles/app.css` gets `.profile-action { display: flex; margin-top: 1rem; }`; no feature stylesheet is needed.
- Changed `ASI.Basecode.WebApp/Views/Profile/Index.cshtml` — added the `.profile-action` row after the membership panel with a secondary link to `Passes/Index`.
- Changed `ASI.Basecode.WebApp/Styles/app.css` and regenerated `ASI.Basecode.WebApp/wwwroot/css/app.css`; the compiled file contains `.profile-action`.
- Verification (2026-11-07): `dotnet build ASI.Basecode.sln` 0 warnings/0 errors; `css:build` completed; `AuthenticationFlowTests` 33/33 passed.
- Rendered smoke (2026-11-07, `DemoData__Enabled=true` against `AttendTaMVP_Test`): signed in as `member.smoke@example.com`; `/Profile` returned 200 with the identity strip, amber notice and "View my passes"; the action resolved to `/Passes/Index`; `/Passes` returned 200 with the "Door Passes" heading.
- Merge resolution (2026-11-07): merged `upstream/dev` at `4f57209` (4.11 and 6.12) into the branch. Only `ASI.Basecode.WebApp/wwwroot/css/app.css` conflicted; resolved by regenerating it with `css:build` from the merged `Styles/app.css`. Build 0 warnings/0 errors, `AuthenticationFlowTests` 33/33, and the rendered profile → passes smoke still pass.
- Review: the separate quick lens was skipped because story 4.13 explicitly waives the per-ticket code-review pass; CI and one peer approval cover the pull request.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — expected: 0 warnings, 0 errors.
- `npm --prefix ASI.Basecode.WebApp run css:build` then `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` — expected: regenerated CSS committed, no drift.
- `$env:ATTENDTA_TEST_SQL='Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True'; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter "FullyQualifiedName~AuthenticationFlowTests"` — expected: all pass, including the owner-scoped read-only profile cases.

**Manual checks (if no CLI):**
- Start with `DemoData__Enabled=true` and a member account; open `/Profile`; confirm the identity strip, badge, amber notice, inactive explanation and read-only panels are unchanged.
- Activate the bottom action: it must land on `/Passes` (no 404, no dead link). Tab to it with the keyboard and confirm visible focus.
- At 390px and 1440px the action stays inside the page with no horizontal overflow.
- Sign in as a member with inactive membership: the action still works and the explanation still says the account can sign in and reach history/certificates.
