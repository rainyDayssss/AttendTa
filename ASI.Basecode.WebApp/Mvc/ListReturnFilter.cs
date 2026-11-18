using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ASI.Basecode.WebApp.Mvc;

/// <summary>Native list POSTs return to the originating search/page after the command executes.</summary>
public sealed class ListReturnFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.Method == "POST" && request.HasFormContentType && context.Result is RedirectToActionResult or RedirectToRouteResult)
        {
            var form = await request.ReadFormAsync(context.HttpContext.RequestAborted);
            var target = form["uiReturnUrl"].ToString();
            if (Models.ListNavigation.IsLocal(target)) context.Result = new LocalRedirectResult(target);
        }
        await next();
    }
}
