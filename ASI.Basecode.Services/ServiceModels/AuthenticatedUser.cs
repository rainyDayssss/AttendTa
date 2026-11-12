namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// The verified identity of a signed-in account.
    /// </summary>
    public sealed class AuthenticatedUser
    {
        /// <summary>
        /// Gets the <c>users.user_id</c>.
        /// </summary>
        public int UserId { get; init; }

        /// <summary>
        /// Gets the account email, which is also the login identifier.
        /// </summary>
        public string Email { get; init; }

        /// <summary>
        /// Gets the account full name.
        /// </summary>
        public string FullName { get; init; }

        /// <summary>
        /// Gets the lowercase role code (<c>admin</c>, <c>officer</c> or <c>member</c>).
        /// </summary>
        public string RoleCode { get; init; }
    }
}
