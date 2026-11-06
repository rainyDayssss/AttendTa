---
id: 3
type: story
title: "Event publication with active master revalidation and activity audit"
parent: epic-events-registration
covers: [R2, R14]
after: [2, 2.3, 2.5]
hitl: false
risk: medium
tracker_id: "31"
remote: "https://github.com/rainyDayssss/AttendTa/issues/31"
tracker_status: done
assignee: "rainyDayssss"
---

# Event publication with active master revalidation and activity audit

## Description

Creates `ASI.Basecode.Data/Queries/ParticipationFacts.cs` with all AD-5 canonical facts (Publishable, Eligible Participant, Valid Attendance, Seat held, Waitlist priority, Event Start Cutoff passed, Signup open, No-show, Attendance rate) consumed downstream by epic-check-in, epic-certificates, and epic-participation-reports (which reads No-show and Attendance rate). Publishes a draft Event through `IEventLifecycleService` under the event type → venue → event lock order, revalidating `ParticipationFacts.Publishable` (active type and venue, `image_path` set, `participant_limit` within venue capacity, `registration_deadline` after the injected `TimeProvider`'s now), then appending an `event.published` Activity Log entry in the same transaction and opening signup in the member feed.

## Requirements

Verify: Services.Tests verify all canonical ParticipationFacts expressions with a fake TimeProvider; an officer publishes a draft Event with a valid cover and future deadline, observing its status change to Published with an `event.published` Activity Log entry, while a draft with a deactivated venue, missing cover or past deadline is refused publication.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
