using System.Text.RegularExpressions;
using LawnDart.EventStore;

namespace LawnDart.Samples.EShop.Infrastructure;

/// <summary>
/// Names for the sample RabbitMQ adapter. LawnDart product packages do not ship this transport.
/// </summary>
public static partial class EShopRabbitMq
{
    /// <summary>
    /// Aspire connection name from <c>AddRabbitMQ("eventbus")</c>.
    /// </summary>
    public const string ConnectionName = "eventbus";

    /// <summary>
    /// Topic exchange shared by eShop services.
    /// </summary>
    public const string ExchangeName = "eshop.integration";

    public static string QueueName(string subscriptionClient)
    {
        ValidateSubscriptionClient(subscriptionClient);
        return "eshop." + subscriptionClient;
    }

    /// <summary>
    /// Routing key for a message type. Event types use the <c>EventTypeName</c> token.
    /// </summary>
    public static string RoutingKey(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        var token = EventTypeCatalog.TryGetDeclaredName(messageType);
        if (!string.IsNullOrWhiteSpace(token))
            return token;

        var name = messageType.FullName ?? messageType.Name;
        return name.Replace('+', '.');
    }

    public static void ValidateSubscriptionClient(string subscriptionClient)
    {
        if (string.IsNullOrWhiteSpace(subscriptionClient) || !ClientName().IsMatch(subscriptionClient))
        {
            throw new ArgumentException(
                "Subscription client must be 1 to 64 characters of lowercase letters, digits, and hyphens.",
                nameof(subscriptionClient));
        }
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex ClientName();
}
