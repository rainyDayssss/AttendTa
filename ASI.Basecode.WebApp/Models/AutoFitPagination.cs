using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;

namespace ASI.Basecode.WebApp.Models;

// Normalize before MVC binds page and before scoped list options are constructed. Keep this
// allowlist tied to actions that actually render an auto-fit list, not controller-wide routes.
public static class AutoFitPagination
{
    public static bool Applies(HttpRequest request)
    {
        var controller = request.RouteValues["controller"]?.ToString()?.ToLowerInvariant();
        var action = request.RouteValues["action"]?.ToString()?.ToLowerInvariant();
        if (HttpMethods.IsGet(request.Method))
            return (controller, action) switch
            {
                ("member" or "venue" or "eventtype" or "memberevents" or "accountrequest" or "adminaccounts" or "activitylog" or "registrationreview" or "certificateledger", "index") => true,
                ("event", "index" or "details") => true,
                ("reports", "participation") => true,
                ("myevents", "registrations" or "attendance" or "certificates") => true,
                _ => false
            };
        return HttpMethods.IsPost(request.Method) && (controller, action) switch
        {
            ("event" or "registrationreview" or "certificateledger", "approveselected") => true,
            ("accountrequest", "approveselected" or "approve" or "reject") => true,
            _ => false
        };
    }

    public static async Task NormalizeAsync(HttpContext context, Func<Task> next)
    {
        var request = context.Request;
        if (!Applies(request) || !int.TryParse(request.Query["pageSize"], out var size) || size >= 5)
        {
            await next();
            return;
        }

        IFormCollection form = null;
        if (HttpMethods.IsPost(request.Method) && request.HasFormContentType)
            form = await request.ReadFormAsync();

        // Form values precede route and query values in MVC's default value providers.
        var pageValue = form != null && form.ContainsKey("page") ? form["page"].ToString()
            : request.RouteValues.TryGetValue("page", out var routePage) ? routePage?.ToString()
            : request.Query["page"].ToString();
        var page = int.TryParse(pageValue, out var parsed) && parsed > 0 ? parsed : 1;
        // The dossier already used a five-row minimum before normalization was introduced.
        var action = request.RouteValues["action"]?.ToString();
        var dossier = string.Equals(request.RouteValues["controller"]?.ToString(), "Event", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(action, "Details", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "ApproveSelected", StringComparison.OrdinalIgnoreCase));
        var oldSize = dossier ? 5 : Math.Max(1, size);
        var normalizedPage = (int)(((long)(page - 1) * oldSize) / 5 + 1);
        var pageText = normalizedPage.ToString(CultureInfo.InvariantCulture);

        var query = new QueryBuilder();
        foreach (var pair in request.Query)
        {
            if (pair.Key.Equals("pageSize", StringComparison.OrdinalIgnoreCase)) query.Add("pageSize", "5");
            else if (pair.Key.Equals("page", StringComparison.OrdinalIgnoreCase)) query.Add("page", pageText);
            else foreach (var value in pair.Value) query.Add(pair.Key, value);
        }
        if (!request.Query.ContainsKey("page")) query.Add("page", pageText);
        var normalizedQuery = query.ToQueryString();

        if (HttpMethods.IsGet(request.Method))
        {
            context.Response.Redirect($"{request.PathBase}{request.Path}{normalizedQuery}");
            return;
        }

        request.QueryString = normalizedQuery;
        if (request.RouteValues.ContainsKey("page")) request.RouteValues["page"] = pageText;
        if (form != null && form.ContainsKey("page"))
        {
            var fields = new Dictionary<string, StringValues>(form, StringComparer.OrdinalIgnoreCase) { ["page"] = pageText };
            request.Form = new FormCollection(fields, form.Files);
        }
        await next();
    }
}
