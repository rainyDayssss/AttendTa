using System;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Immutable values captured for rendering a certificate document.</summary>
public sealed record CertificatePdfSnapshot(
    string RecipientName,
    string EventTitle,
    DateTime EventStartsAt,
    DateTime IssuedAt,
    string CertificateNumber);
