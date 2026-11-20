using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace ASI.Basecode.WebApp.Models;

public static class ListNavigation
{
    public static string Reset(HttpRequest request, IEnumerable<string> filterNames, string targetPath = null, bool preserveStatus = false)
    {
        var changes = filterNames.Where(name => name != "eventId")
            .ToDictionary(name => name, _ => (string)null, StringComparer.OrdinalIgnoreCase);
        changes["search"] = null;
        if (!preserveStatus) changes["status"] = null;
        return Url(request, changes, targetPath);
    }

    public static bool IsLocal(string value) => !string.IsNullOrEmpty(value) && value[0] == '/'
        && (value.Length == 1 || value[1] is not ('/' or '\\')) && !value.Any(char.IsControl);

    public static string Url(HttpRequest request, IDictionary<string, string> changes, string targetPath = null)
    {
        var query = new QueryBuilder();
        foreach (var pair in request.Query)
        {
            if (ListReadOptionsFactory.IsRetiredFilter(request, pair.Key)) continue;
            if (pair.Key.Equals("page", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("fit", StringComparison.OrdinalIgnoreCase) || changes.ContainsKey(pair.Key)) continue;
            foreach (var value in pair.Value) query.Add(pair.Key, value);
        }
        foreach (var pair in changes) if (!string.IsNullOrWhiteSpace(pair.Value)) query.Add(pair.Key, pair.Value);
        var path = $"{request.PathBase}{request.Path}";
        if (request.Method == "POST" && request.HasFormContentType)
        {
            var origin = request.Form["uiReturnUrl"].ToString();
            if (IsLocal(origin)) path = origin.Split('?')[0];
        }
        return $"{targetPath ?? path}{query.ToQueryString()}";
    }
}
