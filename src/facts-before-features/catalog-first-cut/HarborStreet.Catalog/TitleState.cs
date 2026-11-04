using LawnDart;

namespace HarborStreet.Catalog;

public class TitleState : IState
{
    public Guid TitleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AvailableUnits { get; set; }
    public bool IsScarce { get; set; }
    public bool Exists => TitleId != Guid.Empty;
}
