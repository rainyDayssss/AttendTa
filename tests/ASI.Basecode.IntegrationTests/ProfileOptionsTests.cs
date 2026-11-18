using System.Text.RegularExpressions;
using ASI.Basecode.Resources.Constants;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Guards the one source of profile choices and patterns: the lists a form offers must satisfy the
/// patterns the annotations enforce, so editing one without the other fails here first.
/// </summary>
public class ProfileOptionsTests
{
    [Fact]
    public void EveryOfferedSectionMatchesTheSectionPattern()
    {
        var pattern = new Regex(ProfileOptions.SectionPattern);

        Assert.All(ProfileOptions.Sections, section => Assert.Matches(pattern, section));
        Assert.DoesNotMatch(pattern, "D");
        Assert.DoesNotMatch(pattern, "G");
        Assert.DoesNotMatch(pattern, "3A");
        Assert.Matches(pattern, "b");
    }

    [Fact]
    public void EveryOfferedYearLevelFallsInsideTheRangeTheAnnotationsUse()
    {
        Assert.All(ProfileOptions.YearLevels, year => Assert.InRange(year, ProfileOptions.MinYearLevel, ProfileOptions.MaxYearLevel));
        Assert.Equal(ProfileOptions.MaxYearLevel - ProfileOptions.MinYearLevel + 1, ProfileOptions.YearLevels.Count);
    }

    [Fact]
    public void EveryOfferedCourseMatchesTheCoursePattern()
    {
        var pattern = new Regex(ProfileOptions.CoursePattern);

        Assert.All(ProfileOptions.Courses, course => Assert.Matches(pattern, course));
        Assert.DoesNotMatch(pattern, "BSIS");
    }

    [Fact]
    public void StudentNumberPatternKeepsLeadingZeroesAndRejectsWrongLengths()
    {
        var pattern = new Regex(ProfileOptions.StudentNumberPattern);

        Assert.Matches(pattern, "00000001");
        Assert.Matches(pattern, "31234567");
        Assert.DoesNotMatch(pattern, "1234567");
        Assert.DoesNotMatch(pattern, "123456789");
        Assert.DoesNotMatch(pattern, "2023-0123");
    }

    [Fact]
    public void MobileNumberPatternAcceptsEmptyAndPhilippineNumbersOnly()
    {
        var pattern = new Regex(ProfileOptions.MobileNumberPattern);

        Assert.Matches(pattern, "09171234567");
        Assert.DoesNotMatch(pattern, "12345678901");
        Assert.DoesNotMatch(pattern, "0917-123-4567");
        Assert.DoesNotMatch(pattern, "0917123456");
    }
}
