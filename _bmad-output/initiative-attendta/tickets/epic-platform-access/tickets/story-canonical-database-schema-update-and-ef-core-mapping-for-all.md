---
id: 2
type: story
title: "Canonical database schema update and EF Core mapping for all ten tables with lookup foreign keys"
parent: epic-platform-access
after: [10]
hitl: false
risk: high
tracker_id: "10"
remote: "https://github.com/rainyDayssss/AttendTa/issues/10"
tracker_status: done
assignee: "FivePesos"
---

# Canonical database schema update and EF Core mapping for all ten tables with lookup foreign keys

## Description

Makes database/attendta-mvp-schema.sql the single source for all ten tables: lookup tables with explicit tinyint ids and <name>_id foreign keys replace string states (users.role_id required); event_types.description is NOT NULL and seed-event-types.sql supplies descriptions; certificates.issuer_name_snapshot exists; unique keys and filtered pending-only indexes are in place; READ_COMMITTED_SNAPSHOT is ON; activity_logs has an INSTEAD OF trigger blocking UPDATE and DELETE; and the database-name guard becomes an allow-list of AttendTaMVP_Test, AttendTa_IT_* and AttendTa_Demo (applied to database/ scripts). The fluent AsiBasecodeDbContext mapping and Data/Models/Lookups enums cover all ten tables, with no EF migrations and no data annotations. MemberAccountSummary is removed and MemberRepository's join reads users through the mapped entity. Implements the integration test fixture in tests/ASI.Basecode.IntegrationTests that builds a fresh AttendTa_IT_<guid> from the canonical script for each test class, reached through ATTENDTA_TEST_SQL.

## Requirements

Verify: The canonical schema script applies cleanly to a fresh AttendTaMVP_Test and to an AttendTa_IT_<guid> database, and is refused in any other database name. The integration project maps and queries all ten tables and lookups through EF Core. A direct UPDATE or DELETE on activity_logs fails with the trigger error.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-platform-access/epic-platform-access.md
