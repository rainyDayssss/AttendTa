---
id: 4
type: story
title: "Asia/Manila timezone display helper and UTC server-side time standard"
parent: epic-platform-access
after: [1, 11]
hitl: false
risk: low
tracker_id: "15"
remote: "https://github.com/rainyDayssss/AttendTa/issues/15"
tracker_status: done
assignee: "Wynnwrath,wigglebop25"
---

# Asia/Manila timezone display helper and UTC server-side time standard

## Description

Injects System.TimeProvider wherever Services or Data read the time, replaces every DateTime.Now and DateTime.UtcNow in ASI.Basecode.Services (UserService, EventTypeService, MemberService and VenueService included, since AD-11 binds all epics) with GetUtcNow() compared against stored UTC datetime2(3), and adds one shared Asia/Manila (+08:00) Razor formatter in WebApp that appends the zone label.

## Requirements

Verify: a Services.Tests test with a fake TimeProvider shows a service reads only the injected clock; a search of ASI.Basecode.Services finds no DateTime.Now or DateTime.UtcNow; a WebApp formatter test renders a UTC value as Asia/Manila with its label.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-platform-access/epic-platform-access.md
