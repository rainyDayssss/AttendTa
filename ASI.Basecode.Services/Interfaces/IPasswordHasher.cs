namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Hashes and verifies passwords. Hashes are PHC-encoded Argon2id strings.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Produces a PHC-encoded <c>$argon2id$</c> hash with a fresh salt.
        /// </summary>
        /// <param name="password">The plaintext password. It is never stored or logged.</param>
        /// <returns>The PHC-encoded hash.</returns>
        string Hash(string password);

        /// <summary>
        /// Verifies a plaintext password against a stored PHC-encoded hash in fixed time.
        /// </summary>
        /// <param name="password">The plaintext password.</param>
        /// <param name="hash">The stored PHC-encoded hash.</param>
        /// <returns><c>true</c> when the password matches; otherwise <c>false</c>.</returns>
        bool Verify(string password, string hash);
    }
}
