using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.WebApp.Models;

public sealed record ListFilterOption(string Value, string Label);
public sealed record ListFilterField(string Name, string Label, string Type = "text", IReadOnlyList<ListFilterOption> Options = null, bool AllowEmpty = true);

/// <summary>Each Razor surface declares its filters; the shared partial only renders controls.</summary>
public sealed record ListToolbarViewModel(string SearchLabel, IReadOnlyList<ListFilterField> Fields = null)
{
    public static ListFilterField Status(params ListFilterOption[] options) => new("filter", "Status", Options: options);
    public static ListFilterField Course => new("course", "Course", Options: ASI.Basecode.Resources.Constants.ProfileOptions.Courses.Select(value => new ListFilterOption(value, value)).ToArray());
    public static ListFilterField Year => new("year", "Year", Options: ASI.Basecode.Resources.Constants.ProfileOptions.YearLevels.Select(value => new ListFilterOption(value.ToString(), value.ToString())).ToArray());
}
