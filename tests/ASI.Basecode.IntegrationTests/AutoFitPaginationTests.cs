using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class AutoFitPaginationTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 4, 2)]
    [InlineData(4, 4, 3)]
    [InlineData(0, 6, 2)]
    [InlineData(-8, 6, 2)]
    [InlineData(4, int.MaxValue, 1717986917)]
    public async Task LegacyGetRedirectsToPageContainingOriginalFirstItem(int size, int page, int expected)
    {
        var context = Request("GET", "Member", "Index", $"?search=Ana&pageSize={size}&page={page}&fit=2");
        var called = false;
        await AutoFitPagination.NormalizeAsync(context, () => { called = true; return Task.CompletedTask; });
        Assert.False(called);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        var location = context.Response.Headers.Location.ToString();
        Assert.Contains("search=Ana", location);
        Assert.Contains("pageSize=5", location);
        Assert.Contains($"page={expected}", location);
        Assert.Contains("fit=2", location);
    }

    [Fact]
    public async Task DossierRetainsItsPreviouslyClampedPageNumber()
    {
        var context = Request("GET", "event", "details", "?pageSize=2&page=4");
        await AutoFitPagination.NormalizeAsync(context, () => Task.CompletedTask);
        Assert.Contains("page=4", context.Response.Headers.Location.ToString());
        Assert.Contains("pageSize=5", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task LowercaseRoutesNormalizeLikeConventionalRoutes()
    {
        var context = Request("GET", "registrationreview", "index", "?pageSize=2&page=4");
        await AutoFitPagination.NormalizeAsync(context, () => Task.CompletedTask);
        Assert.Contains("page=2", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task PostNormalizesFormPageBeforeBindingWithoutRedirectingOrLosingFields()
    {
        var context = Request("POST", "RegistrationReview", "ApproveSelected", "?id=3&pageSize=2&page=2&status=pending");
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["page"] = "4", ["ids"] = new[] { "11", "12" } });
        var called = false;
        await AutoFitPagination.NormalizeAsync(context, () => { called = true; return Task.CompletedTask; });
        Assert.True(called);
        Assert.Equal("2", context.Request.Form["page"].ToString());
        Assert.Equal(new[] { "11", "12" }, context.Request.Form["ids"].ToArray());
        Assert.Equal("2", context.Request.Query["page"].ToString());
        Assert.Equal("5", context.Request.Query["pageSize"].ToString());
        Assert.Equal("pending", context.Request.Query["status"].ToString());
    }

    [Fact]
    public async Task DossierPostPreservesItsPreviouslyDisplayedPage()
    {
        var context = Request("POST", "Event", "ApproveSelected", "?id=3&pageSize=2&page=4");
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["page"] = "4" });
        await AutoFitPagination.NormalizeAsync(context, () => Task.CompletedTask);
        Assert.Equal("4", context.Request.Form["page"].ToString());
        Assert.Equal("5", context.Request.Query["pageSize"].ToString());
    }

    [Theory]
    [InlineData("GET", "CheckIn", "Index")]
    [InlineData("GET", "Event", "Cover")]
    [InlineData("POST", "Event", "Publish")]
    [InlineData("GET", "MyEvents", "Index")]
    public async Task FixedAndUnrelatedActionsAreUntouched(string method, string controller, string action)
    {
        var context = Request(method, controller, action, "?pageSize=2&page=4");
        var called = false;
        await AutoFitPagination.NormalizeAsync(context, () => { called = true; return Task.CompletedTask; });
        Assert.True(called);
        Assert.Equal("?pageSize=2&page=4", context.Request.QueryString.Value);
    }

    private static DefaultHttpContext Request(string method, string controller, string action, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/" + controller;
        context.Request.RouteValues["controller"] = controller;
        context.Request.RouteValues["action"] = action;
        context.Request.QueryString = new QueryString(query);
        return context;
    }
}
