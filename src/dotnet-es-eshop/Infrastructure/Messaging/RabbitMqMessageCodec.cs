using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LawnDart.Messaging;
using RabbitMQ.Client;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// JSON body plus AMQP properties for one <see cref="IMessageTransport"/> delivery.
/// </summary>
public static class RabbitMqMessageCodec
{
    public const string ContentType = "application/json";
    public const string TransportType = "RabbitMQ";

    private const string CausationHeader = "causation-id";
    private const string TenantHeader = "tenant-id";
    private const string UserHeader = "user-id";
    private const string EnqueuedHeader = "enqueued-at";
    private const string HeaderPrefix = "hdr.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static byte[] SerializeBody<TMessage>(TMessage message)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), JsonOptions);
    }

    public static TMessage DeserializeBody<TMessage>(ReadOnlyMemory<byte> body)
        where TMessage : class
    {
        var message = JsonSerializer.Deserialize<TMessage>(body.Span, JsonOptions);
        if (message is null)
        {
            throw new InvalidOperationException(
                $"RabbitMQ payload did not deserialize to {typeof(TMessage).Name}.");
        }

        return message;
    }

    public static BasicProperties ToProperties(MessageContext context, string routingKey, string? appId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(context.CausationId))
            headers[CausationHeader] = context.CausationId;
        if (!string.IsNullOrWhiteSpace(context.TenantId))
            headers[TenantHeader] = context.TenantId;
        if (!string.IsNullOrWhiteSpace(context.UserId))
            headers[UserHeader] = context.UserId;
        headers[EnqueuedHeader] = context.EnqueuedAt.ToString("O", CultureInfo.InvariantCulture);

        foreach (var pair in context.Headers)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;
            headers[HeaderPrefix + pair.Key] = pair.Value;
        }

        return new BasicProperties
        {
            ContentType = ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = context.MessageId,
            CorrelationId = context.CorrelationId,
            Type = routingKey,
            AppId = appId,
            Headers = headers
        };
    }

    public static MessageContext ToContext(IReadOnlyBasicProperties? properties)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var source = properties?.Headers;
        if (source is not null)
        {
            foreach (var pair in source)
            {
                if (pair.Key.StartsWith(HeaderPrefix, StringComparison.Ordinal) && pair.Key.Length > HeaderPrefix.Length)
                {
                    var value = ReadString(pair.Value);
                    if (value is not null)
                        headers[pair.Key[HeaderPrefix.Length..]] = value;
                }
            }
        }

        return new MessageContext
        {
            MessageId = properties?.MessageId,
            CorrelationId = properties?.CorrelationId,
            CausationId = ReadHeader(source, CausationHeader),
            TenantId = ReadHeader(source, TenantHeader),
            UserId = ReadHeader(source, UserHeader),
            EnqueuedAt = ReadEnqueuedAt(source),
            TransportType = TransportType,
            Headers = headers
        };
    }

    private static DateTimeOffset ReadEnqueuedAt(IDictionary<string, object?>? headers)
    {
        var text = ReadHeader(headers, EnqueuedHeader);
        if (text is not null &&
            DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow;
    }

    private static string? ReadHeader(IDictionary<string, object?>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var value))
            return null;
        return ReadString(value);
    }

    private static string? ReadString(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            _ => value.ToString()
        };
    }
}
