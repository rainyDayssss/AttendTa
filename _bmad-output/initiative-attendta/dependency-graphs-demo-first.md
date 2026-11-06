# AttendTa — demo-first dependency graphs

Snapshot: **6 November 2026**, after the demo rescope and its review fixes on branch
`docs/demo-frontend-replan`. This supersedes
[dependency-graphs-frontend-first.md](dependency-graphs-frontend-first.md); the original
[dependency-graphs.md](dependency-graphs.md) stays archived. Order and phase come from
[delivery-queue.md](delivery-queue.md); demo data rows come from
[demo-mock-register.md](demo-mock-register.md).

## Reading the graphs

- Source: the eight epic `tickets.toml` breakdowns and their `after` values, as `tickets.py status`
  reports them (no cycle, drift, unpinned or undeclared `after`, or order conflict).
- **98 tickets: 40 done, 58 remaining** — 12 demo-critical, 6 demo-polish,
  23 integration, 17 finale. Only remaining work is drawn; prerequisites
  that are already done are satisfied and omitted.
- **153 remaining story edges: 95 within epics, 58 across epics.**
- Arrows run **provider → consumer**. Solid arrows are in-epic or, in the demo graph, between
  drawn tickets; dashed arrows from rounded nodes come from tickets outside the drawn set
  (external nodes repeat for readability and are not extra tickets).
- Labels: **ref · GitHub issue · short title**. `HITL` means a person does part of the work.
  1.14 has no issue yet (`#—`).
- Colors: **blue = demo-critical**, **purple = demo-polish**, **amber = integration**,
  **green = finale**.
