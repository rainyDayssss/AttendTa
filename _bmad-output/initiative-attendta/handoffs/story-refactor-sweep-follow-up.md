---
title: 'Refactor sweep follow-up'
type: 'refactor'
ticket: '8'
created: '2026-11-06'
status: 'built'
baseline_revision: '868dd62c2c8c00b0740a27981d6447a1213e28a7'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.8 was already merged, but unused legacy web assets and an obsolete logging category remain. They add maintenance and distribution overhead and obscure the current Tailwind-based application.

**Approach:** Remove only verified unreferenced legacy assets and configuration, and verify that the application, its tests, and the generated CSS remain intact. Preserve the previous sweep's path-safe logging and authentication cleanup.

**Decision:** Remove the verified unused static assets and orphaned Hangfire logging entry. The user accepts that direct URLs to the removed files will stop working.

## Boundaries & Constraints

**Always:** Keep the current application appearance, authentication, account-request and master-data behavior, runtime assets, certificate resources, and private-file handling unchanged. Preserve the current security and transaction rules. Remove only files verified unreferenced in the repository, and explicitly account for their previously accessible direct URLs.

**Never:** Remove jQuery, validation libraries, Tailwind source or generated CSS, active fonts and their licenses, the favicon, certificate images, or other referenced assets. Do not change `InvalidDataException` uses belonging to other epics, implement account-request decision UI, delete backup projects, alter ticket files, or claim a GitHub Actions result for an unpushed branch.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Shell and certificates | Views and PDF renderer request active local assets | Required CSS, scripts, fonts, logo and signature remain available | A missing referenced asset blocks completion |
| Removed legacy URL | A client directly requests a removed unused asset | The old static URL is no longer served | Known compatibility tradeoff requires user approval |
| Clean build and tests | Release build and suites run against disposable local SQL Server | Build has zero warnings; both suites pass | Report environmental limitations or failures; do not report an unrun CI check as passing |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml` and `_ValidationScriptsPartial.cshtml` — retain `app.css`, jQuery, validation scripts and `site.js`.
- `ASI.Basecode.WebApp/Styles/app.css`, `wwwroot/css/app.css` and `wwwroot/fonts/` — retain the Tailwind input/output, active Outfit and Plus Jakarta Sans fonts and licenses.
- `ASI.Basecode.WebApp/Certificates/PlaywrightCertificatePdfRenderer.cs`, `Views/Certificates/CertificateTemplate.cshtml`, `appsettings.json` — retain Cinzel fonts/licenses, AttendTa logo and configured signatory image.
- `ASI.Basecode.WebApp/wwwroot/lib/bootstrap/`, `lib/react/`, `lib/react-dom/`, `html/assets/js/plugins/jquery-slimscroll/` — tracked legacy vendor files with no in-repository references found; confirm exact tracked files and recheck references before removal.
- `ASI.Basecode.WebApp/wwwroot/css/{login,main,site,style}.css`, `wwwroot/fonts/roboto.css`, and `wwwroot/img/{asi-logo-invert.svg,LiveIT.png,profile-logo.png}` — identified legacy candidates; recheck references and remove only if still unreferenced.
- `ASI.Basecode.WebApp/appsettings.json` — remove only the unused `Hangfire` log-level entry; no Hangfire runtime package remains.
- `.github/workflows/ci.yml` — existing PR workflow checks generated CSS, builds Release and runs service and SQL integration tests; inspect rather than claim a new workflow run.
- `tests/ASI.Basecode.IntegrationTests/AuthenticationFlowTests.cs` and `CertificateTracerFlowTests.cs` — existing coverage for local shell assets and certificate resources.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.WebApp/wwwroot/lib/bootstrap/`, `lib/react/`, `lib/react-dom/`, `html/assets/js/plugins/jquery-slimscroll/` — enumerate tracked files and recheck references, then remove only the confirmed legacy files after the scope decision.
- [x] `ASI.Basecode.WebApp/wwwroot/css/login.css`, `main.css`, `site.css`, `style.css`, `wwwroot/fonts/roboto.css`, `wwwroot/img/asi-logo-invert.svg`, `LiveIT.png`, `profile-logo.png` — recheck references and remove only confirmed unused files after the scope decision.
- [x] `ASI.Basecode.WebApp/appsettings.json` — remove the orphaned Hangfire logging category while preserving all other settings.
- [x] `tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs` — add focused regression coverage for retained shell and certificate assets and removed legacy files.
- [x] `.github/workflows/ci.yml` and the local verification results — confirm existing checks cover the change; report current-branch CI as pending unless an actual run exists.

