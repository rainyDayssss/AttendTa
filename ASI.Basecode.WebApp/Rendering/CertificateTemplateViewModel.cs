namespace ASI.Basecode.WebApp.Rendering;

/// <summary>Encoded, self-contained values consumed by the fixed certificate Razor view.</summary>
public sealed record CertificateTemplateViewModel(
    string RecipientName,
    string EventTitle,
    string EventStartsAtManila,
    string IssuedAtManila,
    string CertificateNumber,
    string LogoDataUri,
    string CinzelRegularDataUri,
    string CinzelSemiboldDataUri,
    string RecipientSizeClass,
    string EventSizeClass,
    string NumberSizeClass,
    string SignatoryName,
    string SignatoryTitle,
    string SignatureDataUri);
