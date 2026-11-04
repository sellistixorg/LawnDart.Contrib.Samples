namespace HarborStreet.Evolution.Views;

/// <summary>Order history v1 — no warehouse column.</summary>
public sealed class OrderHistoryItemV1
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid TitleId { get; set; }
    public int Quantity { get; set; }
    public DateTime PlacedAt { get; set; }
}

/// <summary>Order history v2 — warehouse + optional display name (from upcast or V2 writes).</summary>
public sealed class OrderHistoryItemV2
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid TitleId { get; set; }
    public int Quantity { get; set; }
    public DateTime PlacedAt { get; set; }
    public Guid WarehouseId { get; set; }
    public string? CustomerDisplayName { get; set; }
    public bool WarehouseKnown => WarehouseId != Guid.Empty;
}
