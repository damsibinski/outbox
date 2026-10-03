namespace Outbox.IntegrationTests.Acceptance.Handlers;

internal sealed class HandlerProbe
{
    private readonly List<HandledMessage> _deliveries = [];

    public Result Result { get; set; } = new Success();

    public IReadOnlyList<HandledMessage> Deliveries => _deliveries;

    public void Record(AcceptancePayload payload, IDictionary<string, string> headers)
    {
        _deliveries.Add(new HandledMessage(
            payload,
            new Dictionary<string, string>(headers)));
    }
}
