---
id: 3
type: story
title: "Upcoming registration impact preview for deactivation"
parent: epic-administration
covers: [R3]
after: [1, 3.15]
hitl: false
risk: medium
tracker_id: "54"
remote: "https://github.com/rainyDayssss/AttendTa/issues/54"
tracker_status: done
assignee: "Wynnwrath"
---

# Upcoming registration impact preview for deactivation

## Description

Adds the deactivation preview to the Account access screen: a GET endpoint and dialog that call IRegistrationCancellationService.PreviewUpcomingForUserAsync from epic-events-registration (AD-6) and list the upcoming, not-yet-started Registrations without Valid Attendance that the deactivation would cancel. The preview takes no lock and writes nothing; the Administration module adds no query of its own. Entry 6 reuses this endpoint for membership deactivation.

## Requirements

Verify: In ASI.Basecode.IntegrationTests, requesting the deactivation preview for a user registered for two upcoming un-attended events and one already-attended event (with attendance verified via a seeded attendance row) displays a modal listing only the two affected upcoming registrations.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-administration/epic-administration.md
