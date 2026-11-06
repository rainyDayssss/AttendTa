# Review narrative: legacy web asset cleanup

Target: commit `6d377ec283fb972ad0f604cc4f45a9df481f736b` — *refactor: clean up legacy web assets*

Ticket: [story-refactor-sweep.md](../tickets/epic-platform-access/tickets/story-refactor-sweep.md) · Plan: [story-refactor-sweep-plan.md](../tickets/epic-platform-access/tickets/story-refactor-sweep-plan.md)

Review status: prepared, no block visited yet. **Block 1 is the current block.** No block has changed during review.

---

## Block 1 — Intent

- [ ] Not yet visited · current block · unchanged during review

**Intent (ticket):** "Cleans up technical debt, obsolete stubs, and temporary scaffolds across auth, schema, and UI, with scope set at its start from plans and review findings the epic deferred."

Source: the Description of [story-refactor-sweep.md](../tickets/epic-platform-access/tickets/story-refactor-sweep.md#L18).

**Follow-up scope:** Story 1.8 was already merged, but unused legacy web assets and an obsolete logging category remain. They add maintenance and distribution overhead and obscure the current Tailwind-based application. The approved plan narrows this instance to removing only verified unreferenced legacy assets and configuration, verifying that the application, its tests, and the generated CSS remain intact, and preserving the previous sweep's path-safe logging and authentication cleanup. The user accepts that direct URLs to the removed files stop working.

Source for the follow-up scope: the frozen Intent section of the [approved plan](../tickets/epic-platform-access/tickets/story-refactor-sweep-plan.md#L20).

---

## Block 2 — Broad strokes

- [ ] Not yet visited · unchanged during review

The commit deletes 45 tracked files under the web root, drops one logging category, regenerates the Tailwind output, and adds a test class that pins what must stay and what must stay gone. Four places carry the change:

1. [ASI.Basecode.WebApp/wwwroot/lib](../../../ASI.Basecode.WebApp/wwwroot/lib) — the vendor directory now holds only jQuery and the two validation libraries; the Bootstrap, React, ReactDOM and slimScroll trees are gone.
2. [ASI.Basecode.WebApp/wwwroot/css/app.css](../../../ASI.Basecode.WebApp/wwwroot/css/app.css) — the compiled Tailwind file, rebuilt after the deletions changed what Tailwind scans.
3. [ASI.Basecode.WebApp/appsettings.json:13](../../../ASI.Basecode.WebApp/appsettings.json#L13) — the `Logging` section, now without the Hangfire category.
4. [tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs](../../../tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs) — a new guard for retained shell assets, certificate assets, and retired paths.

---

## Block 3 — Slice: vendor bundles removed

- [ ] Not yet visited · unchanged during review

The shell loads exactly four local scripts and one stylesheet: `app.css`, jQuery, `site.js`, and the validation partial. Nothing under the removed vendor trees is referenced by a view, script, or stylesheet, so the trees were deleted wholesale rather than trimmed.

The largest group is the Bootstrap distribution — 21 files: `LICENSE`, twelve CSS files (the full, grid, and reboot builds plus their minified and map variants) and eight JavaScript files (`bootstrap.js`/`.bundle.js` and their minified and map variants). React contributed three files (`react.development.js`, `react.production.min.js`, `react.profiling.min.js`), ReactDOM eleven, and the slimScroll plugin two (`jquery.slimscroll.js` and its minified twin). The [test's `RemovedLegacyPaths` array](../../../tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs#L37) samples thirteen of those retired paths rather than listing the full deletion set; the [plan's Code Map](../tickets/epic-platform-access/tickets/story-refactor-sweep-plan.md#L49) names the candidate groups.

What still loads is visible in [\_Layout.cshtml:26](../../../ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml#L26) and [\_Layout.cshtml:45](../../../ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml#L45), and the validation scripts in [\_ValidationScriptsPartial.cshtml:1](../../../ASI.Basecode.WebApp/Views/Shared/_ValidationScriptsPartial.cshtml#L1).

---

## Block 4 — Slice: legacy theme CSS, font stylesheet, and images

- [ ] Not yet visited · unchanged during review

The old theme layer — `login.css`, `main.css`, `site.css`, `style.css`, `fonts/roboto.css`, and the images `asi-logo-invert.svg`, `LiveIT.png`, and `profile-logo.png` — was likewise unreferenced. The active shell styles live in the compiled `app.css`, the brand mark is `attendta-logo.jpg`, and the certificate renderer reads its logo, signature, and Cinzel fonts from the web root by relative path.

The two largest deletions are `style.css` (364 lines) and `login.css` (29 lines). The retained counterparts are the brand mark in [\_Header.cshtml:14](../../../ASI.Basecode.WebApp/Views/Shared/_Header.cshtml#L14) and the certificate asset reads in [PlaywrightCertificatePdfRenderer.cs:102](../../../ASI.Basecode.WebApp/Certificates/PlaywrightCertificatePdfRenderer.cs#L102). The plan records the accepted tradeoff that direct URLs to these files stop working.

---

## Block 5 — Slice: Tailwind output regenerated

- [ ] Not yet visited · unchanged during review

Tailwind v4's automatic content detection had been scanning the legacy vendor files, so deleting them changed the compiled output. The rebuilt [wwwroot/css/app.css](../../../ASI.Basecode.WebApp/wwwroot/css/app.css) drops 137 utility classes that only those files had caused to be emitted — Bootstrap-shaped helpers such as `col-*`, `order-*`, `p-*`/`px-*`/`py-*`, `flex-*`, `shadow*`, `sr-only`, and `text-center`. The base and component layers, the font faces, and the utilities the views and `site.js` use are unchanged.

The input side is [Styles/app.css:2](../../../ASI.Basecode.WebApp/Styles/app.css#L2), which explicitly sources `Views/**/*.cshtml` and `wwwroot/js/**/*.js` on top of automatic detection. The commit is a one-line change in the generated file, so the diff is best read as a before/after class inventory.

---

## Block 6 — Slice: Hangfire logging category removed

- [ ] Not yet visited · unchanged during review

The `Logging:LogLevel` object loses its `"Hangfire": "Information"` entry and keeps every other level, including the `Microsoft.EntityFrameworkCore` debug level. No Hangfire package or runtime reference remains in the application project, so the category was orphaned configuration.

The resulting section is at [appsettings.json:13](../../../ASI.Basecode.WebApp/appsettings.json#L13).

---

## Block 7 — Slice: static asset regression tests

- [ ] Not yet visited · unchanged during review

A new xUnit class, [StaticAssetTests.cs](../../../tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs), runs against the existing SQL Server fixture and the `AttendTaWebApplicationFactory`. It holds three checks:

- **Shell assets** — nine paths (`app.css`, `site.js`, jQuery, both validation scripts, the logo, both web fonts, the favicon) must return 200 with a non-HTML content type and a non-empty body. The client is built with `AllowAutoRedirect = false` so a sign-in redirect cannot masquerade as a served asset.
- **Certificate assets** — five relative paths (logo, signature, two Cinzel fonts, the OFL license) must exist in `IWebHostEnvironment.WebRootFileProvider`.
- **Retired paths** — thirteen former URLs must be absent from the file provider and must not come back as a non-HTML 200; the comment notes that anonymous requests can still land on the HTML sign-in page through the fallback policy.

The three entry points are [line 55](../../../tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs#L55), [line 74](../../../tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs#L74), and [line 86](../../../tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs#L86).

---

## Block 8 — Periphery

- [ ] Not yet visited · unchanged during review

- [story-refactor-sweep-plan.md](../tickets/epic-platform-access/tickets/story-refactor-sweep-plan.md) — the approved plan is committed alongside the change, including its Code Map, acceptance criteria, and review triage log.
- [.github/workflows/ci.yml](../../../.github/workflows/ci.yml) — unchanged; it already rebuilds the CSS and fails on drift, builds Release, and runs both test suites against SQL Server.
- The ticket file is untouched, and no application code outside the web root, `appsettings.json`, and the new test file changed.
