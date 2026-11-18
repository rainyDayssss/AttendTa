namespace ASI.Basecode.Services.ServiceModels;

public enum LoginStatus { Invalid, Pending, Authenticated }

public sealed record LoginOutcome(LoginStatus Status, AuthenticatedUser User = null);
