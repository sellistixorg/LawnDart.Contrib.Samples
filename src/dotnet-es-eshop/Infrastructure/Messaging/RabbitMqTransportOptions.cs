namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Settings for <see cref="RabbitMqMessageTransport"/>.
/// </summary>
public sealed class RabbitMqTransportOptions
{
    public RabbitMqTransportOptions(string subscriptionClient, string? exchangeName = null)
    {
        EShopRabbitMq.ValidateSubscriptionClient(subscriptionClient);
        SubscriptionClient = subscriptionClient;
        ExchangeName = string.IsNullOrWhiteSpace(exchangeName)
            ? EShopRabbitMq.ExchangeName
            : exchangeName;
    }

    public string SubscriptionClient { get; }

    public string ExchangeName { get; }

    public string QueueName => EShopRabbitMq.QueueName(SubscriptionClient);
}
