# AttendTa dependency graphs

## Current frontend-first dependency overlay — 6 November 2026

The approved live plan is [delivery-queue.md](delivery-queue.md), backed by current leaf `after`
values and `tickets.toml`. Epics retain capability ids; they are not fixed person lanes or whole-
backend gates for the new frontend slices. Already merged work is reused, including #133/#134.

For the current sequential/parallel five-builder batches and shared-provider handoffs, use
[delivery-waves.md](delivery-waves.md), not the archived original waves below.

```mermaid
flowchart TD
  A["1.12 Early frontend walkthrough"] --> B["1.13 Shell / routes / typed fixture contracts"]
  B --> C["3.17 My Events host"]
  B --> D["4.11 Scanner / manual UI"]
  B --> E["6.12 Certificate ledger / viewer UI"]
  B --> F["7.4 Report UI"]
  B --> G["3.16 Officer dossier / roster UI"]
  C --> H["4.10 Pass UI / 4.12 Attendance UI / 6.13 Certificate history UI"]
  B --> I["2.12 Shared dialog + Event Type"]
  I --> J["2.13 Member / 2.14 Venue dialogs"]
  J --> K["5.11 Access dialogs / 2.11 Editor acceptance"]
  G --> L["3.12 Completion + hook"]
  C --> M["3.13 Real registrations query"]
  D --> N["4.2 Window -> 4.3 Recording -> camera/manual adapters"]
  L --> N
  L --> O["6.4 Reviews -> decisions -> issuance / retained downloads"]
  N --> O
  F --> P["7.1-3 Real report reads"]
  K --> Q["5.4-7 Real access changes"]
  M --> R["Focused backend sweeps -> 8.13 Hardening"]
  H --> R
  N --> R
  O --> R
  P --> R
  Q --> R
  R --> S["8.11 Remove mocks / prove real integration"]
  S --> T["8.12 Final frontend walkthrough / refactor / polish"]
  T --> U["8.10 Pre-acceptance cleanup"]
  U --> V["8.5 Important cases -> 8.4 Combined automated run"]
  V --> W["8.6 Manual -> 8.7 Demo -> 8.8 Sign-off"]
  X["8.14 Actual host readiness"] --> Y["8.2 Phone HTTPS / 8.3 Restore evidence"]
  Y --> W
  W --> Z["8.9 Release LAST"]
```

This is a grouped readability overlay, not a claim that every item in a box is serial. In
particular 4.4 camera and 4.5 manual use separate adapters and can wire in parallel after the
shared engine; real report reads and review creation use canonical mapped facts without waiting
for every scanner screen. Leaf dependencies and the source-boundary handoff remain authoritative.

## Archived original baseline (not current execution order)

The graphs and waves below are retained as historical context from the original planning
snapshot. They omit the frontend slices and show the now-superseded post-release 8.10 sweep.
Do not use their counts, critical path or ownership assumptions to select current work.

Source: `_bmad-output/initiative-attendta/tickets/` at commit `420f72c`. Solid arrow = same epic, provider → consumer. Dashed arrow = prerequisite story in another epic. `hitl` marks a step a person runs.

## Epic graph (count = prerequisite edges between the epics' stories)

```mermaid
flowchart LR
  e1["e1 platform-access<br/>11 stories"]
  e2["e2 master-data<br/>9 stories"]
  e3["e3 events-registration<br/>15 stories"]
  e4["e4 check-in<br/>8 stories"]
  e5["e5 administration<br/>9 stories"]
  e6["e6 certificates<br/>10 stories"]
  e7["e7 participation-reports<br/>3 stories"]
  e8["e8 release-acceptance<br/>10 stories"]
  e1 -->|16| e2
  e1 -->|4| e3
  e2 -->|4| e3
  e3 -->|6| e4
  e1 -->|12| e5
  e2 -->|1| e5
  e3 -->|3| e5
  e1 -->|11| e6
  e2 -->|2| e6
  e3 -->|2| e6
  e4 -->|2| e6
  e1 -->|4| e7
  e3 -->|3| e7
  e4 -->|1| e7
  e1 -->|6| e8
  e2 -->|1| e8
  e3 -->|2| e8
  e4 -->|4| e8
  e5 -->|2| e8
  e6 -->|4| e8
  e7 -->|1| e8
```

