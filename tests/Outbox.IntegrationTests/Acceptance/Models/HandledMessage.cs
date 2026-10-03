namespace Outbox.IntegrationTests.Acceptance.Models;

internal sealed record HandledMessage(
    AcceptancePayload Payload,
    IReadOnlyDictionary<string, string> Headers);
