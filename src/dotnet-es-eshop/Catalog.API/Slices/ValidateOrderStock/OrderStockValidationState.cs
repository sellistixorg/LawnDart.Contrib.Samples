using LawnDart;

namespace LawnDart.Samples.EShop.Catalog.API.Slices.ValidateOrderStock;

/// <summary>
/// Decision state for one order's stock check.
/// </summary>
public sealed class OrderStockValidationState : IState
{
    /// <summary>
    /// Order being validated.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// True after this order has a stock result.
    /// </summary>
    public bool Checked { get; set; }

    /// <summary>
    /// True when the result was Order Stock Confirmed.
    /// </summary>
    public bool Confirmed { get; set; }
}
