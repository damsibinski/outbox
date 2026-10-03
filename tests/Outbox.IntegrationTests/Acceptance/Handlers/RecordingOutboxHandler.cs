namespace Outbox.IntegrationTests.Acceptance.Handlers;

internal sealed class RecordingOutboxHandler(HandlerProbe probe) : IOutboxHandler
{
    public Task<Result> HandleAsync<T>(T payload, IDictionary<string, string> headers)
    {
        if (payload is not AcceptancePayload acceptancePayload)
            return Task.FromResult<Result>(new Failure("acceptance.unexpected-payload"));

        probe.Record(acceptancePayload, headers);
        return Task.FromResult(probe.Result);
    }
}
