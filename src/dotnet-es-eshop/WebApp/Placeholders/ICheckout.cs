using LawnDart.Samples.EShop.WebApp.Models;

namespace LawnDart.Samples.EShop.WebApp.Placeholders;

/// <summary>
/// Checkout form.
/// ShopperAccountProfile supplies the address from GET <see cref="ViewEndpoints.ShopperAccountProfile"/>.
/// The response DTO is ShopperAccountProfileView. That call uses the identity HTTP client.
/// Place Order posts the submit to POST /api/ordering/place-order. BasketContents supplies the bag check.
/// </summary>
public interface ICheckout
{
    /// <summary>
    /// Loads the default shipping address from Shopper Account Profile.
    /// </summary>
    /// <returns>Address fields from the signed-in shopper. Empty strings when nobody is signed in.</returns>
    Task<BasketCheckoutInfo> GetShippingAddressAsync();

    /// <summary>
    /// Bag lines for the checkout check.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The lines from Basket Contents. Empty when the shopper is signed out.</returns>
    Task<IReadOnlyCollection<BasketItem>> GetBasketItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits the checkout form to Place Order.
    /// </summary>
    /// <param name="info">Checkout form values.</param>
    /// <param name="cancellationToken">Token used to cancel the post.</param>
    /// <remarks>
    /// POST /api/ordering/place-order. Buyer id is the shopper account id from the authentication cookie.
    /// The basket is not cleared here.
    /// </remarks>
    Task PlaceOrderAsync(BasketCheckoutInfo info, CancellationToken cancellationToken = default);
}
