using Konscious.Security.Cryptography;
using System;
using System.Security.Cryptography;
using System.Text;

namespace ASI.Basecode.Services.Manager
{
    public static class PasswordManager
    {
        public static string HashPassword(string password)
        {
            if (password == null || password.Length > 128) throw new ArgumentException("Password must be 128 characters or fewer.", nameof(password));
            var salt = RandomNumberGenerator.GetBytes(16);
            var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = 2,
                Iterations = 3,
                MemorySize = 65536
            };
            var hash = argon.GetBytes(32);
            return $"$argon2id$v=19$m=65536,t=3,p=2${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string encoded)
        {
            if (string.IsNullOrEmpty(password) || password.Length > 128 || string.IsNullOrWhiteSpace(encoded) || encoded.Length > 256) return false;
            try
            {
                var parts = encoded.Split('$');
                if (parts.Length != 6 || parts[1] != "argon2id" || parts[2] != "v=19" || parts[3] != "m=65536,t=3,p=2") return false;
                var salt = Convert.FromBase64String(parts[4]);
                var expected = Convert.FromBase64String(parts[5]);
                if (salt.Length != 16 || expected.Length != 32) return false;
                var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
                {
                    Salt = salt,
                    DegreeOfParallelism = 2,
                    Iterations = 3,
                    MemorySize = 65536
                };
                return CryptographicOperations.FixedTimeEquals(argon.GetBytes(32), expected);
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is InvalidOperationException) { return false; }
        }
    }
}
