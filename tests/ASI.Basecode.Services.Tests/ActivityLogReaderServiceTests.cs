using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class ActivityLogReaderServiceTests
{
    [Fact]
    public async Task MissingRequiredFieldsOnBothSidesAndNewOnlySnapshotsAreReported()
    {
        var member = Row(AuditActions.MemberEdited, "members", 1,
            oldValues: """{"FullName":"Old","StudentNumber":"1","Course":"BSCS","YearLevel":1,"Section":"A","Status":"active"}""",
            newValues: """{"FullName":"New","StudentNumber":"1","Course":"BSCS","YearLevel":1,"Section":"A","Status":"active"}""");
        var venue = Row(AuditActions.VenueEdited, "venues", 2,
            oldValues: """{"RoomName":"Old","Capacity":10,"Location":null,"ContactPerson":null,"PhotoChanged":false}""",
            newValues: """{"RoomName":"New","Capacity":10,"Location":null,"ContactPerson":null,"PhotoChanged":false}""");
        var created = Row(AuditActions.EventTypeCreated, "event_types", 3,
            newValues: """{"TypeName":"Seminar","IsActive":true}""");
        var items = (await ReadAsync(member, venue, created)).Items;
        Assert.Equal("Some change details are unavailable", items[0].Details["Recorded changes"]);
        Assert.Equal("Some change details are unavailable", items[1].Details["Recorded changes"]);
        Assert.Equal("Some change details are unavailable", items[2].Details["Recorded changes"]);
        Assert.Equal("Seminar", items[2].Details["Classification"]);
    }

    [Fact]
    public async Task MissingNewPhotoFlagReportsUnavailableWithoutGuessing()
    {
        var item = Assert.Single((await ReadAsync(Row(AuditActions.VenueEdited, "venues", 1,
            oldValues: """{"RoomName":"Room","Capacity":10,"Location":null,"AvailabilityNotes":null,"ContactPerson":null,"PhotoChanged":true}""",
            newValues: """{"RoomName":"Room","Capacity":10,"Location":null,"AvailabilityNotes":null,"ContactPerson":null}"""))).Items);
        Assert.Equal("Unavailable", item.Details["Recorded changes"]);
        Assert.DoesNotContain("Photo changed", item.Details.Keys);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"true\"")]
    [InlineData("1")]
    public async Task InvalidPhotoAndCoverIndicatorsReportUnavailable(string invalid)
    {
        var venue = Row(AuditActions.VenueEdited, "venues", 1,
            oldValues: """{"RoomName":"Old","Capacity":10,"Location":null,"AvailabilityNotes":null,"ContactPerson":null,"PhotoChanged":false}""",
            newValues: $$"""{"RoomName":"New","Capacity":10,"Location":null,"AvailabilityNotes":null,"ContactPerson":null,"PhotoChanged":{{invalid}}}""");
        var cover = Row(AuditActions.EventUpdated, "events", 2,
            oldValues: """{"HasCover":false,"CoverChanged":true}""",
            newValues: $$"""{"HasCover":true,"CoverChanged":{{invalid}}}""");
        var items = (await ReadAsync(venue, cover)).Items;
        Assert.All(items, item => Assert.Equal("Some change details are unavailable", item.Details["Recorded changes"]));
        Assert.DoesNotContain("Photo changed", items[0].Details.Keys);
        Assert.DoesNotContain("Cover changed", items[1].Details.Keys);
    }

    [Fact]
    public async Task InvalidOldIndicatorIsUnavailableEvenWhenNewIndicatorIsValid()
    {
        var item = Assert.Single((await ReadAsync(Row(AuditActions.VenueEdited, "venues", 1,
            oldValues: """{"RoomName":"Room","Capacity":10,"Location":null,"AvailabilityNotes":null,"ContactPerson":null,"PhotoChanged":null}""",
            newValues: """{"RoomName":"Room","Capacity":10,"Location":null,"AvailabilityNotes":null,"ContactPerson":null,"PhotoChanged":true}"""))).Items);
        Assert.Equal("Some change details are unavailable", item.Details["Recorded changes"]);
        Assert.Equal("Yes", item.Details["Photo changed"]);
    }

    [Fact]
    public async Task TimestampChangesWithinAMinuteRemainDistinctAtRecordedPrecision()
    {
        var item = Assert.Single((await ReadAsync(Row(AuditActions.EventPublished, "events", 1,
            oldValues: """{"Status":1,"PublishedAt":"2026-11-05T12:35:01.123Z"}""",
            newValues: """{"Status":2,"PublishedAt":"2026-11-05T12:35:01.456Z"}"""))).Items);
        Assert.Equal("2026-11-05 08:35:01.123 PM → 2026-11-05 08:35:01.456 PM", item.Details["Published at"]);
        var sameInstant = Assert.Single((await ReadAsync(Row(AuditActions.EventPublished, "events", 2,
            oldValues: """{"Status":1,"PublishedAt":"2026-11-05T12:35:01Z"}""",
            newValues: """{"Status":2,"PublishedAt":"2026-11-05T20:35:01+08:00"}"""))).Items);
        Assert.DoesNotContain("Published at", sameInstant.Details.Keys);
    }

    [Fact]
    public async Task CompleteEventCreationMayOmitCoverFieldsButPartialCoverShapeIsUnavailable()
    {
        const string snapshot = """{"Title":"Event","Description":null,"TypeName":null,"VenueName":null,"StartsAt":"2026-12-01T09:00:00Z","RegistrationDeadline":"2026-11-30T09:00:00Z","ParticipantLimit":50,"Status":1}""";
        var created = Row(AuditActions.EventCreated, "events", 1, newValues: snapshot);
        var partialCover = Row(AuditActions.EventUpdated, "events", 2,
            oldValues: """{"HasCover":false,"CoverChanged":true}""",
            newValues: """{"HasCover":true}""");
        var items = (await ReadAsync(created, partialCover)).Items;
        Assert.DoesNotContain("Recorded changes", items[0].Details.Keys);
        Assert.Equal("No → Yes", items[1].Details["Cover attached"]);
        Assert.Equal("Some change details are unavailable", items[1].Details["Recorded changes"]);
        Assert.DoesNotContain("Cover changed", items[1].Details.Keys);
    }

    [Fact]
    public async Task CompleteOneSidedAndCoverOnlyShapesDoNotInventMissingFields()
    {
        var created = Row(AuditActions.EventTypeCreated, "event_types", 1,
            newValues: """{"TypeName":"Seminar","Description":null,"IsActive":true}""");
        var cover = Row(AuditActions.EventUpdated, "events", 2,
            oldValues: """{"HasCover":false,"CoverChanged":true}""",
            newValues: """{"HasCover":true,"CoverChanged":true}""");
        var registration = Row(AuditActions.RegistrationApproved, "registrations", 3,
            oldValues: """{"Status":"Pending"}""", newValues: """{"Status":"Confirmed"}""");
        var items = (await ReadAsync(created, cover, registration)).Items;
        Assert.All(items, item => Assert.DoesNotContain("Recorded changes", item.Details.Keys));
        Assert.Equal("No → Yes", items[1].Details["Cover attached"]);
    }

    [Fact]
    public void FriendlyActionSearchUsesTheSameLabelsIgnoringCaseAndMatchingSubstrings()
    {
        Assert.Contains(AuditActions.UserRoleAssigned, ActivityActionLabels.MatchingCodes("OLE CHAN"));
        Assert.Contains(AuditActions.MemberEdited, ActivityActionLabels.MatchingCodes("updated"));
        Assert.Contains(AuditActions.EventUpdated, ActivityActionLabels.MatchingCodes("updated"));
        Assert.DoesNotContain(AuditActions.MemberEdited, ActivityActionLabels.MatchingCodes("role chan"));
        Assert.Equal("Role changed", ActivityActionLabels.For(AuditActions.UserRoleAssigned));
    }

    [Fact]
    public async Task RoleChangeShowsFriendlyActionCurrentNameAndRecordedRole()
    {
        var row = Row(
            AuditActions.UserRoleAssigned,
            "users",
            42,
            targetName: "Ana Reyes",
            oldValues: """{"RoleCode":"member"}""",
            newValues: """{"RoleCode":"admin"}""",
            reason: "Promoted.");

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("Role changed", item.ActionLabel);
        Assert.Equal("Ana Reyes", item.AffectedRecord);
        Assert.Equal("Ana Reyes", item.Details["Affected record"]);
        Assert.Equal("Account", item.Details["Record type"]);
        Assert.Equal("42", item.Details["Record ID"]);
        Assert.DoesNotContain("Action code", item.Details.Keys);
        Assert.Equal("Member → Administrator", item.Details["Role"]);
        Assert.Equal("Promoted.", item.Details["Reason"]);
        Assert.Equal("Audit Administrator", item.Details["Actor"]);
        Assert.DoesNotContain("Actor type", item.Details.Keys);
        Assert.Equal("2026-11-05 08:35 PM", item.Details["Occurred"]);
    }

    [Fact]
    public async Task EventUpdateFormatsOnlyAllowlistedFieldsAndManilaTimes()
    {
        var row = Row(
            AuditActions.EventUpdated,
            "events",
            7,
            targetName: "Summer Fest",
            oldValues: """
                {"Title":"Spring Fest","Description":"Old","TypeName":"Seminar","TypeId":3,"VenueName":"Room 101","VenueId":9,"StartsAt":"2026-12-01T09:00:00Z","RegistrationDeadline":"2026-11-30T09:00:00Z","ParticipantLimit":50,"Status":1,"PasswordHash":"secret","Extra":"ignored"}
                """,
            newValues: """
                {"Title":"Summer Fest","Description":"Old","TypeName":"Seminar","TypeId":3,"VenueName":"Room 101","VenueId":9,"StartsAt":"2026-12-01T10:00:00Z","RegistrationDeadline":"2026-11-30T09:00:00Z","ParticipantLimit":80,"Status":2}
                """);

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("Event updated", item.ActionLabel);
        Assert.Equal("Spring Fest → Summer Fest", item.Details["Title"]);
        Assert.Equal("2026-12-01 05:00 PM → 2026-12-01 06:00 PM", item.Details["Starts at"]);
        Assert.Equal("Draft → Published", item.Details["Status"]);
        Assert.Equal("50 → 80", item.Details["Participant limit"]);
        Assert.DoesNotContain("Description", item.Details.Keys);
        Assert.DoesNotContain("Registration deadline", item.Details.Keys);
        Assert.DoesNotContain("Event type ID", item.Details.Keys);
        Assert.DoesNotContain("PasswordHash", item.Details.Keys);
        Assert.DoesNotContain("Extra", item.Details.Keys);
    }

    [Fact]
    public async Task MissingSnapshotReportsUnavailableInsteadOfFabricatingState()
    {
        var row = Row(AuditActions.EventUpdated, "events", 7, targetName: "Summer Fest");

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("Unavailable", item.Details["Recorded changes"]);
        Assert.DoesNotContain("Title", item.Details.Keys);
        Assert.DoesNotContain("Status", item.Details.Keys);
    }

    [Fact]
    public async Task MalformedSnapshotReportsUnavailableAndKeepsTheReadableSide()
    {
        var row = Row(
            AuditActions.EventUpdated,
            "events",
            7,
            targetName: "Summer Fest",
            oldValues: "{not-json",
            newValues: """{"Title":"Summer Fest"}""");

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("Some change details are unavailable", item.Details["Recorded changes"]);
        Assert.Equal("Summer Fest", item.Details["Title (recorded new)"]);
    }

    [Fact]
    public async Task PartialSnapshotShowsOnlyTheRecordedSideWithoutAnArrow()
    {
        var created = Row(
            AuditActions.EventCreated,
            "events",
            7,
            targetName: "Summer Fest",
            newValues: """{"Title":"Summer Fest"}""");
        var removed = Row(
            AuditActions.EventUpdated,
            "events",
            8,
            targetName: "Summer Fest",
            oldValues: """{"Title":"Spring Fest"}""");

        var items = (await ReadAsync(created, removed)).Items;

        Assert.Equal("Summer Fest", items[0].Details["Title"]);
        Assert.Equal("Spring Fest", items[1].Details["Title (previous)"]);
        Assert.Equal("Some change details are unavailable", items[0].Details["Recorded changes"]);
        Assert.Equal("Some change details are unavailable", items[1].Details["Recorded changes"]);
    }

    [Fact]
    public async Task EventUpdateWithPartialFieldsReportsUnavailableWithoutInventingValues()
    {
        var row = Row(AuditActions.EventUpdated, "events", 7,
            oldValues: """{"Title":"Old","ParticipantLimit":50}""",
            newValues: """{"Title":"New"}""");

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("Old → New", item.Details["Title"]);
        Assert.Equal("50", item.Details["Participant limit (previous)"]);
        Assert.Equal("Some change details are unavailable", item.Details["Recorded changes"]);
    }

    [Fact]
    public async Task ExplicitNullIsRecordedButMissingPropertyIsUnavailable()
    {
        var complete = Row(AuditActions.EventUpdated, "events", 1,
            oldValues: """{"Title":"Event","Description":null,"TypeName":"Seminar","VenueName":"Room","StartsAt":"2026-12-01T09:00:00Z","RegistrationDeadline":"2026-11-30T09:00:00Z","ParticipantLimit":50,"Status":1}""",
            newValues: """{"Title":"Event","Description":"Added","TypeName":"Seminar","VenueName":"Room","StartsAt":"2026-12-01T09:00:00Z","RegistrationDeadline":"2026-11-30T09:00:00Z","ParticipantLimit":50,"Status":1}""");
        var partial = Row(AuditActions.EventUpdated, "events", 2,
            oldValues: """{"Description":null}""", newValues: """{"Title":"New"}""");
        var items = (await ReadAsync(complete, partial)).Items;
        Assert.Equal("None → Added", items[0].Details["Description"]);
        Assert.DoesNotContain("Recorded changes", items[0].Details.Keys);
        Assert.Equal("None", items[1].Details["Description (previous)"]);
        Assert.Equal("New", items[1].Details["Title (recorded new)"]);
        Assert.Equal("Some change details are unavailable", items[1].Details["Recorded changes"]);
    }

    [Fact]
    public async Task RecordedEmptyTextIsDistinctFromMissingText()
    {
        var row = Row(AuditActions.EventTypeEdited, "event_types", 1,
            oldValues: """{"TypeName":"Seminar","Description":"","IsActive":true}""",
            newValues: """{"TypeName":"Seminar","Description":"Details","IsActive":true}""");
        var reverse = Row(AuditActions.EventTypeEdited, "event_types", 2,
            oldValues: """{"TypeName":"Seminar","Description":"Details","IsActive":true}""",
            newValues: """{"TypeName":"Seminar","Description":"","IsActive":true}""");
        var items = (await ReadAsync(row, reverse)).Items;
        Assert.Equal("(empty) → Details", items[0].Details["Description"]);
        Assert.Equal("Details → (empty)", items[1].Details["Description"]);
        Assert.DoesNotContain("Recorded changes", items[0].Details.Keys);
        Assert.DoesNotContain("Recorded changes", items[1].Details.Keys);
    }

    [Fact]
    public async Task FullEventSnapshotsMissingSameRequiredFieldReportUnavailable()
    {
        var row = Row(AuditActions.EventUpdated, "events", 1,
            oldValues: """{"Title":"Old","Description":null,"TypeName":"Seminar","VenueName":"Room","StartsAt":"2026-12-01T09:00:00Z","RegistrationDeadline":"2026-11-30T09:00:00Z","Status":1}""",
            newValues: """{"Title":"New","Description":null,"TypeName":"Seminar","VenueName":"Room","StartsAt":"2026-12-01T09:00:00Z","RegistrationDeadline":"2026-11-30T09:00:00Z","Status":1}""");
        var item = Assert.Single((await ReadAsync(row)).Items);
        Assert.Equal("Old → New", item.Details["Title"]);
        Assert.Equal("Some change details are unavailable", item.Details["Recorded changes"]);
        Assert.DoesNotContain("Participant limit", item.Details.Keys);
    }

    [Fact]
    public async Task SnapshotSidesFollowActualWriterContracts()
    {
        var role = Row(AuditActions.UserRoleAssigned, "users", 1, newValues: """{"RoleCode":"admin"}""");
        var missingNew = Row(AuditActions.CertificateIssued, "certificates", 2, oldValues: """{"CertificateNumber":"X"}""");
        var decision = Row(AuditActions.CertificateDeclined, "certificates", 3, newValues: """{"Status":"not_eligible","ReviewedAt":"2026-11-05T13:30:00Z"}""");
        var items = (await ReadAsync(role, missingNew, decision)).Items;
        Assert.Equal("Administrator", items[0].Details["Role (recorded new)"]);
        Assert.Equal("Some change details are unavailable", items[0].Details["Recorded changes"]);
        Assert.Equal("Some change details are unavailable", items[1].Details["Recorded changes"]);
        Assert.Equal("Not eligible", items[2].Details["Decision"]);
        Assert.DoesNotContain("Recorded changes", items[2].Details.Keys);
    }

    [Fact]
    public async Task ChangeIndicatorsAndCancellationCauseRemainVisible()
    {
        var cover = Row(AuditActions.EventUpdated, "events", 1,
            oldValues: """{"HasCover":true,"CoverChanged":true}""", newValues: """{"HasCover":true,"CoverChanged":true}""");
        var venue = Row(AuditActions.VenueEdited, "venues", 2,
            oldValues: """{"RoomName":"A","PhotoChanged":false}""", newValues: """{"RoomName":"A","PhotoChanged":true}""");
        var cancellation = Row(AuditActions.RegistrationCancelled, "registrations", 3,
            oldValues: """{"Status":"Confirmed","Cause":"MemberRequested"}""",
            newValues: """{"Status":"Cancelled","Cause":"MemberRequested"}""");
        var items = (await ReadAsync(cover, venue, cancellation)).Items;
        Assert.Equal("Yes", items[0].Details["Cover changed"]);
        Assert.DoesNotContain("Recorded changes", items[0].Details.Keys);
        Assert.Equal("Yes", items[1].Details["Photo changed"]);
        Assert.Equal("MemberRequested", items[2].Details["Cause"]);
    }

    [Fact]
    public async Task UnknownActionAndRecordTypeKeepLegibleFallbacksWithTheRecordId()
    {
        var row = Row("audit.test.unknown", "mystery_records", 777, targetName: null);

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("audit.test.unknown", item.ActionLabel);
        Assert.Null(item.AffectedRecord);
        Assert.Equal("Unavailable (record 777)", item.Details["Affected record"]);
        Assert.Equal("777", item.Details["Record ID"]);
        Assert.Equal("mystery_records", item.Details["Record type"]);
        Assert.DoesNotContain("Recorded changes", item.Details.Keys);
    }

    [Fact]
    public async Task RegistrationIdentifiesTheCurrentParticipantAndEvent()
    {
        var row = Row(
            AuditActions.RegistrationApproved,
            "registrations",
            11,
            targetName: "Ana Reyes",
            targetContext: "Summer Fest",
            oldValues: """{"Status":"pending"}""",
            newValues: """{"Status":"confirmed"}""");

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("Ana Reyes", item.AffectedRecord);
        Assert.Equal("Summer Fest", item.AffectedRecordContext);
        Assert.Equal("Summer Fest", item.Details["Related event"]);
        Assert.Equal("Pending → Confirmed", item.Details["Status"]);
        Assert.Equal("Registration approved", item.ActionLabel);
    }

    [Fact]
    public async Task ApprovalLabelDoesNotMisrepresentWaitlistedOutcome()
    {
        var row = Row(AuditActions.RegistrationApproved, "registrations", 11,
            oldValues: """{"Status":"Pending"}""",
            newValues: """{"Status":"Waitlisted"}""");

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("Registration approved", item.ActionLabel);
        Assert.Equal("Pending → Waitlisted", item.Details["Status"]);
    }

    [Fact]
    public async Task NumericStatusLabelsMatchLookupEnums()
    {
        var cases = new (string Action, string Field, string Detail, int Value, string Label)[]
        {
            (AuditActions.EventUpdated, "Status", "Status", (int)EventStatusCode.Draft, "Draft"),
            (AuditActions.EventUpdated, "Status", "Status", (int)EventStatusCode.Published, "Published"),
            (AuditActions.EventUpdated, "Status", "Status", (int)EventStatusCode.Cancelled, "Cancelled"),
            (AuditActions.EventUpdated, "Status", "Status", (int)EventStatusCode.Completed, "Completed"),
            (AuditActions.RegistrationApproved, "Status", "Status", (int)RegistrationStatusCode.Pending, "Pending"),
            (AuditActions.RegistrationApproved, "Status", "Status", (int)RegistrationStatusCode.Confirmed, "Confirmed"),
            (AuditActions.RegistrationApproved, "Status", "Status", (int)RegistrationStatusCode.Waitlisted, "Waitlisted"),
            (AuditActions.RegistrationApproved, "Status", "Status", (int)RegistrationStatusCode.Cancelled, "Cancelled"),
            (AuditActions.MemberMembershipDeactivated, "Status", "Membership status", (int)MembershipStatusCode.Active, "Active"),
            (AuditActions.MemberMembershipDeactivated, "Status", "Membership status", (int)MembershipStatusCode.Inactive, "Inactive"),
            (AuditActions.AccountRequestApproved, "Status", "Request status", (int)AccountRequestStatusCode.Pending, "Pending"),
            (AuditActions.AccountRequestApproved, "Status", "Request status", (int)AccountRequestStatusCode.Approved, "Approved"),
            (AuditActions.AccountRequestApproved, "Status", "Request status", (int)AccountRequestStatusCode.Rejected, "Rejected"),
            (AuditActions.CertificateApproved, "Status", "Decision", (int)CertificateStatusCode.PendingApproval, "Pending approval"),
            (AuditActions.CertificateApproved, "Status", "Decision", (int)CertificateStatusCode.Eligible, "Eligible"),
            (AuditActions.CertificateApproved, "Status", "Decision", (int)CertificateStatusCode.NotEligible, "Not eligible"),
            (AuditActions.CertificateApproved, "Status", "Decision", (int)CertificateStatusCode.Issued, "Issued"),
            (AuditActions.AttendanceRecorded, "CheckInMethodId", "Check-in method", (int)CheckInMethodCode.QrCode, "QR scan"),
            (AuditActions.AttendanceRecorded, "CheckInMethodId", "Check-in method", (int)CheckInMethodCode.ManualSearch, "Manual lookup"),
            (AuditActions.AttendanceRecorded, "CheckInMethodId", "Check-in method", (int)CheckInMethodCode.StudentNumber, "Student number")
        };

        foreach (var (action, field, detail, value, label) in cases)
        {
            var row = Row(action, "records", value, newValues: $"{{\"{field}\":{value}}}");
            var item = Assert.Single((await ReadAsync(row)).Items);
            Assert.Equal(label, item.Details.TryGetValue(detail, out var valueText) ? valueText : item.Details[$"{detail} (recorded new)"]);
        }
    }

    [Fact]
    public async Task MissingRelatedEventIsReportedAsUnavailable()
    {
        var row = Row(AuditActions.AttendanceRecorded, "attendance", 3, targetName: "Ana Reyes");

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("Unavailable", item.Details["Related event"]);
    }

    [Fact]
    public async Task CheckInMethodAndDecisionLabelsCoverTypedAndStringCodes()
    {
        var attendance = Row(
            AuditActions.AttendanceRecorded,
            "attendance",
            3,
            targetName: "Ana Reyes",
            newValues: """{"CheckInMethodId":2,"RecordedAt":"2026-11-05T13:30:00Z"}""");
        var certificate = Row(
            AuditActions.CertificateDeclined,
            "certificates",
            4,
            targetName: "Ana Reyes",
            newValues: """{"Status":"noteligible","ReviewedAt":"2026-11-05T13:30:00Z"}""");

        var items = (await ReadAsync(attendance, certificate)).Items;

        Assert.Equal("Manual lookup", items[0].Details["Check-in method"]);
        Assert.Equal("2026-11-05 09:30 PM", items[0].Details["Recorded at"]);
        Assert.Equal("Not eligible", items[1].Details["Decision"]);
    }

    [Fact]
    public async Task ContactDataAndUnlistedFieldsAreNeverRendered()
    {
        var row = Row(
            AuditActions.MemberEdited,
            "members",
            5,
            targetName: "Ana Reyes",
            oldValues: """{"FullName":"Old Name","StudentNumber":"2024-0001","Course":"BSCS","YearLevel":2,"Section":"A","MobileNumber":"09171234567","PasswordHash":"hash","MembershipType":"Regular","Status":"active"}""",
            newValues: """{"FullName":"Ana Reyes","StudentNumber":"2024-0002","Course":"BSCS","YearLevel":2,"Section":"A","MobileNumber":"09998887777","PasswordHash":"hash2","MembershipType":"Regular","Status":"active"}""");

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("Old Name → Ana Reyes", item.Details["Full name"]);
        Assert.Equal("2024-0001 → 2024-0002", item.Details["Student number"]);
        Assert.DoesNotContain("MobileNumber", item.Details.Keys);
        Assert.DoesNotContain("PasswordHash", item.Details.Keys);
        Assert.DoesNotContain(item.Details.Values, value => value is not null && value.Contains("09171234567", StringComparison.Ordinal));
        Assert.DoesNotContain(item.Details.Values, value => value is not null && value.Contains("09998887777", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnchangedFieldsProduceNoDetailRowOrUnavailableNotice()
    {
        var row = Row(
            AuditActions.MemberEdited,
            "members",
            5,
            targetName: "Ana Reyes",
            oldValues: """{"FullName":"Ana Reyes","StudentNumber":"2024-0001","Course":"BSCS","YearLevel":2,"Section":"A","MembershipType":null,"Status":"active"}""",
            newValues: """{"FullName":"Ana Reyes","StudentNumber":"2024-0002","Course":"BSCS","YearLevel":2,"Section":"A","MembershipType":null,"Status":"active"}""");

        var item = Assert.Single((await ReadAsync(row)).Items);

        Assert.Equal("2024-0001 → 2024-0002", item.Details["Student number"]);
        Assert.DoesNotContain("Full name", item.Details.Keys);
        Assert.DoesNotContain("Recorded changes", item.Details.Keys);
    }

    [Fact]
    public async Task PagingKeepsTheExistingContractWithoutReorderingRows()
    {
        var rows = Enumerable.Range(1, 3).Select(index => Row(
            AuditActions.UserDeactivated,
            "users",
            index,
            targetName: $"User {index}",
            newValues: """{"IsActive":false}""",
            occurredAt: new DateTime(2026, 11, 5, 20, index, 0, DateTimeKind.Utc))).ToArray();
        var service = new ActivityLogReaderService(new FakeRepository(rows), new ListReadOptions { PageSize = 2 });

        var second = await service.GetPageAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(3, second.TotalCount);
        Assert.Equal(2, second.PageNumber);
        var item = Assert.Single(second.Items);
        Assert.Equal("User 3", item.AffectedRecord);
        Assert.Equal("Inactive", item.Details["Account access (recorded new)"]);
    }

    private static async Task<PagedResult<ActivityLogItemViewModel>> ReadAsync(params ActivityLogListItem[] rows) =>
        await new ActivityLogReaderService(new FakeRepository(rows), new ListReadOptions { PageSize = 25 })
            .GetPageAsync(1, TestContext.Current.CancellationToken);

    private static ActivityLogListItem Row(
        string action,
        string entityType,
        int entityId,
        string? targetName = null,
        string? targetContext = null,
        string? oldValues = null,
        string? newValues = null,
        string? reason = null,
        DateTime? occurredAt = null) => new()
    {
        LogId = entityId,
        ActorType = ActorTypeCode.User,
        ActorName = "Audit Administrator",
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        Reason = reason,
        OccurredAt = occurredAt ?? new DateTime(2026, 11, 5, 12, 35, 0, DateTimeKind.Utc),
        OldValues = oldValues,
        NewValues = newValues,
        TargetName = targetName,
        TargetContext = targetContext
    };

    private sealed class FakeRepository(IReadOnlyList<ActivityLogListItem> rows) : IActivityLogReaderRepository
    {
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows.Count);

        public Task<IReadOnlyList<ActivityLogListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActivityLogListItem>>(rows.Skip(skip).Take(take).ToList());
    }
}