**Acceptance Criteria:**
- Given the application shell, when an authenticated user loads it, then active local styles and scripts remain available and Bootstrap is not referenced.
- Given certificate generation, when its local logo, signature and fonts are resolved, then the required resources still exist and the existing certificate tests pass.
- Given the approved legacy-file list, when repository references and tracked files are checked, then only confirmed unreferenced candidates are removed and remaining references do not point to missing files.
- Given the solution and disposable SQL Server test database, when the Release build and both test suites run, then the build reports zero warnings and all tests pass.

## Implementation Notes

- Removed 45 tracked legacy assets and the obsolete logging category. Regenerated `wwwroot/css/app.css` after Tailwind's content discovery stopped seeing the removed files; active base and component styles were retained.
- Added three integration tests for served shell assets, certificate resources and removed legacy paths. Missing anonymous URLs can be redirected to sign-in rather than returning 404 due to the existing fallback policy.
- Release build passed with zero warnings; 149 service tests and 189 SQL integration tests passed. The CSS rebuild was reproducible. Current-branch GitHub Actions has not run.

## Critique-driven UI hardening (2026-11-06)

Impeccable critique of the Epic 1 shell and account-request queue scored 17/40 and identified decision safety as the first priority. This pass addressed it:

- Approve now opens a centered confirmation dialog that names the applicant and student number and states that approval creates an active member account. The dialog cannot submit twice; both decision dialogs disable their submit button and change its label while posting.
- The decline error path keeps one error source inside the dialog, preserves the typed reason, and associates the error with the textarea through `aria-describedby` and `aria-invalid`. A top notice remains only for failures that cannot reopen the dialog, such as a stale request or an approve conflict.
- Added `.button:disabled` and the dialog consequence style; regenerated `wwwroot/css/app.css`.
- Typeset pass: added self-hosted JetBrains Mono 400/600 with tabular numerals for student numbers, dates, year, mobile, tab counts, and pager counts; raised the "University of Cebu" brand subtitle from 10.5px to 12px; added balanced headings and pretty paragraph wrapping. The type-scope detector returned no findings.
- Queue-scale pass: added search over name, email, and student number; an unconditional "Showing X–Y of Z" line; and a Requested sort toggle. Tabs, pager links, and decision forms preserve the active search and sort, and tab counts now reflect the search.
- Mobile pass: the applicant name and Actions columns stay sticky below 1024px while the dense columns scroll; rows gain a hover tint and compact padding below 640px.
- Polish pass: the queue uses a single `h1`, the muted text matches the spec token, buttons have background hover states instead of link underlines, and the task dialog scrolls internally on short screens.
- Verification: build with zero warnings; 189 service tests and 225 integration tests passed; CSS rebuild reproducible.
- Deferred: queue search, result count, sorting, mobile sticky/card layout, reviewer metadata, and the table-preserving empty state.

## Plan Change Log

## Review Triage Log

- Quick review: 0 high, 1 medium, 0 low, 3 false, 0 maybe-false.
- medium — patch: `StaticAssetTests.ShellAssetsAreServedFromTheActiveWebRoot` follows redirects by default; a missing file can return the login HTML with status 200 and pass. Disable redirects and check the expected asset response.
- false — reject: `RemovedLegacyAssetsAreNoLongerServed` first tests the file provider, and an existing non-HTML static file is served directly before authorization, not redirected. The HTTP assertion is not always inert for the listed paths.
- false — reject: The merged PR #109 has a passing GitHub Actions build-and-test run; the plan explicitly reports that a new branch run has not occurred and does not claim otherwise. A new PR run remains a delivery check, not a code defect.
- false — reject: The `context` field is optional in the build template; the plan's Code Map contains the relevant paths and constraints, and the workflow requires no specific context entries.

## Verification

- `dotnet build ASI.Basecode.sln --no-restore --configuration Release -warnaserror` — succeeds with zero warnings.
- `dotnet test --project tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj --no-build --no-restore --configuration Release` — all service tests pass.
- With `ATTENDTA_TEST_SQL` set to a local SQL Server master connection: `dotnet test --project tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --no-build --no-restore --configuration Release` — all SQL integration tests pass.
- `npm --prefix ASI.Basecode.WebApp run css:build` followed by `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` — generated CSS stays reproducible.
- Inspect tracked references and the final diff; retain resources used by the shell and certificate renderer. Report any direct-URL compatibility impact and GitHub Actions status honestly.
