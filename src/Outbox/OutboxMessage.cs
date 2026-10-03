namespace Outbox;

public sealed record OutboxMessage(
    Guid Id,
    string Data,
    string Type,
    string Headers,
    DateTime InsertDate,
    DateTimeOffset ScheduledAt);
