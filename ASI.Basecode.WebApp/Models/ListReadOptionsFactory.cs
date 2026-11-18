using System;
using System.Globalization;
using ASI.Basecode.Data.Queries;
using Microsoft.AspNetCore.Http;

namespace ASI.Basecode.WebApp.Models;

public static class ListReadOptionsFactory
{
    // Retired controls must not silently keep filtering through old links or hidden inputs.
    public static bool IsRetiredFilter(HttpRequest request, string key)
    {
        var surface = request.RouteValues["controller"]?.ToString();
        return surface switch {
            "Event" or "MemberEvents" or "ActivityLog" => key is "type" or "from" or "to",
            "MyEvents" or "Reports" => key == "filter",
            "CertificateLedger" => key == "filter" && (request.Query["tab"] == "issued" || request.Query["filter"] == "Issued"),
            _ => false
        };
    }

    public static ListReadOptions From(HttpRequest request)
    {
        if (request == null) return new();
        var query = request.Query;
        var surface = request.RouteValues["controller"]?.ToString() ?? request.Path.Value?.Trim('/').Split('/')[0];
        var action = request.RouteValues["action"]?.ToString();
        var dossier = string.Equals(surface, "Event", StringComparison.OrdinalIgnoreCase)
            && string.Equals(action, "Details", StringComparison.OrdinalIgnoreCase);
        var filter = Text("filter");
        if (surface == "MemberEvents") filter = string.Equals(filter, "Completed", StringComparison.OrdinalIgnoreCase) ? "Completed" : "Published";
        if (surface is "MyEvents" or "Reports" || surface == "CertificateLedger" && (Text("tab") == "issued" || filter == "Issued")) filter = null;
        string Text(string key) => string.IsNullOrWhiteSpace(query[key]) ? null : query[key].ToString().Trim();
        // Date filters describe Manila calendar days; list timestamps are stored in UTC.
        DateTime? Date(string key) => DateTime.TryParseExact(Text(key), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date.Year is >= 1900 and < 9999
            ? DateTime.SpecifyKind(date.AddHours(-8), DateTimeKind.Utc) : null;
        return new ListReadOptions
        {
            Surface = surface,
            PageSize = int.TryParse(Text("pageSize"), out var size) ? Math.Clamp(size, dossier ? 5 : 1, 25) : 5,
            Search = Text("search"), Filter = filter, Course = Text("course"),
            Year = int.TryParse(Text("year"), out var year) && year is >= 1 and <= 10 ? year : null,
            Type = surface is "Event" or "MemberEvents" or "ActivityLog" ? null : Text("type"),
            TypeId = surface == "Event" && int.TryParse(Text("typeId"), out var typeId) && typeId > 0 ? typeId : null,
            Role = Text("role"), From = surface is "Event" or "MemberEvents" or "ActivityLog" ? null : Date("from"), To = surface is "Event" or "MemberEvents" or "ActivityLog" ? null : Date("to")
        };
    }
}
