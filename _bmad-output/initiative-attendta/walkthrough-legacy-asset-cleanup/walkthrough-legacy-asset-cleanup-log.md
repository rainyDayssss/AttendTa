# Review log: legacy web asset cleanup

Target: commit `6d377ec283fb972ad0f604cc4f45a9df481f736b` in F:\AttendTaBMAD

## 1 — Preparation — narrative and log created

Session: unavailable · Timestamp: 2026-11-06T13:55:12+08:00

- Action: Read the walkthrough steps and log template, the story 1.8 ticket, and the approved follow-up plan; inspected commit `6d377ec` (45 deletions, `appsettings.json`, regenerated `app.css`, new `StaticAssetTests.cs`, plan file).
- Result: Scope confirmed as the refactor-sweep follow-up. Narrative drafted with 8 blocks: intent, broad strokes, five slices, periphery. No block visited, accepted, or marked complete.
- Evidence: [narrative](walkthrough-legacy-asset-cleanup.md), [plan](../tickets/epic-platform-access/tickets/story-refactor-sweep-plan.md).
- Open: Block 1 awaits the reviewer.

## 2 — Removed assets — reference sweep

Session: unavailable · Timestamp: 2026-11-06T13:55:39+08:00

- Action: Searched the repository (excluding `bin`, `obj`, `.git`, `node_modules`) for Bootstrap, React, slimScroll, the four legacy CSS names, `roboto.css`, and the three legacy images.
- Result: No live references. Hits are limited to the plan and the new test's removal list. The shell loads only `app.css`, jQuery, `site.js`, and the validation partial.
- Evidence: [_Layout.cshtml:26](../../../ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml#L26), [_Layout.cshtml:45](../../../ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml#L45), [_ValidationScriptsPartial.cshtml:1](../../../ASI.Basecode.WebApp/Views/Shared/_ValidationScriptsPartial.cshtml#L1), [StaticAssetTests.cs:37](../../../tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs#L37).
- Open: none.

## 3 — Generated CSS — utility pruning

Session: unavailable · Timestamp: 2026-11-06T13:55:39+08:00

- Action: Diffed the pre- and post-commit `app.css`, extracted the class names unique to the old file, and matched them against `Views/**/*.cshtml` and `wwwroot/js/site.js`.
- Result: 137 utilities dropped, all Bootstrap/React-shaped. No view or script uses a dropped class; base and component layers are unchanged. No finding.
- Evidence: [app.css](../../../ASI.Basecode.WebApp/wwwroot/css/app.css), [Styles/app.css:2](../../../ASI.Basecode.WebApp/Styles/app.css#L2).
- Open: none.

## 4 — Static asset tests — redirect handling

Session: unavailable · Timestamp: 2026-11-06T13:55:39+08:00

- Action: Inspected the new test class against the plan's one medium triage item.
- Result: The shell test sets `AllowAutoRedirect = false` and rejects `text/html`; the retired-path test checks the file provider and rejects a non-HTML 200. The medium item is addressed in this commit.
- Evidence: [StaticAssetTests.cs:55](../../../tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs#L55), [StaticAssetTests.cs:86](../../../tests/ASI.Basecode.IntegrationTests/StaticAssetTests.cs#L86).
- Open: Not executed in this review session.

## 5 — Environment — build, tests, CI state

Session: unavailable · Timestamp: 2026-11-06T13:55:39+08:00

- Action: Checked git state and the CI workflow.
- Result: HEAD equals the target, tree clean, branch `feature/1.8-refactor-sweep`. CI rebuilds CSS with a drift check, builds Release, and runs both suites on PRs; no run for this branch was observed.
- Evidence: [.github/workflows/ci.yml](../../../.github/workflows/ci.yml).
- Open: Current-branch CI pending; no local build or test run performed by this writer.

## 6 — Block 1 / public shell — Demo

Session: unavailable · Timestamp: 2026-11-06T13:59:25+08:00

- Action: Started the app at `http://127.0.0.1:5188` against the configured disposable local database and an isolated temp setup root; loaded the public sign-in page, then the public Request membership form in the shared browser.
- Result: Both public pages rendered with the active shell and stylesheet. No credentials entered, no forms submitted, and no signed-in pages checked by the assistant.
- Evidence: [demo-membership-request.png](demo-membership-request.png).
- Open: Signed-in shell and certificate assets not observed in this demo.

## 7 — Block 1 / environment — Drive

Session: unavailable · Timestamp: 2026-11-06T14:04:33+08:00

- Action: DB-backed pages at `http://127.0.0.1:5188` failed with SQL error 4060 because `AttendTaMVP_Test` did not exist. For the walkthrough only: created that disposable LocalDB database, applied [attendta-mvp-schema.sql](../../../database/attendta-mvp-schema.sql), seeded one Administrator (`walkthrough.admin@attendta.test`) via `--hash-password` and [seed-local-admin.sql](../../../database/seed-local-admin.sql), then ran [seed-event-types.sql](../../../database/seed-event-types.sql).
- Result: Sign-in page renders. No repository files or app code changed. The demo credential is local and disposable and must not be committed.
- Evidence: [database setup guide](../../../docs/database-setup.md).
- Open: DB-backed signed-in pages still unobserved.
