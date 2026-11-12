namespace ASI.Basecode.Services.Interfaces;

public interface IAuditValues { }

public sealed record AuditEntry(string Action, string EntityType, int EntityId, string Reason = null, IAuditValues OldValues = null, IAuditValues NewValues = null);

public interface IActivityLogWriter
{
    void Append(AuditEntry entry);
}
