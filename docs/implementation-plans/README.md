# Implementation plan index and alignment review

Reviewed 2026-11-02 against the reduced WBS, specification/stories/sequences v2.1, supplied ERD, Git QA guidelines, repository README and starter source. This is an index/review, not another feature execution plan. User decisions control scope.

## Which document to use

| Document | Authority and current status |
| --- | --- |
| [ADM-01 phased plan](ADM-01_Independent_Backend_And_Integration_Plan.md) | Sole active ADM execution plan. Phase 1 exists on feature/admin-manage-roles; Data and HTTP integration pending. |
| [ADM-01 original full plan](ADM-01_Admin_Manage_Roles_Backend_Plan.md) | Superseded historical reference. Originally described full implementation; replaced by independent-first sequencing while a teammate supplies canonical Data. Do not follow conflicting older instructions. |
| [ADM-01 handoff](ADM-01_Phase1_Handoff.md) | Record of implemented foundation and historical 38-test result; not a second plan or current Git inventory. |
| [ADM-02 phased plan](ADM-02_Independent_Backend_And_Integration_Plan.md) | Proposed admin activity-history reader; no implementation yet. Consumes shared append-only audit data, with no advanced filters/comparison UI. |
| [CERT-01 phased plan](CERT-01_Independent_Backend_And_Integration_Plan.md) | Active eligibility plan. Phase 1 exists on feature branch; Data, HTTP and cross-writer integration pending. |
| [CERT-01 handoff](CERT-01_Phase1_Handoff.md) | Implemented foundation and historical 134-test result. Coverage and environment caveats corrected during this review. |
| [CERT-02 phased plan](CERT-02_Independent_Backend_And_Integration_Plan.md) | Proposed issuance/private-download plan; no application implementation yet. Covers PDF/storage, final eligibility recheck and correction races. |
| [REP-01 phased plan](REP-01_Independent_Backend_And_Integration_Plan.md) | Proposed report plan. No REP application code exists yet; section 10 separates settled requirements from proposed choices. |

The two ADM-01 plans represent a change in execution strategy, not two ADM-01 features. Keep the original for provenance; use only the phased plan to decide what to build. ADM-02 is a separate activity-history feature. All five assigned features now have phased backend plans; that does not mean they are implemented. No existing files or branches were deleted.

## Findings and disposition

1. **Obsolete ADM rules could produce wrong behavior if reused.** Original plan section 4 preserves any attendance row, including invalidated attendance, and section 6 rejects bad pages/returns empty beyond-last pages. Current specification sections 5/19 and US-29 require valid-attendance semantics and normalized paging. The phased plan already corrects these in sections 6/14. Added an explicit superseded banner; original text remains historical.
2. **REP empty-page metadata differed from existing contracts.** REP proposed TotalPages=1 for no rows; ADM/CERT plans and branch response implementations use 0. Corrected REP to Page=1, TotalPages=0 with no navigation. Requirements prescribe empty-state behavior, not this numeric representation; this is a consistency choice, not a new requirement.
3. **CERT handoff overstated coverage and had stale environment facts.** Corrected the exhaustive-coverage implication, clarified selected combinations and absent-certificate/null-status distinction, and added current branch/worktree context. Historical test counts remain labelled historical; no tests rerun in this review.
4. **Plan status and live availability must be distinguished.** Old planning statements saying code is not implemented and old branch-preparation directions are historical. Added status banners. Current REP plan section 1 records same-session live remote verification: team dev/base 174d1fd; ADM local/origin e262e4e; CERT local e65146e/origin d0f1e01. Feature code is not on team dev; its absence in this checkout is expected.
5. **Some REP choices are proposals, not established scope.** Single-event requests, filters, rounding, noncompleted-event rate display, strict integrity failures, grouping collation and read isolation remain clearly identified proposals. The plan is suitable for independent work once its choices are adopted; this review does not claim the WBS prescribes them. Strict failure of the entire report for invalid data is a proposed policy, not an instruction to add remediation tooling.
6. **Production completion depends on real shared integrations.** Canonical participation models/SQL and current-account authorization are not supplied by this starter base. SQL isolation, protected counts, transaction rollback, current sessions and writer races need real integration tests. Pure test passes cannot satisfy those gates. No frontend now is consistent with the user's backend scope, but final project acceptance still needs presentation integration.

## Requirements alignment

| Feature/shared concern | Assessment |
| --- | --- |
| ADM-01 | Active-admin access, fixed roles, last-admin protection, reasons/audit, current-session enforcement, future eligible cancellation without promotion and history retention align with WBS ADM-01 and specification sections 4-5/17. Shared cancellation belongs to Registration. |
| CERT-01 | Completed event plus valid attendance plus officer/admin decision, irreversible decline, retained correction consequences and issued-state guard align with WBS CERT-01/ATT-02 and specification sections 11/14. PDF issuance/download remains CERT-02. |
| CERT-02 | Fixed retained private PDF, final eligibility recheck, immutable snapshots and owner/management download align with WBS CERT-02 and US-25; storage/SQL/concurrency integration remains unproven. |
| ADM-02 | Admin-only read-only chronological activity history aligns with WBS ADM-02 and US-28; safe transactional append-only logging is a shared producer/writer dependency. |
| REP-01 | Confirmed denominator, valid attendee numerator, zero N/A, completed-event no-shows, signup course/year snapshots, full filtered totals and no exports align with WBS REP-01, specification section 15 and US-26. |
| Common paging | Fixed 25, authorization/filtering before counts, deterministic ID tie-breaker, invalid-to-1 and beyond-last clamping align with specification section 19/US-29. Empty response convention matches all five active plans. |
| Retention/ownership | No archive/delete, automatic promotion, reapplication or profile pictures. Pinca's explicit ADM/CERT/REP ownership overrides Markdown's Member 1 Administration allocation; shared work still requires coordination. |
| Delivery | Existing feature branches and team-dev target, conventional commits, local checks, CI and peer approval align with Git QA guidelines. No commit/push or branch cleanup is authorized by this review. |

## Coding standard and integration tutorial

The earlier review's conventions now live in [CODING_STANDARDS.md](../CODING_STANDARDS.md), the working standard for Pinca's slices. It records layer placement, C# style, contract/null handling, DI, transactions, security and verification. Team-wide adoption and automated analyzer enforcement are not assumed.

[INTEGRATION_GUIDE.md](../INTEGRATION_GUIDE.md) explains who consumes each service, what teammates must supply, what stays or gets adapted, and how to prove a connection works. Each feature plan/handoff now begins with its own integration quick reference. Read the active plan for exact behavior and the handoff for actual implementation evidence; this index supplies navigation, not a runtime dependency.

## Review outcome

The active phased plans align with project goals and the base architecture, with the documentation corrections above. They are not proof of a production-ready implementation. REP can proceed to its independent Phase 1 after the user accepts its proposed contract choices; later phases remain dependent on canonical Data/Auth/Registration/Attendance handoffs. This review changed documentation only; no build/test run, schema change, application code, commit or push.
