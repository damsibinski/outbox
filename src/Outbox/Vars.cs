using System.Text.Json;
using System.Text.Json.Serialization;

namespace Outbox;

public static class Vars
{
    public static class Errors
    {
        public const string SerializePayloadFailedMessage = "Failed to serialize outbox payload of type '{0}'.";
        public const string AssemblyQualifiedNameMissingMessage = "Type '{0}' has no assembly-qualified name.";
        public const string PayloadTypeNotFound = "outbox.payload-type-not-found";
        public const string HandlerNotRegistered = "outbox.handler-not-registered";
        public const string PayloadDeserializationFailed = "outbox.payload-deserialization-failed";
    }

    public static class Processor
    {
        public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan TransientFailureDelay = TimeSpan.FromSeconds(5);
    }

    public static class Json
    {
        public static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
    }
}