Build order: e1 → e2 → e3 → e4 → e5 → e6 → e7 → e8. e5 does not wait on e4: it takes the Valid Attendance exemption through e3.15 and ParticipationFacts (3.3) and seeds attendance rows in its own tests. e7 has no in-epic cross dependency to e5.

Hubs, by how many later stories name them: 1.1 sign-in (17), 1.3 unit of work and audit writer (15), 1.2 canonical schema (12), 1.9 shell (11), 1.6 pager (11), 1.4 clock (9), 3.7 seat allocation (8), 3.15 cancellation service (7).

## Waves (earliest possible start with unlimited parallelism)

| Wave | Stories |
| --- | --- |
| 0 | 1.10 |
| 1 | 1.2 |
| 2 | 1.1 |
| 3 | 1.3, 1.9 |
| 4 | 1.11, 2.2, 2.8 |
| 5 | 1.4, 2.3, 6.1 |
| 6 | 1.5, 6.2, 8.1 |
| 7 | 1.6, 6.3 |
| 8 | 1.7, 2.1, 2.4, 5.1, 5.8 |
| 9 | 1.8, 2.5, 2.6, 5.2 |
| 10 | 2.7, 3.1 |
| 11 | 2.9, 3.2 |
| 12 | 3.3 |
| 13 | 3.4, 3.5 |
| 14 | 3.6, 3.15 |
| 15 | 3.7, 5.3 |
| 16 | 3.8, 3.9, 3.11, 5.4, 5.6 |
| 17 | 3.10, 3.12, 5.5, 5.7 |
| 18 | 3.13, 4.2, 5.10 |
| 19 | 3.14, 4.1, 4.3 |
| 20 | 4.4, 6.4 |
| 21 | 4.5 |
| 22 | 4.6, 8.2 |
| 23 | 4.7, 6.5, 7.1 |
| 24 | 4.9, 6.6, 7.2 |
| 25 | 6.7, 7.3 |
| 26 | 6.8, 8.3, 8.4 |
| 27 | 6.9 |
| 28 | 6.11 |
| 29 | 8.5 |
| 30 | 8.6 |
| 31 | 8.7 |
| 32 | 8.8 |
| 33 | 8.9 |
| 34 | 8.10 |

The critical path runs 1.10 → 1.2 → 1.1 → 1.3 → 1.11 → 1.4 → 1.5 → 1.6 → 2.1 → 2.5 → 3.1 → 3.2 → 3.3 → 3.5 → 3.15 → 3.7 → 3.11 → 3.12 → 4.2 → 4.3 → 4.4 → 4.5 → 4.6 → 6.5 → 6.6 → 6.7 → 6.8 → 6.9 → 6.11 → 8.5 → 8.6 → 8.7 → 8.8 → 8.9 → 8.10: 35 stories.

## e1 platform-access

```mermaid
flowchart LR
  s10["10 net10 baseline + CI + tests · hitl"]
  s2["2 canonical schema + EF maps"]
  s1["1 admin login tracer · hitl"]
  s9["9 Tailwind shell"]
  s3["3 unit of work + audit writer"]
  s11["11 master-data writes in txn"]
  s4["4 clock + Manila display"]
  s5["5 account request submit"]
  s6["6 pager + request queue"]
  s7["7 approve / reject request"]
  s8["8 refactor sweep"]
  s10 --> s2
  s2 --> s1
  s10 --> s1
  s1 --> s9
  s1 --> s3
  s2 --> s3
  s2 --> s11
  s3 --> s11
  s1 --> s4
  s11 --> s4
  s1 --> s5
  s2 --> s5
  s3 --> s5
  s4 --> s5
  s9 --> s5
  s1 --> s6
  s2 --> s6
  s4 --> s6
  s5 --> s6
  s9 --> s6
  s1 --> s7
  s2 --> s7
  s3 --> s7
  s4 --> s7
  s5 --> s7
  s6 --> s7
  s11 --> s7
  s1 --> s8
  s2 --> s8
  s3 --> s8
  s4 --> s8
  s5 --> s8
  s6 --> s8
  s7 --> s8
  s9 --> s8
  s10 --> s8
  s11 --> s8
```

