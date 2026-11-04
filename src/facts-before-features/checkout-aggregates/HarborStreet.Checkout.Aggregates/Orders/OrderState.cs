using LawnDart;

namespace HarborStreet.Checkout.Aggregates.Orders;

public class OrderState : IState
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid TitleId { get; set; }
    public int Quantity { get; set; }
    public bool IsPlaced { get; set; }
    public bool IsCancelled { get; set; }
    public bool Exists => OrderId != Guid.Empty;
}
