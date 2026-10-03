namespace Outbox.IntegrationTests.Infrastructure;

internal static class OutboxTestData
{
    public static Payload CreatePayload(
        string data = "integration-test-payload",
        string type = "integration.test.event",
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return CreatePayload(
            data,
            type,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            headers);
    }

    public static Payload CreatePayload(
        DateTimeOffset scheduledAt,
        string data = "integration-test-payload",
        string type = "integration.test.event",
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return CreatePayload(data, type, scheduledAt, headers);
    }

    public static Payload CreatePayload(
        string data,
        string type,
        DateTimeOffset scheduledAt,
        IReadOnlyDictionary<string, string>? headers)
    {
        var resolvedHeaders = headers is null
            ? new Dictionary<string, string> { ["correlation-id"] = Guid.NewGuid().ToString("N") }
            : new Dictionary<string, string>(headers);

        return new Payload(data, type, resolvedHeaders, scheduledAt);
    }
}
