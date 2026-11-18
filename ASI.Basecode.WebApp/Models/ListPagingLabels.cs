using System;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace ASI.Basecode.WebApp.Models;

public static class ListPagingLabels
{
    public static string Noun(string controller, string action, int count, string custom = null)
    {
        var noun = custom ?? controller switch {
            "Member" => "member", "AccountRequest" => "request", "AdminAccounts" => "account",
            "Venue" => "venue", "EventType" => "event type", "ActivityLog" => "entry",
            "Event" when action == "Details" => "registration", "Event" or "MemberEvents" => "event",
            "RegistrationReview" => "registration", "CertificateLedger" => "review", "Reports" or "CheckIn" => "participant",
            "MyEvents" when action == "Certificates" => "certificate", "MyEvents" when action == "Attendance" => "attendance record",
            "MyEvents" => "registration", _ => "record"
        };
        return count == 1 ? noun : noun == "entry" ? "entries" : noun + "s";
    }

    public static bool IsFiltered(HttpRequest request)
    {
        if (string.Equals(request.RouteValues["controller"]?.ToString(), "CertificateLedger", StringComparison.OrdinalIgnoreCase))
        {
            var options = ListReadOptionsFactory.From(request);
            return !string.IsNullOrWhiteSpace(options.Search) || !string.IsNullOrWhiteSpace(options.Filter);
        }

        return request.Query.Any(pair =>
            !ListReadOptionsFactory.IsRetiredFilter(request, pair.Key) && pair.Key is "search" or "filter" or "status" or "course" or "year" or "type" or "typeId" or "role" or "from" or "to"
            && !string.IsNullOrWhiteSpace(pair.Value) && !string.Equals(pair.Value, "all", StringComparison.OrdinalIgnoreCase));
    }
}
