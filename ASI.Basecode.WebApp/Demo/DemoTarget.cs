using System;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Restricts preliminary demo setup to a dedicated named LocalDB instance.</summary>
internal static class DemoTarget
{
    internal const string Instance = @"(localdb)\AttendTaDemo";
    internal const string Database = "AttendTa_Demo";

    internal static void ValidateMasterConnection(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!Regex.IsMatch(builder.DataSource, @"\A\(localdb\)\\AttendTaDemo(?:_[0-9a-f]{32})?\z",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                || !string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase)
                || !builder.IntegratedSecurity
                || builder.UserID.Length != 0
                || builder.Password.Length != 0
                || builder.AttachDBFilename.Length != 0
                || builder.FailoverPartner.Length != 0
                || builder.MultiSubnetFailover
                || builder.ApplicationIntent != ApplicationIntent.ReadWrite)
                throw new ArgumentException();
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("Demo setup requires integrated authentication to master on the dedicated local AttendTaDemo LocalDB instance.");
        }
    }
}
