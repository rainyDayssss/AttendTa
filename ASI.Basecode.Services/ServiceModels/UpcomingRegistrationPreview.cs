using System;

namespace ASI.Basecode.Services.ServiceModels;

public sealed record UpcomingRegistrationPreview(int RegistrationId, int EventId, string EventTitle, DateTime StartsAt);
