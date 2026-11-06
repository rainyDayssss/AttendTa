namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The single demo-data switch and its two companion keys. Off in every committed
/// <c>appsettings*.json</c>; the demo host sets it in the process environment. (Register M-01.)
/// </summary>
public sealed class DemoDataOptions
{
    public const string SectionName = "DemoData";

    /// <summary>Turns every demo provider on. Guarded at startup against the wrong database.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// A second database name the guard accepts besides <c>AttendTa_Demo</c>, for a builder's
    /// disposable local database. Never committed.
    /// </summary>
    public string AllowDatabase { get; set; }

    /// <summary>The HMAC key for demo pass tokens. Set only in the demo machine's environment.</summary>
    public string TokenKey { get; set; }
}
