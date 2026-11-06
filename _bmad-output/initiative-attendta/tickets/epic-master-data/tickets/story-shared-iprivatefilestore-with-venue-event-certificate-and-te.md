---
id: 2
type: story
title: "Shared IPrivateFileStore with venue, event, certificate and temporary areas and authorized photo route"
parent: epic-master-data
covers: [R5]
after: [1.1, 1.3]
hitl: false
risk: medium
tracker_id: "21"
remote: "https://github.com/rainyDayssss/AttendTa/issues/21"
tracker_status: done
assignee: "LucielCienAmir"
---

# Shared IPrivateFileStore with venue, event, certificate and temporary areas and authorized photo route

## Description

Generalizes VenueImageStorage into IPrivateFileStore, declared in Services and implemented in WebApp/Storage under Common:SetupRoot outside wwwroot, with SaveAsync, CreateTemporaryPath, PromoteAsync, OpenReadAsync and Delete keyed by area: uploads/venues and uploads/events accept JPEG or PNG checked by magic bytes up to 2 MiB, certificates accepts only %PDF-, and temporary holds renderer output; filenames are server-generated GUIDs opened with CreateNew, SQL stores area-prefixed relative paths, every open and delete passes a containment check, existing venues rows are rewritten from venues/… to uploads/venues/… by an idempotent SQL script under database/ shipped in the same PR, venue photos are served only through a Management-policy action, and AGENTS.md and docs/database-setup.md are corrected from 5 MiB to 2 MiB with the new path form. Consumers: epic-events-registration entry 2 (Event Covers) and epic-certificates entries 1 and 7 (temporary and certificates areas).

## Requirements

Verify: A Management-policy Officer uploads a 2 MiB JPEG venue photo that renders through the photo route; a file one byte over 2 MiB, a non-image with a .jpg name, and an anonymous photo request are each refused; an integration test promotes a temporary file into certificates and refuses a certificates write that does not start with %PDF-; a migrated venues row reading uploads/venues/… still renders; and AGENTS.md and docs/database-setup.md state 2 MiB and the uploads/venues path form.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
