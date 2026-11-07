namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Suggested and bounded values for the profile fields that the membership request and the
    /// member editor collect. Course stays free text (the specification keeps it open) but is
    /// offered as a suggestion list; year level, section and membership type are true dropdowns.
    /// </summary>
    public static class ProfileOptions
    {
        /// <summary>The organization's current program list; add a program here when it changes.</summary>
        public static readonly IReadOnlyList<string> Courses = new[]
        {
            "BS Information Technology",
            "BS Computer Science",
            "BS Information Systems"
        };

        /// <summary>Positive year levels offered by the form.</summary>
        public static readonly IReadOnlyList<int> YearLevels = new[] { 1, 2, 3, 4, 5 };

        /// <summary>Sections offered by the form.</summary>
        public static readonly IReadOnlyList<string> Sections = new[] { "A", "B", "C", "D", "E", "F" };

        /// <summary>Membership types the organization uses.</summary>
        public static readonly IReadOnlyList<string> MembershipTypes = new[] { "Regular", "Officer" };
    }
}
