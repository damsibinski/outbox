namespace Outbox.IntegrationTests.Acceptance.Support;

internal static class OutboxAcceptanceData
{
    public static AcceptancePayload GetPayload() =>
        new("feature-a", AcceptanceStatus.Enabled);

    public static Dictionary<string, string> GetHeaders() =>
        new()
        {
            ["correlationId"] = "correlation-123"
        };
}
