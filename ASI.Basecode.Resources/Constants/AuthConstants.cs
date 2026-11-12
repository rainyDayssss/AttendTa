namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Named authorization policies used by endpoints.
    /// </summary>
    public static class Policies
    {
        /// <summary>Officer or Administrator.</summary>
        public const string Management = "Management";

        /// <summary>Administrator only.</summary>
        public const string Administrator = "Administrator";

        /// <summary>Member only.</summary>
        public const string Member = "Member";
    }

    /// <summary>
    /// Lowercase role codes from the <c>roles</c> lookup table.
    /// </summary>
    public static class RoleCodes
    {
        /// <summary>Administrator role code.</summary>
        public const string Admin = "admin";

        /// <summary>Officer role code.</summary>
        public const string Officer = "officer";

        /// <summary>Member role code.</summary>
        public const string Member = "member";
    }

    /// <summary>
    /// Authentication cookie settings shared by the WebApp and its tests.
    /// </summary>
    public static class AuthCookie
    {
        /// <summary>The authentication cookie name.</summary>
        public const string Name = "AttendTa.Auth";

        /// <summary>The non-sliding lifetime of the authentication cookie.</summary>
        public const int LifetimeHours = 8;
    }
}
