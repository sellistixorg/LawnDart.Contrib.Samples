using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Models;
using LawnDart.Samples.EShop.WebApp.Placeholders;

namespace LawnDart.Samples.EShop.WebApp.Services;

/// <summary>
/// Checkout form. The address comes from Shopper Account Profile. Place Order submits the form.
/// </summary>
public sealed class CheckoutClient(
    IHttpClientFactory httpClientFactory,
    ShopperSession shopper,
    IBasketContents basket,
    ShopperAccountProfileClient profiles) : ICheckout
{
    /// <summary>
    /// Last four of the eShop checkout stand-in card. The full number and the security code are not sent.
    /// </summary>
    private const string StandInCardLastFour = "4444";

    /// <summary>
    /// Name on the eShop checkout stand-in card.
    /// </summary>
    private const string StandInCardHolder = "TESTUSER";

    /// <inheritdoc />
    public async Task<BasketCheckoutInfo> GetShippingAddressAsync()
    {
        ShopperAccountProfileView? profile = null;
        if (shopper.AccountId is Guid id)
            profile = await profiles.GetAsync(id).ConfigureAwait(false);

        return new BasketCheckoutInfo
        {
            Street = profile?.Street ?? "",
            City = profile?.City ?? "",
            State = profile?.State ?? "",
            Country = profile?.Country ?? "",
            ZipCode = profile?.ZipCode ?? ""
        };
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<BasketItem>> GetBasketItemsAsync(CancellationToken cancellationToken = default)
        => basket.GetBasketItemsAsync(cancellationToken);

    /// <summary>
    /// Submits the checkout form.
    /// </summary>
    /// <param name="info">Checkout form values.</param>
    /// <param name="cancellationToken">Token used to cancel the post.</param>
    /// <remarks>
    /// POST /api/ordering/place-order on the ordering HTTP client.
    /// Buyer id is the shopper account id from the authentication cookie.
    /// Order lines are the basket lines, with the price captured in the basket.
    /// The order id is <see cref="BasketCheckoutInfo.RequestId"/>.
    /// The page sets that id once, when checkout loads. An empty id here is only a safety net.
    /// The buyer user name is the account <c>UserName</c> from Shopper Account Profile, read at submit.
    /// The card payload is the card type, the cardholder name, the expiry, and the last four digits.
    /// This method does not clear the basket.
    /// A domain rejection throws <see cref="OrderingCommandRejectedException"/> with shopper-facing text.
    /// A basket read that fails throws that same exception with "Basket is temporarily unavailable."
    /// A concurrency conflict uses a fixed retry message and does not show the response body.
    /// A refused connection, a timeout, or an unsupported address on the order post still throws.
    /// This client keeps no shopper data.
    /// </remarks>
    public async Task PlaceOrderAsync(BasketCheckoutInfo info, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (!TryBuyerId(out var buyerId))
            throw new OrderingCommandRejectedException("The account is missing.");

        var items = await ReadBasketAsync(cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
            throw new OrderingCommandRejectedException("Your cart is empty.");

        var requestId = info.RequestId == Guid.Empty ? Guid.NewGuid() : info.RequestId;
        var profile = await profiles.GetAsync(buyerId, cancellationToken).ConfigureAwait(false);
        var userName = profile?.UserName?.Trim() ?? "";
        var expiration = info.CardExpiration ?? DateTime.UtcNow.AddYears(1);
        var cardTypeId = info.CardTypeId < 1 ? 1 : info.CardTypeId;

        await PostAsync(
            new
            {
                CommandId = requestId,
                OrderId = requestId,
                BuyerId = buyerId,
                UserName = userName,
                Street = info.Street ?? "",
                City = info.City ?? "",
                State = info.State,
                Country = info.Country ?? "",
                ZipCode = info.ZipCode ?? "",
                CardTypeId = cardTypeId,
                CardHolderName = StandInCardHolder,
                MaskedCardNumber = StandInCardLastFour,
                CardExpiration = expiration,
                OrderItems = items.Select(line => new
                {
                    line.ProductId,
                    line.ProductName,
                    line.UnitPrice,
                    Units = line.Quantity
                }).ToArray()
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<BasketItem>> ReadBasketAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await basket.GetBasketItemsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new OrderingCommandRejectedException("Basket is temporarily unavailable.");
        }
        catch (NotSupportedException)
        {
            throw new OrderingCommandRejectedException("Basket is temporarily unavailable.");
        }
        catch (JsonException)
        {
            throw new OrderingCommandRejectedException("Basket is temporarily unavailable.");
        }
        catch (TaskCanceledException)
        {
            throw new OrderingCommandRejectedException("Basket is temporarily unavailable.");
        }
    }

    private bool TryBuyerId(out Guid buyerId)
    {
        if (shopper.AccountId is Guid id)
        {
            buyerId = id;
            return true;
        }

        buyerId = Guid.Empty;
        return false;
    }

    private async Task PostAsync<T>(T body, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/ordering/place-order",
            body,
            cancellationToken).ConfigureAwait(false);
        ShopperSessionEndedException.ThrowIfEnded(response);

        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new OrderingCommandRejectedException("The order could not be saved. Please try again.");

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var message = await ReadRejectionAsync(response, cancellationToken).ConfigureAwait(false);
            throw new OrderingCommandRejectedException(message);
        }

        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> ReadRejectionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String)
            {
                return MapRejection(title.GetString());
            }
        }
        catch (JsonException)
        {
            // An unreadable body still uses the generic rejection.
        }
        catch (NotSupportedException)
        {
            // An unreadable body still uses the generic rejection.
        }

        return GenericRejection;
    }

    private const string GenericRejection = "That order could not be placed.";

    private static string MapRejection(string? code) => code switch
    {
        "ORDER_ITEMS_EMPTY" => "Your cart is empty.",
        "ORDER_ADDRESS_REQUIRED" => "A shipping address is required.",
        "ORDER_BUYER_ID_REQUIRED" => "The account is missing.",
        "ORDER_USER_NAME_REQUIRED" => "A buyer name is required.",
        "ORDER_CARD_TYPE_REQUIRED" => "Card details are incomplete.",
        "ORDER_CARD_HOLDER_REQUIRED" => "Card details are incomplete.",
        "ORDER_CARD_LAST_FOUR_REQUIRED" => "Card details are incomplete.",
        "ORDER_CARD_NUMBER_REJECTED" => "Card details are incomplete.",
        "ORDER_CARD_EXPIRATION_REQUIRED" => "Card details are incomplete.",
        "ORDER_ITEM_PRODUCT_REQUIRED" => "That order could not be placed.",
        "ORDER_ITEM_NAME_REQUIRED" => "That order could not be placed.",
        "ORDER_ITEM_PRICE_INVALID" => "That order could not be placed.",
        "ORDER_ITEM_UNITS_INVALID" => "That order could not be placed.",
        _ => GenericRejection
    };

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient(EShopStorefrontClients.Ordering);
        if (client.BaseAddress is null)
            throw new NotSupportedException("The ordering client has no base address.");

        return client;
    }
}
