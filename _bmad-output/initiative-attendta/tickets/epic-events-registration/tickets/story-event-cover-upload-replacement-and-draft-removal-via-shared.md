---
id: 2
type: story
title: "Event Cover upload, replacement, and draft removal via shared storage"
parent: epic-events-registration
covers: [R13]
after: [1, 2.2]
hitl: false
risk: low
tracker_id: "30"
remote: "https://github.com/rainyDayssss/AttendTa/issues/30"
tracker_status: done
assignee: "rainyDayssss"
---

# Event Cover upload, replacement, and draft removal via shared storage

## Description

Integrates Event Cover management with `IPrivateFileStore` from epic-master-data (area `uploads/events`, JPEG or PNG checked by magic bytes, at most 2 MiB) for upload and replacement on draft and published Events, draft cover removal that clears `image_path` and deletes the file after commit, and an authorized cover-serving action for Management and Members; the file path never appears in audit values.

## Requirements

Verify: An officer uploads a valid JPEG cover to a draft Event, removes it while in draft, re-uploads a PNG cover, and an oversized or non-image file is rejected with a validation error; a Member loads the cover only through the authorized action, and no static path to the file is served.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
