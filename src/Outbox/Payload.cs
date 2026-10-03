namespace Outbox;

public sealed record Payload(
    string Data,
    string Type,
    IDictionary<string, string> Headers,
    DateTimeOffset ScheduledAt);
