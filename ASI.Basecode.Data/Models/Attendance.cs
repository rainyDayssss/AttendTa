using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

public class Attendance
{
    public int AttendanceId { get; set; }
    public int RegistrationId { get; set; }
    public CheckInMethodCode CheckInMethodId { get; set; }
    public int RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; }
    public int? InvalidatedBy { get; set; }
    public DateTime? InvalidatedAt { get; set; }
    public string InvalidationReason { get; set; }
}
