using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Validates the separately delivered credential handover before any provisioning.</summary>
internal static class DemoCredentials
{
    internal static readonly string[] AccountIds =
    [
        "admin", "officer", "gabutan", "taguiam", "talisic", "sinco", "pinca",
        "member06", "member07", "member08", "member09", "member10"
    ];
    internal static readonly string[] CredentialIds = [.. AccountIds, "applicant"];

    internal static IReadOnlyDictionary<string, string> Parse(string json)
    {
        // Do not include input or parser exception details in errors: the input contains secrets.
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException();

            var accounts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!CredentialIds.Contains(property.Name, StringComparer.Ordinal)
                    || property.Value.ValueKind != JsonValueKind.String
                    || !accounts.TryAdd(property.Name, property.Value.GetString()))
                    throw new InvalidDataException();
            }

            if (accounts.Count != CredentialIds.Length
                || accounts.Values.Any(value => value is null || value.Length is < 8 or > 128
                    || !Regex.IsMatch(value, @"[A-Za-z]", RegexOptions.CultureInvariant)
                    || !Regex.IsMatch(value, @"[0-9]", RegexOptions.CultureInvariant)
                    || value.Any(char.IsControl))
                || accounts.Values.Distinct(StringComparer.Ordinal).Count() != CredentialIds.Length)
                throw new InvalidDataException();
            return accounts;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException)
        {
            throw new InvalidDataException("Invalid demo credential input; provide exactly the required accounts with distinct valid passwords.");
        }
    }
}
