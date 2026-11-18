namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Rail group labels shared by the rail (<c>_NavLinks</c>) and the breadcrumb
    /// (<c>_Header</c>). Both partials render the same vocabulary, so the names live here
    /// instead of as literals in two files that must be renamed together.
    /// </summary>
    public static class NavGroups
    {
        /// <summary>Home and Browse events. Home is the group's root.</summary>
        public const string Overview = "Overview";

        /// <summary>Records the organization maintains.</summary>
        public const string Manage = "Manage";

        /// <summary>Work performed on events and the output it produces.</summary>
        public const string Operations = "Operations";

        /// <summary>The member rail's record destinations.</summary>
        public const string Records = "Records";
        public const string MyParticipation = "My participation";

        /// <summary>Personal account destinations.</summary>
        public const string Account = "Account";
    }
}
