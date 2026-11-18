using System.Globalization;

namespace ASI.Basecode.IntegrationTests.Support;

/// <summary>
/// Profile values that satisfy the shared rules. One counter per test run keeps student numbers
/// unique without a GUID, because the shared rule is exactly eight digits.
/// </summary>
internal static class ProfileFixtures
{
    private static int studentNumberSeed = 20000000;

    /// <summary>Eight digits, so the value satisfies <c>ProfileOptions.StudentNumberPattern</c>.</summary>
    public static string NewStudentNumber() =>
        Interlocked.Increment(ref studentNumberSeed).ToString("D8", CultureInfo.InvariantCulture);
}
