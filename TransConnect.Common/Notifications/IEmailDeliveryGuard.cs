namespace TransConnect.Common.Notifications;

public interface IEmailDeliveryGuard
{
    string SourceType { get; }
    Task<bool> CanDeliverAsync(Guid sourceId, string recipient, CancellationToken ct);
}

public static class EmailSourceTypes
{
    public const string VerificationCode = "identity.verification-code";
    public const string Invitation = "operator.invitation";
    public const string Engagement = "operator.engagement";
}
