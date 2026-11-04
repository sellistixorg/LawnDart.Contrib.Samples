namespace HarborStreet.Projections.Views;

/// <summary>Stock dashboard row — available vs reserved for a title.</summary>
public sealed class StockDashboardRow
{
    public Guid TitleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Available { get; set; }
    public int Reserved { get; set; }
    public bool IsScarce { get; set; }
}
