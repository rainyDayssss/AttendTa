namespace ASI.Basecode.WebApp.Rendering;

/// <summary>Approved identity used by the fixed official certificate template.</summary>
public sealed class CertificateSignatoryOptions
{
    public const string SectionName = "Certificates:Signatory";
    public const string ApprovedName = "Christian Kyle Ducay";
    public const string ApprovedTitle = "Organization President";
    public const string SignatureAssetPath = "img/christian-kyle-ducay-signature.png";

    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PackagedSignatureAsset { get; set; } = string.Empty;
}