- The GitHub issues were not updated for the demo rescope; issue numbers are the existing ones
  (#135 now maps to 8.15). This document changes no tracker state.

## Phase summary

| Phase | Tickets | Ends when |
|---|---|---|
| Demo-critical | 1.13, 1.14, 3.16, 3.17, 4.10, 6.12, 7.4, 4.11, 4.12, 6.13, 5.11, 8.15 | 8.15 rehearsal runs the demo script and every demo-phase Verify line without a dead end |
| Demo-polish | 3.18, 4.13, 2.12, 2.13, 2.14, 2.10 | Merged before the demo if time allows; otherwise shown as they are |
| Integration | 8.14, 3.13, 3.12, 4.1, 4.2, 4.3, 5.4, 5.5, 5.6, 5.7, 6.4, 4.4, 4.5, 4.6, 4.7, 6.5, 6.6, 6.7, 6.8, 6.9, 7.1, 7.2, 7.3 | Every register row except M-01 is `removed` |
| Finale | 3.14, 4.9, 5.10, 6.11, 8.13, 8.11, 2.11, 8.12, 8.10, 8.5, 8.4, 8.2, 8.3, 8.6, 8.7, 8.8, 8.9 | Initiative Done when |

## Demo phase — what to build before the demo

Only demo-phase tickets are drawn. 1.13 (shell) and 1.14 (demo data seam) run in parallel.
After both, 3.16, 3.17, 4.10, 6.12, 7.4, 4.12, 6.13 and 5.11 are independent: 1.13 creates the
My Events slot partials and the access-action slots, so the slot fillers do not wait on 3.17
and 5.11 does not collide with 2.13. 4.11 waits on 4.10 so a real pass can be scanned. Verify
lines use the seeded demo data, so no screen waits on another screen's output.

```mermaid
flowchart LR
  t1_13["1.13 · #136 · shell, routes, slots"]:::critical
  t1_14["1.14 · #— · demo data seam + startup guard"]:::critical
  t3_16["3.16 · #141 · officer dossier/roster"]:::critical
  t3_17["3.17 · #137 · My Events host/slots"]:::critical
  t4_10["4.10 · #144 · pass cards + QR"]:::critical
  t6_12["6.12 · #139 · certificate ledger/viewer"]:::critical
  t7_4["7.4 · #140 · per-event report"]:::critical
  t4_11["4.11 · #138 · door scanner + live QR decode"]:::critical
  t4_12["4.12 · #145 · attendance history"]:::critical
  t6_13["6.13 · #146 · member certificates"]:::critical
  t5_11["5.11 · #149 · access dialogs"]:::critical
  t8_15["8.15 · #135 · demo rehearsal + Impeccable · HITL"]:::critical
  t3_18["3.18 · #142 · member feed/detail polish"]:::polish
  t4_13["4.13 · #129 · profile → My Passes"]:::polish
  t2_12["2.12 · #143 · shared dialog + Event Type"]:::polish
  t2_13["2.13 · #147 · Member editor dialog"]:::polish
  t2_14["2.14 · #148 · Venue editor dialog"]:::polish
  t2_10["2.10 · #127 · list loading/error states"]:::polish
  t1_13 --> t3_16
  t1_13 --> t3_17
  t1_13 --> t4_10
  t1_13 --> t6_12
  t1_13 --> t7_4
  t1_13 --> t4_11
  t1_13 --> t4_12
  t1_13 --> t6_13
  t1_13 --> t5_11
  t1_13 --> t8_15
  t1_13 --> t3_18
  t1_13 --> t2_12
  t1_13 --> t2_10
  t1_14 --> t3_16
  t1_14 --> t3_17
  t1_14 --> t4_10
  t1_14 --> t6_12
  t1_14 --> t7_4
  t1_14 --> t4_11
  t1_14 --> t4_12
  t1_14 --> t6_13
  t1_14 --> t5_11
  t1_14 --> t8_15
  t3_16 --> t8_15
  t3_17 --> t8_15
  t4_10 --> t4_11
  t4_10 --> t8_15
  t4_10 --> t4_13
  t6_12 --> t8_15
  t7_4 --> t8_15
  t4_11 --> t8_15
  t4_12 --> t8_15
  t6_13 --> t8_15
  t5_11 --> t8_15
  t2_12 --> t2_13
  t2_12 --> t2_14
  t2_12 --> t2_10
  t2_13 --> t2_10
  t2_14 --> t2_10
  classDef critical fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef polish fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef integration fill:#fef3c7,stroke:#d97706,color:#451a03
  classDef finale fill:#dcfce7,stroke:#16a34a,color:#14532d
  classDef ext fill:#f1f5f9,stroke:#64748b,color:#0f172a,stroke-dasharray:5 5
```

Longest demo-critical chain (4 tickets): `1.13 → 4.10 → 4.11 → 8.15`.
With five builders the demo-critical set takes about four rounds: 1.13 and 1.14; the eight
surfaces as builders free up (4.10 first); 4.11; then 8.15. Demo-polish tickets fill idle builders
after 1.13.

## Epic graph — remaining cross-epic story edges

Counts on arrows are direct prerequisites between remaining stories, not whole-epic gates.
e8 depends on most epics because it holds 8.15 and the finale.

```mermaid
flowchart LR
  e1["e1 platform-access<br/>2 remaining / 13 total"]
  e2["e2 master-data<br/>5 remaining / 14 total"]
  e3["e3 events-registration<br/>6 remaining / 18 total"]
  e4["e4 check-in<br/>12 remaining / 12 total"]
  e5["e5 administration<br/>6 remaining / 10 total"]
  e6["e6 certificates<br/>9 remaining / 12 total"]
  e7["e7 participation-reports<br/>4 remaining / 4 total"]
  e8["e8 release-acceptance<br/>14 remaining / 15 total"]
  e1 -->|2| e2
  e1 -->|5| e3
  e1 -->|6| e4
  e1 -->|2| e5
  e1 -->|4| e6
  e1 -->|2| e7
  e1 -->|2| e8
  e2 -->|2| e8
  e3 -->|4| e4
  e3 -->|2| e6
  e3 -->|1| e7
  e3 -->|4| e8
  e4 -->|1| e6
  e4 -->|1| e7
  e4 -->|8| e8
  e5 -->|3| e8
  e6 -->|6| e8
  e7 -->|3| e8
```

## Demo data: who adds and who removes each row

Exactly one ticket removes each row. 8.13 removes the seam (M-01) after every other row is gone;
8.11 then verifies, with checks that fail while any part remains.

| Register row | Added by (demo phase) | Removed by |
|---|---|---|
| M-01 | 1.14 | 8.13 |
| M-02 | 3.17 | 3.13 |
| M-03 | 4.10 | 4.1 |
| M-04 | 1.14 | 4.2 |
| M-05 | 4.11 | 4.3 |
| M-06 | 4.11 | 4.5 |
| M-07 | 4.11 | 4.6 |
| M-08 | 4.12 | 4.7 |
| M-09 | 1.14 | 3.12 |
| M-10a | 6.12 | 6.4 |
| M-10b | 6.12 | 6.6 |
| M-11a | 6.12 | 6.7 |
| M-11b | 6.12 | 6.8 |
| M-12 | 6.13 | 6.9 |
| M-13a | 7.4 | 7.1 |
| M-13b | 7.4 | 7.2 |
| M-13c | 7.4 | 7.3 |
| M-14a | 5.11 | 5.4 |
| M-14b | 5.11 | 5.5 |
| M-15a | 5.11 | 5.6 |
| M-15b | 5.11 | 5.7 |

## Dependency-only layers

A ticket's layer is the length of its longest remaining prerequisite path. Layers show what could
start together with unlimited builders; the queue rank still breaks ties.

| Layer | Tickets (queue order) |
|---:|---|
| 0 | 1.13 (critical), 1.14 (critical), 8.14 (integration) |
| 1 | 3.16 (critical), 3.17 (critical), 4.10 (critical), 6.12 (critical), 7.4 (critical), 4.12 (critical), 6.13 (critical), 5.11 (critical), 3.18 (polish), 2.12 (polish) |
| 2 | 4.11 (critical), 4.13 (polish), 2.13 (polish), 2.14 (polish), 3.13 (integration), 3.12 (integration), 5.4 (integration), 7.1 (integration) |
| 3 | 8.15 (critical), 2.10 (polish), 4.1 (integration), 4.2 (integration), 5.5 (integration), 6.4 (integration), 7.2 (integration), 3.14 (finale), 2.11 (finale) |
| 4 | 4.3 (integration), 5.6 (integration) |
| 5 | 5.7 (integration), 4.4 (integration), 4.5 (integration) |
| 6 | 4.6 (integration), 5.10 (finale), 8.2 (finale) |
| 7 | 4.7 (integration), 6.5 (integration), 7.3 (integration) |
| 8 | 6.6 (integration), 4.9 (finale) |
| 9 | 6.7 (integration) |
| 10 | 6.8 (integration), 8.3 (finale) |
| 11 | 6.9 (integration) |
| 12 | 6.11 (finale) |
| 13 | 8.13 (finale) |
| 14 | 8.11 (finale) |
| 15 | 8.12 (finale) |
| 16 | 8.10 (finale) |
| 17 | 8.5 (finale) |
| 18 | 8.4 (finale) |
| 19 | 8.6 (finale) |
| 20 | 8.7 (finale) |
| 21 | 8.8 (finale) |
| 22 | 8.9 (finale) |

### Critical dependency chain

23 tickets deep (depth, not duration):

```text
1.13 → 3.17 → 3.13 → 4.1 → 4.3 → 4.5 → 4.6 → 6.5 → 6.6 → 6.7 → 6.8 → 6.9 → 6.11 → 8.13 → 8.11 → 8.12 → 8.10 → 8.5 → 8.4 → 8.6 → 8.7 → 8.8 → 8.9
```

After the demo, the real completion, check-in and certificate backend sets the pace to release.

## e1 platform-access — 2 remaining

```mermaid
flowchart LR
  t1_13["1.13 · #136 · shell, routes, slots"]:::critical
  t1_14["1.14 · #— · demo data seam + startup guard"]:::critical
  classDef critical fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef polish fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef integration fill:#fef3c7,stroke:#d97706,color:#451a03
  classDef finale fill:#dcfce7,stroke:#16a34a,color:#14532d
  classDef ext fill:#f1f5f9,stroke:#64748b,color:#0f172a,stroke-dasharray:5 5
```

## e2 master-data — 5 remaining

```mermaid
flowchart LR
  t2_12["2.12 · #143 · shared dialog + Event Type"]:::polish
  t2_13["2.13 · #147 · Member editor dialog"]:::polish
  t2_14["2.14 · #148 · Venue editor dialog"]:::polish
  t2_10["2.10 · #127 · list loading/error states"]:::polish
  t2_11["2.11 · #128 · editor acceptance"]:::finale
  xt1_13(["1.13 · shell, routes, slots"]):::ext
  xt1_13 -.-> t2_12
  xt1_13 -.-> t2_10
  t2_12 --> t2_13
  t2_12 --> t2_14
  t2_12 --> t2_10
  t2_13 --> t2_10
  t2_13 --> t2_11
  t2_14 --> t2_10
  t2_14 --> t2_11
  classDef critical fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef polish fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef integration fill:#fef3c7,stroke:#d97706,color:#451a03
  classDef finale fill:#dcfce7,stroke:#16a34a,color:#14532d
  classDef ext fill:#f1f5f9,stroke:#64748b,color:#0f172a,stroke-dasharray:5 5
```

## e3 events-registration — 6 remaining

```mermaid
flowchart LR
  t3_16["3.16 · #141 · officer dossier/roster"]:::critical
  t3_17["3.17 · #137 · My Events host/slots"]:::critical
  t3_18["3.18 · #142 · member feed/detail polish"]:::polish
  t3_13["3.13 · #42 · real My Events query"]:::integration
  t3_12["3.12 · #41 · real completion + hook"]:::integration
  t3_14["3.14 · #43 · e3 backend sweep"]:::finale
  xt1_13(["1.13 · shell, routes, slots"]):::ext
  xt1_14(["1.14 · demo data seam + startup guard"]):::ext
  xt1_13 -.-> t3_16
  xt1_13 -.-> t3_17
  xt1_13 -.-> t3_18
  xt1_14 -.-> t3_16
  xt1_14 -.-> t3_17
  t3_16 --> t3_12
  t3_16 --> t3_14
  t3_17 --> t3_13
  t3_17 --> t3_14
  t3_18 --> t3_14
  t3_13 --> t3_14
  t3_12 --> t3_14
  classDef critical fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef polish fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef integration fill:#fef3c7,stroke:#d97706,color:#451a03
  classDef finale fill:#dcfce7,stroke:#16a34a,color:#14532d
  classDef ext fill:#f1f5f9,stroke:#64748b,color:#0f172a,stroke-dasharray:5 5
```

## e4 check-in — 12 remaining

```mermaid
flowchart LR
  t4_10["4.10 · #144 · pass cards + QR"]:::critical
  t4_11["4.11 · #138 · door scanner + live QR decode"]:::critical
  t4_12["4.12 · #145 · attendance history"]:::critical
  t4_13["4.13 · #129 · profile → My Passes"]:::polish
  t4_1["4.1 · #44 · real QR PNG + download · HITL"]:::integration
  t4_2["4.2 · #45 · real check-in toggle"]:::integration
  t4_3["4.3 · #46 · real check-in engine"]:::integration
  t4_4["4.4 · #47 · camera vs real engine · HITL"]:::integration
  t4_5["4.5 · #48 · manual → real endpoint"]:::integration
  t4_6["4.6 · #49 · real invalidation"]:::integration
  t4_7["4.7 · #50 · real attendance query"]:::integration
  t4_9["4.9 · #51 · e4 backend sweep"]:::finale
  xt1_13(["1.13 · shell, routes, slots"]):::ext
  xt1_14(["1.14 · demo data seam + startup guard"]):::ext
  xt3_16(["3.16 · officer dossier/roster"]):::ext
  xt3_13(["3.13 · real My Events query"]):::ext
  xt3_12(["3.12 · real completion + hook"]):::ext
  xt1_13 -.-> t4_10
  xt1_13 -.-> t4_11
  xt1_13 -.-> t4_12
  xt1_14 -.-> t4_10
  xt1_14 -.-> t4_11
  xt1_14 -.-> t4_12
  xt3_16 -.-> t4_2
  t4_10 --> t4_11
  t4_10 --> t4_13
  t4_10 --> t4_1
  t4_10 --> t4_9
  t4_11 --> t4_2
  t4_11 --> t4_4
  t4_11 --> t4_5
  t4_11 --> t4_9
  t4_12 --> t4_7
  t4_12 --> t4_9
  t4_13 --> t4_9
  xt3_13 -.-> t4_1
  xt3_13 -.-> t4_7
  xt3_12 -.-> t4_2
  t4_1 --> t4_3
  t4_1 --> t4_7
  t4_1 --> t4_9
  t4_2 --> t4_3
  t4_2 --> t4_4
  t4_2 --> t4_9
  t4_3 --> t4_4
  t4_3 --> t4_5
  t4_3 --> t4_6
  t4_3 --> t4_9
  t4_4 --> t4_9
  t4_5 --> t4_6
  t4_5 --> t4_9
  t4_6 --> t4_7
  t4_6 --> t4_9
  t4_7 --> t4_9
  classDef critical fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef polish fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef integration fill:#fef3c7,stroke:#d97706,color:#451a03
  classDef finale fill:#dcfce7,stroke:#16a34a,color:#14532d
  classDef ext fill:#f1f5f9,stroke:#64748b,color:#0f172a,stroke-dasharray:5 5
```

## e5 administration — 6 remaining

```mermaid
flowchart LR
  t5_11["5.11 · #149 · access dialogs"]:::critical
  t5_4["5.4 · #55 · real account deactivation"]:::integration
  t5_5["5.5 · #56 · real account reactivation"]:::integration
  t5_6["5.6 · #57 · real membership deactivation"]:::integration
  t5_7["5.7 · #58 · real membership reactivation"]:::integration
  t5_10["5.10 · #60 · e5 backend sweep"]:::finale
  xt1_13(["1.13 · shell, routes, slots"]):::ext
  xt1_14(["1.14 · demo data seam + startup guard"]):::ext
  xt1_13 -.-> t5_11
  xt1_14 -.-> t5_11
  t5_11 --> t5_4
  t5_11 --> t5_6
  t5_11 --> t5_10
  t5_4 --> t5_5
  t5_4 --> t5_10
  t5_5 --> t5_6
  t5_5 --> t5_10
  t5_6 --> t5_7
  t5_6 --> t5_10
  t5_7 --> t5_10
  classDef critical fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef polish fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef integration fill:#fef3c7,stroke:#d97706,color:#451a03
  classDef finale fill:#dcfce7,stroke:#16a34a,color:#14532d
  classDef ext fill:#f1f5f9,stroke:#64748b,color:#0f172a,stroke-dasharray:5 5
```

## e6 certificates — 9 remaining

```mermaid
flowchart LR
  t6_12["6.12 · #139 · certificate ledger/viewer"]:::critical
  t6_13["6.13 · #146 · member certificates"]:::critical
  t6_4["6.4 · #64 · real review creation"]:::integration
  t6_5["6.5 · #65 · invalidation guard"]:::integration
  t6_6["6.6 · #66 · real review queue/decisions"]:::integration
  t6_7["6.7 · #67 · real issuance pipeline"]:::integration
  t6_8["6.8 · #68 · real certificate download"]:::integration
  t6_9["6.9 · #69 · real member cert links"]:::integration
  t6_11["6.11 · #70 · e6 backend sweep"]:::finale
  xt1_13(["1.13 · shell, routes, slots"]):::ext
  xt1_14(["1.14 · demo data seam + startup guard"]):::ext
  xt3_13(["3.13 · real My Events query"]):::ext
  xt3_12(["3.12 · real completion + hook"]):::ext
  xt4_6(["4.6 · real invalidation"]):::ext
  xt1_13 -.-> t6_12
  xt1_13 -.-> t6_13
  xt1_14 -.-> t6_12
  xt1_14 -.-> t6_13
  t6_12 --> t6_6
  t6_12 --> t6_8
  t6_12 --> t6_11
  t6_13 --> t6_9
  t6_13 --> t6_11
  xt3_13 -.-> t6_9
  xt3_12 -.-> t6_4
  t6_4 --> t6_5
  t6_4 --> t6_6
  t6_4 --> t6_11
  xt4_6 -.-> t6_5
  t6_5 --> t6_6
  t6_5 --> t6_7
  t6_5 --> t6_11
  t6_6 --> t6_7
  t6_6 --> t6_11
  t6_7 --> t6_8
  t6_7 --> t6_11
  t6_8 --> t6_9
  t6_8 --> t6_11
  t6_9 --> t6_11
  classDef critical fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef polish fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef integration fill:#fef3c7,stroke:#d97706,color:#451a03
  classDef finale fill:#dcfce7,stroke:#16a34a,color:#14532d
  classDef ext fill:#f1f5f9,stroke:#64748b,color:#0f172a,stroke-dasharray:5 5
```

## e7 participation-reports — 4 remaining

```mermaid
flowchart LR
  t7_4["7.4 · #140 · per-event report"]:::critical
  t7_1["7.1 · #71 · real report summary"]:::integration
  t7_2["7.2 · #72 · real no-shows/breakdowns"]:::integration
  t7_3["7.3 · #73 · real detail rows"]:::integration
  xt1_13(["1.13 · shell, routes, slots"]):::ext
  xt1_14(["1.14 · demo data seam + startup guard"]):::ext
  xt3_12(["3.12 · real completion + hook"]):::ext
  xt4_6(["4.6 · real invalidation"]):::ext
  xt1_13 -.-> t7_4
  xt1_14 -.-> t7_4
  t7_4 --> t7_1
  xt3_12 -.-> t7_2
  xt4_6 -.-> t7_3
  t7_1 --> t7_2
  t7_2 --> t7_3
  classDef critical fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef polish fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef integration fill:#fef3c7,stroke:#d97706,color:#451a03
  classDef finale fill:#dcfce7,stroke:#16a34a,color:#14532d
  classDef ext fill:#f1f5f9,stroke:#64748b,color:#0f172a,stroke-dasharray:5 5
```

## e8 release-acceptance — 14 remaining

```mermaid
flowchart LR
  t8_15["8.15 · #135 · demo rehearsal + Impeccable · HITL"]:::critical
  t8_14["8.14 · #150 · demo-host readiness · HITL"]:::integration
  t8_13["8.13 · #151 · remove demo seam + hardening"]:::finale
  t8_11["8.11 · #152 · verify no demo data left"]:::finale
  t8_12["8.12 · #153 · final Impeccable pass"]:::finale
  t8_10["8.10 · #83 · release cleanup"]:::finale
  t8_5["8.5 · #78 · important automated cases"]:::finale
  t8_4["8.4 · #77 · combined suite run"]:::finale
  t8_2["8.2 · #75 · hotspot HTTPS phone scan · HITL"]:::finale
  t8_3["8.3 · #76 · backup/restore/redeploy · HITL"]:::finale
  t8_6["8.6 · #79 · manual SPEC §20 · HITL"]:::finale
  t8_7["8.7 · #80 · real 26-ID demo · HITL"]:::finale
  t8_8["8.8 · #81 · Tier 3 sign-off · HITL"]:::finale
  t8_9["8.9 · #82 · release PR · HITL"]:::finale
  xt1_13(["1.13 · shell, routes, slots"]):::ext
  xt1_14(["1.14 · demo data seam + startup guard"]):::ext
  xt3_16(["3.16 · officer dossier/roster"]):::ext
  xt3_17(["3.17 · My Events host/slots"]):::ext
  xt4_10(["4.10 · pass cards + QR"]):::ext
  xt6_12(["6.12 · certificate ledger/viewer"]):::ext
  xt7_4(["7.4 · per-event report"]):::ext
  xt4_11(["4.11 · door scanner + live QR decode"]):::ext
  xt4_12(["4.12 · attendance history"]):::ext
  xt6_13(["6.13 · member certificates"]):::ext
  xt5_11(["5.11 · access dialogs"]):::ext
  xt2_10(["2.10 · list loading/error states"]):::ext
  xt4_3(["4.3 · real check-in engine"]):::ext
  xt4_4(["4.4 · camera vs real engine"]):::ext
  xt4_5(["4.5 · manual → real endpoint"]):::ext
  xt6_7(["6.7 · real issuance pipeline"]):::ext
  xt7_3(["7.3 · real detail rows"]):::ext
  xt3_14(["3.14 · e3 backend sweep"]):::ext
  xt4_9(["4.9 · e4 backend sweep"]):::ext
  xt5_10(["5.10 · e5 backend sweep"]):::ext
  xt6_11(["6.11 · e6 backend sweep"]):::ext
  xt2_11(["2.11 · editor acceptance"]):::ext
  xt1_13 -.-> t8_15
  xt1_14 -.-> t8_15
  xt3_16 -.-> t8_15
  xt3_17 -.-> t8_15
  xt4_10 -.-> t8_15
  xt6_12 -.-> t8_15
  xt7_4 -.-> t8_15
  xt4_11 -.-> t8_15
  xt4_12 -.-> t8_15
  xt6_13 -.-> t8_15
  xt5_11 -.-> t8_15
  xt2_10 -.-> t8_11
  t8_14 --> t8_2
  t8_14 --> t8_3
  xt4_3 -.-> t8_4
  xt4_4 -.-> t8_2
  xt4_5 -.-> t8_2
  xt6_7 -.-> t8_4
  xt6_7 -.-> t8_3
  xt7_3 -.-> t8_13
  xt7_3 -.-> t8_5
  xt3_14 -.-> t8_13
  xt3_14 -.-> t8_5
  xt4_9 -.-> t8_13
  xt4_9 -.-> t8_5
  xt5_10 -.-> t8_13
  xt5_10 -.-> t8_5
  xt6_11 -.-> t8_13
  xt6_11 -.-> t8_5
  t8_13 --> t8_11
  t8_13 --> t8_10
  t8_13 --> t8_9
  t8_11 --> t8_12
  t8_11 --> t8_10
  t8_11 --> t8_9
  xt2_11 -.-> t8_11
  t8_12 --> t8_10
  t8_12 --> t8_6
  t8_12 --> t8_9
  t8_10 --> t8_5
  t8_10 --> t8_4
  t8_10 --> t8_9
  t8_5 --> t8_4
  t8_5 --> t8_6
  t8_4 --> t8_6
  t8_4 --> t8_8
  t8_2 --> t8_6
  t8_3 --> t8_8
  t8_6 --> t8_7
  t8_7 --> t8_8
  t8_8 --> t8_9
  classDef critical fill:#dbeafe,stroke:#2563eb,color:#172554
  classDef polish fill:#ede9fe,stroke:#7c3aed,color:#2e1065
  classDef integration fill:#fef3c7,stroke:#d97706,color:#451a03
  classDef finale fill:#dcfce7,stroke:#16a34a,color:#14532d
  classDef ext fill:#f1f5f9,stroke:#64748b,color:#0f172a,stroke-dasharray:5 5
```

## Notes

- 8.14 (demo-host readiness) has no remaining prerequisite and may run during the demo phase.
- 2.10 waits on 2.12–2.14 because it changes the same list views their edit triggers live on.
- 6.6 waits on 6.5 because both write certificate review rows.
- 4.3 waits on 4.1 so passes carry real tokens before the real engine validates them.
- Backend tickets that wire into a demo-phase screen keep that edge so the real command replaces
  the demo data behind the screen that already exists.
