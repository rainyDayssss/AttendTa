using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class ListPresentationTests
{
    [Theory]
    [InlineData(@"np:\\.\pipe\LOCALDB#7C5BE61C\tsql\query", true)]
    [InlineData(@"np:\\remote\pipe\LOCALDB#7C5BE61C\tsql\query", false)]
    [InlineData(@"np:\\.\pipe\LOCALDB#7C5BE61C\tsql\query\extra", false)]
    public void IntegrationPipeSupportRemainsLocalOnly(string source, bool allowed) =>
        Assert.Equal(allowed, SqlServerDatabaseFixture.IsLocalSqlServer(source));

    [Theory]
    [InlineData(1, 5)]
    [InlineData(12, 12)]
    [InlineData(50, 25)]
    public void DossierUsesFiveAsMinimumAndAllowsTallerViewports(int requested, int expected)
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["controller"] = "Event";
        context.Request.RouteValues["action"] = "Details";
        context.Request.QueryString = new($"?pageSize={requested}&fit=1&search=Ana");
        var options = ListReadOptionsFactory.From(context.Request);
        Assert.Equal(expected, options.PageSize);
        Assert.Equal("Ana", options.Search);
    }

    [Fact]
    public void FixedCheckInLookupRetainsRequestedPageSize()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["controller"] = "CheckIn";
        context.Request.QueryString = new("?pageSize=2&page=2");
        Assert.Equal(2, ListReadOptionsFactory.From(context.Request).PageSize);
    }

    [Fact]
    public void ResetClearsSearchAndListFiltersButPreservesEventAndSection()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/Certificates";
        context.Request.QueryString = new("?search=Ana&status=eligible&eventId=7&tab=issued&pageSize=12&page=3&fit=2");
        var url = ListNavigation.Reset(context.Request, new[] { "eventId", "status" });
        Assert.Contains("eventId=7", url);
        Assert.Contains("tab=issued", url);
        Assert.Contains("pageSize=12", url);
        Assert.DoesNotContain("search=", url);
        Assert.DoesNotContain("status=", url);
        Assert.DoesNotContain("page=", url);
        Assert.DoesNotContain("fit=", url);
    }

    [Fact]
    public void ResetPreservesStatusSectionsButClearsOtherCriteriaAndPaging()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/AccountRequest";
        context.Request.QueryString = new("?status=all&search=Ana&course=BSIT&year=2&page=3&fit=3d");
        var url = ListNavigation.Reset(context.Request, new[] { "course", "year" }, preserveStatus: true);
        Assert.Contains("status=all", url);
        Assert.DoesNotContain("search=", url);
        Assert.DoesNotContain("course=", url);
        Assert.DoesNotContain("year=", url);
        Assert.DoesNotContain("page=", url);
        Assert.DoesNotContain("fit=", url);
    }

    [Fact]
    public void BrowseDefaultsToActiveAndRetiredControlsCannotKeepFiltering()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["controller"] = "MemberEvents";
        context.Request.QueryString = new("?type=old&from=2026-01-01&to=2026-12-31");
        var options = ListReadOptionsFactory.From(context.Request);
        Assert.Equal("Published", options.Filter);
        Assert.Null(options.Type);
        Assert.Null(options.From);
        Assert.Null(options.To);
        Assert.False(ListPagingLabels.IsFiltered(context.Request));
        var url = ListNavigation.Url(context.Request, new Dictionary<string, string> { ["filter"] = "Completed" });
        Assert.DoesNotContain("from=", url);
        Assert.DoesNotContain("type=", url);
        context.Request.QueryString = new("?filter=Completed");
        Assert.Equal("Completed", ListReadOptionsFactory.From(context.Request).Filter);
    }

    [Fact]
    public void CourseChoicesRejectUnsupportedProgramsAtBothFormBoundaries()
    {
        foreach (var model in new object[] {
            new ASI.Basecode.Services.ServiceModels.AccountRequestViewModel { Course = "BSIS" },
            new ASI.Basecode.Services.ServiceModels.MemberViewModel { Course = "BSIS" }
        })
        {
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, new(model), results, true);
            Assert.Contains(results, result => result.MemberNames.Contains("Course"));
        }
        Assert.Equal("BSIT", ASI.Basecode.Resources.Constants.ProfileOptions.NormalizeCourse(" BS Information Technology "));
        Assert.Equal("BSCS", ASI.Basecode.Resources.Constants.ProfileOptions.NormalizeCourse("bscs"));
        Assert.Equal(new[] { "BSCS", "BSIT" }, ListToolbarViewModel.Course.Options!.Select(item => item.Value));
        Assert.Equal(new[] { "1", "2", "3", "4" }, ListToolbarViewModel.Year.Options!.Select(item => item.Value));
    }

    [Fact]
    public void QueryBindingTrimsFiltersBoundsSizeAndUsesManilaCalendarDays()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["controller"] = "Member";
        context.Request.QueryString = new("?search=%20Ana%20&pageSize=999&year=2&from=2026-11-15&to=2026-11-16");
        var result = ListReadOptionsFactory.From(context.Request);
        Assert.Equal("Ana", result.Search);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(2, result.Year);
        Assert.Equal("Member", result.Surface);
        Assert.Equal(new DateTime(2026, 11, 14, 16, 0, 0, DateTimeKind.Utc), result.From);
    }

    [Fact]
    public void ChangingStatusPreservesOtherCriteriaAndRemovesStalePageAndFit()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/Member";
        context.Request.QueryString = new("?search=Ana&course=BSIT&year=2&page=3&fit=1&pageSize=2&status=active");
        var url = ListNavigation.Url(context.Request, new Dictionary<string, string> { ["status"] = "inactive" });
        Assert.Contains("search=Ana", url);
        Assert.Contains("course=BSIT", url);
        Assert.Contains("year=2", url);
        Assert.Contains("status=inactive", url);
        Assert.DoesNotContain("page=", url);
        Assert.DoesNotContain("fit=", url);
    }

    [Theory]
    [InlineData("/Member?search=Ana", true)]
    [InlineData("https://example.com", false)]
    [InlineData("//example.com", false)]
    [InlineData("/\\example.com", false)]
    [InlineData("/Member\r\nLocation:bad", false)]
    public async Task NativePostReturnsOnlyToALocalOrigin(string target, bool local)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["uiReturnUrl"] = target });
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var filters = new List<IFilterMetadata>();
        var original = new RedirectToActionResult("Index", null, null);
        var executing = new ResultExecutingContext(action, filters, original, new object());
        await new ListReturnFilter().OnResultExecutionAsync(executing, () => Task.FromResult(new ResultExecutedContext(action, filters, executing.Result, new object())));
        if (local) Assert.Equal(target, Assert.IsType<LocalRedirectResult>(executing.Result).Url);
        else Assert.Same(original, executing.Result);
    }

    [Fact]
    public void LabelsDescribeRealSingularPluralAndFilteredRecords()
    {
        Assert.Equal("request", ListPagingLabels.Noun("AccountRequest", "Index", 1, "request"));
        Assert.Equal("requests", ListPagingLabels.Noun("AccountRequest", "Index", 6, "request"));
        Assert.Equal("entries", ListPagingLabels.Noun("ActivityLog", "Index", 0));
        var context = new DefaultHttpContext();
        context.Request.QueryString = new("?status=all&page=2");
        Assert.False(ListPagingLabels.IsFiltered(context.Request));
    }

    [Fact]
    public void CertificateFilterAllIsCountedOnlyWhenItIsApplied()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["controller"] = "CertificateLedger";
        context.Request.QueryString = new("?filter=all");
        Assert.True(ListPagingLabels.IsFiltered(context.Request));
        context.Request.QueryString = new("?Filter=all");
        Assert.True(ListPagingLabels.IsFiltered(context.Request));
        context.Request.QueryString = new("?status=Eligible");
        Assert.False(ListPagingLabels.IsFiltered(context.Request));
        context.Request.QueryString = new("?tab=issued&filter=all");
        Assert.False(ListPagingLabels.IsFiltered(context.Request));
        context.Request.QueryString = new("?filter=Issued");
        Assert.False(ListPagingLabels.IsFiltered(context.Request));
        context.Request.RouteValues["controller"] = "MemberEvents";
        context.Request.QueryString = new("?filter=all");
        Assert.False(ListPagingLabels.IsFiltered(context.Request));
    }
}
