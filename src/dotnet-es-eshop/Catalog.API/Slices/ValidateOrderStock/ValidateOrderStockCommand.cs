using System.Text.Json.Serialization;
using LawnDart;
using ConfirmedLine = LawnDart.Samples.EShop.Shared.Events.ConfirmedOrderStockItem;
using RequestedOrderStockItem = LawnDart.Samples.EShop.Shared.Events.OrderStockItem;

namespace LawnDart.Samples.EShop.Catalog.Commands;

/// <summary>
/// Checks AvailableStock for each order line; confirms or rejects the order stock check.
/// </summary>
/// <remarks>
/// POST /api/catalog/validate-order-stock.
/// </remarks>
/// <param name="CommandId">Command identifier</param>
/// <param name="OrderId">Order being validated</param>
/// <param name="OrderStockItems">Product id + units requested</param>
public sealed record ValidateOrderStockCommand(
    Guid CommandId,
    Guid OrderId,
    IReadOnlyList<RequestedOrderStockItem> OrderStockItems) : ICommand
{
    /// <summary>
    /// Command identifier
    /// </summary>
    public Guid Id => CommandId;

    /// <summary>
    /// Per-line hasStock flags for the products after units are added.
    /// </summary>
    /// <remarks>
    /// The handler sets this before the result is stored. The HTTP body does not include it.
    /// </remarks>
    [JsonIgnore]
    public IReadOnlyList<ConfirmedLine>? ConfirmedOrderStockItems { get; init; }
}
