# AttendTa — sequential waves and shared unblockers

> Superseded 2026-11-06 by the demo rescope in [delivery-queue.md](delivery-queue.md): 1.12 became the closing demo rehearsal 8.15, 1.13 starts first, 5.11 no longer waits on 2.13, and 2.11 moved to the finale. Use the queue, not these waves, to pick work.

Verified snapshot: **6 November 2026**, after planning commit `45d1c20`.

**Start with [#135][i135] → [#136][i136].** Then use the five-builder batches below.
The biggest opportunity is to finish a shared provider once, merge its contract, and let several
builders consume it instead of independently rebuilding the same screen, model or service.

## 1. What has actually been verified?

| Check | Result |
|---|---|
| Local BMAD tree | 97 tickets: 40 done, **57 remaining**. |
| Live GitHub issue enumeration | All **57** remaining issues are open, unassigned backlog items in milestone 1. |
| Remaining delivery phases | **18 frontend, 23 integration, 16 finale**. |
| Ticket dependencies | All 57 native GitHub blocker sets match local `after` prerequisites. |
| Structure | No missing tracker IDs, dependency cycles, drift, undeclared prerequisites or order conflicts. |
| Product scope | Existing R1–R36 and the 26 functional IDs remain required; completed work is not repeated. |
| Implementation evidence | This verifies the **plan**, not working screens, passing feature tests or final acceptance. |

Counts come from individual issue records, not GitHub's inconsistent milestone summary counter.
The 40 completed tickets are treated as satisfied prerequisites and are **not** scheduled again.
Old `in-progress`/`in-review` text in completed build plans is historical, not a new active claim.

Sources: [initiative](initiative-attendta.md), [ranked delivery queue](delivery-queue.md), the eight
`tickets/tickets.toml` breakdowns, the local ticket-status report and live GitHub blockers.
This guide is a **derived execution view**: it changes no issue scope, queue rank or dependency.
Recheck live claims/blockers before starting; skip work that has since merged.

## 2. Shared tickets worth finishing first

**Direct consumer** means a ticket names this provider as a prerequisite. It does **not** mean
the consumer is ready: every other prerequisite must also be complete.

| Finish this shared provider | Direct remaining consumers | What becomes usable / ready |
|---|---|---|
| **[#136][i136] — 1.13: shell, routes, typed contracts and isolated fixture mechanism** | #137, #138, #139, #140, #141, #142, #127, #143, #149 | **Eight UI tasks are ready:** all listed except #149, which still needs the Member editor #147. Start the first five by queue rank; do not launch nine builders. |
| **[#137][i137] — 3.17: My Events host and extension slots** | #144, #145, #146, #42, #43 | **Three UI partials plus one integration:** pass cards #144, attendance history #145, certificate history #146, and the real registrations query #42. #43 is deferred cleanup and has other blockers. |
| **[#143][i143] — 2.12: shared centered dialog + Event Type tracer** | #147, #148 | **Two independent editors:** Member #147 and Venue #148. After #147, access dialogs #149 can proceed; #128 accepts the whole editor scope only after **both** #147 and #148. |
| **[#41][i41] — 3.12: event completion and completion hook** | #45, #64, #72, #43 | Opens the check-in window command #45 and certificate-review creation #64 when their UI contracts are ready. Report calculations #72 also need #71. In the recommended schedule, all three integrations can start after Wave 6. #43 remains finale cleanup. |
| **[#46][i46] — 4.3: one shared check-in validation/recording engine** | #47, #48, #49, #51, #77 | **Two adapters can start:** camera #47 and manual lookup #48. Invalidation #49 still needs #48; do not call it instantly unblocked. Cleanup #51 and final tests #77 wait for their other providers. |
| **[#49][i49] — 4.6: attendance invalidation and invalidation hook** | #50, #65, #73, #51 | **Three integrations can start together:** attendance history #50, certificate invalidation guard #65, and real report details #73—once their existing pass/query/review/report prerequisites are complete. This is Wave 11 below. #51 still waits for #50. |
| **[#64][i64] — 6.4: completion creates certificate reviews** | #65, #66, #70 | Real review decisions #66 can start with the ledger #139 already present. #65 also needs #49; #70 is the final certificate cleanup. |

### The clearest “one ticket unlocks three” examples

```text
#137 My Events host
  ├─ #144 pass-card partial
  ├─ #145 attendance-history partial
  ├─ #146 certificate-history partial
  └─ #42  real registrations query

#49 attendance invalidation + hook
  ├─ #50 real attendance history       (also needs #44, #42 and #145)
  ├─ #65 certificate invalidation guard (also needs #64)
  └─ #73 real report detail rows       (also needs #72)
```

The first is a **shared frontend host**. The second is a **shared business-rule/hook handoff**.
Do not build three different hosts or three different invalidation rules.

## 3. How to use the waves

- **No fixed people or module lanes.** Any available builder claims the next unclaimed, ready
  ticket. One active implementation per builder; at most five implementations across all waves.
- A row is a **dispatch batch**, not a hard barrier. Once a provider is merged and its checks
  pass, its consumers may start even if unrelated work from that row is still running.
- Prefer ready **frontend** work by its existing queue rank. Fill spare capacity with ready
  **integration** work; that is why Waves 5–6 include both. Do not delay an easy real integration
  just to keep using a fixture.
- **#150 may start alongside #135.** It prepares the real host and human inputs. A blocked host
  decision must not stop #136 or the UI; park the blocked claim rather than occupy a coding slot.
- Hold the four module cleanup sweeps until the integration checkpoint. This avoids #43
  cleaning event interfaces while #45 is still extending them.
- Run phone #75 and restore #76 verification after pre-acceptance cleanup #83, alongside final
  case authoring. Their GitHub dependencies allow earlier work; this is a deliberate **execution
  recommendation**, not a new blocker. Prepare their inputs early through #150 to avoid waiting.
- Fewer than five safe tasks can be ready. Review/merge the provider, resolve a blocker or
  prepare its documented inputs; do not invent extra scope or race on shared files to look busy.

**Legend:** F = frontend phase; I = real integration; H = human/environment participation.
Integration tickets #47/#48 are UI-facing too. Frontend/UI-facing implementation, critique and
polish use **Impeccable**; backend-only, test-only and operations work do not.

## 4. Recommended five-builder sequence

Every remaining ticket appears **exactly once in this table**. Tickets within a row have no
dependency on each other; all prerequisites are in an earlier row or the completed baseline.
Parallel work still depends on the source boundaries in section 5.

| Wave | Tickets to take, in queue order | Result / next handoff |
|---:|---|---|
| 1 | [#135][i135] (1.12) frontend walkthrough; [#150][i150] (8.14, H) host readiness/preparation | Walkthrough findings unlock #136. Host inputs are a parallel side path, not a UI gate. |
| 2 | [#136][i136] (1.13) shared shell/routes/contracts | Merge the shared boundary map: eight UI tasks become ready. |
| 3 | [#137][i137] (3.17) My Events host; [#138][i138] (4.11) scanner/manual UI; [#139][i139] (6.12) certificate ledger/viewer; [#140][i140] (7.4) report UI; [#141][i141] (3.16) officer dossier/roster | First five independent surfaces. The My Events host unlocks its three partials and real query. |
| 4 | [#142][i142] (3.18) member browse/details; [#127][i127] (2.10) residual list states; [#143][i143] (2.12) shared dialog/Event Type tracer; [#144][i144] (4.10) pass cards; [#145][i145] (4.12) attendance partial | Reusable dialog unlocks two editors; pass route unlocks the remaining profile action. |
| 5 | [#146][i146] (6.13) certificate-history partial; [#147][i147] (2.13) Member dialog; [#148][i148] (2.14) Venue dialog; [#129][i129] (4.13) profile-to-passes action; [#42][i42] (3.13) real registrations query | Four ready frontend tickets first; use the fifth slot to make My Events real. |
| 6 | [#149][i149] (5.11) access/status dialogs; [#128][i128] (2.11) whole-editor acceptance; [#41][i41] (3.12) completion/hook; [#44][i44] (4.1, H) real QR PNG; [#71][i71] (7.1) real report summaries | Initial frontend is complete. Completion plus real summaries release three integration paths. |
| 7 | [#45][i45] (4.2) check-in window; [#55][i55] (5.4) account deactivation; [#64][i64] (6.4) certificate-review creation; [#72][i72] (7.2) no-shows/distributions | Establish recording, access, review and report providers. |
| 8 | [#46][i46] (4.3) shared attendance engine; [#56][i56] (5.5) account reactivation; [#66][i66] (6.6) real certificate review decisions | Attendance engine unlocks the independent camera/manual adapters. |
| 9 | [#57][i57] (5.6) membership deactivation; [#47][i47] (4.4, H) real camera adapter; [#48][i48] (4.5) real manual adapter | Camera and manual share validation, not one editable adapter file. Manual delivery unlocks #49. |
| 10 | [#58][i58] (5.7) membership reactivation; [#49][i49] (4.6) invalidation/hook | Finish the shared invalidation provider before the next three integrations. |
| 11 | [#50][i50] (4.7) real attendance history; [#65][i65] (6.5) certificate invalidation guard; [#73][i73] (7.3) real report details | **One provider has released three tickets.** The certificate guard is mandatory before issuance. |
| 12 | [#67][i67] (6.7) retained transactional certificate issuance | Render/store/commit/audit safely; never replace an issued PDF. |
| 13 | [#68][i68] (6.8) authorized retained-PDF streaming | Real owner/management downloads replace the sample-document path. |
| 14 | [#69][i69] (6.9) real member certificate links | **Integration checkpoint:** every integration-phase ticket is complete; continue to focused hardening. |
| 15 | [#43][i43] (3.14) event/backend cleanup; [#51][i51] (4.9) attendance/backend cleanup; [#60][i60] (5.10) administration/backend cleanup; [#70][i70] (6.11) certificate/backend cleanup | Four focused module sweeps, not four repeated whole-app UI audits. |
| 16 | [#151][i151] (8.13) cross-module backend hardening | Stabilize transaction, hook, authorization, fact and storage integration. |
| 17 | [#152][i152] (8.11) mock removal and real integration gate | No required runtime fixture provider, simulated mutation or silent fallback remains. |
| 18 | [#153][i153] (8.12) final frontend walkthrough/refactor/polish | Critique and polish the real integrated frontend, not a mock-only build. |
| 19 | [#83][i83] (8.10) pre-acceptance release cleanup | Finish planned code/config cleanup before final test evidence. |
| 20 | [#78][i78] (8.5) important automated cases/coverage map; [#75][i75] (8.2, H) trusted-HTTPS phone verification; [#76][i76] (8.3, H) backup/restore/redeploy verification | Add only missing important automation; obtain actual phone/storage evidence on the stable build. |
| 21 | [#77][i77] (8.4) combined automated execution | One recorded service/SQL/PDF/JS/CSS run; reuse existing coverage. |
| 22 | [#79][i79] (8.6, H) full manual SPEC acceptance | Cover the mapped manual remainder; reuse automated, UI and device evidence. |
| 23 | [#80][i80] (8.7, H) complete functional demo | Demonstrate all 26 functional IDs plus FR-35/FR-36, without mock or SQL workarounds. |
| 24 | [#81][i81] (8.8, H) Tier 3 human sign-off | Confirm the exact tested commit; retest affected cases after fixes. |
| 25 | [#82][i82] (8.9, H) release PR and human merge | **Release last**, with required peer approval and green CI. |

**Capacity check:** 57 unique tickets; no batch exceeds five. The first 18 frontend-phase
tickets are complete by Wave 6, with ready real-backend work already running alongside them.
The remaining 23 integration-phase and 16 finale-phase tickets are all included.

### If only one builder is available

Read the table **top to bottom, left to right**. That is a valid sequential order too.
If #150 is waiting on human/environment inputs, park it and proceed along the UI path;
only #75/#76 and the eventual acceptance that consumes their evidence must wait for it.

## 5. What each shared-provider handoff must contain

Do not mark a shared ticket complete with “the next builder can decide the interface.” Supply:

1. Exact route/policy, request/response or view-model contract, supported states and errors.
2. Actual file/partial/adapter boundaries and the files consumers must **not** all edit.
3. Which sources are already real, which are isolated Development-only fixtures, and the real
   integration issue for each temporary source. No real writes or fake success from fixtures.
4. A merged, checked provider and a short runnable/rendered example—not a new exhaustive suite.
5. Explicit consumer issue links and known remaining blockers.

| Shared surface | Boundary that prevents duplicate/conflicting work |
|---|---|
| #136 shared setup | Own shell/navigation, route skeletons, provider/DI seams and shared JS/CSS setup. Establish the source map **before** consumers implement. |
| #137 My Events host | Own `MyEventsController`, the host/card model and extension slots. #144/#145/#146 own separate partials/contracts; #42 fills the real query, not a second host. |
| #143 centered dialog | Own the reusable interaction/JS primitive and Event Type tracer. #147/#148 consume it in separate editor files; #149 uses the Member status-action seam after #147. |
| #138 scanner UI | Prepare separate camera/manual adapters. #47 and #48 wire them independently into the same #46 engine; manual fallback never waits for a working camera. |
| #41 / #64 completion | #41 declares and invokes `IEventCompletionHandler` in its transaction; #64 implements review creation. Do not create competing completion use cases. |
| #49 / #65 invalidation | #49 declares and invokes `IAttendanceInvalidationHandler`; #65 implements the issued-certificate guard/review effect. Both must be present before #67 enables issuance. |
| Global integration files | One integration claim for `Startup.DI.cs` and global source JS/CSS. Regenerate compiled `wwwroot/css/app.css` after merging sources; never hand-merge it. |

“Same wave” proves dependency readiness, **not** that arbitrary edits to the same file are safe.
Use the boundary map and serialize shared-file changes. The five builders remain interchangeable;
these are temporary source boundaries, not permanent module assignments.

## 6. Critical path and final safeguards

One longest remaining **dependency chain** is:

```text
#135 → #136 → #141 → #41 → #45 → #46 → #48 → #49
→ #65 → #67 → #68 → #69 → #70 → #151 → #152 → #153
→ #83 → #78 → #77 → #79 → #80 → #81 → #82
```

This is 23 dependent tickets, **not 23 hours**. The recommended 25 batches account for the
five-builder limit, frontend-first queue order and stable-build verification. Tasks have different
durations; a wave is neither a time estimate nor a guarantee of finishing within 12 hours.

- Accelerate the shared providers on this path instead of adding builders to an already-owned file.
- Camera/QR, host, phone, restore and final sign-offs need real human/environment participation.
  #150 must resolve Q2 inputs; the configured signatory from completed #63 is reused, not rebuilt.
- Development checks stay small: build/CSS when affected, changed-flow smoke and necessary
  focused guards. Existing required CI stays enabled. Broad important tests stay in the finale.
- [finale-test-cases.md](finale-test-cases.md) defines the important cases and full scenario map.
  Planning, fixture screenshots and a merged tool are not final acceptance evidence.
- If code changes after evidence is captured, retest the affected cases on the final commit.
- This request creates a guide only: **no issue was started, reassigned, reordered or given a new
  prerequisite, and no application feature or test suite was implemented/run.**

[i41]: https://github.com/rainyDayssss/AttendTa/issues/41
[i42]: https://github.com/rainyDayssss/AttendTa/issues/42
[i43]: https://github.com/rainyDayssss/AttendTa/issues/43
[i44]: https://github.com/rainyDayssss/AttendTa/issues/44
[i45]: https://github.com/rainyDayssss/AttendTa/issues/45
[i46]: https://github.com/rainyDayssss/AttendTa/issues/46
[i47]: https://github.com/rainyDayssss/AttendTa/issues/47
[i48]: https://github.com/rainyDayssss/AttendTa/issues/48
[i49]: https://github.com/rainyDayssss/AttendTa/issues/49
[i50]: https://github.com/rainyDayssss/AttendTa/issues/50
[i51]: https://github.com/rainyDayssss/AttendTa/issues/51
[i55]: https://github.com/rainyDayssss/AttendTa/issues/55
[i56]: https://github.com/rainyDayssss/AttendTa/issues/56
[i57]: https://github.com/rainyDayssss/AttendTa/issues/57
[i58]: https://github.com/rainyDayssss/AttendTa/issues/58
[i60]: https://github.com/rainyDayssss/AttendTa/issues/60
[i64]: https://github.com/rainyDayssss/AttendTa/issues/64
[i65]: https://github.com/rainyDayssss/AttendTa/issues/65
[i66]: https://github.com/rainyDayssss/AttendTa/issues/66
[i67]: https://github.com/rainyDayssss/AttendTa/issues/67
[i68]: https://github.com/rainyDayssss/AttendTa/issues/68
[i69]: https://github.com/rainyDayssss/AttendTa/issues/69
[i70]: https://github.com/rainyDayssss/AttendTa/issues/70
[i71]: https://github.com/rainyDayssss/AttendTa/issues/71
[i72]: https://github.com/rainyDayssss/AttendTa/issues/72
[i73]: https://github.com/rainyDayssss/AttendTa/issues/73
[i75]: https://github.com/rainyDayssss/AttendTa/issues/75
[i76]: https://github.com/rainyDayssss/AttendTa/issues/76
[i77]: https://github.com/rainyDayssss/AttendTa/issues/77
[i78]: https://github.com/rainyDayssss/AttendTa/issues/78
[i79]: https://github.com/rainyDayssss/AttendTa/issues/79
[i80]: https://github.com/rainyDayssss/AttendTa/issues/80
[i81]: https://github.com/rainyDayssss/AttendTa/issues/81
[i82]: https://github.com/rainyDayssss/AttendTa/issues/82
[i83]: https://github.com/rainyDayssss/AttendTa/issues/83
[i127]: https://github.com/rainyDayssss/AttendTa/issues/127
[i128]: https://github.com/rainyDayssss/AttendTa/issues/128
[i129]: https://github.com/rainyDayssss/AttendTa/issues/129
[i135]: https://github.com/rainyDayssss/AttendTa/issues/135
[i136]: https://github.com/rainyDayssss/AttendTa/issues/136
[i137]: https://github.com/rainyDayssss/AttendTa/issues/137
[i138]: https://github.com/rainyDayssss/AttendTa/issues/138
[i139]: https://github.com/rainyDayssss/AttendTa/issues/139
[i140]: https://github.com/rainyDayssss/AttendTa/issues/140
[i141]: https://github.com/rainyDayssss/AttendTa/issues/141
[i142]: https://github.com/rainyDayssss/AttendTa/issues/142
[i143]: https://github.com/rainyDayssss/AttendTa/issues/143
[i144]: https://github.com/rainyDayssss/AttendTa/issues/144
[i145]: https://github.com/rainyDayssss/AttendTa/issues/145
[i146]: https://github.com/rainyDayssss/AttendTa/issues/146
[i147]: https://github.com/rainyDayssss/AttendTa/issues/147
[i148]: https://github.com/rainyDayssss/AttendTa/issues/148
[i149]: https://github.com/rainyDayssss/AttendTa/issues/149
[i150]: https://github.com/rainyDayssss/AttendTa/issues/150
[i151]: https://github.com/rainyDayssss/AttendTa/issues/151
[i152]: https://github.com/rainyDayssss/AttendTa/issues/152
[i153]: https://github.com/rainyDayssss/AttendTa/issues/153
