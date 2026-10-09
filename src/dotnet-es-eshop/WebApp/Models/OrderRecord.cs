namespace LawnDart.Samples.EShop.WebApp.Models;

/// <summary>
/// One row on the orders list page.
/// </summary>
/// <param name="OrderNumber">Order id.</param>
/// <param name="Date">When placed.</param>
/// <param name="Status">Submitted|AwaitingValidation|StockConfirmed|Paid|Shipped|Cancelled.</param>
/// <param name="Total">Sum of unit price times units on the place event.</param>
public record OrderRecord(
    Guid OrderNumber,
    DateTime Date,
    string Status,
    decimal Total);
