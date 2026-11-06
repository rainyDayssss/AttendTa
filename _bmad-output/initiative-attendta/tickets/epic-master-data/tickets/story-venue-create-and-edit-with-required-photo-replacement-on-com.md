---
id: 3
type: story
title: "Venue create and edit with required photo, replacement-on-commit, and capacity guard"
parent: epic-master-data
covers: [R4, R5, R6]
after: [1.3, 1.9, 1.11, 2]
hitl: false
risk: medium
tracker_id: "22"
remote: "https://github.com/rainyDayssss/AttendTa/issues/22"
tracker_status: done
assignee: "wigglebop25"
---

# Venue create and edit with required photo, replacement-on-commit, and capacity guard

## Description

Implements Venue create and edit with positive capacity and a required area-prefixed photo path from IPrivateFileStore; adds LockVenueAsync on VenueRepository and takes the venues row lock before the edit; VenueService owns the replacement order (write the new file, commit inside ExecuteInTransactionAsync, delete the old file only after the commit, delete the new file if the commit fails), refuses a capacity below the participant limit of any linked draft or published Event through a Data read query, throws DomainException, and logs changes without image paths.

## Requirements

Verify: An Officer creates a venue with a photo and edits its details; in an IntegrationTests database where a test SQL fixture inserts a published events row with participant_limit 30 linked to the venue, an edit to capacity 29 is refused with an Invalid outcome on capacity; a second connection holding the venue lock delays the edit until it is released; a forced commit failure keeps the old photo file and removes the new one; and successful changes append activity_logs entries containing no image path.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
