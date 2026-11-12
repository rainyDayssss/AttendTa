using System;
using System.Security.Cryptography;
using System.Text;
using ASI.Basecode.Services.Interfaces;
using Konscious.Security.Cryptography;

namespace ASI.Basecode.Services.Security
{
    /// <summary>
    /// Argon2id password hasher that writes PHC-encoded <c>$argon2id$</c> strings.
    /// </summary>
    /// <remarks>
    /// The memory, iteration and parallelism parameters are stored inside each PHC string, so
    /// changing the defaults stays compatible with hashes that were already written.
    /// </remarks>
    public sealed class Argon2PasswordHasher : IPasswordHasher
    {
        /// <summary>The default Argon2id memory cost in KiB (19 MiB).</summary>
        public const int DefaultMemorySize = 19456;

        /// <summary>The default Argon2id iteration count.</summary>
        public const int DefaultIterations = 2;

        /// <summary>The default Argon2id degree of parallelism.</summary>
        public const int DefaultDegreeOfParallelism = 1;

        /// <summary>The salt length in bytes.</summary>
        public const int SaltSize = 16;

        /// <summary>The derived hash length in bytes.</summary>
        public const int HashSize = 32;

        private const string Algorithm = "argon2id";
        private const int AlgorithmVersion = 19;

        private const int MinMemorySize = 8;
        private const int MaxMemorySize = 1048576;
        private const int MinIterations = 1;
        private const int MaxIterations = 64;
        private const int MinDegreeOfParallelism = 1;
        private const int MaxDegreeOfParallelism = 16;
        private const int MinSaltSize = 8;
        private const int MaxSaltSize = 64;
        private const int MinHashSize = 16;
        private const int MaxHashSize = 64;

        /// <inheritdoc />
        public string Hash(string password)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Derive(password, salt, DefaultMemorySize, DefaultIterations, DefaultDegreeOfParallelism, HashSize);

            return string.Join(
                "$",
                string.Empty,
                Algorithm,
                $"v={AlgorithmVersion}",
                $"m={DefaultMemorySize},t={DefaultIterations},p={DefaultDegreeOfParallelism}",
                ToBase64(salt),
                ToBase64(hash));
        }

        /// <inheritdoc />
        public bool Verify(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }

            if (!TryParse(hash, out var salt, out var expectedHash, out var memorySize, out var iterations, out var parallelism))
            {
                return false;
            }

            var actualHash = Derive(password, salt, memorySize, iterations, parallelism, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }

        private static byte[] Derive(string password, byte[] salt, int memorySize, int iterations, int parallelism, int hashSize)
        {
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                MemorySize = memorySize,
                Iterations = iterations,
                DegreeOfParallelism = parallelism
            };

            return argon2.GetBytes(hashSize);
        }

        private static bool TryParse(
            string hash,
            out byte[] salt,
            out byte[] expectedHash,
            out int memorySize,
            out int iterations,
            out int parallelism)
        {
            salt = null;
            expectedHash = null;
            memorySize = 0;
            iterations = 0;
            parallelism = 0;

            var parts = hash.Split('$');
            if (parts.Length != 6 || parts[0].Length != 0)
            {
                return false;
            }

            if (!string.Equals(parts[1], Algorithm, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(parts[2], $"v={AlgorithmVersion}", StringComparison.Ordinal))
            {
                return false;
            }

            var parameters = parts[3].Split(',');
            if (parameters.Length != 3)
            {
                return false;
            }

            if (!TryReadParameter(parameters[0], "m", out memorySize)
                || !TryReadParameter(parameters[1], "t", out iterations)
                || !TryReadParameter(parameters[2], "p", out parallelism))
            {
                return false;
            }

            if (memorySize < MinMemorySize || memorySize > MaxMemorySize
                || iterations < MinIterations || iterations > MaxIterations
                || parallelism < MinDegreeOfParallelism || parallelism > MaxDegreeOfParallelism)
            {
                return false;
            }

            // Konscious rejects a memory cost below 4 KiB per lane; catch it here so a corrupted
            // hash row returns false instead of throwing during sign-in.
            if (memorySize < 4 * parallelism)
            {
                return false;
            }

            if (!TryFromBase64(parts[4], out salt) || salt.Length < MinSaltSize || salt.Length > MaxSaltSize)
            {
                return false;
            }

            if (!TryFromBase64(parts[5], out expectedHash) || expectedHash.Length < MinHashSize || expectedHash.Length > MaxHashSize)
            {
                return false;
            }

            return true;
        }

        private static bool TryReadParameter(string value, string name, out int result)
        {
            result = 0;
            return value.StartsWith($"{name}=", StringComparison.Ordinal)
                && int.TryParse(value.AsSpan(name.Length + 1), out result);
        }

        private static string ToBase64(byte[] value)
        {
            return Convert.ToBase64String(value).TrimEnd('=');
        }

        private static bool TryFromBase64(string value, out byte[] result)
        {
            result = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var padding = (4 - (value.Length % 4)) % 4;
            try
            {
                result = Convert.FromBase64String(value + new string('=', padding));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
