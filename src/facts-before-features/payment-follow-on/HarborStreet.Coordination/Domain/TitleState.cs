using LawnDart;

namespace HarborStreet.Coordination.Domain;

public class TitleState : IState
{
    public Guid TitleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AvailableUnits { get; set; }
    public int ReservedUnits { get; set; }
    public bool Exists => TitleId != Guid.Empty;
}
