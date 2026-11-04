namespace HarborStreet.Projections.Views;

/// <summary>Order history entry — commitment facts for staff/customer.</summary>
public sealed class OrderHistoryItem
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid TitleId { get; set; }
    public int Quantity { get; set; }
    public DateTime PlacedAt { get; set; }
}
