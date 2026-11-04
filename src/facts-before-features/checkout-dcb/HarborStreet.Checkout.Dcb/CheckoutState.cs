using LawnDart;

namespace HarborStreet.Checkout.Dcb;

public class CheckoutState : IState
{
    public Guid TitleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsScarce { get; set; }
    public int AvailableUnits { get; set; }
    public int ReservedUnits { get; set; }
    public bool Exists => TitleId != Guid.Empty;
}
