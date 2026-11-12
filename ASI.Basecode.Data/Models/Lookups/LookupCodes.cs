namespace ASI.Basecode.Data.Models.Lookups;

public enum RoleCode : byte
{
    Admin = 1,
    Officer = 2,
    Member = 3
}

public enum AccountRequestStatusCode : byte
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum MembershipStatusCode : byte
{
    Active = 1,
    Inactive = 2
}

public enum EventStatusCode : byte
{
    Draft = 1,
    Published = 2,
    Cancelled = 3,
    Completed = 4
}

public enum RegistrationStatusCode : byte
{
    Pending = 1,
    Confirmed = 2,
    Waitlisted = 3,
    Cancelled = 4
}

public enum CheckInMethodCode : byte
{
    QrCode = 1,
    ManualSearch = 2,
    StudentNumber = 3
}

public enum CertificateStatusCode : byte
{
    PendingApproval = 1,
    Eligible = 2,
    NotEligible = 3,
    Issued = 4
}

public enum ActorTypeCode : byte
{
    User = 1,
    System = 2
}