## e2 master-data

```mermaid
flowchart LR
  s1["1 event type CRUD"]
  s2["2 IPrivateFileStore + photo route"]
  s3["3 venue create / edit"]
  s4["4 venue list + toggle"]
  s5["5 active master reads"]
  s6["6 member profile edit"]
  s7["7 member list + search"]
  s8["8 member self profile"]
  s9["9 refactor sweep"]
  x1_1(["1.1"]):::ext
  x1_11(["1.11"]):::ext
  x1_3(["1.3"]):::ext
  x1_6(["1.6"]):::ext
  x1_7(["1.7"]):::ext
  x1_9(["1.9"]):::ext
  x1_1 -.-> s1
  x1_3 -.-> s1
  x1_6 -.-> s1
  x1_11 -.-> s1
  x1_1 -.-> s2
  x1_3 -.-> s2
  x1_3 -.-> s3
  x1_9 -.-> s3
  x1_11 -.-> s3
  s2 --> s3
  x1_6 -.-> s4
  s2 --> s4
  s3 --> s4
  s1 --> s5
  s3 --> s5
  x1_3 -.-> s6
  x1_7 -.-> s6
  x1_9 -.-> s6
  x1_6 -.-> s7
  s6 --> s7
  x1_1 -.-> s8
  x1_9 -.-> s8
  s1 --> s9
  s2 --> s9
  s3 --> s9
  s4 --> s9
  s5 --> s9
  s6 --> s9
  s7 --> s9
  s8 --> s9
  classDef ext fill:transparent,stroke-dasharray:4 3,stroke:#999
```

## e3 events-registration

```mermaid
flowchart LR
  s1["1 draft event CRUD · hitl"]
  s2["2 event cover"]
  s3["3 publish + ParticipationFacts"]
  s4["4 published edits"]
  s5["5 member browse + signup"]
  s6["6 signup deadline"]
  s15["15 cancellation service"]
  s7["7 review + seat allocation"]
  s8["8 waitlist promotion"]
  s9["9 member cancel"]
  s10["10 start cutoff"]
  s11["11 event cancel"]
  s12["12 event complete + hook decl"]
  s13["13 My Events"]
  s14["14 refactor sweep"]
  x1_3(["1.3"]):::ext
  x1_4(["1.4"]):::ext
  x1_6(["1.6"]):::ext
  x2_2(["2.2"]):::ext
  x2_3(["2.3"]):::ext
  x2_5(["2.5"]):::ext
  x2_5 -.-> s1
  x1_3 -.-> s1
  x1_4 -.-> s1
  x1_6 -.-> s1
  s1 --> s2
  x2_2 -.-> s2
  s2 --> s3
  x2_3 -.-> s3
  x2_5 -.-> s3
  s3 --> s4
  s3 --> s5
  x1_4 -.-> s5
  s5 --> s6
  s5 --> s15
  s6 --> s7
  s15 --> s7
  s7 --> s8
  s7 --> s9
  s15 --> s9
  s8 --> s10
  s9 --> s10
  s4 --> s11
  s7 --> s11
  s15 --> s11
  s11 --> s12
  s9 --> s13
  s10 --> s13
  s1 --> s14
  s2 --> s14
  s3 --> s14
  s4 --> s14
  s5 --> s14
  s6 --> s14
  s7 --> s14
  s8 --> s14
  s9 --> s14
  s10 --> s14
  s11 --> s14
  s12 --> s14
  s13 --> s14
  s15 --> s14
  classDef ext fill:transparent,stroke-dasharray:4 3,stroke:#999
```

## e4 check-in

