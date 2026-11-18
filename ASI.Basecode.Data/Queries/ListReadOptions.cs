using System;

namespace ASI.Basecode.Data.Queries;

/// <summary>Immutable, request-scoped list preferences. No HTTP dependency or mutable global state.</summary>
public sealed record ListReadOptions
{
    public string Surface { get; init; }
    public int PageSize { get; init; } = 5;
    public string Search { get; init; }
    public string Filter { get; init; }
    public string Course { get; init; }
    public int? Year { get; init; }
    public string Type { get; init; }
    public int? TypeId { get; init; }
    public string Role { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public bool IsSurface(params string[] names) => Array.Exists(names, name => string.Equals(Surface, name, StringComparison.OrdinalIgnoreCase));
}
