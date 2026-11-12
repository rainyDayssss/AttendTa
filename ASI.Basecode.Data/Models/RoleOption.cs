using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

/// <summary>
/// Display-only role data for the role select. The numeric role id stays in the data layer;
/// the form posts the lowercase <see cref="Code"/> so a caller cannot invent a role id.
/// </summary>
public sealed record RoleOption
{
    public RoleCode RoleId { get; init; }
    public string Code { get; init; }
    public string Label { get; init; }
}