```mermaid
flowchart LR
  s1["1 QR Pass PNG · hitl"]
  s2["2 check-in window toggle"]
  s3["3 validation + recording"]
  s4["4 camera scanner · hitl"]
  s5["5 manual check-in"]
  s6["6 invalidation + hook decl"]
  s7["7 member attendance view"]
  s9["9 refactor sweep"]
  x3_1(["3.1"]):::ext
  x3_12(["3.12"]):::ext
  x3_13(["3.13"]):::ext
  x3_3(["3.3"]):::ext
  x3_7(["3.7"]):::ext
  x3_7 -.-> s1
  x3_13 -.-> s1
  x3_1 -.-> s2
  x3_12 -.-> s2
  s2 --> s3
  x3_3 -.-> s3
  x3_7 -.-> s3
  s2 --> s4
  s3 --> s4
  s3 --> s5
  s4 --> s5
  s3 --> s6
  s5 --> s6
  s1 --> s7
  s6 --> s7
  s1 --> s9
  s2 --> s9
  s3 --> s9
  s4 --> s9
  s5 --> s9
  s6 --> s9
  s7 --> s9
  classDef ext fill:transparent,stroke-dasharray:4 3,stroke:#999
```

## e5 administration

```mermaid
flowchart LR
  s1["1 accounts list"]
  s2["2 role assignment + last-admin lock"]
  s3["3 deactivation preview"]
  s4["4 account deactivation"]
  s5["5 account reactivation"]
  s6["6 membership deactivation"]
  s7["7 membership reactivation"]
  s8["8 activity log viewer"]
  s10["10 refactor sweep"]
  x1_1(["1.1"]):::ext
  x1_2(["1.2"]):::ext
  x1_3(["1.3"]):::ext
  x1_4(["1.4"]):::ext
  x1_6(["1.6"]):::ext
  x1_9(["1.9"]):::ext
  x2_6(["2.6"]):::ext
  x3_15(["3.15"]):::ext
  x1_1 -.-> s1
  x1_2 -.-> s1
  x1_6 -.-> s1
  x1_9 -.-> s1
  s1 --> s2
  x1_1 -.-> s2
  x1_2 -.-> s2
  x1_3 -.-> s2
  s1 --> s3
  x3_15 -.-> s3
  s2 --> s4
  s3 --> s4
  x1_1 -.-> s4
  x3_15 -.-> s4
  s4 --> s5
  s3 --> s6
  x2_6 -.-> s6
  x3_15 -.-> s6
  s6 --> s7
  x1_3 -.-> s8
  x1_4 -.-> s8
  x1_6 -.-> s8
  x1_9 -.-> s8
  s1 --> s10
  s2 --> s10
  s3 --> s10
  s4 --> s10
  s5 --> s10
  s6 --> s10
  s7 --> s10
  s8 --> s10
  classDef ext fill:transparent,stroke-dasharray:4 3,stroke:#999
```

## e6 certificates

```mermaid
flowchart LR
  s1["1 Playwright issuance tracer · hitl"]
  s2["2 PDF template"]
  s3["3 signatory details · hitl"]
  s4["4 review seeding hook"]
  s5["5 invalidation hook"]
  s6["6 review queue + decisions"]
  s7["7 issuance pipeline"]
  s8["8 download endpoint"]
  s9["9 My Events link"]
  s11["11 refactor sweep"]
  x1_1(["1.1"]):::ext
  x1_2(["1.2"]):::ext
  x1_3(["1.3"]):::ext
  x1_4(["1.4"]):::ext
  x1_6(["1.6"]):::ext
  x1_9(["1.9"]):::ext
  x2_2(["2.2"]):::ext
  x3_12(["3.12"]):::ext
  x3_13(["3.13"]):::ext
  x4_3(["4.3"]):::ext
  x4_6(["4.6"]):::ext
  x1_1 -.-> s1
  x1_2 -.-> s1
  x2_2 -.-> s1
  s1 --> s2
  x1_4 -.-> s2
  s2 --> s3
  x1_2 -.-> s4
  x1_3 -.-> s4
  x3_12 -.-> s4
  x4_3 -.-> s4
  s4 --> s5
  x4_6 -.-> s5
  s4 --> s6
  s5 --> s6
  x1_3 -.-> s6
  x1_6 -.-> s6
  x1_9 -.-> s6
  s2 --> s7
  s6 --> s7
  x1_3 -.-> s7
  x1_6 -.-> s7
  x1_9 -.-> s7
  x2_2 -.-> s7
  s7 --> s8
  s8 --> s9
  x3_13 -.-> s9
  s1 --> s11
  s2 --> s11
  s3 --> s11
  s4 --> s11
  s5 --> s11
  s6 --> s11
  s7 --> s11
  s8 --> s11
  s9 --> s11
  classDef ext fill:transparent,stroke-dasharray:4 3,stroke:#999
```

