namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Result of a successful registration submission. A full event still creates a pending row;
/// <paramref name="SeatNotGuaranteed"/> tells the UI to warn that no seat is reserved.
/// </summary>
public sealed record MemberRegistrationResult(int RegistrationId, bool SeatNotGuaranteed);
