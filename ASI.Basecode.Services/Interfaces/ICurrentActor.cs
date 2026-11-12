namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// The authenticated actor of the current request. Services take the actor only from here;
    /// request models never carry actor ids or roles.
    /// </summary>
    public interface ICurrentActor
    {
        /// <summary>
        /// Gets a value indicating whether the current request is authenticated.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Gets the authenticated <c>users.user_id</c>.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when the request is anonymous.</exception>
        int UserId { get; }

        /// <summary>
        /// Gets the lowercase role code of the authenticated actor, or <c>null</c> when anonymous.
        /// </summary>
        string Role { get; }
    }
}
