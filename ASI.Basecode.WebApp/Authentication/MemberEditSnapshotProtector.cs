using System;
using System.Security.Cryptography;
using System.Text.Json;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.DataProtection;

namespace ASI.Basecode.WebApp.Authentication
{
    /// <summary>Protects the original member-edit state against client modification.</summary>
    public sealed class MemberEditSnapshotProtector
    {
        private readonly IDataProtector _protector;

        public MemberEditSnapshotProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("AttendTa.MemberEditSnapshot.v1");
        }

        public string Protect(MemberEditSnapshot snapshot) =>
            _protector.Protect(JsonSerializer.Serialize(snapshot));

        public MemberEditSnapshot Unprotect(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            try
            {
                return JsonSerializer.Deserialize<MemberEditSnapshot>(_protector.Unprotect(token));
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException or ArgumentException)
            {
                return null;
            }
        }
    }
}
