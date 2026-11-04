using LawnDart;

namespace HarborStreet.EndToEnd.Domain;

public class OrderState : IState
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid TitleId { get; set; }
    public int Quantity { get; set; }
    public bool IsPlaced { get; set; }
    public bool IsPaid { get; set; }
    public bool PaymentFailed { get; set; }
    public bool IsCancelled { get; set; }
    public bool FulfillmentStarted { get; set; }
    public bool Exists => OrderId != Guid.Empty;
}