## e7 participation-reports

```mermaid
flowchart LR
  s1["1 report KPIs"]
  s2["2 no-shows + distributions"]
  s3["3 detail rows + totals"]
  x1_1(["1.1"]):::ext
  x1_4(["1.4"]):::ext
  x1_6(["1.6"]):::ext
  x1_9(["1.9"]):::ext
  x3_11(["3.11"]):::ext
  x3_12(["3.12"]):::ext
  x3_7(["3.7"]):::ext
  x4_6(["4.6"]):::ext
  x1_1 -.-> s1
  x1_9 -.-> s1
  x3_7 -.-> s1
  x4_6 -.-> s1
  s1 --> s2
  x3_11 -.-> s2
  x3_12 -.-> s2
  s2 --> s3
  x1_4 -.-> s3
  x1_6 -.-> s3
  classDef ext fill:transparent,stroke-dasharray:4 3,stroke:#999
```

## e8 release-acceptance

```mermaid
flowchart LR
  s1["1 demo host provisioning · hitl"]
  s2["2 hotspot HTTPS scanning · hitl"]
  s3["3 backup / restore · hitl"]
  s4["4 whole concurrency suite"]
  s5["5 SPEC §20 automated"]
  s6["6 SPEC §20 manual · hitl"]
  s7["7 demo walkthrough · hitl"]
  s8["8 Tier 3 E2E · hitl"]
  s9["9 dev to main release · hitl"]
  s10["10 refactor sweep"]
  x1_1(["1.1"]):::ext
  x1_2(["1.2"]):::ext
  x1_3(["1.3"]):::ext
  x1_7(["1.7"]):::ext
  x1_8(["1.8"]):::ext
  x2_9(["2.9"]):::ext
  x3_14(["3.14"]):::ext
  x3_7(["3.7"]):::ext
  x4_3(["4.3"]):::ext
  x4_4(["4.4"]):::ext
  x4_5(["4.5"]):::ext
  x4_9(["4.9"]):::ext
  x5_10(["5.10"]):::ext
  x5_2(["5.2"]):::ext
  x6_1(["6.1"]):::ext
  x6_11(["6.11"]):::ext
  x6_7(["6.7"]):::ext
  x7_3(["7.3"]):::ext
  x1_1 -.-> s1
  x1_2 -.-> s1
  x1_3 -.-> s1
  x6_1 -.-> s1
  s1 --> s2
  x4_4 -.-> s2
  x4_5 -.-> s2
  s1 --> s3
  x6_7 -.-> s3
  x1_1 -.-> s4
  x1_7 -.-> s4
  x3_7 -.-> s4
  x4_3 -.-> s4
  x5_2 -.-> s4
  x6_7 -.-> s4
  x1_8 -.-> s5
  x2_9 -.-> s5
  x3_14 -.-> s5
  x4_9 -.-> s5
  x5_10 -.-> s5
  x6_11 -.-> s5
  x7_3 -.-> s5
  s2 --> s6
  s5 --> s6
  s6 --> s7
  s3 --> s8
  s4 --> s8
  s7 --> s8
  s8 --> s9
  s1 --> s10
  s2 --> s10
  s3 --> s10
  s4 --> s10
  s5 --> s10
  s6 --> s10
  s7 --> s10
  s8 --> s10
  s9 --> s10
  classDef ext fill:transparent,stroke-dasharray:4 3,stroke:#999
```
